using System;
using System.IO;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class PlaylistTests {
  private static Profile Read(string steps) {
   using(var dir=new TempDirectory()) {
    var p=new Profile{Name="Playlist test"};var store=new SettingsStore(dir.Path);string file=Path.Combine(dir.Path,"profile.json");store.ExportProfile(p,file);
    string json=File.ReadAllText(file).Replace("\"Mode\":0","\"Mode\":3,\"Steps\":"+steps);
    File.WriteAllText(file,json);return store.ImportProfile(file);
   }
  }
  private const string Steps="[{\"Pattern\":0,\"Seconds\":2,\"Fps\":2},{\"Pattern\":1,\"Seconds\":2,\"Fps\":4}]";
  [Test] public static void Playlist_ChangesPatternAndSpeedAtStepBoundary() {
   var a=Read(Steps).Cpu.Animation;var engine=new AnimationEngine(new Random(1));
   Assert.Equal(0,engine.Advance(a,null,0,false).Frame);
   Assert.Equal(2,engine.Advance(a,null,1,false).Frame);
   var next=engine.Advance(a,null,1,false);Assert.Equal(7,next.Frame);Assert.Near(4,next.FramesPerSecond.Value);
   Assert.Equal(5,engine.Advance(a,null,.5,false).Frame);
   engine.Advance(a,null,.5,false);Assert.Equal(0,engine.Advance(a,null,1,false).Frame);
  }
  [Test] public static void Playlist_PauseAndDelayedWorkerDoNotSkipThroughEffects() {
   var a=Read(Steps).Cpu.Animation;var engine=new AnimationEngine(new Random(1));engine.Advance(a,null,0,false);
   Assert.Equal(1,engine.Advance(a,null,.5,false).Frame);
   Assert.Equal(1,engine.Advance(a,null,20,true).Frame);
   Assert.Equal(1,engine.Advance(a,null,.5,false).Frame);
   Assert.Equal(1,engine.Advance(a,null,20,false).Frame);
   Assert.Equal(3,engine.Advance(a,null,1,false).Frame);
  }
  [Test] public static void Playlist_RejectsEmptyAndInvalidEntries() {
   Assert.Throws<InvalidDataException>(()=>Read("[]"));
   Assert.Throws<InvalidDataException>(()=>Read("[{\"Pattern\":0,\"Seconds\":0,\"Fps\":2}]"));
   Assert.Throws<InvalidDataException>(()=>Read("[{\"Pattern\":0,\"Seconds\":2,\"Fps\":8}]"));
  }
  [Test] public static void Playlist_EditorRoundTripAndCopyDoNotShareSteps() {
   var profile=Read(Steps);using(var editor=new ChannelControl()) {
    editor.Load(profile.Cpu,null);var edited=editor.GetDraft();
    var roundTrip=profile.Copy();roundTrip.Cpu=edited;Assert.True(profile.SameContent(roundTrip));
    var list=UiBindingTests.Find<ListBox>(editor,"PlaylistSteps");list.SelectedIndex=1;
    UiBindingTests.Find<NumericUpDown>(editor,"StepSeconds").Value=12;
    var draft=editor.GetDraft();Assert.Equal(12,draft.Animation.Steps[1].Seconds);Assert.Equal(2,profile.Cpu.Animation.Steps[1].Seconds);
    draft.Animation.Steps[1].Seconds=99;Assert.Equal(12,editor.GetDraft().Animation.Steps[1].Seconds);
   }
  }
 }
}
