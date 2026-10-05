using System;
namespace ZX6DisplayControl {
 public static class PacketCodec {
  public static byte[] Encode(double cpuCelsius,double gpuCelsius,int cpuFrame,int gpuFrame) {
   ValidateTemperature(cpuCelsius,"cpuCelsius"); ValidateTemperature(gpuCelsius,"gpuCelsius");
   if(cpuFrame<0 || cpuFrame>7) throw new ArgumentOutOfRangeException("cpuFrame");
   if(gpuFrame<0 || gpuFrame>7) throw new ArgumentOutOfRangeException("gpuFrame");
   int cpu=(int)Math.Floor(cpuCelsius),gpu=(int)Math.Floor(gpuCelsius);
   var packet=new byte[20]; packet[1]=(byte)(gpu>>4); packet[2]=(byte)((gpu&15)<<4); packet[5]=169;
   packet[6]=(byte)(cpu/10);packet[7]=(byte)(cpu%10);packet[8]=(byte)(gpu/10);packet[9]=(byte)(gpu%10);
   packet[10]=(byte)cpuFrame;packet[11]=(byte)gpuFrame;return packet;
  }
  public static byte[] Control(byte command) {
   if(command!=108 && command!=109 && command!=255) throw new ArgumentOutOfRangeException("command");
   return new byte[]{0,0,0,0,0,command};
  }
  private static void ValidateTemperature(double value,string name) {
   if(double.IsNaN(value) || double.IsInfinity(value) || value<0 || value>99) throw new ArgumentOutOfRangeException(name,"Temperature must be between 0 and 99 °C.");
  }
 }
}
