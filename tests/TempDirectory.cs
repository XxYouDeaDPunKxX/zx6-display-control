using System;
using System.IO;
namespace ZX6DisplayControl.Tests {
 public sealed class TempDirectory:IDisposable {
  private readonly string boundary;
  public string Path {get;private set;}
  public TempDirectory() {
   boundary=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","obj","tests"));
   Path=System.IO.Path.Combine(boundary,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);
  }
  public void Dispose() {
   string resolved=System.IO.Path.GetFullPath(Path);
   if(!resolved.StartsWith(boundary+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test directory outside boundary");
   if(Directory.Exists(resolved)) Directory.Delete(resolved,true);
  }
 }
}
