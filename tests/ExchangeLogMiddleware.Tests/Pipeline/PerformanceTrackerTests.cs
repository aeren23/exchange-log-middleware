namespace ExchangeLogMiddleware.Tests.Pipeline;

using ExchangeLogMiddleware.Middleware.Pipeline;

/// <summary>
/// <see cref="PerformanceTracker"/> unit testleri.
/// Thread-safety, sayaç doğruluğu ve Reset davranışı doğrulanır.
/// </summary>
public sealed class PerformanceTrackerTests
{
    private static PerformanceTracker CreateTracker() => new();

    // ─── Temel sayaç doğruluğu ───────────────────────────────────────────────

    [Fact]
    public void IncrementTotalReceived_SingleCall_CountIsOne()
    {
        var tracker = CreateTracker();

        tracker.IncrementTotalReceived();

        Assert.Equal(1, tracker.TotalReceived);
    }

    [Fact]
    public void IncrementDroppedByFilter_SingleCall_CountIsOne()
    {
        var tracker = CreateTracker();

        tracker.IncrementDroppedByFilter();

        Assert.Equal(1, tracker.DroppedByFilter);
    }

    [Fact]
    public void IncrementSuccessfullyProcessed_SingleCall_CountIsOne()
    {
        var tracker = CreateTracker();

        tracker.IncrementSuccessfullyProcessed();

        Assert.Equal(1, tracker.SuccessfullyProcessed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void IncrementTotalReceived_MultipleCalls_CountMatchesCallCount(int callCount)
    {
        var tracker = CreateTracker();

        for (var i = 0; i < callCount; i++)
            tracker.IncrementTotalReceived();

        Assert.Equal(callCount, tracker.TotalReceived);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(50)]
    public void IncrementDroppedByFilter_MultipleCalls_CountMatchesCallCount(int callCount)
    {
        var tracker = CreateTracker();

        for (var i = 0; i < callCount; i++)
            tracker.IncrementDroppedByFilter();

        Assert.Equal(callCount, tracker.DroppedByFilter);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(30)]
    public void IncrementSuccessfullyProcessed_MultipleCalls_CountMatchesCallCount(int callCount)
    {
        var tracker = CreateTracker();

        for (var i = 0; i < callCount; i++)
            tracker.IncrementSuccessfullyProcessed();

        Assert.Equal(callCount, tracker.SuccessfullyProcessed);
    }

    // ─── Sayaç bağımsızlığı ──────────────────────────────────────────────────

    [Fact]
    public void Counters_AreIndependent_IncrementingOneDoesNotAffectOthers()
    {
        var tracker = CreateTracker();

        tracker.IncrementTotalReceived();
        tracker.IncrementTotalReceived();
        tracker.IncrementDroppedByFilter();

        Assert.Equal(2, tracker.TotalReceived);
        Assert.Equal(1, tracker.DroppedByFilter);
        Assert.Equal(0, tracker.SuccessfullyProcessed);
    }

    // ─── Reset ───────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_AfterIncrements_AllCountersReturnToZero()
    {
        var tracker = CreateTracker();
        tracker.IncrementTotalReceived();
        tracker.IncrementTotalReceived();
        tracker.IncrementDroppedByFilter();
        tracker.IncrementSuccessfullyProcessed();

        tracker.Reset();

        Assert.Equal(0, tracker.TotalReceived);
        Assert.Equal(0, tracker.DroppedByFilter);
        Assert.Equal(0, tracker.SuccessfullyProcessed);
    }

    [Fact]
    public void Reset_OnFreshTracker_RemainsAtZero()
    {
        var tracker = CreateTracker();

        tracker.Reset();

        Assert.Equal(0, tracker.TotalReceived);
        Assert.Equal(0, tracker.DroppedByFilter);
        Assert.Equal(0, tracker.SuccessfullyProcessed);
    }

    // ─── Thread-Safety ───────────────────────────────────────────────────────

    [Fact]
    public async Task IncrementTotalReceived_ConcurrentTasks_CountIsExact()
    {
        const int taskCount = 20;
        const int incrementsPerTask = 500;
        var tracker = CreateTracker();

        var tasks = Enumerable
            .Range(0, taskCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < incrementsPerTask; i++)
                    tracker.IncrementTotalReceived();
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(taskCount * incrementsPerTask, tracker.TotalReceived);
    }

    [Fact]
    public async Task AllCounters_ConcurrentMixedIncrements_EachCountIsExact()
    {
        const int iterationsPerTask = 200;
        var tracker = CreateTracker();

        var receivedTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterationsPerTask; i++)
                    tracker.IncrementTotalReceived();
            }));

        var droppedTasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterationsPerTask; i++)
                    tracker.IncrementDroppedByFilter();
            }));

        var processedTasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterationsPerTask; i++)
                    tracker.IncrementSuccessfullyProcessed();
            }));

        await Task.WhenAll(receivedTasks.Concat(droppedTasks).Concat(processedTasks));

        Assert.Equal(10 * iterationsPerTask, tracker.TotalReceived);
        Assert.Equal(5 * iterationsPerTask, tracker.DroppedByFilter);
        Assert.Equal(5 * iterationsPerTask, tracker.SuccessfullyProcessed);
    }
}
