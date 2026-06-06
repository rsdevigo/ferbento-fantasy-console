using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Avalonia.Media;

namespace Ferbento.Editor.Completion;

public sealed class Pico8CompletionItem : ICompletionData
{
    public string Text { get; }
    public object? Content => Text;
    public object? Description { get; }
    public IImage? Image => null;
    public double Priority { get; set; }

    public Pico8CompletionItem(string text, string description = "", double priority = 0)
    {
        Text = text;
        Description = description;
        Priority = priority;
    }

    public void Complete(TextArea area, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        area.Document.Replace(completionSegment, Text);
    }
}

public static class Pico8CompletionDataSource
{
    public static IEnumerable<ICompletionData> GetCompletions(string prefix)
    {
        return AllSymbols
            .Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static readonly IReadOnlyList<Pico8CompletionItem> AllSymbols = new List<Pico8CompletionItem>
    {
        new("abs", "abs(x) → absolute value of x", 1),
        new("add", "add(t, v) → append v to table t", 1),
        new("atan2", "atan2(dx, dy) → angle in [0..1]", 1),
        new("band", "band(a, b) → bitwise AND", 1),
        new("btn", "btn(i) → is button i pressed?", 1),
        new("btnp", "btnp(i) → was button i just pressed?", 1),
        new("bnot", "bnot(a) → bitwise NOT", 1),
        new("bor", "bor(a, b) → bitwise OR", 1),
        new("bxor", "bxor(a, b) → bitwise XOR", 1),
        new("camera", "camera(x, y) → set camera offset", 1),
        new("cartdata", "cartdata(id) → set persistent data slot", 1),
        new("circ", "circ(x, y, r, col) → draw circle outline", 1),
        new("circfill", "circfill(x, y, r, col) → draw filled circle", 1),
        new("clip", "clip(x, y, w, h) → set clipping region", 1),
        new("cls", "cls(col?) → clear screen", 1),
        new("cocreate", "cocreate(f) → create coroutine from function", 1),
        new("color", "color(c) → set draw color", 1),
        new("coresume", "coresume(co, ...) → resume coroutine", 1),
        new("cos", "cos(a) → cosine of a [0..1]", 1),
        new("costatus", "costatus(co) → get coroutine status", 1),
        new("count", "count(t) → count of array elements", 1),
        new("cursor", "cursor(x, y) → set print cursor", 1),
        new("del", "del(t, v) → remove v from table t", 1),
        new("dget", "dget(idx) → get cart data value", 1),
        new("dset", "dset(idx, val) → set cart data value", 1),
        new("extcmd", "extcmd(cmd) → send command to host", 1),
        new("fget", "fget(sprite, flag?) → get sprite flag", 1),
        new("fillp", "fillp(p) → set fill pattern", 1),
        new("flip", "flip() → present frame buffer", 1),
        new("flr", "flr(x) → floor of x", 1),
        new("fset", "fset(sprite, flag, val) → set sprite flag", 1),
        new("line", "line(x0, y0, x1, y1, col?) → draw line", 1),
        new("map", "map(cel_x?, cel_y?, sx?, sy?, cel_w?, cel_h?) → draw tilemap", 1),
        new("max", "max(a, b, ...) → maximum", 1),
        new("memcpy", "memcpy(dst, src, len) → copy memory", 1),
        new("memset", "memset(dst, val, len) → fill memory", 1),
        new("mget", "mget(x, y) → get map tile", 1),
        new("mid", "mid(a, b, c) → middle value", 1),
        new("min", "min(a, b, ...) → minimum", 1),
        new("mset", "mset(x, y, v) → set map tile", 1),
        new("music", "music(n, fade?, mask?) → play music pattern", 1),
        new("pal", "pal(c0, c1, p?) → set palette mapping", 1),
        new("palt", "palt(c, t?) → set palette transparency", 1),
        new("peek", "peek(addr) → read byte", 1),
        new("peek2", "peek2(addr) → read 16-bit word", 1),
        new("peek4", "peek4(addr) → read 32-bit dword", 1),
        new("pget", "pget(x, y) → get pixel color", 1),
        new("poke", "poke(addr, val) → write byte", 1),
        new("poke2", "poke2(addr, val) → write 16-bit word", 1),
        new("poke4", "poke4(addr, val) → write 32-bit dword", 1),
        new("printh", "printh(msg) → print to host console", 1),
        new("pset", "pset(x, y, col) → set pixel", 1),
        new("rect", "rect(x0, y0, x1, y1, col?) → draw rectangle outline", 1),
        new("rectfill", "rectfill(x0, y0, x1, y1, col?) → draw filled rect", 1),
        new("reboot", "reboot() → restart cartridge", 1),
        new("rnd", "rnd(max?) → random in [0..max)", 1),
        new("run", "run() → restart cartridge", 1),
        new("sget", "sget(x, y) → get sprite pixel", 1),
        new("shl", "shl(a, b) → bit shift left", 1),
        new("shr", "shr(a, b) → bit shift right", 1),
        new("sin", "sin(a) → sine of a [0..1]", 1),
        new("sgn", "sgn(x) → sign of x, sgn(0)=1", 1),
        new("sfx", "sfx(n, channel?, offset?, length?) → play SFX", 1),
        new("sqrt", "sqrt(x) → square root", 1),
        new("spr", "spr(n, x, y, w?, h?, flip_x?, flip_y?) → draw sprite", 1),
        new("srand", "srand(seed) → seed random number generator", 1),
        new("sspr", "sspr(sx, sy, sw, sh, dx, dy, dw?, dh?) → draw sprite region", 1),
        new("sset", "sset(x, y, col) → set sprite pixel", 1),
        new("stat", "stat(n) → get system stat", 1),
        new("sub", "sub(s, from, to?) → substring", 1),
        new("time", "time() → elapsed seconds", 1),
        new("tonum", "tonum(s) → string to number", 1),
        new("tostr", "tostr(x, hex?) → value to string", 1),
        new("type", "type(v) → type name string", 1),
        new("yield", "yield() → suspend coroutine", 1),
    };
}
