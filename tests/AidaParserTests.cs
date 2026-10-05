using System;
using System.Globalization;
using System.Text;
using System.Threading;
namespace ZX6DisplayControl.Tests {
 public static class AidaParserTests {
  public const string Cpu="<temp><id>TCPU</id><label>CPU</label><value>42.5</value></temp>";
  public static SensorReadResult Parse(string text) {return ExportParser.Parse(Encoding.Default.GetBytes(text+"\0"),500);}
  [Test] public static void AidaParser_CultureIndependent() {
   var before=Thread.CurrentThread.CurrentCulture;
   try {foreach(string culture in new[]{"it-IT","en-US"}) {Thread.CurrentThread.CurrentCulture=new CultureInfo(culture);var r=Parse(Cpu);Assert.True(r.Snapshot!=null,r.Message);Assert.Equal(42.5,r.Snapshot.Values["TCPU"].Number.Value);Assert.Equal("°C",r.Snapshot.Values["TCPU"].Unit);Assert.Equal(500L,r.Snapshot.ReadAtMilliseconds);}}
   finally {Thread.CurrentThread.CurrentCulture=before;}
  }
  [Test] public static void AidaParser_RejectsPartialAndAmbiguousBuffers() {
   foreach(string bad in new[]{"",Cpu.Substring(0,Cpu.Length-4),Cpu+Cpu,"<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///invalid'>]><temp><id>TCPU</id><value>&x;</value></temp>","<temp><id>TCPU</id></temp>","<temp><id>TCPU</id><value>3</value><value>4</value></temp>"}) Assert.True(Parse(bad).Snapshot==null,"Accepted invalid fragment");
   Assert.True(ExportParser.Parse(Encoding.ASCII.GetBytes(Cpu),0).Snapshot==null,"Accepted missing terminator");
   Assert.True(ExportParser.Parse(new byte[1048577],0).Snapshot==null,"Accepted oversize");
  }
  [Test] public static void AidaParser_UnicodeAndFiniteNumbers() {
   var bytes=Encoding.Unicode.GetBytes(Cpu+"\0");
   Assert.Equal(42.5,ExportParser.Parse(bytes,0).Snapshot.Values["TCPU"].Number.Value);
   Assert.True(ExportParser.Parse(Encoding.Unicode.GetBytes(Cpu),0).Snapshot==null);
   foreach(string invalid in new[]{"NaN","Infinity","-Infinity","unknown","42,5"}) {var row=Parse(Cpu.Replace("42.5",invalid)).Snapshot.Values["TCPU"];Assert.True(!row.Number.HasValue);Assert.Equal(invalid,row.RawValue);}
  }
  [Test] public static void AidaParser_UnitsAreNotGuessedFromLabel() {
   var r=Parse("<sys><id>SCPUUTI</id><label>Load</label><value>25</value></sys><sys><id>CUSTOM</id><label>Temperature</label><value>8</value></sys>");
   Assert.Equal("%",r.Snapshot.Values["SCPUUTI"].Unit);Assert.Equal<string>(null,r.Snapshot.Values["CUSTOM"].Unit);
  }
 }
}
