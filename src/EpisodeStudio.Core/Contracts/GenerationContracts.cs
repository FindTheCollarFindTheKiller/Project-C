namespace EpisodeStudio.Core.Contracts;

public sealed record GenerationRequest(
    Guid ProjectId,
    GenerationCapability Capability,
    string Prompt,
    IReadOnlyDictionary<string, string> Parameters,
    string? PreferredProvider,
    string? PreferredModel,
    string CorrelationId);

public sealed record GenerationResponse(
    bool Success,
    string Provider,
    string Model,
    string? OutputText,
    string? OutputPath,
    int InputTokens,
    int OutputTokens,
    TimeSpan Duration,
    string? Error);

public enum GenerationCapability
{
    ScriptPlanning,
    StoryboardImage,
    VoiceSynthesis,
    MusicSuggestion,
    VideoGeneration
}

public interface IGenerationProvider
{
    string Name { get; }
    bool Supports(GenerationCapability capability);
    Task<GenerationResponse> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default);
}
