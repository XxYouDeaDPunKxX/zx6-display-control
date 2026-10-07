using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
namespace ZX6DisplayControl {
 internal sealed class ThemedTabControl:TabControl {
  public ThemedTabControl(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.Clear(UiTheme.Background);
   using(var brush=new SolidBrush(UiTheme.Surface))e.Graphics.FillRectangle(brush,DisplayRectangle);
   using(var pen=new Pen(UiTheme.Muted))e.Graphics.DrawRectangle(pen,DisplayRectangle.X-1,DisplayRectangle.Y-1,Math.Max(0,DisplayRectangle.Width+1),Math.Max(0,DisplayRectangle.Height+1));
   for(int i=0;i<TabCount;i++) {
    var bounds=GetTabRect(i);bool selected=i==SelectedIndex;using(var brush=new SolidBrush(selected?UiTheme.Surface:UiTheme.Background))e.Graphics.FillRectangle(brush,bounds);
    TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,bounds,selected?UiTheme.Accent:UiTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
    if(selected){using(var brush=new SolidBrush(UiTheme.Accent))e.Graphics.FillRectangle(brush,bounds.Left+8,bounds.Bottom-3,bounds.Width-16,2);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(bounds,-4,-4),UiTheme.Text,UiTheme.Surface);}
   }
  }
 }
 internal static class UiTheme {
  private sealed class Original {public Color Fore;public bool Installed;}
  private static readonly ConditionalWeakTable<Control,Original> originals=new ConditionalWeakTable<Control,Original>();
  public static AppTheme Current {get;private set;}
  public static bool Dark {get;private set;}
  public static bool HighContrast {get;private set;}
  public static Color Background {get{return HighContrast?SystemColors.Window:Dark?Color.FromArgb(27,30,35):Color.FromArgb(243,245,247);}}
  public static Color Surface {get{return HighContrast?SystemColors.Window:Dark?Color.FromArgb(34,38,44):Color.White;}}
  public static Color Input {get{return HighContrast?SystemColors.Window:Dark?Color.FromArgb(46,52,60):Color.White;}}
  public static Color Text {get{return HighContrast?SystemColors.WindowText:Dark?Color.FromArgb(232,235,239):Color.FromArgb(28,35,43);}}
  public static Color Muted {get{return HighContrast?SystemColors.GrayText:Dark?Color.FromArgb(174,185,198):Color.FromArgb(85,99,115);}}
  public static Color Accent {get{return HighContrast?SystemColors.Highlight:Dark?Color.FromArgb(118,207,232):Color.FromArgb(22,86,117);}}
  public static Color Error {get{return HighContrast?SystemColors.WindowText:Dark?Color.FromArgb(255,150,150):Color.Firebrick;}}
  public static Color Success {get{return HighContrast?Text:Dark?Color.FromArgb(134,219,164):Color.DarkGreen;}}
  public static Color Warning {get{return HighContrast?Text:Dark?Color.FromArgb(240,199,110):Color.FromArgb(133,85,0);}}
  public static bool SystemIsDark() {try {using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){return key!=null && object.Equals(key.GetValue("AppsUseLightTheme"),0);}}catch(System.Security.SecurityException){return false;}}
  public static void Apply(Control root,AppTheme value) {
   Apply(root,value,SystemInformation.HighContrast);
  }
  public static void Apply(Control root,AppTheme value,bool highContrast) {
   HighContrast=highContrast;
   Current=value;Dark=!HighContrast && (value==AppTheme.Dark || (value==AppTheme.System && SystemIsDark()));ApplyControl(root);
   var form=root as Form;if(form!=null && form.IsHandleCreated){int dark=Dark?1:0;try{DwmSetWindowAttribute(form.Handle,20,ref dark,sizeof(int));}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
  }
  private static void ApplyControl(Control c) {
   var original=originals.GetValue(c,x=>new Original{Fore=x.ForeColor});
   c.BackColor=c is Form?Background:Surface;c.ForeColor=original.Fore==Color.Firebrick?Error:original.Fore==Color.DimGray || original.Fore==SystemColors.GrayText?Muted:Text;
   var button=c as Button;if(button!=null) {button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=1;button.FlatAppearance.BorderColor=HighContrast?SystemColors.WindowText:Dark?Color.FromArgb(88,101,116):Color.FromArgb(160,174,187);button.BackColor=Input;if(c.Name=="ApplyChanges"){button.BackColor=HighContrast?SystemColors.Highlight:Dark?Color.FromArgb(32,94,119):Color.FromArgb(22,86,117);button.ForeColor=HighContrast?SystemColors.HighlightText:Color.White;}}
   if(button!=null && !original.Installed)button.Paint+=PaintDisabledButton;
   var check=c as CheckBox;if(check!=null)check.UseVisualStyleBackColor=false;
   var link=c as LinkLabel;if(link!=null){link.LinkColor=Accent;link.ActiveLinkColor=Accent;link.VisitedLinkColor=Accent;}
   if(c is TextBox || c is ListBox || c is ListView || c is NumericUpDown)c.BackColor=Input;
   var combo=c as ComboBox;if(combo!=null){combo.BackColor=Input;if(combo.FlatStyle!=FlatStyle.Popup)combo.FlatStyle=FlatStyle.Popup;if(combo.DrawMode!=DrawMode.OwnerDrawFixed)combo.DrawMode=DrawMode.OwnerDrawFixed;if(!original.Installed)combo.DrawItem+=DrawChoice;}
   var tabs=c as TabControl;if(tabs!=null){tabs.DrawMode=TabDrawMode.OwnerDrawFixed;if(!original.Installed)tabs.DrawItem+=DrawTab;}
   var list=c as ListView;if(list!=null) {list.OwnerDraw=true;if(!original.Installed){list.DrawColumnHeader+=(s,e)=>{using(var brush=new SolidBrush(Input))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,e.Font,e.Bounds,Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};list.DrawItem+=(s,e)=>{if(list.View!=View.Details)e.DrawDefault=true;};list.DrawSubItem+=(s,e)=>{bool selected=e.Item.Selected;using(var brush=new SolidBrush(selected?SystemColors.Highlight:Input))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,e.Bounds,selected?SystemColors.HighlightText:Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};}}
   original.Installed=true;foreach(Control child in c.Controls)ApplyControl(child);c.Invalidate();
  }
  private static void PaintDisabledButton(object sender,PaintEventArgs e) {
   var button=(Button)sender;if(button.Enabled || !Dark || HighContrast)return;
   e.Graphics.Clear(Input);var bounds=button.ClientRectangle;bounds.Width--;bounds.Height--;
   using(var pen=new Pen(Color.FromArgb(88,101,116)))e.Graphics.DrawRectangle(pen,bounds);
   TextRenderer.DrawText(e.Graphics,button.Text,button.Font,Rectangle.Inflate(bounds,-4,-2),Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
  private static void DrawChoice(object sender,DrawItemEventArgs e) {
   var combo=(ComboBox)sender;bool selected=(e.State&DrawItemState.Selected)!=0 && (e.State&DrawItemState.ComboBoxEdit)==0;
   using(var brush=new SolidBrush(selected?SystemColors.Highlight:Input))e.Graphics.FillRectangle(brush,e.Bounds);
   string text=e.Index>=0 && e.Index<combo.Items.Count?combo.GetItemText(combo.Items[e.Index]):combo.Text;
   TextRenderer.DrawText(e.Graphics,text,combo.Font,new Rectangle(e.Bounds.X+3,e.Bounds.Y,e.Bounds.Width-3,e.Bounds.Height),!combo.Enabled?Muted:selected?SystemColors.HighlightText:Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
  }
  private static void DrawTab(object sender,DrawItemEventArgs e) {var tabs=(TabControl)sender;bool selected=e.Index==tabs.SelectedIndex;using(var brush=new SolidBrush(selected?Surface:Background))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,tabs.Font,e.Bounds,selected?Accent:Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}
  public static void Menu(ContextMenuStrip menu) {StyleMenu(menu);}
  private static void StyleMenu(ToolStrip menu) {menu.Renderer=new ToolStripProfessionalRenderer(new MenuColors());menu.BackColor=Surface;menu.ForeColor=Text;foreach(ToolStripItem item in menu.Items){item.BackColor=Surface;item.ForeColor=Text;var child=item as ToolStripDropDownItem;if(child!=null && child.HasDropDownItems)StyleMenu(child.DropDown);}}
  private sealed class MenuColors:ProfessionalColorTable {public override Color ToolStripDropDownBackground{get{return Surface;}}public override Color ImageMarginGradientBegin{get{return Surface;}}public override Color ImageMarginGradientMiddle{get{return Surface;}}public override Color ImageMarginGradientEnd{get{return Surface;}}public override Color MenuItemSelected{get{return Input;}}public override Color MenuItemBorder{get{return Accent;}}public override Color MenuBorder{get{return Muted;}}}
  [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 }
 internal static class AppDialog {
  public static bool Confirm(IWin32Window owner,string text,string title,string action) {return Show(owner,text,title,MessageBoxButtons.YesNo,MessageBoxIcon.Warning,action)==DialogResult.Yes;}
  public static DialogResult Show(IWin32Window owner,string text,string title,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,string confirmCaption=null) {
   using(var dialog=new Form{Text=title,ClientSize=new Size(510,180),StartPosition=FormStartPosition.CenterParent,Font=new Font("Segoe UI",10),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(20)}) {
    var layout=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1};var label=new Label{Text=text,AutoSize=true,MaximumSize=new Size(470,0),UseMnemonic=false,Margin=new Padding(0,0,0,20)};layout.Controls.Add(label);
    var actions=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill,WrapContents=false};layout.Controls.Add(actions);dialog.Controls.Add(layout);
    var results=buttons==MessageBoxButtons.YesNoCancel?new[]{DialogResult.Cancel,DialogResult.No,DialogResult.Yes}:buttons==MessageBoxButtons.YesNo?new[]{DialogResult.No,DialogResult.Yes}:new[]{DialogResult.OK};
    foreach(var result in results){string caption=result.ToString();if(buttons==MessageBoxButtons.YesNo)caption=result==DialogResult.Yes?(confirmCaption??"Continue"):"Cancel";if(buttons==MessageBoxButtons.YesNoCancel)caption=result==DialogResult.Yes?"Save":result==DialogResult.No?"Discard":"Cancel";var button=new Button{Text=caption,AutoSize=true,DialogResult=result,AccessibleDescription=result==DialogResult.Cancel?"Return without closing or saving.":result==DialogResult.No?(buttons==MessageBoxButtons.YesNoCancel?"Continue without saving these changes.":"Cancel this action."):"Confirm and continue."};actions.Controls.Add(button);if(result==DialogResult.Cancel || (result==DialogResult.No && buttons!=MessageBoxButtons.YesNoCancel))dialog.CancelButton=button;if(result==DialogResult.OK || (buttons!=MessageBoxButtons.YesNo && result==DialogResult.Yes) || (buttons==MessageBoxButtons.YesNo && result==DialogResult.No))dialog.AcceptButton=button;}
    using(var help=new UiHelp(dialog)){UiTheme.Apply(dialog,UiTheme.Current);dialog.Shown+=(s,e)=>UiTheme.Apply(dialog,UiTheme.Current);return dialog.ShowDialog(owner);}
   }
  }
 }
}
