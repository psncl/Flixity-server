using FluentAssertions;

namespace server.Features.Movies.Tests;

public class CitySearchLocksTests
{
    private readonly CitySearchLocks _locks = new();

    [Fact]
    public async Task SameCity_Waits_OtherCity_DoesNot()
    {
        var first = await _locks.AcquireAsync(1, default);
        var second = _locks.AcquireAsync(1, default);

        (await _locks.AcquireAsync(2, default)).Dispose();
        second.IsCompleted.Should().BeFalse();

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    [Fact]
    public async Task SharedOutcome_ReachesOnlySearchesThatWereWaiting()
    {
        var outcome = new UnsavedOutcome(503);
        var first = await _locks.AcquireAsync(1, default);
        first.OutcomeWhileWaiting.Should().BeNull();
        var waiting = _locks.AcquireAsync(1, default);
        first.Share(outcome);
        var late = _locks.AcquireAsync(1, default); // Arrives after the outcome was shared

        first.Dispose();
        var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        second.OutcomeWhileWaiting.Should().Be(outcome);

        second.Dispose();
        using var third = await late.WaitAsync(TimeSpan.FromSeconds(5));
        third.OutcomeWhileWaiting.Should().BeNull();
    }

    [Fact]
    public async Task UnusedLocks_AreRemoved_EvenAfterCancelledWaiting()
    {
        var held = await _locks.AcquireAsync(1, default);
        using var cancellation = new CancellationTokenSource();
        var waiting = _locks.AcquireAsync(1, cancellation.Token);

        await cancellation.CancelAsync();
        var act = () => waiting;
        await act.Should().ThrowAsync<OperationCanceledException>();
        _locks.Count.Should().Be(1);

        held.Dispose();
        _locks.Count.Should().Be(0);
    }
}
