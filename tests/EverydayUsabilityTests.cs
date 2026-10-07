using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class EverydayUsabilityTests {
  private sealed class NamedChoice {public string Label{get{return "CPU · Temperature";}}public override string ToString(){return "CPU · Temperature · TCPUDIO";}}
  private static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
  [Test] public static void Everyday_DrawUsesTheSensorDisplayLabelAndThemesTrayChildren() {
   var theme=typeof(MainForm).Assembly.GetType("ZX6DisplayControl.UiTheme");var draw=theme.GetMethod("DrawChoice",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
   using(var named=new ComboBox{DisplayMember="Label"})using(var plain=new ComboBox())using(var a=new System.Drawing.Bitmap(400,30))using(var b=new System.Drawing.Bitmap(400,30)) {
    named.Items.Add(new NamedChoice());plain.Items.Add("CPU · Temperature");
    using(var ga=System.Drawing.Graphics.FromImage(a))using(var gb=System.Drawing.Graphics.FromImage(b)) {
     draw.Invoke(null,new object[]{named,new DrawItemEventArgs(ga,named.Font,new System.Drawing.Rectangle(0,0,400,30),0,DrawItemState.Default)});
     draw.Invoke(null,new object[]{plain,new DrawItemEventArgs(gb,plain.Font,new System.Drawing.Rectangle(0,0,400,30),0,DrawItemState.Default)});
    }
    for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)Assert.Equal(b.GetPixel(x,y),a.GetPixel(x,y));
   }
  }
  [Test] public static void Everyday_TrayProfileChildrenUseDarkTheme() {
   var theme=typeof(MainForm).Assembly.GetType("ZX6DisplayControl.UiTheme");using(var host=new Form())using(var menu=new ContextMenuStrip()) {
    theme.GetMethod("Apply",new[]{typeof(Control),typeof(AppTheme),typeof(bool)}).Invoke(null,new object[]{host,AppTheme.Dark,false});
    var parent=new ToolStripMenuItem("Profiles");var child=new ToolStripMenuItem("Classic");parent.DropDownItems.Add(child);menu.Items.Add(parent);theme.GetMethod("Menu").Invoke(null,new object[]{menu});Assert.True(child.ForeColor.GetBrightness()>.7,"Tray profile labels must follow the dark theme");
   }
  }
  [Test] public static void Everyday_SaveDraftThenActivateSameBuiltInHonorsRequestedName() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);
    using(var form=new MainForm(AppSettings.Defaults(),store,new FakeController{State=rig.Session.State},new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";UiTestPump.Wait(form.ActivateProfile("Classic",()=>DialogResult.Yes));
     var saved=store.Load().Settings;Assert.Equal("Classic",saved.ActiveProfileName);Assert.Equal(3.0,saved.Profiles.Single(p=>p.Name=="Classic (custom)").Cpu.Animation.FixedFps);
    }
   }
  }
  [Test] public static void Everyday_CopyAnimationKeepsDestinationSensorsAndRange() {
   using(var channel=new ChannelControl(true)) {
    channel.Load(new ChannelSettings{TemperatureId="TGPU1HOT",Animation=new AnimationSettings{SensorId="SGPU1UTI",InputMin=0,InputMax=100}},null);
    var other=new ChannelSettings{TemperatureId="TCPUDIO",Paused=true,Animation=new AnimationSettings{Mode=AnimationMode.SensorLevel,SensorId="TCPUDIO",InputMin=30,InputMax=80,Invert=true}};
    var method=typeof(ChannelControl).GetMethod("CopyAnimationFrom");Assert.True(method!=null,"Copy animation action missing");method.Invoke(channel,new object[]{other,null});
    var draft=channel.GetDraft();Assert.Equal("TGPU1HOT",draft.TemperatureId);Assert.Equal("SGPU1UTI",draft.Animation.SensorId);Assert.Equal(100.0,draft.Animation.InputMax);Assert.True(draft.Paused && draft.Animation.Invert);Assert.Equal(AnimationMode.SensorLevel,draft.Animation.Mode);
   }
  }
  [Test] public static void Everyday_DirectActivationHonorsCancelAndPersistsChosenProfile() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var store=new SettingsStore(dir.Path);store.Save(AppSettings.Defaults());var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(store.Load().Settings,store,controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var method=typeof(MainForm).GetMethod("ActivateProfile");Assert.True(method!=null,"Direct activation missing");
     UiBindingTests.Find<TextBox>(UiBindingTests.Find<ChannelControl>(form,"CpuChannel"),"FixedFps").Text="3";
     UiTestPump.Wait((System.Threading.Tasks.Task)method.Invoke(form,new object[]{"Activity",new Func<DialogResult>(()=>DialogResult.Cancel)}));Assert.Equal(0,controller.Applied);Assert.Equal(3.0,UiBindingTests.Find<ChannelControl>(form,"CpuChannel").GetDraft().Animation.FixedFps);
     UiTestPump.Wait((System.Threading.Tasks.Task)method.Invoke(form,new object[]{"Thermal",new Func<DialogResult>(()=>DialogResult.No)}));Assert.Equal("Thermal",store.Load().Settings.ActiveProfileName);Assert.Equal(1,controller.Applied);Assert.Equal("TCPU",controller.Configuration.Cpu.Animation.SensorId);
    }
   }
  }
  [Test] public static void Everyday_ProfileRowsIdentifyTheSensorAndOfferDirectActivation() {
   using(var profiles=new ProfilesControl()) {
    profiles.SetProfiles(AppSettings.Defaults(),"Activity");
    var button=All(profiles).OfType<Button>().FirstOrDefault(b=>b.Text=="Use profile");Assert.True(button!=null,"Direct profile activation is missing");
    var detail=UiBindingTests.Find<Label>(profiles,"ProfileDescription");Assert.True(detail.Text.Contains("usage"),"Activity description must identify usage");
    profiles.SetProfiles(AppSettings.Defaults(),"Thermal");Assert.True(detail.Text.Contains("temperature"),"Thermal description must identify temperature");
   }
  }
  [Test] public static void Everyday_UsingTheSameProfileDoesNotSilentlySaveItsDraft() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<TextBox>(cpu,"FixedFps").Text="3";
     UiTestPump.Wait(form.ActivateProfile("Classic",()=>DialogResult.Cancel));Assert.Equal(0,controller.Applied);Assert.Equal(3.0,cpu.GetDraft().Animation.FixedFps);
     UiTestPump.Wait(form.ActivateProfile("Classic",()=>DialogResult.No));Assert.Equal(2.0,cpu.GetDraft().Animation.FixedFps);Assert.Equal(1,controller.Applied);
    }
   }
  }
  [Test] public static void Everyday_SensorPickerSeparatesReadableLabelFromTechnicalIdentity() {
   var snapshot=AidaParserTests.Parse("<temp><id>TCPUDIO</id><label>CPU Diode</label><value>50</value></temp><temp><id>TGPU1</id><label>GPU</label><value>35</value></temp>").Snapshot;
   using(var picker=new SensorPicker(true)) {
    picker.SetSnapshot(snapshot);picker.SelectedId="TCPUDIO";
    var combo=UiBindingTests.Find<ComboBox>(picker,"SensorChoice");Assert.True(combo.Text.Contains("CPU Diode"));Assert.True(!combo.Text.Contains("TCPUDIO"),"Technical ID should not dominate a unique sensor label");
    picker.SetSnapshot(snapshot);Assert.Equal("TCPUDIO",picker.SelectedId);
    Assert.True(All(picker).OfType<Label>().Any(l=>l.Text.Contains("TCPUDIO")),"Selected identity remains available in secondary details");
   }
  }
 }
}
