namespace EpisodeStudio.Core.Observability;

public sealed record LogEvent(
    DateTimeOffset Timestamp,
    string Level,
    string EventName,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Properties,
    string? Exception = null);

public sealed record JobMetric(
    Guid JobId,
    string Stage,
    TimeSpan Duration,
    bool Success,
    int RetryCount,
    DateTimeOffset Timestamp);

public sealed record UsageRecord(
    Guid ProjectId,
    string Provider,
    decimal CostUsd,
    int InputTokens,
    int OutputTokens,
    DateTimeOffset Timestamp);

public sealed record BudgetStatus(
    Guid ProjectId,
    decimal BudgetUsd,
    decimal SpentUsd,
    bool IsExceeded,
    decimal RemainingUsd);

public interface IStructuredLogger
{
    Task WriteAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
}

public interface IJobMetricsStore
{
    Task WriteAsync(JobMetric metric, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobMetric>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public interface IUsageTracker
{
    Task RecordAsync(UsageRecord usageRecord, CancellationToken cancellationToken = default);
    Task<BudgetStatus> GetBudgetStatusAsync(Guid projectId, decimal budgetUsd, CancellationToken cancellationToken = default);
}
