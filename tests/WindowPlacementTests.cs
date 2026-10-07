using System;
using System.Drawing;
using System.Reflection;
namespace ZX6DisplayControl.Tests {
 public static class WindowPlacementTests {
  [Test] public static void Placement_OrderlyExitRetainsBoundsForPostLoopSave() {
   foreach(bool maximized in new[]{false,true})using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.Show();form.Bounds=new Rectangle(60,60,1000,700);if(maximized)form.WindowState=System.Windows.Forms.FormWindowState.Maximized;System.Windows.Forms.Application.DoEvents();var before=form.CaptureWindowPlacement();
     form.RequestExit(()=>System.Windows.Forms.DialogResult.No);UiTestPump.Until(()=>form.IsDisposed);var after=form.CaptureWindowPlacement();Assert.Equal(before.Bounds,after.Bounds);Assert.Equal(before.Maximized,after.Maximized);
    }
   }
  }
  [Test] public static void Placement_PersistenceIsSeparateAndRejectsCorruptGeometry() {
   using(var dir=new TempDirectory()) {
    var settings=new SettingsStore(dir.Path);settings.Save(AppSettings.Defaults());string original=System.IO.File.ReadAllText(System.IO.Path.Combine(dir.Path,"settings.json"));
    var store=new WindowPlacementStore(dir.Path);store.Save(new WindowPlacement{X=50,Y=90,Width=1000,Height=700,Maximized=true});var read=store.Load();Assert.Equal(50,read.X);Assert.True(read.Maximized);Assert.Equal(original,System.IO.File.ReadAllText(System.IO.Path.Combine(dir.Path,"settings.json")));
    System.IO.File.WriteAllText(System.IO.Path.Combine(dir.Path,"window.json"),"{broken");Assert.True(store.Load()==null);
   }
  }
  [Test] public static void Placement_TrayRestorePreservesMaximizedState() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.Show();form.WindowState=System.Windows.Forms.FormWindowState.Maximized;System.Windows.Forms.Application.DoEvents();form.WindowState=System.Windows.Forms.FormWindowState.Minimized;form.ShowWindow();Assert.Equal(System.Windows.Forms.FormWindowState.Maximized,form.WindowState);Assert.True(form.CaptureWindowPlacement().Maximized);
    }
   }
  }
  [Test] public static void Placement_RemovedMonitorReturnsWindowToVisibleWorkArea() {
   var type=typeof(MainForm).Assembly.GetType("ZX6DisplayControl.WindowPlacement");Assert.True(type!=null,"Window placement persistence missing");
   var method=type.GetMethod("Fit");var result=(Rectangle)method.Invoke(null,new object[]{new Rectangle(6000,-2000,1800,1200),new[]{new Rectangle(0,0,1366,728)},new Size(860,620)});
   Assert.True(new Rectangle(0,0,1366,728).Contains(result),"Saved window must fit the current screen");
  }
 }
}
