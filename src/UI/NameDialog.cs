using System.Drawing;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 internal static class NameDialog {
  public static string Ask(IWin32Window owner,string title,string initial) {
   using(var dialog=new Form{Text=title,StartPosition=FormStartPosition.CenterParent,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi,ClientSize=new Size(430,130),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(16),FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false,Font=new Font("Segoe UI",10)}) {
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    var field=new TextBox{Text=initial,Dock=DockStyle.Top,MinimumSize=new Size(380,0),Margin=new Padding(0,4,0,20),AccessibleName="Profile name"};var ok=new Button{Text="OK",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true};
    var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};actions.Controls.Add(cancel);actions.Controls.Add(ok);layout.Controls.Add(field,0,0);layout.Controls.Add(actions,0,1);dialog.Controls.Add(layout);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
    using(var help=new UiHelp(dialog)){UiTheme.Apply(dialog,UiTheme.Current);dialog.Shown+=(s,e)=>UiTheme.Apply(dialog,UiTheme.Current);return dialog.ShowDialog(owner)==DialogResult.OK?field.Text.Trim():null;}
   }
  }
 }
}
