namespace Ferbento.Editor.Sprites;

public static class PixelDrawing
{
    public static void SetPixelSafe(SpriteDocument doc, int localX, int localY, int color, Selection? selection = null)
    {
        if (selection?.HasPixelSelection == true && !selection.ContainsLocalPixel(localX, localY))
            return;
        if (localX >= 0 && localY >= 0 && localX < doc.ViewWidth && localY < doc.ViewHeight)
            doc.SetLocalPixel(localX, localY, color);
    }

    public static void PaintBrush(SpriteDocument doc, int localX, int localY, int color, int brushSize, Selection? selection = null)
    {
        var size = Math.Clamp(brushSize, 1, 8);
        for (var dy = 0; dy < size; dy++)
        for (var dx = 0; dx < size; dx++)
            SetPixelSafe(doc, localX + dx, localY + dy, color, selection);
    }

    public static void DrawLine(SpriteDocument doc, int x0, int y0, int x1, int y1, int color, Selection? selection = null)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            SetPixelSafe(doc, x0, y0, color, selection);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    public static (int x, int y) SnapLine(int x0, int y0, int x1, int y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        if (dx == 0 || dy == 0) return (x1, y1);
        if (Math.Abs(dx) == Math.Abs(dy)) return (x1, y1);
        if (Math.Abs(dx) > Math.Abs(dy))
            return (x1, y0);
        return (x0, y1);
    }

    public static void DrawRect(SpriteDocument doc, int x0, int y0, int x1, int y1, int color, bool filled, Selection? selection = null)
    {
        var left = Math.Min(x0, x1);
        var right = Math.Max(x0, x1);
        var top = Math.Min(y0, y1);
        var bottom = Math.Max(y0, y1);

        if (filled)
        {
            for (var y = top; y <= bottom; y++)
            for (var x = left; x <= right; x++)
                SetPixelSafe(doc, x, y, color, selection);
        }
        else
        {
            for (var x = left; x <= right; x++)
            {
                SetPixelSafe(doc, x, top, color, selection);
                SetPixelSafe(doc, x, bottom, color, selection);
            }
            for (var y = top; y <= bottom; y++)
            {
                SetPixelSafe(doc, left, y, color, selection);
                SetPixelSafe(doc, right, y, color, selection);
            }
        }
    }

    public static void DrawCircle(SpriteDocument doc, int cx, int cy, int x1, int y1, int color, bool filled, bool perfect, Selection? selection = null)
    {
        var rx = Math.Abs(x1 - cx);
        var ry = Math.Abs(y1 - cy);
        if (perfect) ry = rx;

        if (rx == 0 && ry == 0)
        {
            SetPixelSafe(doc, cx, cy, color, selection);
            return;
        }

        if (filled)
        {
            for (var y = cy - ry; y <= cy + ry; y++)
            for (var x = cx - rx; x <= cx + rx; x++)
            {
                var dx = (double)(x - cx) / Math.Max(1, rx);
                var dy = (double)(y - cy) / Math.Max(1, ry);
                if (dx * dx + dy * dy <= 1.0)
                    SetPixelSafe(doc, x, y, color, selection);
            }
        }
        else
        {
            var x = 0;
            var y = ry;
            var d1 = ry * ry - rx * rx * ry + rx * rx / 4;
            var dx = 2 * ry * ry * x;
            var dy = 2 * rx * rx * y;

            while (dx < dy)
            {
                PlotEllipse(doc, cx, cy, x, y, color, selection);
                if (d1 < 0) { x++; dx += 2 * ry * ry; d1 += dx + ry * ry; }
                else { x++; y--; dx += 2 * ry * ry; dy -= 2 * rx * rx; d1 += dx - dy + ry * ry; }
            }

            var d2 = ry * ry * (x + 0.5) * (x + 0.5) + rx * rx * (y - 1) * (y - 1) - rx * rx * ry * ry;
            while (y >= 0)
            {
                PlotEllipse(doc, cx, cy, x, y, color, selection);
                if (d2 > 0) { y--; dy -= 2 * rx * rx; d2 += rx * rx - dy; }
                else { y--; x++; dy -= 2 * rx * rx; dx += 2 * ry * ry; d2 += dx - dy + rx * rx; }
            }
        }
    }

