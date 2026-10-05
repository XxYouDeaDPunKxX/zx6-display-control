using System;
using System.Linq;
namespace ZX6DisplayControl.Tests {
 public static class HolderSessionTests {
  [Test] public static void Session_SlowOpenStillWaitsOneHundredMilliseconds() {
   using(var r=new SessionRig()) {
    r.Serial.OnOpen=()=>r.Clock.Now=500;r.At(0);r.At(525);Assert.Equal(0,r.Serial.Packets.Count);
    r.At(599);Assert.Equal(0,r.Serial.Packets.Count);r.At(600);Assert.Equal((byte)255,r.Serial.Packets[0][5]);
   }
  }
  [Test] public static void Session_AmbiguityAfterConnectionStopsData() {
   using(var r=new SessionRig()) {
    r.At(0);r.At(100);int sent=r.Serial.DataCount;
    r.Discovery.Devices.Add(new HolderDevice(DeviceDiscovery.ExpectedInstance,"COM9","USB35INCHIPSV2"));r.At(3000);
    Assert.Equal("Ambiguous",r.Session.State.DeviceStatus);Assert.Equal(sent,r.Serial.DataCount);Assert.True(!r.Serial.IsOpen);
    r.Discovery.Devices.RemoveAt(1);r.At(6000);r.At(6100);Assert.True(r.Session.State.Connected);
   }
  }
  [Test] public static void Session_ReportsBoundedHealthAndFinalCounters() {
   var clock=new FakeClock();var rows=new System.Collections.Generic.List<string>();
   var session=new HolderSession(new FakeReader(),new FakeDiscovery(),new FakeSerial(),clock,rows.Add);
   session.Tick();clock.Now=100;session.Tick();Assert.Equal(0,rows.Count(s=>s.StartsWith("stats |")));
   clock.Now=60000;session.Tick();Assert.Equal(1,rows.Count(s=>s.StartsWith("stats |")));session.Tick();Assert.Equal(1,rows.Count(s=>s.StartsWith("stats |")));
   session.Stop();Assert.Equal(2,rows.Count(s=>s.StartsWith("stats |")));Assert.True(rows.Last().Contains("writes=5"));
  }
  [Test] public static void Session_InitCadenceAndNoBacklog() {
   using(var r=new SessionRig()) {
    r.At(0);Assert.Equal(1,r.Serial.OpenAttempts);Assert.Equal(0,r.Serial.Packets.Count);
    r.At(99);Assert.Equal(0,r.Serial.Packets.Count);r.At(100);Assert.Equal((byte)255,r.Serial.Packets[0][5]);Assert.Equal(1,r.Serial.DataCount);
    r.At(224);Assert.Equal(1,r.Serial.DataCount);r.At(225);Assert.Equal(2,r.Serial.DataCount);
    r.At(499);Assert.Equal(1,r.Reader.Reads);r.At(500);Assert.Equal(2,r.Reader.Reads);
    int before=r.Serial.DataCount;r.At(15000);Assert.Equal(before+1,r.Serial.DataCount);Assert.Equal(3,r.Reader.Reads);
   }
  }
  [Test] public static void Session_MissingDataAndRecovery() {
   using(var r=new SessionRig()) {
    r.At(0);r.At(100);r.Reader.Result=SensorReadResult.Failure("AidaMissing","absent");r.At(500);r.At(5001);
    Assert.Equal((byte)108,r.Serial.Packets.Last()[5]);int count=r.Serial.DataCount;r.At(7000);Assert.Equal(count,r.Serial.DataCount);
    r.Reader.Result=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><value>44</value></temp>");r.At(7500);
    Assert.Equal((byte)109,r.Serial.Packets[r.Serial.Packets.Count-2][5]);Assert.Equal((byte)4,r.Serial.Packets.Last()[9]);Assert.Equal(false,r.Session.State.DisplayOff);
   }
   using(var r=new SessionRig()) {r.Reader.Result=SensorReadResult.Failure("ExportMissing","absent");r.At(0);r.At(100);Assert.Equal(0,r.Serial.DataCount);Assert.Equal((byte)108,r.Serial.Packets.Last()[5]);}
  }
  [Test] public static void Session_MissingAnimationSourceKeepsTemperatures() {
   using(var r=new SessionRig()) {
    var config=new SessionConfiguration();config.Cpu.Animation.RateMode=AnimationRateMode.Sensor;config.Cpu.Animation.SensorId="SCPUUTI";r.Session.Apply(config);
    r.At(0);r.At(100);r.At(350);int frame=r.Session.State.Cpu.Frame;
    r.Reader.Result=AidaParserTests.Parse(AidaParserTests.Cpu.Replace("42.5","48")+"<temp><id>TGPU1</id><value>34</value></temp>");
    r.At(500);r.At(750);Assert.Equal(frame,r.Session.State.Cpu.Frame);Assert.True(r.Session.State.Cpu.SourceMissing);Assert.Equal(48,r.Session.State.CpuNumber.Value);Assert.Equal(false,r.Session.State.DisplayOff);
   }
  }
  [Test] public static void Session_UserOffSurvivesReconnectAndResume() {
   using(var r=new SessionRig()) {
    r.Session.Apply(new SessionConfiguration{DisplayEnabled=false});r.At(0);r.At(100);Assert.Equal(0,r.Serial.DataCount);
    r.Session.Suspend();Assert.Equal(false,r.Serial.IsOpen);r.Session.Resume();r.At(1000);r.At(1100);
    Assert.Equal(0,r.Serial.DataCount);Assert.True(!r.Serial.Packets.Any(p=>p[5]==109));Assert.True(r.Session.State.DisplayOff);
    r.Session.Stop();int count=r.Serial.Packets.Count;r.At(2000);Assert.Equal(count,r.Serial.Packets.Count);
   }
  }
  [Test] public static void Session_PortBusyRetryAndIdentity() {
   using(var r=new SessionRig()) {r.Serial.Busy=true;r.At(0);Assert.Equal("PortBusy",r.Session.State.DeviceStatus);r.At(2999);Assert.Equal(1,r.Serial.OpenAttempts);r.At(3000);Assert.Equal(2,r.Serial.OpenAttempts);}
   using(var r=new SessionRig()) {
    r.At(0);r.At(100);r.Serial.FailWrites=true;r.At(225);Assert.True(!r.Serial.IsOpen);
    r.Serial.FailWrites=false;r.Discovery.Devices.Clear();r.Discovery.Devices.Add(new HolderDevice(@"USB\VID_9999&PID_0000\OTHER","COM5","OTHER"));r.At(3225);Assert.Equal(1,r.Serial.OpenAttempts);
    r.Discovery.Devices.Add(new HolderDevice(DeviceDiscovery.ExpectedInstance,"COM9","USB35INCHIPSV2"));r.At(6225);r.At(6325);Assert.Equal("COM9",r.Serial.Opened.Last());Assert.True(r.Session.State.Connected);
   }
   using(var r=new SessionRig()) {r.Discovery.Devices.Add(new HolderDevice(DeviceDiscovery.ExpectedInstance,"COM9","USB35INCHIPSV2"));r.At(0);Assert.Equal("Ambiguous",r.Session.State.DeviceStatus);Assert.Equal(0,r.Serial.OpenAttempts);}
  }
  [Test] public static void Session_ApplyCopiesAndClearsChangedTemperature() {
   using(var r=new SessionRig()) {var config=new SessionConfiguration();r.Session.Apply(config);config.Cpu.TemperatureId="MISSING";r.At(0);r.At(100);Assert.Equal(42,r.Session.State.CpuNumber.Value);r.Session.Apply(config);r.At(200);Assert.True(r.Session.State.DisplayOff);Assert.True(!r.Session.State.CpuNumber.HasValue);}
  }
  [Test] public static void Session_PnpIdentityNotPortNumber() {
   Assert.Equal("COM17",DeviceDiscovery.FromPnp(DeviceDiscovery.ExpectedInstance,"Dispositivo seriale USB (COM17)",true).PortName);
   Assert.True(DeviceDiscovery.FromPnp(@"USB\VID_9999&PID_0000\OTHER","(COM5)",true)==null);
   Assert.True(DeviceDiscovery.FromPnp(DeviceDiscovery.ExpectedInstance,"(COM5)",false)==null);
   Assert.True(DeviceDiscovery.FromPnp(DeviceDiscovery.ExpectedInstance,"(COM5)garbage",true)==null);
  }
 }
}
