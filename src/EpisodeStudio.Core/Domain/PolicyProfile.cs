namespace EpisodeStudio.Core.Domain;

public sealed record PolicyProfile(
    string Name,
    SafetyLevel SafetyLevel,
    bool AllowViolence,
    bool AllowSexualContent,
    bool AllowHateOrHarassment,
    bool AllowSelfHarmContent,
    bool RequireWatermark,
    IReadOnlyList<string> AllowedProviders,
    IReadOnlyList<string> AllowedModels,
    int MaxPromptLength,
    int MaxOutputSeconds);

public enum SafetyLevel
{
    Strict,
    Moderate,
    Configurable
}
