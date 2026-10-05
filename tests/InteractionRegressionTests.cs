using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZX6DisplayControl.Tests {
 public static class InteractionRegressionTests {
  [DllImport("user32.dll",CharSet=CharSet.Auto)]
  private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
  private static IEnumerable<Control> All(Control root) {
   foreach(Control child in root.Controls) {yield return child;foreach(var nested in All(child))yield return nested;}
  }
  private static void ShowOffscreen(Form form) {form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();}
  private static void RaiseComboEvent(ComboBox combo,string name) {
   typeof(ComboBox).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combo,new object[]{EventArgs.Empty});
  }

  [Test] public static void Interaction_ClosingSensorMenuBeforeChangeNotificationPreservesChoice() {
   // Win32 permits CBN_CLOSEUP before CBN_SELCHANGE. Exercise the real control,
   // setting only this test's native combo selection without issuing a notification.
   var snapshot=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><label>GPU</label><value>33</value></temp>").Snapshot;
   using(var form=new Form())using(var picker=new SensorPicker(true)) {
    form.Controls.Add(picker);ShowOffscreen(form);picker.SetSnapshot(snapshot);picker.SelectedId="TCPU";
    var combo=UiBindingTests.Find<ComboBox>(picker,"SensorChoice");
    int target=Enumerable.Range(0,combo.Items.Count).Single(i=>combo.Items[i].ToString().Contains("TGPU1"));
    SendMessage(combo.Handle,0x014E,new IntPtr(target),IntPtr.Zero); // CB_SETCURSEL
    RaiseComboEvent(combo,"OnDropDownClosed");
    RaiseComboEvent(combo,"OnSelectedIndexChanged");
    picker.SetSnapshot(snapshot);
    Assert.Equal("TGPU1",picker.SelectedId);
   }
  }

  [Test] public static void Interaction_SensorMenuDisambiguatesTemperatureAndFan() {
   var snapshot=AidaParserTests.Parse("<temp><id>TGPU1</id><label>GPU</label><value>33</value></temp><fan><id>FGPU1</id><label>GPU</label><value>0</value></fan>").Snapshot;
   using(var picker=new SensorPicker(false)) {
    picker.SetSnapshot(snapshot);var options=UiBindingTests.Find<ComboBox>(picker,"SensorChoice").Items.Cast<object>().Select(x=>x.ToString()).ToArray();
    Assert.True(options.Single(x=>x.Contains("TGPU1")).Contains("Temperature"),"Temperature must be identifiable without decoding an AIDA ID");
    Assert.True(options.Single(x=>x.Contains("FGPU1")).Contains("RPM"),"Fan speed must be distinguishable from temperature");
   }
  }

  [Test] public static void Interaction_PauseAndValidationHaveAccessibleText() {
   using(var channel=new ChannelControl()) {
    var pause=All(channel).OfType<CheckBox>().Single(x=>x.Text.StartsWith("Pause animation"));
    Assert.Equal(pause.Text,pause.AccessibilityObject.Name);
    UiBindingTests.Find<TextBox>(channel,"InputMin").Text="200";
    var error=All(channel).OfType<Label>().Single(x=>x.Text.Contains("Sensor minimum"));
    Assert.Equal(error.Text,error.AccessibilityObject.Name);
   }
  }

  [Test] public static void Interaction_SaveFeedbackSurvivesSensorRefresh() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";
     UiTestPump.Wait(form.ApplyChanges());form.RefreshFromState();
     Assert.True(All(form).OfType<Label>().Any(x=>x.Text.IndexOf("saved",StringComparison.OrdinalIgnoreCase)>=0),"Saving must leave persistent feedback, independent of connection refresh");
    }
   }
  }

  [Test] public static void Interaction_InvalidCpuIsExplainedWhileViewingGpu() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     ShowOffscreen(form);var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<TextBox>(cpu,"InputMin").Text="200";
     All(form).OfType<TabControl>().Single(x=>x.TabPages.Cast<TabPage>().Any(p=>p.Text=="CPU")).SelectedIndex=1;form.RefreshFromState();
     Assert.True(All(form).OfType<Label>().Any(x=>x.Visible && x.Text.Contains("CPU") && x.Text.Contains("minimum")),"The invalid channel and reason must remain visible outside that tab");
    }
   }
  }

  [Test] public static void Interaction_DiagnosticSelectionSurvivesFreshSnapshot() {
   using(var form=new Form())using(var diagnostics=new DiagnosticsControl())using(var rig=new SessionRig()) {
    form.Controls.Add(diagnostics);ShowOffscreen(form);rig.At(0);diagnostics.UpdateState(rig.Session.State);
    var list=All(diagnostics).OfType<ListView>().Single();list.Items.Cast<ListViewItem>().Single(x=>x.Text=="TCPU").Selected=true;
    rig.Reader.Result=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><label>GPU</label><value>34</value></temp>");rig.At(500);diagnostics.UpdateState(rig.Session.State);
    Assert.Equal(1,list.SelectedItems.Count);Assert.Equal("TCPU",list.SelectedItems[0].Text);
   }
  }
 }
}
