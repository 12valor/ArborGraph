using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DiskScope;

public partial class App : Application
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiskScopePro",
        "app.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("Application OnStartup begin.");
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        Exit += App_Exit;

        try
        {
            Log("Creating MainWindow...");
            var win = new Views.MainWindow();
            MainWindow = win;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            Log("Showing MainWindow...");
            win.Show();
            Log("MainWindow shown successfully.");
        }
        catch (Exception ex)
        {
            Log($"FATAL: Exception creating/showing MainWindow: {ex}");
            MessageBox.Show($"Startup failure:\n\n{ex}", "DiskScope", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        Log($"Application Exiting with code {e.ApplicationExitCode}.");
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"DispatcherUnhandledException: {e.Exception}");
        MessageBox.Show($"An unexpected UI error occurred:\n\n{e.Exception.Message}",
            "DiskScope", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log($"CurrentDomain_UnhandledException: {e.ExceptionObject}");
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"A critical error occurred:\n\n{ex.Message}",
                "DiskScope", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log($"TaskScheduler_UnobservedTaskException: {e.Exception}");
        e.SetObserved();
    }

    private static void Log(string message)
    {
        try
        {
            string? dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
