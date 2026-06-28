using Avalonia.Media;
using Ferbento.Core.Audio;

namespace Ferbento.Editor.Sfx;

public static class SfxTheme
{
  public static readonly Color Background = Color.Parse("#1E1E1E");
  public static readonly Color CanvasBackground = Color.Parse("#0E0E0E");
  public static readonly Color GridLine = Color.Parse("#1A1A2E");
  public static readonly Color HeaderBg = Color.Parse("#1E1E1E");
  public static readonly Color Playhead = Color.Parse("#44FF44");
  public static readonly Color PlayheadBg = Color.FromArgb(60, 68, 170, 68);
  public static readonly Color Hover = Color.Parse("#334466");
  public static readonly Color SelectionOutline = Colors.White;
  public static readonly Color LoopRegion = Color.FromArgb(26, 255, 180, 60);
  public static readonly Color CursorFocus = Colors.White;
  public static readonly Color MutedText = Color.Parse("#AAAAAA");
  public static readonly Color AccentNote = Color.Parse("#FFB347");

  public static readonly IReadOnlyList<string> WaveformNames =
    new[] { "triangle", "saw", "square", "noise", "pulse25", "pulse12", "tri2", "noise2" };

  public static readonly IReadOnlyList<string> EffectNames =
    new[] { "none", "slide", "vibrato", "drop", "fadein", "fadeout", "arpeg", "arpeg2" };

  private static readonly Color[] WaveformColors =
  {
    Color.Parse("#5588FF"),
    Color.Parse("#FF7755"),
    Color.Parse("#55FF88"),
    Color.Parse("#AA88FF"),
    Color.Parse("#FFDD44"),
    Color.Parse("#FF9900"),
    Color.Parse("#44DDFF"),
    Color.Parse("#DD44AA"),
  };

  public static Color WaveformColor(int instrument, int volume)
  {
    int idx = Math.Clamp(instrument & 7, 0, 7);
    var baseColor = WaveformColors[idx];
    double t = volume <= 0 ? 0.2 : volume / 7.0;
    return Color.FromArgb(
      255,
      (byte)(baseColor.R * t),
      (byte)(baseColor.G * t),
      (byte)(baseColor.B * t));
  }

  public static Color WaveformColor(SfxNote note) =>
    note.IsEmpty ? CanvasBackground : WaveformColor(note.Instrument, note.Volume);
}
