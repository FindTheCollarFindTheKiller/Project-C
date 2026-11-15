using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Pipeline.Orchestration;

public interface IPipelineStageExecutor
{
    PipelineStage Stage { get; }

    Task<StageExecutionResult> ExecuteAsync(PipelineExecutionContext context, CancellationToken cancellationToken = default);
}

public interface IRenderJobStateStore
{
    Task<RenderJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task SaveAsync(RenderJob renderJob, CancellationToken cancellationToken = default);
}
