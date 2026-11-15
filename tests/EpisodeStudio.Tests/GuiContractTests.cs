namespace EpisodeStudio.Tests;

public class GuiContractTests
{
    private const string Win32GuiPath = "Project C.cpp";

    [Fact]
    public void Win32Gui_ShouldExposeAudioTaskInTaskSelector()
    {
        var guiSource = LoadGuiSource();

        Assert.Contains("Script to Audio (Local Transformers, WAV)", guiSource, StringComparison.Ordinal);
        Assert.Contains("return L\"script_to_audio\";", guiSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Win32Gui_ShouldApplyDefaultAudioModel()
    {
        var guiSource = LoadGuiSource();

        Assert.Contains("defaultModel = L\"suno/bark-small\";", guiSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Win32Gui_ShouldAutoAlignProviderForMediaTasks()
    {
        var guiSource = LoadGuiSource();

        Assert.Contains("bool IsMediaGenerationTask(int taskIndex)", guiSource, StringComparison.Ordinal);
        Assert.Contains("if (IsMediaGenerationTask(taskIndex) && providerIndex != 2)", guiSource, StringComparison.Ordinal);
        Assert.Contains("Provider switched to Local Transformers for media generation.", guiSource, StringComparison.Ordinal);
    }

    private static string LoadGuiSource() => File.ReadAllText(ToAbsolutePath(Win32GuiPath));

    private static string ToAbsolutePath(string relativePath)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
}
