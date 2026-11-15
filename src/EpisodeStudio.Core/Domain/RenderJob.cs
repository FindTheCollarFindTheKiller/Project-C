namespace EpisodeStudio.Core.Domain;

public sealed record RenderJob(
    Guid Id,
    Guid ProjectId,
    RenderJobStatus Status,
    PipelineStage CurrentStage,
    IReadOnlyList<StageCheckpoint> Checkpoints,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    Guid? OutputAssetId);

public sealed record StageCheckpoint(
    PipelineStage Stage,
    CheckpointStatus Status,
    int RetryCount,
    DateTimeOffset LastUpdatedAt,
    string? Details);

public enum RenderJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled
}

public enum CheckpointStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

public enum PipelineStage
{
    ScriptParse,
    ScenePlan,
    StoryboardGen,
    VoiceGen,
    TimelineCompose,
    RenderExport
}
