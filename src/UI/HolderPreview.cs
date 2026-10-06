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
  public HolderPreview() {
   Name="HolderPreview";AccessibleName="Display preview";AccessibleRole=AccessibleRole.Graphic;
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   Caption="Waiting for display output";MinimumSize=new Size(200,140);
  }
  public void ShowOutput(string caption,int? cpu,int? gpu,int? cpuLevel,int? gpuLevel,bool draft) {
   Caption=caption;CpuTemperature=cpu;GpuTemperature=gpu;CpuLevel=cpuLevel;GpuLevel=gpuLevel;IsDraft=draft;
   AccessibleDescription=caption+". CPU "+Reading(cpu)+", level "+Reading(cpuLevel)+" of 7. GPU "+Reading(gpu)+", level "+Reading(gpuLevel)+" of 7. Representation of sent commands; the holder does not report its screen state.";Invalidate();
  }
  private static string Reading(int? n) {return n.HasValue?n.Value.ToString():"unavailable";}
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);var g=e.Graphics;g.Clear(UiTheme.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;
   int line=TextRenderer.MeasureText("Ag",Font).Height;
   TextRenderer.DrawText(g,Caption,Font,new Rectangle(0,12,Width,line*2+6),IsDraft?UiTheme.Accent:UiTheme.Text,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPrefix);
   float captionBottom=line*2+26,footer=Height>=340?line*4+26:line+16;
   float room=Math.Max(60,Height-captionBottom-footer),width=Math.Min(Width-12,room/.53f),height=width*.53f;
   float top=captionBottom+Math.Max(0,(room-height)*.35f);
   var screen=new RectangleF((Width-width)/2,top,width,height);
   using(var fill=new SolidBrush(SystemInformation.HighContrast?SystemColors.Window:Color.FromArgb(10,16,20)))g.FillRectangle(fill,screen);
   using(var pen=new Pen(UiTheme.Muted,1))g.DrawRectangle(pen,screen.X,screen.Y,screen.Width,screen.Height);
   var state=g.Save();g.TranslateTransform(screen.X,screen.Y);g.ScaleTransform(screen.Width/360f,screen.Height/190f);
   Color light=SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(114,230,224),dim=SystemInformation.HighContrast?SystemColors.GrayText:Color.FromArgb(28,55,61);
   DrawSide(g,0,CpuTemperature,CpuLevel,"CPU",light,dim);DrawSide(g,180,GpuTemperature,GpuLevel,"GPU",light,dim);g.Restore(state);
   int y=(int)screen.Bottom+14;
   TextRenderer.DrawText(g,"CPU  "+Reading(CpuLevel)+" / 7     GPU  "+Reading(GpuLevel)+" / 7",Font,new Rectangle(0,y,Width,line*2),UiTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);
   if(Height>=340)TextRenderer.DrawText(g,IsDraft?"Changes stay here until you save or use the profile.":"Preview of commands sent to the holder.",Font,new Rectangle(4,y+line*2+10,Width-8,line*3),UiTheme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter);
  }
  private static void DrawSide(Graphics g,int x,int? temperature,int? level,string label,Color lit,Color dim) {
   using(var labelFont=new Font("Segoe UI",12,FontStyle.Bold))using(var brush=new SolidBrush(lit)) {
    g.DrawString(label,labelFont,brush,x+70,140);g.DrawString("°C",labelFont,brush,x+137,77);
   }
   DrawDigit(g,x+49,51,temperature.HasValue?temperature.Value/10:-1,lit,dim);DrawDigit(g,x+96,51,temperature.HasValue?temperature.Value%10:-1,lit,dim);
   for(int i=0;i<7;i++) {
    float cy=128-i*13;bool on=level.HasValue && i<level.Value;
    using(var pen=new Pen(on?lit:dim,5)){g.DrawLines(pen,new[]{new PointF(x+31,cy+6),new PointF(x+24,cy),new PointF(x+31,cy-6)});g.DrawLines(pen,new[]{new PointF(x+159,cy+6),new PointF(x+166,cy),new PointF(x+159,cy-6)});}
   }
  }
  private static readonly int[] digits={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
  private static void DrawDigit(Graphics g,float x,float y,int digit,Color lit,Color dim) {
   var parts=new[]{new RectangleF(x+6,y,25,5),new RectangleF(x+32,y+6,5,28),new RectangleF(x+32,y+40,5,28),new RectangleF(x+6,y+69,25,5),new RectangleF(x,y+40,5,28),new RectangleF(x,y+6,5,28),new RectangleF(x+6,y+35,25,5)};
   for(int i=0;i<7;i++)using(var brush=new SolidBrush(digit<0?(i==6?lit:dim):(digits[digit]&(1<<i))!=0?lit:dim))g.FillRectangle(brush,parts[i]);
  }
 }
}
