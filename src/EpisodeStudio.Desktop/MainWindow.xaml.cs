using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace EpisodeStudio.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private int _sceneCounter = 2;
    private string _projectName = "Episode 1";
    private string _budgetUsd = "50";
    private string _scriptText = "INT. CITY STREET - NIGHT\nA detective follows a glowing collar through heavy rain...";
    private double _pipelineProgress;
    private string _statusMessage = "Ready.";
    private string _videoModel = "cerspense/zeroscope_v2_576w";
    private string _offloadMode = "sequential";
    private string _precision = "fp16";
    private string _videoWidth = "576";
    private string _videoHeight = "320";
    private string _numFrames = "16";
    private string _inferenceSteps = "20";
    private string _guidanceScale = "7.0";
    private string _fps = "8";
    private string _seed = string.Empty;

    public MainWindow()
    {
        Scenes =
        [
            new SceneRow { Sequence = 1, Title = "Cold Open", Description = "Detective receives mysterious signal.", ShotCount = 4, DurationSeconds = 35 }
        ];

        ProgressLog =
        [
            "Pipeline idle.",
            "Waiting for script input."
        ];

        Scenes.CollectionChanged += Scenes_CollectionChanged;

        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SceneRow> Scenes { get; }

    public ObservableCollection<string> ProgressLog { get; }

    public IReadOnlyList<string> OffloadModes { get; } = ["sequential", "model", "none"];

    public IReadOnlyList<string> PrecisionModes { get; } = ["fp16", "fp32"];

    public string ProjectName
    {
        get => _projectName;
        set => SetField(ref _projectName, value);
    }

    public string BudgetUsd
    {
        get => _budgetUsd;
        set => SetField(ref _budgetUsd, value);
    }

    public string ScriptText
    {
        get => _scriptText;
        set
        {
            if (SetField(ref _scriptText, value))
            {
                OnPropertyChanged(nameof(ScriptStats));
                OnPropertyChanged(nameof(CanGenerateScenePlan));
            }
        }
    }

    public double PipelineProgress
    {
        get => _pipelineProgress;
        set
        {
            if (SetField(ref _pipelineProgress, value))
            {
                OnPropertyChanged(nameof(PipelineProgressText));
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string ScriptStats => $"{ScriptText.Length} characters";

    public string SceneSummary => $"{Scenes.Count} scenes • {Scenes.Sum(scene => scene.DurationSeconds)}s total";

    public string PipelineProgressText => $"{PipelineProgress:0}% complete";

    public string VideoModel
    {
        get => _videoModel;
        set => SetVideoSetting(ref _videoModel, value);
    }

    public string OffloadMode
    {
        get => _offloadMode;
        set => SetVideoSetting(ref _offloadMode, value);
    }

    public string Precision
    {
        get => _precision;
        set => SetVideoSetting(ref _precision, value);
    }

    public string VideoWidth
    {
        get => _videoWidth;
        set => SetVideoSetting(ref _videoWidth, value);
    }

    public string VideoHeight
    {
        get => _videoHeight;
        set => SetVideoSetting(ref _videoHeight, value);
    }

    public string NumFrames
    {
        get => _numFrames;
        set => SetVideoSetting(ref _numFrames, value);
    }

    public string InferenceSteps
    {
        get => _inferenceSteps;
        set => SetVideoSetting(ref _inferenceSteps, value);
    }

    public string GuidanceScale
    {
        get => _guidanceScale;
        set => SetVideoSetting(ref _guidanceScale, value);
    }

    public string Fps
    {
        get => _fps;
        set => SetVideoSetting(ref _fps, value);
    }

    public string Seed
    {
        get => _seed;
        set => SetVideoSetting(ref _seed, value);
    }

    public string VideoConfigSummary => $"{VideoWidth}x{VideoHeight} • {NumFrames} frames/scene • offload {OffloadMode} • {Precision}";

    public bool CanGenerateScenePlan => !string.IsNullOrWhiteSpace(ScriptText);

    public bool CanStartGeneration => Scenes.Count > 0;

    public bool CanExport => PipelineProgress >= 90;

    private void GenerateScenePlan_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ScriptText))
        {
            StatusMessage = "Script is empty. Add script content before generating a scene plan.";
            return;
        }

        if (!decimal.TryParse(BudgetUsd, out var budget) || budget <= 0)
        {
            StatusMessage = "Enter a valid budget value (greater than 0).";
            return;
        }

        ProgressLog.Add("ScenePlan generated from script.");
        PipelineProgress = Math.Max(PipelineProgress, 20);
        StatusMessage = "Scene plan generated.";
    }

    private void ClearScript_Click(object sender, RoutedEventArgs e)
    {
        ScriptText = string.Empty;
        StatusMessage = "Script cleared.";
    }

    private void AddScene_Click(object sender, RoutedEventArgs e)
    {
        Scenes.Add(new SceneRow
        {
            Sequence = _sceneCounter,
            Title = $"Scene {_sceneCounter}",
            Description = "Edit scene description.",
            ShotCount = 3,
            DurationSeconds = 30
        });

        _sceneCounter++;
        StatusMessage = "Scene added.";
    }

    private void GenerateStoryboard_Click(object sender, RoutedEventArgs e)
    {
        if (!CanStartGeneration)
        {
            StatusMessage = "Add at least one scene before generating storyboards.";
            return;
        }

        ProgressLog.Add("StoryboardGen completed for reviewed scenes.");
        PipelineProgress = Math.Max(PipelineProgress, 45);
        StatusMessage = "Storyboards generated.";
    }

    private void GenerateVoice_Click(object sender, RoutedEventArgs e)
    {
        ProgressLog.Add("VoiceGen completed for narration/dialog tracks.");
        PipelineProgress = Math.Max(PipelineProgress, 65);
        StatusMessage = "Voice tracks generated.";
    }

    private void StartGeneration_Click(object sender, RoutedEventArgs e)
    {
        if (!CanStartGeneration)
        {
            StatusMessage = "No scenes available. Generate or add scenes before starting full generation.";
            return;
        }

        if (!TryValidateVideoConfig(out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        ProgressLog.Add("TimelineCompose completed.");
        ProgressLog.Add($"Video config queued: model={VideoModel}, offload={OffloadMode}, precision={Precision}, size={VideoWidth}x{VideoHeight}, frames={NumFrames}.");
        ProgressLog.Add("RenderExport queued.");
        PipelineProgress = 90;
        StatusMessage = "Render started with current local video settings.";
    }

    private void RetryStage_Click(object sender, RoutedEventArgs e)
    {
        ProgressLog.Add("Retry requested for failed stage.");
        StatusMessage = "Retry queued.";
    }

    private void ResetVideoSettings_Click(object sender, RoutedEventArgs e)
    {
        VideoModel = "cerspense/zeroscope_v2_576w";
        OffloadMode = "sequential";
        Precision = "fp16";
        VideoWidth = "576";
        VideoHeight = "320";
        NumFrames = "16";
        InferenceSteps = "20";
        GuidanceScale = "7.0";
        Fps = "8";
        Seed = string.Empty;
        StatusMessage = "Local video settings reset to defaults.";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!CanExport)
        {
            StatusMessage = "Complete generation steps before exporting.";
            return;
        }

        PipelineProgress = 100;
        ProgressLog.Add("MP4 export completed.");
        StatusMessage = "Export finished.";
    }

    private void Scenes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SceneSummary));
        OnPropertyChanged(nameof(CanStartGeneration));
    }

    private bool TryValidateVideoConfig(out string message)
    {
        if (string.IsNullOrWhiteSpace(VideoModel))
        {
            message = "Video model cannot be empty.";
            return false;
        }

        if (!int.TryParse(VideoWidth, out var width) || width <= 0
            || !int.TryParse(VideoHeight, out var height) || height <= 0
            || !int.TryParse(NumFrames, out var frames) || frames <= 0
            || !int.TryParse(InferenceSteps, out var steps) || steps <= 0
            || !int.TryParse(Fps, out var fps) || fps <= 0)
        {
            message = "Width, height, frames, steps, and fps must be positive numbers.";
            return false;
        }

        if (!double.TryParse(GuidanceScale, out var guidance) || guidance < 0)
        {
            message = "Guidance scale must be a number greater than or equal to 0.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Seed) && !int.TryParse(Seed, out _))
        {
            message = "Seed must be empty or a whole number.";
            return false;
        }

        message = "Video settings look good.";
        return true;
    }

    private void SetVideoSetting(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (SetField(ref field, value, propertyName))
        {
            OnPropertyChanged(nameof(VideoConfigSummary));
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class SceneRow
{
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ShotCount { get; set; }
    public int DurationSeconds { get; set; }
}
