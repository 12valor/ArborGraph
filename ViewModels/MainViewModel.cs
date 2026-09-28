using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly ScannerService _scannerService;
    private readonly DiskService _diskService;
    private readonly FileActionService _fileActionService;
    private readonly DuplicateAnalyzer _duplicateAnalyzer;

    private object _currentView;
    private string _currentTab = "Overview";
    private bool _isScanning;
    private CancellationTokenSource? _scanCts;
    private string _customScanPath = string.Empty;

    public MainViewModel()
    {
        _dbService = new DatabaseService();
        _dbService.Initialize();

        _scannerService = new ScannerService(_dbService);
        _diskService = new DiskService();
        _fileActionService = new FileActionService();
        _duplicateAnalyzer = new DuplicateAnalyzer(_dbService);

        OverviewVM = new OverviewViewModel(_dbService, _diskService);
        LargestFilesVM = new LargestFilesViewModel(_dbService, _fileActionService);
        LargestFoldersVM = new LargestFoldersViewModel(_dbService, _fileActionService);
        FileTypesVM = new FileTypesViewModel(_dbService);
        OldFilesVM = new OldFilesViewModel(_dbService, _fileActionService);
        DuplicatesVM = new DuplicateViewModel(_duplicateAnalyzer, _fileActionService);
        PhotoshopVM = new PhotoshopViewModel(_dbService, _fileActionService);
        ScanLogVM = new ScanLogViewModel();

        _currentView = OverviewVM;

        StartScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning);
        StopScanCommand = new RelayCommand(_ => StopScan(), _ => IsScanning);
        NavigateCommand = new RelayCommand(param => NavigateTo(param?.ToString() ?? "Overview"));
        BrowseCustomFolderCommand = new RelayCommand(_ => BrowseCustomFolder());
    }

    public OverviewViewModel OverviewVM { get; }
    public LargestFilesViewModel LargestFilesVM { get; }
    public LargestFoldersViewModel LargestFoldersVM { get; }
    public FileTypesViewModel FileTypesVM { get; }
    public OldFilesViewModel OldFilesVM { get; }
    public DuplicateViewModel DuplicatesVM { get; }
    public PhotoshopViewModel PhotoshopVM { get; }
    public ScanLogViewModel ScanLogVM { get; }

    public object CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string CurrentTab
    {
        get => _currentTab;
        set => SetProperty(ref _currentTab, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                (StartScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string CustomScanPath
    {
        get => _customScanPath;
        set => SetProperty(ref _customScanPath, value);
    }

    public ICommand StartScanCommand { get; }
    public ICommand StopScanCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand BrowseCustomFolderCommand { get; }

    public void NavigateTo(string tabName)
    {
        CurrentTab = tabName;
        CurrentView = tabName switch
        {
            "LargestFiles" => LargestFilesVM,
            "LargestFolders" => LargestFoldersVM,
            "FileTypes" => FileTypesVM,
            "OldFiles" => OldFilesVM,
            "Duplicates" => DuplicatesVM,
            "Photoshop" => PhotoshopVM,
            "ScanLog" => ScanLogVM,
            _ => OverviewVM
        };

        // Auto-refresh target view if not scanning
        if (!IsScanning)
        {
            RefreshCurrentView(tabName);
        }
    }

    private void RefreshCurrentView(string tabName)
    {
        try
        {
            switch (tabName)
            {
                case "LargestFiles":
                    LargestFilesVM.RefreshData();
                    break;
                case "LargestFolders":
                    LargestFoldersVM.RefreshData(OverviewVM.Stats.LogicalBytesIndexed);
                    break;
                case "FileTypes":
                    FileTypesVM.RefreshData();
                    break;
                case "OldFiles":
                    OldFilesVM.RefreshData();
                    break;
                case "Photoshop":
                    PhotoshopVM.RefreshData();
                    break;
            }
        }
        catch (Exception ex)
        {
            ScanLogVM.AddLog("WARN", $"Failed refreshing view {tabName}: {ex.Message}");
        }
    }

    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        var rootsToScan = new List<string>();

        if (!string.IsNullOrWhiteSpace(CustomScanPath) && Directory.Exists(CustomScanPath))
        {
            rootsToScan.Add(CustomScanPath);
        }
        else
        {
            foreach (var d in OverviewVM.Drives)
            {
                if (d.IsSelected && Directory.Exists(d.Name))
                {
                    rootsToScan.Add(d.Name);
                }
            }
        }

        if (rootsToScan.Count == 0)
        {
            MessageBox.Show("Please select at least one drive or specify a valid directory to scan.",
                "DiskScope", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        OverviewVM.Stats.Reset();
        OverviewVM.Stats.State = ScanState.Scanning;
        ScanLogVM.ClearLogs();
        ScanLogVM.AddLog("INFO", $"Started scan for target: {string.Join(", ", rootsToScan)}");

        var progress = new Progress<ScanProgressReport>(report =>
        {
            OverviewVM.Stats.DirectoriesVisited = report.DirectoriesVisited;
            OverviewVM.Stats.DirectoriesProcessed = report.DirectoriesProcessed;
            OverviewVM.Stats.DirectoriesSkipped = report.DirectoriesSkipped;

            OverviewVM.Stats.FilesDiscovered = report.FilesDiscovered;
            OverviewVM.Stats.FilesIndexed = report.FilesIndexed;
            OverviewVM.Stats.FilesSkipped = report.FilesSkipped;

            OverviewVM.Stats.LogicalBytesIndexed = report.LogicalBytesIndexed;
            OverviewVM.Stats.Elapsed = report.Elapsed;
            OverviewVM.Stats.FilesPerSecond = report.FilesPerSecond;
            OverviewVM.Stats.BytesPerSecond = report.BytesPerSecond;
            OverviewVM.Stats.CurrentDirectory = report.CurrentDirectory;
            OverviewVM.Stats.State = report.State;

            if (!string.IsNullOrEmpty(report.NewRecentDirectory))
            {
                OverviewVM.AddRecentDirectory(report.NewRecentDirectory);
            }

            if (report.SkippedDirectory.HasValue)
            {
                ScanLogVM.AddSkippedDirectory(report.SkippedDirectory.Value.Path, report.SkippedDirectory.Value.Reason);
            }

            if (report.SkippedFile.HasValue)
            {
                ScanLogVM.AddSkippedFile(report.SkippedFile.Value.Path, report.SkippedFile.Value.Reason);
            }
        });

        try
        {
            var finalStats = await _scannerService.ScanDrivesAsync(rootsToScan, progress, _scanCts.Token);
            OverviewVM.Stats = finalStats;

            foreach (var (dir, reason) in _scannerService.SkippedDirectories)
            {
                ScanLogVM.AddSkippedDirectory(dir, reason);
            }

            foreach (var (file, reason) in _scannerService.SkippedFiles)
            {
                ScanLogVM.AddSkippedFile(file, reason);
            }

            OverviewVM.VerifyIntegrity();

            string statusMsg = finalStats.State == ScanState.Cancelled
                ? "Scan cancelled safely by user."
                : $"Scan complete: {finalStats.FilesIndexed:N0} files indexed in {finalStats.FormattedElapsed}.";

            ScanLogVM.AddLog("INFO", statusMsg);

            // Refresh data in all views
            RefreshAllViews();
        }
        catch (OperationCanceledException)
        {
            OverviewVM.Stats.State = ScanState.Cancelled;
            ScanLogVM.AddLog("INFO", "Scan cancelled safely by user.");
        }
        catch (Exception ex)
        {
            OverviewVM.Stats.State = ScanState.Failed;
            ScanLogVM.AddLog("ERROR", "Scan encountered an unhandled error", ex.Message);
            MessageBox.Show($"Scan failed: {ex.Message}", "DiskScope", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
            try { _scanCts?.Dispose(); } catch { }
            _scanCts = null;
        }
    }

    public void StopScan()
    {
        try
        {
            if (_scanCts != null && !_scanCts.IsCancellationRequested)
            {
                OverviewVM.Stats.State = ScanState.Stopping;
                ScanLogVM.AddLog("INFO", "Stop requested. Finalizing SQLite transaction and stopping scan safely...");
                _scanCts.Cancel();
            }
        }
        catch (ObjectDisposedException) { }
    }

    private void RefreshAllViews()
    {
        try { LargestFilesVM.RefreshData(); } catch { }
        try { LargestFoldersVM.RefreshData(OverviewVM.Stats.LogicalBytesIndexed); } catch { }
        try { FileTypesVM.RefreshData(); } catch { }
        try { OldFilesVM.RefreshData(); } catch { }
        try { PhotoshopVM.RefreshData(); } catch { }
    }

    private void BrowseCustomFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Directory to Scan",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            CustomScanPath = dialog.FolderName;
        }
    }
}
