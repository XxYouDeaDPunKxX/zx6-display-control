using System.Diagnostics;
namespace ZX6DisplayControl {
 public sealed class MonotonicClock:IClock {
  private readonly Stopwatch clock=Stopwatch.StartNew();
  public long ElapsedMilliseconds {get{return clock.ElapsedMilliseconds;}}
 }
}
