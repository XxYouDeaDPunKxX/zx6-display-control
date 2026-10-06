using System;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace ZX6DisplayControl {
 public sealed class SensorPicker:UserControl {
  private readonly bool temperaturesOnly;
  private readonly TextBox search=new TextBox{Name="SensorSearch",Dock=DockStyle.Fill,AccessibleName="Search by sensor name or ID"};
  private readonly ComboBox categories=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,AccessibleName="Sensor category"};
  private readonly ComboBox choice=new ComboBox{Name="SensorChoice",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,IntegralHeight=false,DropDownHeight=260,AccessibleName="Sensor"};
  private readonly Label info=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.DimGray};
  private readonly LinkLabel find=new LinkLabel{Text="Search…",AutoSize=true,Dock=DockStyle.Right,AccessibleName="Search or filter sensors"};
  private bool expanded,available=true;
  private SensorSnapshot snapshot;private string selectedId;private bool updating;
  private readonly string[] kinds={null,"temp","sys","fan","duty","volt","curr","pwr"};
  public event EventHandler SelectionChanged;
  public SensorPicker(bool temperaturesOnly) {
   this.temperaturesOnly=temperaturesOnly;Height=50;MinimumSize=new Size(240,50);Margin=new Padding(0,0,0,6);
   choice.AccessibleDescription=temperaturesOnly?"Choose the AIDA64 temperature for this side's number. The holder displays whole degrees from 0 to 99 °C; fractions are truncated.":"Choose the exported AIDA64 sensor that controls this bar's speed or level. The temperature number uses its own sensor.";
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
   layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   layout.Controls.Add(choice,0,0);layout.SetColumnSpan(choice,2);layout.Controls.Add(info,0,1);layout.Controls.Add(find,1,1);layout.Controls.Add(search,0,2);layout.Controls.Add(categories,1,2);Controls.Add(layout);search.Visible=false;categories.Visible=false;
   find.LinkClicked+=(s,e)=>{expanded=!expanded;search.Visible=expanded;categories.Visible=expanded && !temperaturesOnly;layout.SetColumnSpan(search,temperaturesOnly?2:1);Height=MinimumSize.Height+(expanded?search.PreferredHeight+8:0);find.Text=expanded?"Clear search":"Search…";if(expanded)search.Focus();else{search.Text="";categories.SelectedIndex=temperaturesOnly?1:0;}};
   categories.Items.AddRange(new object[]{"All categories","Temperatures","System","Fans","Duty cycle","Voltages","Currents","Power"});categories.SelectedIndex=temperaturesOnly?1:0;categories.Enabled=!temperaturesOnly;
   search.TextChanged+=(s,e)=>Populate();categories.SelectedIndexChanged+=(s,e)=>Populate();choice.DropDown+=(s,e)=>{choice.DropDownWidth=Math.Max(choice.Width,650);};
   // CLOSEUP can precede SELCHANGE. Rebuilding here would erase the native
   // selection before its notification reaches SelectedIndexChanged.
   choice.SelectedIndexChanged+=(s,e)=>{
    if(updating) return;var item=choice.SelectedItem as Choice;if(item==null)return;
    SensorValue row;
    if(snapshot==null || !snapshot.Values.TryGetValue(item.Id,out row) || !row.Number.HasValue) {Populate();return;}
    selectedId=item.Id;ShowInfo();if(SelectionChanged!=null) SelectionChanged(this,EventArgs.Empty);
   };
  }
  public string SelectedId {get{return selectedId;}set{selectedId=value;Populate();}}
  public SensorValue SelectedSensor {get{SensorValue value;return snapshot!=null && selectedId!=null && snapshot.Values.TryGetValue(selectedId,out value)?value:null;}}
  public void SetSnapshot(SensorSnapshot value,bool deferStructure=false) {snapshot=value;if(!deferStructure && !choice.DroppedDown) Populate();else ShowInfo();}
  public void SetAvailable(bool value) {available=value;ShowInfo();}
  private void Populate() {
   if(updating) return;updating=true;
   try {
    var items=SensorCatalog.Filter(snapshot,search.Text,kinds[Math.Max(0,categories.SelectedIndex)],temperaturesOnly).Where(v=>v.Number.HasValue).Select(v=>new Choice(v.Id,Describe(v))).ToList();
    if(!string.IsNullOrEmpty(selectedId) && !items.Any(i=>i.Id==selectedId)) {
     var selected=SelectedSensor;items.Insert(0,new Choice(selectedId,selected==null?selectedId+" · unavailable":Describe(selected)));
    }
    bool same=choice.Items.Count==items.Count && choice.Items.Cast<Choice>().Zip(items,(before,after)=>before.Id==after.Id && before.ToString()==after.ToString()).All(equal=>equal);
    if(!same) {
     choice.BeginUpdate();
     try {choice.Items.Clear();choice.Items.AddRange(items.Cast<object>().ToArray());}
     finally {choice.EndUpdate();}
    }
    choice.SelectedIndex=items.FindIndex(i=>i.Id==selectedId);ShowInfo();
   } finally {updating=false;}
  }
  private static string Describe(SensorValue value) {return SensorPresentation.Describe(value);}
  private void ShowInfo() {
   var s=SelectedSensor;bool invalid=temperaturesOnly && s!=null && (!s.Number.HasValue || s.Number.Value<0 || s.Number.Value>99);
   info.Text=s==null?(string.IsNullOrEmpty(selectedId)?"Select an exported AIDA64 sensor":"Unavailable · export this sensor in AIDA64"):(!available?"Last reading: ":"")+(s.Number.HasValue?s.Number.Value.ToString("0.##",CultureInfo.CurrentCulture):"Non-numeric value")+" "+(s.Unit??"· unit unknown")+(invalid?" · display needs 0–99 °C":"");
   info.ForeColor=invalid || s==null?UiTheme.Warning:UiTheme.Muted;
  }
  private sealed class Choice {public readonly string Id;private readonly string label;public Choice(string id,string label){Id=id;this.label=label;}public override string ToString(){return label;}}
 }
 internal static class SensorPresentation {
  public static string Kind(SensorValue value) {
   switch(value.Kind) {case "temp":return "Temperature";case "fan":return "Fan speed";case "duty":return "Fan duty";case "volt":return "Voltage";case "curr":return "Current";case "pwr":return "Power";case "sys":return value.Unit=="%"?"Usage":"System";default:return value.Kind;}
  }
  public static string Describe(SensorValue value) {return value.Label+" · "+Kind(value)+(value.Unit==null?"":" ("+value.Unit+")")+" · "+value.Id;}
 }
}
