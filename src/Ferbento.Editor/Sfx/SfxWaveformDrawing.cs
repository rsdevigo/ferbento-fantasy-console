using Avalonia;
using Avalonia.Media;

namespace Ferbento.Editor.Sfx;

public static class SfxWaveformDrawing
{
  public static void Draw(DrawingContext ctx, Rect rect, int instrument, Color color)
  {
    double mid = rect.Center.Y;
    var pen = new Pen(new SolidColorBrush(color), 1.2);
    double w = Math.Max(4, rect.Width - 4);
    double x0 = rect.X + 2;

    switch (instrument & 7)
    {
      case 0:
      case 6:
        ctx.DrawLine(pen, new Point(x0, mid + 3), new Point(x0 + w * 0.25, mid - 3));
        ctx.DrawLine(pen, new Point(x0 + w * 0.25, mid - 3), new Point(x0 + w * 0.5, mid + 3));
        ctx.DrawLine(pen, new Point(x0 + w * 0.5, mid + 3), new Point(x0 + w * 0.75, mid - 3));
        if (instrument == 6)
          ctx.DrawLine(pen, new Point(x0 + w * 0.75, mid - 3), new Point(x0 + w, mid + 3));
        break;
      case 1:
        ctx.DrawLine(pen, new Point(x0, mid + 4), new Point(x0 + w, mid - 4));
        break;
      case 2:
      case 4:
      case 5:
        double duty = instrument == 5 ? 0.15 : instrument == 4 ? 0.25 : 0.5;
        ctx.DrawLine(pen, new Point(x0, mid + 3), new Point(x0 + w * duty, mid + 3));
        ctx.DrawLine(pen, new Point(x0 + w * duty, mid + 3), new Point(x0 + w * duty, mid - 3));
        ctx.DrawLine(pen, new Point(x0 + w * duty, mid - 3), new Point(x0 + w, mid - 3));
        break;
      default:
        for (double i = 0; i < w; i += instrument == 7 ? 2 : 3)
        {
          double h = instrument == 3 ? 3 : 2;
          ctx.DrawLine(pen, new Point(x0 + i, mid - h), new Point(x0 + i + 1, mid + h));
        }
        break;
    }
  }
}
