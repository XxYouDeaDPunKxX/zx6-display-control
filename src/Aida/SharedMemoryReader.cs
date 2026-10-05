using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
namespace ZX6DisplayControl {
 public sealed class SharedMemoryReader:IAidaReader {
  private readonly string mapName; private readonly Func<bool> processAvailable;
  public SharedMemoryReader(string mapName="AIDA64_SensorValues",Func<bool> processAvailable=null) {this.mapName=mapName;this.processAvailable=processAvailable??AidaExists;}
  public SensorReadResult Read(long nowMilliseconds) {
   try {
    if(!processAvailable()) return SensorReadResult.Failure("AidaMissing","AIDA64 is not running.");
    using(var map=MemoryMappedFile.OpenExisting(mapName,MemoryMappedFileRights.Read))
    using(var view=map.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read)) {
     if(view.Capacity<=0 || view.Capacity>1048576) return SensorReadResult.Failure("InvalidExport","AIDA64 export exceeds the 1 MB limit.");
     var buffer=new byte[(int)view.Capacity];view.ReadArray(0,buffer,0,buffer.Length);
     return ExportParser.Parse(buffer,nowMilliseconds);
    }
   } catch(FileNotFoundException) {return SensorReadResult.Failure("ExportMissing","Enable shared memory in AIDA64 Preferences > Hardware monitoring > External applications.");}
     catch(UnauthorizedAccessException) {return SensorReadResult.Failure("AccessDenied","Access to AIDA64 shared memory was denied.");}
     catch(IOException) {return SensorReadResult.Failure("ReadError","AIDA64 read failed. Retrying automatically.");}
  }
  private static bool AidaExists() {var processes=Process.GetProcessesByName("aida64");bool found=processes.Length>0;foreach(var process in processes) process.Dispose();return found;}
 }
}
