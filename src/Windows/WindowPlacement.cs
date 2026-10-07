using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace ZX6DisplayControl {
 [DataContract] public sealed class WindowPlacement {
  [DataMember] public int X, Y, Width, Height;
  [DataMember] public bool Maximized;
  public Rectangle Bounds {get{return new Rectangle(X,Y,Width,Height);}}
  public static Rectangle Fit(Rectangle bounds,Rectangle[] workAreas,Size minimum) {
   var area=workAreas.OrderByDescending(a=>{var intersection=Rectangle.Intersect(a,bounds);return (long)intersection.Width*intersection.Height;}).First();
   int width=Math.Min(area.Width,Math.Max(minimum.Width,bounds.Width)),height=Math.Min(area.Height,Math.Max(minimum.Height,bounds.Height));
   return new Rectangle(Math.Max(area.Left,Math.Min(area.Right-width,bounds.X)),Math.Max(area.Top,Math.Min(area.Bottom-height,bounds.Y)),width,height);
  }
 }
 public sealed class WindowPlacementStore {
  private readonly string path;
  public WindowPlacementStore(string directory){path=Path.Combine(directory,"window.json");}
  public WindowPlacement Load() {
   try {if(!File.Exists(path) || new FileInfo(path).Length>4096)return null;using(var stream=File.OpenRead(path)){var value=(WindowPlacement)new DataContractJsonSerializer(typeof(WindowPlacement)).ReadObject(stream);return value!=null && value.Width>0 && value.Height>0 && value.Width<32768 && value.Height<32768 && Math.Abs((long)value.X)<100000 && Math.Abs((long)value.Y)<100000?value:null;}}
   catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(SerializationException){return null;}
  }
  public void Save(WindowPlacement value) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".tmp";
   try {using(var stream=File.Create(temporary))new DataContractJsonSerializer(typeof(WindowPlacement)).WriteObject(stream,value);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
   finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
