using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Pipeline.Orchestration;

public sealed class RenderPipelineOrchestrator
{
    private static readonly PipelineStage[] OrderedStages =
    [
        PipelineStage.ScriptParse,
        PipelineStage.ScenePlan,
        PipelineStage.StoryboardGen,
        PipelineStage.VoiceGen,
        PipelineStage.TimelineCompose,
        PipelineStage.RenderExport
    ];

    private readonly IReadOnlyDictionary<PipelineStage, IPipelineStageExecutor> _executors;
    private readonly IRenderJobStateStore _stateStore;
    private readonly PipelineOptions _options;

    public RenderPipelineOrchestrator(
        IEnumerable<IPipelineStageExecutor> executors,
        IRenderJobStateStore stateStore,
        PipelineOptions? options = null)
    {
        _executors = executors.ToDictionary(e => e.Stage, e => e);
        _stateStore = stateStore;
        _options = options ?? new PipelineOptions();
    }

    public async Task<RenderJob> RunAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _stateStore.GetAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Render job '{jobId}' was not found.");

        job = job with
        {
            Status = RenderJobStatus.Running,
            StartedAt = job.StartedAt ?? DateTimeOffset.UtcNow
        };

        await _stateStore.SaveAsync(job, cancellationToken);

        foreach (var stage in OrderedStages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsStageCompleted(job, stage))
            {
                continue;
            }

            job = job with { CurrentStage = stage };
            await _stateStore.SaveAsync(job, cancellationToken);

            var execution = await ExecuteWithRetryAsync(job, stage, cancellationToken);
            var result = execution.Result;
            var retriesUsed = execution.RetriesUsed;
            if (!result.Success)
            {
                job = UpdateCheckpoint(job, stage, CheckpointStatus.Failed, retriesUsed, result.Details ?? result.Exception?.Message);
                job = job with
                {
                    Status = RenderJobStatus.Failed,
                    Error = result.Exception?.Message ?? result.Details,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                await _stateStore.SaveAsync(job, cancellationToken);
                return job;
            }

            job = UpdateCheckpoint(job, stage, CheckpointStatus.Completed, retriesUsed, result.Details);
            await _stateStore.SaveAsync(job, cancellationToken);
        }

        job = job with
        {
            Status = RenderJobStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };

        await _stateStore.SaveAsync(job, cancellationToken);
        return job;
    }

    private async Task<(StageExecutionResult Result, int RetriesUsed)> ExecuteWithRetryAsync(RenderJob job, PipelineStage stage, CancellationToken cancellationToken)
    {
        if (!_executors.TryGetValue(stage, out var executor))
        {
            throw new InvalidOperationException($"No executor registered for stage '{stage}'.");
        }

        for (var attempt = 1; attempt <= _options.MaxRetriesPerStage + 1; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inProgressJob = UpdateCheckpoint(job, stage, CheckpointStatus.InProgress, attempt - 1, $"Attempt {attempt}");
            await _stateStore.SaveAsync(inProgressJob, cancellationToken);
            job = inProgressJob;

            try
            {
                var context = new PipelineExecutionContext(job.Id, job.ProjectId, stage, attempt, DateTimeOffset.UtcNow);
                var result = await executor.ExecuteAsync(context, cancellationToken);
                if (result.Success)
                {
                    return (result, attempt - 1);
                }

                if (attempt > _options.MaxRetriesPerStage)
                {
                    return (result, attempt - 1);
                }
            }
            catch (Exception ex)
            {
                if (attempt > _options.MaxRetriesPerStage)
                {
                    return (StageExecutionResult.Fail(ex, ex.Message), attempt - 1);
                }
            }
        }

        return (StageExecutionResult.Fail(new InvalidOperationException("Stage failed after retries.")), _options.MaxRetriesPerStage);
    }

    private static bool IsStageCompleted(RenderJob job, PipelineStage stage) =>
        job.Checkpoints.Any(c => c.Stage == stage && c.Status == CheckpointStatus.Completed);

    private static int GetRetryCount(RenderJob job, PipelineStage stage) =>
        job.Checkpoints.FirstOrDefault(c => c.Stage == stage)?.RetryCount ?? 0;

    private static RenderJob UpdateCheckpoint(RenderJob job, PipelineStage stage, CheckpointStatus status, int retryCount, string? details)
    {
        var updatedCheckpoint = new StageCheckpoint(stage, status, retryCount, DateTimeOffset.UtcNow, details);
        var checkpoints = job.Checkpoints.Where(c => c.Stage != stage).ToList();
        checkpoints.Add(updatedCheckpoint);

        return job with
        {
            Checkpoints = checkpoints
                .OrderBy(c => Array.IndexOf(OrderedStages, c.Stage))
                .ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
