using System;
using System.IO;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class SetupTests {
  [Test] public static void Setup_SelectionsStayDraftAndInvalidSecondSideDoesNotChangeFirst() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var controller=new FakeController{State=rig.Session.State};var store=new SettingsStore(dir.Path);store.Save(AppSettings.Defaults());
    using(var form=new MainForm(store.Load().Settings,store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     Assert.Throws<InvalidOperationException>(()=>form.StageSetup("TGPU1","missing",null,null));Assert.Equal("TCPU",UiBindingTests.Find<ChannelControl>(form,"CpuChannel").GetDraft().TemperatureId);
     form.StageSetup("TGPU1","TCPU","SCPUUTI","SCPUUTI");Assert.Equal(0,controller.Applied);Assert.Equal("TCPU",store.Load().Settings.ActiveProfile.Cpu.TemperatureId);Assert.Equal("TGPU1",UiBindingTests.Find<ChannelControl>(form,"CpuChannel").GetDraft().TemperatureId);
    }
   }
  }
  [Test] public static void Setup_DisplayCheckRestoresOnAnotherActionOrFailure() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(1000);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartDisplayCheck();UiTestPump.Wait(form.ActivateProfile("Thermal",()=>DialogResult.No));Assert.Equal(AnimationMode.Cycle,controller.Configuration.Cpu.Animation.Mode);Assert.Equal(AnimationRateMode.Sensor,controller.Configuration.Cpu.Animation.RateMode);
     form.StartDisplayCheck();controller.FailureMessage="stopped";form.RefreshFromState();Assert.Equal(AnimationMode.Cycle,controller.Configuration.Cpu.Animation.Mode);
    }
   }
  }
  [Test] public static void Setup_DisplayCheckRestoresOutputAndNeverSavesTestSettings() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(1000);var store=new SettingsStore(dir.Path);store.Save(AppSettings.Defaults());string before=File.ReadAllText(Path.Combine(dir.Path,"settings.json"));var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(store.Load().Settings,store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var start=typeof(MainForm).GetMethod("StartDisplayCheck");Assert.True(start!=null,"Display check missing");start.Invoke(form,null);
     Assert.Equal(AnimationMode.Fixed,controller.Configuration.Cpu.Animation.Mode);Assert.Equal(0,controller.Configuration.Cpu.Animation.FixedFrame);Assert.Equal(7,controller.Configuration.Gpu.Animation.FixedFrame);Assert.Equal("TCPU",controller.Configuration.Cpu.TemperatureId);
     typeof(MainForm).GetMethod("StopDisplayCheck").Invoke(form,null);Assert.Equal(AnimationMode.Cycle,controller.Configuration.Cpu.Animation.Mode);Assert.Equal(before,File.ReadAllText(Path.Combine(dir.Path,"settings.json")));
    }
   }
  }
 }
}