    private static void PlotEllipse(SpriteDocument doc, int cx, int cy, int x, int y, int color, Selection? selection)
    {
        SetPixelSafe(doc, cx + x, cy + y, color, selection);
        SetPixelSafe(doc, cx - x, cy + y, color, selection);
        SetPixelSafe(doc, cx + x, cy - y, color, selection);
        SetPixelSafe(doc, cx - x, cy - y, color, selection);
    }

    public static void FloodFill(SpriteDocument doc, int localX, int localY, int newColor, Selection? selection = null)
    {
        var target = doc.GetLocalPixel(localX, localY);
        if (target == newColor) return;

        var stack = new Stack<(int x, int y)>();
        stack.Push((localX, localY));
        var w = doc.ViewWidth;
        var h = doc.ViewHeight;

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || x >= w || y < 0 || y >= h) continue;
            if (selection?.HasPixelSelection == true && !selection.ContainsLocalPixel(x, y)) continue;
            if (doc.GetLocalPixel(x, y) != target) continue;
            doc.SetLocalPixel(x, y, newColor);
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }
    }

    public static void InterpolateStroke(SpriteDocument doc, int fromX, int fromY, int toX, int toY, int color, Selection? selection = null, int brushSize = 1)
    {
        var dx = Math.Abs(toX - fromX);
        var dy = Math.Abs(toY - fromY);
        var sx = fromX < toX ? 1 : -1;
        var sy = fromY < toY ? 1 : -1;
        var err = dx - dy;
        var x = fromX;
        var y = fromY;

        while (true)
        {
            PaintBrush(doc, x, y, color, brushSize, selection);
            if (x == toX && y == toY) break;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    public static byte[] FlipHorizontal(byte[] data, int tileSize)
    {
        var result = (byte[])data.Clone();
        for (var y = 0; y < tileSize; y++)
        for (var x = 0; x < tileSize / 2; x++)
            (result[y * tileSize + x], result[y * tileSize + (tileSize - 1 - x)]) =
                (result[y * tileSize + (tileSize - 1 - x)], result[y * tileSize + x]);
        return result;
    }

    public static byte[] FlipVertical(byte[] data, int tileSize)
    {
        var result = (byte[])data.Clone();
        for (var y = 0; y < tileSize / 2; y++)
        for (var x = 0; x < tileSize; x++)
            (result[y * tileSize + x], result[(tileSize - 1 - y) * tileSize + x]) =
                (result[(tileSize - 1 - y) * tileSize + x], result[y * tileSize + x]);
        return result;
    }

    public static byte[] Rotate90(byte[] data, int tileSize)
    {
        var dst = new byte[data.Length];
        for (var y = 0; y < tileSize; y++)
        for (var x = 0; x < tileSize; x++)
            dst[x * tileSize + (tileSize - 1 - y)] = data[y * tileSize + x];
        return dst;
    }

    public static byte[] ShiftCircular(byte[] data, int tileSize, int dx, int dy)
    {
        var result = new byte[data.Length];
        for (var y = 0; y < tileSize; y++)
        for (var x = 0; x < tileSize; x++)
        {
            var srcX = ((x - dx) % tileSize + tileSize) % tileSize;
            var srcY = ((y - dy) % tileSize + tileSize) % tileSize;
            result[y * tileSize + x] = data[srcY * tileSize + srcX];
        }
        return result;
    }

    public static byte[] ShiftRegion(byte[] data, int width, int height, int dx, int dy, int fillColor = 0)
    {
        var result = new byte[data.Length];
        Array.Fill(result, (byte)fillColor);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var destX = x + dx;
            var destY = y + dy;
            if (destX >= 0 && destX < width && destY >= 0 && destY < height)
                result[destY * width + destX] = data[y * width + x];
        }
        return result;
    }

    public static void ReplaceColor(SpriteDocument doc, int oldColor, int newColor, Selection? selection = null)
    {
        for (var y = 0; y < doc.ViewHeight; y++)
        for (var x = 0; x < doc.ViewWidth; x++)
        {
            if (selection?.HasPixelSelection == true && !selection.ContainsLocalPixel(x, y)) continue;
            if (doc.GetLocalPixel(x, y) == oldColor)
                doc.SetLocalPixel(x, y, newColor);
        }
    }
}
