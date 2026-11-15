using System.Collections.Concurrent;
using EpisodeStudio.Core.Observability;

namespace EpisodeStudio.Infrastructure.Observability;

public sealed class InMemoryJobMetricsStore : IJobMetricsStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<JobMetric>> _metrics = new();

    public Task WriteAsync(JobMetric metric, CancellationToken cancellationToken = default)
    {
        var bag = _metrics.GetOrAdd(metric.JobId, _ => new ConcurrentBag<JobMetric>());
        bag.Add(metric);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JobMetric>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_metrics.TryGetValue(jobId, out var bag))
        {
            return Task.FromResult<IReadOnlyList<JobMetric>>(Array.Empty<JobMetric>());
        }

        return Task.FromResult<IReadOnlyList<JobMetric>>(bag.OrderBy(m => m.Timestamp).ToList());
    }
}
