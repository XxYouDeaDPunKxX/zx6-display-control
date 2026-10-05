using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace ZX6DisplayControl {
 public sealed class SensorValue {
  public string Id {get;private set;} public string Label {get;private set;} public string Kind {get;private set;}
  public string Unit {get;private set;} public string RawValue {get;private set;} public double? Number {get;private set;}
  public SensorValue(string id,string label,string kind,string unit,string rawValue,double? number) {Id=id;Label=label;Kind=kind;Unit=unit;RawValue=rawValue;Number=number;}
 }
 public sealed class SensorSnapshot {
  public IReadOnlyDictionary<string,SensorValue> Values {get;private set;} public long ReadAtMilliseconds {get;private set;}
  public SensorSnapshot(IDictionary<string,SensorValue> values,long at) {Values=new ReadOnlyDictionary<string,SensorValue>(new Dictionary<string,SensorValue>(values,StringComparer.Ordinal));ReadAtMilliseconds=at;}
 }
 public sealed class SensorReadResult {
  public SensorSnapshot Snapshot {get;private set;} public string ErrorCode {get;private set;} public string Message {get;private set;}
  public static SensorReadResult Success(SensorSnapshot snapshot) {return new SensorReadResult{Snapshot=snapshot};}
  public static SensorReadResult Failure(string code,string message) {return new SensorReadResult{ErrorCode=code,Message=message};}
 }
 public interface IAidaReader {SensorReadResult Read(long nowMilliseconds);}
}
