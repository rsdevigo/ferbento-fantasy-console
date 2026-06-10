using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class GraphicsApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        var bus = ctx.Bus;
        var renderer = bus.Display.Renderer;
        var state = bus.Display.DrawState;

        env.Define("cls", ScriptValue.Native(args =>
        {
            var col = args.Length > 0 ? args[0].AsInt() : 0;
            renderer.Cls(col);
            return ScriptValue.Nil;
        }));

        env.Define("pset", ScriptValue.Native(args =>
        {
            var x = args.Length > 0 ? args[0].AsInt() : 0;
            var y = args.Length > 1 ? args[1].AsInt() : 0;
            var col = args.Length > 2 ? args[2].AsInt() : (int?)null;
            renderer.PSet(x, y, col);
            return ScriptValue.Nil;
        }));

        env.Define("pget", ScriptValue.Native(args =>
        {
            var x = args.Length > 0 ? args[0].AsInt() : 0;
            var y = args.Length > 1 ? args[1].AsInt() : 0;
            return ScriptValue.Number(renderer.PGet(x, y));
        }));

        env.Define("color", ScriptValue.Native(args =>
        {
            if (args.Length > 0) state.PenColor = args[0].AsInt() & 0xF;
            return ScriptValue.Number(state.PenColor);
        }));

        env.Define("line", ScriptValue.Native(args =>
        {
            renderer.Line(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : 0,
                args.Length > 3 ? args[3].AsInt() : 0,
                args.Length > 4 ? args[4].AsInt() : null);
            return ScriptValue.Nil;
        }));

        env.Define("rect", ScriptValue.Native(args =>
        {
            renderer.Rect(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : 0,
                args.Length > 3 ? args[3].AsInt() : 0,
                args.Length > 4 ? args[4].AsInt() : null);
            return ScriptValue.Nil;
        }));

        env.Define("rectfill", ScriptValue.Native(args =>
        {
            renderer.RectFill(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : 0,
                args.Length > 3 ? args[3].AsInt() : 0,
                args.Length > 4 ? args[4].AsInt() : null);
            return ScriptValue.Nil;
        }));

        env.Define("circ", ScriptValue.Native(args =>
        {
            renderer.Circ(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : 0,
                args.Length > 3 ? args[3].AsInt() : null);
            return ScriptValue.Nil;
        }));

        env.Define("circfill", ScriptValue.Native(args =>
        {
            renderer.CircFill(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : 0,
                args.Length > 3 ? args[3].AsInt() : null);
            return ScriptValue.Nil;
        }));

        env.Define("sget", ScriptValue.Native(args =>
            ScriptValue.Number(bus.Sprite.SGet(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0))));

        env.Define("sset", ScriptValue.Native(args =>
        {
            bus.Sprite.SSet(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : state.PenColor);
            return ScriptValue.Nil;
        }));

        env.Define("fget", ScriptValue.Native(args =>
            ScriptValue.Number(bus.Sprite.FGet(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : -1))));

        env.Define("fset", ScriptValue.Native(args =>
        {
            bus.Sprite.FSet(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : -1,
                args.Length > 2 ? args[2].AsInt() : 0);
            return ScriptValue.Nil;
        }));

        env.Define("mget", ScriptValue.Native(args =>
            ScriptValue.Number(bus.Tilemap.MGet(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0))));

        env.Define("mset", ScriptValue.Native(args =>
        {
            bus.Tilemap.MSet(
                args.Length > 0 ? args[0].AsInt() : 0,
                args.Length > 1 ? args[1].AsInt() : 0,
                args.Length > 2 ? args[2].AsInt() : 0);
            return ScriptValue.Nil;
        }));

        env.Define("spr", ScriptValue.Native(args => { DrawSprite(bus, args); return ScriptValue.Nil; }));
        env.Define("sspr", ScriptValue.Native(args => { DrawSubSprite(bus, args); return ScriptValue.Nil; }));
        env.Define("map", ScriptValue.Native(args => { DrawMap(bus, args); return ScriptValue.Nil; }));

        env.Define("pal", ScriptValue.Native(args =>
        {
            var c0 = args.Length > 0 ? args[0].AsInt() & 0xF : 0;
            var c1 = args.Length > 1 ? args[1].AsInt() & 0xF : c0;
            var p = args.Length > 2 ? args[2].AsInt() : 0;
            var palette = p == 1 ? state.ScreenPalette : state.DrawPalette;
            palette[c0] = c1;
            return ScriptValue.Nil;
        }));

        env.Define("palt", ScriptValue.Native(args =>
        {
            var col = args.Length > 0 ? args[0].AsInt() & 0xF : 0;
            var t = args.Length <= 1 || args[1].IsTruthy();
            state.PaletteTransparent[col] = t;
            return ScriptValue.Nil;
        }));

        env.Define("camera", ScriptValue.Native(args =>
        {
            if (args.Length >= 2)
            {
                state.CameraX = args[0].AsInt();
                state.CameraY = args[1].AsInt();
            }
            else
            {
                state.CameraX = 0;
                state.CameraY = 0;
            }
            return ScriptValue.Nil;
        }));

        env.Define("clip", ScriptValue.Native(args =>
        {
            if (args.Length >= 4)
            {
                state.ClipX = args[0].AsInt();
                state.ClipY = args[1].AsInt();
                state.ClipW = args[2].AsInt();
                state.ClipH = args[3].AsInt();
                state.ClipEnabled = true;
            }
            else
            {
                state.ClipEnabled = false;
            }
            return ScriptValue.Nil;
        }));

        env.Define("cursor", ScriptValue.Native(args =>
        {
            state.CursorX = args.Length > 0 ? args[0].AsInt() : 0;
            state.CursorY = args.Length > 1 ? args[1].AsInt() : 0;
            return ScriptValue.Nil;
        }));

        env.Define("fillp", ScriptValue.Native(args =>
        {
            state.FillPattern = args.Length > 0 ? (ushort)(args[0].AsInt() & 0xFFFF) : (ushort)0xFFFF;
            return ScriptValue.Nil;
        }));

        env.Define("flip", ScriptValue.Native(_ =>
        {
            ctx.FlipCalled = true;
            bus.Display.Flip();
            return ScriptValue.Nil;
        }));
    }

    private static void DrawSprite(Core.Chips.ChipBus bus, ScriptValue[] args)
    {
        var n = args.Length > 0 ? args[0].AsInt() : 0;
        var x = args.Length > 1 ? args[1].AsInt() : 0;
        var y = args.Length > 2 ? args[2].AsInt() : 0;
        var w = args.Length > 3 ? args[3].AsInt() : 1;
        var h = args.Length > 4 ? args[4].AsInt() : 1;
        var fx = args.Length > 5 && args[5].IsTruthy();
        var fy = args.Length > 6 && args[6].IsTruthy();
        var state = bus.Display.DrawState;
        var renderer = bus.Display.Renderer;
        var tile = bus.Sprite.TileSize;
        var cols = bus.Sprite.SpritesPerRow;
        var tileW = w * tile;
        var tileH = h * tile;

        for (var row = 0; row < tileH; row++)
        {
            for (var col = 0; col < tileW; col++)
            {
                var sx = (n % cols) * tile + (fx ? tileW - 1 - col : col);
                var sy = (n / cols) * tile + (fy ? tileH - 1 - row : row);
                var c = bus.Sprite.SGet(sx, sy);
                if (state.PaletteTransparent[c & 0xF]) continue;
                renderer.PSet(x + col, y + row, c & 0xF);
            }
        }
    }

    private static void DrawSubSprite(Core.Chips.ChipBus bus, ScriptValue[] args)
    {
        var sx = args.Length > 0 ? args[0].AsInt() : 0;
        var sy = args.Length > 1 ? args[1].AsInt() : 0;
        var sw = args.Length > 2 ? args[2].AsInt() : 0;
        var sh = args.Length > 3 ? args[3].AsInt() : 0;
        var dx = args.Length > 4 ? args[4].AsInt() : 0;
        var dy = args.Length > 5 ? args[5].AsInt() : 0;
        var dw = args.Length > 6 ? args[6].AsInt() : sw;
        var dh = args.Length > 7 ? args[7].AsInt() : sh;
        var state = bus.Display.DrawState;
        var renderer = bus.Display.Renderer;

        for (var row = 0; row < dh; row++)
        {
            for (var col = 0; col < dw; col++)
            {
                var srcX = sx + col * sw / Math.Max(1, dw);
                var srcY = sy + row * sh / Math.Max(1, dh);
                var c = bus.Sprite.SGet(srcX, srcY);
                if (state.PaletteTransparent[c & 0xF]) continue;
                renderer.PSet(dx + col, dy + row, c & 0xF);
            }
        }
    }

    private static void DrawMap(Core.Chips.ChipBus bus, ScriptValue[] args)
    {
        var cx = args.Length > 0 ? args[0].AsInt() : 0;
        var cy = args.Length > 1 ? args[1].AsInt() : 0;
        var sx = args.Length > 2 ? args[2].AsInt() : 0;
        var sy = args.Length > 3 ? args[3].AsInt() : 0;
        var cw = args.Length > 4 ? args[4].AsInt() : 16;
        var ch = args.Length > 5 ? args[5].AsInt() : 16;
        var layer = args.Length > 6 ? args[6].AsInt() : 0;
        var state = bus.Display.DrawState;

        for (var ty = 0; ty < ch; ty++)
        {
            for (var tx = 0; tx < cw; tx++)
            {
                var tile = bus.Tilemap.MGet(cx + tx, cy + ty);
                if (layer > 0 && bus.Sprite.FGet(tile, layer) == 0) continue;
                var spriteArgs = new[]
                {
                    ScriptValue.Number(tile),
                    ScriptValue.Number(sx + tx * 8),
                    ScriptValue.Number(sy + ty * 8)
                };
                DrawSprite(bus, spriteArgs);
            }
        }
    }
}
