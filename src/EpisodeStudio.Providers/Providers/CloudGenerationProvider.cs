using EpisodeStudio.Core.Contracts;

namespace EpisodeStudio.Providers.Providers;

public sealed class CloudGenerationProvider : IGenerationProvider
{
    private readonly HashSet<GenerationCapability> _capabilities;

    public CloudGenerationProvider(string name, IEnumerable<GenerationCapability> capabilities)
    {
        Name = name;
        _capabilities = capabilities.ToHashSet();
    }

    public string Name { get; }

    public bool Supports(GenerationCapability capability) => _capabilities.Contains(capability);

    public Task<GenerationResponse> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        var outputPath = request.Capability switch
        {
            GenerationCapability.StoryboardImage => $"artifacts/{request.ProjectId}/storyboard/{Guid.NewGuid():N}.png",
            GenerationCapability.VoiceSynthesis => $"artifacts/{request.ProjectId}/audio/{Guid.NewGuid():N}.wav",
            GenerationCapability.VideoGeneration => $"artifacts/{request.ProjectId}/video/{Guid.NewGuid():N}.mp4",
            _ => null
        };

        var response = new GenerationResponse(
            Success: true,
            Provider: Name,
            Model: request.PreferredModel ?? "cloud-default",
            OutputText: request.Capability == GenerationCapability.ScriptPlanning ? "Generated scene and shot plan." : null,
            OutputPath: outputPath,
            InputTokens: Math.Max(1, request.Prompt.Length / 4),
            OutputTokens: 512,
            Duration: TimeSpan.FromSeconds(2),
            Error: null);

        return Task.FromResult(response);
    }
}
