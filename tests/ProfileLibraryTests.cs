using System;
using System.Linq;
using System.IO;
namespace ZX6DisplayControl.Tests {
 public static class ProfileLibraryTests {
  [Test] public static void ProfileLibrary_LegacyActiveNameUsesCaseInsensitiveIdentity() {
   var settings=AppSettings.Defaults();settings.PresetLibraryVersion=0;foreach(var p in settings.Profiles)p.BuiltInId=null;settings.Rename("Classic","Classico");settings.ActiveProfileName="classico";settings.Validate();ProfileLibrary.Upgrade(settings);settings.Validate();Assert.Equal("Classic",settings.ActiveProfile.Name);
  }
  [Test] public static void ProfileLibrary_StandardAndCreativeUseSupportedEffects() {
   var s=AppSettings.Defaults();Assert.Equal(8,s.Profiles.Count);s.Validate();
   Assert.True(new[]{"Classic","Activity","Thermal","Sensor levels","Random","Breathe","Counterflow","Split tempo"}.All(n=>s.Profiles.Any(p=>p.Name==n)));
   Assert.Equal(AnimationMode.SensorLevel,s.Profiles.Single(p=>p.Name=="Sensor levels").Cpu.Animation.Mode);
   var opposite=s.Profiles.Single(p=>p.Name=="Counterflow");Assert.Equal(SequenceKind.Forward,opposite.Cpu.Animation.Sequence);Assert.Equal(SequenceKind.Reverse,opposite.Gpu.Animation.Sequence);
   var split=s.Profiles.Single(p=>p.Name=="Split tempo");Assert.True(split.Cpu.Animation.FixedFps<split.Gpu.Animation.FixedFps);
  }
  [Test] public static void ProfileLibrary_UpgradePreservesExistingChoices() {
   using(var dir=new TempDirectory()) {
    var store=new SettingsStore(dir.Path);var old=AppSettings.Defaults();old.PresetLibraryVersion=0;foreach(var p in old.Profiles)p.BuiltInId=null;old.Profiles=old.Profiles.Take(4).ToList();old.ActiveProfileName=old.Profiles[1].Name;
    old.Rename("Classic","Classico");old.Rename("Activity","Attività");old.Rename("Thermal","Termico");old.Rename("Random","Casuale");
    old.ActiveProfile.Gpu.TemperatureId="TGPU1HOT";old.ActiveProfile.Cpu.Animation.Mode=AnimationMode.SensorLevel;store.Save(old);
    var path=Path.Combine(dir.Path,"settings.json");var json=File.ReadAllText(path);json=System.Text.RegularExpressions.Regex.Replace(json,"\"PresetLibraryVersion\":\\d+,?","");File.WriteAllText(path,json);
    var updated=store.Load().Settings;Assert.Equal("Activity (custom)",updated.ActiveProfileName);Assert.Equal("TGPU1HOT",updated.ActiveProfile.Gpu.TemperatureId);Assert.Equal(AnimationMode.SensorLevel,updated.ActiveProfile.Cpu.Animation.Mode);Assert.Equal(9,updated.Profiles.Count);
    store.Save(updated);Assert.Equal(9,store.Load().Settings.Profiles.Count);
   }
  }
 }
}
