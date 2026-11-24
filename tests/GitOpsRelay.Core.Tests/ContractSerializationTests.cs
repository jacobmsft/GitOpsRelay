using System.Text.Json;
using GitOpsRelay.Contracts;

namespace GitOpsRelay.Core.Tests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void SubscriptionFilterRoundTripsWithSourceGeneratedJson()
    {
        var filter = new SubscriptionFilter
        {
            RepositoryIds = ["github.com/octo/app"],
            EventTypes = [GitSignalTypes.Push, GitSignalTypes.PullRequestUpdated],
            RefGlobs = ["refs/heads/release/*"],
            PathGlobs = ["deploy/**"]
        };
        var json = JsonSerializer.Serialize(filter, GitSignalJsonContext.Default.SubscriptionFilter);
        var roundTrip = JsonSerializer.Deserialize(json, GitSignalJsonContext.Default.SubscriptionFilter);

        Assert.NotNull(roundTrip);
        Assert.Equal(filter.RepositoryIds, roundTrip.RepositoryIds);
        Assert.Equal(filter.EventTypes, roundTrip.EventTypes);
        Assert.Equal(filter.RefGlobs, roundTrip.RefGlobs);
        Assert.Equal(filter.PathGlobs, roundTrip.PathGlobs);
    }
}
