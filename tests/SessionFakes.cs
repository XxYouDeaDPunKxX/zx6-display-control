using System;
using System.Collections.Generic;
using System.Linq;
namespace ZX6DisplayControl.Tests {
 public sealed class FakeClock:IClock {public long Now;public long ElapsedMilliseconds {get{return Now;}}}
 public sealed class FakeReader:IAidaReader {
  public SensorReadResult Result=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><label>GPU</label><value>33</value></temp><sys><id>SCPUUTI</id><label>Load</label><value>100</value></sys>");
  public int Reads;
  public SensorReadResult Read(long now) {Reads++;return Result;}
 }
 public sealed class FakeDiscovery:IDeviceDiscovery {
  public List<HolderDevice> Devices=new List<HolderDevice>{new HolderDevice(DeviceDiscovery.ExpectedInstance,"COM5","USB35INCHIPSV2")};
  public IReadOnlyList<HolderDevice> Find() {return Devices.AsReadOnly();}
 }
 public sealed class FakeSerial:ISerialTransport {
  public bool IsOpen {get;private set;} public bool Busy,FailWrites;public Action OnOpen;
  public List<string> Opened=new List<string>();public List<byte[]> Packets=new List<byte[]>();public int OpenAttempts,Closed;
  public void Open(string name) {OpenAttempts++;if(Busy) throw new UnauthorizedAccessException();if(IsOpen) throw new InvalidOperationException("double open");Opened.Add(name);IsOpen=true;if(OnOpen!=null)OnOpen();}
  public void Write(byte[] packet) {if(!IsOpen || FailWrites) throw new System.IO.IOException("removed");Packets.Add((byte[])packet.Clone());}
  public void Close() {if(IsOpen) Closed++;IsOpen=false;}
  public void Dispose() {Close();}
  public int DataCount {get{return Packets.Count(p=>p[5]==169);}}
 }
 public sealed class SessionRig:IDisposable {
  public FakeClock Clock=new FakeClock();public FakeReader Reader=new FakeReader();public FakeDiscovery Discovery=new FakeDiscovery();public FakeSerial Serial=new FakeSerial();public HolderSession Session;
  public SessionRig() {Session=new HolderSession(Reader,Discovery,Serial,Clock,s=>{});}
  public void At(long now) {Clock.Now=now;Session.Tick();}
  public void Dispose() {Session.Stop();}
 }
}
