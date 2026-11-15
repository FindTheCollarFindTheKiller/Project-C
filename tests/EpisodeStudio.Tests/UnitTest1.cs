using EpisodeStudio.Core.Contracts;
using EpisodeStudio.Core.Domain;
using EpisodeStudio.Core.Policy;
using EpisodeStudio.Pipeline.Media;
using EpisodeStudio.Pipeline.Orchestration;
using EpisodeStudio.Providers;
using EpisodeStudio.Providers.Providers;

namespace EpisodeStudio.Tests;

public class PolicyEngineTests
{
    [Fact]
    public void Evaluate_ShouldBlockDisallowedContent()
    {
        var profile = new PolicyProfile(
            Name: "Configurable",
            SafetyLevel: SafetyLevel.Configurable,
            AllowViolence: false,
            AllowSexualContent: false,
            AllowHateOrHarassment: false,
            AllowSelfHarmContent: false,
            RequireWatermark: true,
            AllowedProviders: ["Cloud"],
            AllowedModels: ["model-a"],
            MaxPromptLength: 2000,
            MaxOutputSeconds: 120);

        var decision = new PolicyEngine().Evaluate(new PolicyEvaluationRequest(
            profile,
            "Create a murder scene in a dark alley.",
            "Cloud",
            "model-a",
            60));

        Assert.False(decision.IsAllowed);
        Assert.Contains(decision.Violations, v => v.Contains("Violence", StringComparison.OrdinalIgnoreCase));
    }
}

public class ProviderRoutingTests
{
    [Fact]
    public async Task Router_ShouldRespectPreferredProvider_WhenSupported()
    {
        var cloud = new CloudGenerationProvider("Cloud", [GenerationCapability.ScriptPlanning, GenerationCapability.StoryboardImage]);
        var local = new LocalGenerationProvider("Local", [GenerationCapability.ScriptPlanning]);
        var router = new GenerationProviderRouter([cloud, local]);

        var response = await router.GenerateAsync(new GenerationRequest(
            Guid.NewGuid(),
            GenerationCapability.ScriptPlanning,
            "Plan a 3-scene detective episode",
            new Dictionary<string, string>(),
            PreferredProvider: "Local",
            PreferredModel: "local-default",
            CorrelationId: Guid.NewGuid().ToString("N")));

        Assert.Equal("Local", response.Provider);
    }
}

public class OrchestrationTests
{
    [Fact]
    public async Task RunAsync_ShouldResumeAndCompleteStages()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var initialJob = new RenderJob(
            Id: jobId,
            ProjectId: projectId,
            Status: RenderJobStatus.Queued,
            CurrentStage: PipelineStage.ScriptParse,
            Checkpoints: [new StageCheckpoint(PipelineStage.ScriptParse, CheckpointStatus.Completed, 0, DateTimeOffset.UtcNow, "done")],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            StartedAt: null,
            CompletedAt: null,
            Error: null,
            OutputAssetId: null);

        var store = new InMemoryRenderJobStateStore();
        await store.SaveAsync(initialJob);

        var executors = Enum.GetValues<PipelineStage>()
            .Select(s => new SuccessExecutor(s))
            .Cast<IPipelineStageExecutor>()
            .ToList();

        var orchestrator = new RenderPipelineOrchestrator(executors, store, new PipelineOptions(MaxRetriesPerStage: 1));
        var finalJob = await orchestrator.RunAsync(jobId);

