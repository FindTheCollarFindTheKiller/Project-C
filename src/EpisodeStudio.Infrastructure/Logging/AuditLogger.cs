using System.Text.Json;

namespace EpisodeStudio.Infrastructure.Logging;

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string EventType,
    string Actor,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);

public interface IAuditLogger
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public sealed class FileAuditLogger : IAuditLogger
{
    private readonly string _auditFilePath;

    public FileAuditLogger(string? auditFilePath = null)
    {
        _auditFilePath = auditFilePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpisodeStudio", "logs", "audit.jsonl");

        var directory = Path.GetDirectoryName(_auditFilePath)
            ?? throw new InvalidOperationException("Audit log path has no directory.");

        Directory.CreateDirectory(directory);
    }

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(auditEvent);
        await File.AppendAllTextAsync(_auditFilePath, json + Environment.NewLine, cancellationToken);
    }
}
