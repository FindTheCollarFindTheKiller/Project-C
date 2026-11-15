using EpisodeStudio.Core.Contracts;

namespace EpisodeStudio.Providers;

public sealed record ProviderDescriptor(
    string Name,
    bool IsLocal,
    IReadOnlyCollection<GenerationCapability> Capabilities,
    bool RequiresApiKey);

public interface IProviderCatalog
{
    IReadOnlyCollection<ProviderDescriptor> ListProviders();
}

public sealed class ProviderCatalog : IProviderCatalog
{
    private readonly IReadOnlyCollection<ProviderDescriptor> _providers;

    public ProviderCatalog(IEnumerable<ProviderDescriptor> providers)
    {
        _providers = providers.ToList();
    }

    public IReadOnlyCollection<ProviderDescriptor> ListProviders() => _providers;
}