        Assert.Equal(RenderJobStatus.Completed, finalJob.Status);
        Assert.All(finalJob.Checkpoints, cp => Assert.Equal(CheckpointStatus.Completed, cp.Status));
    }

    private sealed class SuccessExecutor(PipelineStage stage) : IPipelineStageExecutor
    {
        public PipelineStage Stage { get; } = stage;

        public Task<StageExecutionResult> ExecuteAsync(PipelineExecutionContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(StageExecutionResult.Ok("ok"));
    }

    [Fact]
    public async Task RunAsync_WhenStageFails_ShouldRecordActualRetryCount()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var initialJob = new RenderJob(
            Id: jobId,
            ProjectId: projectId,
            Status: RenderJobStatus.Queued,
            CurrentStage: PipelineStage.ScriptParse,
            Checkpoints: [],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            StartedAt: null,
            CompletedAt: null,
            Error: null,
            OutputAssetId: null);

        var store = new InMemoryRenderJobStateStore();
        await store.SaveAsync(initialJob);

        var executors = Enum.GetValues<PipelineStage>()
            .Select(s => s == PipelineStage.ScenePlan
                ? new FailingExecutor(s)
                : new SuccessExecutor(s) as IPipelineStageExecutor)
            .ToList();

        var orchestrator = new RenderPipelineOrchestrator(executors, store, new PipelineOptions(MaxRetriesPerStage: 1));
        var finalJob = await orchestrator.RunAsync(jobId);

        Assert.Equal(RenderJobStatus.Failed, finalJob.Status);
        var failedCheckpoint = finalJob.Checkpoints.Single(c => c.Stage == PipelineStage.ScenePlan);
        Assert.Equal(1, failedCheckpoint.RetryCount);
    }

    [Fact]
    public async Task RunAsync_WhenStageExecutorMissing_ShouldThrow()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var initialJob = new RenderJob(
            Id: jobId,
            ProjectId: projectId,
            Status: RenderJobStatus.Queued,
            CurrentStage: PipelineStage.ScriptParse,
            Checkpoints: [],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            StartedAt: null,
            CompletedAt: null,
            Error: null,
            OutputAssetId: null);

        var store = new InMemoryRenderJobStateStore();
        await store.SaveAsync(initialJob);

        var executors = Enum.GetValues<PipelineStage>()
            .Where(s => s != PipelineStage.ScenePlan)
            .Select(s => new SuccessExecutor(s))
            .Cast<IPipelineStageExecutor>()
            .ToList();

        var orchestrator = new RenderPipelineOrchestrator(executors, store, new PipelineOptions(MaxRetriesPerStage: 1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.RunAsync(jobId));
        Assert.Contains("No executor registered for stage 'ScenePlan'", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FailingExecutor(PipelineStage stage) : IPipelineStageExecutor
    {
        public PipelineStage Stage { get; } = stage;

        public Task<StageExecutionResult> ExecuteAsync(PipelineExecutionContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(StageExecutionResult.Fail(new InvalidOperationException("boom"), "boom"));
    }
}

public class FfmpegCompositorTests
{
    [Fact]
    public void BuildArguments_ShouldGenerateGoldenCommandShape()
    {
        var timeline = new TimelineModel(
            ProjectId: Guid.NewGuid(),
            Tracks:
            [
                new TimelineTrack("Video", TrackType.Video,
                [
                    new TimelineClip(Guid.NewGuid(), null, "shot1.png", TimeSpan.Zero, TimeSpan.FromSeconds(3), "fade", "Hello"),
                    new TimelineClip(Guid.NewGuid(), null, "shot2.png", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), "fade", "World")
                ]),
                new TimelineTrack("Voice", TrackType.Voice,
                [new TimelineClip(Guid.NewGuid(), null, "narration.wav", TimeSpan.Zero, TimeSpan.FromSeconds(6), null, null)])
            ],
            TotalDuration: TimeSpan.FromSeconds(6),
            Resolution: "1920x1080",
            FrameRate: 24,
            IncludeSubtitles: true);

        var compositor = new FfmpegCompositor();
        var args = compositor.BuildArguments(new ComposeRequest(timeline, ".", "out.mp4"));

        Assert.Contains("-filter_complex", args);
        Assert.Contains("concat=n=2", args);
        Assert.Contains("-r 24", args);
        Assert.Contains("-s 1920x1080", args);
        Assert.Contains("subtitles=subtitles.srt", args);
        Assert.Contains("out.mp4", args);
    }

    [Fact]
    public void BuildArguments_ShouldThrowForEmptyVideoTrack()
    {
        var timeline = new TimelineModel(
            ProjectId: Guid.NewGuid(),
            Tracks:
            [
                new TimelineTrack("Video", TrackType.Video, []),
                new TimelineTrack("Voice", TrackType.Voice,
                [new TimelineClip(Guid.NewGuid(), null, "narration.wav", TimeSpan.Zero, TimeSpan.FromSeconds(6), null, null)])
            ],
            TotalDuration: TimeSpan.FromSeconds(6),
            Resolution: "1920x1080",
            FrameRate: 24,
            IncludeSubtitles: false);

        var compositor = new FfmpegCompositor();
        Assert.Throws<InvalidOperationException>(() => compositor.BuildArguments(new ComposeRequest(timeline, ".", "out.mp4")));
    }

    [Fact]
    public void BuildArguments_WithSubtitles_ShouldMapFilteredVideo()
    {
        var timeline = new TimelineModel(
            ProjectId: Guid.NewGuid(),
            Tracks:
            [
                new TimelineTrack("Video", TrackType.Video,
                [new TimelineClip(Guid.NewGuid(), null, "shot1.png", TimeSpan.Zero, TimeSpan.FromSeconds(3), null, "Hello")])
            ],
            TotalDuration: TimeSpan.FromSeconds(3),
            Resolution: "1920x1080",
            FrameRate: 24,
            IncludeSubtitles: true);

        var compositor = new FfmpegCompositor();
        var args = compositor.BuildArguments(new ComposeRequest(timeline, ".", "out.mp4"));

        Assert.Contains("-map \"[vsub]\"", args);
    }
}
