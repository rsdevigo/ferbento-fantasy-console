using System.Collections.Concurrent;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

// Regression test for BUG-004: GDShrapt.Reader is not thread-safe, so concurrent loads used to
// corrupt each other (wrong results or InvalidOperationException).
public class ParserConcurrencyTests
{
    [Fact]
    public void ConcurrentLoads_ProduceCorrectResults()
    {
        var errors = new ConcurrentBag<string>();
        Parallel.For(0, 400, _ =>
        {
            try
            {
                var rt = new FerbentoRuntime(source: "func test():\n    return 3 + 4 * 2\n");
                rt.Loop.Boot();
                var result = rt.Sandbox.Call("test");
                if (result.AsInt() != 11) errors.Add($"{result.Type}:{result}");
            }
            catch (Exception e)
            {
                errors.Add($"{e.GetType().Name}: {e.Message}");
            }
        });
        Assert.True(errors.IsEmpty, $"{errors.Count} errors; {string.Join(" | ", errors.Distinct().Take(3))}");
    }
}
