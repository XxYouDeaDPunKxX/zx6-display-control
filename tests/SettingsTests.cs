using System;
using System.IO;
using System.Linq;
namespace ZX6DisplayControl.Tests {
 public static class SettingsTests {
  [Test] public static void Settings_AtomicBackupAndCorruption() {
   using(var dir=new TempDirectory()) {
    var store=new SettingsStore(dir.Path);var settings=AppSettings.Defaults();store.Save(settings);settings.ActiveProfileName="Random";store.Save(settings);
    Assert.Equal("Random",store.Load().Settings.ActiveProfileName);
    File.WriteAllText(Path.Combine(dir.Path,"settings.json"),"broken");var recovered=store.Load();Assert.True(recovered.UsedBackup);Assert.Equal("Classic",recovered.Settings.ActiveProfileName);Assert.Equal("broken",File.ReadAllText(Path.Combine(dir.Path,"settings.json")));
    store.Save(recovered.Settings);Assert.True(Directory.GetFiles(dir.Path,"settings.corrupt-*.json").Length==1);Assert.Equal("Classic",store.Load().Settings.ActiveProfileName);
    File.WriteAllText(Path.Combine(dir.Path,"settings.json"),"bad-main");File.WriteAllText(Path.Combine(dir.Path,"settings.previous.json"),"bad-backup");var fallback=store.Load();Assert.True(!string.IsNullOrEmpty(fallback.Warning));Assert.Equal(8,fallback.Settings.Profiles.Count);Assert.Equal("bad-main",File.ReadAllText(Path.Combine(dir.Path,"settings.json")));
   }
  }
  [Test] public static void Settings_ImportRejectsInvalidAndPreservesValues() {
   using(var dir=new TempDirectory()) {
    var store=new SettingsStore(dir.Path);string path=Path.Combine(dir.Path,"profile.json");var profile=AppSettings.Defaults().Profiles[1];profile.Cpu.Paused=true;profile.Gpu.Animation.Sequence=SequenceKind.Reverse;
    store.ExportProfile(profile,path);var result=store.ImportProfile(path);Assert.Equal("SCPUUTI",result.Cpu.Animation.SensorId);Assert.True(result.Cpu.Paused);Assert.Equal(SequenceKind.Reverse,result.Gpu.Animation.Sequence);
    string valid=File.ReadAllText(path);File.WriteAllText(path,valid.Replace("\"SchemaVersion\":1","\"SchemaVersion\":2"));Assert.Throws<InvalidDataException>(()=>store.ImportProfile(path));
    File.WriteAllText(path,valid.Replace("\"MaxFps\":4","\"MaxFps\":8"));Assert.Throws<InvalidDataException>(()=>store.ImportProfile(path));
    File.WriteAllText(path,valid.Replace("\"InputMax\":100","\"InputMax\":0"));Assert.Throws<InvalidDataException>(()=>store.ImportProfile(path));
    File.WriteAllText(path,valid.Replace("\"Mode\":0","\"Mode\":99"));Assert.Throws<InvalidDataException>(()=>store.ImportProfile(path));
    File.WriteAllText(path,new string(' ',1048577));Assert.Throws<InvalidDataException>(()=>store.ImportProfile(path));
    profile.Cpu.Animation.MinFps=double.NaN;Assert.Throws<InvalidDataException>(()=>store.ExportProfile(profile,path));
   }
  }
  [Test] public static void Settings_ProfileNamesAndDefaults() {
   var s=AppSettings.Defaults();Assert.True(!s.StartWithWindows);Assert.Equal("TCPU",s.ActiveProfile.Cpu.TemperatureId);Assert.Equal(2.0,s.ActiveProfile.Cpu.Animation.FixedFps);
   var clone=s.ActiveProfile.Copy();clone.Name="New";s.Add(clone);clone.Cpu.TemperatureId="MISSING";Assert.Equal("TCPU",s.Profiles.Last().Cpu.TemperatureId);
   Assert.Throws<ArgumentException>(()=>s.Add(new Profile{Name="Classic"}));Assert.Throws<ArgumentException>(()=>s.Rename("New"," "));
   s.ActiveProfileName="New";s.Rename("New","Principale");Assert.Equal("Principale",s.ActiveProfileName);
   s.Delete("Principale");Assert.Equal("Classic",s.ActiveProfileName);Assert.Equal(8,s.Profiles.Count);
  }
 }
}
