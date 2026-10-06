using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace ZX6DisplayControl.Tests {
 public static class ReviewFollowupTests {
  [Test] public static void Followup_BrowsingLegacyPausedFixedProfileIsNotAnEdit() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var settings=AppSettings.Defaults();var personal=new Profile{Name="Paused fixed"};personal.Cpu.TemperatureId="TCPU";personal.Gpu.TemperatureId="TGPU1";
    personal.Cpu.Animation.Mode=AnimationMode.Fixed;personal.Cpu.Animation.FixedFrame=5;personal.Cpu.Paused=true;settings.Add(personal);
    var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(settings,new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiTestPump.Wait(form.SelectProfile(personal.Name,()=>DialogResult.Cancel));Assert.True(!form.Text.Contains("unsaved"),"A stored pause flag is dormant in Fixed mode, not an edit");
     Assert.Equal("Use profile",UiBindingTests.Find<Button>(form,"ApplyChanges").Text);
     int prompts=0;form.RequestExit(()=>{prompts++;return DialogResult.Cancel;});Assert.Equal(0,prompts);Assert.Equal(0,controller.Applied);
    }
   }
  }
  [Test] public static void Followup_AlreadyMigratedMainPreservesRemainingLegacyBackup() {
   using(var dir=new TempDirectory()) {
    var legacy=AppSettings.Defaults();legacy.PresetLibraryVersion=1;legacy.ActiveProfile.Cpu.TemperatureId="TCPUDIO";
    string previous=Path.Combine(dir.Path,"settings.previous.json");using(var stream=File.Create(previous))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream,legacy);
    var bytes=File.ReadAllBytes(previous);using(var stream=File.Create(Path.Combine(dir.Path,"settings.json")))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream,AppSettings.Defaults());
    var store=new SettingsStore(dir.Path);store.Save(store.Load().Settings);
    string backup=Path.Combine(dir.Path,"settings.before-profile-library-v2.json");Assert.True(File.Exists(backup),"Retain a migration source left by an earlier build before rotating it away");Assert.True(bytes.SequenceEqual(File.ReadAllBytes(backup)));
   }
  }
  [Test] public static void Followup_OverRangeTemperatureKeepsDisplayOnAndReportsRealValue() {
   using(var rig=new SessionRig())using(var dir=new TempDirectory()) {
    rig.Reader.Result=AidaParserTests.Parse("<temp><id>TCPU</id><value>105.5</value></temp><temp><id>TGPU1</id><value>99.9</value></temp>");
    rig.At(0);rig.At(100);rig.At(6100);
    var state=rig.Session.State;Assert.True(state.Connected && !state.DisplayOff,"An over-range temperature must not blank the display");
    Assert.Equal(99,state.CpuNumber.Value);Assert.Equal(99,state.GpuNumber.Value);Assert.Equal("TemperatureLimited",state.AidaStatus);
    Assert.True(state.Error.Contains("105.5") && state.Error.Contains("99.9"));Assert.Equal(105.5,state.Snapshot.Values["TCPU"].Number.Value);
    var packet=rig.Serial.Packets.Last(p=>p[5]==169);Assert.Equal((byte)9,packet[6]);Assert.Equal((byte)9,packet[7]);
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=state},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var preview=UiBindingTests.Find<HolderPreview>(form,"HolderPreview");Assert.Equal(99,preview.CpuTemperature.Value);
     Assert.True(UiBindingTests.Find<Button>(form,"AidaStatus").Text.Contains("99"));
    }
    rig.Reader.Result=AidaParserTests.Parse("<temp><id>TCPU</id><value>48</value></temp><temp><id>TGPU1</id><value>42</value></temp>");rig.At(6600);
    Assert.Equal("Ready",rig.Session.State.AidaStatus);Assert.Equal(48,rig.Session.State.CpuNumber.Value);Assert.True(!rig.Session.State.Error.Contains("105.5"));
    rig.Reader.Result=AidaParserTests.Parse("<temp><id>TCPU</id><value>-1</value></temp><temp><id>TGPU1</id><value>42</value></temp>");rig.At(7100);rig.At(11601);Assert.True(rig.Session.State.DisplayOff);
   }
  }
  [Test] public static void Followup_MigrationFromRecoveryBackupAlsoPreservesSource() {
   using(var dir=new TempDirectory()) {
    var legacy=AppSettings.Defaults();legacy.PresetLibraryVersion=1;legacy.ActiveProfile.Cpu.TemperatureId="TCPUDIO";
    string previous=Path.Combine(dir.Path,"settings.previous.json");using(var stream=File.Create(previous))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream,legacy);
    var bytes=File.ReadAllBytes(previous);File.WriteAllText(Path.Combine(dir.Path,"settings.json"),"broken");
    var store=new SettingsStore(dir.Path);store.Save(store.Load().Settings);store.Save(store.Load().Settings);
    string backup=Path.Combine(dir.Path,"settings.before-profile-library-v2.json");Assert.True(File.Exists(backup),"Recovery must protect the legacy backup before it rotates");Assert.True(bytes.SequenceEqual(File.ReadAllBytes(backup)));
   }
  }
  [Test] public static void Followup_FixedLevelIgnoresLegacyPause() {
   var engine=new AnimationEngine(new Random(1));
   var settings=new AnimationSettings{Mode=AnimationMode.Fixed,FixedFrame=5};
   Assert.Equal(5,engine.Advance(settings,null,0,true).Frame);
   settings.FixedFrame=3;Assert.Equal(3,engine.Advance(settings,null,.25,true).Frame);
  }
  [Test] public static void Followup_BrowsingProfileDoesNotAskToSaveOrActivateOnExit() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var controller=new FakeController{State=rig.Session.State};var store=new SettingsStore(dir.Path);
    using(var form=new MainForm(AppSettings.Defaults(),store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiTestPump.Wait(form.SelectProfile("Activity",()=>DialogResult.Cancel));
     Assert.True(UiBindingTests.Find<Button>(form,"ApplyChanges").Enabled,"A browsed profile must still be usable");
     Assert.True(!form.Text.Contains("unsaved"),"Browsing is not an edit");
     UiBindingTests.Find<ComboBox>(form,"ThemeChoice").SelectedIndex=2;
     UiTestPump.Wait(form.SavePreferences());Assert.Equal("Classic",store.Load().Settings.ActiveProfileName);Assert.Equal(0,controller.Applied);
     int prompts=0;form.RequestExit(()=>{prompts++;return DialogResult.Cancel;});Assert.Equal(0,prompts);
    }
   }
  }
  [Test] public static void Followup_MigrationBackupSurvivesOrdinarySaves() {
   using(var dir=new TempDirectory()) {
    var legacy=AppSettings.Defaults();legacy.PresetLibraryVersion=1;legacy.ActiveProfileName="Activity";legacy.ActiveProfile.Cpu.TemperatureId="TCPUDIO";
    string main=Path.Combine(dir.Path,"settings.json");
    using(var stream=File.Create(main))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream,legacy);
    byte[] original=File.ReadAllBytes(main);var store=new SettingsStore(dir.Path);var upgraded=store.Load().Settings;
    store.Save(upgraded);upgraded.DisplayEnabled=false;store.Save(upgraded);upgraded.CloseToTrayExplained=true;store.Save(upgraded);
    string backup=Path.Combine(dir.Path,"settings.before-profile-library-v2.json");
    Assert.True(File.Exists(backup),"The first migration must retain the original settings independently of the rotating backup");
    Assert.True(original.SequenceEqual(File.ReadAllBytes(backup)));Assert.Equal("TCPUDIO",store.Load().Settings.ActiveProfile.Cpu.TemperatureId);
    File.WriteAllBytes(main,original);store.Save(store.Load().Settings);Assert.True(original.SequenceEqual(File.ReadAllBytes(backup)),"Never replace the original migration snapshot");
   }
  }
  [Test] public static void Followup_PresetExplanationIsVisibleAndUpdatesCatalogUnit() {
   using(var host=new Form())using(var channel=new ChannelControl()) {
    host.Location=new System.Drawing.Point(-20000,-20000);host.StartPosition=FormStartPosition.Manual;host.Controls.Add(channel);channel.Dock=DockStyle.Fill;host.Show();Application.DoEvents();
    var explanation=UiBindingTests.Find<Label>(channel,"PresetExplanation");Assert.True(explanation!=null && explanation.Visible,"Named presets need their explanation too");
    var value=new ChannelSettings{TemperatureId="TCPU"};value.Animation.Mode=AnimationMode.SensorLevel;value.Animation.SensorId="SCPUUTI";value.Animation.InputMax=90;channel.Load(value,null);
    channel.SetCatalog(AidaParserTests.Parse("<sys><id>SCPUUTI</id><label>Usage</label><value>32</value></sys>").Snapshot);
    Assert.True(explanation.Text.Contains("90 %"),"Late sensor discovery must supply the unit in the explanation");
   }
  }
  [Test] public static void Followup_UnexpectedWorkerFailureIsLoggedBeforeExit() {
   using(var dir=new TempDirectory()) {
    var log=new EventLog(dir.Path);
    var constructor=typeof(SessionController).GetConstructor(new[]{typeof(Func<HolderSession>),typeof(SessionConfiguration),typeof(Action<string,Exception>)});
    Assert.True(constructor!=null,"Worker failure needs a diagnostic sink independent of app exit");
    Func<HolderSession> factory=()=>{throw new InvalidOperationException("worker fixture",new IOException("inner fixture"));};
    Action<string,Exception> sink=(message,error)=>log.Write("controller failure",message,error);
    using(var controller=(SessionController)constructor.Invoke(new object[]{factory,new SessionConfiguration(),sink})) {
     controller.Start();Assert.True(controller.Completion.Wait(4000));
     string content=File.ReadAllText(Path.Combine(dir.Path,"events.log"));Assert.True(content.Contains("InvalidOperationException") && content.Contains("IOException") && content.Contains("inner fixture"));
     Assert.True(controller.FailureMessage.Contains("worker fixture"));
    }
   }
  }
 }
}
