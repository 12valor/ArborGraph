using System.IO;
using System.Windows;

namespace DiskScope.Views;

public partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiskScopePro",
        "app.log");

    public MainWindow()
    {
        Log("MainWindow constructor begin.");
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Log("MainWindow constructor completed.");
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log("MainWindow Loaded event fired. Window is active on desktop.");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Log("MainWindow Closing event fired.");
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Log("MainWindow Closed event fired.");
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
