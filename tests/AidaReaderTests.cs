using System;
using System.IO.MemoryMappedFiles;
using System.Text;
namespace ZX6DisplayControl.Tests {
 public static class AidaReaderTests {
  [Test] public static void AidaReader_MappingLifecycle() {
   string name="ZX6DisplayTest_"+Guid.NewGuid().ToString("N"); var reader=new SharedMemoryReader(name,()=>true);
   Assert.Equal("ExportMissing",reader.Read(0).ErrorCode);
   using(var map=MemoryMappedFile.CreateNew(name,4096)) {
    using(var view=map.CreateViewAccessor()) {var bytes=Encoding.ASCII.GetBytes(AidaParserTests.Cpu+"\0");view.WriteArray(0,bytes,0,bytes.Length);}
    Assert.Equal(42.5,reader.Read(5).Snapshot.Values["TCPU"].Number.Value);
    Assert.Equal("AidaMissing",new SharedMemoryReader(name,()=>false).Read(5).ErrorCode);
   }
   Assert.Equal("ExportMissing",reader.Read(10).ErrorCode);
   using(var large=MemoryMappedFile.CreateNew(name,1048577)) Assert.Equal("InvalidExport",reader.Read(20).ErrorCode);
  }
 }
}
