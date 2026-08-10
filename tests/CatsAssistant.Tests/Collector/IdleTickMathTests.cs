using CatsAssistant.Collector;

namespace CatsAssistant.Tests.Collector;

public class IdleTickMathTests
{
    [Fact]
    public void ComputeIdleDuration_NormalCase_ReturnsDifference()
    {
        var duration = IdleTickMath.ComputeIdleDuration(currentTickCount: 10_000, lastInputTickCount: 4_000);

        Assert.Equal(TimeSpan.FromMilliseconds(6_000), duration);
    }

    [Fact]
    public void ComputeIdleDuration_NoElapsedTime_ReturnsZero()
    {
        var duration = IdleTickMath.ComputeIdleDuration(currentTickCount: 5_000, lastInputTickCount: 5_000);

        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void ComputeIdleDuration_TickCountWrapsAround_StaysCorrect()
    {
        // GetTickCount wraps to 0 after ~49.7 days (uint.MaxValue ms).
        var lastInput = uint.MaxValue - 999;
        var current = 4_000u;

        var duration = IdleTickMath.ComputeIdleDuration(current, lastInput);

        Assert.Equal(TimeSpan.FromMilliseconds(5_000), duration);
    }
}
