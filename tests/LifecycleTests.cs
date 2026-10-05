using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace ZX6DisplayControl.Tests {
 public static class LifecycleTests {
  [Test] public static void Lifecycle_StopBeforeStartAndRepeatedDisposalAreSafe() {
   bool started=false;var controller=new SessionController(()=>{started=true;return new HolderSession(new FakeReader(),new FakeDiscovery(),new FakeSerial(),new FakeClock(),s=>{});},new SessionConfiguration());
   controller.Retry();controller.Stop();controller.Start();controller.Stop();controller.Apply(new SessionConfiguration());controller.Suspend();controller.Resume();controller.Dispose();controller.Dispose();
   Assert.True(controller.Completion.IsCompleted && !started,"A stopped controller restarted or retained pending work");
  }
  private sealed class HeldReader:IAidaReader {
   public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
   public SensorReadResult Read(long now){Entered.Set();if(!Release.Wait(5000))throw new TimeoutException("fixture timeout");return SensorReadResult.Failure("Unavailable","Fixture");}
  }
  private sealed class FailingClosePort:ISerialTransport {
   public bool IsOpen{get;private set;}public bool FailWrite,FailClose;public int Closes;
   public void Open(string name){IsOpen=true;}public void Write(byte[] data){if(FailWrite)throw new IOException("power-off fixture");}
   public void Close(){Closes++;IsOpen=false;if(FailClose)throw new IOException("close fixture");}public void Dispose(){IsOpen=false;}
  }
  private static void StartIfAvailable(SessionController controller){var method=typeof(SessionController).GetMethod("Start");if(method!=null)method.Invoke(controller,null);}
  private static void AwaitStopped(SessionController controller){var property=typeof(SessionController).GetProperty("Completion");if(property!=null)Assert.True(((Task)property.GetValue(controller,null)).Wait(3000),"Worker did not finish");}
  [Test] public static void Lifecycle_ConstructionDoesNotStartExternalWork() {
   using(var called=new ManualResetEventSlim())using(var controller=new SessionController(()=>{called.Set();return new HolderSession(new FakeReader(),new FakeDiscovery(),new FakeSerial(),new FakeClock(),s=>{});},new SessionConfiguration())) {
    Assert.True(!called.Wait(150),"Construction started the worker before the form was ready");StartIfAvailable(controller);Assert.True(called.Wait(2500),"Explicit start did not run worker");controller.Stop();AwaitStopped(controller);
   }
  }
  [Test] public static void Lifecycle_StopReturnsWhileExternalReadIsPending() {
   var reader=new HeldReader();var controller=new SessionController(()=>new HolderSession(reader,new FakeDiscovery(),new FakeSerial(),new FakeClock(),s=>{}),new SessionConfiguration());Task request=null;
   try {StartIfAvailable(controller);Assert.True(reader.Entered.Wait(2500));request=Task.Run(()=>controller.Stop());Assert.True(request.Wait(250),"Stop blocked its caller on pending external I/O");}
   finally {reader.Release.Set();if(request!=null)request.Wait(3000);controller.Stop();AwaitStopped(controller);controller.Dispose();reader.Entered.Dispose();reader.Release.Dispose();}
  }
  [Test] public static void Lifecycle_ShutdownRetainsPowerAndCloseErrors() {
   var port=new FailingClosePort();var logs=new List<string>();var clock=new FakeClock();var session=new HolderSession(new FakeReader(),new FakeDiscovery(),port,clock,logs.Add);
   session.Tick();clock.Now=100;session.Tick();port.FailWrite=true;port.FailClose=true;session.Stop();
   Assert.True(session.State.Error.Contains("power-off fixture") && session.State.Error.Contains("close fixture"),"Shutdown failures disappeared from state");
   Assert.True(logs.Any(x=>x.Contains("power-off fixture")) && logs.Any(x=>x.Contains("close fixture")),"Shutdown failures disappeared from diagnostics");
   Assert.True(!session.State.DisplayOff,"Failed power-off was reported as successful");Assert.True(!session.State.Connected);Assert.Equal("Stopped",session.State.DeviceStatus);
  }
 }
}
