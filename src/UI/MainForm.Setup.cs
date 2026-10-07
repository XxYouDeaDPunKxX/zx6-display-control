using System;
using System.Diagnostics;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 public sealed partial class MainForm {
  private readonly Stopwatch displayCheckClock=new Stopwatch();
  private bool displayCheckSecond;
  public void OpenSetup() {
   tabs.SelectedIndex=2;deviceSetup.OpenSetup(ReadDraft(),controller.State==null?null:controller.State.Snapshot);
  }
  public void StageSetup(string cpuTemperature,string gpuTemperature,string cpuSource,string gpuSource) {
   var state=controller.State;var snapshot=state==null?null:state.Snapshot;
   var draft=ReadDraft();ConfigureSetupChannel(draft.Cpu,cpuTemperature,cpuSource,snapshot);ConfigureSetupChannel(draft.Gpu,gpuTemperature,gpuSource,snapshot);
   cpu.Load(draft.Cpu,snapshot);gpu.Load(draft.Gpu,snapshot);tabs.SelectedIndex=0;lastSaveFeedback="Sensor choices ready for review.";RefreshFromState();
  }
  private static void ConfigureSetupChannel(ChannelSettings channel,string temperature,string source,SensorSnapshot snapshot) {
   SensorValue sensor;if(snapshot==null || temperature==null || !snapshot.Values.TryGetValue(temperature,out sensor) || sensor.Kind!="temp" || !sensor.Number.HasValue || sensor.Number<0)throw new InvalidOperationException("Choose an available temperature for both CPU and GPU.");
   channel.TemperatureId=temperature;
   if(string.IsNullOrEmpty(source)) {if(channel.Animation.NeedsSource)throw new InvalidOperationException("This profile needs a control sensor for each bar. Choose one above.");return;}
   if(!snapshot.Values.TryGetValue(source,out sensor) || !sensor.Number.HasValue)throw new InvalidOperationException("Choose an available bar sensor, or use the Display tab to keep the current selection.");
   if(sensor.Unit!="%" && sensor.Unit!="°C")throw new InvalidOperationException("Quick setup accepts usage or temperature for bars. Use Advanced settings on the Display tab for other units.");
   string oldUnit=null;SensorValue previous;if(channel.Animation.SensorId!=null && snapshot.Values.TryGetValue(channel.Animation.SensorId,out previous))oldUnit=previous.Unit;
   channel.Animation.SensorId=source;if(oldUnit!=sensor.Unit){channel.Animation.InputMin=sensor.Unit=="%"?0:30;channel.Animation.InputMax=sensor.Unit=="%"?100:80;}
  }
  public void StartDisplayCheck() {
   if(busy || closing)throw new InvalidOperationException("Wait for the current operation to finish.");
   var state=controller.State;
   if(!saved.DisplayEnabled || state==null || !state.Connected || !string.IsNullOrEmpty(controller.FailureMessage) || (state.AidaStatus!="Ready" && state.AidaStatus!="TemperatureLimited"))throw new InvalidOperationException("Turn the display on and activate a profile with two available temperatures before testing the bars.");
   tabs.SelectedIndex=2;displayCheckSecond=false;displayCheckClock.Restart();SendDisplayCheck(false);
   deviceSetup.SetCheckStatus("CPU empty · GPU full. In five seconds the levels swap.",true);
  }
  private void SendDisplayCheck(bool second) {
   var value=Configuration(saved);value.Cpu.Paused=value.Gpu.Paused=false;
   value.Cpu.Animation=new AnimationSettings{Mode=AnimationMode.Fixed,FixedFrame=second?7:0};value.Gpu.Animation=new AnimationSettings{Mode=AnimationMode.Fixed,FixedFrame=second?0:7};controller.Apply(value);
  }
  public void StopDisplayCheck() {
   if(!displayCheckClock.IsRunning)return;displayCheckClock.Stop();controller.Apply(Configuration(saved));deviceSetup.SetCheckStatus("Display check finished. The active profile has been restored.",false);
  }
  public void SuspendDisplayCheck() {if(IsDisposed || Disposing)return;if(InvokeRequired){BeginInvoke(new Action(StopDisplayCheck));return;}StopDisplayCheck();}
  private void UpdateDisplayCheck() {
   if(!displayCheckClock.IsRunning)return;var state=controller.State;
   if(closing || state==null || !state.Connected || !string.IsNullOrEmpty(controller.FailureMessage) || (state.AidaStatus!="Ready" && state.AidaStatus!="TemperatureLimited") || displayCheckClock.Elapsed.TotalSeconds>=10){StopDisplayCheck();return;}
   if(!displayCheckSecond && displayCheckClock.Elapsed.TotalSeconds>=5){displayCheckSecond=true;SendDisplayCheck(true);deviceSetup.SetCheckStatus("CPU full · GPU empty. The active profile resumes in five seconds.",true);}
  }
 }
}
