using System;
using System.IO.Ports;
namespace ZX6DisplayControl {
 public sealed class SerialTransport:ISerialTransport {
  private SerialPort port;
  public bool IsOpen {get{return port!=null && port.IsOpen;}}
  public void Open(string portName) {
   if(IsOpen) throw new InvalidOperationException("Port is already open.");
   Close();port=new SerialPort(portName,115200,Parity.None,8,StopBits.One){DtrEnable=true,RtsEnable=true,WriteTimeout=1000};
   try {port.Open();} catch {Close();throw;}
  }
  public void Write(byte[] packet) {if(!IsOpen) throw new System.IO.IOException("Port is closed.");port.Write(packet,0,packet.Length);}
  public void Close() {var old=port;port=null;if(old!=null) old.Dispose();}
  public void Dispose() {Close();}
 }
}
