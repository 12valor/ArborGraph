using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DiskScope;

public partial class App : Application
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiskScope",
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
            // Prevent premature shutdown during pre-MainWindow dialogs
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settingsService = new Services.SettingsService();
            var settings = settingsService.CurrentSettings;

            if (!settings.HasAcceptedEula)
            {
                Log("EULA has not been accepted yet. Showing EulaDialog...");
                var eulaDialog = new Views.EulaDialog(isReviewMode: false);
                bool? accepted = eulaDialog.ShowDialog();

                if (accepted != true)
                {
                    Log("User declined or dismissed EULA. Terminating application.");
                    Shutdown(0);
                    return;
                }

                settings.HasAcceptedEula = true;
                settings.EulaAcceptedVersion = "1.0";
                settings.EulaAcceptedDate = DateTime.UtcNow;
                settingsService.SaveSettings(settings);
                Log("User accepted EULA v1.0. Consent recorded locally in settings.");
            }

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

    private bool _isHandlingException;

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"DispatcherUnhandledException: {e.Exception}");
        if (_isHandlingException)
        {
            e.Handled = true;
            return;
        }

        _isHandlingException = true;
        try
        {
            MessageBox.Show($"An unexpected UI error occurred:\n\n{e.Exception.Message}",
                "DiskScope", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
        finally
        {
            _isHandlingException = false;
        }
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
