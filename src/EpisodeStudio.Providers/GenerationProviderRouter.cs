using EpisodeStudio.Core.Contracts;

namespace EpisodeStudio.Providers;

public sealed class GenerationProviderRouter
{
    private readonly IReadOnlyDictionary<string, IGenerationProvider> _providersByName;

    public GenerationProviderRouter(IEnumerable<IGenerationProvider> providers)
    {
        _providersByName = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IGenerationProvider Resolve(GenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PreferredProvider)
            && _providersByName.TryGetValue(request.PreferredProvider, out var preferred)
            && preferred.Supports(request.Capability))
        {
            return preferred;
        }

        var firstSupportingProvider = _providersByName.Values.FirstOrDefault(p => p.Supports(request.Capability));
        if (firstSupportingProvider is null)
        {
            throw new InvalidOperationException($"No provider supports capability '{request.Capability}'.");
        }

        return firstSupportingProvider;
    }

    public Task<GenerationResponse> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        var provider = Resolve(request);
        return provider.GenerateAsync(request, cancellationToken);
    }
}
