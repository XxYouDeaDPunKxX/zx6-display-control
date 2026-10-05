using System;
namespace ZX6DisplayControl.Tests {
 public static class PacketCodecTests {
  [Test] public static void PacketCodec_ObservedPacket() {
   Assert.SequenceEqual(new byte[]{0,2,192,0,0,169,3,3,4,4,0,7,0,0,0,0,0,0,0,0},PacketCodec.Encode(33,44,0,7));
   Assert.SequenceEqual(new byte[]{0,0,0,0,0,169,0,0,0,0,7,0,0,0,0,0,0,0,0,0},PacketCodec.Encode(0,0,7,0));
  }
  [Test] public static void PacketCodec_RejectsUnrepresentableValues() {
   foreach(double bad in new[]{-1,100,99.9,double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {
    Assert.Throws<ArgumentOutOfRangeException>(()=>PacketCodec.Encode(bad,44,0,7));
    Assert.Throws<ArgumentOutOfRangeException>(()=>PacketCodec.Encode(33,bad,0,7));
   }
   foreach(int bad in new[]{-1,8}) {
    Assert.Throws<ArgumentOutOfRangeException>(()=>PacketCodec.Encode(33,44,bad,7));
    Assert.Throws<ArgumentOutOfRangeException>(()=>PacketCodec.Encode(33,44,0,bad));
   }
  }
  [Test] public static void PacketCodec_DecimalsAndControls() {
   Assert.Equal((byte)3,PacketCodec.Encode(33.9,44,0,7)[7]);
   Assert.Equal((byte)9,PacketCodec.Encode(99,99,0,7)[9]);
   foreach(byte command in new byte[]{108,109,255}) Assert.SequenceEqual(new byte[]{0,0,0,0,0,command},PacketCodec.Control(command));
   Assert.Throws<ArgumentOutOfRangeException>(()=>PacketCodec.Control(110));
  }
 }
}
