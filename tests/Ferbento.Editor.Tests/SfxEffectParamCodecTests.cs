using Ferbento.Core.Audio;
using Ferbento.Editor.Sfx;

namespace Ferbento.Editor.Tests;

public class SfxEffectParamCodecTests
{
  [Fact]
  public void Slide_PacksTargetAndSpeed()
  {
    int packed = SfxEffectParamCodec.Pack(SfxEffect.Slide, 60, 80);
    SfxEffectParamCodec.Unpack(SfxEffect.Slide, packed, out int target, out int speed, out _);
    Assert.Equal(60, target);
    Assert.Equal(80, speed);
  }

  [Fact]
  public void Arpeggio_PacksThreeOffsets()
  {
    int packed = SfxEffectParamCodec.Pack(SfxEffect.Arpeggio, 2, 4, 7);
    SfxEffectParamCodec.Unpack(SfxEffect.Arpeggio, packed, out int o0, out int o1, out int o2);
    Assert.Equal(2, o0);
    Assert.Equal(4, o1);
    Assert.Equal(7, o2);
  }

  [Fact]
  public void DefaultParam_ReturnsUsableValues()
  {
    Assert.True(SfxEffectParamCodec.DefaultParam(SfxEffect.Vibrato) > 0);
    Assert.True(SfxEffectParamCodec.DefaultParam(SfxEffect.Arpeggio) > 0);
  }
}
