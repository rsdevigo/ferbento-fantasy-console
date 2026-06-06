using Ferbento.Core.Math;

namespace Ferbento.Core.Tests;

public class FixedPointTests
{
    [Fact]
    public void FromInt_RoundTrips()
    {
        var fp = FixedPoint.FromInt(42);
        Assert.Equal(42, fp.ToInt());
    }

    [Fact]
    public void Sgn_Zero_IsOne()
    {
        Assert.Equal(1, FixedPoint.Sgn(FixedPoint.FromInt(0)));
    }

    [Fact]
    public void Sin_ReturnsInvertedRange()
    {
        var result = FixedPoint.Sin(FixedPoint.FromDouble(0));
        Assert.True(result.Raw <= 0);
    }
}
