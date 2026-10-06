using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZX6DisplayControl {
 public sealed partial class MainForm {
  public bool CanApply {get{return cpu.ValidationErrors().Count==0 && gpu.ValidationErrors().Count==0;}}
  private Profile ReadDraft() {
   if(!CanApply)throw new InvalidOperationException("Fix the highlighted profile settings before saving.");
   var result=editing.Copy();result.Cpu=cpu.GetDraft();result.Gpu=gpu.GetDraft();return result;
  }
  private bool ContentChanged {
   get {if(editing==null)return false;try{return !editing.SameContent(ReadDraft());}catch(InvalidOperationException){return true;}}
  }
  private bool ProfilePending {get{return editing!=null && (ContentChanged || editing.Name!=saved.ActiveProfileName);}}
  private bool PreferencesPending {get{return startupBox.Checked!=saved.StartWithWindows || closeBox.Checked!=saved.CloseToTray || minimizeBox.Checked!=saved.MinimizeToTray || updateStartupBox.Checked!=saved.CheckUpdatesOnStartup || Math.Max(0,themeChoice.SelectedIndex)!=(int)saved.Theme;}}
  public Task ApplyChanges() {return RunOperation("Save profile",ApplyCore);}
  private async Task ApplyCore() {
   var value=ReadDraft();var snapshot=saved.Copy();
   if(value.IsBuiltIn && ContentChanged) {
    value=value.PersonalCopy(ProfileLibrary.UniqueName(snapshot.Profiles,value.Name+" (custom)"));snapshot.Add(value);
   }else if(!value.IsBuiltIn) {
    int index=snapshot.Profiles.FindIndex(p=>p.Name==value.Name);snapshot.Profiles[index]=value.Copy();
   }
   snapshot.ActiveProfileName=value.Name;snapshot.Validate();await Task.Run(()=>store.Save(snapshot));
   if(IsDisposed || Disposing)return;
   saved=snapshot;editing=saved.ActiveProfile.Copy();controller.Apply(Configuration(saved));
   lastSaveFeedback="Saved and using “"+saved.ActiveProfileName+"”.";LoadEditor();
  }
  public void CancelChanges() {editing=saved.ActiveProfile.Copy();lastSaveFeedback="Profile changes discarded.";LoadEditor();}
  private void LoadEditor() {
   updating=true;try {
    var state=controller.State;var snapshot=state==null?null:state.Snapshot;
    cpu.Load(editing.Cpu,snapshot);gpu.Load(editing.Gpu,snapshot);LoadProfileChoices();
   }finally{updating=false;}
   cpuPreview.Reset();gpuPreview.Reset();RefreshFromState();
  }
  private void LoadProfileChoices() {
   bool previous=updating;updating=true;try {
    profileChoice.Items.Clear();profileChoice.Items.AddRange(saved.Profiles.Select(p=>(object)p.Name).ToArray());profileChoice.SelectedItem=editing.Name;profiles.SetProfiles(saved,editing.Name);
   }finally{updating=previous;}
  }
  private DialogResult AskSwitch() {return AppDialog.Show(this,"Save changes to “"+editing.Name+"” before opening another profile?"+(editing.IsBuiltIn?"\nSaving creates a personal copy.":""),"Unsaved profile",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);}
  // The decision belongs to the current draft only; no other profile is staged.
  public async Task<bool> SelectProfile(string name,Func<DialogResult> ask) {
   bool selected=false;await RunOperation("Change profile",async()=>{selected=await ChooseProfile(name,ask);});return selected;
  }
  private async Task<bool> ChooseProfile(string name,Func<DialogResult> ask) {
   if(string.IsNullOrEmpty(name) || name==editing.Name)return true;
   var next=saved.Profiles.Single(p=>p.Name==name).Copy();
   try {
    if(ContentChanged) {
     var decision=ask();if(decision==DialogResult.Cancel || (decision!=DialogResult.Yes && decision!=DialogResult.No)){lastSaveFeedback="Profile change canceled.";return false;}
     if(decision==DialogResult.Yes)await ApplyCore();
    }
    editing=next;lastSaveFeedback=null;LoadEditor();return true;
   }finally{updating=true;profileChoice.SelectedItem=editing.Name;updating=false;}
  }
  private void LoadPreferences() {
   updating=true;try {startupBox.Checked=saved.StartWithWindows;closeBox.Checked=saved.CloseToTray;minimizeBox.Checked=saved.MinimizeToTray;updateStartupBox.Checked=saved.CheckUpdatesOnStartup;themeChoice.SelectedIndex=(int)saved.Theme;}finally{updating=false;}ApplyTheme();
  }
  public void RevertPreferences() {LoadPreferences();preferenceStatus.Text="Preferences reverted.";RefreshFromState();}
  public Task SavePreferences() {return RunOperation("Save preferences",SavePreferencesCore);}
  private async Task SavePreferencesCore() {
   var snapshot=saved.Copy();snapshot.StartWithWindows=startupBox.Checked;snapshot.CloseToTray=closeBox.Checked;snapshot.MinimizeToTray=minimizeBox.Checked;snapshot.CheckUpdatesOnStartup=updateStartupBox.Checked;snapshot.Theme=(AppTheme)Math.Max(0,themeChoice.SelectedIndex);
   await Task.Run(()=>startup.CommitEnabled(snapshot.StartWithWindows,exePath,()=>store.Save(snapshot)));
   if(IsDisposed || Disposing)return;saved=snapshot;preferenceStatus.Text="Preferences saved.";lastSaveFeedback="Preferences saved. Profile edits are unchanged.";await RefreshStartup();
  }
  private async Task RefreshStartup() {
   try {bool enabled=await Task.Run(()=>startup.GetEnabled(exePath));if(IsDisposed || Disposing)return;
    startupStatus.Text=enabled?"This copy is registered to start with Windows.":saved.StartWithWindows?"Startup registration does not point to this copy. Save preferences to repair it.":"This copy is not registered to start with Windows.";
   }catch(Exception e){if(!IsDisposed && !Disposing)startupStatus.Text="Could not check Windows startup: "+e.Message;}
  }
  private void Changed() {if(updating)return;lastSaveFeedback=null;preferenceStatus.Text=PreferencesPending?"Unsaved preferences":"";RefreshFromState();}
  private static SessionConfiguration Configuration(AppSettings value) {return new SessionConfiguration{Cpu=value.ActiveProfile.Cpu.Copy(),Gpu=value.ActiveProfile.Gpu.Copy(),DisplayEnabled=value.DisplayEnabled};}
  private Task TogglePower() {return RunOperation("Change display power",async()=>{
   var snapshot=saved.Copy();snapshot.DisplayEnabled=!snapshot.DisplayEnabled;await Task.Run(()=>store.Save(snapshot));
   if(IsDisposed || Disposing)return;saved=snapshot;controller.Apply(Configuration(saved));lastSaveFeedback=saved.DisplayEnabled?"Display on requested.":"Display off requested.";
  });}
  private void RetryConnection() {AttemptAsync(()=>RunOperation("Refresh connection",()=>{
   if(!string.IsNullOrEmpty(controller.FailureMessage))throw new InvalidOperationException("The controller has stopped. Exit and reopen the app to reconnect.");
   controller.Retry();lastSaveFeedback="Rechecking the USB connection…";return Task.FromResult(true);
  }));}
  private async Task RunOperation(string name,Func<Task> action,bool duringExit=false) {
   if(busy || (closing && !duringExit))throw new InvalidOperationException("Wait for the current operation to finish.");
   busy=true;operationName=name;operationIdle=new TaskCompletionSource<bool>();var idle=operationIdle;lastSaveFeedback=null;SetEditingEnabled(false);RefreshFromState();
   try {
    Exception failure=null;
    try {await log.WriteAsync("action started",name);if(IsDisposed || Disposing)return;await action();await log.WriteAsync("action finished",name+(lastSaveFeedback==null?"":": "+lastSaveFeedback));}
    catch(Exception e){failure=e;lastSaveFeedback=name+" failed: "+e.Message;}
    if(failure!=null){await log.WriteAsync("action failed",name+": "+failure.Message,failure);System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();}
   }finally {busy=false;operationName=null;if(!IsDisposed && !Disposing){SetEditingEnabled(!closing);RefreshFromState();}idle.TrySetResult(true);}
  }
  private void SetEditingEnabled(bool enabled) {
   if(enabled)tabs.Enabled=true;
   cpu.Enabled=gpu.Enabled=profiles.Enabled=deviceSetup.Enabled=settingsPage.Enabled=profileChoice.Enabled=power.Enabled=trayPower.Enabled=enabled;
   checkUpdates.Enabled=enabled && updates!=null && !checkingUpdates;
   diagnostics.SetActionsEnabled(enabled);apply.Enabled=enabled && ProfilePending && CanApply;cancel.Enabled=enabled && ProfilePending;
  }
  private async Task ProfileAction(string action) {
   string selected=profiles.SelectedName??editing.Name;var current=saved.Profiles.Single(p=>p.Name==selected);
   if(action=="Edit") {if(await ChooseProfile(selected,AskSwitch))tabs.SelectedIndex=0;return;}
   lastSaveFeedback="Action canceled.";
   if(action=="Export") {
    using(var dialog=new SaveFileDialog{Title="Export saved profile: "+selected,Filter="Profile JSON|*.json",FileName="holder-profile.json",OverwritePrompt=true}) {
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;string path=dialog.FileName;var snapshot=current.Copy();await Task.Run(()=>store.ExportProfile(snapshot,path));lastSaveFeedback="Saved profile “"+selected+"” exported.";
    }return;
   }
   var collection=saved.Copy();string resultName=null;bool reload=false;
   if(action=="New" || action=="Duplicate") {
    string proposed=ProfileLibrary.UniqueName(collection.Profiles,action=="New"?"My profile":current.Name+" copy");
    string name=NameDialog.Ask(this,action+" profile",proposed);if(name==null)return;
    var value=action=="New"?new Profile():current.PersonalCopy(name);value.Name=name;collection.Add(value);resultName=name;
   }else if(action=="Rename") {
    if(current.IsBuiltIn)throw new InvalidOperationException("Built-in profiles cannot be renamed. Make a copy first.");
    string name=NameDialog.Ask(this,"Rename profile",selected);if(name==null)return;collection.Rename(selected,name);resultName=name;
   }else if(action=="Delete") {
    if(current.IsBuiltIn)throw new InvalidOperationException("Built-in profiles cannot be deleted.");
    if(!AppDialog.Confirm(this,"Delete “"+selected+"”?"+(selected==editing.Name && ContentChanged?" Unsaved edits to this profile will also be discarded.":"")+"\nExport the profile first if you want to keep a copy.","Delete profile","Delete profile"))return;
    collection.Delete(selected);reload=selected==editing.Name;
   }else if(action=="Import") {
    using(var dialog=new OpenFileDialog{Title="Import profile",Filter="Profile JSON|*.json"}) {
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;string path=dialog.FileName;var value=await Task.Run(()=>store.ImportProfile(path));if(IsDisposed || Disposing)return;
     value.Name=ProfileLibrary.UniqueName(collection.Profiles,value.Name);collection.Add(value);resultName=value.Name;
    }
   }else throw new InvalidOperationException("Unknown profile action.");
   await Task.Run(()=>store.Save(collection));if(IsDisposed || Disposing)return;
   bool activeChanged=saved.ActiveProfileName!=collection.ActiveProfileName;saved=collection;
   if(action=="Rename" && selected==editing.Name)editing.Name=resultName;
   if(reload){editing=saved.ActiveProfile.Copy();LoadEditor();}else LoadProfileChoices();
   if(activeChanged)controller.Apply(Configuration(saved));
   if(resultName!=null)profiles.SetProfiles(saved,resultName);
   lastSaveFeedback=action=="Delete"?"Profile deleted.":action=="Rename"?"Profile renamed to “"+resultName+"”.":"“"+resultName+"” added to My profiles. Select Edit to customize or use it.";
  }
 }
}
