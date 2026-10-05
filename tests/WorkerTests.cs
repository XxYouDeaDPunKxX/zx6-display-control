using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace ZX6DisplayControl.Tests {
 public static class WorkerTests {
  private sealed class TrackingPort:ISerialTransport {
   public readonly List<int> Threads=new List<int>(); public readonly List<byte[]> Packets=new List<byte[]>();private bool open;
   private void Touch(){Threads.Add(Thread.CurrentThread.ManagedThreadId);}
   public bool IsOpen {get{Touch();return open;}}
   public void Open(string name){Touch();open=true;}public void Write(byte[] packet){Touch();Packets.Add(packet);}
   public void Close(){Touch();open=false;}public void Dispose(){Touch();open=false;}
  }
  private static void Until(Func<bool> predicate) {Assert.True(SpinWait.SpinUntil(predicate,4000),"Worker did not reach expected state");}
  [Test] public static void Worker_SingleOwnerCopiesAndStops() {
   int caller=Thread.CurrentThread.ManagedThreadId;var port=new TrackingPort();var config=new SessionConfiguration();
   using(var controller=new SessionController(()=>new HolderSession(new FakeReader(),new FakeDiscovery(),port,new MonotonicClock(),s=>{}),config)) {
    controller.Start();
    config.Cpu.TemperatureId="MISSING";
    Until(()=>controller.State!=null && controller.State.Connected);Assert.Equal(42,controller.State.CpuNumber.Value);
    controller.Apply(new SessionConfiguration{DisplayEnabled=false});Until(()=>controller.State.DisplayOff && !controller.State.RequestedDisplayOn);
    controller.Suspend();Until(()=>controller.State.DeviceStatus=="Suspended");controller.Resume();Until(()=>controller.State.Connected);Assert.True(controller.State.DisplayOff);
    controller.Stop();Assert.True(controller.Completion.Wait(4000));Assert.Equal("Stopped",controller.State.DeviceStatus);Assert.True(controller.FailureMessage==null);
   }
   Assert.Equal(1,port.Threads.Distinct().Count());Assert.True(port.Threads[0]!=caller);Assert.Equal((byte)108,port.Packets.Last()[5]);
  }
  [Test] public static void Worker_UnexpectedFailureIsVisible() {
   using(var controller=new SessionController(()=>{throw new InvalidOperationException("factory test");},new SessionConfiguration())) {
    controller.Start();
    Until(()=>controller.FailureMessage!=null);Assert.True(controller.FailureMessage.Contains("factory test"));controller.Stop();
   }
  }
  [Test] public static void Worker_SecondInstanceSignalsExistingAndReleases() {
   string name="HolderTest"+Guid.NewGuid().ToString("N");
   using(var first=new SingleInstance(name))using(var second=new SingleInstance(name)) {
    Assert.True(first.TryAcquire());Assert.True(!second.TryAcquire());Assert.True(!first.ConsumeOpenRequest());second.SignalExistingWindow();Assert.True(first.ConsumeOpenRequest());Assert.True(!first.ConsumeOpenRequest());
   }
   using(var next=new SingleInstance(name))Assert.True(next.TryAcquire());
  }
 }
}
