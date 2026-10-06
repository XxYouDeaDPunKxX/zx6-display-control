using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed partial class MainForm {
  private readonly UpdateService updates;
  private readonly CheckBox updateStartupBox=new CheckBox{Name="CheckUpdatesOnStartup",Text="Check for updates at startup (once a day)",AutoSize=true,AccessibleDescription="Check GitHub at startup, at most once every 24 hours. Save preferences to keep this choice. Manual checks remain available. Updates are never installed automatically."};
  private readonly Button checkUpdates=ActionButton("CheckForUpdates","Check for updates","Check GitHub now for a newer published app version, including betas. This does not download or install the app.");
  private readonly Button openRelease=ActionButton("OpenRelease","Open release","Open the available version's release page on GitHub in your browser.");
  private readonly Label updateStatus=new Label{Name="UpdateStatus",AutoSize=true,Dock=DockStyle.Top};
  private readonly LinkLabel updateNotice=new LinkLabel{Name="UpdateNotice",AutoSize=true,Anchor=AnchorStyles.Right,Visible=false,AccessibleDescription="A newer app version is available. Open its GitHub release page in your browser."};
  private CancellationTokenSource updateCancellation;
  private UpdateResult updateResult;
  private bool checkingUpdates;

  private void BuildUpdateSection(TableLayoutPanel table) {
   InfoPage.Heading(table,"Updates");
   InfoPage.Paragraph(table,"Installed: "+UpdateService.CurrentVersion+". Checks include beta releases. Download and replace the app yourself from GitHub.");
   var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};actions.Controls.Add(checkUpdates);actions.Controls.Add(openRelease);InfoPage.Add(table,actions);InfoPage.Add(table,updateStatus);
   checkUpdates.Enabled=updates!=null;openRelease.Enabled=false;
   updateStatus.Text=updates==null?"Update checks are unavailable in this session.":"Check GitHub for a newer version.";
   checkUpdates.Click+=(s,e)=>AttemptAsync(()=>CheckForUpdates());
   openRelease.Click+=(s,e)=>Attempt(OpenAvailableRelease);updateNotice.LinkClicked+=(s,e)=>Attempt(OpenAvailableRelease);
  }
  public async Task CheckForUpdates(bool automatic=false) {
   if(updates==null || checkingUpdates || closing || IsDisposed || Disposing)return;
   checkingUpdates=true;checkUpdates.Enabled=false;updateStatus.Text="Checking GitHub…";
   var cancellation=new CancellationTokenSource();updateCancellation=cancellation;Exception failure=null;
   try {
    await log.WriteAsync("update check",automatic?"Startup check requested.":"Manual check requested.");
    var result=await updates.CheckAsync(UpdateService.CurrentVersion,automatic,cancellation.Token);
    if(closing || IsDisposed || Disposing)return;
    updateResult=result;updateStatus.Text=result.Message;openRelease.Enabled=result.ReleaseUrl!=null;
    if(!HasOpenDropDown(this))RefreshUpdateNotice();
    await log.WriteAsync("update check",result.Message);
   }catch(OperationCanceledException) {
    if(!closing && !IsDisposed && !Disposing)updateStatus.Text="Update check canceled. You can check again manually.";
   }catch(Exception error) {
    if(!closing && !IsDisposed && !Disposing)updateStatus.Text="Could not check for updates. Try again later.";
    failure=error;
   }finally {
    updateCancellation=null;cancellation.Dispose();checkingUpdates=false;
    if(!IsDisposed && !Disposing){checkUpdates.Enabled=!closing;if(closing)updateStatus.Text="Update check canceled.";}
   }
   if(failure!=null)await log.WriteAsync("update check failed",failure.Message,failure);
  }
  private void RefreshUpdateNotice() {
   bool available=updateResult!=null && updateResult.ReleaseUrl!=null;
   if(available)updateNotice.Text="Update "+updateResult.AvailableVersion+" available · Open release";
   updateNotice.Visible=available;
  }
  private void OpenAvailableRelease() {
   if(updateResult!=null && updateResult.ReleaseUrl!=null)Process.Start(new ProcessStartInfo(updateResult.ReleaseUrl.AbsoluteUri){UseShellExecute=true});
  }
  private void CancelUpdateCheck() {if(updateCancellation!=null)updateCancellation.Cancel();}
 }
}
