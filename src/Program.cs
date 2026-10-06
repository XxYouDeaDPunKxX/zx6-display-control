using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
namespace ZX6DisplayControl {
 internal static class Program {
  [STAThread] private static void Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   if(args.Any(a=>a!="--tray")){MessageBox.Show("The only supported launch option is --tray.","Z-X6 Display Control");return;}
   string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZX6DisplayControl");
   var log=new EventLog(directory);
   try {
    using(var instance=new SingleInstance()) {
     if(!instance.TryAcquire()){instance.SignalExistingWindow();return;}
     var store=new SettingsStore(directory);var loaded=store.Load();var settings=loaded.Settings;
     var initial=new SessionConfiguration{Cpu=settings.ActiveProfile.Cpu.Copy(),Gpu=settings.ActiveProfile.Gpu.Copy(),DisplayEnabled=settings.DisplayEnabled};
     using(var controller=new SessionController(()=>new HolderSession(new SharedMemoryReader(),new DeviceDiscovery(),new SerialTransport(),new MonotonicClock(),s=>log.Write("session",s)),initial,(message,error)=>log.Write("controller failure",message,error)))
     using(var form=new MainForm(settings,store,controller,log,new StartupRegistration(),Application.ExecutablePath,new UpdateService(directory))) {
      PowerModeChangedEventHandler power=(s,e)=>{if(e.Mode==PowerModes.Suspend)controller.Suspend();else if(e.Mode==PowerModes.Resume)controller.Resume();};
      SystemEvents.PowerModeChanged+=power;
      form.BringToFrontRequested+=()=>{if(instance.ConsumeOpenRequest())form.ShowWindow();};
      form.Shown+=(s,e)=>{
       var area=Screen.FromControl(form).WorkingArea;if(form.Width>area.Width || form.Height>area.Height)form.WindowState=FormWindowState.Maximized;
       if(args.Contains("--tray"))form.StartInTray();
       if(loaded.Warning!=null)MessageBox.Show(form,loaded.Warning,"Settings recovered",MessageBoxButtons.OK,MessageBoxIcon.Warning);
      };
      log.Write("start","Z-X6 Display Control 0.1.0-beta.1");
      try {Application.Run(form);}finally{SystemEvents.PowerModeChanged-=power;controller.Stop();bool ended=controller.Completion.Wait(2000);string error=controller.FailureMessage??(controller.State==null?null:controller.State.CleanupError);log.Write("stop",!ended?"Shutdown requested; cleanup has not completed.":error??"Controller stopped; USB cleanup completed.");}
     }
    }
   }catch(Exception e){log.Write("fatal",e.Message,e);MessageBox.Show("The app could not continue: "+e.Message+"\n\nLog: "+directory,"Z-X6 Display Control",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}
