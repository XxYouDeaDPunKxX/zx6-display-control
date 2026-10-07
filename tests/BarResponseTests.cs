using System;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class BarResponseTests {
  private static readonly SensorSnapshot Catalog=AidaParserTests.Parse("<sys><id>SCPUUTI</id><label>CPU usage</label><value>8</value></sys><temp><id>TCPU</id><label>CPU</label><value>50</value></temp>").Snapshot;
  private static string Feedback(ChannelControl control,AnimationOutput output) {control.SetPreview(output);return UiBindingTests.Find<Label>(control,"BarResponse").Text;}
  [Test] public static void Response_PreviousPlaylistOutputCannotIndexTheNewShorterDraft() {
   var settings=new ChannelSettings{TemperatureId="TCPU",Animation=new AnimationSettings{Mode=AnimationMode.Playlist,Steps=new System.Collections.Generic.List<PlaylistStep>{new PlaylistStep{Seconds=1},new PlaylistStep{Seconds=1},new PlaylistStep{Seconds=1}}}};var engine=new AnimationEngine(new Random(1));engine.Advance(settings.Animation,null,0,false);engine.Advance(settings.Animation,null,1,false);var old=engine.Advance(settings.Animation,null,1,false);
   settings.Animation.Steps.RemoveRange(1,2);using(var control=new ChannelControl()){control.Load(settings,Catalog);Assert.True(Feedback(control,old).Contains("Updating playlist"));}
  }
  [Test] public static void Response_ExplainsTheSensorValueAndDiscreteBarOutput() {
   var settings=new ChannelSettings{TemperatureId="TCPU",Animation=new AnimationSettings{Mode=AnimationMode.SensorLevel,SensorId="SCPUUTI",HysteresisPercent=0}};var engine=new AnimationEngine(new Random(1));
   using(var control=new ChannelControl()) {control.Load(settings,Catalog);string text=Feedback(control,engine.Advance(settings.Animation,8,0,false));Assert.True(text.Contains("CPU usage") && text.Contains("8 %") && text.Contains("1 of 7 segments"),text);Assert.True(text.Contains("0–100 %"),text);}
  }
  [Test] public static void Response_ExplainsSmoothingAndBoundaryHoldWithoutCallingItStale() {
   var settings=new ChannelSettings{TemperatureId="TCPU",Animation=new AnimationSettings{Mode=AnimationMode.SensorLevel,SensorId="SCPUUTI",FilterSeconds=2}};var engine=new AnimationEngine(new Random(1));
   using(var control=new ChannelControl()) {
    control.Load(settings,Catalog);engine.Advance(settings.Animation,100,0,false);string text=Feedback(control,engine.Advance(settings.Animation,0,.5,false));Assert.True(text.Contains("Smoothed") && text.Contains("2 s") && text.Contains("0 %"),text);
    settings.Animation.FilterSeconds=0;control.Load(settings,Catalog);engine.Advance(settings.Animation,0,0,false);text=Feedback(control,engine.Advance(settings.Animation,8,.5,false));Assert.True(text.Contains("0 of 7 segments") && text.Contains("Held near a level boundary"),text);
   }
  }
  [Test] public static void Response_DistinguishesSpeedFromLevelAndExplainsMissingSource() {
   var settings=new ChannelSettings{TemperatureId="TCPU",Animation=new AnimationSettings{Mode=AnimationMode.Cycle,SensorId="SCPUUTI",RateMode=AnimationRateMode.Sensor}};var engine=new AnimationEngine(new Random(1));
   using(var control=new ChannelControl()) {
    control.Load(settings,Catalog);string text=Feedback(control,engine.Advance(settings.Animation,100,0,false));Assert.True(text.Contains("100 %") && text.Contains("4 updates/s") && text.Contains("Fill"),text);
    text=Feedback(control,engine.Advance(settings.Animation,null,.5,false));Assert.True(text.Contains("CPU usage unavailable") && text.Contains("holding"),text);
   }
  }
 }
}
