using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;
namespace ZX6DisplayControl {
 public sealed class DeviceDiscovery:IDeviceDiscovery {
  public const string ExpectedInstance=@"USB\VID_1A86&PID_5722\USB35INCHIPSV2";
  public IReadOnlyList<HolderDevice> Find() {
   var devices=new List<HolderDevice>();
   var options=new EnumerationOptions{ReturnImmediately=false,Timeout=TimeSpan.FromSeconds(2)};
   using(var searcher=new ManagementObjectSearcher(new ManagementScope(@"root\CIMV2"),new ObjectQuery("SELECT PNPDeviceID,Name,ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'"),options))
   using(var results=searcher.Get()) foreach(ManagementObject row in results) using(row) {
    var device=FromPnp(Convert.ToString(row["PNPDeviceID"]),Convert.ToString(row["Name"]),Convert.ToInt32(row["ConfigManagerErrorCode"])==0);
    if(device!=null) devices.Add(device);
   }
   return devices.AsReadOnly();
  }
  public static HolderDevice FromPnp(string instanceId,string friendlyName,bool present) {
   if(!present || !string.Equals(instanceId,ExpectedInstance,StringComparison.OrdinalIgnoreCase)) return null;
   var match=Regex.Match(friendlyName??"",@"\((COM[1-9][0-9]*)\)$",RegexOptions.IgnoreCase);
   return match.Success?new HolderDevice(instanceId,match.Groups[1].Value.ToUpperInvariant(),"USB35INCHIPSV2"):null;
  }
 }
}
