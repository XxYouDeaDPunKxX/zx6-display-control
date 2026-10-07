using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
namespace ZX6DisplayControl {
 [DataContract] public enum AnimationMode {[EnumMember] Cycle,[EnumMember] SensorLevel,[EnumMember] Fixed,[EnumMember] Playlist}
 [DataContract] public enum SequenceKind {[EnumMember] Forward,[EnumMember] Reverse,[EnumMember] PingPong,[EnumMember] Random}
 [DataContract] public enum AnimationRateMode {[EnumMember] Fixed,[EnumMember] Sensor}
 [DataContract] public sealed class PlaylistStep {
  [DataMember] public SequenceKind Pattern {get;set;}
  [DataMember] public int Seconds {get;set;}
  [DataMember] public double Fps {get;set;}
  public PlaylistStep() {Seconds=10;Fps=2;}
  public PlaylistStep Copy() {return (PlaylistStep)MemberwiseClone();}
  public override string ToString() {return new[]{"Fill","Empty","Bounce","Random"}[(int)Pattern]+" · "+Seconds+" s · "+Fps.ToString("0.##")+" fps";}
 }
 [DataContract] public sealed class AnimationSettings {
  [DataMember] public AnimationMode Mode {get;set;}
  [DataMember] public SequenceKind Sequence {get;set;}
  [DataMember] public AnimationRateMode RateMode {get;set;}
  [DataMember] public string SensorId {get;set;}
  [DataMember] public double InputMin {get;set;}
  [DataMember] public double InputMax {get;set;}
  [DataMember] public double FixedFps {get;set;}
  [DataMember] public double MinFps {get;set;}
  [DataMember] public double MaxFps {get;set;}
  [DataMember] public double FilterSeconds {get;set;}
  [DataMember] public double HysteresisPercent {get;set;}
  [DataMember] public bool Invert {get;set;}
  [DataMember] public int FixedFrame {get;set;}
  [DataMember(EmitDefaultValue=false)] public List<PlaylistStep> Steps {get;set;}
  public AnimationSettings() {InputMax=100;FixedFps=2;MinFps=1;MaxFps=4;HysteresisPercent=2;}
  public bool NeedsSource {get{return Mode==AnimationMode.SensorLevel || (Mode==AnimationMode.Cycle && RateMode==AnimationRateMode.Sensor);}}
  public AnimationSettings Copy() {var copy=(AnimationSettings)MemberwiseClone();copy.Steps=Steps==null?null:Steps.Select(s=>s==null?null:s.Copy()).ToList();return copy;}
  public IReadOnlyList<string> Validate() {
   var errors=new List<string>();
   if(!Enum.IsDefined(typeof(AnimationMode),Mode) || !Enum.IsDefined(typeof(SequenceKind),Sequence) || !Enum.IsDefined(typeof(AnimationRateMode),RateMode)) errors.Add("Invalid animation mode.");
   if(!Finite(InputMin) || !Finite(InputMax) || InputMin>=InputMax || !Finite(InputMax-InputMin)) errors.Add("Sensor minimum must be lower than the maximum.");
   if(!ValidRate(FixedFps) || !ValidRate(MinFps) || !ValidRate(MaxFps) || MinFps>MaxFps) errors.Add("Speed must be between 1 and 4 fps, with minimum no higher than maximum.");
   if(FilterSeconds!=0 && FilterSeconds!=1 && FilterSeconds!=2 && FilterSeconds!=5) errors.Add("Response time must be off, 1, 2 or 5 seconds.");
   if(!Finite(HysteresisPercent) || HysteresisPercent<0 || HysteresisPercent>10) errors.Add("Level hysteresis must be between 0 and 10%.");
   if(FixedFrame<0 || FixedFrame>7) errors.Add("Level must be between 0 and 7.");
   if(NeedsSource && string.IsNullOrWhiteSpace(SensorId)) errors.Add("Choose an animation control sensor.");
   if(Mode==AnimationMode.Playlist && (Steps==null || Steps.Count==0))errors.Add("Add at least one playlist effect.");
   if(Steps!=null && (Steps.Count>16 || Steps.Any(s=>s==null || !Enum.IsDefined(typeof(SequenceKind),s.Pattern) || s.Seconds<1 || s.Seconds>600 || !ValidRate(s.Fps))))errors.Add("Use up to 16 playlist effects, each lasting 1–600 seconds at 1–4 fps.");
   return errors.AsReadOnly();
  }
  internal static bool Finite(double value) {return !double.IsNaN(value) && !double.IsInfinity(value);}
  private static bool ValidRate(double value) {return Finite(value) && value>=1 && value<=4;}
  internal bool Equivalent(AnimationSettings other) {
   return other!=null && Mode==other.Mode && Sequence==other.Sequence && RateMode==other.RateMode && SensorId==other.SensorId && InputMin==other.InputMin && InputMax==other.InputMax && FixedFps==other.FixedFps && MinFps==other.MinFps && MaxFps==other.MaxFps && FilterSeconds==other.FilterSeconds && HysteresisPercent==other.HysteresisPercent && Invert==other.Invert && FixedFrame==other.FixedFrame && SameSteps(Steps,other.Steps);
  }
  private static bool SameSteps(List<PlaylistStep> a,List<PlaylistStep> b) {a=a??new List<PlaylistStep>();b=b??new List<PlaylistStep>();return a.Count==b.Count && a.Zip(b,(x,y)=>x!=null && y!=null && x.Pattern==y.Pattern && x.Seconds==y.Seconds && x.Fps==y.Fps).All(v=>v);}
 }
 public sealed class AnimationOutput {
  public int Frame {get;internal set;}
  public double? RawValue {get;internal set;} public double? FilteredValue {get;internal set;}
  public double? NormalizedValue {get;internal set;} public double? FramesPerSecond {get;internal set;}
  public bool SourceMissing {get;internal set;} public bool Clamped {get;internal set;}
  public bool HeldByHysteresis {get;internal set;}
  public int? PlaylistStepNumber {get;internal set;}
 }
}
