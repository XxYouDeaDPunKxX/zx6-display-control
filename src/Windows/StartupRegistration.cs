using System;
using System.IO;
using Microsoft.Win32;
namespace ZX6DisplayControl {
 public interface IStartupStore {string Read();void Write(string value);void Delete();}
 public sealed class StartupRegistration {
  private readonly IStartupStore store;
  public StartupRegistration(IStartupStore store=null) {this.store=store??new RegistryStartupStore();}
  public bool GetEnabled(string exePath) {return string.Equals(store.Read(),Command(exePath),StringComparison.OrdinalIgnoreCase);}
  public void SetEnabled(bool enabled,string exePath) {if(enabled) store.Write(Command(exePath));else store.Delete();}
  public void CommitEnabled(bool enabled,string exePath,Action persistSettings) {
   string previous=store.Read(),desired=enabled?Command(exePath):null;
   bool changed=!string.Equals(previous,desired,StringComparison.OrdinalIgnoreCase);
   if(changed)SetCommand(desired);
   try {persistSettings();}
   catch(Exception saveError) {
    if(changed)try{SetCommand(previous);}catch(Exception rollbackError){throw new AggregateException("Settings could not be saved and the previous startup setting could not be restored. Check Start with Windows before exiting.",saveError,rollbackError);}
    throw;
   }
  }
  private void SetCommand(string value) {if(value==null)store.Delete();else store.Write(value);}
  private static string Command(string exePath) {
   if(string.IsNullOrWhiteSpace(exePath) || !Path.IsPathRooted(exePath) || exePath.IndexOf('"')>=0) throw new ArgumentException("Invalid executable path.");
   return "\""+Path.GetFullPath(exePath)+"\" --tray";
  }
 }
 internal sealed class RegistryStartupStore:IStartupStore {
  private const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run",Name="ZX6DisplayControl";
  public string Read() {using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key==null?null:key.GetValue(Name) as string;}
  public void Write(string value) {using(var key=Registry.CurrentUser.CreateSubKey(Key)) key.SetValue(Name,value,RegistryValueKind.String);}
  public void Delete() {using(var key=Registry.CurrentUser.OpenSubKey(Key,true)) if(key!=null) key.DeleteValue(Name,false);}
 }
}
