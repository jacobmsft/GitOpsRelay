using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GitOpsRelay.Core.Polling;

public static class PollingServiceCollectionExtensions
{
    public static IServiceCollection AddGitOpsRelayPolling(
        this IServiceCollection services,
        Action<PollingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddLogging();
        services.AddOptions<PollingOptions>()
            .Configure(configure ?? (_ => { }))
            .Validate(IsValid, "Polling options contain invalid interval or backoff settings.")
            .ValidateOnStart();
        services.TryAddSingleton<PollingBackgroundService>();
        services.AddHostedService(provider => provider.GetRequiredService<PollingBackgroundService>());
        return services;
    }

    private static bool IsValid(PollingOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
