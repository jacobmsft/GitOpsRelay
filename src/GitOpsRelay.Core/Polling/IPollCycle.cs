namespace GitOpsRelay.Core.Polling;

public interface IPollCycle
{
    ValueTask RunOnceAsync(CancellationToken cancellationToken);
}
