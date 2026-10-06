using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class UiTestPump {
  public static void Until(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition() && watch.ElapsedMilliseconds<6000){Application.DoEvents();Thread.Sleep(1);}Assert.True(condition(),"Async UI state did not settle");}
  public static void Wait(Task task){var watch=Stopwatch.StartNew();while(!task.IsCompleted && watch.ElapsedMilliseconds<6000){Application.DoEvents();Thread.Sleep(1);}Assert.True(task.IsCompleted,"Async UI action did not finish");task.GetAwaiter().GetResult();}
  public static T Wait<T>(Task<T> task){Wait((Task)task);return task.GetAwaiter().GetResult();}
 }
 public static class AsyncUiTests {
  private sealed class HeldStartup:IStartupStore {
   public readonly ManualResetEventSlim Release=new ManualResetEventSlim();public bool Fail;public string Read(){if(!Release.Wait(4000))throw new TimeoutException("fixture");if(Fail)throw new IOException("save fixture");return null;}public void Write(string value){}public void Delete(){}
  }
  [Test] public static void AsyncUi_CanceledDeferredExitRestoresEditors() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var backend=new HeldStartup{Fail=true};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(backend),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<TextBox>(cpu,"FixedFps").Text="3";
     PublicationPreference(form);var save=form.SavePreferences();bool asked=false;form.RequestExit(()=>{asked=true;return DialogResult.Cancel;});backend.Release.Set();Assert.Throws<IOException>(()=>UiTestPump.Wait(save));UiTestPump.Until(()=>asked);Application.DoEvents();
     Assert.True(!form.IsDisposed && cpu.Enabled,"Cancel left the editor disabled by its parent tab control");
    }
   }
  }
  private static void PublicationPreference(MainForm form) {form.Controls.Cast<Control>().SelectMany(Flatten).OfType<CheckBox>().Single(x=>x.Text=="Minimize to tray").Checked=true;}
  private static Task InvokeTask(MainForm form,string name) {try{return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(form,null) as Task??Task.FromResult(true);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
  [Test] public static void AsyncUi_SaveDoesNotBlockWhilePersistenceIsSlow() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var backend=new HeldStartup();var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(backend),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";
     using(var release=new System.Threading.Timer(s=>backend.Release.Set(),null,1000,Timeout.Infinite)) {
      var watch=Stopwatch.StartNew();PublicationPreference(form);Task save=InvokeTask(form,"SavePreferences");watch.Stop();
      try{Assert.True(watch.ElapsedMilliseconds<300,"Save blocked UI on persistence");Assert.Equal(0,controller.Applied);bool callback=false;form.BeginInvoke(new Action(()=>callback=true));Application.DoEvents();Assert.True(callback);}
      finally{backend.Release.Set();UiTestPump.Wait(save);}
      Assert.Equal(0,controller.Applied);Assert.True(new SettingsStore(dir.Path).Load().Settings.MinimizeToTray);Assert.Equal(2.0,new SettingsStore(dir.Path).Load().Settings.ActiveProfile.Cpu.Animation.FixedFps);
     }
    }
   }
  }
  [Test] public static void AsyncUi_FailedPowerSaveDoesNotInvertTheNextRequest() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);string target=Path.Combine(dir.Path,"settings-target");File.WriteAllText(target,"fixture");var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(target),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     Assert.Throws<IOException>(()=>UiTestPump.Wait(InvokeTask(form,"TogglePower")));Assert.Equal(0,controller.Applied);
     File.Delete(target);UiTestPump.Wait(InvokeTask(form,"TogglePower"));Assert.True(!controller.Configuration.DisplayEnabled,"A failed save already changed the remembered power state");
    }
   }
  }
  private sealed class DeferredController:ISessionController {
   public readonly TaskCompletionSource<bool> End=new TaskCompletionSource<bool>();public int Stops,Starts;
   public SessionState State{get;set;}public string FailureMessage{get{return null;}}public Task Completion{get{return End.Task;}}
   public void Start(){Starts++;}public void Stop(){Stops++;}public void Apply(SessionConfiguration c){}public void Retry(){}public void Suspend(){}public void Resume(){}
  }
  [Test] public static void AsyncUi_FailedExitSaveRestoresEditors() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var backend=new HeldStartup{Fail=true};backend.Release.Set();var controller=new DeferredController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(backend),@"C:\Holder.exe"))using(var dismiss=new System.Windows.Forms.Timer{Interval=20}) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<TextBox>(cpu,"FixedFps").Text="3";PublicationPreference(form);bool errorShown=false;
     dismiss.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=form && x.Text=="Z-X6 Display Control");if(dialog==null)return;errorShown=true;dialog.DialogResult=DialogResult.OK;};dismiss.Start();
     form.RequestExit(()=>DialogResult.Yes);UiTestPump.Until(()=>errorShown);dismiss.Stop();
     Assert.True(!form.IsDisposed && cpu.Enabled,"Failed exit save left the editor disabled");Assert.Equal(0,controller.Stops);Assert.Equal(3.0,cpu.GetDraft().Animation.FixedFps);
     controller.End.SetResult(true);form.RequestExit(()=>DialogResult.No);UiTestPump.Until(()=>form.IsDisposed);
    }
   }
  }
  [Test] public static void AsyncUi_SlowShutdownShowsWarningAndHandlesLateCompletion() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var controller=new DeferredController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();form.RequestExit(()=>DialogResult.No);
     try {
      UiTestPump.Until(()=>form.Controls.Cast<Control>().SelectMany(Flatten).OfType<Label>().Any(x=>x.Visible && x.Text.Contains("Still waiting for the controller")));
      bool callback=false;form.BeginInvoke(new Action(()=>callback=true));Application.DoEvents();Assert.True(callback && !form.IsDisposed);
     }finally{controller.End.SetResult(true);UiTestPump.Until(()=>form.IsDisposed);}
    }
   }
  }
  private static System.Collections.Generic.IEnumerable<Control> Flatten(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in Flatten(c))yield return child;}
  [Test] public static void AsyncUi_TrayExitShowsPendingShutdown() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var controller=new DeferredController{State=rig.Session.State};var settings=AppSettings.Defaults();settings.CloseToTrayExplained=true;
    using(var form=new MainForm(settings,new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();form.Close();Assert.True(!form.Visible);form.RequestExit(()=>DialogResult.No);
     try{Assert.True(form.Visible,"A pending tray shutdown has no visible status");}finally{controller.End.SetResult(true);UiTestPump.Until(()=>form.IsDisposed);}
    }
   }
  }
  [Test] public static void AsyncUi_SystemCloseWaitsForPendingSettingsTransaction() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var backend=new HeldStartup();
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(backend),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";PublicationPreference(form);var save=form.SavePreferences();var args=new FormClosingEventArgs(CloseReason.WindowsShutDown,false);
     typeof(Form).GetMethod("OnFormClosing",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{args});
     try{Assert.True(args.Cancel,"System close abandoned an in-flight settings transaction");}finally{backend.Release.Set();UiTestPump.Wait(save);}
     UiTestPump.Until(()=>form.IsDisposed);Assert.True(new SettingsStore(dir.Path).Load().Settings.MinimizeToTray);Assert.Equal(2.0,new SettingsStore(dir.Path).Load().Settings.ActiveProfile.Cpu.Animation.FixedFps);
    }
   }
  }
  [Test] public static void AsyncUi_IdleWindowsShutdownQueryDoesNotVetoOrStopController() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var controller=new DeferredController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();
     var args=new FormClosingEventArgs(CloseReason.WindowsShutDown,false);typeof(Form).GetMethod("OnFormClosing",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{args});
     try{Assert.True(!args.Cancel,"Idle app vetoed Windows shutdown");Assert.Equal(0,controller.Stops);}
     finally{controller.End.SetResult(true);}
    }
   }
  }
  [Test] public static void AsyncUi_ExitWaitsForWorkerWithoutDisposingWindow() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var controller=new DeferredController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();
     form.RequestExit(()=>DialogResult.No);Assert.True(!form.IsDisposed,"Window disposed while controller still owns resources");
     bool callback=false;form.BeginInvoke(new Action(()=>callback=true));Application.DoEvents();Assert.True(callback,"UI cannot dispatch while stopping");
     controller.End.SetResult(true);var watch=Stopwatch.StartNew();while(!form.IsDisposed && watch.ElapsedMilliseconds<3000)Application.DoEvents();Assert.True(form.IsDisposed);Assert.Equal(1,controller.Starts);Assert.Equal(1,controller.Stops);
    }
   }
  }
  [Test] public static void AsyncUi_StartWaitsUntilWindowShown() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {rig.At(0);var controller=new DeferredController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     Assert.Equal(0,controller.Starts);form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();Application.DoEvents();Assert.Equal(1,controller.Starts);controller.End.SetResult(true);
    }
   }
  }
 }
}
