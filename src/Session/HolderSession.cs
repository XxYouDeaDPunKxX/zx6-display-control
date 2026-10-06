using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
namespace ZX6DisplayControl {
 public sealed class HolderSession {
  private readonly IAidaReader reader;private readonly IDeviceDiscovery discovery;private readonly ISerialTransport port;private readonly IClock clock;private readonly Action<string> log;
  private readonly AnimationEngine cpuEngine=new AnimationEngine(new Random());
  private readonly AnimationEngine gpuEngine=new AnimationEngine(new Random(Guid.NewGuid().GetHashCode()));
  private SessionConfiguration config=new SessionConfiguration();
  private SensorSnapshot snapshot,currentSnapshot;
  private IReadOnlyList<HolderDevice> devices=new List<HolderDevice>().AsReadOnly();
  private AnimationOutput cpu=new AnimationOutput(),gpu=new AnimationOutput();
  private AnimationOutput sentCpu,sentGpu;
  private int sentCpuNumber,sentGpuNumber;
  private long nextRead,nextWrite,nextConnect,initAt,nextPresence,nextReport=60000;
  private long? lastGood,lastAdvance;
  private double cpuTemperature,gpuTemperature;
  private bool initialized,suspended,stopped;
  private bool? displayOff;
  private string aidaStatus="Waiting",deviceStatus="Disconnected",aidaMessage="Waiting for AIDA64.",deviceMessage="",portName,connectedId,lastTransition,cleanupError;
  private long validReads,readErrors,writes;

