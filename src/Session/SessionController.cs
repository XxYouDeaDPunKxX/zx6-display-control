using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
namespace ZX6DisplayControl {
 public interface ISessionController {
  SessionState State {get;} string FailureMessage {get;}
  Task Completion {get;}
  void Start();void Apply(SessionConfiguration configuration);void Retry();void Suspend();void Resume();void Stop();
 }
 public sealed class SessionController:ISessionController,IDisposable {
  private readonly ConcurrentQueue<Action<HolderSession>> queue=new ConcurrentQueue<Action<HolderSession>>();
  private readonly AutoResetEvent wake=new AutoResetEvent(false);private readonly Thread worker;private readonly object lifecycle=new object();
  private readonly TaskCompletionSource<bool> completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  private volatile SessionState state;private volatile string failure;private volatile bool stopping;private bool started,disposed;
  private readonly Action<string,Exception> reportFailure;
  public SessionController(Func<HolderSession> factory,SessionConfiguration initial,Action<string,Exception> reportFailure=null) {
   if(factory==null || initial==null)throw new ArgumentNullException();var configuration=initial.Copy();
   this.reportFailure=reportFailure;
   worker=new Thread(()=>Run(factory,configuration)){IsBackground=true,Name="Z-X6 serial worker"};
  }
  public Task Completion {get{return completion.Task;}}
  public void Start() {lock(lifecycle){if(started || stopping)return;started=true;try{worker.Start();}catch(Exception e){RecordFailure("Display controller could not start",e);stopping=true;DisposeWake();completion.TrySetResult(true);}}}
  public SessionState State {get{return state;}} public string FailureMessage {get{return failure;}}
  private void Run(Func<HolderSession> factory,SessionConfiguration initial) {
   HolderSession session=null;
   try {session=factory();session.Apply(initial);state=session.State;
    while(!stopping) {Action<HolderSession> command;while(!stopping && queue.TryDequeue(out command))command(session);if(stopping)break;session.Tick();state=session.State;wake.WaitOne(25);}
   }catch(Exception e){RecordFailure("Display controller stopped",e);}
   finally {
    if(session!=null){try{session.Stop();state=session.State;}catch(Exception e){RecordFailure("Display shutdown incomplete",e);}}
    lock(lifecycle){stopping=true;DisposeWake();}completion.TrySetResult(true);
   }
  }
  private void RecordFailure(string message,Exception error) {
   string detail=message+": "+AppLanguage.ErrorMessage(error);failure=string.IsNullOrEmpty(failure)?detail:failure+" | "+detail;
   // Diagnostics must never prevent port cleanup or completion notification.
   if(reportFailure!=null)try{reportFailure(message,error);}catch(Exception){}
  }
  private void Enqueue(Action<HolderSession> command) {lock(lifecycle){if(stopping)return;queue.Enqueue(command);wake.Set();}}
  public void Apply(SessionConfiguration value) {if(value==null)throw new ArgumentNullException("value");var copy=value.Copy();Enqueue(s=>s.Apply(copy));}
  public void Retry() {Enqueue(s=>s.Retry());}
  public void Suspend() {Enqueue(s=>s.Suspend());}
  public void Resume() {Enqueue(s=>s.Resume());}
  public void Stop() {lock(lifecycle){if(stopping)return;stopping=true;if(!started){DisposeWake();completion.TrySetResult(true);}else wake.Set();}}
  private void DisposeWake() {if(!disposed){wake.Dispose();disposed=true;}}
  public void Dispose() {Stop();}
 }
}
