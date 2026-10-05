using System.IO;
using System.Linq;
namespace ZX6DisplayControl.Tests {
 public static class EventLogTests {
  [Test] public static void EventLog_BoundsAndExportPrivacy() {
   using(var dir=new TempDirectory()) using(var r=new SessionRig()) {
    var log=new EventLog(dir.Path);for(int i=0;i<350;i++) log.Write("test",new string('è',8000));
    var files=Directory.GetFiles(dir.Path,"events*.log");Assert.Equal(2,files.Length);Assert.True(files.All(p=>new FileInfo(p).Length<=1048576));
    r.Reader.Result=AidaParserTests.Parse(AidaParserTests.Cpu+"<temp><id>TGPU1</id><value>33</value></temp><sys><id>UNRELATED_SECRET</id><label>PRIVATE-LABEL</label><value>123</value></sys>");r.At(0);r.At(100);
    string report=Path.Combine(dir.Path,"diagnostic.txt");log.Export(report,r.Session.State,AppSettings.Defaults(),"0.1.0");string text=File.ReadAllText(report);
    Assert.True(text.Contains("0.1.0") && text.Contains("COM5") && text.Contains("TCPU"));Assert.True(!text.Contains("UNRELATED_SECRET") && !text.Contains("PRIVATE-LABEL"));
   }
  }
 }
}
