using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace ZX6DisplayControl {
 public sealed class ChannelControl:UserControl {
  private readonly SensorPicker temperature=new SensorPicker(true){Name="TemperatureSource"},source=new SensorPicker(false){Name="AnimationSource"};
  private readonly ComboBox mode=Combo("Mode","Animated bar · speed","Sensor meter · level","Fixed level","Playlist");
  private readonly PlaylistEditor playlist=new PlaylistEditor{Name="PlaylistEditor"};
  private readonly ComboBox sequence=Combo("Sequence","Fill","Empty","Bounce","Random");
  private readonly ComboBox rate=Combo("RateMode","Fixed","Sensor-driven");
  private readonly ComboBox smoothing=Combo("FilterSeconds","Off","1 second","2 seconds","5 seconds");
  private readonly ComboBox simplePreset=Combo("SimplePreset","Slow","Normal","Fast","Follow usage","Follow temperature","Custom");
  private readonly CheckBox advanced=new CheckBox{Name="AdvancedOptions",Text="Advanced settings",AutoSize=true};
  private readonly Label explanation=new Label{Name="PresetExplanation",AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.DimGray};
  private readonly TextBox inputMin=Field("InputMin"),inputMax=Field("InputMax"),fixedFps=Field("FixedFps"),minFps=Field("MinFps"),maxFps=Field("MaxFps"),hysteresis=Field("HysteresisPercent");
  private readonly NumericUpDown fixedFrame=new NumericUpDown{Name="FixedFrame",Minimum=0,Maximum=7,Dock=DockStyle.Fill};
  private readonly CheckBox invert=new CheckBox{Text="Invert sensor response",AutoSize=true};
  private readonly CheckBox pause=new CheckBox{Text="Pause animation",AutoSize=true};
  private readonly CheckBox confirmRange=new CheckBox{Text="Use this sensor range",AutoSize=true};
  private readonly Label error=new Label{AutoSize=true,ForeColor=Color.Firebrick,Dock=DockStyle.Fill};
  private readonly Label preview=new Label{Name="BarResponse",AutoSize=true,Dock=DockStyle.Fill,Text="Waiting for sensor data",AccessibleDescription="The reading and resulting bar output. A meter has seven lit segments plus empty. Smoothing delays changes; hysteresis holds near a boundary to prevent flicker. An animated bar uses the reading to set speed instead of height. Preview response refers to unsaved changes."};
  private readonly TableLayoutPanel table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(14)};
  private readonly Dictionary<Control,Label> captions=new Dictionary<Control,Label>();
  private bool loading,unknownRange;private string sourceUnit;private readonly bool isGpu;private AnimationOutput lastPreview;private readonly double[] filterValues={0,1,2,5};
  public event EventHandler DraftChanged;
  public event Action CopyRequested;
  public ChannelControl(bool gpu=false) {
   isGpu=gpu;
   temperature.GpuChannel=source.GpuChannel=gpu;
   AutoScroll=true;BackColor=Color.White;table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(table);
   table.SizeChanged+=(s,e)=>preview.MaximumSize=new Size(Math.Max(100,table.ClientSize.Width-table.GetColumnWidths()[0]-table.Padding.Horizontal-8),0);
   AddSection("Temperature number");AddRow("Sensor",temperature);AddSection("Bar behavior");AddRow("Behavior",mode);AddRow("Pattern",sequence);AddRow("Speed preset",simplePreset);AddRow("",explanation);AddRow("Control sensor",source);AddRow("Level",fixedFrame);AddRow("Effects",playlist);AddRow("",advanced);AddRow("Speed control",rate);AddRow("Speed (fps)",fixedFps);
   AddRow("Minimum input",inputMin);AddRow("Maximum input",inputMax);AddRow("Minimum speed (fps)",minFps);AddRow("Maximum speed (fps)",maxFps);
   AddRow("Response time",smoothing);AddRow("Level hysteresis (%)",hysteresis);AddRow("",invert);AddRow("",confirmRange);AddRow("",pause);AddRow("Response",preview);AddRow("",error);
   var copy=new Button{Name="CopyAnimation",Text=gpu?"Copy CPU animation":"Copy GPU animation",AutoSize=true,AccessibleDescription="Copy the other side's bar behavior into this draft. Keep this side's temperature, control sensor and input range. Save changes to activate it."};copy.Click+=(s,e)=>{if(CopyRequested!=null)CopyRequested();};AddRow("",copy);
   foreach(var field in new[]{inputMin,inputMax,fixedFps,minFps,maxFps,hysteresis}) field.TextChanged+=(s,e)=>Changed();
   foreach(var combo in new[]{sequence,smoothing}) combo.SelectedIndexChanged+=(s,e)=>Changed();
   rate.SelectedIndexChanged+=(s,e)=>{if(!loading){RepairInactiveFields();Changed();}};
   mode.SelectedIndexChanged+=(s,e)=>ModeChanged();simplePreset.SelectedIndexChanged+=(s,e)=>{if(!loading)ApplyPreset(simplePreset.SelectedItem as string);};
   advanced.CheckedChanged+=(s,e)=>{if(!loading){UpdateVisibility();if(lastPreview!=null)SetPreview(lastPreview);}};
   fixedFrame.ValueChanged+=(s,e)=>Changed();invert.CheckedChanged+=(s,e)=>Changed();pause.CheckedChanged+=(s,e)=>Changed();confirmRange.CheckedChanged+=(s,e)=>Changed();
   playlist.Changed+=(s,e)=>Changed();
   temperature.SelectionChanged+=(s,e)=>{if(!loading && (simplePreset.SelectedItem as string)=="Follow temperature")ApplyPreset("Follow temperature");else Changed();};source.SelectionChanged+=(s,e)=>SourceChanged();
   Load(new ChannelSettings{TemperatureId="TCPU"},null);
  }
  private void AddSection(string text) {
   int row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   var label=new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=SystemInformation.HighContrast?SystemColors.ControlText:Color.FromArgb(27,67,93),Margin=new Padding(0,row==0?0:8,0,8)};
   table.Controls.Add(label,0,row);table.SetColumnSpan(label,2);
  }
  private void AddRow(string caption,Control control) {
   int row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   var label=new Label{Text=caption,AutoSize=true,MaximumSize=new Size(128,0),Margin=new Padding(0,6,12,4)};control.Margin=new Padding(0,3,0,4);control.Dock=DockStyle.Top;
   if(!(control is Label) && !string.IsNullOrWhiteSpace(caption))control.AccessibleName=caption;
   table.Controls.Add(label,0,row);table.Controls.Add(control,1,row);captions[control]=label;
  }
  private static ComboBox Combo(string name,params string[] values) {var box=new ComboBox{Name=name,DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};box.Items.AddRange(values);box.SelectedIndex=0;return box;}
  private static TextBox Field(string name) {return new TextBox{Name=name,Dock=DockStyle.Fill};}
  private static double Number(TextBox box) {double value;if(!double.TryParse(box.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out value) || !AnimationSettings.Finite(value)) throw new FormatException("Enter a valid number for "+box.AccessibleName+".");return value;}
  public ChannelSettings GetDraft() {
   return new ChannelSettings{TemperatureId=temperature.SelectedId,Paused=pause.Checked,Animation=new AnimationSettings{Mode=(AnimationMode)mode.SelectedIndex,Sequence=(SequenceKind)sequence.SelectedIndex,RateMode=(AnimationRateMode)rate.SelectedIndex,SensorId=source.SelectedId,InputMin=Number(inputMin),InputMax=Number(inputMax),FixedFps=Number(fixedFps),MinFps=Number(minFps),MaxFps=Number(maxFps),FilterSeconds=filterValues[Math.Max(0,smoothing.SelectedIndex)],HysteresisPercent=Number(hysteresis),FixedFrame=(int)fixedFrame.Value,Invert=invert.Checked,Steps=playlist.GetSteps()}};
  }
  public IReadOnlyList<string> ValidationErrors() {
   var errors=new List<string>();try {var c=GetDraft();if(string.IsNullOrWhiteSpace(c.TemperatureId)) errors.Add("Choose a display temperature.");errors.AddRange(c.Animation.Validate());if(c.Animation.NeedsSource && unknownRange && !confirmRange.Checked) errors.Add("Review and confirm this sensor range in Advanced settings.");} catch(FormatException e) {errors.Add(e.Message);}return errors.AsReadOnly();
  }
  public new void Load(ChannelSettings settings,SensorSnapshot catalog) {
   loading=true;try {
    var a=settings.Animation;playlist.SetSteps(a.Steps);temperature.SetSnapshot(catalog);temperature.SelectedId=settings.TemperatureId;source.SetSnapshot(catalog);source.SelectedId=a.SensorId;
    mode.SelectedIndex=(int)a.Mode;sequence.SelectedIndex=(int)a.Sequence;rate.SelectedIndex=(int)a.RateMode;smoothing.SelectedIndex=Array.IndexOf(filterValues,a.FilterSeconds);
    inputMin.Text=a.InputMin.ToString(CultureInfo.CurrentCulture);inputMax.Text=a.InputMax.ToString(CultureInfo.CurrentCulture);fixedFps.Text=a.FixedFps.ToString(CultureInfo.CurrentCulture);minFps.Text=a.MinFps.ToString(CultureInfo.CurrentCulture);maxFps.Text=a.MaxFps.ToString(CultureInfo.CurrentCulture);hysteresis.Text=a.HysteresisPercent.ToString(CultureInfo.CurrentCulture);
    fixedFrame.Value=a.FixedFrame;invert.Checked=a.Invert;pause.Checked=settings.Paused;unknownRange=false;confirmRange.Checked=false;advanced.Checked=false;sourceUnit=source.SelectedSensor==null?null:source.SelectedSensor.Unit;SetPresetChoices();simplePreset.SelectedItem=AnimationPresets.Match(a,isGpu,settings.TemperatureId);
   } finally {loading=false;}UpdateVisibility();error.Text="";
  }
  public void SetCatalog(SensorSnapshot catalog,bool deferStructure=false) {temperature.SetSnapshot(catalog,deferStructure);source.SetSnapshot(catalog,deferStructure);if(deferStructure)return;var unit=source.SelectedSensor==null?null:source.SelectedSensor.Unit;sourceUnit=unit;captions[inputMin].Text="Minimum input"+(unit==null?"":" ("+unit+")");captions[inputMax].Text="Maximum input"+(unit==null?"":" ("+unit+")");UpdateExplanation();}
  public void SetAvailable(bool value) {temperature.SetAvailable(value);source.SetAvailable(value);}
  public void SetPreview(AnimationOutput output,bool draft=false) {
   captions[preview].Text=draft?"Preview response":"Bar response";
   if(output==null) {lastPreview=null;preview.Text="No display output";return;}lastPreview=output;
   AnimationSettings settings;try{settings=GetDraft().Animation;}catch(FormatException){preview.Text="Fix the highlighted settings to preview the response.";return;}
   var sensor=source.SelectedSensor;string label=sensor==null?(source.SelectedId??"Control sensor"):sensor.Label,unit=sensor==null || string.IsNullOrEmpty(sensor.Unit)?"":" "+sensor.Unit;
   string level=output.Frame+" of 7 segments",pattern=new[]{"Fill","Empty","Bounce","Random"}[(int)settings.Sequence];
   if(output.SourceMissing){preview.Text=label+" unavailable · holding "+level+".\nExport this sensor in AIDA64 or select another control sensor.";return;}
   if(pause.Checked && settings.Mode!=AnimationMode.Fixed){preview.Text="Paused at "+level+".\nTemperature updates continue.";return;}
   if(settings.Mode==AnimationMode.Fixed){preview.Text="Fixed at "+level+".\nTemperature updates continue.";return;}
   if(settings.Mode==AnimationMode.Playlist){int index=output.PlaylistStepNumber.GetValueOrDefault()-1;if(settings.Steps==null || index<0 || index>=settings.Steps.Count){preview.Text="Updating playlist…";return;}var step=settings.Steps[index];preview.Text="Effect "+output.PlaylistStepNumber+" of "+settings.Steps.Count+" · "+new[]{"Fill","Empty","Bounce","Random"}[(int)step.Pattern]+"\n"+Format(output.FramesPerSecond)+" updates/s · "+step.Seconds+" s per effect";return;}
   if(!settings.NeedsSource){preview.Text=pattern+" · "+Format(output.FramesPerSecond)+" updates/s\nConstant speed";return;}
   string result=settings.Mode==AnimationMode.SensorLevel?level:Format(output.FramesPerSecond)+" updates/s · "+pattern;
   var lines=new List<string>{label+": "+Format(output.RawValue)+unit+" → "+result};
   if(settings.FilterSeconds>0)lines.Add("Smoothed: "+Format(output.FilteredValue)+unit+" · response "+Format(settings.FilterSeconds)+" s");
   lines.Add("Range: "+Format(settings.InputMin)+"–"+Format(settings.InputMax)+unit+(settings.Invert?" · inverted":""));
   if(output.HeldByHysteresis)lines.Add("Held near a level boundary to prevent flicker.");else if(output.Clamped)lines.Add("Outside the range · output limited to the nearest end.");
   preview.Text=string.Join(Environment.NewLine,lines);
  }
  private static string Format(double? value) {return value.HasValue?value.Value.ToString("0.##",CultureInfo.CurrentCulture):"—";}
  public void CopyAnimationFrom(ChannelSettings other,SensorSnapshot catalog) {
   var own=GetDraft();string id=own.Animation.SensorId;double min=own.Animation.InputMin,max=own.Animation.InputMax;
   own.Animation=other.Animation.Copy();own.Animation.SensorId=id;own.Animation.InputMin=min;own.Animation.InputMax=max;own.Paused=other.Paused;
   Load(own,catalog);Changed();
  }
  private void VisibleRow(Control control,bool visible) {control.Visible=visible;captions[control].Visible=visible;}
  private void UpdateVisibility() {
   bool cycle=mode.SelectedIndex==0,sensor=mode.SelectedIndex==1 || (cycle && rate.SelectedIndex==1);
   bool details=advanced.Checked;VisibleRow(sequence,cycle);VisibleRow(simplePreset,mode.SelectedIndex<2);VisibleRow(explanation,mode.SelectedIndex<2);VisibleRow(advanced,mode.SelectedIndex<2);VisibleRow(playlist,mode.SelectedIndex==3);VisibleRow(rate,cycle && details);VisibleRow(fixedFps,cycle && rate.SelectedIndex==0 && details);
   captions[simplePreset].Text=cycle?"Animation speed":"Meter source";
   VisibleRow(source,sensor);foreach(var control in new Control[]{inputMin,inputMax,smoothing,invert}) VisibleRow(control,sensor && details);
   VisibleRow(minFps,cycle && sensor && details);VisibleRow(maxFps,cycle && sensor && details);VisibleRow(hysteresis,mode.SelectedIndex==1 && details);VisibleRow(fixedFrame,mode.SelectedIndex==2);VisibleRow(confirmRange,sensor && unknownRange && details);
   VisibleRow(pause,mode.SelectedIndex!=2);
   UpdateExplanation();
  }
  private void UpdateExplanation() {
   string preset=simplePreset.SelectedItem as string;bool sensor=mode.SelectedIndex==1 || (mode.SelectedIndex==0 && rate.SelectedIndex==1);
   string unit=source.SelectedSensor==null?null:source.SelectedSensor.Unit;
   explanation.Text=preset=="Follow usage"?(mode.SelectedIndex==1?"Level follows ":"Speed follows ")+(isGpu?"GPU usage.":"CPU usage."):preset=="Follow temperature"?(mode.SelectedIndex==1?"Level follows ":"Speed follows ")+"the display temperature (30–80 °C).":preset=="Custom"?(sensor?"Sensor range: "+inputMin.Text+"–"+inputMax.Text+(string.IsNullOrEmpty(unit)?"":" "+unit)+".":"Custom speed."):"Constant speed · "+fixedFps.Text+" fps.";
  }
  private void SetPresetChoices() {simplePreset.Items.Clear();if(mode.SelectedIndex==0)simplePreset.Items.AddRange(new object[]{"Slow","Normal","Fast"});simplePreset.Items.AddRange(new object[]{"Follow usage","Follow temperature","Custom"});}
  private void ModeChanged() {
   if(loading)return;
   loading=true;try {
    SetPresetChoices();
    if(mode.SelectedIndex==3)playlist.EnsureDefaults();
    if(mode.SelectedIndex==1 && string.IsNullOrEmpty(source.SelectedId))source.SelectedId=isGpu?"SGPU1UTI":"SCPUUTI";
   }finally{loading=false;}
   RepairInactiveFields();Changed();
  }
  private static bool InRange(TextBox box,double min,double max) {try{double value=Number(box);return value>=min && value<=max;}catch(FormatException){return false;}}
  private void RepairInactiveFields() {
   loading=true;try {
    bool cycle=mode.SelectedIndex==0,sensor=mode.SelectedIndex==1 || (cycle && rate.SelectedIndex==1);
    if(!(cycle && rate.SelectedIndex==0) && !InRange(fixedFps,1,4))fixedFps.Text="2";
    if(!(cycle && rate.SelectedIndex==1) && (!InRange(minFps,1,4) || !InRange(maxFps,1,4) || Number(minFps)>Number(maxFps))){minFps.Text="1";maxFps.Text="4";}
    if(mode.SelectedIndex!=1 && !InRange(hysteresis,0,10))hysteresis.Text="2";
    if(!sensor) {
     bool valid;try{double min=Number(inputMin),max=Number(inputMax);valid=min<max && AnimationSettings.Finite(max-min);}catch(FormatException){valid=false;}
     if(!valid){inputMin.Text="0";inputMax.Text="100";}
    }
   }finally{loading=false;}
  }
  private void ApplyPreset(string name) {
   if(name=="Custom"){advanced.Checked=true;Changed();return;}
   if(name==null)return;
   var a=AnimationPresets.Create(name,(AnimationMode)mode.SelectedIndex,(SequenceKind)sequence.SelectedIndex,isGpu,temperature.SelectedId);
   loading=true;try {
    rate.SelectedIndex=(int)a.RateMode;source.SelectedId=a.SensorId;inputMin.Text=a.InputMin.ToString(CultureInfo.CurrentCulture);inputMax.Text=a.InputMax.ToString(CultureInfo.CurrentCulture);fixedFps.Text=a.FixedFps.ToString(CultureInfo.CurrentCulture);minFps.Text=a.MinFps.ToString(CultureInfo.CurrentCulture);maxFps.Text=a.MaxFps.ToString(CultureInfo.CurrentCulture);smoothing.SelectedIndex=Array.IndexOf(filterValues,a.FilterSeconds);hysteresis.Text=a.HysteresisPercent.ToString(CultureInfo.CurrentCulture);invert.Checked=false;unknownRange=false;confirmRange.Checked=false;
    sourceUnit=source.SelectedSensor==null?null:source.SelectedSensor.Unit;
   }finally{loading=false;}Changed();
  }
  private void SourceChanged() {
   if(loading)return;string unit=source.SelectedSensor==null?null:source.SelectedSensor.Unit;
   loading=true;try {
    unknownRange=unit!="%" && unit!="°C";confirmRange.Checked=false;
    if(unit!=sourceUnit && !unknownRange){inputMin.Text=unit=="%"?"0":"30";inputMax.Text=unit=="%"?"100":"80";}
    sourceUnit=unit;
   }finally{loading=false;}Changed();
  }
  private void Changed() {
   if(loading)return;loading=true;try{simplePreset.SelectedItem=AnimationPresets.Match(GetDraft().Animation,isGpu,temperature.SelectedId);}catch(FormatException){simplePreset.SelectedItem="Custom";}finally{loading=false;}
   UpdateVisibility();error.Text=string.Join(Environment.NewLine,ValidationErrors());if(DraftChanged!=null)DraftChanged(this,EventArgs.Empty);
  }
 }
}
