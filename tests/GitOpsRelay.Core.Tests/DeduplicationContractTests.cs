using GitOpsRelay.Core.State;

namespace GitOpsRelay.Core.Tests;

public sealed class DeduplicationContractTests
{
    [Fact]
    public async Task AbandonedAndDisposedReservationsCanRetryButCommittedKeyIsDuplicate()
    {
        var deduplicator = new TestDeduplicator();
        var first = await deduplicator.TryReserveAsync(
            "key", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), CancellationToken.None);
        Assert.Equal(SignalDeduplicationStatus.Reserved, first.Status);
        await first.Reservation!.AbandonAsync(CancellationToken.None);

        var retry = await deduplicator.TryReserveAsync(
            "key", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), CancellationToken.None);
        await retry.Reservation!.DisposeAsync();

        var afterDispose = await deduplicator.TryReserveAsync(
            "key", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), CancellationToken.None);
        await afterDispose.Reservation!.CommitAsync(CancellationToken.None);

        var duplicate = await deduplicator.TryReserveAsync(
            "key", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), CancellationToken.None);
        Assert.Equal(SignalDeduplicationStatus.DuplicateCommitted, duplicate.Status);
        Assert.Null(duplicate.Reservation);
    }

    [Fact]
    public async Task InFlightReservationIsNotReportedAsCommittedDuplicate()
    {
        var deduplicator = new TestDeduplicator();
        var reserved = await deduplicator.TryReserveAsync(
            "key", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), CancellationToken.None);
        var inFlight = await deduplicator.TryReserveAsync(
            "key", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal(SignalDeduplicationStatus.Reserved, reserved.Status);
        Assert.Equal(SignalDeduplicationStatus.InFlight, inFlight.Status);
        Assert.NotEqual(SignalDeduplicationStatus.DuplicateCommitted, inFlight.Status);
        Assert.NotNull(inFlight.RetryAfter);
        await reserved.Reservation!.DisposeAsync();
    }

    private sealed class TestDeduplicator : ISignalDeduplicator
    {
        private readonly HashSet<string> _reserved = [];
        private readonly HashSet<string> _committed = [];

        public ValueTask<SignalDeduplicationResult> TryReserveAsync(
            string idempotencyKey,
            TimeSpan reservationTtl,
            TimeSpan retentionWindow,
            CancellationToken cancellationToken)
        {
            if (_committed.Contains(idempotencyKey))
            {
                return ValueTask.FromResult(new SignalDeduplicationResult(
                    SignalDeduplicationStatus.DuplicateCommitted));
            }
            if (!_reserved.Add(idempotencyKey))
            {
                return ValueTask.FromResult(new SignalDeduplicationResult(
                    SignalDeduplicationStatus.InFlight,
                    RetryAfter: TimeSpan.FromSeconds(1)));
            }

            return ValueTask.FromResult(new SignalDeduplicationResult(
                SignalDeduplicationStatus.Reserved,
                new Reservation(this, idempotencyKey)));
        }

        private sealed class Reservation(TestDeduplicator owner, string key)
            : ISignalDeduplicationReservation
        {
            private bool _completed;

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                if (!_completed)
                {
                    owner._reserved.Remove(key);
                    owner._committed.Add(key);
                    _completed = true;
                }

                return ValueTask.CompletedTask;
            }

            public ValueTask AbandonAsync(CancellationToken cancellationToken)
            {
                if (!_completed)
                {
                    owner._reserved.Remove(key);
                    _completed = true;
                }

                return ValueTask.CompletedTask;
            }

        }
    }
}
