using System.Collections.Concurrent;
using EpisodeStudio.Core.Observability;

namespace EpisodeStudio.Infrastructure.Observability;

public sealed class InMemoryUsageTracker : IUsageTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<UsageRecord>> _usageByProject = new();

    public Task RecordAsync(UsageRecord usageRecord, CancellationToken cancellationToken = default)
    {
        var bag = _usageByProject.GetOrAdd(usageRecord.ProjectId, _ => new ConcurrentBag<UsageRecord>());
        bag.Add(usageRecord);
        return Task.CompletedTask;
    }

    public Task<BudgetStatus> GetBudgetStatusAsync(Guid projectId, decimal budgetUsd, CancellationToken cancellationToken = default)
    {
        var spent = _usageByProject.TryGetValue(projectId, out var bag)
            ? bag.Sum(x => x.CostUsd)
            : 0m;

        var remaining = budgetUsd - spent;
        var status = new BudgetStatus(
            ProjectId: projectId,
            BudgetUsd: budgetUsd,
            SpentUsd: spent,
            IsExceeded: remaining < 0,
            RemainingUsd: Math.Max(0, remaining));

        return Task.FromResult(status);
    }
}
