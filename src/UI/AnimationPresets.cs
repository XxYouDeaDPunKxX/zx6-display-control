using System;
namespace ZX6DisplayControl {
 public static class AnimationPresets {
  public static AnimationSettings Create(string name,AnimationMode mode,SequenceKind sequence,bool gpu,string temperatureId) {
   var value=new AnimationSettings{Mode=mode,Sequence=sequence};
   switch(name) {
    case "Slow":value.FixedFps=1;break;
    case "Normal":value.FixedFps=2;break;
    case "Fast":value.FixedFps=4;break;
    case "Follow usage":value.RateMode=AnimationRateMode.Sensor;value.SensorId=gpu?"SGPU1UTI":"SCPUUTI";value.FilterSeconds=2;break;
    case "Follow temperature":value.RateMode=AnimationRateMode.Sensor;value.SensorId=temperatureId;value.InputMin=30;value.InputMax=80;value.FilterSeconds=2;break;
    default:throw new ArgumentException("Preset unavailable.");
   }
   return value;
  }
  public static string Match(AnimationSettings value,bool gpu,string temperatureId) {
   if(!value.NeedsSource) return value.FixedFps==1?"Slow":value.FixedFps==2?"Normal":value.FixedFps==4?"Fast":"Custom";
   foreach(string name in new[]{"Follow usage","Follow temperature"}) {
    var preset=Create(name,value.Mode,value.Sequence,gpu,temperatureId);
    if(value.SensorId==preset.SensorId && value.InputMin==preset.InputMin && value.InputMax==preset.InputMax && value.FilterSeconds==preset.FilterSeconds && !value.Invert && (value.Mode!=AnimationMode.Cycle || (value.MinFps==1 && value.MaxFps==4)) && (value.Mode!=AnimationMode.SensorLevel || value.HysteresisPercent==2))return name;
   }
   return "Custom";
  }
 }
}
