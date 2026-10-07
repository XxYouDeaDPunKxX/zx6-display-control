using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace ZX6DisplayControl {
 public static class AppLanguage {
  public static void Initialize() {
   var english=CultureInfo.GetCultureInfo("en-US");
   CultureInfo.DefaultThreadCurrentUICulture=english;Thread.CurrentThread.CurrentUICulture=english;
   // Process-local preference; Windows components may fall back to installed resources.
   // CurrentCulture is deliberately unchanged so regional number input keeps working.
   uint count;SetProcessPreferredUILanguages(8,"en-US\0\0",out count);
  }
  public static string ErrorMessage(Exception error) {
   var native=error as Win32Exception;uint hresult=unchecked((uint)error.HResult);
   bool win32=native!=null || (hresult&0xffff0000)==0x80070000;
   if(!win32 && !(error is ExternalException))return error.Message;
   uint code=native!=null?unchecked((uint)native.NativeErrorCode):win32?hresult&0xffff:hresult;
   string identity=win32?"Windows error "+code.ToString(CultureInfo.InvariantCulture):"System error 0x"+code.ToString("X8",CultureInfo.InvariantCulture);
   string known=win32?CommonWindowsError(code):null;
   if(known!=null)return known+" ("+identity+")";
   var buffer=new StringBuilder(2048);
   uint length=FormatMessage(0x1200,IntPtr.Zero,code,0x0409,buffer,(uint)buffer.Capacity,IntPtr.Zero);
   string message=buffer.ToString().Trim();
   return length==0 || System.Text.RegularExpressions.Regex.IsMatch(message,@"%[1-9]")?identity+".":message+" ("+identity+")";
  }
  private static string CommonWindowsError(uint code) {
   // English system resources are not installed on every Windows edition.
   switch(code) {
    case 2:return "The file was not found.";
    case 3:return "The folder was not found.";
    case 5:return "Access is denied. Check permissions or close the app using this resource.";
    case 6:return "The device or file handle is no longer valid. Retry the operation.";
    case 21:return "The device is not ready. Check its connection.";
    case 31:return "The device is not working. Check its connection.";
    case 32:case 33:return "The file or device is in use by another process. Close it and retry.";
    case 87:return "The system rejected an operation parameter.";
    case 112:return "There is not enough free disk space.";
    case 121:return "The operation timed out. Check the device connection and retry.";
    case 123:return "The file or folder name is invalid.";
    case 206:return "The file path is too long. Choose a shorter path.";
    case 995:return "The operation was canceled.";
    case 1167:return "The device is disconnected. Reconnect it and retry.";
    default:return null;
   }
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetProcessPreferredUILanguages(uint flags,string languages,out uint count);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="FormatMessageW")]
  private static extern uint FormatMessage(uint flags,IntPtr source,uint message,uint language,StringBuilder buffer,uint size,IntPtr arguments);
 }
}
