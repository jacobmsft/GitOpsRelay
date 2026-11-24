using GitOpsRelay.Core.Polling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GitOpsRelay.Core.Tests;

public sealed class PollingBackgroundServiceTests
{
    [Fact]
    public async Task TransientPollFailureIsLoggedAndLoopContinues()
    {
        var cycle = new ThrowsOncePollCycle();
        var service = new PollingBackgroundService(
            cycle,
            Options.Create(new PollingOptions
            {
                Interval = TimeSpan.FromMilliseconds(5),
                FailureBackoff = TimeSpan.FromMilliseconds(5),
                MaximumFailureBackoff = TimeSpan.FromMilliseconds(10),
                JitterRatio = 0
            }),
            TimeProvider.System,
            NullLogger<PollingBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await cycle.SecondRun.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StopAsync(timeout.Token);

        Assert.True(cycle.CallCount >= 2);
    }

    [Fact]
    public void PollingOptionsRejectNonPositiveIntervals()
    {
        var invalidOptions = Options.Create(new PollingOptions { Interval = TimeSpan.Zero });
        Assert.Throws<InvalidOperationException>(() => new PollingBackgroundService(
            new ThrowsOncePollCycle(),
            invalidOptions,
            TimeProvider.System,
            NullLogger<PollingBackgroundService>.Instance));
    }

    private sealed class ThrowsOncePollCycle : IPollCycle
    {
        private int _calls;
        public int CallCount => Volatile.Read(ref _calls);
        public TaskCompletionSource SecondRun { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask RunOnceAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                throw new InvalidOperationException("Transient network error.");
            }

            SecondRun.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
