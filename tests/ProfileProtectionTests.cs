using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace ZX6DisplayControl.Tests {
 public static class ProfileProtectionTests {
  [Test] public static void Review_LegacyWhitespaceNamesPreserveIdentityAndActiveProfile() {
   using(var dir=new TempDirectory()) {
    var legacy=AppSettings.Defaults();legacy.PresetLibraryVersion=1;
    foreach(var p in legacy.Profiles)p.BuiltInId=null;
    legacy.Profiles.Add(new Profile{Name=" Personal ",Cpu=new ChannelSettings{TemperatureId="TCPUDIO"}});
    legacy.Profiles.Add(new Profile{Name="Personal "});legacy.Profiles.Add(new Profile{Name="Personal"});legacy.ActiveProfileName=" Personal ";legacy.Validate();
    using(var stream=File.Create(Path.Combine(dir.Path,"settings.json")))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream,legacy);
    var store=new SettingsStore(dir.Path);var result=store.Load();Assert.True(result.Warning==null,"A valid legacy collection must not fall back");
    Assert.Equal(" Personal ",result.Settings.ActiveProfileName);Assert.Equal("TCPUDIO",result.Settings.ActiveProfile.Cpu.TemperatureId);
    Assert.True(new[]{" Personal ","Personal ","Personal"}.All(name=>result.Settings.Profiles.Any(p=>p.Name==name)));Assert.Equal(11,result.Settings.Profiles.Count);
    store.Save(result.Settings);Assert.Equal(" Personal ",store.Load().Settings.ActiveProfileName);
   }
  }
  [Test] public static void Rework_BuiltInsRejectRenameAndDeletion() {
   var settings=AppSettings.Defaults();
   Assert.Throws<InvalidOperationException>(()=>settings.Rename("Classic","Changed"));
   Assert.Throws<InvalidOperationException>(()=>settings.Delete("Activity"));
   Assert.Equal(8,settings.Profiles.Count);
  }
  [Test] public static void Rework_ModifiedBuiltInCannotBePersisted() {
   using(var dir=new TempDirectory()) {
    var settings=AppSettings.Defaults();settings.ActiveProfile.Cpu.Animation.FixedFps=4;
    Assert.Throws<InvalidDataException>(()=>new SettingsStore(dir.Path).Save(settings));
    Assert.True(!File.Exists(Path.Combine(dir.Path,"settings.json")));
   }
  }
  [Test] public static void Rework_LegacyCustomizationSurvivesAsActiveCustomCopy() {
   using(var dir=new TempDirectory()) {
    var legacy=AppSettings.Defaults();legacy.PresetLibraryVersion=1;legacy.ActiveProfileName="Activity";
    legacy.ActiveProfile.Cpu.TemperatureId="TCPUDIO";legacy.ActiveProfile.Cpu.Animation.Mode=AnimationMode.SensorLevel;
    legacy.Add(new Profile{Name="Activity (custom)"});
    using(var stream=File.Create(Path.Combine(dir.Path,"settings.json")))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream,legacy);
    var store=new SettingsStore(dir.Path);var result=store.Load();
    Assert.True(result.Warning==null,"Migration must not fall back to defaults");
    Assert.Equal("Activity (custom) 2",result.Settings.ActiveProfileName);
    Assert.Equal("TCPUDIO",result.Settings.ActiveProfile.Cpu.TemperatureId);
    Assert.Equal(AnimationMode.SensorLevel,result.Settings.ActiveProfile.Cpu.Animation.Mode);
    Assert.Equal(AnimationMode.Cycle,result.Settings.Profiles.Single(p=>p.Name=="Activity").Cpu.Animation.Mode);
    Assert.Equal(10,result.Settings.Profiles.Count);
    store.Save(result.Settings);Assert.Equal(10,store.Load().Settings.Profiles.Count);
   }
  }
  [Test] public static void Rework_ImportedPresetBecomesEditablePersonalProfile() {
   using(var dir=new TempDirectory()) {
    var store=new SettingsStore(dir.Path);var settings=AppSettings.Defaults();
    string file=Path.Combine(dir.Path,"profile.json");store.ExportProfile(settings.ActiveProfile,file);
    var imported=store.ImportProfile(file);imported.Name="Imported";settings.Add(imported);
    settings.Rename("Imported","My copy");settings.Delete("My copy");Assert.Equal(8,settings.Profiles.Count);
   }
  }
 }
}
