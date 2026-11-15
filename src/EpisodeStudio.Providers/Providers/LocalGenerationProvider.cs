using EpisodeStudio.Core.Contracts;

namespace EpisodeStudio.Providers.Providers;

public sealed class LocalGenerationProvider : IGenerationProvider
{
    private readonly HashSet<GenerationCapability> _capabilities;

    public LocalGenerationProvider(string name, IEnumerable<GenerationCapability> capabilities)
    {
        Name = name;
        _capabilities = capabilities.ToHashSet();
    }

    public string Name { get; }

    public bool Supports(GenerationCapability capability) => _capabilities.Contains(capability);

    public Task<GenerationResponse> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        var response = new GenerationResponse(
            Success: true,
            Provider: Name,
            Model: request.PreferredModel ?? "local-default",
            OutputText: request.Capability == GenerationCapability.ScriptPlanning
                ? "Local preprocessing completed."
                : "Local generation completed.",
            OutputPath: null,
            InputTokens: 0,
            OutputTokens: 0,
            Duration: TimeSpan.FromSeconds(1),
            Error: null);

        return Task.FromResult(response);
    }
}
