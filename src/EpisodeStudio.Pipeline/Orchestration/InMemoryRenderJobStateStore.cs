using System.Collections.Concurrent;
using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Pipeline.Orchestration;

public sealed class InMemoryRenderJobStateStore : IRenderJobStateStore
{
    private readonly ConcurrentDictionary<Guid, RenderJob> _jobs = new();

    public Task<RenderJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task SaveAsync(RenderJob renderJob, CancellationToken cancellationToken = default)
    {
        _jobs[renderJob.Id] = renderJob;
        return Task.CompletedTask;
    }
}
