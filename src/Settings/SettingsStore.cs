using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
namespace ZX6DisplayControl {
 public sealed class SettingsStore {
  private readonly string directory;
  public string DirectoryPath {get{return directory;}}
  private string MainPath {get{return Path.Combine(directory,"settings.json");}}
  private string BackupPath {get{return Path.Combine(directory,"settings.previous.json");}}
  public SettingsStore(string directory) {this.directory=Path.GetFullPath(directory);}
  public SettingsLoadResult Load() {
   string warning=null;
   if(File.Exists(MainPath)) {
    try {var value=Read<AppSettings>(MainPath);value.Validate();ProfileLibrary.Upgrade(value);return new SettingsLoadResult{Settings=value};}
    catch(Exception e) {if(!Recoverable(e))throw;warning="Settings could not be read. The original file has been retained.";}
   }
   if(File.Exists(BackupPath)) {
    try {var value=Read<AppSettings>(BackupPath);value.Validate();ProfileLibrary.Upgrade(value);return new SettingsLoadResult{Settings=value,UsedBackup=true,Warning="Previous settings restored. The original file has been retained."};}
    catch(Exception e) {if(!Recoverable(e))throw;warning="Settings and backup could not be read. Default profiles loaded; both files have been retained.";}
   }
   return new SettingsLoadResult{Settings=AppSettings.Defaults(),Warning=warning};
  }
  public void Save(AppSettings value) {
   value.Validate();byte[] bytes=Serialize(value);Directory.CreateDirectory(directory);
   string temporary=MainPath+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {
    File.WriteAllBytes(temporary,bytes);
    if(File.Exists(MainPath)) {
     bool valid=true;
     try {Read<AppSettings>(MainPath).Validate();} catch(Exception e) {if(!Recoverable(e))throw;valid=false;}
     if(valid) File.Replace(temporary,MainPath,BackupPath);
     else {File.Move(MainPath,Path.Combine(directory,"settings.corrupt-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json"));File.Move(temporary,MainPath);}
    } else File.Move(temporary,MainPath);
   } finally {if(File.Exists(temporary)) File.Delete(temporary);}
  }
  public Profile ImportProfile(string path) {
   var document=Read<ProfileDocument>(path);
   if(document==null || document.SchemaVersion!=1 || document.Profile==null || document.Profile.Validate().Count>0) throw new InvalidDataException("Invalid profile or unsupported profile version.");
    return document.Profile.PersonalCopy(document.Profile.Name);
  }
  public void ExportProfile(Profile value,string path) {
   if(value==null || value.Validate().Count>0) throw new InvalidDataException("Invalid profile.");
    File.WriteAllBytes(path,Serialize(new ProfileDocument{SchemaVersion=1,Profile=value.PersonalCopy(value.Name)}));
  }
  internal static byte[] Serialize<T>(T value) {
   using(var stream=new MemoryStream()) {new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);if(stream.Length>1048576) throw new InvalidDataException("Document exceeds the 1 MB limit.");return stream.ToArray();}
  }
  private static T Read<T>(string path) {
   try {
    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
     if(stream.Length<=0 || stream.Length>1048576) throw new InvalidDataException("Invalid document size (maximum 1 MB).");
     var value=(T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
     if(value==null) throw new InvalidDataException("Document is empty.");return value;
    }
   } catch(SerializationException e) {throw new InvalidDataException("Invalid JSON.",e);}
     catch(XmlException e) {throw new InvalidDataException("Invalid JSON.",e);}
  }
  private static bool Recoverable(Exception e) {return e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is SerializationException || e is ArgumentException;}
 }
}
