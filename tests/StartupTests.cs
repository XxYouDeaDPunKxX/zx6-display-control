using System;
namespace ZX6DisplayControl.Tests {
 public sealed class FakeStartupStore:IStartupStore {public string Value;public int Writes,Deletes;public string Read(){return Value;}public void Write(string value){Value=value;Writes++;}public void Delete(){Value=null;Deletes++;}}
 public static class StartupTests {
  [Test] public static void Startup_ExactRegistrationAndOptIn() {
   var store=new FakeStartupStore();var registration=new StartupRegistration(store);string exe=@"C:\Programs With Spaces\Holder.exe";
   Assert.True(!registration.GetEnabled(exe));Assert.Equal(0,store.Writes);
   registration.SetEnabled(true,exe);Assert.Equal("\"C:\\Programs With Spaces\\Holder.exe\" --tray",store.Value);Assert.True(registration.GetEnabled(exe));
   Assert.True(!registration.GetEnabled(@"C:\Different\Holder.exe"));registration.SetEnabled(false,exe);Assert.Equal(1,store.Deletes);Assert.True(!registration.GetEnabled(exe));
   Assert.Throws<ArgumentException>(()=>registration.SetEnabled(true,"relative.exe"));
  }
 }
}
