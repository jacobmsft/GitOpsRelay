using GitOpsRelay.Core.Polling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GitOpsRelay.Core.Tests;

public sealed class PollingServiceCollectionExtensionsTests
{
    [Fact]
    public void RegistrationProvidesTimeProviderAndHostedPollingService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPollCycle, NoopPollCycle>();
        services.AddGitOpsRelayPolling(options => options.Interval = TimeSpan.FromSeconds(1));

        using var provider = services.BuildServiceProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.IsType<PollingBackgroundService>(provider.GetRequiredService<IHostedService>());
    }

    [Fact]
    public void RegistrationValidatesOptionsWhenHostedServiceStarts()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPollCycle, NoopPollCycle>();
        services.AddGitOpsRelayPolling(options => options.Interval = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IHostedService>());
    }

    private sealed class NoopPollCycle : IPollCycle
    {
        public ValueTask RunOnceAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
