using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 internal sealed class UiHelp:IDisposable {
  private readonly ToolTip tips=new ToolTip{InitialDelay=450,ReshowDelay=150,AutoPopDelay=15000,ShowAlways=true};
  private static readonly Dictionary<string,string> help=new Dictionary<string,string> {
   {"Profile","Load a profile into the editor. Apply saves the selection and starts using it on the holder."},
   {"ApplyChanges","Save your changes and activate the selected profile on the display."},
   {"Revert","Discard all pending profile and app-setting changes. The active configuration stays in use."},
   {"Power","Turn the entire display on or off immediately. This does not apply pending edits."},
   {"Reconnect","Retry USB detection now. Only one application can use the holder's serial port at a time."},
   {"Exit","Stop the controller, release the USB port and close the app. You will be asked about unsaved changes."},
   {"Mode","Loop plays the built-in bar animation. Sensor level maps a reading to one of eight levels. Fixed level holds a chosen level."},
   {"Sequence","Fill, empty, bounce between both ends, or jump between random levels. Each side runs independently."},
   {"SimplePreset","Choose a ready-made speed or sensor response. Select Custom to adjust its range and timing."},
   {"AdvancedOptions","Show sensor ranges, exact speed limits, smoothing and response direction for this side only."},
   {"RateMode","Use a constant speed or let the selected sensor control speed within your limits."},
   {"InputMin","Sensor reading mapped to the lowest speed or empty bar. Values below this limit are clamped."},
   {"InputMax","Sensor reading mapped to the highest speed or full bar. Must exceed the minimum input."},
   {"FixedFps","Animation updates per second, from 1 to 4. Decimal values are supported."},
   {"MinFps","Slowest sensor-driven animation speed, from 1 to 4 updates per second."},
   {"MaxFps","Fastest sensor-driven animation speed, from 1 to 4 updates per second. Must be at least the minimum speed."},
   {"FilterSeconds","Smooth sensor changes over this response time. Off reacts immediately; longer times reduce jitter."},
   {"HysteresisPercent","Margin around each level boundary to prevent flickering when the reading hovers near it. Range: 0–10%."},
   {"FixedFrame","Built-in bar level: 0 is empty and 7 is full. The temperature number continues updating."},
   {"Invert sensor response","Reverse the mapping: higher readings produce a lower speed or bar level."},
   {"Pause animation","Hold this side's bar at its current level while keeping the temperature number updated."},
   {"Use this sensor range","Confirm the input range for a sensor whose unit has no preset. Check its normal values in Diagnostics first."},
   {"SensorSearch","Filter exported sensors by their AIDA64 name or ID. This does not change the selected sensor."},
   {"Sensor category","Narrow the list by measurement type. Display numbers accept temperature sensors only."},
   {"SensorChoice","Choose a sensor exported by AIDA64. Names, measurement types, units and IDs distinguish similar entries."},
   {"Search or filter sensors","Show optional search and category filters. Clear search restores the full list."},
   {"Detected USB display","The holder is detected automatically, even if its COM port changes. This beta supports one Z-X6 at a time. Disconnect any additional matching holders."},
   {"Exported AIDA64 sensors","Live values exported through AIDA64 shared memory. These are the sensors available to this app."},
   {"CatalogSearch","Filter the diagnostic sensor list by name or ID without changing any profile."},
   {"Refresh connection","Retry USB detection now. Existing sensor and profile settings are retained."},
   {"Export diagnostics…","Save device status, selected sensor IDs, counters and recent app events to a text file you can inspect and share."},
   {"Available profiles","Select a saved profile for the actions below. Editing a profile does not activate it until Apply."},
   {"Edit","Load the selected profile into the CPU and GPU editors."},
   {"New","Create a profile with default temperature sources and animation settings."},
   {"Duplicate","Create a named copy of the selected profile, preserving both sides' settings."},
   {"More…","Rename, import, export or delete profiles, or restore the default collection."},
   {"Start with Windows","Start this copy of the app in the notification area when you sign in. Apply saves this choice."},
   {"Minimize to tray","Hide the taskbar window when minimized. Open it again from the notification-area icon."},
   {"Close to tray","Keep the controller running when you close the window. Choose Exit to stop it completely."},
   {"Open data folder","Open the folder containing settings, backups and logs."},
   {"Profile name","Enter a unique name for this profile. Existing custom names are preserved."},
   {"OK","Confirm the name and return to the profile editor. Apply saves the profile collection."},
   {"Cancel","Close this dialog without changing the profile name."}
  };
  public UiHelp(Control root) {
   tips.OwnerDraw=true;tips.Draw+=(s,e)=>{using(var brush=new SolidBrush(UiTheme.Input))e.Graphics.FillRectangle(brush,e.Bounds);using(var pen=new Pen(UiTheme.Muted))e.Graphics.DrawRectangle(pen,0,0,e.Bounds.Width-1,e.Bounds.Height-1);TextRenderer.DrawText(e.Graphics,e.ToolTipText,root.Font,Rectangle.Inflate(e.Bounds,-5,-3),UiTheme.Text,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);};
   tips.Popup+=(s,e)=>{string text=tips.GetToolTip(e.AssociatedControl);var size=TextRenderer.MeasureText(text,root.Font,new Size(390,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);e.ToolTipSize=new Size(size.Width+12,size.Height+10);};Install(root);
  }
  private void Install(Control control) {
   if(SystemInformation.HighContrast){control.BackColor=SystemColors.Window;control.ForeColor=SystemColors.WindowText;}
   string description=control.AccessibleDescription;
   if(string.IsNullOrEmpty(description)) {
    foreach(string key in new[]{control.Name,control.AccessibleName,control.Text.Replace("&","")})
     if(key!=null && help.TryGetValue(key,out description))break;
   }
   if(!string.IsNullOrEmpty(description)) {
    control.AccessibleDescription=description;tips.SetToolTip(control,description);
    control.HelpRequested+=(s,e)=>{tips.Show(control.AccessibleDescription,control,Math.Min(20,control.Width),control.Height,15000);e.Handled=true;};
   }
   if(control is Button) {var button=(Button)control;button.Padding=new Padding(10,3,10,3);button.MinimumSize=new Size(80,32);button.UseVisualStyleBackColor=true;}
   foreach(Control child in control.Controls)Install(child);
  }
  public void Dispose(){tips.Dispose();}
 }
 internal static class InfoPage {
  public static TableLayoutPanel Layout(Control parent) {
   parent.BackColor=SystemColors.Window;parent.Padding=new Padding(18);var scrolling=parent as ScrollableControl;if(scrolling!=null)scrolling.AutoScroll=true;
   var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));parent.Controls.Add(table);return table;
  }
  public static void Add(TableLayoutPanel table,Control control) {int row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));control.Margin=new Padding(0,0,0,12);table.Controls.Add(control,0,row);}
  public static void Heading(TableLayoutPanel table,string text) {Add(table,new Label{AutoSize=true,UseMnemonic=false,Text=text,Font=new Font("Segoe UI",11,FontStyle.Bold),Margin=new Padding(0,8,0,10)});}
  public static void Paragraph(TableLayoutPanel table,string text) {var label=new Label{AutoSize=true,UseMnemonic=false,Text=text,Dock=DockStyle.Top};table.SizeChanged+=(s,e)=>label.MaximumSize=new Size(Math.Max(100,table.ClientSize.Width-8),0);Add(table,label);}
 }
 internal sealed class DeviceSetupControl:UserControl {
  private readonly Label state=new Label{AutoSize=true};
  public event Action RetryRequested;
  public DeviceSetupControl() {
   var table=InfoPage.Layout(this);InfoPage.Heading(table,"Z-X6");
   InfoPage.Paragraph(table,"GPU support bracket with CPU and GPU temperature readouts.");InfoPage.Add(table,state);
   InfoPage.Paragraph(table,"This beta supports one Z-X6 at a time. If multiple matching displays are detected, updates stop until only one remains.");
   var retry=new Button{Name="Reconnect",Text="Reconnect",AutoSize=true};retry.Click+=(s,e)=>{if(RetryRequested!=null)RetryRequested();};InfoPage.Add(table,retry);
   InfoPage.Heading(table,"Connect AIDA64");
   InfoPage.Paragraph(table,"1. In AIDA64 Preferences → Hardware Monitoring → External Applications, enable shared memory and select the sensors to export.");
   InfoPage.Paragraph(table,"2. Disable AIDA64's Turing LCD output and close the original GPU LCD utility. The holder's USB port must be available.");
   InfoPage.Paragraph(table,"3. Choose a temperature for each side. For usage-based animation, also export CPU Utilization and GPU Utilization.");
   InfoPage.Paragraph(table,"AIDA64 Extreme supplies the readings. SensorPanel can remain enabled. This app does not change AIDA64 preferences.");
   InfoPage.Heading(table,"Display controls");
   InfoPage.Paragraph(table,"Two temperature numbers (0–99 °C) and two independent bars with eight built-in levels. The app controls their sequence, speed, sensor response and display power.");
   InfoPage.Paragraph(table,"The app has no controls for brightness, RGB lighting, custom images or firmware updates.");
  }
  public void UpdateState(SessionState value) {state.Text="USB port: "+(value.PortName??"not connected")+" · Matching devices: "+value.Devices.Count;}
 }
}
