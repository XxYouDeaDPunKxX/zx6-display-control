namespace ZX6DisplayControl.Tests {
 public static class ProfileEditorTests {
  [Test] public static void ProfileEditor_CancelAndAtomicApply() {
   var original=AppSettings.Defaults().ActiveProfile;var editor=new ProfileEditor(original);editor.Draft.Cpu.Animation.FixedFps=4;
   Assert.Equal(2.0,original.Cpu.Animation.FixedFps);Assert.Equal(2.0,editor.Draft.Gpu.Animation.FixedFps);editor.Cancel();Assert.Equal(2.0,editor.Draft.Cpu.Animation.FixedFps);
   editor.Draft.Cpu.TemperatureId="TEMPORARILY_MISSING";Assert.Equal(0,editor.Validate(null).Count);var applied=editor.Apply();editor.Draft.Cpu.TemperatureId="TCPU";Assert.Equal("TEMPORARILY_MISSING",applied.Cpu.TemperatureId);
   editor.Draft.Cpu.Animation.InputMax=0;Assert.True(editor.Validate(null).Count>0);Assert.Throws<System.InvalidOperationException>(()=>editor.Apply());
  }
 }
}
