namespace GitOpsRelay.Core.Polling;

public sealed class PollingOptions
{
    /// <summary>Delay between successful polling cycles.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Initial delay after a failed cycle.</summary>
    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum delay after a failed cycle.</summary>
    public TimeSpan MaximumFailureBackoff { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Symmetric random jitter applied to failure backoff, from zero to one.</summary>
    public double JitterRatio { get; set; } = 0.2;

    public void Validate()
    {
        if (Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(Interval)} must be positive.");
        }
        if (FailureBackoff <= TimeSpan.Zero || MaximumFailureBackoff < FailureBackoff)
        {
            throw new InvalidOperationException("Polling failure backoff values must be positive and ordered.");
        }
        if (JitterRatio is < 0 or > 1 || double.IsNaN(JitterRatio))
        {
            throw new InvalidOperationException($"{nameof(JitterRatio)} must be between zero and one.");
        }
    }
}
