using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Pipeline.Orchestration;

public sealed record PipelineExecutionContext(
    Guid JobId,
    Guid ProjectId,
    PipelineStage Stage,
    int Attempt,
    DateTimeOffset StartedAt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record StageExecutionResult(
    bool Success,
    string? Details = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    Exception? Exception = null)
{
    public static StageExecutionResult Ok(string? details = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        new(true, details, metadata);

    public static StageExecutionResult Fail(Exception exception, string? details = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        new(false, details, metadata, exception);
}

public sealed record PipelineOptions(int MaxRetriesPerStage = 2);
