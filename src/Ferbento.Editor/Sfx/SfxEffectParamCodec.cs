using Ferbento.Core.Audio;

namespace Ferbento.Editor.Sfx;

public static class SfxEffectParamCodec
{
  public static void Unpack(SfxEffect effect, int param, out int field0, out int field1, out int field2)
  {
    field0 = param & 0xFF;
    field1 = (param >> 8) & 0xFF;
    field2 = (param >> 16) & 0xFF;
    if (effect is SfxEffect.Arpeggio or SfxEffect.Arpeggio2)
    {
      field0 = param & 0xF;
      field1 = (param >> 4) & 0xF;
      field2 = (param >> 8) & 0xF;
      return;
    }

    if (effect is SfxEffect.FadeIn or SfxEffect.FadeOut)
      field1 = 0;
  }

  public static int Pack(SfxEffect effect, int field0, int field1, int field2 = 0) =>
    effect switch
    {
      SfxEffect.Slide or SfxEffect.Vibrato or SfxEffect.Drop =>
        ((field1 & 0xFF) << 8) | (field0 & 0xFF),
      SfxEffect.FadeIn or SfxEffect.FadeOut =>
        field0 & 0xFF,
      SfxEffect.Arpeggio or SfxEffect.Arpeggio2 =>
        ((field2 & 0xF) << 8) | ((field1 & 0xF) << 4) | (field0 & 0xF),
      _ => 0,
    };

  public static (string f0, string f1, string f2) FieldLabels(SfxEffect effect) =>
    effect switch
    {
      SfxEffect.Slide => ("Target note", "Speed", ""),
      SfxEffect.Vibrato => ("Depth", "Speed", ""),
      SfxEffect.Drop => ("Semitones", "Speed", ""),
      SfxEffect.FadeIn or SfxEffect.FadeOut => ("Speed", "", ""),
      SfxEffect.Arpeggio or SfxEffect.Arpeggio2 => ("+0", "+1", "+2"),
      _ => ("", "", ""),
    };
}
