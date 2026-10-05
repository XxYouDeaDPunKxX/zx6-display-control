using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed class DiagnosticsControl:UserControl {
  private readonly Label status=new Label{AutoSize=true,Dock=DockStyle.Fill};
  private readonly Label devices=new Label{AutoSize=true,Dock=DockStyle.Fill,UseMnemonic=false,AccessibleName="Detected USB display"};
  private readonly ListView sensors=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,GridLines=false,AccessibleName="Exported AIDA64 sensors"};
  private readonly TextBox search=new TextBox{Name="CatalogSearch",Dock=DockStyle.Fill,AccessibleName="Search sensors by name or ID"};
  private readonly Label count=new Label{AutoSize=true,Dock=DockStyle.Fill};
  private readonly Label logStatus=new Label{AutoSize=true,Dock=DockStyle.Top};
  private readonly TextBox events=new TextBox{Name="RecentEvents",Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,AccessibleName="Recent app events",AccessibleDescription="The latest 200 events in this session. Select text and press Ctrl+C to copy. Export diagnostics saves the available log files and this history."};
  private long logRevision=-1;private DateTimeOffset? lastReadAt;private SensorSnapshot observedRead;
  private Button retry,export;
  public event Action RetryRequested,ExportRequested;
  private SensorSnapshot last;
  public DiagnosticsControl() {
   Padding=new Padding(18);BackColor=Color.White;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   layout.Controls.Add(status,0,0);layout.Controls.Add(devices,0,1);
   var pages=new ThemedTabControl{Dock=DockStyle.Fill};var sensorPage=new TabPage("Sensors");var eventPage=new TabPage("Events");pages.TabPages.Add(sensorPage);pages.TabPages.Add(eventPage);layout.Controls.Add(pages,0,2);
   var sensorLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};for(int i=0;i<3;i++)sensorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));sensorLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));sensorLayout.Controls.Add(new Label{Text="Search sensors",AutoSize=true},0,0);sensorLayout.Controls.Add(search,0,1);sensorLayout.Controls.Add(count,0,2);sensorLayout.Controls.Add(sensors,0,3);sensorPage.Controls.Add(sensorLayout);search.TextChanged+=(s,e)=>RefreshSensors();
   var eventLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};eventLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));eventLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));eventLayout.Controls.Add(logStatus,0,0);eventLayout.Controls.Add(events,0,1);eventPage.Controls.Add(eventLayout);
   foreach(string title in new[]{"ID","Name","Type","Value","Unit"}) sensors.Columns.Add(title,title=="Name"?240:110);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};retry=new Button{Text="Refresh connection",AutoSize=true};export=new Button{Text="Export diagnostics…",AutoSize=true};actions.Controls.Add(retry);actions.Controls.Add(export);layout.Controls.Add(actions,0,3);Controls.Add(layout);
   retry.Click+=(s,e)=>{if(RetryRequested!=null)RetryRequested();};export.Click+=(s,e)=>{if(ExportRequested!=null)ExportRequested();};
  }
  public void SetActionsEnabled(bool enabled){retry.Enabled=export.Enabled=enabled;}
  public void UpdateLog(EventLog log,string action) {
   logStatus.Text=log.LastError==null?(action==null?"Recent events · current session":action+"…"):"Log file unavailable: "+log.LastError;
   logStatus.ForeColor=log.LastError==null?UiTheme.Text:UiTheme.Warning;
   if(logRevision==log.Revision)return;logRevision=log.Revision;int start=events.SelectionStart,length=events.SelectionLength;bool preserve=events.Focused || length>0;
   events.Text=string.Join(Environment.NewLine,log.Recent());events.SelectionStart=preserve?Math.Min(start,events.TextLength):events.TextLength;events.SelectionLength=preserve?Math.Min(length,events.TextLength-events.SelectionStart):0;if(!preserve)events.ScrollToCaret();
  }
  public void UpdateState(SessionState value,bool? effectiveAvailability=null) {
   bool available=effectiveAvailability??(value.AidaStatus=="Ready" || value.AidaStatus=="TemperatureInvalid");
   if(value.Snapshot!=null && !object.ReferenceEquals(observedRead,value.Snapshot)){observedRead=value.Snapshot;lastReadAt=DateTimeOffset.Now;}
   status.Text="USB descriptor: Turing / UsbMonitor\nUSB 1A86:5722 · USB35INCHIPSV2 · "+(value.PortName??"no open port")+"\nValid reads: "+value.ValidReads+" · Read errors: "+value.ReadErrors+" · Writes: "+value.Writes+"\n"+(lastReadAt.HasValue?"Last received: "+lastReadAt.Value.ToString("HH:mm:ss")+" · ":"")+(available?"Unchanged values can be normal.":"Readings stopped · showing the last readings.");
   devices.Text=value.Devices.Count==0?"Z-X6 display: not detected":value.Devices.Count==1?"Z-X6 display: "+value.Devices[0]:"Multiple matching displays found. Connect one holder at a time.";
   if(value.Snapshot!=null && !object.ReferenceEquals(last,value.Snapshot)) {
    last=value.Snapshot;RefreshSensors();
   }
  }
  private void RefreshSensors() {
   var rows=SensorCatalog.Filter(last,search.Text,null,false).ToArray();
   var selected=sensors.SelectedItems.Cast<ListViewItem>().Select(x=>x.Text).ToArray();string focused=sensors.FocusedItem==null?null:sensors.FocusedItem.Text;
   string top=sensors.TopItem==null?null:sensors.TopItem.Text;
   bool same=sensors.Items.Cast<ListViewItem>().Select(x=>x.Text).SequenceEqual(rows.Select(x=>x.Id));
   sensors.BeginUpdate();try {
    if(!same)sensors.Items.Clear();
    for(int i=0;i<rows.Length;i++) {var row=rows[i];var values=new[]{row.Id,row.Label,SensorPresentation.Kind(row),row.RawValue,row.Unit??"unknown"};
     if(same){for(int c=1;c<values.Length;c++)sensors.Items[i].SubItems[c].Text=values[c];}
     else {var item=new ListViewItem(values){Selected=selected.Contains(row.Id),Focused=row.Id==focused};sensors.Items.Add(item);}
    }
    if(!same && top!=null){var item=sensors.Items.Cast<ListViewItem>().FirstOrDefault(x=>x.Text==top);if(item!=null)sensors.TopItem=item;}
   }finally{sensors.EndUpdate();}
   count.Text=rows.Length+" sensors"+(last!=null && rows.Length!=last.Values.Count?" of "+last.Values.Count+" · filtered":"");
  }
 }
}
