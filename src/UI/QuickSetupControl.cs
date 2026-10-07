using System;
using System.Windows.Forms;
namespace ZX6DisplayControl {
 internal sealed class QuickSetupControl:UserControl {
  private readonly SensorPicker cpuTemp=new SensorPicker(true){Name="SetupCpuTemperature"},gpuTemp=new SensorPicker(true){Name="SetupGpuTemperature",GpuChannel=true};
  private readonly SensorPicker cpuBar=new SensorPicker(false){Name="SetupCpuBar"},gpuBar=new SensorPicker(false){Name="SetupGpuBar",GpuChannel=true};
  public event Action<string,string,string,string> ReviewRequested;
  public QuickSetupControl() {
   AutoSize=true;Dock=DockStyle.Top;var table=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=1};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(table);
   InfoPage.Paragraph(table,"Choose the readings for this profile. Bar sensors are used only by sensor-driven behaviors. Review the profile before activating it.");
   foreach(var pair in new[]{Tuple.Create("CPU temperature",cpuTemp),Tuple.Create("GPU temperature",gpuTemp),Tuple.Create("CPU bar sensor",cpuBar),Tuple.Create("GPU bar sensor",gpuBar)}) {
    InfoPage.Add(table,new Label{Text=pair.Item1,AutoSize=true});pair.Item2.Dock=DockStyle.Top;InfoPage.Add(table,pair.Item2);
   }
   var review=new Button{Name="ReviewSetup",Text="Review profile",AutoSize=true,AccessibleDescription="Load these sensor choices into the current profile draft. No settings are saved or sent until you use or save the profile."};review.Click+=(s,e)=>{if(ReviewRequested!=null)ReviewRequested(cpuTemp.SelectedId,gpuTemp.SelectedId,cpuBar.SelectedId,gpuBar.SelectedId);};InfoPage.Add(table,review);
  }
  public void SetProfile(Profile profile,SensorSnapshot snapshot) {UpdateCatalog(snapshot,true,false);cpuTemp.SelectedId=profile.Cpu.TemperatureId;gpuTemp.SelectedId=profile.Gpu.TemperatureId;cpuBar.SelectedId=profile.Cpu.Animation.SensorId;gpuBar.SelectedId=profile.Gpu.Animation.SensorId;}
  public void UpdateCatalog(SensorSnapshot snapshot,bool available,bool defer) {foreach(var picker in new[]{cpuTemp,gpuTemp,cpuBar,gpuBar}){picker.SetSnapshot(snapshot,defer);picker.SetAvailable(available);}}
 }
}
