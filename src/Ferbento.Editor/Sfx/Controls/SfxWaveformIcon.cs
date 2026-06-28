using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxWaveformIcon : Control
{
  public static readonly StyledProperty<int> InstrumentProperty =
    AvaloniaProperty.Register<SfxWaveformIcon, int>(nameof(Instrument));

  public int Instrument
  {
    get => GetValue(InstrumentProperty);
    set => SetValue(InstrumentProperty, value);
  }

  static SfxWaveformIcon()
  {
    InstrumentProperty.Changed.AddClassHandler<SfxWaveformIcon>((c, _) => c.InvalidateVisual());
  }

  public override void Render(DrawingContext ctx)
  {
    base.Render(ctx);
    var rect = new Rect(Bounds.Size);
    SfxWaveformDrawing.Draw(ctx, rect, Instrument & 7, SfxTheme.WaveformColor(Instrument & 7, 7));
  }
}