  public HolderSession(IAidaReader reader,IDeviceDiscovery discovery,ISerialTransport port,IClock clock,Action<string> log) {
   this.reader=reader;this.discovery=discovery;this.port=port;this.clock=clock;this.log=log??(s=>{});Publish();
  }
  public SessionState State {get;private set;}
  public void Apply(SessionConfiguration value) {
   if(value==null || !ValidChannel(value.Cpu) || !ValidChannel(value.Gpu)) throw new ArgumentException("Invalid configuration.");
   bool changedTemperature=value.Cpu.TemperatureId!=config.Cpu.TemperatureId || value.Gpu.TemperatureId!=config.Gpu.TemperatureId;
   config=value.Copy();
   if(changedTemperature) {lastGood=null;nextRead=0;}
   Publish();
  }
  public void Tick() {
   if(stopped || suspended) return;
   long now=clock.ElapsedMilliseconds;
   if(now>=nextRead) {ReadSensors(now);nextRead=now+500;}
   double elapsed=lastAdvance.HasValue?Math.Max(0,(now-lastAdvance.Value)/1000.0):0;lastAdvance=now;
   cpu=cpuEngine.Advance(config.Cpu.Animation,AnimationValue(config.Cpu),elapsed,config.Cpu.Paused);
   gpu=gpuEngine.Advance(config.Gpu.Animation,AnimationValue(config.Gpu),elapsed,config.Gpu.Paused);
   try {
    if(!port.IsOpen && now>=nextConnect) Connect(now);
    if(port.IsOpen && now>=nextPresence) {
     devices=FindDevices();nextPresence=now+3000;
     if(devices.Count>1) {
      CloseConnection(true);deviceStatus="Ambiguous";deviceMessage="Multiple matching displays found. Connect one holder at a time.";nextConnect=now+3000;
     } else if(!devices.Any(d=>string.Equals(d.InstanceId,connectedId,StringComparison.OrdinalIgnoreCase) && d.PortName==portName)) {
      CloseConnection(false);deviceStatus="Disconnected";deviceMessage="Display disconnected. Reconnecting automatically.";nextConnect=now+3000;
     }
    }
    if(port.IsOpen && !initialized && now>=initAt) {Send(PacketCodec.Control(255));initialized=true;deviceStatus="Connected";nextWrite=now;}
    if(initialized && port.IsOpen) {
     bool shouldOff=!config.DisplayEnabled || !lastGood.HasValue || now-lastGood.Value>5000;
     if(shouldOff) {if(displayOff!=true){Send(PacketCodec.Control(108));displayOff=true;sentCpu=sentGpu=null;}}
     else {
      if(displayOff!=false) {Send(PacketCodec.Control(109));displayOff=false;}
      if(now>=nextWrite) {
       Send(PacketCodec.Encode(cpuTemperature,gpuTemperature,cpu.Frame,gpu.Frame));
       // Publish only a completed write, not the engines' next unsent frame.
       sentCpu=cpu;sentGpu=gpu;sentCpuNumber=(int)Math.Floor(cpuTemperature);sentGpuNumber=(int)Math.Floor(gpuTemperature);nextWrite=now+125;
      }
     }
    }
   } catch(UnauthorizedAccessException) {ConnectionFailed(now,"PortBusy","USB port busy. Close GPULCD and disable the Turing LCD module in AIDA64.");}
     catch(Exception e) {ConnectionFailed(now,"ConnectionError","Display connection failed: "+e.Message);}
   Publish();
  }
  private void ReadSensors(long now) {
   SensorReadResult result;
   try {result=reader.Read(now);} catch(Exception e) {result=SensorReadResult.Failure("ReadError","AIDA64 read failed: "+e.Message);}
   currentSnapshot=result.Snapshot;
   if(currentSnapshot==null) {aidaStatus=result.ErrorCode;aidaMessage=result.Message;readErrors++;return;}
   snapshot=currentSnapshot;validReads++;
   double c,g;
   if(!Temperature(config.Cpu.TemperatureId,out c) || !Temperature(config.Gpu.TemperatureId,out g)) {
    aidaStatus="TemperatureInvalid";aidaMessage="Display temperature missing, non-numeric or below 0 °C. Check "+config.Cpu.TemperatureId+" and "+config.Gpu.TemperatureId+".";return;
   }
   cpuTemperature=Math.Min(c,99);gpuTemperature=Math.Min(g,99);lastGood=now;
   bool limited=c>99 || g>99;aidaStatus=limited?"TemperatureLimited":"Ready";
   aidaMessage=limited?"Display limit: "+string.Join("; ",new[]{TemperatureLimit("CPU",c),TemperatureLimit("GPU",g)}.Where(s=>s!=null))+".":"";
  }
  private static string TemperatureLimit(string side,double value) {return value>99?side+" "+value.ToString("0.##",CultureInfo.InvariantCulture)+" °C (shown as 99 °C)":null;}
  private bool Temperature(string id,out double value) {
   value=0;SensorValue row;
   if(currentSnapshot==null || !currentSnapshot.Values.TryGetValue(id,out row) || row.Kind!="temp" || !row.Number.HasValue) return false;
   value=row.Number.Value;return AnimationSettings.Finite(value) && value>=0;
  }
  private double? AnimationValue(ChannelSettings channel) {SensorValue value;return currentSnapshot!=null && !string.IsNullOrEmpty(channel.Animation.SensorId) && currentSnapshot.Values.TryGetValue(channel.Animation.SensorId,out value)?value.Number:null;}
  private IReadOnlyList<HolderDevice> FindDevices() {return discovery.Find().Where(d=>string.Equals(d.InstanceId,DeviceDiscovery.ExpectedInstance,StringComparison.OrdinalIgnoreCase) && string.Equals(d.Serial,"USB35INCHIPSV2",StringComparison.OrdinalIgnoreCase) && System.Text.RegularExpressions.Regex.IsMatch(d.PortName??"",@"^COM[1-9][0-9]*$")).ToList().AsReadOnly();}
  private void Connect(long now) {
   nextConnect=now+3000;devices=FindDevices();
   if(devices.Count!=1) {deviceStatus=devices.Count>1?"Ambiguous":"Disconnected";deviceMessage=devices.Count>1?"Multiple matching displays found. Connect one holder at a time.":"Compatible USB display not found.";return;}
   port.Open(devices[0].PortName);portName=devices[0].PortName;connectedId=devices[0].InstanceId;
   long openedAt=clock.ElapsedMilliseconds;
   initAt=openedAt+100;nextPresence=openedAt+3000;initialized=false;displayOff=null;sentCpu=sentGpu=null;deviceStatus="Initializing";deviceMessage="";cleanupError=null;
  }
  private void Send(byte[] packet) {port.Write(packet);writes++;}
  private void ConnectionFailed(long now,string code,string message) {CloseConnection(false);deviceStatus=code;deviceMessage=message;nextConnect=now+3000;}
  private void CloseConnection(bool powerOff) {
   try {if(powerOff && port.IsOpen){Send(PacketCodec.Control(108));displayOff=true;}}
   catch(Exception e) {displayOff=null;RecordCleanupError("Display power-off failed",e);}
   finally {try {port.Close();} catch(Exception e) {RecordCleanupError("USB port close failed",e);}initialized=false;sentCpu=sentGpu=null;portName=null;connectedId=null;}
  }
  private void RecordCleanupError(string action,Exception error) {string message=action+": "+error.Message;cleanupError=string.IsNullOrEmpty(cleanupError)?message:cleanupError+" | "+message;log("cleanup | "+message);}
  private void Publish() {
   string animationError=(cpu.SourceMissing?"CPU animation sensor unavailable: "+config.Cpu.Animation.SensorId+". ":"")+(gpu.SourceMissing?"GPU animation sensor unavailable: "+config.Gpu.Animation.SensorId+".":"");
   bool outputKnown=initialized && port.IsOpen && displayOff==false && sentCpu!=null;
   State=new SessionState{AidaStatus=aidaStatus,DeviceStatus=deviceStatus,Error=string.Join(" ",new[]{aidaMessage,deviceMessage,animationError,cleanupError}.Where(s=>!string.IsNullOrWhiteSpace(s))),CleanupError=cleanupError,DisplayPowerKnown=displayOff.HasValue,Snapshot=snapshot,Cpu=outputKnown?sentCpu:null,Gpu=outputKnown?sentGpu:null,PortName=portName,RequestedDisplayOn=config.DisplayEnabled,DisplayOff=displayOff==true,Connected=initialized && port.IsOpen,Devices=devices,ValidReads=validReads,ReadErrors=readErrors,Writes=writes,CpuNumber=outputKnown?(int?)sentCpuNumber:null,GpuNumber=outputKnown?(int?)sentGpuNumber:null};
   string transition=aidaStatus+" | "+deviceStatus+" | "+State.Error;
   if(transition!=lastTransition) {lastTransition=transition;log(transition);}
   if(clock.ElapsedMilliseconds>=nextReport){ReportCounters();nextReport=clock.ElapsedMilliseconds+60000;}
  }
  private void ReportCounters() {log("stats | reads="+validReads+" errors="+readErrors+" writes="+writes+" cpu="+State.CpuNumber+" gpu="+State.GpuNumber+" frames="+cpu.Frame+","+gpu.Frame+" state="+aidaStatus+"/"+deviceStatus);}
  private static bool ValidChannel(ChannelSettings c) {return c!=null && !string.IsNullOrWhiteSpace(c.TemperatureId) && c.Animation!=null && c.Animation.Validate().Count==0;}
  public void Suspend() {if(stopped)return;suspended=true;CloseConnection(true);lastGood=null;deviceStatus="Suspended";deviceMessage="Windows suspended.";Publish();}
  public void Resume() {if(stopped)return;suspended=false;lastAdvance=null;nextRead=0;nextConnect=0;cpuEngine.Reset();gpuEngine.Reset();deviceStatus="Disconnected";deviceMessage="Reconnecting after resume.";Publish();}
  public void Stop() {if(stopped)return;stopped=true;CloseConnection(true);try{port.Dispose();}catch(Exception e){RecordCleanupError("USB port disposal failed",e);}deviceStatus="Stopped";deviceMessage="App stopped.";Publish();ReportCounters();}
  public void Retry() {if(stopped)return;nextConnect=0;nextRead=0;nextPresence=0;}
 }
}
