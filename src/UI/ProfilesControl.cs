using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed class ProfilesControl:UserControl {
  private readonly ListBox list=new ListBox{Dock=DockStyle.Fill,DisplayMember="Name",AccessibleName="Available profiles"};
  private readonly Label description=new Label{Name="ProfileDescription",AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,10,0,8)};
  private string activeName;
  private readonly HolderPreview preview=new HolderPreview{Name="ProfilePreview",Dock=DockStyle.Fill};
  private readonly AnimationEngine cpuPreview=new AnimationEngine(new Random()),gpuPreview=new AnimationEngine(new Random(Guid.NewGuid().GetHashCode()));
  public event Action<string> Requested;
  public string SelectedName {get{return list.SelectedItem is Profile?((Profile)list.SelectedItem).Name:null;}}
  public ProfilesControl() {
   Padding=new Padding(18);BackColor=Color.White;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   layout.Controls.Add(new Label{Text="Choose a profile to use now, or edit a personal copy.",AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,0,0,12)},0,0);layout.Controls.Add(list,0,1);layout.Controls.Add(description,0,2);list.DrawMode=DrawMode.OwnerDrawFixed;list.BorderStyle=BorderStyle.None;
   layout.SizeChanged+=(s,e)=>description.MaximumSize=new Size(Math.Max(100,layout.ClientSize.Width-8),0);
   list.SelectedIndexChanged+=(s,e)=>{cpuPreview.Reset();gpuPreview.Reset();var p=list.SelectedItem as Profile;description.Text=p==null?"":(p.IsBuiltIn?ProfileLibrary.Description(p.Name)+"\n":"")+"CPU: "+Summary(p.Cpu)+" · GPU: "+Summary(p.Gpu);};
   list.FontChanged+=(s,e)=>ResizeRows();ResizeRows();
   list.DrawItem+=(s,e)=>{if(e.Index<0 || e.Index>=list.Items.Count)return;var value=(Profile)list.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;var back=selected?SystemColors.Highlight:UiTheme.Input;var fore=selected?SystemColors.HighlightText:UiTheme.Text;using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);int line=TextRenderer.MeasureText("Ag",list.Font).Height,gap=Math.Max(4,line/4),inset=gap*2;var title=new Rectangle(e.Bounds.Left+inset,e.Bounds.Top+gap,Math.Max(0,e.Bounds.Width-inset*2),line);TextRenderer.DrawText(e.Graphics,(value.Name==activeName?"Active · ":"")+value.Name+" · "+ProfileLibrary.Group(value),list.Font,title,fore,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);var detail=new Rectangle(title.Left,title.Bottom+gap,title.Width,line);TextRenderer.DrawText(e.Graphics,"CPU: "+Summary(value.Cpu)+"   ·   GPU: "+Summary(value.Gpu),list.Font,detail,selected?fore:UiTheme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
   var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
   foreach(string action in new[]{"Use","Edit","New","Duplicate"}) {string name=action;var button=new Button{Text=action=="Use"?"Use profile":action,AutoSize=true,Padding=new Padding(8,3,8,3),Margin=new Padding(0,8,8,0),AccessibleDescription=action=="Use"?"Activate this saved profile. Unsaved edits are resolved first.":null};button.Click+=(s,e)=>{if(Requested!=null)Requested(name);};buttons.Controls.Add(button);}
   var more=new Button{Text="More…",AutoSize=true,Padding=new Padding(8,3,8,3),Margin=new Padding(0,8,8,0)};var menu=new ContextMenuStrip();
   string[] extra={"Rename","Import","Export","Delete"};string[] hints={"Rename a personal profile.","Import as a personal profile. Existing profiles are kept.","Export the saved version of this profile, without unsaved editor changes.","Delete a personal profile. Export first if you want to keep a copy."};
   menu.ShowItemToolTips=true;for(int i=0;i<extra.Length;i++){string name=extra[i];menu.Items.Add(new ToolStripMenuItem(name,null,(s,e)=>{if(Requested!=null)Requested(name);}){ToolTipText=hints[i]});}
   more.Click+=(s,e)=>{var selected=list.SelectedItem as Profile;menu.Items[0].Enabled=menu.Items[3].Enabled=selected!=null && !selected.IsBuiltIn;UiTheme.Menu(menu);menu.Show(more,new Point(0,more.Height));};more.Disposed+=(s,e)=>menu.Dispose();buttons.Controls.Add(more);
   layout.Controls.Add(preview,1,1);layout.SetColumnSpan(layout.GetControlFromPosition(0,0),2);layout.SetColumnSpan(description,2);layout.Controls.Add(buttons,0,3);layout.SetColumnSpan(buttons,2);Controls.Add(layout);
  }
  private void ResizeRows(){int line=TextRenderer.MeasureText("Ag",list.Font).Height;list.ItemHeight=line*2+Math.Max(4,line/4)*3;list.Invalidate();}
  private static string Summary(ChannelSettings channel) {var a=channel.Animation;if(channel.Paused && a.Mode!=AnimationMode.Fixed)return "paused";if(a.Mode==AnimationMode.Playlist)return "playlist · "+a.Steps.Count+" effects";if(a.Mode==AnimationMode.SensorLevel)return "level from "+SensorName(a.SensorId);if(a.Mode==AnimationMode.Fixed)return "fixed level "+a.FixedFrame;return new[]{"fill","empty","bounce","random"}[(int)a.Sequence]+(a.RateMode==AnimationRateMode.Sensor?" · speed from "+SensorName(a.SensorId):" · "+a.FixedFps.ToString("0.##")+" fps");}
  private static string SensorName(string id){switch(id){case "TCPU":return "CPU temperature";case "TCPUDIO":return "CPU diode";case "TGPU1":return "GPU temperature";case "TGPU1HOT":return "GPU hotspot";case "SCPUUTI":return "CPU usage";case "SGPU1UTI":return "GPU usage";default:return id??"unselected sensor";}}
  public void UpdatePreview(SensorSnapshot snapshot,bool available,double elapsed) {
   if(!Visible)return;var p=list.SelectedItem as Profile;if(p==null)return;
   Func<string,double?> reading=id=>{SensorValue v;return available && snapshot!=null && id!=null && snapshot.Values.TryGetValue(id,out v)?v.Number:null;};
   Func<string,int?> temperature=id=>{double? n=reading(id);return n.HasValue && n>=0?(int?)Math.Min(99,Math.Floor(n.Value)):null;};
   var c=cpuPreview.Advance(p.Cpu.Animation,reading(p.Cpu.Animation.SensorId),elapsed,p.Cpu.Paused);var g=gpuPreview.Advance(p.Gpu.Animation,reading(p.Gpu.Animation.SensorId),elapsed,p.Gpu.Paused);
   preview.ShowOutput(c.SourceMissing || g.SourceMissing?"Preview · control sensor unavailable":"Profile preview · not applied",temperature(p.Cpu.TemperatureId),temperature(p.Gpu.TemperatureId),c.Frame,g.Frame,true);
  }
  public void SetProfiles(AppSettings settings,string selectedName) {activeName=settings.ActiveProfileName;list.DataSource=settings.Profiles.OrderBy(p=>p.IsBuiltIn?(ProfileLibrary.Group(p.Name)=="Standard"?0:1):2).Select(p=>p.Copy()).ToList();for(int i=0;i<list.Items.Count;i++) if(((Profile)list.Items[i]).Name==selectedName)list.SelectedIndex=i;list.Invalidate();}
 }
}
