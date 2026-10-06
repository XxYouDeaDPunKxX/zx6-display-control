using System;
using System.Collections.Generic;
using System.Linq;
namespace ZX6DisplayControl {
 public static class ProfileLibrary {
  public static List<Profile> Create() {
   var classic=new Profile{Name="Classic"};var activity=new Profile{Name="Activity"};var thermal=new Profile{Name="Thermal"};var random=new Profile{Name="Random"};
   activity.Cpu.Animation.RateMode=activity.Gpu.Animation.RateMode=AnimationRateMode.Sensor;
   activity.Cpu.Animation.SensorId="SCPUUTI";activity.Gpu.Animation.SensorId="SGPU1UTI";activity.Cpu.Animation.FilterSeconds=activity.Gpu.Animation.FilterSeconds=2;
   foreach(var channel in new[]{thermal.Cpu,thermal.Gpu}){channel.Animation.RateMode=AnimationRateMode.Sensor;channel.Animation.SensorId=channel.TemperatureId;channel.Animation.InputMin=30;channel.Animation.InputMax=80;channel.Animation.FilterSeconds=2;}
   random.Cpu.Animation.Sequence=random.Gpu.Animation.Sequence=SequenceKind.Random;
   var levels=activity.Copy();levels.Name="Sensor levels";levels.Cpu.Animation.Mode=levels.Gpu.Animation.Mode=AnimationMode.SensorLevel;
   var breathe=new Profile{Name="Breathe"};breathe.Cpu.Animation.Sequence=breathe.Gpu.Animation.Sequence=SequenceKind.PingPong;breathe.Cpu.Animation.FixedFps=breathe.Gpu.Animation.FixedFps=1;
   var counter=new Profile{Name="Counterflow"};counter.Gpu.Animation.Sequence=SequenceKind.Reverse;
   var split=new Profile{Name="Split tempo"};split.Cpu.Animation.FixedFps=1;split.Gpu.Animation.FixedFps=4;
   var library=new List<Profile>{classic,activity,thermal,random,levels,breathe,counter,split};
   foreach(var profile in library)profile.BuiltInId=profile.Name.ToLowerInvariant().Replace(' ','-');
   return library;
  }
  public static string Group(string name) {switch(name){case "Classic":case "Activity":case "Thermal":case "Sensor levels":return "Standard";case "Random":case "Breathe":case "Counterflow":case "Split tempo":return "Creative";default:return "Custom";}}
  public static string Group(Profile profile) {return profile.IsBuiltIn?"Built-in · "+Group(profile.Name):"My profiles";}
  public static string UniqueName(IEnumerable<Profile> profiles,string proposed) {var names=new HashSet<string>(profiles.Select(p=>p.Name),StringComparer.OrdinalIgnoreCase);string name=proposed;int suffix=2;while(names.Contains(name))name=proposed+" "+suffix++;return name;}
  public static string Description(string name) {
   switch(name){case "Classic":return "Both bars fill at a steady 2 fps.";case "Activity":return "Animation speed follows CPU and GPU usage independently.";case "Thermal":return "Animation speed follows each side's temperature, from 30 to 80 °C.";case "Sensor levels":return "Bars show CPU and GPU usage as levels from empty to full.";case "Random":return "Each bar jumps between random levels at 2 fps.";case "Breathe":return "Both bars slowly fill and empty at 1 fps.";case "Counterflow":return "CPU fills while GPU empties, both at 2 fps.";case "Split tempo":return "CPU fills slowly at 1 fps; GPU fills quickly at 4 fps.";default:return "Your saved temperature sources and animation settings.";}
  }
  public static void Upgrade(AppSettings settings) {
   if(settings.PresetLibraryVersion>=2)return;
   foreach(var profile in settings.Profiles)profile.BuiltInId=null;
   if(settings.PresetLibraryVersion<1) {
   var names=new Dictionary<string,string>{{"Classico","Classic"},{"Attività","Activity"},{"Attivita","Activity"},{"Termico","Thermal"},{"Casuale","Random"}};
   foreach(var pair in names) {
    var profile=settings.Profiles.FirstOrDefault(p=>p.Name==pair.Key);if(profile==null)continue;
    string name=pair.Value;int suffix=2;while(settings.Profiles.Any(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)))name=pair.Value+" "+suffix++;
    settings.Rename(pair.Key,name);
   }
   foreach(var preset in Create().Where(p=>new[]{"Sensor levels","Breathe","Counterflow","Split tempo"}.Contains(p.Name)))if(!settings.Profiles.Any(p=>string.Equals(p.Name,preset.Name,StringComparison.OrdinalIgnoreCase)))settings.Add(preset);
   }
   var canonical=Create();var custom=new List<Profile>();string active=settings.ActiveProfileName;
   var reserved=settings.Profiles.Concat(canonical).ToList();
   foreach(var previous in settings.Profiles) {
    var original=canonical.FirstOrDefault(p=>string.Equals(p.Name,previous.Name,StringComparison.OrdinalIgnoreCase));
    string target;
    if(original!=null && original.SameContent(previous))target=original.Name;
    else {
     target=original==null?previous.Name:UniqueName(reserved,original.Name+" (custom)");
     var copy=previous.Copy();copy.BuiltInId=null;copy.Name=target;custom.Add(copy);reserved.Add(copy);
    }
    if(string.Equals(settings.ActiveProfileName,previous.Name,StringComparison.OrdinalIgnoreCase))active=target;
   }
   settings.Profiles=canonical.Concat(custom).ToList();settings.ActiveProfileName=active;settings.PresetLibraryVersion=2;settings.Validate();
  }
 }
}
