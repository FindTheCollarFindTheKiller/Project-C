namespace EpisodeStudio.Core.Domain;

public sealed record TimelineModel(
    Guid ProjectId,
    IReadOnlyList<TimelineTrack> Tracks,
    TimeSpan TotalDuration,
    string Resolution,
    int FrameRate,
    bool IncludeSubtitles);

public sealed record TimelineTrack(
    string Name,
    TrackType TrackType,
    IReadOnlyList<TimelineClip> Clips);

public sealed record TimelineClip(
    Guid Id,
    Guid? AssetId,
    string SourcePath,
    TimeSpan Start,
    TimeSpan Duration,
    string? Transition,
    string? SubtitleText);

public enum TrackType
{
    Video,
    Voice,
    Music,
    Subtitle
}
