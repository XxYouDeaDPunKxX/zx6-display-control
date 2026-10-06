using System;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace ZX6DisplayControl {
 public sealed class MainForm:Form {
  private AppSettings saved,working;private ProfileEditor editor;
  private readonly SettingsStore store;private readonly ISessionController controller;private readonly EventLog log;private readonly StartupRegistration startup;private readonly string exePath;
  private readonly ChannelControl cpu=new ChannelControl{Name="CpuChannel",Dock=DockStyle.Fill},gpu=new ChannelControl(true){Name="GpuChannel",Dock=DockStyle.Fill};
  private readonly ProfilesControl profiles=new ProfilesControl{Dock=DockStyle.Fill};private readonly DiagnosticsControl diagnostics=new DiagnosticsControl{Dock=DockStyle.Fill};
  private readonly DeviceSetupControl deviceSetup=new DeviceSetupControl{Dock=DockStyle.Fill};
  private readonly ComboBox profileChoice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220,AccessibleName="Profile"};
  private readonly Label aidaLabel=new Label{AutoSize=true},holderLabel=new Label{AutoSize=true},notice=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.Firebrick},catalogLabel=new Label{AutoSize=true,ForeColor=Color.DimGray};
  private readonly Label profileStatus=new Label{Name="ProfileStatus",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,3,0,10)};
  private readonly Button apply=new Button{Name="ApplyChanges",Text="&Apply",AutoSize=true,Padding=new Padding(12,4,12,4)},cancel=new Button{Text="&Revert",AutoSize=true},power=new Button{Name="Power",Text="Turn display off",AutoSize=true};
  private readonly TabControl tabs=new ThemedTabControl{Dock=DockStyle.Fill};
  private string lastSaveFeedback;
  private readonly CheckBox startupBox=new CheckBox{Text="Start with Windows",AutoSize=true};
  private readonly CheckBox minimizeBox=new CheckBox{Text="Minimize to tray",AutoSize=true},closeBox=new CheckBox{Text="Close to tray",AutoSize=true};
  private readonly ComboBox themeChoice=new ComboBox{Name="ThemeChoice",DropDownStyle=ComboBoxStyle.DropDownList,Width=220,AccessibleName="Theme",AccessibleDescription="Choose Light, Dark or the Windows app theme. Preview is immediate; Apply saves your choice and Revert restores it."};
  private bool? lastSystemDark,lastHighContrast;private DateTime nextThemeCheck;
  private readonly UiHelp contextHelp;
  private ToolStripMenuItem trayPower,trayStatus;
  private readonly Timer timer=new Timer{Interval=125};private readonly NotifyIcon tray;
  private readonly AnimationEngine cpuPreview=new AnimationEngine(new Random(101)),gpuPreview=new AnimationEngine(new Random(102));private readonly Stopwatch previewClock=Stopwatch.StartNew();private double previousPreview;
  private SensorSnapshot lastCatalog;private bool updating,dirty,exiting,closing,systemClosing;private string shutdownStatus;
  private bool busy;private string operationName;private TaskCompletionSource<bool> operationIdle;private Control settingsPage;
  public event Action BringToFrontRequested;
  public MainForm(AppSettings settings,SettingsStore store,ISessionController controller,EventLog log,StartupRegistration startup,string exePath) {
   saved=settings.Copy();working=settings.Copy();this.store=store;this.controller=controller;this.log=log;this.startup=startup;this.exePath=exePath;editor=new ProfileEditor(working.ActiveProfile);
   Text="Z-X6 Display Control";Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);MinimumSize=new Size(800,640);Size=new Size(1000,840);StartPosition=FormStartPosition.CenterScreen;BackColor=SystemInformation.HighContrast?SystemColors.Control:Color.FromArgb(243,245,247);
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=6,ColumnCount=1,Padding=new Padding(16)};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
   var header=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=2,Margin=new Padding(0,0,0,18)};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   header.Controls.Add(new Label{Text="Z-X6 Display Control",AutoSize=true,Font=new Font("Segoe UI",17,FontStyle.Bold),Margin=new Padding(0,0,24,3)},0,0);
   header.Controls.Add(new Label{Text="Z-X6 GPU support bracket",AutoSize=true,ForeColor=SystemColors.GrayText,Margin=Padding.Empty},0,1);
   header.Controls.Add(aidaLabel,1,0);header.Controls.Add(holderLabel,1,1);root.Controls.Add(header,0,0);
   var toolbar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8)};toolbar.Controls.Add(new Label{Text="Profile",AutoSize=true,Margin=new Padding(0,7,10,0)});toolbar.Controls.Add(profileChoice);toolbar.Controls.Add(power);catalogLabel.Margin=new Padding(14,7,0,0);toolbar.Controls.Add(catalogLabel);root.Controls.Add(toolbar,0,1);root.Controls.Add(notice,0,2);root.Controls.Add(profileStatus,0,3);
   tabs.Padding=new Point(14,7);AddTab(tabs,"CPU",cpu);AddTab(tabs,"GPU",gpu);AddTab(tabs,"Profiles",profiles);AddTab(tabs,"Diagnostics",diagnostics);AddTab(tabs,"Device & setup",deviceSetup);AddTab(tabs,"Settings & About",BuildSettingsPage());root.Controls.Add(tabs,0,4);
   var footer=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Padding=new Padding(0,12,0,0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   footer.Controls.Add(new Label{Text="F1 for help with the selected control",AutoSize=true,ForeColor=SystemColors.GrayText,Margin=new Padding(0,9,0,0)},0,0);
   var actions=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Dock=DockStyle.Fill,MinimumSize=new Size(190,40)};actions.Controls.Add(cancel);actions.Controls.Add(apply);footer.Controls.Add(actions,1,0);root.Controls.Add(footer,0,5);
   Icon=AppIcon.Load();tray=new NotifyIcon{Text="Z-X6 Display Control",Icon=Icon,Visible=true};var menu=new ContextMenuStrip{ShowItemToolTips=true};
   menu.Items.Add(new ToolStripMenuItem("Open Z-X6 Display Control",null,(s,e)=>ShowWindow()){Font=new Font(Font,FontStyle.Bold),ToolTipText="Show the window and keep the current controller session running."});
   trayStatus=new ToolStripMenuItem("Connecting…"){Enabled=false};menu.Items.Add(trayStatus);menu.Items.Add(new ToolStripSeparator());
   trayPower=new ToolStripMenuItem("Turn display off",null,(s,e)=>AttemptAsync(TogglePower)){ToolTipText="Toggle display power immediately without applying pending edits."};menu.Items.Add(trayPower);
   menu.Items.Add(new ToolStripMenuItem("Settings",null,(s,e)=>{ShowWindow();tabs.SelectedIndex=5;}){ToolTipText="Configure startup, minimizing and closing behavior."});menu.Items.Add(new ToolStripSeparator());
   menu.Items.Add(new ToolStripMenuItem("Exit",null,(s,e)=>ExitApp()){ToolTipText="Stop the controller and release the USB port. Unsaved changes require a choice."});tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>ShowWindow();
   apply.Click+=(s,e)=>AttemptAsync(ApplyChanges);cancel.Click+=(s,e)=>CancelChanges();power.Click+=(s,e)=>AttemptAsync(TogglePower);
   cpu.DraftChanged+=(s,e)=>Changed();gpu.DraftChanged+=(s,e)=>Changed();profileChoice.SelectedIndexChanged+=(s,e)=>{if(!updating)Attempt(()=>ChooseProfile(profileChoice.SelectedItem as string));};
   profiles.Requested+=action=>AttemptAsync(()=>RunOperation("Profile: "+action,()=>ProfileAction(action)));diagnostics.RetryRequested+=RetryConnection;diagnostics.ExportRequested+=()=>AttemptAsync(()=>RunOperation("Export diagnostics",ExportDiagnostics));
   foreach(var option in new[]{startupBox,minimizeBox,closeBox})option.CheckedChanged+=(s,e)=>{if(!updating)Changed();};deviceSetup.RetryRequested+=RetryConnection;
   themeChoice.SelectedIndexChanged+=(s,e)=>{if(!updating){ApplyTheme();Changed();}};Shown+=(s,e)=>{ApplyTheme();controller.Start();timer.Start();};
   Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized && saved.MinimizeToTray)Hide();};
   contextHelp=new UiHelp(this);timer.Tick+=(s,e)=>RefreshFromState();FormClosing+=OnFormClosing;LoadEditor();
  }
  private Control BuildSettingsPage() {
   var page=new UserControl{Dock=DockStyle.Fill};settingsPage=page;var table=InfoPage.Layout(page);
   InfoPage.Heading(table,"Appearance");themeChoice.Items.AddRange(new object[]{"System","Light","Dark"});InfoPage.Add(table,themeChoice);
   InfoPage.Heading(table,"Startup & window");InfoPage.Add(table,startupBox);InfoPage.Add(table,minimizeBox);InfoPage.Add(table,closeBox);
   InfoPage.Paragraph(table,"When running in the tray, the controller keeps updating the holder. Double-click its notification-area icon to reopen the window. Exit stops the controller and releases the USB port.");
   InfoPage.Heading(table,"Local data");InfoPage.Paragraph(table,"Settings, backups and logs are stored in your Windows user profile.");
   var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var folder=new Button{Text="Open data folder",AutoSize=true};folder.Click+=(s,e)=>AttemptAsync(()=>RunOperation("Open data folder",()=>Task.Run(()=>{Directory.CreateDirectory(store.DirectoryPath);Process.Start(new ProcessStartInfo(store.DirectoryPath){UseShellExecute=true});})));var export=new Button{Text="Export diagnostics…",AutoSize=true};export.Click+=(s,e)=>AttemptAsync(()=>RunOperation("Export diagnostics",ExportDiagnostics));actions.Controls.Add(folder);actions.Controls.Add(export);InfoPage.Add(table,actions);
   InfoPage.Heading(table,"About");InfoPage.Paragraph(table,"Z-X6 Display Control · 0.1.0-beta.1\nControls the Z-X6 display using sensor readings from AIDA64.");
   var exit=new Button{Text="Exit",AutoSize=true};exit.Click+=(s,e)=>ExitApp();InfoPage.Add(table,exit);return page;
  }
  private void ApplyTheme() {UiTheme.Apply(this,(AppTheme)Math.Max(0,themeChoice.SelectedIndex));UiTheme.Menu(tray.ContextMenuStrip);notice.ForeColor=UiTheme.Error;}
  private static void AddTab(TabControl tabs,string name,Control content) {var page=new TabPage(name){BackColor=Color.White,Padding=new Padding(0)};page.Controls.Add(content);tabs.TabPages.Add(page);}
  public bool CanApply {get{return cpu.ValidationErrors().Count==0 && gpu.ValidationErrors().Count==0;}}
  private Profile ReadDraft() {if(!CanApply)throw new InvalidOperationException("Fix the highlighted settings before applying.");editor.Draft.Cpu=cpu.GetDraft();editor.Draft.Gpu=gpu.GetDraft();return editor.Apply();}
  private void ReplaceWorking(Profile profile) {int index=working.Profiles.FindIndex(p=>p.Name==profile.Name);if(index<0)throw new InvalidOperationException("Profile not found.");working.Profiles[index]=profile.Copy();}
  public Task ApplyChanges() {return RunOperation("Save settings",ApplyCore);}
  private async Task ApplyCore() {
   var profile=ReadDraft();ReplaceWorking(profile);working.ActiveProfileName=profile.Name;working.StartWithWindows=startupBox.Checked;working.CloseToTray=closeBox.Checked;working.MinimizeToTray=minimizeBox.Checked;working.Theme=(AppTheme)themeChoice.SelectedIndex;
   working.Validate();var snapshot=working.Copy();await Task.Run(()=>startup.CommitEnabled(snapshot.StartWithWindows,exePath,()=>store.Save(snapshot)));
   if(IsDisposed || Disposing)return;saved=snapshot;controller.Apply(Configuration(saved));profiles.SetProfiles(working,profiles.SelectedName??editor.Draft.Name);dirty=false;lastSaveFeedback="Profile “"+saved.ActiveProfileName+"” saved.";RefreshFromState();
  }
  public void CancelChanges() {working=saved.Copy();editor=new ProfileEditor(working.ActiveProfile);dirty=false;lastSaveFeedback="Changes reverted. Active profile: “"+saved.ActiveProfileName+"”.";LoadEditor();}
  private void LoadEditor() {
   updating=true;try {var snapshot=controller.State==null?null:controller.State.Snapshot;cpu.Load(editor.Draft.Cpu,snapshot);gpu.Load(editor.Draft.Gpu,snapshot);profileChoice.Items.Clear();profileChoice.Items.AddRange(working.Profiles.Select(p=>(object)p.Name).ToArray());profileChoice.SelectedItem=editor.Draft.Name;profiles.SetProfiles(working,editor.Draft.Name);startupBox.Checked=working.StartWithWindows;closeBox.Checked=working.CloseToTray;minimizeBox.Checked=working.MinimizeToTray;}finally{updating=false;}
   updating=true;themeChoice.SelectedIndex=(int)working.Theme;updating=false;ApplyTheme();cpuPreview.Reset();gpuPreview.Reset();RefreshFromState();
  }
  private void ChooseProfile(string name) {
   if(string.IsNullOrEmpty(name) || name==editor.Draft.Name)return;
   if(!CanApply) {updating=true;profileChoice.SelectedItem=editor.Draft.Name;updating=false;throw new InvalidOperationException("Apply or revert the invalid settings before changing profile.");}
   ReplaceWorking(ReadDraft());working.ActiveProfileName=name;editor=new ProfileEditor(working.ActiveProfile);dirty=true;LoadEditor();
  }
  private void Changed() {if(updating)return;working.StartWithWindows=startupBox.Checked;working.CloseToTray=closeBox.Checked;working.MinimizeToTray=minimizeBox.Checked;working.Theme=(AppTheme)Math.Max(0,themeChoice.SelectedIndex);dirty=true;lastSaveFeedback=null;apply.Enabled=CanApply;Text="Z-X6 Display Control · unsaved changes";}
  private static SessionConfiguration Configuration(AppSettings value) {return new SessionConfiguration{Cpu=value.ActiveProfile.Cpu.Copy(),Gpu=value.ActiveProfile.Gpu.Copy(),DisplayEnabled=value.DisplayEnabled};}
  private Task TogglePower() {return RunOperation("Change display power",async()=>{var snapshot=saved.Copy();snapshot.DisplayEnabled=!snapshot.DisplayEnabled;await Task.Run(()=>store.Save(snapshot));if(IsDisposed || Disposing)return;saved=snapshot;working.DisplayEnabled=saved.DisplayEnabled;controller.Apply(Configuration(saved));});}
  private void RetryConnection(){AttemptAsync(()=>RunOperation("Refresh connection",()=>{controller.Retry();return Task.FromResult(true);}));}
  private async Task RunOperation(string name,Func<Task> action,bool duringExit=false) {
   if(busy || (closing && !duringExit))throw new InvalidOperationException("Wait for the current operation to finish.");
   busy=true;operationName=name;operationIdle=new TaskCompletionSource<bool>();var idle=operationIdle;lastSaveFeedback=null;SetEditingEnabled(false);RefreshFromState();
   try {
    Exception failure=null;
    try{await log.WriteAsync("action started",name);if(IsDisposed || Disposing)return;await action();if(lastSaveFeedback==null)lastSaveFeedback=name+" completed.";await log.WriteAsync("action completed",name);}
    catch(Exception e){failure=e;lastSaveFeedback=name+" failed: "+e.Message;}
    if(failure!=null){await log.WriteAsync("action failed",name+": "+failure.Message,failure);System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();}
   }
   finally{busy=false;operationName=null;if(!IsDisposed && !Disposing){SetEditingEnabled(!closing);RefreshFromState();}idle.TrySetResult(true);}
  }
  private void SetEditingEnabled(bool enabled) {
   if(enabled)tabs.Enabled=true;
   cpu.Enabled=gpu.Enabled=profiles.Enabled=deviceSetup.Enabled=settingsPage.Enabled=profileChoice.Enabled=power.Enabled=trayPower.Enabled=enabled;
   diagnostics.SetActionsEnabled(enabled);apply.Enabled=enabled && dirty && CanApply;cancel.Enabled=enabled && dirty;
  }
  public void RefreshFromState() {
   // Keep open drop-downs stable while the session worker updates the holder.
   if(!closing && HasOpenDropDown(this))return;
   diagnostics.UpdateLog(log,closing?shutdownStatus:operationName);
   if(closing){notice.Text=shutdownStatus;notice.Visible=true;apply.Enabled=cancel.Enabled=power.Enabled=profileChoice.Enabled=tabs.Enabled=trayPower.Enabled=false;return;}
   if(DateTime.UtcNow>=nextThemeCheck){nextThemeCheck=DateTime.UtcNow.AddSeconds(3);bool systemDark=UiTheme.SystemIsDark(),highContrast=SystemInformation.HighContrast;if(lastHighContrast!=highContrast || (themeChoice.SelectedIndex==0 && lastSystemDark!=systemDark)){lastSystemDark=systemDark;lastHighContrast=highContrast;ApplyTheme();}}
   var state=controller.State;if(state==null){notice.Text=controller.FailureMessage??"Starting controller…";notice.Visible=true;return;}bool failed=!string.IsNullOrEmpty(controller.FailureMessage);
   aidaLabel.Text=state.AidaStatus=="Ready"?"AIDA64 · connected":"AIDA64 · "+(state.AidaStatus=="TemperatureInvalid"?"check sensors":"unavailable");aidaLabel.ForeColor=state.AidaStatus=="Ready"?UiTheme.Success:UiTheme.Warning;
   holderLabel.Text="Holder · "+DeviceText(state.DeviceStatus);holderLabel.ForeColor=state.Connected?UiTheme.Success:UiTheme.Warning;power.Text=state.RequestedDisplayOn?"Turn display off":"Turn display on";
   notice.Text=string.Join(" ",new[]{controller.FailureMessage??state.Error,log.LastError==null?null:"Log file unavailable: "+log.LastError}.Where(x=>!string.IsNullOrEmpty(x)));notice.Visible=!string.IsNullOrWhiteSpace(notice.Text);
   if(!object.ReferenceEquals(lastCatalog,state.Snapshot)) {lastCatalog=state.Snapshot;cpu.SetCatalog(lastCatalog);gpu.SetCatalog(lastCatalog);catalogLabel.Text=lastCatalog==null?"No sensor data":lastCatalog.Values.Count+" AIDA64 sensors";}
   double now=previewClock.Elapsed.TotalSeconds,elapsed=now-previousPreview;previousPreview=now;
   var cpuErrors=cpu.ValidationErrors();var gpuErrors=gpu.ValidationErrors();bool available=!failed && (state.AidaStatus=="Ready" || state.AidaStatus=="TemperatureInvalid");
   if(failed){aidaLabel.Text="AIDA64 · readings stopped";aidaLabel.ForeColor=UiTheme.Warning;holderLabel.Text="Holder · controller stopped";holderLabel.ForeColor=UiTheme.Error;}
   cpu.SetAvailable(available);gpu.SetAvailable(available);deviceSetup.UpdateState(state);
   if(cpuErrors.Count==0) {var c=cpu.GetDraft();cpu.SetPreview(cpuPreview.Advance(c.Animation,SensorNumber(available?lastCatalog:null,c.Animation.SensorId),elapsed,c.Paused));}
   if(gpuErrors.Count==0) {var g=gpu.GetDraft();gpu.SetPreview(gpuPreview.Advance(g.Animation,SensorNumber(available?lastCatalog:null,g.Animation.SensorId),elapsed,g.Paused));}
   string validation=string.Join(" · ",cpuErrors.Select(e=>"CPU: "+e).Concat(gpuErrors.Select(e=>"GPU: "+e)));
   profileStatus.Text=busy?operationName+"…":validation.Length>0?validation:lastSaveFeedback??(dirty?"Unsaved changes · Active profile: “"+saved.ActiveProfileName+"”.":"Active profile: “"+saved.ActiveProfileName+"”.");
   profileStatus.ForeColor=validation.Length>0?UiTheme.Error:UiTheme.Muted;
   apply.Enabled=!busy && dirty && CanApply;cancel.Enabled=!busy && dirty;Text="Z-X6 Display Control"+(dirty?" · unsaved changes":"");diagnostics.UpdateState(state,available);
   tray.Text=("Z-X6 Display Control · "+DeviceText(state.DeviceStatus)).Substring(0,Math.Min(63,("Z-X6 Display Control · "+DeviceText(state.DeviceStatus)).Length));
   trayPower.Text=power.Text;trayStatus.Text=failed?"Controller stopped":state.Connected?(state.DisplayOff?"Display off · ":"Display enabled · ")+ (available?"AIDA64 connected":"AIDA64 unavailable"):"Holder "+DeviceText(state.DeviceStatus);
   if(BringToFrontRequested!=null)BringToFrontRequested();
  }
  private static bool HasOpenDropDown(Control root) {
   var combo=root as ComboBox;
   if(combo!=null && combo.IsHandleCreated && combo.DroppedDown)return true;
   foreach(Control child in root.Controls)if(HasOpenDropDown(child))return true;
   return false;
  }
  private static double? SensorNumber(SensorSnapshot snapshot,string id) {SensorValue row;return snapshot!=null && id!=null && snapshot.Values.TryGetValue(id,out row)?row.Number:null;}
  private static string DeviceText(string state) {switch(state){case "Connected":return "connected";case "Initializing":return "connecting";case "PortBusy":return "port busy";case "Ambiguous":return "multiple displays detected";case "Suspended":return "suspended";case "Stopped":return "stopped";case "ConnectionError":return "connection error";default:return "disconnected";}}
  private async Task ProfileAction(string action) {
   string selected=profiles.SelectedName??editor.Draft.Name;
   if(action=="Edit") {ChooseProfile(selected);tabs.SelectedIndex=0;return;}
   if(!CanApply) throw new InvalidOperationException("Fix or revert invalid settings before editing profiles.");ReplaceWorking(ReadDraft());
   if(action.StartsWith("Preset|",StringComparison.Ordinal)) {var preset=ProfileLibrary.Create().Single(p=>p.Name==action.Substring(7));string proposed=preset.Name;int suffix=2;while(working.Profiles.Any(p=>string.Equals(p.Name,proposed,StringComparison.OrdinalIgnoreCase)))proposed=preset.Name+" "+suffix++;string name=NameDialog.Ask(this,"Add preset",proposed);if(name==null)return;preset.Name=name;working.Add(preset);working.ActiveProfileName=name;editor=new ProfileEditor(working.ActiveProfile);dirty=true;LoadEditor();tabs.SelectedIndex=0;return;}
   var current=working.Profiles.Single(p=>p.Name==selected);
   if(action=="New" || action=="Duplicate") {string name=NameDialog.Ask(this,action+" profile",action=="New"?"New profile":current.Name+" copy");if(name==null)return;var value=action=="New"?new Profile():current.Copy();value.Name=name;working.Add(value);working.ActiveProfileName=name;}
   else if(action=="Rename") {string name=NameDialog.Ask(this,"Rename profile",selected);if(name==null)return;working.Rename(selected,name);}
   else if(action=="Delete") {if(AppDialog.Show(this,"Delete profile “"+selected+"”?","Profiles",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;working.Delete(selected);}
   else if(action=="Restore defaults") {if(AppDialog.Show(this,"Replace this profile collection with the eight Standard and Creative presets? Revert can undo this before Apply.","Profiles",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;var defaults=AppSettings.Defaults();working.Profiles=defaults.Profiles;working.ActiveProfileName=defaults.ActiveProfileName;}
   else if(action=="Import") {using(var dialog=new OpenFileDialog{Filter="Profile JSON|*.json"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;string path=dialog.FileName;var value=await Task.Run(()=>store.ImportProfile(path));if(IsDisposed || Disposing)return;int at=working.Profiles.FindIndex(p=>string.Equals(p.Name,value.Name,StringComparison.OrdinalIgnoreCase));if(at>=0){if(AppDialog.Show(this,"Replace profile “"+value.Name+"”?","Import profile",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;working.Profiles[at]=value;}else working.Add(value);working.ActiveProfileName=value.Name;}}
   else if(action=="Export") {using(var dialog=new SaveFileDialog{Filter="Profile JSON|*.json",FileName="holder-profile.json",OverwritePrompt=true}){if(dialog.ShowDialog(this)==DialogResult.OK){string path=dialog.FileName;var snapshot=current.Copy();await Task.Run(()=>store.ExportProfile(snapshot,path));}}return;}
   editor=new ProfileEditor(working.ActiveProfile);dirty=true;LoadEditor();
  }
  private async Task ExportDiagnostics() {using(var dialog=new SaveFileDialog{Filter="Diagnostics text|*.txt",FileName="holder-diagnostics.txt",OverwritePrompt=true})if(dialog.ShowDialog(this)==DialogResult.OK){string path=dialog.FileName;var state=controller.State;var snapshot=saved.Copy();await Task.Run(()=>log.Export(path,state,snapshot,"0.1.0-beta.1"));}}
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
   closing=true;shutdownStatus=busy?"Finishing the current operation…":"Stopping controller…";RefreshFromState();FinishExit(ask,decision,askAfterOperation);return true;
  }
  private async void FinishExit(Func<DialogResult> ask,DialogResult decision,bool askAfterOperation) {
   try {
    if(busy){var pending=operationIdle.Task;if(await Task.WhenAny(pending,Task.Delay(5000))!=pending){if(IsDisposed || Disposing)return;shutdownStatus="Still finishing the current file operation. The app will close when it completes.";RefreshFromState();}await pending;}if(IsDisposed || Disposing)return;
    if(askAfterOperation && dirty){decision=ask();if(decision!=DialogResult.Yes && decision!=DialogResult.No){closing=false;SetEditingEnabled(true);RefreshFromState();return;}}
    if(decision==DialogResult.Yes)await RunOperation("Save settings",ApplyCore,true);
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
   if(!saved.CloseToTrayExplained && !busy){tray.ShowBalloonTip(5000,"Z-X6 Display Control is still running","Open the tray icon to return, or choose Exit to stop the controller.",ToolTipIcon.Info);AttemptAsync(()=>RunOperation("Remember tray preference",async()=>{var snapshot=saved.Copy();snapshot.CloseToTrayExplained=true;await Task.Run(()=>store.Save(snapshot));if(IsDisposed || Disposing)return;saved=snapshot;working.CloseToTrayExplained=true;}));}
   Hide();
  }
  protected override void Dispose(bool disposing) {if(disposing){if(!closing)controller.Stop();closing=true;timer.Stop();timer.Dispose();if(contextHelp!=null)contextHelp.Dispose();tray.Visible=false;var menu=tray.ContextMenuStrip;tray.Dispose();if(menu!=null)menu.Dispose();if(Icon!=null)Icon.Dispose();}base.Dispose(disposing);}
 }
}
