using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitOpsRelay.Core.Polling;

public sealed class PollingBackgroundService(
    IPollCycle pollCycle,
    IOptions<PollingOptions> options,
    TimeProvider timeProvider,
    ILogger<PollingBackgroundService> logger) : BackgroundService
{
    private readonly PollingOptions _options = Validate(options.Value);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pollCycle.RunOnceAsync(stoppingToken).ConfigureAwait(false);
                consecutiveFailures = 0;
                await Task.Delay(_options.Interval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                consecutiveFailures = Math.Min(consecutiveFailures + 1, 30);
                logger.LogError(exception, "GitOpsRelay poll cycle failed; retrying after backoff.");
                var delay = CalculateBackoff(_options, consecutiveFailures);
                await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static PollingOptions Validate(PollingOptions options)
    {
        options.Validate();
        return options;
    }

    private static TimeSpan CalculateBackoff(PollingOptions options, int failureCount)
    {
        var multiplier = Math.Pow(2, Math.Min(failureCount - 1, 30));
        var ticks = Math.Min(options.FailureBackoff.Ticks * multiplier, options.MaximumFailureBackoff.Ticks);
        var jitterFactor = 1 + ((Random.Shared.NextDouble() * 2 - 1) * options.JitterRatio);
        var jitteredTicks = Math.Min(ticks * jitterFactor, options.MaximumFailureBackoff.Ticks);
        return TimeSpan.FromTicks(Math.Max(1, (long)jitteredTicks));
    }
}
