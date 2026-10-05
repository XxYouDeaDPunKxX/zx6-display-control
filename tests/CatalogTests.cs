using System.Linq;
namespace ZX6DisplayControl.Tests {
 public static class CatalogTests {
  [Test] public static void Catalog_FilterAndIdentity() {
   var snapshot=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><label>CPU</label><value>33</value></temp><sys><id>SCPUUTI</id><label>CPU Utilization</label><value>12</value></sys><sys><id>STEXT</id><label>Info</label><value>unknown</value></sys>").Snapshot;
   Assert.Equal(2,SensorCatalog.Filter(snapshot,"cpu",null,true).Count);
   Assert.Equal("TGPU1",SensorCatalog.Filter(snapshot,"tgpu1",null,true).Single().Id);
   Assert.Equal(2,SensorCatalog.Filter(snapshot,"","sys",false).Count);
   Assert.True(!SensorCatalog.Filter(snapshot,"STEXT",null,false).Single().Number.HasValue);
   Assert.Equal(0,SensorCatalog.Filter(null,"",null,false).Count);
  }
 }
}
