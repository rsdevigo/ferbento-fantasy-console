using Ferbento.Scripting.Compiler.Lua;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Tests.Compiler;

/// <summary>Example programs in plain Lua (no PICO-8 shorthand yet: that is story 004).</summary>
public static class RoundTripPrograms
{
    public const string Platformer = """
        -- tiny platformer
        local gravity, jump = 0.3, -4
        player = {x = 64, y = 64, dx = 0, dy = 0, grounded = false}
        particles = {}

        function _init()
          cls(0)
          for i = 1, 20 do
            add(particles, {x = rnd(128), y = rnd(128), life = 30 + flr(rnd(30))})
          end
        end

        local function solid(x, y)
          return fget(mget(x \ 8, y \ 8), 0)
        end

        function _update60()
          local p = player
          if btn(0) then p.dx = -1 elseif btn(1) then p.dx = 1 else p.dx = p.dx * 0.8 end
          if btnp(4) and p.grounded then p.dy = jump end
          p.dy = p.dy + gravity
          if solid(p.x, p.y + p.dy + 8) then
            p.dy, p.grounded = 0, true
          else
            p.grounded = false
          end
          p.x, p.y = mid(0, p.x + p.dx, 120), p.y + p.dy

          for i = #particles, 1, -1 do
            local pt = particles[i]
            pt.life = pt.life - 1
            if pt.life <= 0 then deli(particles, i) goto continue end
            pt.y = pt.y - 0.5
            ::continue::
          end
        end

        function _draw()
          cls()
          map(0, 0, 0, 0, 16, 16)
          for pt in all(particles) do pset(pt.x, pt.y, 7 + pt.life % 3) end
          spr(1, p and p.x or player.x, player.y, 1, 1, player.dx < 0)
          print("score: " .. tostr(score or 0), 1, 1, 7)
          local bits = (flr(t()) & 0xff) | 1 << 4 ^^ ~0x0f
          print(bits >>> 2 .. "," .. (bits <<> 1) .. "," .. -bits >>< 3, 1, 8)
        end
        """;

    public const string ObjectsAndCoroutines = """
        local enemy = {}
        enemy.__index = enemy

        function enemy.new(x, y, ...)
          local e = setmetatable({x = x, y = y, tags = {...}}, enemy)
          e.hp = select("#", ...) > 0 and 3 or 1
          return e
        end

        function enemy:hit(damage)
          self.hp = self.hp - (damage or 1)
          return self.hp <= 0
        end

        function enemy:update()
          self.x = self.x + cos(t() / 4) * 0.5
        end

        local cutscene = cocreate(function()
          for i = 1, 3 do
            print("line " .. i)
            for _ = 1, 30 do yield() end
          end
          repeat
            local done = btnp(5)
            yield()
          until done
        end)

        function first(...)
          return (...)
        end

        function pack(...)
          return {n = select("#", ...), ...}
        end

        local e = enemy.new(10, 20, "boss", "red")
        if e:hit(2) then e = nil end
        coresume(cutscene)
        local lines = [[
        multi
        line]]
        local text = 'it\'s ' .. "\"quoted\"" .. "\x41\65\n"
        do
          local x <const> = nil
        end
        """;

    public const string EdgeCases = """
        ;;;
        local a, b, c
        a = {[1] = {}, [2] = {nested = {deep = true}}, "pos"; 'semi', f = function() end}
        b = a[2].nested.deep and not a[1] or #a ~= 0
        c = - - -1
        c = 2 ^ - 3 ^ 2
        c = (1 + 2) * (3 - 4) / 5 % 6 \ 7
        c = "a" .. "b" .. ("c" .. "d")
        c = (a)[1]
        c = (f()).field
        local t = {f = print}
        t.f "string arg"
        t.f {table = "arg"}
        t:f()
        f()(1)(2):m "x"
        while true do break end
        for i = 1, 3 do for j = i, 1, -1 do end end
        if a then elseif b then elseif c then else end
        do do do end end end
        return a, b, c
        """;

    public static TheoryData<string, string> All => new()
    {
        { nameof(Platformer), Platformer },
        { nameof(ObjectsAndCoroutines), ObjectsAndCoroutines.Replace("local x <const> = nil", "local x = nil") },
        { nameof(EdgeCases), EdgeCases },
    };
}

public class RoundTripTests
{
    /// <summary>source → IR → Lua → IR gives the same IR, and printing is stable.</summary>
    [Theory]
    [MemberData(nameof(RoundTripPrograms.All), MemberType = typeof(RoundTripPrograms))]
    public void SourceToIrToLuaToIr_IsStable(string name, string source)
    {
        var first = LuaParser.Parse(source, name);
        Assert.True(first.Diagnostics.IsEmpty, string.Join("\n", first.Diagnostics));

        var printed = LuaPrinter.Print(first.Chunk);
        var second = LuaParser.Parse(printed, name);
        Assert.True(second.Diagnostics.IsEmpty, $"{string.Join("\n", second.Diagnostics)}\n---\n{printed}");

        IrAssert.Equal(first.Chunk, second.Chunk);
        Assert.Equal(printed, LuaPrinter.Print(second.Chunk));
    }

    [Fact]
    public void Lua54Attributes_AreNotPico8()
    {
        // PICO-8 is Lua 5.2: '<const>' is a syntax error.
        Assert.True(LuaParser.Parse(RoundTripPrograms.ObjectsAndCoroutines).HasErrors);
    }
}
