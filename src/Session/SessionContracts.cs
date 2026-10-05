using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace ZX6DisplayControl {
 public interface IClock {long ElapsedMilliseconds {get;}}
 public interface IDeviceDiscovery {IReadOnlyList<HolderDevice> Find();}
 public interface ISerialTransport:IDisposable {bool IsOpen {get;} void Open(string portName);void Write(byte[] packet);void Close();}
 public sealed class HolderDevice {
  public string InstanceId {get;private set;} public string PortName {get;private set;} public string Serial {get;private set;}
  public HolderDevice(string instanceId,string portName,string serial) {InstanceId=instanceId;PortName=portName;Serial=serial;}
  public override string ToString() {return PortName+" · "+Serial;}
 }
 [DataContract] public sealed class ChannelSettings {
  [DataMember] public string TemperatureId {get;set;}
  [DataMember] public AnimationSettings Animation {get;set;}
  [DataMember] public bool Paused {get;set;}
  public ChannelSettings() {Animation=new AnimationSettings();}
  public ChannelSettings Copy() {return new ChannelSettings{TemperatureId=TemperatureId,Animation=Animation==null?null:Animation.Copy(),Paused=Paused};}
 }
 public sealed class SessionConfiguration {
  public ChannelSettings Cpu {get;set;} public ChannelSettings Gpu {get;set;}
  public bool DisplayEnabled {get;set;}
  public SessionConfiguration() {Cpu=new ChannelSettings{TemperatureId="TCPU"};Gpu=new ChannelSettings{TemperatureId="TGPU1"};DisplayEnabled=true;}
  public SessionConfiguration Copy() {return new SessionConfiguration{Cpu=Cpu.Copy(),Gpu=Gpu.Copy(),DisplayEnabled=DisplayEnabled};}
 }
 public sealed class SessionState {
  public string AidaStatus {get;internal set;} public string DeviceStatus {get;internal set;} public string Error {get;internal set;}
  public string CleanupError {get;internal set;} public bool DisplayPowerKnown {get;internal set;}
  public SensorSnapshot Snapshot {get;internal set;} public AnimationOutput Cpu {get;internal set;} public AnimationOutput Gpu {get;internal set;}
  public string PortName {get;internal set;} public bool RequestedDisplayOn {get;internal set;} public bool DisplayOff {get;internal set;} public bool Connected {get;internal set;}
  public int? CpuNumber {get;internal set;} public int? GpuNumber {get;internal set;}
  public IReadOnlyList<HolderDevice> Devices {get;internal set;}
  public long ValidReads {get;internal set;} public long ReadErrors {get;internal set;} public long Writes {get;internal set;}
 }
}
