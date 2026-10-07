using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
namespace ZX6DisplayControl.Tests {
 public static class LanguageTests {
  private sealed class LocalizedReader:IAidaReader {
   public SensorReadResult Read(long now){throw new Win32Exception(5,"Accesso negato");}
  }
  [Test] public static void Language_SystemErrorReachesSessionInEnglish() {
   var session=new HolderSession(new LocalizedReader(),new FakeDiscovery(),new FakeSerial(),new FakeClock(),s=>{});
   try {session.Tick();Assert.True(session.State.Error.Contains("Access is denied") && !session.State.Error.Contains("Accesso negato"),session.State.Error);}
   finally {session.Stop();}
  }
  private static System.Type Language() {var type=typeof(MainForm).Assembly.GetType("ZX6DisplayControl.AppLanguage");Assert.True(type!=null,"Application language policy is missing");return type;}
  private static string Error(Exception error){return (string)Language().GetMethod("ErrorMessage").Invoke(null,new object[]{error});}
  [Test] public static void Language_NativeErrorCodesAndAuthoredValidationRemainUseful() {
   string text=Error(new IOException("Accesso negato",unchecked((int)0x80070005)));
   Assert.True(text.Contains("Access is denied") && text.Contains("5"),text);
   text=Error(new Win32Exception(0x1FFFFFFE,"Errore sconosciuto"));
   Assert.True(text.Contains("Windows error") && text.Contains("536870910") && !text.Contains("sconosciuto"),text);
   Assert.Equal("Choose a display temperature.",Error(new InvalidOperationException("Choose a display temperature.")));
  }
  [Test] public static void Language_EnglishUiPreservesRegionalNumberInput() {
   var culture=Thread.CurrentThread.CurrentCulture;var ui=Thread.CurrentThread.CurrentUICulture;var defaultUi=CultureInfo.DefaultThreadCurrentUICulture;
   try {
    Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("it-IT");Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
    Language().GetMethod("Initialize").Invoke(null,null);
    Assert.Equal("en-US",Thread.CurrentThread.CurrentUICulture.Name);
    string workerUi=null;var worker=new Thread(()=>workerUi=Thread.CurrentThread.CurrentUICulture.Name);worker.Start();Assert.True(worker.Join(2000));Assert.Equal("en-US",workerUi);
    Assert.Equal("1,5",1.5.ToString("0.0"));Assert.Equal(1.5,double.Parse("1,5"));
   } finally {CultureInfo.DefaultThreadCurrentUICulture=defaultUi;Thread.CurrentThread.CurrentUICulture=ui;Thread.CurrentThread.CurrentCulture=culture;}
  }
 }
}
