namespace EpisodeStudio.Core.Domain;

public sealed record ProjectModel(
    Guid Id,
    string Name,
    string Script,
    IReadOnlyList<Scene> Scenes,
    IReadOnlyList<Asset> Assets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PolicyProfile PolicyProfile,
    decimal CloudBudgetLimitUsd,
    decimal CloudSpendToDateUsd);

public sealed record Scene(
    Guid Id,
    int Sequence,
    string Title,
    string Description,
    IReadOnlyList<Shot> Shots,
    TimeSpan EstimatedDuration);

public sealed record Shot(
    Guid Id,
    int Sequence,
    string Prompt,
    string CameraDirection,
    TimeSpan Duration,
    Guid? StoryboardAssetId,
    Guid? VoiceTrackId);

public sealed record Asset(
    Guid Id,
    AssetKind Kind,
    string RelativePath,
    string Provider,
    string Model,
    DateTimeOffset CreatedAt,
    string Checksum,
    string Provenance);

public enum AssetKind
{
    StoryboardImage,
    GeneratedVideo,
    VoiceTrack,
    MusicTrack,
    Subtitle,
    ProjectFile
}

public sealed record VoiceTrack(
    Guid Id,
    Guid SceneId,
    string VoiceName,
    string Language,
    string Text,
    TimeSpan Duration,
    Guid AssetId);
