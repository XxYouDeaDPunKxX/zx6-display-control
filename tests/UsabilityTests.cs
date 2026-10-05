using System;
using System.Drawing;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class UsabilityTests {
  [Test] public static void Usability_ModeAndRateChangesRepairOnlyInvalidInactiveFields() {
   using(var c=new ChannelControl()) {
    UiBindingTests.Find<ComboBox>(c,"SimplePreset").SelectedItem="Follow usage";
    UiBindingTests.Find<TextBox>(c,"InputMin").Text="10";
    UiBindingTests.Find<TextBox>(c,"MinFps").Text="9";
    UiBindingTests.Find<ComboBox>(c,"Mode").SelectedIndex=1;
    Assert.Equal(0,c.ValidationErrors().Count);Assert.Equal(10.0,c.GetDraft().Animation.InputMin);
    UiBindingTests.Find<TextBox>(c,"HysteresisPercent").Text="-1";
    UiBindingTests.Find<ComboBox>(c,"Mode").SelectedIndex=0;
    Assert.Equal(0,c.ValidationErrors().Count);Assert.Equal(10.0,c.GetDraft().Animation.InputMin);
    UiBindingTests.Find<TextBox>(c,"InputMin").Text="200";
    UiBindingTests.Find<ComboBox>(c,"RateMode").SelectedIndex=0;
    Assert.Equal(0,c.ValidationErrors().Count);
    UiBindingTests.Find<TextBox>(c,"FixedFps").Text="invalid";
    UiBindingTests.Find<ComboBox>(c,"RateMode").SelectedIndex=1;
    Assert.Equal(0,c.ValidationErrors().Count);
   }
  }
  [Test] public static void Usability_UnknownSensorRequiresExplicitRangeWithoutOpeningDetails() {
   var snapshot=AidaParserTests.Parse(AidaParserTests.Cpu+"<fan><id>FGPU1</id><label>GPU</label><value>900</value></fan>").Snapshot;
   using(var c=new ChannelControl()) {
    c.SetCatalog(snapshot);UiBindingTests.Find<ComboBox>(c,"SimplePreset").SelectedItem="Follow usage";
    var choice=UiBindingTests.Find<ComboBox>(UiBindingTests.Find<SensorPicker>(c,"AnimationSource"),"SensorChoice");
    for(int i=0;i<choice.Items.Count;i++)if(choice.Items[i].ToString().Contains("FGPU1"))choice.SelectedIndex=i;
    Assert.True(c.ValidationErrors().Count>0);Assert.True(!UiBindingTests.Find<CheckBox>(c,"AdvancedOptions").Checked,"Expert fields open only when requested");
   }
  }
  [Test] public static void Usability_LateCatalogPreservesCustomBoundsWhenChoosingSameUnit() {
   var snapshot=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><label>GPU</label><value>33</value></temp>").Snapshot;
   var value=new ChannelSettings{TemperatureId="TCPU"};value.Animation.RateMode=AnimationRateMode.Sensor;value.Animation.SensorId="TCPU";value.Animation.InputMin=20;value.Animation.InputMax=70;
   using(var channel=new ChannelControl()) {
    channel.Load(value,null);channel.SetCatalog(snapshot);
    var choice=UiBindingTests.Find<ComboBox>(UiBindingTests.Find<SensorPicker>(channel,"AnimationSource"),"SensorChoice");
    for(int i=0;i<choice.Items.Count;i++)if(choice.Items[i].ToString().Contains("TGPU1"))choice.SelectedIndex=i;
    Assert.Equal("TGPU1",channel.GetDraft().Animation.SensorId);Assert.Equal(20.0,channel.GetDraft().Animation.InputMin);Assert.Equal(70.0,channel.GetDraft().Animation.InputMax);
   }
  }
  [Test] public static void Usability_SwitchingToFixedLevelDoesNotKeepHiddenInvalidBounds() {
   using(var channel=new ChannelControl()) {
    UiBindingTests.Find<ComboBox>(channel,"SimplePreset").SelectedItem="Follow usage";
    UiBindingTests.Find<TextBox>(channel,"InputMin").Text="200";
    UiBindingTests.Find<ComboBox>(channel,"Mode").SelectedIndex=2;
    Assert.Equal(0,channel.ValidationErrors().Count);
   }
  }
  [Test] public static void Usability_SensorPresetShowsPickerWithoutTechnicalFields() {
   var snapshot=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><label>GPU</label><value>33</value></temp><sys><id>SCPUUTI</id><label>CPU utilization</label><value>25</value></sys>").Snapshot;
   using(var form=new Form())using(var channel=new ChannelControl()) {
    form.Controls.Add(channel);form.Location=new Point(-20000,-20000);form.StartPosition=FormStartPosition.Manual;form.Show();channel.SetCatalog(snapshot);
    UiBindingTests.Find<ComboBox>(channel,"SimplePreset").SelectedItem="Follow usage";
    var source=UiBindingTests.Find<SensorPicker>(channel,"AnimationSource");Assert.True(source.Visible,"A sensor can be chosen without opening advanced numeric settings");
    var choice=UiBindingTests.Find<ComboBox>(source,"SensorChoice");for(int i=0;i<choice.Items.Count;i++)if(choice.Items[i].ToString().Contains("TGPU1"))choice.SelectedIndex=i;
    Assert.Equal("TGPU1",channel.GetDraft().Animation.SensorId);Assert.Equal(30.0,channel.GetDraft().Animation.InputMin);Assert.Equal(80.0,channel.GetDraft().Animation.InputMax);
    Assert.True(!UiBindingTests.Find<TextBox>(channel,"InputMin").Visible,"A temperature choice should not force expert settings");
   }
  }
  [Test] public static void Usability_SensorSelectionIsNumericAndSearchIsOptional() {
   var snapshot=AidaParserTests.Parse(AidaParserTests.Cpu+"<sys><id>SNAME</id><value>CPU name</value></sys><sys><id>SCPUUTI</id><label>CPU utilization</label><value>25</value></sys>").Snapshot;
   using(var form=new Form())using(var picker=new SensorPicker(false)) {
    form.Location=new Point(-20000,-20000);form.StartPosition=FormStartPosition.Manual;form.Controls.Add(picker);form.Show();picker.SetSnapshot(snapshot);
    Assert.True(!UiBindingTests.Find<TextBox>(picker,"SensorSearch").Visible,"Search should be optional");
    var choice=UiBindingTests.Find<ComboBox>(picker,"SensorChoice");for(int i=0;i<choice.Items.Count;i++)Assert.True(!choice.Items[i].ToString().Contains("SNAME"),"Text rows are not selectable sensor values");
    for(int i=0;i<choice.Items.Count;i++)if(choice.Items[i].ToString().Contains("SCPUUTI"))choice.SelectedIndex=i;
    picker.SetSnapshot(snapshot);Assert.Equal("SCPUUTI",picker.SelectedId);
   }
  }
  [Test] public static void Usability_ChosenSensorSurvivesRefreshAndIsApplied() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");UiBindingTests.Find<ComboBox>(cpu,"SimplePreset").SelectedItem="Follow usage";UiBindingTests.Find<CheckBox>(cpu,"AdvancedOptions").Checked=true;
     var source=UiBindingTests.Find<SensorPicker>(cpu,"AnimationSource");var choice=UiBindingTests.Find<ComboBox>(source,"SensorChoice");
     for(int i=0;i<choice.Items.Count;i++)if(choice.Items[i].ToString().Contains("TGPU1"))choice.SelectedIndex=i;
     cpu.SetCatalog(rig.Session.State.Snapshot);Assert.Equal("TGPU1",source.SelectedId);Assert.True(form.CanApply);UiTestPump.Wait(form.ApplyChanges());Assert.Equal("TGPU1",controller.Configuration.Cpu.Animation.SensorId);
    }
   }
  }
  [Test] public static void Usability_PresetsResolveIndependentSensorsAndValidBounds() {
   foreach(bool gpu in new[]{false,true})foreach(var mode in new[]{AnimationMode.Cycle,AnimationMode.SensorLevel}) {
    var activity=AnimationPresets.Create("Follow usage",mode,SequenceKind.Random,gpu,"TCPU");
    Assert.Equal(gpu?"SGPU1UTI":"SCPUUTI",activity.SensorId);Assert.Equal(0.0,activity.InputMin);Assert.Equal(100.0,activity.InputMax);Assert.Equal(SequenceKind.Random,activity.Sequence);Assert.Equal(0,activity.Validate().Count);
    var thermal=AnimationPresets.Create("Follow temperature",mode,SequenceKind.Forward,gpu,"TCUSTOM");Assert.Equal("TCUSTOM",thermal.SensorId);Assert.Equal(30.0,thermal.InputMin);Assert.Equal(80.0,thermal.InputMax);Assert.Equal("Follow temperature",AnimationPresets.Match(thermal,gpu,"TCUSTOM"));
   }
   Assert.Equal(1.0,AnimationPresets.Create("Slow",AnimationMode.Cycle,SequenceKind.Forward,false,"TCPU").FixedFps);
   Assert.Equal(2.0,AnimationPresets.Create("Normal",AnimationMode.Cycle,SequenceKind.Forward,false,"TCPU").FixedFps);
   Assert.Equal(4.0,AnimationPresets.Create("Fast",AnimationMode.Cycle,SequenceKind.Forward,false,"TCPU").FixedFps);
  }
  [Test] public static void Usability_BasicPresetsHideTechnicalFieldsAndNeverSendBeforeApply() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);rig.At(100);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
     var cpu=UiBindingTests.Find<ChannelControl>(form,"CpuChannel");var preset=UiBindingTests.Find<ComboBox>(cpu,"SimplePreset");preset.SelectedItem="Follow usage";
     Assert.True(!UiBindingTests.Find<TextBox>(cpu,"InputMin").Visible);Assert.True(!UiBindingTests.Find<ComboBox>(cpu,"RateMode").Visible);Assert.True(form.CanApply);Assert.Equal(0,controller.Applied);
     Assert.Equal("SCPUUTI",cpu.GetDraft().Animation.SensorId);UiTestPump.Wait(form.ApplyChanges());Assert.Equal(1,controller.Applied);
     var details=UiBindingTests.Find<CheckBox>(cpu,"AdvancedOptions");details.Checked=true;Assert.True(UiBindingTests.Find<TextBox>(cpu,"InputMin").Visible);
     UiBindingTests.Find<TextBox>(cpu,"InputMin").Text="10";details.Checked=false;Assert.Equal(10.0,cpu.GetDraft().Animation.InputMin);Assert.True(!UiBindingTests.Find<TextBox>(cpu,"InputMin").Visible);Assert.Equal(1,controller.Applied);
     form.CancelChanges();Assert.Equal(0.0,cpu.GetDraft().Animation.InputMin);Assert.True(!details.Checked);
    }
   }
  }
  [Test] public static void Usability_LoadingCustomConfigPreservesItWithoutOpeningDetails() {
   var value=new ChannelSettings{TemperatureId="TCPU"};value.Animation.RateMode=AnimationRateMode.Sensor;value.Animation.SensorId="CUSTOM";value.Animation.InputMin=11;value.Animation.InputMax=71;value.Animation.FilterSeconds=5;
   using(var c=new ChannelControl()) {c.Load(value,null);Assert.Equal(11.0,c.GetDraft().Animation.InputMin);Assert.Equal("CUSTOM",c.GetDraft().Animation.SensorId);Assert.True(!UiBindingTests.Find<CheckBox>(c,"AdvancedOptions").Checked);Assert.Equal("Custom",UiBindingTests.Find<ComboBox>(c,"SimplePreset").SelectedItem as string);}
  }
 }
}
