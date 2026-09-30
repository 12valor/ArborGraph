using System.IO;
using System.Windows;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiskScope",
        "app.log");

    public MainWindow() : this(null)
    {
    }

    public MainWindow(MainViewModel? vm)
    {
        Log("MainWindow constructor begin.");
        InitializeComponent();
        DataContext = vm ?? new MainViewModel();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Log("MainWindow constructor completed.");
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = btn.DataContext;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
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
        try
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OverviewVM.Dispose();
            }
        }
        catch { }
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
