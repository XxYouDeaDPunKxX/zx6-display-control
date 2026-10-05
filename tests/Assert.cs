using System;
using System.Linq;
namespace ZX6DisplayControl.Tests {
 [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
 public static class Assert {
  public static void True(bool value,string message="Expected true") { if(!value) throw new Exception(message); }
  public static void Equal<T>(T expected,T actual) { if(!object.Equals(expected,actual)) throw new Exception("Expected "+expected+", actual "+actual); }
  public static void SequenceEqual(byte[] expected,byte[] actual) { True(expected!=null && actual!=null && expected.SequenceEqual(actual),"Expected "+BitConverter.ToString(expected)+", actual "+(actual==null?"null":BitConverter.ToString(actual))); }
  public static void Near(double expected,double actual,double tolerance=1e-9) { True(!double.IsNaN(actual) && Math.Abs(expected-actual)<=tolerance,"Expected "+expected+", actual "+actual); }
  public static void Throws<T>(Action action) where T:Exception { try { action(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name); }
 }
}
