using EpisodeStudio.Core.Domain;

namespace EpisodeStudio.Core.Persistence;

public interface IProjectRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ProjectModel project, CancellationToken cancellationToken = default);
    Task<ProjectModel?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public sealed record AssetWriteRequest(
    Guid ProjectId,
    AssetKind Kind,
    string SourceFilePath,
    string Provider,
    string Model,
    string Provenance,
    string RegenerationHash);

public interface IAssetCache
{
    Task<Asset> StoreAsync(AssetWriteRequest request, CancellationToken cancellationToken = default);
    Task<Asset?> ResolveLatestAsync(Guid projectId, AssetKind kind, CancellationToken cancellationToken = default);
}
