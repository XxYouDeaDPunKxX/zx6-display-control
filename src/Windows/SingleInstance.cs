using System;
using System.Threading;
using System.Security.Principal;
namespace ZX6DisplayControl {
 public sealed class SingleInstance:IDisposable {
  private readonly string name;private Mutex mutex;private EventWaitHandle open;private bool owner;
  public SingleInstance(string scope="ZX6DisplayControl") {using(var identity=WindowsIdentity.GetCurrent())name=@"Local\"+scope+"-"+identity.User.Value;}
  public bool TryAcquire() {if(mutex!=null)return owner;mutex=new Mutex(true,name,out owner);open=new EventWaitHandle(false,EventResetMode.AutoReset,name+"-open");return owner;}
  public void SignalExistingWindow() {if(open!=null)open.Set();}
  public bool ConsumeOpenRequest() {return owner && open!=null && open.WaitOne(0);}
  public void Dispose() {if(open!=null){open.Dispose();open=null;}if(mutex!=null){if(owner)mutex.ReleaseMutex();mutex.Dispose();mutex=null;owner=false;}}
 }
}
