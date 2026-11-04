using System.Collections;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Tests.Compiler;

/// <summary>
/// Structural comparison of IR trees. Records compare <c>ImmutableArray</c> fields by reference,
/// so plain <c>Assert.Equal</c> on IR is useless; this walks the public properties instead.
/// </summary>
public static class IrAssert
{
    public static void Equal(IrNode expected, IrNode actual, bool compareSpans = false) =>
        Compare(expected, actual, "root", compareSpans);

    private static void Compare(object? expected, object? actual, string path, bool compareSpans)
    {
        if (expected is null || actual is null)
        {
            if (expected is not null || actual is not null)
                Assert.Fail($"{path}: expected {Show(expected)}, got {Show(actual)}");
            return;
        }

        if (expected.GetType() != actual.GetType())
            Assert.Fail($"{path}: expected {expected.GetType().Name}, got {actual.GetType().Name}");

        if (expected is IrNode)
        {
            foreach (var property in expected.GetType().GetProperties())
            {
                if (property.Name == "EqualityContract") continue;
                if (property.Name == nameof(IrNode.Span) && !compareSpans) continue;
                Compare(property.GetValue(expected), property.GetValue(actual), $"{path}.{property.Name}", compareSpans);
            }
            return;
        }

        if (expected is IEnumerable expectedItems and not string)
        {
            var e = expectedItems.Cast<object?>().ToList();
            var a = ((IEnumerable)actual).Cast<object?>().ToList();
            if (e.Count != a.Count)
                Assert.Fail($"{path}: expected {e.Count} items, got {a.Count}");
            for (var i = 0; i < e.Count; i++) Compare(e[i], a[i], $"{path}[{i}]", compareSpans);
            return;
        }

        if (!expected.Equals(actual))
            Assert.Fail($"{path}: expected {Show(expected)}, got {Show(actual)}");
    }

    private static string Show(object? value) => value is null ? "null" : $"{value} ({value.GetType().Name})";
}
