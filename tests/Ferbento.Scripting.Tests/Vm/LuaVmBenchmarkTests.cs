using System.Diagnostics;
using Ferbento.Scripting.Compiler.Bytecode;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Runtime;
using Ferbento.Scripting.Vm;
using Xunit.Abstractions;

namespace Ferbento.Scripting.Tests.Vm;

/// <summary>
/// The same loop (arithmetic, calls and table reads) on the VM and on the old GDScript interpreter.
/// Story 009 asks the VM to be at least as fast. Each side takes the best of a few runs.
/// </summary>
public class LuaVmBenchmarkTests(ITestOutputHelper output)
{
    private const int Outer = 200;
    private const int Inner = 100;

    private const string Lua = """
        local function add(a, b) return a + b end
        function bench()
          local t = {}
          for i = 1, 100 do t[i] = i end
          local s, x = 0, 0
          for n = 1, 200 do
            for i = 1, 100 do
              s = add(s, 1)
              x = t[i] * 2 - x
            end
          end
          return s
        end
        """;

    private const string GdScript = """
        func add(a, b):
            return a + b

        func bench():
            var t = []
            var i = 1
            while i <= 100:
                t[i] = i
                i = i + 1
            var s = 0
            var x = 0
            var n = 1
            while n <= 200:
                i = 1
                while i <= 100:
                    s = add(s, 1)
                    x = t[i] * 2 - x
                    i = i + 1
                n = n + 1
            return s
        """;

    [Fact]
    [Trait("Category", "Benchmark")]
    public void Vm_IsAtLeastAsFastAsTheOldInterpreter()
    {
        var vm = new LuaVm();
        var compiled = LuaCompiler.Compile(Lua);
        Assert.True(compiled.Success);
        vm.Execute(compiled.Main!);
        var bench = vm.GetGlobal("bench");

        var runtime = new FerbentoRuntime(source: GdScript);
        runtime.Sandbox.Context.MaxCpuSteps = int.MaxValue;

        Assert.Equal(Outer * Inner, vm.Call(bench)[0].AsInt());
        Assert.Equal(Outer * Inner, runtime.Sandbox.Call("bench").AsInt());

        var vmTime = Best(() => vm.Call(bench));
        var interpreterTime = Best(() =>
        {
            runtime.Sandbox.Context.ResetCpuBudget();
            runtime.Sandbox.Call("bench");
        });

        output.WriteLine($"VM: {vmTime.TotalMilliseconds:F1} ms; interpretador antigo: {interpreterTime.TotalMilliseconds:F1} ms");
        Assert.True(vmTime <= interpreterTime,
            $"VM {vmTime.TotalMilliseconds:F1} ms, interpretador antigo {interpreterTime.TotalMilliseconds:F1} ms");
    }

    private static TimeSpan Best(Action run)
    {
        run(); // warm-up (JIT)
        var best = TimeSpan.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            run();
            watch.Stop();
            if (watch.Elapsed < best) best = watch.Elapsed;
        }
        return best;
    }
}
