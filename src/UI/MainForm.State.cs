using System;
using System.Linq;
using System.Windows.Forms;

namespace ZX6DisplayControl {
 public sealed partial class MainForm {
  public void RefreshFromState() {
   if(editing==null)return;UpdateDisplayCheck();
   diagnostics.UpdateLog(log,closing?shutdownStatus:operationName);
   if(closing){notice.Text=shutdownStatus;notice.Visible=true;apply.Enabled=cancel.Enabled=power.Enabled=profileChoice.Enabled=tabs.Enabled=trayPower.Enabled=false;return;}
   bool open=HasOpenDropDown(this);
   if(!open)RefreshUpdateNotice();
   if(!open && DateTime.UtcNow>=nextThemeCheck) {
    nextThemeCheck=DateTime.UtcNow.AddSeconds(3);bool dark=UiTheme.SystemIsDark(),contrast=SystemInformation.HighContrast;
    if(lastHighContrast!=contrast || (themeChoice.SelectedIndex==0 && lastSystemDark!=dark)){lastSystemDark=dark;lastHighContrast=contrast;ApplyTheme();}
   }
   bool pending=ProfilePending,changed=ContentChanged;
   string validation=string.Join(" · ",cpu.ValidationErrors().Select(e=>"CPU: "+e).Concat(gpu.ValidationErrors().Select(e=>"GPU: "+e)));
   string active="Active profile: “"+saved.ActiveProfileName+"”";
   profileStatus.Text=busy?operationName+"…":validation.Length>0?validation:lastSaveFeedback??(changed?(editing.IsBuiltIn?"Built-in profile · changes will be saved as a personal copy.  ":"Unsaved profile changes.  ")+active:pending?"Previewing “"+editing.Name+"”.  "+active:active);
   profileStatus.ForeColor=validation.Length>0?UiTheme.Error:UiTheme.Muted;
   apply.Text=editing.IsBuiltIn && changed?"Save as new profile":changed?"Save changes":"Use profile";
   apply.Enabled=!busy && pending && CanApply;cancel.Enabled=!busy && pending;
   savePreferences.Enabled=!busy;revertPreferences.Enabled=!busy && PreferencesPending;
   Text="Z-X6 Display Control"+(dirty?" · unsaved changes":"");
   var state=controller.State;bool failed=!string.IsNullOrEmpty(controller.FailureMessage);
   if(state==null) {
    if(!open){notice.Text=failed?"The controller stopped. Exit and reopen the app to reconnect.":"Starting controller…";notice.Visible=true;}
    aidaStatus.Text=failed?"AIDA64 · readings stopped":"AIDA64 · waiting";holderStatus.Text=failed?"Display · controller stopped":"Display · connecting…";
    power.Enabled=trayPower.Enabled=!busy && !failed;diagnostics.SetActionsEnabled(!busy,!failed);deviceSetup.UpdateUnavailable(failed,!busy);
    trayStatus.Text=failed?"Controller stopped":"Starting controller…";
    displayPreview.ShowOutput(failed?"Controller stopped":"Waiting for the controller",null,null,null,null,false);return;
   }
   bool limited=state.AidaStatus=="TemperatureLimited";
   bool available=!failed && (state.AidaStatus=="Ready" || limited || state.AidaStatus=="TemperatureInvalid");
   aidaStatus.Text=failed?"AIDA64 · readings stopped":limited?"AIDA64 · above 99 °C":state.AidaStatus=="Ready"?"AIDA64 · sensors ready":state.AidaStatus=="TemperatureInvalid"?"AIDA64 · check sensors":"AIDA64 · unavailable";
   aidaStatus.ForeColor=!failed && state.AidaStatus=="Ready"?UiTheme.Success:UiTheme.Warning;
   holderStatus.Text=failed?"Display · controller stopped":"Display · "+(state.Connected && state.DisplayOff?"off":DeviceText(state.DeviceStatus));holderStatus.ForeColor=state.Connected && !failed?UiTheme.Success:UiTheme.Warning;
   power.Text=saved.DisplayEnabled?"Turn display off":"Turn display on";power.Enabled=trayPower.Enabled=!busy && !failed;
   if(!open){notice.Text=string.Join(" ",new[]{failed?"The controller stopped. Exit and reopen the app to reconnect.":state.Error,log.LastError==null?null:"Log file unavailable: "+log.LastError}.Where(x=>!string.IsNullOrEmpty(x)));notice.Visible=!string.IsNullOrWhiteSpace(notice.Text);}
   cpu.SetCatalog(state.Snapshot,open);gpu.SetCatalog(state.Snapshot,open);cpu.SetAvailable(available);gpu.SetAvailable(available);
   deviceSetup.UpdateState(state,failed,!busy);deviceSetup.UpdateCatalog(state.Snapshot,available,open);diagnostics.SetActionsEnabled(!busy,!failed);diagnostics.UpdateState(state,available);
   double now=previewClock.Elapsed.TotalSeconds,elapsed=now-previousPreview;previousPreview=now;profiles.UpdatePreview(state.Snapshot,available,elapsed);
   bool canShow=!failed && saved.DisplayEnabled && state.Connected && state.DisplayPowerKnown && !state.DisplayOff && available;
   AnimationOutput c=null,g=null;int? ct=null,gt=null;
   string caption=failed?"Controller stopped":!saved.DisplayEnabled || state.DisplayOff?"Display off":!state.Connected?"Display disconnected":!available?"Waiting for sensor readings":"Waiting for display output";
   if(canShow) {
    if(pending && CanApply) {
     var draft=ReadDraft();c=cpuPreview.Advance(draft.Cpu.Animation,SensorNumber(state.Snapshot,draft.Cpu.Animation.SensorId),elapsed,draft.Cpu.Paused);
     g=gpuPreview.Advance(draft.Gpu.Animation,SensorNumber(state.Snapshot,draft.Gpu.Animation.SensorId),elapsed,draft.Gpu.Paused);
     ct=Temperature(state.Snapshot,draft.Cpu.TemperatureId);gt=Temperature(state.Snapshot,draft.Gpu.TemperatureId);caption="Preview · not applied";
    }else if(!pending) {c=state.Cpu;g=state.Gpu;ct=state.CpuNumber;gt=state.GpuNumber;caption=limited?"Output sent · limited to 99 °C":state.AidaStatus=="Ready"?"Output sent to display":"Last valid output · check sensors";}
    else caption="Preview unavailable · check settings";
   }
   displayPreview.ShowOutput(caption,ct,gt,c==null?(int?)null:c.Frame,g==null?(int?)null:g.Frame,pending);
   // Updating response labels inside the editor can dismiss a native popup.
   // Keep the drawn preview and operational status live; defer only these labels.
   if(!open){cpu.SetPreview(c,pending);gpu.SetPreview(g,pending);}
   tray.Text="Z-X6 Display Control · "+DeviceText(state.DeviceStatus);trayPower.Text=power.Text;trayStatus.Text=failed?"Controller stopped":state.Connected?(state.DisplayOff?"Display off · ":"Display enabled · ")+(available?"AIDA64 ready":"AIDA64 unavailable"):"Display "+DeviceText(state.DeviceStatus);
   if(BringToFrontRequested!=null)BringToFrontRequested();
  }
  private static bool HasOpenDropDown(Control root) {var box=root as ComboBox;if(box!=null && box.IsHandleCreated && box.DroppedDown)return true;foreach(Control child in root.Controls)if(HasOpenDropDown(child))return true;return false;}
  private static double? SensorNumber(SensorSnapshot snapshot,string id) {SensorValue row;return snapshot!=null && id!=null && snapshot.Values.TryGetValue(id,out row)?row.Number:null;}
  private static int? Temperature(SensorSnapshot snapshot,string id) {SensorValue row;if(snapshot==null || id==null || !snapshot.Values.TryGetValue(id,out row) || row.Kind!="temp")return null;double? value=row.Number;return value.HasValue && AnimationSettings.Finite(value.Value) && value.Value>=0?(int?)Math.Floor(Math.Min(99,value.Value)):null;}
  private static string DeviceText(string state) {switch(state){case "Connected":return "connected";case "Initializing":return "connecting";case "PortBusy":return "port in use";case "Ambiguous":return "disconnect extra holders";case "Suspended":return "suspended";case "Stopped":return "stopped";case "ConnectionError":return "connection error";default:return "disconnected";}}
 }
}
