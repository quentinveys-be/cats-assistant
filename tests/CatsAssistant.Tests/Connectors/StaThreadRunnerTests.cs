using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class StaThreadRunnerTests
{
    [Fact]
    public void Run_ExecutesActionOnStaThread()
    {
        var apartmentState = StaThreadRunner.Run(() => Thread.CurrentThread.GetApartmentState());

        Assert.Equal(ApartmentState.STA, apartmentState);
    }

    [Fact]
    public void Run_ReturnsActionResult()
    {
        var result = StaThreadRunner.Run(() => 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Run_PropagatesExceptionFromAction_WithOriginalTypeAndMessage()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            StaThreadRunner.Run<int>(() => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", thrown.Message);
    }

    [Fact]
    public void Run_AlreadyOnStaThread_RunsInlineWithoutSpawningAnotherThread()
    {
        int callingThreadId = -1;
        int actionThreadId = -2;

        var thread = new Thread(() =>
        {
            callingThreadId = Environment.CurrentManagedThreadId;
            actionThreadId = StaThreadRunner.Run(() => Environment.CurrentManagedThreadId);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Equal(callingThreadId, actionThreadId);
    }
}
