using System.Text.Json;
using EpisodeStudio.Core.Observability;

namespace EpisodeStudio.Infrastructure.Observability;

public sealed class JsonLineStructuredLogger : IStructuredLogger
{
    private readonly string _path;

    public JsonLineStructuredLogger(string? path = null)
    {
        _path = path
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpisodeStudio", "logs", "app.jsonl");

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Log path has no directory.");

        Directory.CreateDirectory(directory);
    }

    public Task WriteAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(logEvent) + Environment.NewLine;
        return File.AppendAllTextAsync(_path, line, cancellationToken);
    }
}
