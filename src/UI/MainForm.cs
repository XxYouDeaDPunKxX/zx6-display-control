using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed partial class MainForm:Form {
  private async Task ExportDiagnostics() {lastSaveFeedback="Export canceled.";using(var dialog=new SaveFileDialog{Filter="Diagnostics text|*.txt",FileName="holder-diagnostics.txt",OverwritePrompt=true})if(dialog.ShowDialog(this)==DialogResult.OK){string path=dialog.FileName;var state=controller.State;var snapshot=saved.Copy();await Task.Run(()=>log.Export(path,state,snapshot,UpdateService.CurrentVersion));lastSaveFeedback="Diagnostics exported.";}}
  private async void Attempt(Action action) {Exception failure=null;try{action();}catch(Exception e){failure=e;}if(failure!=null){await log.WriteAsync("action failed",failure.Message,failure);ShowActionError(failure);}}
  private async void AttemptAsync(Func<Task> action) {try{await action();}catch(Exception e){ShowActionError(e);}}
  private void ShowActionError(Exception error){if(!IsDisposed && !Disposing && !closing)AppDialog.Show(this,error.Message,"Z-X6 Display Control",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  public void ShowWindow() {Show();WindowState=FormWindowState.Normal;Activate();}
  public void StartInTray() {Hide();}
  private DialogResult AskSave() {ShowWindow();return AppDialog.Show(this,"Save changes before exiting?","Unsaved changes",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);}
  public bool RequestExit(Func<DialogResult> ask) {
   if(closing)return true;
   if(!Visible)ShowWindow();
   DialogResult decision=DialogResult.No;bool askAfterOperation=busy;
   if(!busy && dirty){decision=ask();if(decision!=DialogResult.Yes && decision!=DialogResult.No)return false;}
   closing=true;CancelUpdateCheck();shutdownStatus=busy?"Finishing the current operation…":"Stopping controller…";RefreshFromState();FinishExit(ask,decision,askAfterOperation);return true;
  }
  private async void FinishExit(Func<DialogResult> ask,DialogResult decision,bool askAfterOperation) {
   try {
    if(busy){var pending=operationIdle.Task;if(await Task.WhenAny(pending,Task.Delay(5000))!=pending){if(IsDisposed || Disposing)return;shutdownStatus="Still finishing the current file operation. The app will close when it completes.";RefreshFromState();}await pending;}if(IsDisposed || Disposing)return;
    if(askAfterOperation && dirty){decision=ask();if(decision!=DialogResult.Yes && decision!=DialogResult.No){closing=false;SetEditingEnabled(true);RefreshFromState();return;}}
    if(decision==DialogResult.Yes)await RunOperation("Save changes",async()=>{if(ContentChanged)await ApplyCore();if(PreferencesPending)await SavePreferencesCore();},true);
    if(IsDisposed || Disposing)return;shutdownStatus="Stopping controller…";controller.Stop();RefreshFromState();
    if(await Task.WhenAny(controller.Completion,Task.Delay(5000))!=controller.Completion) {
     if(IsDisposed || Disposing)return;shutdownStatus="Still waiting for the controller to release the USB port…";RefreshFromState();
    }
    await controller.Completion;if(IsDisposed || Disposing)return;
    string failure=controller.FailureMessage??(controller.State==null?null:controller.State.CleanupError);
    if(!string.IsNullOrEmpty(failure) && !systemClosing)AppDialog.Show(this,"The controller stopped, but cleanup reported a problem:\n\n"+failure,"Shutdown",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    exiting=true;Close();
   }catch(Exception e){if(!IsDisposed && !Disposing){closing=false;SetEditingEnabled(true);lastSaveFeedback="Exit canceled: "+e.Message;RefreshFromState();ShowActionError(e);}}
  }
  private void ExitApp() {Attempt(()=>RequestExit(AskSave));}
  private void OnFormClosing(object sender,FormClosingEventArgs e) {
   if(exiting)return;
   if(e.CloseReason==CloseReason.WindowsShutDown){
    // An idle end-session query is not confirmation: another app may veto it.
    if(!busy)return;
    e.Cancel=true;systemClosing=true;RequestExit(()=>DialogResult.No);
    shutdownStatus="Windows shutdown was postponed while the current operation finishes. Retry shutdown after this app closes.";RefreshFromState();return;
   }
   if(e.CloseReason==CloseReason.TaskManagerClosing){e.Cancel=true;systemClosing=true;RequestExit(()=>DialogResult.No);return;}
   e.Cancel=true;
   if(closing){ShowWindow();return;}
   if(!saved.CloseToTray){BeginInvoke(new Action(ExitApp));return;}
   if(!saved.CloseToTrayExplained && !busy){tray.ShowBalloonTip(5000,"Z-X6 Display Control is still running","Open the tray icon to return, or choose Exit to stop the controller.",ToolTipIcon.Info);AttemptAsync(()=>RunOperation("Remember tray preference",async()=>{var snapshot=saved.Copy();snapshot.CloseToTrayExplained=true;await Task.Run(()=>store.Save(snapshot));if(IsDisposed || Disposing)return;saved=snapshot;}));}
   Hide();
  }
  protected override void Dispose(bool disposing) {if(disposing){CancelUpdateCheck();if(!closing)controller.Stop();closing=true;timer.Stop();timer.Dispose();if(contextHelp!=null)contextHelp.Dispose();tray.Visible=false;var menu=tray.ContextMenuStrip;tray.Dispose();if(menu!=null)menu.Dispose();if(Icon!=null)Icon.Dispose();}base.Dispose(disposing);}
 }
}
