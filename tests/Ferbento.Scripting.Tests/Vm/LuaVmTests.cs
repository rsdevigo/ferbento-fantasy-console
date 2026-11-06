using Ferbento.Core.Math;
using Ferbento.Scripting.Compiler.Analysis;
using Ferbento.Scripting.Compiler.Bytecode;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Vm;

namespace Ferbento.Scripting.Tests.Vm;

/// <summary>Programs compiled from Lua source and run on the VM (story 009).</summary>
public class LuaVmTests
{
    private static Prototype Compile(string source)
    {
        var result = LuaCompiler.Compile(source, "test.lua");
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(d => d.ToString())));
        return result.Main!;
    }

    private static ScriptValue[] Run(string source, LuaVm? vm = null) => (vm ?? new LuaVm()).Execute(Compile(source));

    private static ScriptValue Run1(string source, LuaVm? vm = null) => Run(source, vm).FirstOrDefault() ?? ScriptValue.Nil;

    private static string[] Show(ScriptValue[] values) => [.. values.Select(v => v.ToString())];

    private static (LuaVm Vm, List<string> Output) VmWithPrint()
    {
        var vm = new LuaVm();
        var output = new List<string>();
        vm.Register("print", args =>
        {
            output.Add(args.Length > 0 ? args[0].ToString() : "");
            return ScriptValue.Nil;
        });
        return (vm, output);
    }

    // ---- basics ----

    [Fact]
    public void Return_Arithmetic_UsesPrecedence()
    {
        Assert.Equal(7, Run1("return 1 + 2 * 3").AsInt());
    }

    [Fact]
    public void Return_NothingGivesNoValues()
    {
        Assert.Empty(Run("local x = 1"));
    }

    [Fact]
    public void Arithmetic_FollowsPico8FixedPoint()
    {
        // ref: Pico8Ops (008): overflow wraps, division by zero saturates, \ floors; & binds tighter than ^^.
        Assert.Equal(["-32768", "true", "-4", "3"], Show(Run("return 32767 + 1, 1 / 0 == 0x7fff.ffff, -7 \\ 2, 2 ^^ 3 & 1")));
    }

    [Fact]
    public void Concat_And_Comparison_GoThroughPico8Ops()
    {
        Assert.Equal(["a12", "true", "false"], Show(Run("""return "a" .. 1 .. 2, "a" < "b", 2 <= 1""")));
    }

    [Fact]
    public void Globals_AreSharedWithTheHost()
    {
        var vm = new LuaVm();
        vm.SetGlobal("base", ScriptValue.Number(10));
        Run("""
            function add(n) return base + n end
            result = add(5)
            """, vm);
        Assert.Equal(15, vm.GetGlobal("result").AsInt());
        Assert.Equal(17, vm.Call(vm.GetGlobal("add"), ScriptValue.Number(7))[0].AsInt());
    }

    [Fact]
    public void Dialect_ShortIfAndShortPrint_Run()
    {
        var (vm, output) = VmWithPrint();
        Run("""
            x = 3
            if (x > 2) ? "big"
            if (x > 5) ? "huge"
            x += 1 ? x
            """, vm);
        Assert.Equal(["big", "4"], output);
    }

    // ---- functions and recursion ----

    [Fact]
    public void Recursion_Fibonacci()
    {
        Assert.Equal(610, Run1("""
            local function fib(n)
              if n < 2 then return n end
              return fib(n - 1) + fib(n - 2)
            end
            return fib(15)
            """).AsInt());
    }

    [Fact]
    public void Recursion_MutualThroughGlobals()
    {
        Assert.Equal(["true", "false"], Show(Run("""
            function is_even(n) if n == 0 then return true end return is_odd(n - 1) end
            function is_odd(n) if n == 0 then return false end return is_even(n - 1) end
            return is_even(10), is_even(7)
            """)));
    }

    [Fact]
    public void Recursion_DeepCallsDoNotUseTheCSharpStack()
    {
        // 20000 nested Lua calls would overflow a recursive C# interpreter.
        Assert.Equal(20000, Run1("""
            local function depth(n)
              if n == 0 then return 0 end
              return 1 + depth(n - 1)
            end
            return depth(20000)
            """).AsInt());
    }

    [Fact]
    public void Recursion_Infinite_IsAStackOverflowError()
    {
        var error = Assert.Throws<Pico8RuntimeException>(() => Run("""
            local function f() return 1 + f() end
            return f()
            """));
        Assert.Equal("stack overflow", error.Message);
    }

    [Fact]
    public void Arguments_MissingAreNil_ExtraAreDropped()
    {
        Assert.Equal(["1", "nil", "3"], Show(Run("""
            local function f(a, b) return a, b end
            local function g(a) return a end
            local x, y = f(1)
            return x, y, g(3, 4, 5)
            """)));
    }

    [Fact]
    public void MethodCall_PassesSelf_AndEvaluatesTheReceiverOnce()
    {
        Assert.Equal(["5", "1"], Show(Run("""
            local obj = {x = 3}
            function obj:get(d) return self.x + d end
            local calls = 0
            local function find() calls += 1 return obj end
            return find():get(2), calls
            """)));
    }

    [Fact]
    public void FunctionStatement_WithFieldPath()
    {
        Assert.Equal(9, Run1("""
            local m = {sub = {}}
            function m.sub.sq(n) return n * n end
            return m.sub.sq(3)
            """).AsInt());
    }

    // ---- multiple results and varargs ----

    [Fact]
    public void MultipleResults_AreAdjusted()
    {
        Assert.Equal(["1", "2", "3", "1", "1", "nil"], Show(Run("""
            local function three() return 1, 2, 3 end
            local a, b, c = three()
            local d, e, f = (three()), nil
            return a, b, c, d, three(), f
            """)).Take(6).ToArray());
    }

    [Fact]
    public void Return_PassesAllResultsOfALastCall()
    {
        Assert.Equal(["0", "1", "2", "3"], Show(Run("""
            local function three() return 1, 2, 3 end
            local function wrap() return 0, three() end
            return wrap()
            """)));
    }

    [Fact]
    public void Varargs_InTablesCallsAndLocals()
    {
        Assert.Equal(["3", "c", "a", "4", "nil"], Show(Run("""
            local function count(...) local t = {...} return #t end
            local function rev(...) local a, b, c = ... return c, b, a end
            local function first(...) return (...) end
            local function pass(...) return count(1, ...) end
            local x, y, z = rev("a", "b", "c")
            return count(1, 2, 3), x, z, pass(2, 3, 4), first()
            """)));
    }

    [Fact]
    public void Varargs_OfTheMainChunk()
    {
        var vm = new LuaVm();
        var results = vm.Execute(Compile("local a, b = ... return b, a"), ScriptValue.Number(1), ScriptValue.Number(2));
        Assert.Equal(["2", "1"], Show(results));
    }

    [Fact]
    public void NativeFunctions_SingleAndMultipleResults()
    {
        var vm = new LuaVm();
        vm.Register("double", args => ScriptValue.Number(args[0].AsInt() * 2));
        vm.Register("pair", (Func<ScriptValue[], ScriptValue[]>)(_ => [ScriptValue.Number(7), ScriptValue.Str("x")]));
        Assert.Equal(["8", "7", "x", "7", "3"], Show(Run("""
            local a, b = pair()
            local t = {pair(), pair()}
            return double(4), a, b, (pair()), #t
            """, vm)));
    }

    [Fact]
    public void NativeFunctions_CanCallBackIntoLua()
    {
        var vm = new LuaVm();
        vm.Register("each", args =>
        {
            var table = args[0].AsTable();
            foreach (var value in table.ArrayValues.ToList()) vm.Call(args[1], value);
            return ScriptValue.Nil;
        });
        Assert.Equal(60, Run1("""
            local sum = 0
            each({10, 20, 30}, function(v) sum += v end)
            return sum
            """, vm).AsInt());
    }

    // ---- closures ----

    [Fact]
    public void Closures_InNumericFor_CaptureEachIteration()
    {
        Assert.Equal(["1", "2", "3"], Show(Run("""
            local fs = {}
            for i = 1, 3 do fs[i] = function() return i end end
            return fs[1](), fs[2](), fs[3]()
            """)));
    }

    [Fact]
    public void Closures_InWhile_CaptureEachLocalDeclaration()
    {
        Assert.Equal(["10", "20", "30"], Show(Run("""
            local fs, n = {}, 0
            while n < 3 do
              n += 1
              local v = n * 10
              fs[n] = function() return v end
            end
            return fs[1](), fs[2](), fs[3]()
            """)));
    }

    [Fact]
    public void Closures_InGenericFor_CaptureEachIteration()
    {
        Assert.Equal(["a", "b"], Show(Run("""
            local function iter(t, i)
              i += 1
              if t[i] then return i, t[i] end
            end
            local fs = {}
            for i, v in iter, {"a", "b"}, 0 do fs[i] = function() return v end end
            return fs[1](), fs[2]()
            """)));
    }

    [Fact]
    public void Closures_ShareUpvalues()
    {
        Assert.Equal(["2", "2", "12"], Show(Run("""
            local function counter()
              local n = 0
              return function() n += 1 return n end, function() return n end
            end
            local inc, get = counter()
            inc()
            local after = inc()
            local inc2, get2 = counter()
            for i = 1, 12 do inc2() end
            return after, get(), get2()
            """)));
    }

    [Fact]
    public void Closures_UpvalueFromTwoLevelsUp_AndCapturedParameter()
    {
        // f runs twice (x 3, p 5); g once (x 2, p 101).
        Assert.Equal(["8", "103"], Show(Run("""
            local function outer(p)
              local x = 1
              return function()
                return function() x += 1 p += 1 return x + p end
              end
            end
            local f = outer(3)()
            local g = outer(100)()
            f()
            return f(), g()
            """)));
    }

    [Fact]
    public void Closures_SeeLaterChangesToTheVariable()
    {
        Assert.Equal(5, Run1("""
            local x = 1
            local function get() return x end
            x = 5
            return get()
            """).AsInt());
    }

    // ---- tables ----

    [Fact]
    public void Tables_Nested_IndexAndAssign()
    {
        Assert.Equal(["6", "2", "ok", "3"], Show(Run("""
            local t = {a = {b = {c = 5}}, list = {1, 2, {k = "ok"}}}
            t.a.b.c += 1
            t["a"]["x"] = 2
            return t.a.b.c, t.a.x, t.list[3].k, #t.list
            """)));
    }

    [Fact]
    public void Tables_Constructor_PositionalKeyedAndOpenTail()
    {
        Assert.Equal(["4", "1", "x", "3", "2"], Show(Run("""
            local function two() return 3, 4 end
            local t = {1, k = "x", 2, two()}
            local u = {two(), two()}
            return #t, t[1], t.k, t[3], #u + 0 * #u - 1
            """)));
    }

    [Fact]
    public void Tables_Constructor_MoreThanOneFlush()
    {
        var items = string.Join(", ", Enumerable.Range(1, 120));
        Assert.Equal(["120", "1", "51", "120"], Show(Run($"local t = {{{items}}} return #t, t[1], t[51], t[120]")));
    }

    [Fact]
    public void Tables_IndexingNil_ReportsTheLine()
    {
        var error = Assert.Throws<Pico8RuntimeException>(() => Run("""
            local t = nil
            local x = 1
            return t.field
            """));
        Assert.Equal("attempt to index a nil value", error.Message);
        Assert.Equal(3, error.Span?.Line);
    }

    // ---- assignment ----

    [Fact]
    public void MultipleAssignment_EvaluatesValuesBeforeAssigning()
    {
        Assert.Equal(["2", "1", "b", "a"], Show(Run("""
            local a, b = 1, 2
            a, b = b, a
            local t = {"a", "b"}
            t[1], t[2] = t[2], t[1]
            return a, b, t[1], t[2]
            """)));
    }

    [Fact]
    public void MultipleAssignment_MixedTargets_AndExtraValues()
    {
        Assert.Equal(["1", "2", "3", "nil", "1"], Show(Run("""
            local calls = 0
            local function f() calls += 1 return 9 end
            local t = {}
            local x
            t.a, x, t.b = 1, 2, 3, f()
            local y, z = 1
            return t.a, x, t.b, z, calls
            """)));
    }

    [Fact]
    public void CompoundAssign_EvaluatesTableAndKeyOnce()
    {
        Assert.Equal(["15", "1", "1"], Show(Run("""
            local calls, tcalls = 0, 0
            local t = {5}
            local function key() calls += 1 return 1 end
            local function tab() tcalls += 1 return t end
            tab()[key()] += 10
            return t[1], calls, tcalls
            """)));
    }

    [Fact]
    public void CompoundAssign_ValueIsAWholeExpression()
    {
        Assert.Equal(["10", "ab!", "2"], Show(Run("""
            local a = 2
            a *= 2 + 3
            local s = "a"
            s ..= "b" .. "!"
            g = 5
            g \= 2
            return a, s, g
            """)));
    }

    [Fact]
    public void PeekOperators_CallPeekFunctions()
    {
        var vm = new LuaVm();
        var calls = new List<string>();
        foreach (var name in new[] { "peek", "peek2", "peek4" })
        {
            vm.Register(name, args =>
            {
                calls.Add($"{name}({args[0]})");
                return ScriptValue.Number(1);
            });
        }
        Assert.Equal(3, Run1("return @0x5f00 + %(100 + 1) + $8", vm).AsInt());
        Assert.Equal(["peek(24320)", "peek2(101)", "peek4(8)"], calls);
    }

    // ---- control flow ----

    [Fact]
    public void AndOr_ShortCircuit()
    {
        Assert.Equal(["false", "0", "nil", "5", "nil", "yes", "no"], Show(Run("""
            local called = 0
            local function f() called += 1 return true end
            local r1 = false and f()
            local r2 = 1 or f()
            local r3 = nil and f()
            local function pick(c) return c and "yes" or "no" end
            return r1, called, r3, nil or 5, 1 and nil, pick(1), pick(false)
            """)).Take(7).ToArray());
    }

    [Fact]
    public void Goto_ContinueIdiom()
    {
        Assert.Equal(25, Run1("""
            local sum = 0
            for i = 1, 10 do
              if i % 2 == 0 then goto continue end
              sum += i
              ::continue::
            end
            return sum
            """).AsInt());
    }

    [Fact]
    public void Goto_BackwardLoop_AndOutOfNestedLoops()
    {
        Assert.Equal(["5", "2", "6"], Show(Run("""
            local n = 0
            ::again::
            n += 1
            if n < 5 then goto again end

            local fi, fj
            for i = 1, 10 do
              for j = 1, 10 do
                if i * j == 12 then fi, fj = i, j goto done end
              end
            end
            ::done::
            return n, fi, fj
            """)));
    }

    [Fact]
    public void Loops_BreakWhileRepeat()
    {
        Assert.Equal(["4", "16", "5"], Show(Run("""
            local i = 0
            while true do
              i += 1
              if i == 4 then break end
            end
            local x = 1
            repeat local y = x * 2 x = y until y > 10
            local k = 0
            for j = 1, 100 do k = j if j == 5 then break end end
            return i, x, k
            """)));
    }

    [Fact]
    public void NumericFor_StepsAndLimits()
    {
        Assert.Equal(["3,2,1,", "0,0.5,1,", "", "1,2,3,"], Show(Run("""
            local function run(a, b, c)
              local s = ""
              for i = a, b, c do s ..= i .. "," end
              return s
            end
            local t = ""
            for i = 1, 3 do t ..= i .. "," i = 100 end
            return run(3, 1, -1), run(0, 1, 0.5), run(1, 0, 1), t
            """)));
    }

    [Fact]
    public void NumericFor_NonNumber_IsAnError()
    {
        var error = Assert.Throws<Pico8RuntimeException>(() => Run("for i = 1, {} do end"));
        Assert.Equal("'for' limit must be a number", error.Message);
    }

    [Fact]
    public void GenericFor_WithAStatefulIterator()
    {
        Assert.Equal(6, Run1("""
            local function range(n)
              local i = 0
              return function() i += 1 if i <= n then return i end end
            end
            local s = 0
            for x in range(3) do s += x end
            return s
            """).AsInt());
    }

    // ---- environment ----

    [Fact]
    public void LocalEnv_RedirectsFreeNames()
    {
        var vm = new LuaVm();
        Assert.Equal(["5", "7", "nil"], Show(Run("""
            local _ENV = {x = 5}
            y = 7
            return x, _ENV.y, z
            """, vm)));
        Assert.Equal(ScriptValueType.Nil, vm.GetGlobal("y").Type);
    }

    // ---- errors ----

    [Fact]
    public void Errors_CallingNil_HasLineAndKeepsTheVmUsable()
    {
        var vm = new LuaVm();
        var error = Assert.Throws<Pico8RuntimeException>(() => Run("""
            local function f()
              return missing()
            end
            return f()
            """, vm));
        Assert.Equal("attempt to call a nil value", error.Message);
        Assert.Equal(2, error.Span?.Line);

        Assert.Equal(3, Run1("return 1 + 2", vm).AsInt());
    }

    [Fact]
    public void Errors_GreaterThan_ComparesInLuaOrder()
    {
        // ref: Lua 5.2 compiles a > b as b < a, so the message names b's type first.
        var error = Assert.Throws<Pico8RuntimeException>(() => Run("""return 1 > "x" """));
        Assert.Equal("attempt to compare string with number", error.Message);
    }

    [Fact]
    public void Compile_KeepsTheLineOfEachInstruction()
    {
        var main = Compile("""
            local a = 1
            local b = 2
            return a + b
            """);
        Assert.Equal(main.Code.Length, main.Spans.Length);
        Assert.Contains(main.Code.Select((_, pc) => main.LineAt(pc)), line => line == 3);
    }

    [Theory]
    [MemberData(nameof(Compiler.RoundTripPrograms.All), MemberType = typeof(Compiler.RoundTripPrograms))]
    public void SamplePrograms_CompileToBytecode(string name, string source)
    {
        var result = LuaCompiler.Compile(source, name);
        Assert.True(result.Success, string.Join("\n", result.Errors));
    }

    [Fact]
    public void SamplePlatformer_RunsSomeFrames()
    {
        var (vm, output) = VmWithPrint();
        foreach (var name in new[] { "cls", "map", "pset", "spr" }) vm.Register(name, _ => ScriptValue.Nil);
        foreach (var name in new[] { "fget", "btn", "btnp" }) vm.Register(name, _ => ScriptValue.False);
        vm.Register("mget", _ => ScriptValue.Number(0));
        vm.Register("t", _ => ScriptValue.Number(0));
        vm.Register("rnd", args => ScriptValue.Number(args[0].AsNumber().ToDouble() / 2));
        vm.Register("flr", args => ScriptValue.Number(args[0].AsNumber().ToInt()));
        vm.Register("tostr", args => ScriptValue.Str(args[0].ToString()));
        vm.Register("mid", args =>
        {
            var v = args.Select(a => a.AsNumber().ToDouble()).Order().ToArray();
            return ScriptValue.Number(v[1]);
        });
        vm.Register("add", args =>
        {
            var table = args[0].AsTable();
            table.Set(table.Length + 1, args[1]);
            return args[1];
        });
        vm.Register("deli", args =>
        {
            var table = args[0].AsTable();
            var length = table.Length;
            for (var i = args[1].AsInt(); i < length; i++) table.Set(i, table.Get(i + 1));
            table.Set(length, ScriptValue.Nil);
            return ScriptValue.Nil;
        });
        vm.Register("all", args =>
        {
            var table = args[0].AsTable();
            var i = 0;
            return ScriptValue.Native(_ => table.Get(++i));
        });

        // The sample's last print only exercises the parser: '..' binds tighter than the shifts, so
        // 'bits >>> 2 .. ","' shifts by a string and fails, in PICO-8 too.
        const string bitsPrint = """print(bits >>> 2 .. "," .. (bits <<> 1) .. "," .. -bits >>< 3, 1, 8)""";
        Assert.Contains(bitsPrint, Compiler.RoundTripPrograms.Platformer);
        Run(Compiler.RoundTripPrograms.Platformer.Replace(bitsPrint, """print((bits >>> 2) .. "," .. (-bits >>< 3), 1, 8)"""), vm);
        vm.Call(vm.GetGlobal("_init"));
        for (var frame = 0; frame < 60; frame++)
        {
            vm.Call(vm.GetGlobal("_update60"));
            vm.Call(vm.GetGlobal("_draw"));
        }

        // 20 particles with life 30..44 (rnd gives half): all gone after 60 frames.
        Assert.Equal(0, vm.GetGlobal("particles").AsTable().Length);
        Assert.Equal(120, output.Count);
        Assert.Equal("score: 0", output[0]);
    }

    [Fact]
    public void Compile_IrBuiltByHand()
    {
        // local n = 4; return n * n, n > 3  (no spans: synthetic nodes)
        var n = () => new IrName("n");
        var chunk = new IrChunk([
            new IrLocal(["n"], [new IrNumber(FixedPoint.FromInt(4))]),
            new IrReturn([
                new IrBinary(IrBinaryOp.Multiply, n(), n()),
                new IrBinary(IrBinaryOp.Greater, n(), new IrNumber(FixedPoint.FromInt(3))),
            ]),
        ]);
        var main = BytecodeCompiler.Compile(chunk, StaticAnalyzer.Analyze(chunk));
        Assert.Equal(["16", "true"], Show(new LuaVm().Execute(main)));
    }

    [Fact]
    public void Compile_AnalysisErrors_PreventTheBytecode()
    {
        var result = LuaCompiler.Compile("break");
        Assert.False(result.Success);
        Assert.Contains(result.Errors, d => d.Code == "FB0201");
    }
}
