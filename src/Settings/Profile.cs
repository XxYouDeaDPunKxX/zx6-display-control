using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.IO;
namespace ZX6DisplayControl {
 [DataContract] public enum AppTheme {[EnumMember] System,[EnumMember] Light,[EnumMember] Dark}
 [DataContract] public sealed class Profile {
  [DataMember(IsRequired=true)] public string Name {get;set;}
  [DataMember(IsRequired=true)] public ChannelSettings Cpu {get;set;}
  [DataMember(IsRequired=true)] public ChannelSettings Gpu {get;set;}
  public Profile() {Cpu=new ChannelSettings{TemperatureId="TCPU"};Gpu=new ChannelSettings{TemperatureId="TGPU1"};}
  public Profile Copy() {return new Profile{Name=Name,Cpu=Cpu==null?null:Cpu.Copy(),Gpu=Gpu==null?null:Gpu.Copy()};}
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
  [DataMember] public AppTheme Theme {get;set;}
  [DataMember] public int PresetLibraryVersion {get;set;}
  [OnDeserializing] private void ReadDefaults(StreamingContext context) {CloseToTray=true;}
  public static AppSettings Defaults() {
   return new AppSettings{SchemaVersion=1,Profiles=ProfileLibrary.Create(),ActiveProfileName="Classic",DisplayEnabled=true,CloseToTray=true,PresetLibraryVersion=1};
  }
  public Profile ActiveProfile {get{return Profiles.Single(p=>string.Equals(p.Name,ActiveProfileName,StringComparison.OrdinalIgnoreCase));}}
  public AppSettings Copy() {return new AppSettings{SchemaVersion=SchemaVersion,Profiles=Profiles.Select(p=>p.Copy()).ToList(),ActiveProfileName=ActiveProfileName,SelectedInstanceId=SelectedInstanceId,DisplayEnabled=DisplayEnabled,StartWithWindows=StartWithWindows,CloseToTrayExplained=CloseToTrayExplained,CloseToTray=CloseToTray,MinimizeToTray=MinimizeToTray,Theme=Theme,PresetLibraryVersion=PresetLibraryVersion};}
  public void Validate() {
   if(!Enum.IsDefined(typeof(AppTheme),Theme))throw new InvalidDataException("Invalid appearance setting.");
   if(SchemaVersion!=1 || Profiles==null || Profiles.Count==0 || Profiles.Any(p=>p==null || p.Validate().Count>0) || Profiles.Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Profiles.Count || Profiles.Count(p=>string.Equals(p.Name,ActiveProfileName,StringComparison.OrdinalIgnoreCase))!=1) throw new InvalidDataException("Invalid settings or unsupported settings version.");
  }
  public void Add(Profile profile) {if(profile==null || profile.Validate().Count>0) throw new ArgumentException("Invalid profile.");EnsureAvailable(profile.Name,null);Profiles.Add(profile.Copy());}
  public void Rename(string name,string newName) {
   EnsureAvailable(newName,name);var profile=Profiles.Single(p=>p.Name==name);profile.Name=newName.Trim();if(string.Equals(ActiveProfileName,name,StringComparison.OrdinalIgnoreCase)) ActiveProfileName=profile.Name;
  }
  public void Delete(string name) {
   if(Profiles.Count<=1) throw new InvalidOperationException("Keep at least one profile.");
   var profile=Profiles.Single(p=>p.Name==name);Profiles.Remove(profile);if(ActiveProfileName==name) ActiveProfileName=Profiles[0].Name;
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
  public AppSettings Settings {get;internal set;} public string Warning {get;internal set;} public bool UsedBackup {get;internal set;}
 }
}
