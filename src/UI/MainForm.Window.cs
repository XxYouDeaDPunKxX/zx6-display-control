using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed partial class MainForm {
  private FormWindowState restoredState=FormWindowState.Normal;
  public void RestoreWindowPlacement(WindowPlacement value) {
   if(value==null)return;StartPosition=FormStartPosition.Manual;
   var areas=Screen.AllScreens.Select(s=>s.WorkingArea).ToArray();var fitted=WindowPlacement.Fit(value.Bounds,areas,MinimumSize);
   MinimumSize=new Size(Math.Min(MinimumSize.Width,fitted.Width),Math.Min(MinimumSize.Height,fitted.Height));Bounds=fitted;
   WindowState=value.Maximized?FormWindowState.Maximized:FormWindowState.Normal;restoredState=WindowState;
  }
  public WindowPlacement CaptureWindowPlacement() {
   var bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
   return new WindowPlacement{X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height,Maximized=restoredState==FormWindowState.Maximized};
  }
  private void RefreshTrayProfiles() {
   trayProfiles.DropDownItems.Clear();
   foreach(var profile in saved.Profiles) {
    string name=profile.Name;var item=new ToolStripMenuItem(name){Checked=name==saved.ActiveProfileName,ToolTipText="Activate this saved profile"};
    item.Click+=(s,e)=>AttemptAsync(()=>ActivateProfile(name,AskSwitch));trayProfiles.DropDownItems.Add(item);
   }
   UiTheme.Menu(tray.ContextMenuStrip);
  }
 }
}
