using System;
using System.Linq;
using System.Windows.Forms;

namespace ZX6DisplayControl.Tests {
 public static class WorkflowReworkTests {
  [Test] public static void Review_FailureBeforeFirstStateOffersStoppedRecovery() {
   using(var dir=new TempDirectory())using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{FailureMessage="Session factory failed"},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
    Assert.True(!UiBindingTests.Find<Button>(form,"Power").Enabled,"Power cannot operate without a controller");
    Assert.True(!UiBindingTests.Find<Button>(form,"Reconnect").Enabled,"Stopped worker cannot retry");
    Assert.Equal("Controller stopped",UiBindingTests.Find<HolderPreview>(form,"HolderPreview").Caption);
    Assert.True(UiBindingTests.Find<Button>(form,"ControllerExit").Enabled);
   }
  }
  [Test] public static void Rework_FixedLevelIsNotOverriddenByHiddenPause() {
   using(var channel=new ChannelControl()) {
    var value=new ChannelSettings{TemperatureId="TCPU",Paused=true};channel.Load(value,null);
    UiBindingTests.Find<ComboBox>(channel,"Mode").SelectedIndex=2;UiBindingTests.Find<NumericUpDown>(channel,"FixedFrame").Value=5;
    var draft=channel.GetDraft();var output=new AnimationEngine(new Random(1)).Advance(draft.Animation,null,0,draft.Paused);Assert.Equal(5,output.Frame);
    UiBindingTests.Find<ComboBox>(channel,"Mode").SelectedIndex=0;Assert.True(channel.GetDraft().Paused,"Returning to Loop should retain its paused state");
   }
  }
  [Test] public static void Rework_ProfileSwitchResolvesOnlyCurrentDraft() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var speed=UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps");speed.Text="3";
     Assert.True(!UiTestPump.Wait(form.SelectProfile("Thermal",()=>DialogResult.Cancel)));Assert.Equal("3",speed.Text);Assert.Equal(0,controller.Applied);
     Assert.True(UiTestPump.Wait(form.SelectProfile("Thermal",()=>DialogResult.No)));UiTestPump.Wait(form.ApplyChanges());
     Assert.Equal("Thermal",store.Load().Settings.ActiveProfileName);Assert.Equal(8,store.Load().Settings.Profiles.Count);Assert.Equal(2.0,store.Load().Settings.Profiles.Single(p=>p.Name=="Classic").Cpu.Animation.FixedFps);
     UiTestPump.Wait(form.SelectProfile("Classic",()=>DialogResult.Cancel));speed.Text="4";
     Assert.True(UiTestPump.Wait(form.SelectProfile("Activity",()=>DialogResult.Yes)));var value=store.Load().Settings;
     Assert.Equal("Classic (custom)",value.ActiveProfileName);Assert.Equal(4.0,value.ActiveProfile.Cpu.Animation.FixedFps);Assert.Equal(2,controller.Applied);
     form.CancelChanges();Assert.Equal(4.0,UiBindingTests.Find<ChannelControl>(form,"CpuChannel").GetDraft().Animation.FixedFps);
    }
   }
  }
  [Test] public static void Rework_PreferencesSaveIndependentlyOfInvalidDraft() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var speed=UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps");speed.Text="invalid";UiBindingTests.Find<ComboBox>(form,"ThemeChoice").SelectedIndex=2;
     UiTestPump.Wait(form.SavePreferences());Assert.Equal(AppTheme.Dark,store.Load().Settings.Theme);Assert.Equal("Classic",store.Load().Settings.ActiveProfileName);Assert.Equal(0,controller.Applied);Assert.Equal("invalid",speed.Text);
     form.CancelChanges();Assert.Equal(2,UiBindingTests.Find<ComboBox>(form,"ThemeChoice").SelectedIndex);
    }
   }
  }
  [Test] public static void Rework_ProfileSaveDoesNotWritePreferencesOrAutorun() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);var registry=new FakeStartupStore{Value="previous startup target"};
    using(var form=new MainForm(AppSettings.Defaults(),store,new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(registry),@"C:\Holder.exe")) {
     UiBindingTests.Find<ComboBox>(form,"ThemeChoice").SelectedIndex=2;
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";UiTestPump.Wait(form.ApplyChanges());
     Assert.Equal(AppTheme.System,store.Load().Settings.Theme);Assert.Equal("previous startup target",registry.Value);Assert.Equal(2,UiBindingTests.Find<ComboBox>(form,"ThemeChoice").SelectedIndex);
    }
   }
  }
  [Test] public static void Rework_PreviewUsesSentFramesAndClearsWhenStopped() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);rig.At(1000);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var preview=UiBindingTests.Find<HolderPreview>(form,"HolderPreview");form.RefreshFromState();
     Assert.Equal(controller.State.Cpu.Frame,preview.CpuLevel.Value);Assert.Equal(controller.State.CpuNumber,preview.CpuTemperature);Assert.True(!preview.IsDraft);
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";Assert.True(preview.IsDraft);Assert.True(preview.Caption.Contains("not applied"));
     controller.FailureMessage="fixture stopped";form.RefreshFromState();Assert.True(!preview.CpuTemperature.HasValue && !preview.CpuLevel.HasValue);Assert.True(!UiBindingTests.Find<Button>(form,"Reconnect").Enabled);
    }
   }
  }
  [Test] public static void Rework_LiveStatusContinuesWhileModeDropDownIsOpen() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();
     var mode=UiBindingTests.Find<ComboBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"Mode");mode.Focus();mode.DroppedDown=true;Assert.True(mode.DroppedDown,"Fixture did not open the mode menu");
     controller.FailureMessage="fixture stopped";form.RefreshFromState();
     Assert.True(mode.DroppedDown,"Live refresh dismissed the open menu");Assert.True(UiBindingTests.Find<Button>(form,"HolderStatus").Text.Contains("stopped"));mode.DroppedDown=false;
    }
   }
  }
  [Test] public static void Rework_ApplyCreatesPersonalCopyWithoutChangingBuiltIn() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);
    using(var form=new MainForm(AppSettings.Defaults(),store,new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";
     UiTestPump.Wait(form.ApplyChanges());var saved=store.Load().Settings;
     Assert.True(!saved.ActiveProfile.IsBuiltIn);Assert.Equal(3.0,saved.ActiveProfile.Cpu.Animation.FixedFps);
     Assert.Equal(2.0,saved.Profiles.Single(p=>p.Name=="Classic").Cpu.Animation.FixedFps);
    }
   }
  }
  [Test] public static void Rework_ReturningToOriginalValueClearsPendingChanges() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var speed=UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps");speed.Text="3";speed.Text="2";form.RefreshFromState();
     Assert.True(!UiBindingTests.Find<Button>(form,"ApplyChanges").Enabled,"Returning to the saved value must clear pending changes");
    }
   }
  }
  [Test] public static void Rework_FixedModeRoundTripPreservesCustomSensorResponse() {
   using(var channel=new ChannelControl()) {
    var value=new ChannelSettings{TemperatureId="TCPU"};value.Animation.Mode=AnimationMode.SensorLevel;value.Animation.SensorId="FCPU";value.Animation.InputMin=500;value.Animation.InputMax=1800;value.Animation.FilterSeconds=5;value.Animation.Invert=true;
    channel.Load(value,null);var mode=UiBindingTests.Find<ComboBox>(channel,"Mode");mode.SelectedIndex=2;mode.SelectedIndex=1;
    var draft=channel.GetDraft();Assert.Equal("FCPU",draft.Animation.SensorId);Assert.Equal(500.0,draft.Animation.InputMin);Assert.Equal(1800.0,draft.Animation.InputMax);Assert.Equal(5.0,draft.Animation.FilterSeconds);Assert.True(draft.Animation.Invert);
   }
  }
 }
}
