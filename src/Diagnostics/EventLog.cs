using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace ZX6DisplayControl {
 public sealed class EventLog {
  private readonly string directory;private readonly object gate=new object(),statusGate=new object();
  private readonly Queue<string> recent=new Queue<string>();private string lastError;private long revision;
  public string LastError {get{lock(statusGate)return lastError;}}
  public long Revision {get{lock(statusGate)return revision;}}
  public string[] Recent() {lock(statusGate)return recent.ToArray();}
  private void SetError(string value){lock(statusGate){if(lastError!=value){lastError=value;revision++;}}}
  public EventLog(string directory) {this.directory=Path.GetFullPath(directory);}
  public void Write(string eventName,string message) {Write(eventName,message,null);}
  public Task WriteAsync(string eventName,string message,Exception error=null) {return Task.Run(()=>Write(eventName,message,error));}
  public void Write(string eventName,string message,Exception error) {
   string content=(eventName??"")+" | "+(message??"");if(content.Length>4096)content=content.Substring(0,4096)+"…";
   string time=DateTimeOffset.Now.ToString("o");
   string summary=content.Length>512?content.Substring(0,512)+"…":content;
   lock(statusGate){recent.Enqueue(time+" "+summary.Replace('\r',' ').Replace('\n',' '));while(recent.Count>200)recent.Dequeue();revision++;}
   lock(gate) try {
    Directory.CreateDirectory(directory);string current=Path.Combine(directory,"events.log"),previous=Path.Combine(directory,"events.previous.log");
    if(error!=null)content+=" | "+error;if(content.Length>8192)content=content.Substring(0,8192)+"…";
    var bytes=Encoding.UTF8.GetBytes(time+" "+content.Replace('\r',' ').Replace('\n',' ')+Environment.NewLine);
    if(File.Exists(current) && new FileInfo(current).Length+bytes.Length>1048576) {if(File.Exists(previous)) File.Delete(previous);File.Move(current,previous);}
    using(var stream=new FileStream(current,FileMode.Append,FileAccess.Write,FileShare.Read)) stream.Write(bytes,0,bytes.Length);
    SetError(null);
   } catch(IOException e) {SetError(e.Message);} catch(UnauthorizedAccessException e) {SetError(e.Message);}
  }
  public void Export(string destination,SessionState state,AppSettings settings,string appVersion) {
   string report=BuildReport(state,settings,appVersion);File.WriteAllText(destination,report,new UTF8Encoding(false));
  }
  public void Export(TextWriter destination,SessionState state,AppSettings settings,string appVersion) {destination.Write(BuildReport(state,settings,appVersion));}
  private string BuildReport(SessionState state,AppSettings settings,string appVersion) {
   lock(gate) {
    var text=new StringBuilder();text.AppendLine("Z-X6 Display Control "+appVersion);text.AppendLine("Exported: "+DateTimeOffset.Now.ToString("o"));
    text.AppendLine("Supported model: Z-X6");text.AppendLine("USB descriptor: Turing / UsbMonitor");text.AppendLine("USB: 1A86:5722 / USB35INCHIPSV2");
    if(state!=null){text.AppendLine("Port: "+state.PortName);text.AppendLine("AIDA: "+state.AidaStatus+"; holder: "+state.DeviceStatus);text.AppendLine("Status: "+state.Error);text.AppendLine("Valid reads: "+state.ValidReads+"; read errors: "+state.ReadErrors+"; writes: "+state.Writes);}
    else text.AppendLine("Controller has not published a reading yet.");
    text.AppendLine("Active profile:");text.AppendLine(Encoding.UTF8.GetString(SettingsStore.Serialize(new ProfileDocument{SchemaVersion=1,Profile=settings.ActiveProfile.Copy()})));
    foreach(string name in new[]{"events.previous.log","events.log"}) {string path=Path.Combine(directory,name);if(File.Exists(path)){text.AppendLine(name);text.AppendLine(File.ReadAllText(path));}}
    text.AppendLine("Current-session events:");foreach(string entry in Recent())text.AppendLine(entry);
    if(LastError!=null)text.AppendLine("Log file error: "+LastError);return text.ToString();
   }
  }
 }
}
