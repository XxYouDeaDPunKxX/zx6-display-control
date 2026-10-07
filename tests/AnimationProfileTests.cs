using System;
using System.Linq;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class AnimationProfileTests {
  [Test] public static void Animation_SmoothedUsageReachesBothEndsOfTheBar() {
   foreach(double hysteresis in new[]{2.0,10.0}) {
   var settings=new AnimationSettings{Mode=AnimationMode.SensorLevel,SensorId="SCPUUTI",FilterSeconds=2,HysteresisPercent=hysteresis};
   var engine=new AnimationEngine(new Random(7));
   engine.Advance(settings,0,0,false);AnimationOutput output=null;
   for(int i=0;i<48;i++)output=engine.Advance(settings,100,.125,false);
   Assert.Equal(7,output.Frame);
   for(int i=0;i<48;i++)output=engine.Advance(settings,0,.125,false);
   Assert.Equal(0,output.Frame);
   }
  }
  [Test] public static void Animation_UsageSelectsNearestSegmentWithoutBoundaryFlicker() {
   var settings=new AnimationSettings{Mode=AnimationMode.SensorLevel,SensorId="SCPUUTI",HysteresisPercent=0};
   var engine=new AnimationEngine(new Random(7));
   double[] readings={0,10,25,50,75,95,100};int[] levels={0,1,2,4,5,7,7};
   for(int i=0;i<readings.Length;i++)Assert.Equal(levels[i],engine.Advance(settings,readings[i],.5,false).Frame);
   settings.HysteresisPercent=2;engine.Reset();
   double[] noisy={0,8,10,6,5};int[] stable={0,0,1,1,0};
   for(int i=0;i<noisy.Length;i++)Assert.Equal(stable[i],engine.Advance(settings,noisy[i],.5,false).Frame);
  }
  [Test] public static void Profiles_AppliedBuiltInsSendTheirExpectedCpuAndGpuPatterns() {
   string[] names={"Classic","Activity","Thermal","Sensor levels","Breathe","Counterflow","Split tempo","Random"};
   int[] cpuFrames={2,4,4,7,1,2,1,-1},gpuFrames={2,1,1,0,1,5,4,-1};
   for(int i=0;i<names.Length;i++)using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.Reader.Result=AidaParserTests.Parse("<temp><id>TCPU</id><value>80</value></temp><temp><id>TGPU1</id><value>30</value></temp><sys><id>SCPUUTI</id><value>100</value></sys><sys><id>SGPU1UTI</id><value>0</value></sys>");
    rig.At(0);var controller=new FakeController{State=rig.Session.State};
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),controller,new EventLog(dir.Path),new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     UiTestPump.Wait(form.SelectProfile(names[i],()=>DialogResult.Cancel));UiTestPump.Wait(form.ApplyChanges());
     rig.Session.Apply(controller.Configuration);rig.At(100);
     for(int tick=1;tick<=8;tick++)rig.At(100+tick*125);
     var packets=rig.Serial.Packets.Where(p=>p.Length==20).ToArray();var last=packets.Last();
     if(cpuFrames[i]>=0){Assert.Equal(cpuFrames[i],(int)last[10]);Assert.Equal(gpuFrames[i],(int)last[11]);}
     else {Assert.True(packets.Select(p=>p[10]).Distinct().Count()>1);Assert.True(packets.Select(p=>p[11]).Distinct().Count()>1);}
     Assert.Equal(names[i],new SettingsStore(dir.Path).Load().Settings.ActiveProfileName);
    }
   }
  }
 }
}
