using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class PublicationUiTests {
  [Test] public static void Publication_ProfileRowsFitEnlargedText() {
   using(var profiles=new ProfilesControl())using(var font=new System.Drawing.Font("Segoe UI",18)) {
    profiles.Font=font;profiles.SetProfiles(AppSettings.Defaults(),"Classic");
    var list=All(profiles).OfType<ListBox>().Single();
    int textHeight=TextRenderer.MeasureText("Ag",font).Height;
    Assert.True(list.ItemHeight>=textHeight*2+8,"Profile title and details clip or overlap at a larger font size");
   }
  }
  [Test] public static void Publication_ProfileSummaryRefreshesAfterApply() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="4";UiTestPump.Wait(form.ApplyChanges());
     var list=All(form).OfType<ListBox>().Single();Assert.Equal(4.0,list.Items.Cast<Profile>().Single(p=>p.Name=="Classic (custom)").Cpu.Animation.FixedFps);
    }
   }
  }
  [Test] public static void Publication_ExitDialogEscapeCancels() {
   var method=typeof(MainForm).Assembly.GetType("ZX6DisplayControl.AppDialog").GetMethod("Show");
   using(var timer=new Timer{Interval=50}) {
    timer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Exit regression");if(dialog==null)return;timer.Stop();typeof(Form).GetMethod("ProcessDialogKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(dialog,new object[]{Keys.Escape});};
    timer.Start();var result=(DialogResult)method.Invoke(null,new object[]{null,"Save changes before exiting?","Exit regression",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,null});Assert.Equal(DialogResult.Cancel,result);
   }
  }
  [Test] public static void Publication_HighContrastOverridesAppearance() {
   var theme=typeof(MainForm).Assembly.GetType("ZX6DisplayControl.UiTheme");var method=theme.GetMethod("Apply",new[]{typeof(Control),typeof(AppTheme),typeof(bool)});Assert.True(method!=null,"High contrast must be applied independently of the theme preference");
   using(var form=new Form()) {var apply=new Button{Name="ApplyChanges"};form.Controls.Add(apply);method.Invoke(null,new object[]{form,AppTheme.Dark,true});Assert.Equal(System.Drawing.SystemColors.Highlight,apply.BackColor);Assert.Equal(System.Drawing.SystemColors.HighlightText,apply.ForeColor);}
  }
  [Test] public static void Publication_DiagnosticsMarksWorkerFailure() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State,FailureMessage="Controller stopped"},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var diagnostic=All(form).OfType<DiagnosticsControl>().Single();Assert.True(All(diagnostic).OfType<Label>().Any(x=>x.Text.Contains("last readings")),"Diagnostics must identify retained values after worker failure");
    }
   }
  }
  [Test] public static void Publication_ProfileSwitchKeepsPendingAppSettings() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var theme=UiBindingTests.Find<ComboBox>(form,"ThemeChoice");theme.SelectedItem="Dark";
     var minimize=All(form).OfType<CheckBox>().Single(x=>x.Text=="Minimize to tray");minimize.Checked=true;
     UiTestPump.Wait(form.SelectProfile("Thermal",()=>DialogResult.Cancel));
     Assert.Equal("Dark",theme.SelectedItem as string);Assert.True(minimize.Checked);UiTestPump.Wait(form.ApplyChanges());UiTestPump.Wait(form.SavePreferences());
     var saved=new SettingsStore(dir.Path).Load().Settings;Assert.Equal(AppTheme.Dark,saved.Theme);Assert.True(saved.MinimizeToTray);
    }
   }
  }
  [Test] public static void Publication_FailedWorkerLabelsCachedReadings() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);var controller=new FakeController{State=rig.Session.State,FailureMessage="Controller stopped"};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.RefreshFromState();Assert.True(All(form).OfType<Label>().Any(x=>x.Text.Contains("Last reading")),"Stopped worker must not present cached values as live");
     Assert.True(!All(form).OfType<Label>().Any(x=>x.Text=="AIDA64 · connected"));
    }
   }
  }
  [Test] public static void Publication_TrayLifecyclePreservesDraftAndRestoresWindow() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var settings=AppSettings.Defaults();settings.CloseToTrayExplained=true;
    using(var form=new MainForm(settings,new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-15000,-15000);form.Show();
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<TextBox>(cpu,"FixedFps").Text="3";
     form.Close();Assert.True(!form.IsDisposed && !form.Visible);form.ShowWindow();Assert.True(form.Visible);Assert.Equal(3.0,cpu.GetDraft().Animation.FixedFps);
     var minimize=All(form).OfType<CheckBox>().Single(x=>x.Text=="Minimize to tray");minimize.Checked=true;form.RevertPreferences();Assert.True(!minimize.Checked);
     minimize.Checked=true;UiTestPump.Wait(form.SavePreferences());form.WindowState=FormWindowState.Minimized;Application.DoEvents();Assert.True(!form.Visible);form.ShowWindow();Assert.Equal(FormWindowState.Normal,form.WindowState);
     Assert.True(form.RequestExit(()=>DialogResult.No));
    }
   }
  }
  [Test] public static void Publication_DarkThemeAppliesWithoutDiscardingDraft() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var theme=All(form).OfType<ComboBox>().FirstOrDefault(x=>x.Name=="ThemeChoice");Assert.True(theme!=null,"Appearance setting is missing");
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<TextBox>(cpu,"FixedFps").Text="3";
     theme.SelectedItem="Dark";Assert.True(form.BackColor.GetBrightness()<0.2);Assert.True(cpu.ForeColor.GetBrightness()>0.7);Assert.Equal(3.0,cpu.GetDraft().Animation.FixedFps);
     UiTestPump.Wait(form.SavePreferences());var saved=new SettingsStore(dir.Path).Load().Settings;var property=typeof(AppSettings).GetProperty("Theme");Assert.True(property!=null);Assert.Equal("Dark",property.GetValue(saved,null).ToString());
     theme.SelectedItem="Light";Assert.True(form.BackColor.GetBrightness()>0.8);form.RevertPreferences();Assert.True(form.BackColor.GetBrightness()<0.2);
    }
   }
  }
  [Test] public static void Publication_TraySettingsMigrateAndPersist() {
   using(var dir=new TempDirectory()) {
    var property=typeof(AppSettings).GetProperty("CloseToTray");var minimize=typeof(AppSettings).GetProperty("MinimizeToTray");
    Assert.True(property!=null && minimize!=null,"Tray behavior must be configurable");
    var store=new SettingsStore(dir.Path);var value=AppSettings.Defaults();store.Save(value);
    string path=System.IO.Path.Combine(dir.Path,"settings.json");
    string json=System.IO.File.ReadAllText(path);json=System.Text.RegularExpressions.Regex.Replace(json,"\"(CloseToTray|MinimizeToTray)\":(true|false),?","");
    System.IO.File.WriteAllText(path,json);var legacy=store.Load().Settings;
    Assert.Equal(true,(bool)property.GetValue(legacy,null));Assert.Equal(false,(bool)minimize.GetValue(legacy,null));
    property.SetValue(legacy,false,null);minimize.SetValue(legacy,true,null);store.Save(legacy.Copy());
    Assert.Equal(false,(bool)property.GetValue(store.Load().Settings,null));Assert.Equal(true,(bool)minimize.GetValue(store.Load().Settings,null));
   }
  }
  private static IEnumerable<Control> All(Control root) {foreach(Control c in root.Controls){yield return c;foreach(var nested in All(c))yield return nested;}}
  [Test] public static void Publication_DeviceAndSettingsAreDiscoverable() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var pages=All(form).OfType<TabPage>().Select(x=>x.Text).ToArray();
     Assert.True(pages.Contains("Connection"));Assert.True(pages.Contains("Settings & About"));
     Assert.True(All(form).OfType<Label>().Any(x=>x.Text.Contains("Z-X6")));
    }
   }
  }
  [Test] public static void Publication_InteractiveControlsHaveContextHelp() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var functions=All(form).Where(x=>(x is ButtonBase || x is ComboBox || x is TextBox || x is ListBox || x is ListView || x is NumericUpDown) && !(x.Parent is NumericUpDown));
     foreach(var c in functions)Assert.True(!string.IsNullOrWhiteSpace(c.AccessibleDescription),"Missing contextual help: "+c.Name+" / "+c.Text);
    }
   }
  }
  [Test] public static void Publication_MissingAidaMarksLastReading() {
   using(var picker=new SensorPicker(true)) {
    picker.SetSnapshot(AidaParserTests.Parse(AidaParserTests.Cpu).Snapshot);picker.SelectedId="TCPU";
    var available=typeof(SensorPicker).GetMethod("SetAvailable");Assert.True(available!=null,"Sensor picker must distinguish a cached value from current data");available.Invoke(picker,new object[]{false});
    Assert.True(All(picker).OfType<Label>().Any(x=>x.Text.Contains("Last reading")));
    available.Invoke(picker,new object[]{true});Assert.True(!All(picker).OfType<Label>().Any(x=>x.Text.Contains("Last reading")));
   }
  }
  [Test] public static void Publication_ExitCanKeepDraftOrSaveIt() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var controller=new FakeController{State=rig.Session.State};var store=new SettingsStore(dir.Path);
    using(var form=new MainForm(AppSettings.Defaults(),store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";
     var request=typeof(MainForm).GetMethod("RequestExit");Assert.True(request!=null,"Exit must resolve pending edits before stopping");
     Assert.Equal(false,(bool)request.Invoke(form,new object[]{new Func<DialogResult>(()=>DialogResult.Cancel)}));Assert.Equal(0,controller.Applied);Assert.True(form.CanApply);
     Assert.Equal(true,(bool)request.Invoke(form,new object[]{new Func<DialogResult>(()=>DialogResult.Yes)}));UiTestPump.Until(()=>form.IsDisposed);Assert.Equal(1,controller.Applied);Assert.Equal(3.0,store.Load().Settings.ActiveProfile.Cpu.Animation.FixedFps);
    }
   }
  }
 }
}
