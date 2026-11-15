using System.Text.Json;
using EpisodeStudio.Core.Domain;
using EpisodeStudio.Core.Persistence;

namespace EpisodeStudio.Infrastructure.Persistence;

public sealed class FileAssetCache : IAssetCache
{
    private readonly string _baseDirectory;

    public FileAssetCache(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpisodeStudio", "assets");

        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<Asset> StoreAsync(AssetWriteRequest request, CancellationToken cancellationToken = default)
    {
        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var extension = Path.GetExtension(request.SourceFilePath);
        var targetDirectory = Path.Combine(_baseDirectory, request.ProjectId.ToString("N"), request.Kind.ToString());
        Directory.CreateDirectory(targetDirectory);

        var assetId = Guid.NewGuid();
        var fileName = $"{version}_{assetId:N}{extension}";
        var targetPath = Path.Combine(targetDirectory, fileName);

        await using (var source = File.OpenRead(request.SourceFilePath))
        await using (var target = File.Create(targetPath))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        var checksum = await CalculateChecksumAsync(targetPath, cancellationToken);

        var metadata = new AssetMetadata(
            AssetId: assetId,
            ProjectId: request.ProjectId,
            Kind: request.Kind,
            Provider: request.Provider,
            Model: request.Model,
            Provenance: request.Provenance,
            RegenerationHash: request.RegenerationHash,
            CreatedAt: DateTimeOffset.UtcNow,
            RelativePath: targetPath,
            Checksum: checksum);

        var metaPath = targetPath + ".meta.json";
        var metaJson = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(metaPath, metaJson, cancellationToken);

        return new Asset(
            metadata.AssetId,
            metadata.Kind,
            metadata.RelativePath,
            metadata.Provider,
            metadata.Model,
            metadata.CreatedAt,
            metadata.Checksum,
            metadata.Provenance);
    }

    public Task<Asset?> ResolveLatestAsync(Guid projectId, AssetKind kind, CancellationToken cancellationToken = default)
    {
        var kindDirectory = Path.Combine(_baseDirectory, projectId.ToString("N"), kind.ToString());
        if (!Directory.Exists(kindDirectory))
        {
            return Task.FromResult<Asset?>(null);
        }

        var metadataFile = Directory
            .EnumerateFiles(kindDirectory, "*.meta.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (metadataFile is null)
        {
            return Task.FromResult<Asset?>(null);
        }

        AssetMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<AssetMetadata>(File.ReadAllText(metadataFile));
        }
        catch (JsonException)
        {
            return Task.FromResult<Asset?>(null);
        }
        catch (IOException)
        {
            return Task.FromResult<Asset?>(null);
        }

        if (metadata is null || !File.Exists(metadata.RelativePath))
        {
            return Task.FromResult<Asset?>(null);
        }

        Asset asset = new(
            metadata.AssetId,
            metadata.Kind,
            metadata.RelativePath,
            metadata.Provider,
            metadata.Model,
            metadata.CreatedAt,
            metadata.Checksum,
            metadata.Provenance);

        return Task.FromResult<Asset?>(asset);
    }

    private static async Task<string> CalculateChecksumAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private sealed record AssetMetadata(
        Guid AssetId,
        Guid ProjectId,
        AssetKind Kind,
        string Provider,
        string Model,
        string Provenance,
        string RegenerationHash,
        DateTimeOffset CreatedAt,
        string RelativePath,
        string Checksum);
}
