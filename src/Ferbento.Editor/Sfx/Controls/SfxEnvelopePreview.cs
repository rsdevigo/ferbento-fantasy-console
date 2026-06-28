using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ferbento.Editor.Sfx;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxEnvelopePreview : Control
{
  private SfxEditorController? _ctrl;

  public void Bind(SfxEditorController controller)
  {
    _ctrl = controller;
    InvalidateVisual();
  }

  public override void Render(DrawingContext ctx)
  {
    base.Render(ctx);
    var bounds = Bounds;
    ctx.FillRectangle(new SolidColorBrush(Color.Parse("#141414")), bounds);

    if (_ctrl == null) return;

    var notes = _ctrl.Document.Notes;
    int count = _ctrl.NotesPerSfx;
    if (count <= 0) return;

    var points = new List<Point>();
    for (int i = 0; i < count; i++)
    {
      double x = bounds.X + (i + 0.5) / count * bounds.Width;
      double vol = notes[i].IsEmpty ? 0 : notes[i].Volume / 7.0;
      double y = bounds.Bottom - vol * (bounds.Height - 4) - 2;
      points.Add(new Point(x, y));
    }

    if (points.Count < 2) return;

    var geo = new StreamGeometry();
    using (var g = geo.Open())
    {
      g.BeginFigure(points[0], false);
      for (int i = 1; i < points.Count; i++)
        g.LineTo(points[i]);
    }

    ctx.DrawGeometry(null, new Pen(new SolidColorBrush(SfxTheme.AccentNote), 1.5), geo);
  }
}
