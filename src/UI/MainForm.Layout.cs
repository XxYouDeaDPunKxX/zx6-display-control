using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZX6DisplayControl {
 public sealed partial class MainForm {
  private AppSettings saved;
  private Profile editing;
  private readonly SettingsStore store;
  private readonly ISessionController controller;
  private readonly EventLog log;
  private readonly StartupRegistration startup;
  private readonly string exePath;
  private readonly ChannelControl cpu=new ChannelControl{Name="CpuChannel",Dock=DockStyle.Fill};
  private readonly ChannelControl gpu=new ChannelControl(true){Name="GpuChannel",Dock=DockStyle.Fill};
  private readonly ProfilesControl profiles=new ProfilesControl{Dock=DockStyle.Fill};
  private readonly DiagnosticsControl diagnostics=new DiagnosticsControl{Dock=DockStyle.Fill};
  private readonly DeviceSetupControl deviceSetup=new DeviceSetupControl{Dock=DockStyle.Fill};
  private readonly HolderPreview displayPreview=new HolderPreview{Dock=DockStyle.Fill};
  private readonly ComboBox profileChoice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=250,AccessibleName="Profile"};
  private readonly Button aidaStatus=ActionButton("AidaStatus","AIDA64 · checking…","Check sensor sharing and connection details.");
  private readonly Button holderStatus=ActionButton("HolderStatus","Display · connecting…","Check the USB connection and reconnect the display.");
  private readonly Label notice=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.Firebrick};
  private readonly Label profileStatus=new Label{Name="ProfileStatus",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,4,0,8)};
  private readonly Button apply=ActionButton("ApplyChanges","Use profile","Save this profile and use it on the display.");
  private readonly Button cancel=ActionButton("RevertProfile","Revert profile","Discard edits to this profile. App preferences are kept.");
  private readonly Button power=ActionButton("Power","Turn display off","Change display power immediately without saving profile edits.");
  private readonly Button savePreferences=ActionButton("SavePreferences","Save preferences","Save app preferences and update Windows startup registration for this copy.");
  private readonly Button revertPreferences=ActionButton("RevertPreferences","Revert preferences","Discard unsaved app preferences. Profile edits are kept.");
  private readonly TabControl tabs=new ThemedTabControl{Dock=DockStyle.Fill};
  private readonly TabControl channels=new ThemedTabControl{Dock=DockStyle.Fill};
  private readonly CheckBox startupBox=new CheckBox{Text="Start with Windows",AutoSize=true};
  private readonly CheckBox minimizeBox=new CheckBox{Text="Minimize to tray",AutoSize=true};
  private readonly CheckBox closeBox=new CheckBox{Text="Close to tray",AutoSize=true};
  private readonly ComboBox themeChoice=new ComboBox{Name="ThemeChoice",DropDownStyle=ComboBoxStyle.DropDownList,Width=220,AccessibleName="Theme",AccessibleDescription="Preview Light, Dark or the Windows theme. Save preferences keeps your choice; Revert preferences restores it."};
  private readonly Label startupStatus=new Label{AutoSize=true,Dock=DockStyle.Top,Text="Startup registration will be checked when the window opens."};
  private readonly Label preferenceStatus=new Label{AutoSize=true,Dock=DockStyle.Top};
  private readonly UiHelp contextHelp;
  private readonly Timer timer=new Timer{Interval=125};
  private readonly NotifyIcon tray;
  private ToolStripMenuItem trayPower,trayStatus;
  private readonly AnimationEngine cpuPreview=new AnimationEngine(new Random(101)),gpuPreview=new AnimationEngine(new Random(102));
  private readonly Stopwatch previewClock=Stopwatch.StartNew();
  private double previousPreview;
  private bool updating,busy,exiting,closing,systemClosing;
  private bool? lastSystemDark,lastHighContrast;
  private DateTime nextThemeCheck;
  private string shutdownStatus,operationName,lastSaveFeedback;
  private TaskCompletionSource<bool> operationIdle;
  private Control settingsPage;
  private bool dirty {get{return ProfilePending || PreferencesPending;}}
  public event Action BringToFrontRequested;

  public MainForm(AppSettings settings,SettingsStore store,ISessionController controller,EventLog log,StartupRegistration startup,string exePath) {
   saved=settings.Copy();editing=saved.ActiveProfile.Copy();this.store=store;this.controller=controller;this.log=log;this.startup=startup;this.exePath=exePath;
   Text="Z-X6 Display Control";Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
   MinimumSize=new Size(900,700);Size=new Size(1180,840);StartPosition=FormStartPosition.CenterScreen;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(18)};
   root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
   root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
   var header=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Margin=new Padding(0,0,0,4)};
   header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   header.Controls.Add(new Label{Text="Z-X6 Display Control",AutoSize=true,Font=new Font("Segoe UI",18,FontStyle.Bold),Margin=new Padding(0,4,15,0)},0,0);
   aidaStatus.AutoSize=holderStatus.AutoSize=false;aidaStatus.Size=holderStatus.Size=new Size(205,44);
   var statuses=new FlowLayoutPanel{AutoSize=true,WrapContents=false};statuses.Controls.Add(aidaStatus);statuses.Controls.Add(holderStatus);header.Controls.Add(statuses,1,0);root.Controls.Add(header,0,0);
   root.Controls.Add(notice,0,1);root.Controls.Add(profileStatus,0,2);
   var display=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(12)};
   display.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));display.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,61));display.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(6,6,18,6)};
   left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   var choiceRow=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1};choiceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   InfoPage.Add(choiceRow,new Label{Text="Editing profile",AutoSize=true,Font=new Font(Font,FontStyle.Bold)});profileChoice.Dock=DockStyle.Top;InfoPage.Add(choiceRow,profileChoice);
   left.Controls.Add(choiceRow,0,0);left.Controls.Add(displayPreview,0,1);power.Dock=DockStyle.Top;left.Controls.Add(power,0,2);
   AddTab(channels,"CPU",cpu);AddTab(channels,"GPU",gpu);channels.Padding=new Point(20,8);display.Controls.Add(left,0,0);display.Controls.Add(channels,1,0);
   tabs.Padding=new Point(16,8);AddTab(tabs,"Display",display);AddTab(tabs,"Profiles",profiles);AddTab(tabs,"Connection",deviceSetup);AddTab(tabs,"Diagnostics",diagnostics);AddTab(tabs,"Settings & About",BuildSettingsPage());root.Controls.Add(tabs,0,3);
   var footer=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(0,12,0,0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   footer.Controls.Add(new Label{Text="F1 · Help for the selected control",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,10,0,0)},0,0);
   var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=false};actions.Controls.Add(cancel);actions.Controls.Add(apply);footer.Controls.Add(actions,1,0);root.Controls.Add(footer,0,4);
   Icon=AppIcon.Load();tray=new NotifyIcon{Text="Z-X6 Display Control",Icon=Icon,Visible=true};var menu=new ContextMenuStrip{ShowItemToolTips=true};
   menu.Items.Add(new ToolStripMenuItem("Open Z-X6 Display Control",null,(s,e)=>ShowWindow()){Font=new Font(Font,FontStyle.Bold)});
   trayStatus=new ToolStripMenuItem("Connecting…"){Enabled=false};menu.Items.Add(trayStatus);menu.Items.Add(new ToolStripSeparator());
   trayPower=new ToolStripMenuItem("Turn display off",null,(s,e)=>AttemptAsync(TogglePower));menu.Items.Add(trayPower);
   menu.Items.Add(new ToolStripMenuItem("Settings",null,(s,e)=>{ShowWindow();tabs.SelectedIndex=4;}));menu.Items.Add(new ToolStripSeparator());menu.Items.Add(new ToolStripMenuItem("Exit",null,(s,e)=>ExitApp()));tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>ShowWindow();
   apply.Click+=(s,e)=>AttemptAsync(ApplyChanges);cancel.Click+=(s,e)=>CancelChanges();power.Click+=(s,e)=>AttemptAsync(TogglePower);
   aidaStatus.Click+=(s,e)=>tabs.SelectedIndex=2;holderStatus.Click+=(s,e)=>tabs.SelectedIndex=2;
   cpu.DraftChanged+=(s,e)=>Changed();gpu.DraftChanged+=(s,e)=>Changed();
   profileChoice.SelectedIndexChanged+=(s,e)=>{if(!updating){string name=profileChoice.SelectedItem as string;AttemptAsync(()=>RunOperation("Change profile",()=>ChooseProfile(name,AskSwitch)));}};
   profiles.Requested+=action=>AttemptAsync(()=>RunOperation("Profile: "+action,()=>ProfileAction(action)));
   diagnostics.RetryRequested+=RetryConnection;diagnostics.ExportRequested+=()=>AttemptAsync(()=>RunOperation("Export diagnostics",ExportDiagnostics));deviceSetup.RetryRequested+=RetryConnection;
   deviceSetup.EditSensorsRequested+=()=>{tabs.SelectedIndex=0;channels.SelectedIndex=0;cpu.Focus();};deviceSetup.ExitRequested+=ExitApp;
   foreach(var box in new[]{startupBox,minimizeBox,closeBox})box.CheckedChanged+=(s,e)=>{if(!updating)Changed();};
   themeChoice.SelectedIndexChanged+=(s,e)=>{if(!updating){ApplyTheme();Changed();}};
   savePreferences.Click+=(s,e)=>AttemptAsync(SavePreferences);revertPreferences.Click+=(s,e)=>RevertPreferences();
   Shown+=(s,e)=>{ApplyTheme();controller.Start();timer.Start();AttemptAsync(RefreshStartup);};
   Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized && saved.MinimizeToTray)Hide();};
   contextHelp=new UiHelp(this);timer.Tick+=(s,e)=>RefreshFromState();FormClosing+=OnFormClosing;
   LoadPreferences();LoadEditor();
  }
  private static Button ActionButton(string name,string text,string help) {return new Button{Name=name,Text=text,AutoSize=true,Padding=new Padding(10,5,10,5),AccessibleDescription=help};}
  private Control BuildSettingsPage() {
   var page=new UserControl{Dock=DockStyle.Fill};settingsPage=page;var table=InfoPage.Layout(page);
   InfoPage.Heading(table,"Appearance");themeChoice.Items.AddRange(new object[]{"System","Light","Dark"});InfoPage.Add(table,themeChoice);
   InfoPage.Heading(table,"Startup & window");InfoPage.Add(table,startupBox);InfoPage.Add(table,startupStatus);InfoPage.Add(table,minimizeBox);InfoPage.Add(table,closeBox);
   InfoPage.Paragraph(table,"The controller keeps running in the tray. Double-click its icon to reopen this window; Exit stops it and releases the USB port.");
   var prefs=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};prefs.Controls.Add(revertPreferences);prefs.Controls.Add(savePreferences);InfoPage.Add(table,prefs);InfoPage.Add(table,preferenceStatus);
   InfoPage.Heading(table,"Local data");InfoPage.Paragraph(table,"Settings, backups and logs are stored in your Windows user profile.");
   var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var folder=ActionButton("","Open data folder","Open settings, backups and logs.");
   folder.Click+=(s,e)=>AttemptAsync(()=>RunOperation("Open data folder",()=>Task.Run(()=>{Directory.CreateDirectory(store.DirectoryPath);Process.Start(new ProcessStartInfo(store.DirectoryPath){UseShellExecute=true});})));
   var export=ActionButton("","Export diagnostics…","Save connection details, sensor IDs and recent events to a file.");export.Click+=(s,e)=>AttemptAsync(()=>RunOperation("Export diagnostics",ExportDiagnostics));actions.Controls.Add(folder);actions.Controls.Add(export);InfoPage.Add(table,actions);
   InfoPage.Heading(table,"About");InfoPage.Paragraph(table,"Z-X6 Display Control · 0.1.0-beta.1\nControls the Z-X6 display using sensor readings from AIDA64.");
   var exit=ActionButton("","Exit","Stop the controller and close the app.");exit.Click+=(s,e)=>ExitApp();InfoPage.Add(table,exit);return page;
  }
  private void ApplyTheme() {UiTheme.Apply(this,(AppTheme)Math.Max(0,themeChoice.SelectedIndex));UiTheme.Menu(tray.ContextMenuStrip);notice.ForeColor=UiTheme.Error;}
  private static void AddTab(TabControl target,string title,Control content) {var page=new TabPage(title);page.Controls.Add(content);target.TabPages.Add(page);}
 }
}
