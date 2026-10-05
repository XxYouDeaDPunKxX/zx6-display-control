using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace ZX6DisplayControl.Tests {
 public static class TestRunner {
  [STAThread] public static int Main(string[] args) {
   string filter=args.Length==0?"":args[0]; int passed=0,failed=0;
   var methods=Assembly.GetExecutingAssembly().GetTypes().SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.Static)).Where(m=>m.GetCustomAttributes(typeof(TestAttribute),false).Length>0 && m.Name.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(m=>m.Name);
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   // Keep the real UI context alive across nested DoEvents calls in async tests.
   EventHandler run=null;run=(sender,eventArgs)=>{
    Application.Idle-=run;
    try {foreach(var test in methods) { try {test.Invoke(null,null);passed++;Console.WriteLine("PASS "+test.Name);} catch(Exception e){failed++;Console.WriteLine("FAIL "+test.Name+": "+(e.InnerException??e).Message);} }}
    finally {Application.ExitThread();}
   };
   Application.Idle+=run;Application.Run();
   Console.WriteLine(passed+" passed, "+failed+" failed"); return failed==0 && passed>0?0:1;
  }
 }
}
