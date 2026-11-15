using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace EpisodeStudio.Desktop;

public partial class App : Application
{
    private readonly string _crashLogPath;

    public App()
    {
        var baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpisodeStudio", "logs");
        Directory.CreateDirectory(baseDirectory);
        _crashLogPath = Path.Combine(baseDirectory, "crash.log");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PersistCrash("DispatcherUnhandledException", e.Exception);
        MessageBox.Show("A fatal error occurred. Crash details were written to the local crash log.", "EpisodeStudio", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception.");
        PersistCrash("AppDomainUnhandledException", exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        PersistCrash("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private void PersistCrash(string source, Exception exception)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTimeOffset.UtcNow:O}] Source={source}");
            sb.AppendLine(exception.ToString());
            sb.AppendLine(new string('-', 80));
            File.AppendAllText(_crashLogPath, sb.ToString());
        }
        catch
        {
            // Never throw from crash logging path.
        }
    }
}

