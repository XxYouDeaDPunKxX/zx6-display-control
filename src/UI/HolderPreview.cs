using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZX6DisplayControl {
 public sealed class HolderPreview:Control {
  public int? CpuTemperature {get;private set;}
  public int? GpuTemperature {get;private set;}
  public int? CpuLevel {get;private set;}
  public int? GpuLevel {get;private set;}
  public bool IsDraft {get;private set;}
  public string Caption {get;private set;}
  private static readonly int[] digits={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
  private static readonly Color[] barColors={Color.FromArgb(26,155,225),Color.FromArgb(24,195,242),Color.FromArgb(25,227,249),Color.FromArgb(75,241,253),Color.FromArgb(147,247,252),Color.FromArgb(213,253,255),Color.FromArgb(244,255,255)};
  public HolderPreview() {
   Name="HolderPreview";AccessibleName="Display preview";AccessibleRole=AccessibleRole.Graphic;
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   Caption="Waiting for display output";MinimumSize=new Size(200,140);
  }
  public void ShowOutput(string caption,int? cpu,int? gpu,int? cpuLevel,int? gpuLevel,bool draft) {
   Caption=caption;CpuTemperature=cpu;GpuTemperature=gpu;CpuLevel=cpuLevel;GpuLevel=gpuLevel;IsDraft=draft;
   AccessibleDescription=caption+". CPU "+Reading(cpu)+", level "+Reading(cpuLevel)+" of 7. GPU "+Reading(gpu)+", level "+Reading(gpuLevel)+" of 7. Representation of sent commands; the holder does not report its screen state.";Invalidate();
  }
  private static string Reading(int? value) {return value.HasValue?value.Value.ToString():"unavailable";}
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);var g=e.Graphics;g.Clear(UiTheme.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;
   int line=TextRenderer.MeasureText("Ag",Font).Height;
   float room=Math.Max(40,Height-line*4-38),width=Math.Min(Width-8,room*2),height=width/2;
   float top=Math.Max(line*2+12,(Height-height-line)/2);
   var screen=new RectangleF((Width-width)/2,top,width,height);
   TextRenderer.DrawText(g,Caption,Font,new Rectangle(0,(int)top-line*2-10,Width,line*2),IsDraft?UiTheme.Accent:UiTheme.Text,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
   var state=g.Save();g.TranslateTransform(screen.X,screen.Y);g.ScaleTransform(width/480f,height/240f);
   DrawFace(g);g.Restore(state);
   TextRenderer.DrawText(g,IsDraft?"Use profile to send this preview to the display.":"Live command preview",Font,new Rectangle(0,(int)screen.Bottom+16,Width,line*2),UiTheme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPrefix);
  }
  // Original scalable illustration of the Z-X6 face: two outer segmented
  // chevrons, white temperature digits and yellow CPU/GPU symbols.
  private void DrawFace(Graphics g) {
   bool contrast=SystemInformation.HighContrast;
   using(var bezel=new LinearGradientBrush(new Rectangle(0,0,480,240),Color.FromArgb(65,69,73),Color.FromArgb(19,21,23),LinearGradientMode.Vertical))
   using(var edge=new Pen(contrast?SystemColors.WindowText:Color.FromArgb(91,97,102),1))
   using(var face=new SolidBrush(contrast?SystemColors.Window:Color.FromArgb(4,6,8))) {
    g.FillRectangle(bezel,0,0,480,240);g.DrawRectangle(edge,0,0,479,239);g.FillRectangle(face,8,8,464,224);
   }
   bool active=CpuTemperature.HasValue || GpuTemperature.HasValue;
   Color outline=contrast?SystemColors.WindowText:active?Color.FromArgb(60,214,244):Color.FromArgb(20,39,45);
   using(var pen=new Pen(outline,1.7f)) {
    g.DrawLines(pen,new[]{new PointF(112,26),new PointF(66,37),new PointF(20,120),new PointF(66,207),new PointF(211,207)});
    g.DrawLines(pen,new[]{new PointF(368,26),new PointF(414,37),new PointF(460,120),new PointF(414,207),new PointF(269,207)});
   }
   DrawBar(g,CpuLevel,false,contrast);DrawBar(g,GpuLevel,true,contrast);
   DrawTemperature(g,98,CpuTemperature,"CPU",false,contrast);DrawTemperature(g,279,GpuTemperature,"GPU",true,contrast);
  }
  private static void DrawBar(Graphics g,int? level,bool right,bool contrast) {
   var state=g.Save();if(right){g.TranslateTransform(480,0);g.ScaleTransform(-1,1);}
   using(var shape=new GraphicsPath()) {
    shape.AddPolygon(new[]{new PointF(77,47),new PointF(95,47),new PointF(54,120),new PointF(95,193),new PointF(77,193),new PointF(36,120)});
    g.SetClip(shape,CombineMode.Intersect);
    for(int i=0;i<7;i++) {
     bool lit=level.HasValue && i<level.Value;
     Color color=contrast?(lit?SystemColors.WindowText:SystemColors.GrayText):lit?barColors[i]:Color.FromArgb(14,25,31);
     using(var brush=new SolidBrush(color))g.FillRectangle(brush,34,193-(i+1)*146f/7,63,146f/7-2.5f);
    }
   }
   g.Restore(state);
  }
  private static void DrawTemperature(Graphics g,float x,int? temperature,string label,bool gpu,bool contrast) {
   Color lit=contrast?SystemColors.WindowText:Color.FromArgb(245,251,251),dim=contrast?SystemColors.GrayText:Color.FromArgb(13,19,22);
   using(var small=new Font("Segoe UI",9,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var caption=new Font("Segoe UI",18,FontStyle.Regular,GraphicsUnit.Pixel))
   using(var unit=new Font("Segoe UI",17,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var ink=new SolidBrush(temperature.HasValue?lit:dim)) {
    g.DrawString("TEMP",small,ink,x+5,53);g.DrawString("°C",unit,ink,x+93,130);g.DrawString(label,caption,ink,x+34,179);
   }
   var digitState=g.Save();g.TranslateTransform(x+7,75);
   using(var slant=new Matrix(1,0,-.055f,1,4,0))g.MultiplyTransform(slant);
   DrawDigit(g,0,temperature.HasValue?temperature.Value/10:-1,lit,dim);DrawDigit(g,44,temperature.HasValue?temperature.Value%10:-1,lit,dim);g.Restore(digitState);
   Color symbol=contrast?SystemColors.WindowText:temperature.HasValue?Color.FromArgb(247,215,64):dim;
   using(var pen=new Pen(symbol,1.8f)) {
    float sx=x+10,sy=184;g.DrawRectangle(pen,sx,sy,14,12);g.DrawRectangle(pen,sx+4,sy+3,6,6);
    if(gpu){g.DrawLine(pen,sx+15,sy,sx+18,sy);g.DrawLine(pen,sx+18,sy,sx+18,sy+14);g.DrawLine(pen,sx+3,sy+15,sx+12,sy+15);}
    else for(int i=0;i<3;i++){g.DrawLine(pen,sx-3,sy+2+i*4,sx,sy+2+i*4);g.DrawLine(pen,sx+14,sy+2+i*4,sx+17,sy+2+i*4);g.DrawLine(pen,sx+3+i*4,sy-3,sx+3+i*4,sy);g.DrawLine(pen,sx+3+i*4,sy+12,sx+3+i*4,sy+15);}
   }
  }
  private static void DrawDigit(Graphics g,float x,int digit,Color lit,Color dim) {
   PointF[][] parts={
    new[]{new PointF(7,0),new PointF(31,0),new PointF(37,5),new PointF(30,10),new PointF(8,10),new PointF(2,5)},
    new[]{new PointF(38,7),new PointF(38,39),new PointF(33,44),new PointF(28,38),new PointF(28,14)},
    new[]{new PointF(38,49),new PointF(38,81),new PointF(28,74),new PointF(28,55),new PointF(33,45)},
    new[]{new PointF(7,79),new PointF(31,79),new PointF(37,85),new PointF(30,89),new PointF(8,89),new PointF(2,85)},
    new[]{new PointF(0,49),new PointF(5,45),new PointF(10,55),new PointF(10,74),new PointF(0,81)},
    new[]{new PointF(0,7),new PointF(10,14),new PointF(10,38),new PointF(5,44),new PointF(0,39)},
    new[]{new PointF(7,40),new PointF(31,40),new PointF(36,45),new PointF(30,50),new PointF(8,50),new PointF(2,45)}
   };
   var state=g.Save();g.TranslateTransform(x,0);
   for(int i=0;i<7;i++)using(var brush=new SolidBrush(digit<0?dim:(digits[digit]&(1<<i))!=0?lit:dim))g.FillPolygon(brush,parts[i]);g.Restore(state);
  }
 }
}
