using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace DiskScope;

public partial class App : Application
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArborGraph",
        "app.log");

    // Single-instance coordination (shared by every ArborGraph build for the current user session,
    // since they all share the same SQLite index under %LOCALAPPDATA%\ArborGraph).
    private const string InstanceMutexName = @"Local\ArborGraph.SingleInstance";
    private const string ActivateEventName = @"Local\ArborGraph.Activate";
    private const string ActivateAckEventName = @"Local\ArborGraph.ActivateAck";
    private const int ActivationAckTimeoutMs = 10000;

    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWait;
    private SplashScreen? _splash;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("Application OnStartup begin.");
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        Exit += App_Exit;

        // Prevent premature shutdown during pre-MainWindow dialogs
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!AcquireSingleInstance())
        {
            Log("Another ArborGraph instance handled this launch. Exiting.");
            Shutdown(0);
            return;
        }

        ListenForActivationRequests();
        ShowSplash();

        try
        {

            var settingsService = new Services.SettingsService();
            var settings = settingsService.CurrentSettings;

            if (!settings.HasAcceptedEula)
            {
                Log("EULA has not been accepted yet. Showing EulaDialog...");
                CloseSplash();
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
            Log("Step 1: Instantiating MainViewModel...");
            var mainVm = new ViewModels.MainViewModel(null, null, null, settingsService);
            Log("Step 2: MainViewModel instantiated successfully.");
            Log("Step 3: Instantiating MainWindow...");
            var win = new Views.MainWindow(mainVm);
            Log("Step 4: MainWindow instantiated successfully.");
            MainWindow = win;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            win.ContentRendered += (_, _) => CloseSplash();
            Log("Step 5: Showing MainWindow...");
            win.Show();
            win.Activate();
            Log("MainWindow shown successfully.");
        }
        catch (Exception ex)
        {
            Log($"FATAL: Exception creating/showing MainWindow: {ex}");
            CloseSplash();
            MessageBox.Show($"Startup failure:\n\n{ex}", "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
            // ShutdownMode is still OnExplicitShutdown here; without this the process
            // would linger invisibly and block future launches.
            Shutdown(1);
        }
    }

    /// <summary>
    /// Ensures only one ArborGraph process runs per user session. A second launch asks the
    /// running instance to come to the foreground. If the running instance does not respond
    /// (stuck/hung with no window), the user is offered to end it and start fresh.
    /// </summary>
    /// <returns>true if this process should continue starting up.</returns>
    private bool AcquireSingleInstance()
    {
        try
        {
            _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool createdNew);
            _ownsInstanceMutex = createdNew || TryTakeMutex(0);
            if (_ownsInstanceMutex)
            {
                return true;
            }

            Log("Existing ArborGraph instance detected. Requesting activation...");
            try { AllowSetForegroundWindow(ASFW_ANY); } catch { }

            using var ack = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateAckEventName);
            if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var activate))
            {
                using (activate)
                {
                    activate.Set();
                }

                if (ack.WaitOne(ActivationAckTimeoutMs))
                {
                    Log("Existing instance acknowledged activation.");
                    return false;
                }
            }

            Log("Existing instance did not respond to activation request.");
            var others = GetOtherInstances();
            if (others.Count == 0)
            {
                // Owner vanished between checks; try to take over.
                _ownsInstanceMutex = TryTakeMutex(5000);
                return _ownsInstanceMutex;
            }

            var answer = MessageBox.Show(
                "ArborGraph is already running but is not responding.\n\n" +
                "Close the unresponsive instance and start ArborGraph again?",
                "ArborGraph",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return false;
            }

            foreach (var p in others)
            {
                try
                {
                    Log($"Terminating unresponsive instance PID {p.Id}.");
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    Log($"Failed to terminate PID {p.Id}: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }

            _ownsInstanceMutex = TryTakeMutex(5000);
            if (!_ownsInstanceMutex)
            {
                MessageBox.Show("ArborGraph could not close the existing instance. Please end ArborGraph.exe in Task Manager and try again.",
                    "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return _ownsInstanceMutex;
        }
        catch (Exception ex)
        {
            // Never block startup because of the guard itself.
            Log($"Single-instance check failed, continuing startup: {ex.Message}");
            return true;
        }
    }

    private bool TryTakeMutex(int timeoutMs)
    {
        if (_instanceMutex == null) return false;
        try
        {
            return _instanceMutex.WaitOne(timeoutMs);
        }
        catch (AbandonedMutexException)
        {
            // Previous owner crashed or was killed; ownership transfers to us.
            return true;
        }
    }

    private static List<Process> GetOtherInstances()
    {
        var current = Process.GetCurrentProcess();
        var result = new List<Process>();
        foreach (var name in new[] { current.ProcessName, "ArborGraph" }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                if (p.Id == current.Id || result.Any(r => r.Id == p.Id))
                {
                    p.Dispose();
                    continue;
                }
                result.Add(p);
            }
        }
        return result;
    }

    private void ListenForActivationRequests()
    {
        try
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _activateWait = ThreadPool.RegisterWaitForSingleObject(
                _activateEvent,
                (_, _) => Dispatcher.BeginInvoke(new Action(BringToForeground)),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);
        }
        catch (Exception ex)
        {
            Log($"Activation listener could not be registered: {ex.Message}");
        }
    }

    private void BringToForeground()
    {
        try
        {
            Window? w = MainWindow;
            if (w == null || !w.IsLoaded)
            {
                w = Windows.OfType<Window>().FirstOrDefault(x => x.IsVisible);
            }

            if (w != null)
            {
                if (!w.IsVisible) w.Show();
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();
                w.Topmost = true;
                w.Topmost = false;
                w.Focus();
            }
            Log("Activation request from a second launch handled.");
        }
        catch (Exception ex)
        {
            Log($"BringToForeground failed: {ex.Message}");
        }
        finally
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(ActivateAckEventName, out var ack))
                {
                    using (ack) ack.Set();
                }
            }
            catch { }
        }
    }

    private void ShowSplash()
    {
        try
        {
            // Native layered window: paints immediately even while the UI thread is busy loading the index.
            _splash = new SplashScreen(typeof(App).Assembly, "Resources/splash.png");
            _splash.Show(autoClose: false, topMost: false);
        }
        catch (Exception ex)
        {
            _splash = null;
            Log($"Splash screen unavailable: {ex.Message}");
        }
    }

    private void CloseSplash()
    {
        var splash = _splash;
        _splash = null;
        if (splash == null) return;
        try { splash.Close(TimeSpan.FromMilliseconds(150)); } catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _activateWait?.Unregister(null); } catch { }
        try { _activateEvent?.Dispose(); } catch { }
        if (_ownsInstanceMutex)
        {
            try { _instanceMutex?.ReleaseMutex(); } catch { }
        }
        try { _instanceMutex?.Dispose(); } catch { }
        base.OnExit(e);
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
                "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
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
                "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log($"TaskScheduler_UnobservedTaskException: {e.Exception}");
        e.SetObserved();
    }

    public static void Log(string message)
    {
        try
        {
            string? dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 5 * 1024 * 1024)
            {
                try
                {
                    string oldFile = LogFile + ".old";
                    if (File.Exists(oldFile)) File.Delete(oldFile);
                    File.Move(LogFile, oldFile);
                }
                catch { }
            }
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
