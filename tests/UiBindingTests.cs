using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public sealed class FakeController:ISessionController {
  public SessionState State {get;set;}public string FailureMessage {get;set;}public int Applied;public SessionConfiguration Configuration;
  public System.Threading.Tasks.Task Completion {get{return System.Threading.Tasks.Task.FromResult(true);}}public void Start(){}
  public void Apply(SessionConfiguration c){Applied++;Configuration=c.Copy();}public void Retry(){}public void Suspend(){}public void Resume(){}public void Stop(){}
 }
 public static class UiBindingTests {
  private sealed class DeniedStartup:IStartupStore {public string Read(){return null;}public void Write(string value){throw new UnauthorizedAccessException("registry test");}public void Delete(){throw new UnauthorizedAccessException("registry test");}}
  private static CheckBox StartupCheck(Control root) {foreach(Control child in root.Controls){var check=child as CheckBox;if(check!=null && check.Text=="Start with Windows")return check;var found=StartupCheck(child);if(found!=null)return found;}return null;}
  [Test] public static void UiBinding_DisablingMovedAutorunRemovesPreviousPath() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var registry=new FakeStartupStore{Value="\"C:\\Old\\Holder.exe\" --tray"};var settings=AppSettings.Defaults();settings.StartWithWindows=true;
    using(var form=new MainForm(settings,new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(registry),@"C:\New\Holder.exe")) {
     StartupCheck(form).Checked=false;UiTestPump.Wait(form.SavePreferences());Assert.True(registry.Value==null,"Old autorun path still present");
    }
   }
  }
  [Test] public static void UiBinding_RegistryFailureDoesNotPersistUnappliedProfile() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);var settings=AppSettings.Defaults();store.Save(settings);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(settings,store,controller,new EventLog(dir.Path),new StartupRegistration(new DeniedStartup()),@"C:\Holder.exe")) {
     Find<TextBox>(Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="4";StartupCheck(form).Checked=true;
     Assert.Throws<UnauthorizedAccessException>(()=>UiTestPump.Wait(form.SavePreferences()));form.CancelChanges();Assert.Equal(0,controller.Applied);Assert.Equal(2.0,store.Load().Settings.ActiveProfile.Cpu.Animation.FixedFps);Assert.True(!store.Load().Settings.StartWithWindows);
    }
   }
  }
  [Test] public static void UiBinding_FileFailureRestoresExactPreviousAutorun() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);string blocked=System.IO.Path.Combine(dir.Path,"blocked");System.IO.File.WriteAllText(blocked,"not a directory");
    var registry=new FakeStartupStore{Value="\"C:\\Old\\Holder.exe\" --tray"};string previous=registry.Value;var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(blocked),controller,new EventLog(dir.Path),new StartupRegistration(registry),@"C:\New\Holder.exe")) {
     StartupCheck(form).Checked=true;Assert.Throws<System.IO.IOException>(()=>UiTestPump.Wait(form.SavePreferences()));Assert.Equal(previous,registry.Value);Assert.Equal(0,controller.Applied);
    }
   }
  }
  public static T Find<T>(Control root,string name) where T:Control {var result=root.Controls.Find(name,true).OfType<T>().FirstOrDefault();Assert.True(result!=null,"Missing UI control "+name);return result;}
  [Test] public static void UiBinding_SensorSelectionSurvivesCatalogOrderAndMissing() {
   var first=AidaParserTests.Parse(AidaParserTests.Cpu+"<sys><id>SCPUUTI</id><label>CPU load</label><value>25</value></sys><temp><id>TGPU1</id><value>33</value></temp>").Snapshot;
   var second=AidaParserTests.Parse("<temp><id>TGPU1</id><value>34</value></temp>"+AidaParserTests.Cpu).Snapshot;
   using(var picker=new SensorPicker(true)) {
    picker.SetSnapshot(first);picker.SelectedId="TGPU1";picker.SetSnapshot(second);Assert.Equal("TGPU1",picker.SelectedId);
    var combo=Find<ComboBox>(picker,"SensorChoice");Assert.True(!combo.Items.Cast<object>().Any(o=>o.ToString().Contains("SCPUUTI")));
    picker.SetSnapshot(AidaParserTests.Parse(AidaParserTests.Cpu).Snapshot);Assert.Equal("TGPU1",picker.SelectedId);Assert.True(combo.Text.Contains("unavailable"));
   }
  }
  [Test] public static void UiBinding_DecimalCultureAndValidation() {
   var before=Thread.CurrentThread.CurrentCulture;Thread.CurrentThread.CurrentCulture=new CultureInfo("it-IT");
   try {using(var channel=new ChannelControl()) {
    channel.Load(new ChannelSettings{TemperatureId="TCPU"},null);Find<TextBox>(channel,"FixedFps").Text="2,5";Assert.Equal(2.5,channel.GetDraft().Animation.FixedFps);
    Find<TextBox>(channel,"InputMin").Text="50";Find<TextBox>(channel,"InputMax").Text="50";Assert.True(channel.GetDraft().Animation.Validate().Count>0);
   }} finally {Thread.CurrentThread.CurrentCulture=before;}
  }
  [Test] public static void UiBinding_PreviewCancelAndAtomicApply() {
   using(var dir=new TempDirectory()) using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var cpu=Find<ChannelControl>(form,"CpuChannel");Find<TextBox>(cpu,"FixedFps").Text="4";form.RefreshFromState();Assert.Equal(0,controller.Applied);Assert.True(form.CanApply);
     form.CancelChanges();Assert.Equal(2.0,cpu.GetDraft().Animation.FixedFps);Assert.Equal(0,controller.Applied);
     Find<TextBox>(cpu,"FixedFps").Text="3";UiTestPump.Wait(form.ApplyChanges());Assert.Equal(1,controller.Applied);Assert.Equal(3.0,controller.Configuration.Cpu.Animation.FixedFps);Assert.Equal(2.0,controller.Configuration.Gpu.Animation.FixedFps);
     Find<TextBox>(cpu,"InputMin").Text="100";Assert.True(!form.CanApply);Assert.Throws<InvalidOperationException>(()=>UiTestPump.Wait(form.ApplyChanges()));Assert.Equal(1,controller.Applied);
    }
   }
  }
 }
}
