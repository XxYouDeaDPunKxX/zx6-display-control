using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class DiagnosticRegressionTests {
  private static IEnumerable<Control> All(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
  [Test] public static void Diagnostic_LogFailureAndRecentActionRemainVisible() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig()) {
    rig.At(0);string blocked=Path.Combine(dir.Path,"blocked");File.WriteAllText(blocked,"fixture");var log=new EventLog(blocked);log.Write("action failed","export fixture");
    using(var form=new MainForm(AppSettings.Defaults(),new SettingsStore(dir.Path),new FakeController{State=rig.Session.State},log,new StartupRegistration(new FakeStartupStore()),@"C:\Holder.exe")) {
     form.RefreshFromState();Assert.True(All(form).OfType<Label>().Any(x=>x.Text.Contains("Log file unavailable")),"File logging failure was hidden");
     var recent=All(form).OfType<TextBox>().SingleOrDefault(x=>x.Name=="RecentEvents");Assert.True(recent!=null && recent.ReadOnly && recent.Text.Contains("export fixture"),"Recent action was not readable in Diagnostics");
    }
   }
  }
  private sealed class HeldWriter:TextWriter {
   public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
   public override Encoding Encoding{get{return Encoding.UTF8;}}
   public override void Write(string text){Entered.Set();if(!Release.Wait(4000))throw new TimeoutException("writer fixture");}
  }
  [Test] public static void Diagnostic_ExportDestinationDoesNotBlockSessionLogging() {
   using(var dir=new TempDirectory())using(var rig=new SessionRig())using(var writer=new HeldWriter()) {
    var method=typeof(EventLog).GetMethod("Export",new[]{typeof(TextWriter),typeof(SessionState),typeof(AppSettings),typeof(string)});
    Assert.True(method!=null,"Export must separate its report snapshot from destination writing");
    var log=new EventLog(dir.Path);Task export=Task.Run(()=>method.Invoke(log,new object[]{writer,rig.Session.State,AppSettings.Defaults(),"test"}));
    try {Assert.True(writer.Entered.Wait(2500));var write=Task.Run(()=>log.Write("session","still running"));Assert.True(write.Wait(500),"Slow export blocked session logger");}
    finally {writer.Release.Set();export.GetAwaiter().GetResult();}
   }
  }
  [Test] public static void Diagnostic_RecentEventsSurviveLogFailureAndAreBounded() {
   using(var dir=new TempDirectory()) {
    string file=Path.Combine(dir.Path,"occupied");File.WriteAllText(file,"fixture");var log=new EventLog(file);
    for(int i=0;i<240;i++)log.Write("action","entry "+i);
    var method=typeof(EventLog).GetMethod("Recent");Assert.True(method!=null,"No event history remains available when file logging fails");
    var lines=(string[])method.Invoke(log,null);Assert.True(lines.Length<=200 && lines.Length>0);Assert.True(lines.Last().Contains("entry 239"));Assert.True(log.LastError!=null);
   }
  }
 }
}
