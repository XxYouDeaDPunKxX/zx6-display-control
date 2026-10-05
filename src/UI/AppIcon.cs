using System.Drawing;
using System.Reflection;
namespace ZX6DisplayControl {
 internal static class AppIcon {
  public static Icon Load() {using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ZX6DisplayControl.holder.ico"))using(var icon=new Icon(stream,32,32))return (Icon)icon.Clone();}
 }
}
