using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ferbento.Core.Audio;
using Ferbento.Editor.Sfx;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxPropertyStrip : Control
{
  private SfxEditorController? _ctrl;
  private const int RowH = 18;
  private const int PianoW = 32;

  public void Bind(SfxEditorController controller)
  {
    _ctrl = controller;
    InvalidateMeasure();
    InvalidateVisual();
  }

  protected override Size MeasureOverride(Size availableSize)
  {
    if (_ctrl == null) return new Size(0, RowH * 3);
    int w = PianoW + _ctrl.NotesPerSfx * _ctrl.CellWidth;
    return new Size(w, RowH * 3 + 4);
  }

  public override void Render(DrawingContext ctx)
  {
    base.Render(ctx);
    if (_ctrl == null) return;

    int steps = _ctrl.NotesPerSfx;
    int cellW = _ctrl.CellWidth;
    double y = 0;

    DrawRow(ctx, ref y, steps, cellW, DrawWaveformGlyph);
    DrawRow(ctx, ref y, steps, cellW, DrawVolumeBar);
    DrawRow(ctx, ref y, steps, cellW, DrawEffectBadge);
  }

  private void DrawRow(DrawingContext ctx, ref double y, int steps, int cellW, Action<DrawingContext, Rect, int> drawCell)
  {
    for (int step = 0; step < steps; step++)
    {
      double x = PianoW + step * cellW;
      var rect = new Rect(x, y, cellW, RowH);
      ctx.FillRectangle(new SolidColorBrush(SfxTheme.CanvasBackground), rect);
      drawCell(ctx, rect, step);
      ctx.DrawRectangle(new Pen(new SolidColorBrush(SfxTheme.GridLine), 0.3), rect);
    }

    y += RowH;
  }

  private void DrawWaveformGlyph(DrawingContext ctx, Rect rect, int step)
  {
    var note = _ctrl!.GetNote(step);
    if (note.IsEmpty) return;
    if (note.CustomInst)
    {
      var ft = new FormattedText(
        note.Instrument.ToString(CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        9,
        new SolidColorBrush(SfxTheme.CustomInstAccent));
      ctx.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + 3));
      return;
    }

    SfxWaveformDrawing.Draw(ctx, rect, note.Instrument, SfxTheme.WaveformColor(note));
  }

  private void DrawVolumeBar(DrawingContext ctx, Rect rect, int step)
  {
    var note = _ctrl!.GetNote(step);
    if (note.IsEmpty) return;
    double h = (note.Volume / 7.0) * (rect.Height - 4);
    var bar = new Rect(rect.X + 4, rect.Bottom - 2 - h, rect.Width - 8, h);
    ctx.FillRectangle(new SolidColorBrush(SfxTheme.WaveformColor(note)), bar);
  }

  private void DrawEffectBadge(DrawingContext ctx, Rect rect, int step)
  {
    var note = _ctrl!.GetNote(step);
    if (note.IsEmpty || note.Effect == SfxEffect.None) return;
    string label = note.Effect switch
    {
      SfxEffect.Slide => "S",
      SfxEffect.Vibrato => "V",
      SfxEffect.Drop => "D",
      SfxEffect.FadeIn => "FI",
      SfxEffect.FadeOut => "FO",
      SfxEffect.Arpeggio => "A",
      SfxEffect.Arpeggio2 => "A2",
      _ => "",
    };
    if (label.Length == 0) return;
    var ft = new FormattedText(
      label,
      CultureInfo.InvariantCulture,
      FlowDirection.LeftToRight,
      Typeface.Default,
      8,
      Brushes.White);
    ctx.DrawText(ft, new Point(rect.X + 2, rect.Y + 4));
    if (note.EffectParam > 0)
    {
      var paramFt = new FormattedText(
        note.EffectParam.ToString(CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        7,
        new SolidColorBrush(SfxTheme.MutedText));
      ctx.DrawText(paramFt, new Point(rect.Right - paramFt.Width - 2, rect.Bottom - paramFt.Height - 1));
    }
  }
}
