using Ferbento.Core.Chips;

namespace Ferbento.Core.Rendering;

public sealed class Renderer
{
    private readonly PixelBuffer _buffer;
    private readonly DrawState _state;

    public Renderer(PixelBuffer buffer, DrawState state)
    {
        _buffer = buffer;
        _state = state;
    }

    public PixelBuffer Buffer => _buffer;
    public DrawState State => _state;

    public void Cls(int color = 0)
    {
        var c = _state.DrawPalette[color & 0xF];
        _buffer.Clear(c);
    }

    public void PSet(int x, int y, int? color = null)
    {
        x -= _state.CameraX;
        y -= _state.CameraY;
        if (!InClip(x, y)) return;
        var c = _state.DrawPalette[(color ?? _state.PenColor) & 0xF];
        if (!ShouldDraw(x, y)) return;
        _buffer.SetPixel(x, y, c);
    }

    public int PGet(int x, int y)
    {
        x -= _state.CameraX;
        y -= _state.CameraY;
        if (x < 0 || y < 0 || x >= _buffer.Width || y >= _buffer.Height) return 0;
        return _buffer.GetPixel(x, y);
    }

    public void Line(int x0, int y0, int x1, int y1, int? color = null)
    {
        var c = (color ?? _state.PenColor) & 0xF;
        var dx = System.Math.Abs(x1 - x0);
        var dy = System.Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            PSet(x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            var e2 = err * 2;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    public void Rect(int x0, int y0, int x1, int y1, int? color = null)
    {
        Line(x0, y0, x1, y0, color);
        Line(x1, y0, x1, y1, color);
        Line(x1, y1, x0, y1, color);
        Line(x0, y1, x0, y0, color);
    }

    public void RectFill(int x0, int y0, int x1, int y1, int? color = null)
    {
        if (x0 > x1) (x0, x1) = (x1, x0);
        if (y0 > y1) (y0, y1) = (y1, y0);
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                PSet(x, y, color);
    }

    public void Circ(int cx, int cy, int r, int? color = null)
    {
        var x = 0;
        var y = r;
        var d = 3 - 2 * r;
        while (x <= y)
        {
            PlotCircle(cx, cy, x, y, color);
            if (d < 0) d += 4 * x + 6;
            else { d += 4 * (x - y) + 10; y--; }
            x++;
        }
    }

    public void CircFill(int cx, int cy, int r, int? color = null)
    {
        for (var y = -r; y <= r; y++)
            for (var x = -r; x <= r; x++)
                if (x * x + y * y <= r * r)
                    PSet(cx + x, cy + y, color);
    }

    private void PlotCircle(int cx, int cy, int x, int y, int? color)
    {
        PSet(cx + x, cy + y, color);
        PSet(cx - x, cy + y, color);
        PSet(cx + x, cy - y, color);
        PSet(cx - x, cy - y, color);
        PSet(cx + y, cy + x, color);
        PSet(cx - y, cy + x, color);
        PSet(cx + y, cy - x, color);
        PSet(cx - y, cy - x, color);
    }

    private bool InClip(int x, int y)
    {
        if (!_state.ClipEnabled) return true;
        return x >= _state.ClipX && y >= _state.ClipY
            && x < _state.ClipX + _state.ClipW
            && y < _state.ClipY + _state.ClipH;
    }

    private bool ShouldDraw(int x, int y)
    {
        if ((_state.FillPattern & 0x8000) != 0) return true;
        var bit = ((_state.FillPattern >> ((x & 3) + (y & 3) * 4)) & 1) != 0;
        return bit;
    }
}
