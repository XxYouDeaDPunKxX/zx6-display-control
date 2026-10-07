using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed class PlaylistEditor:UserControl {
  private readonly ListBox list=new ListBox{Name="PlaylistSteps",Dock=DockStyle.Top,Height=100,IntegralHeight=false,AccessibleName="Playlist effects",AccessibleDescription="Effects play in this order and repeat. Select one to edit its pattern, duration and speed."};
  private readonly ComboBox pattern=new ComboBox{Name="StepPattern",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,AccessibleName="Effect pattern",AccessibleDescription="Choose Fill, Empty, Bounce or Random for this playlist step."};
  private readonly NumericUpDown seconds=new NumericUpDown{Name="StepSeconds",Minimum=1,Maximum=600,Value=10,Dock=DockStyle.Fill,AccessibleName="Duration in seconds",AccessibleDescription="How long this effect plays before moving to the next step, from 1 to 600 seconds."};
  private readonly NumericUpDown speed=new NumericUpDown{Name="StepSpeed",Minimum=1,Maximum=4,Value=2,DecimalPlaces=2,Increment=.25M,Dock=DockStyle.Fill,AccessibleName="Effect speed",AccessibleDescription="Bar updates per second, from 1 to 4."};
  private bool loading;
  public event EventHandler Changed;
  public PlaylistEditor() {
   AutoSize=true;pattern.Items.AddRange(new object[]{"Fill","Empty","Bounce","Random"});
   var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(table);table.Controls.Add(list);
   var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=true};
   AddButton(buttons,"Add",()=>{if(list.Items.Count>=16)return;list.Items.Add(new PlaylistStep());list.SelectedIndex=list.Items.Count-1;Notify();},"Append an effect. A playlist can contain up to 16 steps.");
   AddButton(buttons,"Remove",()=>{int i=list.SelectedIndex;if(i<0)return;list.Items.RemoveAt(i);if(list.Items.Count>0)list.SelectedIndex=Math.Min(i,list.Items.Count-1);SelectStep();Notify();},"Remove the selected step from this draft.");
   AddButton(buttons,"Up",()=>MoveStep(-1),"Move the selected effect earlier.");AddButton(buttons,"Down",()=>MoveStep(1),"Move the selected effect later.");table.Controls.Add(buttons);
   var fields=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2};fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(var pair in new[]{Tuple.Create("Pattern",(Control)pattern),Tuple.Create("Seconds",(Control)seconds),Tuple.Create("Speed (fps)",(Control)speed)}){int row=fields.RowCount++;fields.Controls.Add(new Label{Text=pair.Item1,AutoSize=true,Margin=new Padding(0,6,12,3)},0,row);fields.Controls.Add(pair.Item2,1,row);}table.Controls.Add(fields);
   list.SelectedIndexChanged+=(s,e)=>SelectStep();pattern.SelectedIndexChanged+=(s,e)=>EditStep();seconds.ValueChanged+=(s,e)=>EditStep();speed.ValueChanged+=(s,e)=>EditStep();SelectStep();
  }
  private static void AddButton(FlowLayoutPanel parent,string text,Action action,string help){var b=new Button{Text=text,AutoSize=true,AccessibleDescription=help};b.Click+=(s,e)=>action();parent.Controls.Add(b);}
  public void SetSteps(IEnumerable<PlaylistStep> steps) {loading=true;try{list.Items.Clear();if(steps!=null)foreach(var step in steps)list.Items.Add(step.Copy());if(list.Items.Count>0)list.SelectedIndex=0;}finally{loading=false;}SelectStep();}
  public List<PlaylistStep> GetSteps(){return list.Items.Count==0?null:list.Items.Cast<PlaylistStep>().Select(s=>s.Copy()).ToList();}
  public void EnsureDefaults(){if(list.Items.Count==0)SetSteps(new[]{new PlaylistStep(),new PlaylistStep{Pattern=SequenceKind.Reverse}});}
  private void SelectStep(){loading=true;try{var step=list.SelectedItem as PlaylistStep;pattern.Enabled=seconds.Enabled=speed.Enabled=step!=null;if(step!=null){pattern.SelectedIndex=(int)step.Pattern;seconds.Value=step.Seconds;speed.Value=(decimal)step.Fps;}}finally{loading=false;}}
  private void EditStep(){if(loading || list.SelectedIndex<0)return;int i=list.SelectedIndex;loading=true;try{list.Items[i]=new PlaylistStep{Pattern=(SequenceKind)Math.Max(0,pattern.SelectedIndex),Seconds=(int)seconds.Value,Fps=(double)speed.Value};}finally{loading=false;}Notify();}
  private void MoveStep(int delta){int from=list.SelectedIndex,to=from+delta;if(from<0 || to<0 || to>=list.Items.Count)return;var item=list.Items[from];list.Items.RemoveAt(from);list.Items.Insert(to,item);list.SelectedIndex=to;Notify();}
  private void Notify(){if(!loading && Changed!=null)Changed(this,EventArgs.Empty);}
 }
}
