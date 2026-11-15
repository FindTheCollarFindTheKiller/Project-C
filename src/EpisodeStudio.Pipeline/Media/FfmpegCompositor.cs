using System.Diagnostics;
using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Pipeline.Media;

public sealed record ComposeRequest(
    TimelineModel Timeline,
    string WorkingDirectory,
    string OutputPath,
    string FfmpegPath = "ffmpeg");

public interface IMediaCompositor
{
    string BuildArguments(ComposeRequest request);
    Task<int> ComposeAsync(ComposeRequest request, CancellationToken cancellationToken = default);
}

public sealed class FfmpegCompositor : IMediaCompositor
{
    public string BuildArguments(ComposeRequest request)
    {
        var videoTrack = request.Timeline.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Video);
        var voiceTrack = request.Timeline.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Voice);
        var musicTrack = request.Timeline.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Music);

        if (videoTrack is not null && videoTrack.Clips.Count == 0)
        {
            throw new InvalidOperationException("Video track exists but has no clips.");
        }

        if (voiceTrack is not null && voiceTrack.Clips.Count == 0)
        {
            throw new InvalidOperationException("Voice track exists but has no clips.");
        }

        if (musicTrack is not null && musicTrack.Clips.Count == 0)
        {
            throw new InvalidOperationException("Music track exists but has no clips.");
        }

        var inputs = new List<string>();
        var filterComplex = new List<string>();

        if (videoTrack is not null)
        {
            for (var i = 0; i < videoTrack.Clips.Count; i++)
            {
                inputs.Add($"-i \"{videoTrack.Clips[i].SourcePath}\"");
                filterComplex.Add($"[{i}:v]setpts=PTS-STARTPTS[v{i}]");
            }

            if (videoTrack.Clips.Count > 1)
            {
                filterComplex.Add(string.Join(";", BuildConcatFilters(videoTrack.Clips.Count, "v", "vout", "v=1:a=0")));
            }
        }

        var nextInputIndex = inputs.Count;

        if (voiceTrack is not null)
        {
            inputs.Add($"-i \"{voiceTrack.Clips.First().SourcePath}\"");
        }

        if (musicTrack is not null)
        {
            inputs.Add($"-i \"{musicTrack.Clips.First().SourcePath}\"");
        }

        var mapVideo = videoTrack is null
            ? string.Empty
            : videoTrack.Clips.Count == 1
                ? "-map 0:v"
                : "-map \"[vout]\"";
        var mapAudio = string.Empty;

        if (voiceTrack is not null && musicTrack is not null)
        {
            filterComplex.Add($"[{nextInputIndex}:a][{nextInputIndex + 1}:a]amix=inputs=2:duration=longest[aout]");
            mapAudio = "-map \"[aout]\"";
        }
        else if (voiceTrack is not null)
        {
            mapAudio = $"-map {nextInputIndex}:a";
        }
        else if (musicTrack is not null)
        {
            var musicIndex = voiceTrack is null ? nextInputIndex : nextInputIndex + 1;
            mapAudio = $"-map {musicIndex}:a";
        }

        if (request.Timeline.IncludeSubtitles)
        {
            if (videoTrack is null)
            {
                throw new InvalidOperationException("Subtitles require a video track.");
            }

            if (videoTrack.Clips.Count == 1)
            {
                filterComplex.Add("[0:v]subtitles=subtitles.srt[vsub]");
            }
            else
            {
                filterComplex.Add("[vout]subtitles=subtitles.srt[vsub]");
            }
        }

        var filterArgument = filterComplex.Count > 0
            ? $"-filter_complex \"{string.Join(';', filterComplex)}\""
            : string.Empty;

        if (request.Timeline.IncludeSubtitles)
        {
            mapVideo = "-map \"[vsub]\"";
        }

        return $"{string.Join(' ', inputs)} {filterArgument} {mapVideo} {mapAudio} -r {request.Timeline.FrameRate} -s {request.Timeline.Resolution} -y \"{request.OutputPath}\"";
    }

    public async Task<int> ComposeAsync(ComposeRequest request, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(request.WorkingDirectory);
        var arguments = BuildArguments(request);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = request.FfmpegPath,
            Arguments = arguments,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Unable to start ffmpeg process.");

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private static IEnumerable<string> BuildConcatFilters(int count, string prefix, string output, string parameters)
    {
        var inputs = string.Join(string.Empty, Enumerable.Range(0, count).Select(i => $"[{prefix}{i}]"));
        yield return $"{inputs}concat=n={count}:{parameters}[{output}]";
    }
}
