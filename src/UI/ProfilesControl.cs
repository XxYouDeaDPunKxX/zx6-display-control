using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed class ProfilesControl:UserControl {
  private readonly ListBox list=new ListBox{Dock=DockStyle.Fill,DisplayMember="Name",AccessibleName="Available profiles"};
  public event Action<string> Requested;
  public string SelectedName {get{return list.SelectedItem is Profile?((Profile)list.SelectedItem).Name:null;}}
  public ProfilesControl() {
   Padding=new Padding(18);BackColor=Color.White;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   var library=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
   foreach(string group in new[]{"Standard","Creative"}) {
    var button=new Button{Text=group+" presets…",AutoSize=true,AccessibleDescription=group=="Standard"?"Add a preset for steady animation, usage, temperature or sensor levels.":"Add a preset with random, breathing, opposing or mixed-speed movement."};var presets=new ContextMenuStrip{ShowItemToolTips=true};
    foreach(var template in ProfileLibrary.Create().Where(p=>ProfileLibrary.Group(p.Name)==group)) {string name=template.Name;presets.Items.Add(new ToolStripMenuItem(name,null,(s,e)=>{if(Requested!=null)Requested("Preset|"+name);}){ToolTipText=ProfileLibrary.Description(name),AccessibleDescription=ProfileLibrary.Description(name)});}
    button.Click+=(s,e)=>{UiTheme.Menu(presets);presets.Show(button,new Point(0,button.Height));};button.Disposed+=(s,e)=>presets.Dispose();library.Controls.Add(button);
   }
   layout.Controls.Add(library,0,0);layout.Controls.Add(list,0,1);list.DrawMode=DrawMode.OwnerDrawFixed;list.BorderStyle=BorderStyle.None;
   list.FontChanged+=(s,e)=>ResizeRows();ResizeRows();
   list.DrawItem+=(s,e)=>{if(e.Index<0 || e.Index>=list.Items.Count)return;var value=(Profile)list.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;var back=selected?SystemColors.Highlight:UiTheme.Input;var fore=selected?SystemColors.HighlightText:UiTheme.Text;using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);int line=TextRenderer.MeasureText("Ag",list.Font).Height,gap=Math.Max(4,line/4),inset=gap*2;var title=new Rectangle(e.Bounds.Left+inset,e.Bounds.Top+gap,Math.Max(0,e.Bounds.Width-inset*2),line);TextRenderer.DrawText(e.Graphics,ProfileLibrary.Group(value.Name)+" · "+value.Name,list.Font,title,fore,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);var detail=new Rectangle(title.Left,title.Bottom+gap,title.Width,line);TextRenderer.DrawText(e.Graphics,"CPU: "+Summary(value.Cpu)+"   ·   GPU: "+Summary(value.Gpu),list.Font,detail,selected?fore:UiTheme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
   var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
   foreach(string action in new[]{"Edit","New","Duplicate"}) {string name=action;var button=new Button{Text=action=="Edit"?"Edit":action,AutoSize=true,Padding=new Padding(8,3,8,3),Margin=new Padding(0,8,8,0)};button.Click+=(s,e)=>{if(Requested!=null)Requested(name);};buttons.Controls.Add(button);}
   var more=new Button{Text="More…",AutoSize=true,Padding=new Padding(8,3,8,3),Margin=new Padding(0,8,8,0)};var menu=new ContextMenuStrip();
   string[] extra={"Rename","Import","Export","Delete","Restore defaults"};string[] hints={"Change the selected profile's name.","Load a profile from a JSON file. You choose whether to replace a matching name.","Save the selected profile to a JSON file.","Remove the selected profile from this collection. Apply saves the deletion.","Replace the collection with eight Standard and Creative presets. Revert can undo this before Apply."};
   menu.ShowItemToolTips=true;for(int i=0;i<extra.Length;i++){string name=extra[i];menu.Items.Add(new ToolStripMenuItem(name,null,(s,e)=>{if(Requested!=null)Requested(name);}){ToolTipText=hints[i]});}
   more.Click+=(s,e)=>{UiTheme.Menu(menu);menu.Show(more,new Point(0,more.Height));};more.Disposed+=(s,e)=>menu.Dispose();buttons.Controls.Add(more);
   layout.Controls.Add(buttons,0,2);Controls.Add(layout);
  }
  private void ResizeRows(){int line=TextRenderer.MeasureText("Ag",list.Font).Height;list.ItemHeight=line*2+Math.Max(4,line/4)*3;list.Invalidate();}
  private static string Summary(ChannelSettings channel) {var a=channel.Animation;if(channel.Paused)return "paused";if(a.Mode==AnimationMode.SensorLevel)return "sensor level";if(a.Mode==AnimationMode.Fixed)return "level "+a.FixedFrame;return new[]{"fill","empty","bounce","random"}[(int)a.Sequence]+(a.RateMode==AnimationRateMode.Sensor?" · sensor speed":" · "+a.FixedFps.ToString("0.##")+" fps");}
  public void SetProfiles(AppSettings settings,string selectedName) {list.DataSource=settings.Profiles.OrderBy(p=>ProfileLibrary.Group(p.Name)=="Standard"?0:ProfileLibrary.Group(p.Name)=="Creative"?1:2).Select(p=>p.Copy()).ToList();for(int i=0;i<list.Items.Count;i++) if(((Profile)list.Items[i]).Name==selectedName)list.SelectedIndex=i;}
 }
}
