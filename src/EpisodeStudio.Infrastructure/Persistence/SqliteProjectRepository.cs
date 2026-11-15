using System.Text.Json;
using EpisodeStudio.Core.Domain;
using EpisodeStudio.Core.Persistence;
using Microsoft.Data.Sqlite;

namespace EpisodeStudio.Infrastructure.Persistence;

public sealed class SqliteProjectRepository : IProjectRepository
{
    private readonly string _connectionString;

    public SqliteProjectRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Projects (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Script TEXT NOT NULL,
                DataJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAsync(ProjectModel project, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Projects (Id, Name, Script, DataJson, CreatedAt, UpdatedAt)
            VALUES ($id, $name, $script, $dataJson, $createdAt, $updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Script = excluded.Script,
                DataJson = excluded.DataJson,
                UpdatedAt = excluded.UpdatedAt;
            """;

        command.Parameters.AddWithValue("$id", project.Id.ToString("N"));
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$script", project.Script);
        command.Parameters.AddWithValue("$dataJson", JsonSerializer.Serialize(project));
        command.Parameters.AddWithValue("$createdAt", project.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", project.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProjectModel?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DataJson FROM Projects WHERE Id = $id";
        command.Parameters.AddWithValue("$id", projectId.ToString("N"));

        var result = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectModel>(result);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
