using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZX6DisplayControl.Tests {
 public static class UpdateTests {
  private sealed class HeldStartup:IStartupStore {
   public readonly ManualResetEventSlim Release=new ManualResetEventSlim();
   public string Read(){if(!Release.Wait(5000))throw new TimeoutException("fixture");return null;}
   public void Write(string value){}public void Delete(){}
  }
  public static byte[] Releases(params string[] versions) {
   return Encoding.UTF8.GetBytes("["+string.Join(",",Array.ConvertAll(versions,v=>"{\"tag_name\":\"v"+v+"\",\"draft\":false,\"prerelease\":true,\"assets\":[{\"name\":\"ZX6DisplayControl.zip\",\"state\":\"uploaded\"}]}"))+"]");
  }
  [Test] public static void Updates_VersionsCompareNumericallyAndRespectStablePrecedence() {
   Assert.True(ReleaseVersion.Parse("v0.1.0-beta.10").CompareTo(ReleaseVersion.Parse("0.1.0-beta.2"))>0);
   Assert.True(ReleaseVersion.Parse("0.1.0").CompareTo(ReleaseVersion.Parse("0.1.0-rc.9"))>0);
   Assert.True(ReleaseVersion.Parse("0.2.0-beta.1").CompareTo(ReleaseVersion.Parse("0.1.0"))>0);
   Assert.Equal(0,ReleaseVersion.Parse("1.2.3+build.9").CompareTo(ReleaseVersion.Parse("1.2.3+other")));
   Assert.Throws<FormatException>(()=>ReleaseVersion.Parse("0.1.0-beta.02"));
   Assert.Throws<FormatException>(()=>ReleaseVersion.Parse("https://outside.example"));
   Assert.Throws<FormatException>(()=>ReleaseVersion.Parse("0.1.0\n"));
  }
  [Test] public static void Updates_SelectNewestPublishedAppIncludingBeta() {
   var result=UpdateService.SelectRelease(Releases("0.1.0-beta.2","0.1.0-beta.10","0.1.0-beta.1"),"0.1.0-beta.1");
   Assert.Equal("0.1.0-beta.10",result.AvailableVersion);
   Assert.Equal("https://github.com/XxYouDeaDPunKxX/zx6-display-control/releases/tag/v0.1.0-beta.10",result.ReleaseUrl.AbsoluteUri);
   Assert.True(UpdateService.SelectRelease(Releases("0.1.0-beta.1"),"0.1.0-beta.2").AvailableVersion==null);
   var draft=Encoding.UTF8.GetString(Releases("9.0.0")).Replace("\"draft\":false","\"draft\":true");
   Assert.True(UpdateService.SelectRelease(Encoding.UTF8.GetBytes(draft),"0.1.0").AvailableVersion==null);
   var noBinary=Encoding.UTF8.GetString(Releases("9.0.0")).Replace("ZX6DisplayControl.zip","source.zip");
   Assert.True(UpdateService.SelectRelease(Encoding.UTF8.GetBytes(noBinary),"0.1.0").Message.Contains("No published app release"));
  }
  [Test] public static void Updates_DailyThrottlePersistsAndManualCheckBypassesIt() {
   using(var dir=new TempDirectory()) {
    var now=new DateTime(2026,10,6,10,0,0,DateTimeKind.Utc);int calls=0;
    Func<CancellationToken,Task<byte[]>> fetch=ct=>{calls++;return Task.FromResult(Releases("0.1.0-beta.2"));};
    var service=new UpdateService(dir.Path,fetch,()=>now);
    Assert.Equal("0.1.0-beta.2",UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",true,CancellationToken.None)).AvailableVersion);
    service=new UpdateService(dir.Path,fetch,()=>now.AddHours(23));
    Assert.Equal("0.1.0-beta.2",UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",true,CancellationToken.None)).AvailableVersion);Assert.Equal(1,calls);
    Assert.True(UiTestPump.Wait(service.CheckAsync("0.1.0-beta.2",true,CancellationToken.None)).AvailableVersion==null,"Installing the cached version must clear the notification");
    service=new UpdateService(dir.Path,fetch,()=>now.AddHours(24));
    UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",true,CancellationToken.None));Assert.Equal(2,calls);
    UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",false,CancellationToken.None));Assert.Equal(3,calls);
    Assert.True(!File.Exists(Path.Combine(dir.Path,"settings.json")) && !File.Exists(Path.Combine(dir.Path,"settings.previous.json")),"Update checks must not save profiles");
   }
  }
  [Test] public static void Updates_FailedCheckIsThrottledAndDoesNotClaimCurrent() {
   using(var dir=new TempDirectory()) {
    int calls=0;Func<CancellationToken,Task<byte[]>> fetch=ct=>{calls++;throw new IOException("offline fixture");};
    var service=new UpdateService(dir.Path,fetch);
    var result=UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",true,CancellationToken.None));Assert.True(result.Message.Contains("Could not check"));
    UiTestPump.Wait(new UpdateService(dir.Path,fetch).CheckAsync("0.1.0-beta.1",true,CancellationToken.None));Assert.Equal(1,calls);
   }
  }
  [Test] public static void Updates_UnwritableCacheSkipsAutomaticNetworkButAllowsManual() {
   using(var dir=new TempDirectory()) {
    string path=Path.Combine(dir.Path,"blocked");File.WriteAllText(path,"not a folder");int calls=0;
    var service=new UpdateService(path,ct=>{calls++;return Task.FromResult(Releases("0.1.0-beta.2"));});
    var result=UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",true,CancellationToken.None));Assert.Equal(0,calls);Assert.True(result.Message.Contains("Automatic check skipped"));
    result=UiTestPump.Wait(service.CheckAsync("0.1.0-beta.1",false,CancellationToken.None));Assert.Equal(1,calls);Assert.True(result.AvailableVersion!=null);
   }
  }
  [Test] public static void Updates_UIStartsAfterShownAndPreservesDraftWhileChecking() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);int calls=0;var pending=new TaskCompletionSource<byte[]>();var controller=new FakeController{State=rig.Session.State};
    var service=new UpdateService(dir.Path,ct=>{Interlocked.Increment(ref calls);return pending.Task;});
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe",service)) {
     Assert.Equal(0,calls);form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();
     UiTestPump.Until(()=>calls==1);var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");var speed=UiBindingTests.Find<TextBox>(cpu,"FixedFps");speed.Text="3";
     Assert.True(cpu.Enabled,"A network request must not disable profile editing");bool responsive=false;form.BeginInvoke(new Action(()=>responsive=true));UiTestPump.Until(()=>responsive);
     pending.SetResult(Releases("0.1.0-beta.2"));UiTestPump.Until(()=>UiBindingTests.Find<Button>(form,"OpenRelease").Enabled);
     Assert.True(UiBindingTests.Find<Label>(form,"UpdateStatus").Text.Contains("0.1.0-beta.2"));Assert.Equal("3",speed.Text);Assert.Equal(0,controller.Applied);
     var notice=UiBindingTests.Find<LinkLabel>(form,"UpdateNotice");Assert.True(notice.Visible && notice.Text.Contains("0.1.0-beta.2"),"New releases must have a visible notice outside Settings");
     Assert.True(!File.Exists(Path.Combine(dir.Path,"settings.json")),"Checking must not commit pending edits");
    }
   }
  }
  [Test] public static void Updates_UIOptOutPersistsIndependentlyAndStillAllowsManualCheck() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);int calls=0;var service=new UpdateService(dir.Path,ct=>{calls++;return Task.FromResult(Releases("0.1.0-beta.2"));});var store=new SettingsStore(dir.Path);
    var property=typeof(AppSettings).GetProperty("CheckUpdatesOnStartup");Assert.True(property!=null,"Startup update preference missing");
    var settings=AppSettings.Defaults();Assert.Equal(true,(bool)property.GetValue(settings,null));property.SetValue(settings,false,null);
    using(var form=new MainForm(settings,store,new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe",service)) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();Assert.Equal(0,calls);
     var speed=UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps");speed.Text="invalid";
     UiTestPump.Wait(form.SavePreferences());Assert.Equal(false,(bool)property.GetValue(store.Load().Settings,null));
     UiTestPump.Wait(form.CheckForUpdates());Assert.Equal(1,calls);Assert.Equal("invalid",speed.Text);
     var option=UiBindingTests.Find<CheckBox>(form,"CheckUpdatesOnStartup");option.Checked=true;form.RevertPreferences();Assert.True(!option.Checked);
     option.Checked=true;UiTestPump.Wait(form.SavePreferences());Assert.True(store.Load().Settings.CheckUpdatesOnStartup);Assert.Equal("invalid",speed.Text);
    }
   }
  }
  [Test] public static void Updates_OlderSettingsEnableChecksAndSavedOptOutSurvivesCopy() {
   using(var dir=new TempDirectory()) {
    var store=new SettingsStore(dir.Path);store.Save(AppSettings.Defaults());string path=Path.Combine(dir.Path,"settings.json");
    string json=File.ReadAllText(path);json=json.Replace("\"CheckUpdatesOnStartup\":true,","").Replace(",\"CheckUpdatesOnStartup\":true","");
    Assert.True(!json.Contains("CheckUpdatesOnStartup"));File.WriteAllText(path,json);
    Assert.True(store.Load().Settings.CheckUpdatesOnStartup);
    var settings=store.Load().Settings;settings.CheckUpdatesOnStartup=false;store.Save(settings.Copy());Assert.True(!store.Load().Settings.CheckUpdatesOnStartup);
   }
  }
  [Test] public static void Updates_ExitCancelsPendingRequestWithoutWaitingForNetwork() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var pending=new TaskCompletionSource<byte[]>();var started=new ManualResetEventSlim(false);bool canceled=false;
    var service=new UpdateService(dir.Path,ct=>{ct.Register(()=>canceled=true);started.Set();return pending.Task;});
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe",service)) {
     var check=form.CheckForUpdates();UiTestPump.Until(()=>started.IsSet);form.Dispose();Assert.True(canceled,"Disposal must cancel the network operation");
     pending.SetResult(Releases("0.1.0-beta.2"));UiTestPump.Wait(check);
    }
    started.Dispose();
   }
  }
  [Test] public static void Updates_CanceledDeferredExitRestoresManualCheck() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var backend=new HeldStartup();var settings=AppSettings.Defaults();settings.CheckUpdatesOnStartup=false;
    var started=new ManualResetEventSlim();var pending=new TaskCompletionSource<byte[]>();
    var service=new UpdateService(dir.Path,ct=>{started.Set();ct.Register(()=>pending.TrySetCanceled());return pending.Task;});
    using(var form=new MainForm(settings,new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(backend),@"C:\Holder.exe",service)) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";
     var check=form.CheckForUpdates();UiTestPump.Until(()=>started.IsSet);var save=form.SavePreferences();bool asked=false;
     try{form.RequestExit(()=>{asked=true;return DialogResult.Cancel;});UiTestPump.Wait(check);}finally{backend.Release.Set();}
     UiTestPump.Wait(save);UiTestPump.Until(()=>asked);Application.DoEvents();
     var tabs=UiBindingTests.Find<Button>(form,"CheckForUpdates");
     Assert.True(tabs.Enabled,"Canceling Exit must restore the manual update action");
    }
    started.Dispose();backend.Release.Dispose();
   }
  }
 }
}
