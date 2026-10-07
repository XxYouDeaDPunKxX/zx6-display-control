using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.IO;
namespace ZX6DisplayControl {
 [DataContract] public enum AppTheme {[EnumMember] System,[EnumMember] Light,[EnumMember] Dark}
 [DataContract] public sealed class Profile {
  [DataMember(IsRequired=true)] public string Name {get;set;}
  [DataMember(EmitDefaultValue=false)] public string BuiltInId {get;set;}
  public bool IsBuiltIn {get{return !string.IsNullOrEmpty(BuiltInId);}}
  [DataMember(IsRequired=true)] public ChannelSettings Cpu {get;set;}
  [DataMember(IsRequired=true)] public ChannelSettings Gpu {get;set;}
  public Profile() {Cpu=new ChannelSettings{TemperatureId="TCPU"};Gpu=new ChannelSettings{TemperatureId="TGPU1"};}
  public Profile Copy() {return new Profile{Name=Name,BuiltInId=BuiltInId,Cpu=Cpu==null?null:Cpu.Copy(),Gpu=Gpu==null?null:Gpu.Copy()};}
  public Profile PersonalCopy(string name) {var copy=Copy();copy.BuiltInId=null;copy.Name=name.Trim();return copy;}
  public bool SameContent(Profile other) {return other!=null && SameChannel(Cpu,other.Cpu) && SameChannel(Gpu,other.Gpu);}
  private static bool SameChannel(ChannelSettings a,ChannelSettings b) {return a!=null && b!=null && a.TemperatureId==b.TemperatureId && a.Paused==b.Paused && a.Animation!=null && a.Animation.Equivalent(b.Animation);}
  public IReadOnlyList<string> Validate() {
   var errors=new List<string>();if(string.IsNullOrWhiteSpace(Name)) errors.Add("Enter a profile name.");
   ValidateChannel(Cpu,"CPU",errors);ValidateChannel(Gpu,"GPU",errors);return errors.AsReadOnly();
  }
  private static void ValidateChannel(ChannelSettings channel,string label,List<string> errors) {
   if(channel==null || channel.Animation==null) {errors.Add(label+": configuration missing.");return;}
   if(string.IsNullOrWhiteSpace(channel.TemperatureId)) errors.Add(label+": choose a display temperature.");
   errors.AddRange(channel.Animation.Validate().Select(e=>label+": "+e));
  }
 }
 [DataContract] public sealed class AppSettings {
  [DataMember(IsRequired=true)] public int SchemaVersion {get;set;}
  [DataMember(IsRequired=true)] public List<Profile> Profiles {get;set;}
  [DataMember(IsRequired=true)] public string ActiveProfileName {get;set;}
  // Retained for existing settings files; holder discovery is automatic.
  [DataMember] public string SelectedInstanceId {get;set;}
  [DataMember] public bool DisplayEnabled {get;set;}
  [DataMember] public bool StartWithWindows {get;set;}
  [DataMember] public bool CloseToTrayExplained {get;set;}
  [DataMember] public bool CloseToTray {get;set;}
  [DataMember] public bool MinimizeToTray {get;set;}
  [DataMember] public bool CheckUpdatesOnStartup {get;set;}
  [DataMember] public AppTheme Theme {get;set;}
  [DataMember] public int PresetLibraryVersion {get;set;}
  [OnDeserializing] private void ReadDefaults(StreamingContext context) {CloseToTray=true;CheckUpdatesOnStartup=true;}
  public static AppSettings Defaults() {
   return new AppSettings{SchemaVersion=1,Profiles=ProfileLibrary.Create(),ActiveProfileName="Classic",DisplayEnabled=true,CloseToTray=true,CheckUpdatesOnStartup=true,PresetLibraryVersion=2};
  }
  public Profile ActiveProfile {get{return Profiles.Single(p=>string.Equals(p.Name,ActiveProfileName,StringComparison.OrdinalIgnoreCase));}}
  public AppSettings Copy() {return new AppSettings{SchemaVersion=SchemaVersion,Profiles=Profiles.Select(p=>p.Copy()).ToList(),ActiveProfileName=ActiveProfileName,SelectedInstanceId=SelectedInstanceId,DisplayEnabled=DisplayEnabled,StartWithWindows=StartWithWindows,CloseToTrayExplained=CloseToTrayExplained,CloseToTray=CloseToTray,MinimizeToTray=MinimizeToTray,CheckUpdatesOnStartup=CheckUpdatesOnStartup,Theme=Theme,PresetLibraryVersion=PresetLibraryVersion};}
  public void Validate() {
   if(!Enum.IsDefined(typeof(AppTheme),Theme))throw new InvalidDataException("Invalid appearance setting.");
   if(SchemaVersion!=1 || Profiles==null || Profiles.Count==0 || Profiles.Any(p=>p==null || p.Validate().Count>0) || Profiles.Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Profiles.Count || Profiles.Count(p=>string.Equals(p.Name,ActiveProfileName,StringComparison.OrdinalIgnoreCase))!=1) throw new InvalidDataException("Invalid settings or unsupported settings version.");
   if(PresetLibraryVersion>=2) {
    var library=ProfileLibrary.Create();
    if(Profiles.Count(p=>p.IsBuiltIn)!=library.Count || library.Any(p=>Profiles.Count(v=>v.BuiltInId==p.BuiltInId && v.Name==p.Name && v.SameContent(p))!=1))throw new InvalidDataException("Built-in profiles cannot be changed. Save a personal copy instead.");
   }
  }
  public void Add(Profile profile) {if(profile==null || profile.Validate().Count>0) throw new ArgumentException("Invalid profile.");EnsureAvailable(profile.Name,null);Profiles.Add(profile.PersonalCopy(profile.Name));}
  public void Rename(string name,string newName) {
   var profile=Profiles.Single(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));if(profile.IsBuiltIn)throw new InvalidOperationException("Built-in profiles cannot be renamed. Create a personal copy instead.");EnsureAvailable(newName,profile.Name);profile.Name=newName.Trim();if(string.Equals(ActiveProfileName,name,StringComparison.OrdinalIgnoreCase)) ActiveProfileName=profile.Name;
  }
  public void Delete(string name) {
   if(Profiles.Count<=1) throw new InvalidOperationException("Keep at least one profile.");
   var profile=Profiles.Single(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));if(profile.IsBuiltIn)throw new InvalidOperationException("Built-in profiles cannot be deleted.");Profiles.Remove(profile);if(string.Equals(ActiveProfileName,name,StringComparison.OrdinalIgnoreCase)) ActiveProfileName=Profiles[0].Name;
  }
  private void EnsureAvailable(string name,string except) {
   if(string.IsNullOrWhiteSpace(name) || Profiles.Any(p=>p.Name!=except && string.Equals(p.Name,name.Trim(),StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Enter a name that is not already used by another profile.");
  }
 }
 [DataContract] public sealed class ProfileDocument {
  [DataMember(IsRequired=true)] public int SchemaVersion {get;set;}
  [DataMember(IsRequired=true)] public Profile Profile {get;set;}
 }
 public sealed class SettingsLoadResult {
  public AppSettings Settings {get;internal set;} public string Warning {get;internal set;} public bool UsedBackup {get;internal set;} public bool FirstRun {get;internal set;}
 }
}
