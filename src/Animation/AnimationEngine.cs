using System;
namespace ZX6DisplayControl {
 public sealed class AnimationEngine {
  private readonly Random random;
  private AnimationSettings previousSettings;
  private int frame,position,playlistIndex;
  private double phase,playlistTime;
  private double? filtered;
  private bool wasPaused,levelInitialized;

  public AnimationEngine(Random random) {if(random==null) throw new ArgumentNullException("random");this.random=random;}
  public void Reset() {previousSettings=null;frame=0;position=0;phase=0;filtered=null;wasPaused=false;levelInitialized=false;playlistIndex=0;playlistTime=0;}

  public AnimationOutput Advance(AnimationSettings settings,double? sourceValue,double elapsedSeconds,bool paused) {
   if(settings==null || settings.Validate().Count>0) throw new ArgumentException("Invalid animation settings.","settings");
   if(!AnimationSettings.Finite(elapsedSeconds) || elapsedSeconds<0) throw new ArgumentOutOfRangeException("elapsedSeconds");
   bool changed=!settings.Equivalent(previousSettings);
   if(changed) {Reset();previousSettings=settings.Copy();frame=settings.Sequence==SequenceKind.Reverse?7:0;}
   var output=new AnimationOutput{Frame=frame,RawValue=sourceValue};
   if(settings.Mode==AnimationMode.Playlist && changed)frame=output.Frame=settings.Steps[0].Pattern==SequenceKind.Reverse?7:0;
   bool missing=settings.NeedsSource && (!sourceValue.HasValue || !AnimationSettings.Finite(sourceValue.Value));
   if(missing) {phase=0;output.SourceMissing=true;wasPaused=paused;return output;}

   double normalized=0;
   if(settings.NeedsSource) {
    double value=sourceValue.Value;
    if(!filtered.HasValue || settings.FilterSeconds==0) filtered=value;
    else filtered=filtered.Value+(value-filtered.Value)*(1-Math.Exp(-elapsedSeconds/settings.FilterSeconds));
    normalized=Normalize(filtered.Value,settings.InputMin,settings.InputMax,settings.Invert);
    output.FilteredValue=filtered;output.NormalizedValue=normalized;
    output.Clamped=filtered.Value<settings.InputMin || filtered.Value>settings.InputMax;
   }
   double fps=settings.RateMode==AnimationRateMode.Sensor?Frequency(normalized,settings.MinFps,settings.MaxFps):settings.FixedFps;
   if(settings.Mode==AnimationMode.Cycle) output.FramesPerSecond=fps;
   if(settings.Mode==AnimationMode.Playlist){output.FramesPerSecond=settings.Steps[playlistIndex].Fps;output.PlaylistStepNumber=playlistIndex+1;}
   if(paused && settings.Mode!=AnimationMode.Fixed) {phase=0;wasPaused=true;return output;}
   if(settings.Mode==AnimationMode.Fixed) frame=settings.FixedFrame;
   else if(settings.Mode==AnimationMode.SensorLevel) {
    // Choose the nearest visible segment. Truncation made the full bar require
    // an exact maximum, which a smoothed reading may never reach.
    int candidate=(int)Math.Floor(7*normalized+.5);
    double h=settings.HysteresisPercent/100;
    // A band wider than half a segment would put the endpoint threshold
    // outside 0..1. In that case use the endpoint bin's ordinary boundary.
    bool endpoint=(candidate==0 || candidate==7) && h>=.5/7.0;
    if(!levelInitialized || normalized<=0 || normalized>=1 || endpoint ||
       (candidate>frame && normalized>=(frame+.5)/7.0+h) ||
       (candidate<frame && normalized<=(frame-.5)/7.0-h)) frame=candidate;
    levelInitialized=true;
   } else {
    // A delayed worker skips missed time; it never replays a backlog to USB.
    double cycleTime=changed || wasPaused || elapsedSeconds>1?0:elapsedSeconds;
    SequenceKind sequence=settings.Sequence;
    if(settings.Mode==AnimationMode.Playlist) {
     playlistTime+=cycleTime;
     if(playlistTime>=settings.Steps[playlistIndex].Seconds) {
      playlistTime-=settings.Steps[playlistIndex].Seconds;playlistIndex=(playlistIndex+1)%settings.Steps.Count;
      phase=0;position=0;frame=settings.Steps[playlistIndex].Pattern==SequenceKind.Reverse?7:0;cycleTime=playlistTime;
     }
     sequence=settings.Steps[playlistIndex].Pattern;fps=settings.Steps[playlistIndex].Fps;
     output.FramesPerSecond=fps;output.PlaylistStepNumber=playlistIndex+1;
    }
    phase+=cycleTime*fps;
    int steps=(int)Math.Floor(phase+1e-9);phase=Math.Max(0,phase-steps);
    for(int i=0;i<steps;i++) {
     switch(sequence) {
      case SequenceKind.Forward:frame=(frame+1)%8;break;
      case SequenceKind.Reverse:frame=(frame+7)%8;break;
      case SequenceKind.PingPong:position=(position+1)%14;frame=position<=7?position:14-position;break;
      case SequenceKind.Random:frame=(frame+random.Next(1,8))%8;break;
     }
    }
   }
   wasPaused=false;output.Frame=frame;return output;
  }
  public static double Normalize(double value,double min,double max,bool invert) {
   if(!AnimationSettings.Finite(value) || !AnimationSettings.Finite(min) || !AnimationSettings.Finite(max) || min>=max || !AnimationSettings.Finite(max-min)) throw new ArgumentOutOfRangeException("value");
   double normalized=Math.Max(0.0,Math.Min(1.0,(value-min)/(max-min)));
   return invert?1-normalized:normalized;
  }
  public static double Frequency(double normalized,double minFps,double maxFps) {
   if(!AnimationSettings.Finite(normalized) || normalized<0 || normalized>1 || !AnimationSettings.Finite(minFps) || !AnimationSettings.Finite(maxFps) || minFps<1 || maxFps>4 || minFps>maxFps) throw new ArgumentOutOfRangeException("normalized");
   return minFps+normalized*(maxFps-minFps);
  }
 }
}
