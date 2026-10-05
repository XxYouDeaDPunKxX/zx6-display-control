using System;
namespace ZX6DisplayControl.Tests {
 public static class AnimationEngineTests {
  static AnimationEngine Engine() {return new AnimationEngine(new Random(117));}
  static AnimationSettings Level() {return new AnimationSettings{Mode=AnimationMode.SensorLevel,SensorId="SCPUUTI",HysteresisPercent=0};}
  [Test] public static void Animation_FractionalMappingAndBounds() {
   Assert.Near(.25,AnimationEngine.Normalize(25,0,100,false));Assert.Near(.75,AnimationEngine.Normalize(25,0,100,true));
   Assert.Near(0,AnimationEngine.Normalize(-10,0,100,false));Assert.Near(1,AnimationEngine.Normalize(110,0,100,false));
   Assert.Near(2.5,AnimationEngine.Frequency(.5,1,4));
   Assert.Throws<ArgumentOutOfRangeException>(()=>AnimationEngine.Normalize(50,50,50,false));
   int[] expected={0,1,3,7};double[] inputs={0,25,50,100};var e=Engine();var s=Level();
   for(int i=0;i<inputs.Length;i++) Assert.Equal(expected[i],e.Advance(s,inputs[i],.5,false).Frame);
  }
  [Test] public static void Animation_SequencesAndRandom() {
   int[][] expected={new[]{0,1,2,3,4,5,6,7,0},new[]{7,6,5,4,3,2,1,0,7},new[]{0,1,2,3,4,5,6,7,6,5,4,3,2,1,0}};
   SequenceKind[] kinds={SequenceKind.Forward,SequenceKind.Reverse,SequenceKind.PingPong};
   for(int j=0;j<kinds.Length;j++) {var e=Engine();var s=new AnimationSettings{Sequence=kinds[j],FixedFps=4};for(int i=0;i<expected[j].Length;i++) Assert.Equal(expected[j][i],e.Advance(s,null,i==0?0:.25,false).Frame);}
   var a=Engine();var b=Engine();var random=new AnimationSettings{Sequence=SequenceKind.Random,FixedFps=4};int previous=a.Advance(random,null,0,false).Frame;b.Advance(random,null,0,false);
   for(int i=0;i<100;i++){int frame=a.Advance(random,null,.25,false).Frame;Assert.True(frame>=0 && frame<=7 && frame!=previous);Assert.Equal(frame,b.Advance(random,null,.25,false).Frame);previous=frame;}
  }
  [Test] public static void Animation_TimePauseMissingAndIndependentChannels() {
   var cpu=Engine();var gpu=Engine();var slow=new AnimationSettings{FixedFps=1};var fast=new AnimationSettings{FixedFps=4};
   cpu.Advance(slow,null,0,false);gpu.Advance(fast,null,0,false);
   Assert.Equal(0,cpu.Advance(slow,null,.25,false).Frame);Assert.Equal(1,gpu.Advance(fast,null,.25,false).Frame);
   Assert.Equal(1,gpu.Advance(fast,null,.25,true).Frame);Assert.Equal(1,gpu.Advance(fast,null,10,false).Frame);
   Assert.Equal(2,gpu.Advance(fast,null,.25,false).Frame);
   var s=new AnimationSettings{RateMode=AnimationRateMode.Sensor,SensorId="SCPUUTI"};var e=Engine();e.Advance(s,100,0,false);Assert.Equal(1,e.Advance(s,100,.25,false).Frame);
   var missing=e.Advance(s,null,.5,false);Assert.True(missing.SourceMissing);Assert.Equal(1,missing.Frame);
   Assert.Equal(1,e.Advance(s,100,0,false).Frame);
   Assert.Equal(2,e.Advance(s,100,.25,false).Frame);
   var fixedMode=new AnimationSettings{Mode=AnimationMode.Fixed,FixedFrame=6};Assert.Equal(6,e.Advance(fixedMode,null,0,false).Frame);
  }
  [Test] public static void Animation_FilterIsIndependentOfTickCount() {
   var s=Level();s.FilterSeconds=2;var a=Engine();var b=Engine();a.Advance(s,0,0,false);b.Advance(s,0,0,false);
   double single=a.Advance(s,100,2,false).FilteredValue.Value;AnimationOutput multiple=null;
   for(int i=0;i<4;i++) multiple=b.Advance(s,100,.5,false);
   Assert.Near(63.21205588,single,1e-7);Assert.Near(single,multiple.FilteredValue.Value);
  }
  [Test] public static void Animation_HysteresisAndEndpoints() {
   var s=Level();s.HysteresisPercent=2;var e=Engine();
   double[] inputs={50,58,60,56,55,100,0};int[] frames={3,3,4,4,3,7,0};
   for(int i=0;i<inputs.Length;i++) Assert.Equal(frames[i],e.Advance(s,inputs[i],.5,false).Frame);
  }
  [Test] public static void Animation_ValidatesConfigurations() {
   Assert.Equal(0,new AnimationSettings().Validate().Count);
   var s=Level();s.InputMax=0;Assert.True(s.Validate().Count>0);
   s=Level();s.FixedFps=8;Assert.True(s.Validate().Count>0);
   s=Level();s.FilterSeconds=3;Assert.True(s.Validate().Count>0);
   s=Level();s.InputMin=double.NaN;Assert.True(s.Validate().Count>0);
   s=Level();s.Mode=(AnimationMode)99;Assert.True(s.Validate().Count>0);
  }
 }
}
