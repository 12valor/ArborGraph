using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private readonly JunkCleanerService _junkCleanerService;
    private readonly ExportService _exportService;
    private readonly SettingsService _settingsService;

    private object _currentView;
    private string _currentTab = "Overview";
    private bool _isScanning;
    private CancellationTokenSource? _scanCts;
    private string _customScanPath = string.Empty;

    public MainViewModel() : this(null)
    {
    }

    public MainViewModel(DatabaseService? dbService)
    {
        _dbService = dbService ?? new DatabaseService();
        _dbService.Initialize();

        _settingsService = new SettingsService();
        _scannerService = new ScannerService(_dbService, usnService: null, _settingsService);
        _diskService = new DiskService();
        _fileActionService = new FileActionService();
        _duplicateAnalyzer = new DuplicateAnalyzer(_dbService);
        _junkCleanerService = new JunkCleanerService();
        _exportService = new ExportService();
        var developerStorageService = new DeveloperStorageService(_dbService);

        OverviewVM = new OverviewViewModel(_dbService, _diskService);
        AnalyticsVM = new AnalyticsViewModel(_dbService, _diskService, _fileActionService);
        CleanupCenterVM = new CleanupCenterViewModel(_dbService, developerStorageService, _fileActionService, () => _ = StartScanAsync());
        DeveloperStorageVM = new DeveloperStorageViewModel(developerStorageService, _fileActionService);
        LargestFilesVM = new LargestFilesViewModel(_dbService, _fileActionService);
        LargestFoldersVM = new LargestFoldersViewModel(_dbService, _fileActionService);
        FileTypesVM = new FileTypesViewModel(_dbService, _fileActionService);
        OldFilesVM = new OldFilesViewModel(_dbService, _fileActionService);
        DuplicatesVM = new DuplicateViewModel(_duplicateAnalyzer, _fileActionService);
        PhotoshopVM = new PhotoshopViewModel(_dbService, _fileActionService);
        JunkCleanerVM = new JunkCleanerViewModel(_junkCleanerService, _fileActionService);
        TreemapVM = new TreemapViewModel(_dbService, _fileActionService);
        ScanLogVM = new ScanLogViewModel();
        SettingsVM = new SettingsViewModel(_settingsService, _dbService);
        ScannerVM = new ScannerViewModel(this);
        FilesVM = new FilesViewModel(this);
        SettingsVM.ScanLogVM = ScanLogVM;

        OverviewVM.ReviewCleanupCommand = new RelayCommand(_ => NavigateTo("CleanupCenter"));
        OverviewVM.StartScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning);

        _currentView = OverviewVM;

        StartScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning);
        StopScanCommand = new RelayCommand(_ => StopScan(), _ => IsScanning && _scanCts != null && !_scanCts.IsCancellationRequested);
        NavigateCommand = new RelayCommand(param => NavigateTo(param?.ToString() ?? "Overview"));
        BrowseCustomFolderCommand = new RelayCommand(_ => BrowseCustomFolder());

        ExportHtmlReportCommand = new RelayCommand(async _ => await ExportHtmlReportAsync(), _ => !IsScanning);
        ExportJsonReportCommand = new RelayCommand(async _ => await ExportJsonReportAsync(), _ => !IsScanning);
        ExportFilesCsvCommand = new RelayCommand(async _ => await ExportFilesCsvAsync(), _ => !IsScanning);
        ExportDuplicatesCsvCommand = new RelayCommand(async _ => await ExportDuplicatesCsvAsync(), _ => !IsScanning);
        ExportJunkCsvCommand = new RelayCommand(async _ => await ExportJunkCsvAsync(), _ => !IsScanning);
    }

    public OverviewViewModel OverviewVM { get; }
    public AnalyticsViewModel AnalyticsVM { get; }
    public CleanupCenterViewModel CleanupCenterVM { get; }
    public DeveloperStorageViewModel DeveloperStorageVM { get; }
    public LargestFilesViewModel LargestFilesVM { get; }
    public LargestFoldersViewModel LargestFoldersVM { get; }
    public FileTypesViewModel FileTypesVM { get; }
    public OldFilesViewModel OldFilesVM { get; }
    public DuplicateViewModel DuplicatesVM { get; }
    public PhotoshopViewModel PhotoshopVM { get; }
    public JunkCleanerViewModel JunkCleanerVM { get; }
    public TreemapViewModel TreemapVM { get; }
    public ScanLogViewModel ScanLogVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public ScannerViewModel ScannerVM { get; }
    public FilesViewModel FilesVM { get; }

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
                (OverviewVM.StartScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportHtmlReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportJsonReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportFilesCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportDuplicatesCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportJunkCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
                CommandManager.InvalidateRequerySuggested();
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
    public ICommand ExportHtmlReportCommand { get; }
    public ICommand ExportJsonReportCommand { get; }
    public ICommand ExportFilesCsvCommand { get; }
    public ICommand ExportDuplicatesCsvCommand { get; }
    public ICommand ExportJunkCsvCommand { get; }

    private bool _isTabLoading;
    private string _tabLoadingTitle = string.Empty;
    private string _tabLoadingMessage = string.Empty;
    private long _navSequence = 0;

    public bool IsTabLoading
    {
        get => _isTabLoading;
        set => SetProperty(ref _isTabLoading, value);
    }

    public string TabLoadingTitle
    {
        get => _tabLoadingTitle;
        set => SetProperty(ref _tabLoadingTitle, value);
    }

    public string TabLoadingMessage
    {
        get => _tabLoadingMessage;
        set => SetProperty(ref _tabLoadingMessage, value);
    }

    public void NavigateTo(string tabName)
    {
        _ = NavigateToAsync(tabName);
    }

    public async Task NavigateToAsync(string tabName)
    {
        long seq = Interlocked.Increment(ref _navSequence);

        try
        {
            CurrentTab = tabName;
            CurrentView = tabName switch
            {
                "Scanner" => ScannerVM,
                "Files" => FilesVM,
                "Analytics" => AnalyticsVM,
                "CleanupCenter" => CleanupCenterVM,
                "DeveloperStorage" => DeveloperStorageVM,
                "LargestFiles" => LargestFilesVM,
                "LargestFolders" => LargestFoldersVM,
                "FileTypes" => FileTypesVM,
                "OldFiles" => OldFilesVM,
                "Duplicates" => DuplicatesVM,
                "Photoshop" => PhotoshopVM,
                "JunkCleaner" => JunkCleanerVM,
                "Treemap" => TreemapVM,
                "ScanLog" => ScanLogVM,
                "Settings" => SettingsVM,
                _ => OverviewVM
            };

            // Lightweight, instant, or active-scanning states do not block with full loading card
            if (IsScanning || tabName == "Scanner" || tabName == "Files" || tabName == "ScanLog" || tabName == "Duplicates")
            {
                IsTabLoading = false;
                return;
            }

            if (tabName == "Settings")
            {
                SettingsVM.LoadFromService();
                IsTabLoading = false;
                return;
            }

            if (tabName == "Overview")
            {
                OverviewVM.OnViewLoaded();
                IsTabLoading = false;
                return;
            }

            // Heavy data tabs: Activate loading state first
            TabLoadingTitle = GetTabFriendlyTitle(tabName);
            TabLoadingMessage = GetTabLoadingMessage(tabName);
            IsTabLoading = true;

            // Allow the UI thread to immediately paint the loading indicator
            await Task.Yield();

            Task? refreshTask = tabName switch
            {
                "Analytics" => AnalyticsVM.RefreshDataAsync(),
                "CleanupCenter" => CleanupCenterVM.LoadCleanupCategoriesAsync(),
                "DeveloperStorage" => DeveloperStorageVM.ScanIndexedStorageAsync(),
                "LargestFiles" => LargestFilesVM.RefreshDataAsync(),
                "LargestFolders" => LargestFoldersVM.RefreshDataAsync(OverviewVM.Stats.LogicalBytesIndexed),
                "FileTypes" => FileTypesVM.RefreshDataAsync(),
                "OldFiles" => OldFilesVM.RefreshDataAsync(),
                "Photoshop" => PhotoshopVM.RefreshDataAsync(),
                "JunkCleaner" => Task.Run(() => JunkCleanerVM.RefreshData()),
                "Treemap" => TreemapVM.LoadCurrentLevelAsync(),
                _ => Task.CompletedTask
            };

            if (refreshTask != null)
            {
                // 10-second safety timeout guard against any deadlock or hanging I/O
                var completed = await Task.WhenAny(refreshTask, Task.Delay(10000));
                if (completed != refreshTask)
                {
                    ScanLogVM.AddLog("WARN", $"Loading view '{tabName}' timed out after 10 seconds.");
                }
            }
        }
        catch (Exception ex)
        {
            ScanLogVM.AddLog("WARN", $"Failed refreshing view {tabName}: {ex.Message}");
        }
        finally
        {
            if (seq == Volatile.Read(ref _navSequence))
            {
                IsTabLoading = false;
            }
        }
    }

    private static string GetTabFriendlyTitle(string tabName) => tabName switch
    {
        "Analytics" => "Storage Analytics",
        "CleanupCenter" => "Cleanup Center",
        "DeveloperStorage" => "Developer Storage Hub",
        "LargestFiles" => "Largest Files",
        "LargestFolders" => "Largest Folders",
        "FileTypes" => "File Types & Categories",
        "OldFiles" => "Old & Dormant Files",
        "Duplicates" => "Duplicate Files",
        "Photoshop" => "Photoshop & Media Assets",
        "JunkCleaner" => "Junk & Cache Cleaner",
        "Treemap" => "Interactive Space Treemap",
        "ScanLog" => "Scan Diagnostics",
        "Settings" => "Settings & Rules",
        _ => "Storage Overview"
    };

    private static string GetTabLoadingMessage(string tabName) => tabName switch
    {
        "Analytics" => "Analyzing storage trends, age buckets, and reclaimable space...",
        "CleanupCenter" => "Scanning developer caches and potential cleanup opportunities...",
        "DeveloperStorage" => "Inspecting package caches, build targets, and container storage...",
        "LargestFiles" => "Querying top storage-consuming files from SQLite index...",
        "LargestFolders" => "Aggregating directory sizes and hierarchy metrics...",
        "FileTypes" => "Calculating category breakdowns and file type distribution...",
        "OldFiles" => "Identifying files untouched for 180+ days...",
        "Duplicates" => "Preparing duplicate file detection engine...",
        "Photoshop" => "Inspecting PSD/PSB files and Adobe cache trees...",
        "JunkCleaner" => "Checking system temp folders and browser caches...",
        "Treemap" => "Computing squarified layout for proportional visualization...",
        _ => "Loading view data..."
    };

    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        var rootsToScan = new List<string>();

        string cleanCustomPath = CustomScanPath?.Trim().Trim('"').Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(cleanCustomPath))
        {
            if (Directory.Exists(cleanCustomPath))
            {
                rootsToScan.Add(cleanCustomPath);
            }
            else
            {
                ScanLogVM.AddLog("ERROR", $"Custom folder path does not exist: {cleanCustomPath}");
                MessageBox.Show($"The specified scan folder does not exist:\n{cleanCustomPath}", "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (rootsToScan.Count == 0)
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
            var defaultDrive = OverviewVM.Drives.FirstOrDefault(d => Directory.Exists(d.Name));
            if (defaultDrive != null)
            {
                defaultDrive.IsSelected = true;
                rootsToScan.Add(defaultDrive.Name);
            }
            else
            {
                rootsToScan.Add("C:\\");
            }
        }

        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        OverviewVM.Stats.Reset();
        OverviewVM.Stats.State = ScanState.Scanning;
        OverviewVM.ClearRecentDirectories();
        OverviewVM.AddRecentDirectory($"Starting scan on target: {string.Join(", ", rootsToScan)}...", force: true);
        ScanLogVM.ClearLogs();
        ScanLogVM.AddLog("INFO", $"Started scan for target: {string.Join(", ", rootsToScan)}");

        // Immediately navigate to the active Scanner console so the user sees live streaming
        if (CurrentTab == "Overview")
        {
            NavigateTo("Scanner");
        }

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
            var finalStats = await _scannerService.ScanDrivesAsync(rootsToScan, progress, _scanCts.Token, enableIncremental: false);
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

            // Refresh data in all views on completion; for cancelled scans only refresh lightweight overview
            if (finalStats.State != ScanState.Cancelled)
            {
                RefreshAllViews();
            }
            else
            {
                try { OverviewVM.GenerateStorageExplanation(); } catch { }
            }
        }
        catch (OperationCanceledException)
        {
            OverviewVM.Stats.State = ScanState.Cancelled;
            ScanLogVM.AddLog("INFO", "Scan cancelled safely by user.");
            try { OverviewVM.GenerateStorageExplanation(); } catch { }
        }
        catch (Exception ex)
        {
            OverviewVM.Stats.State = ScanState.Failed;
            ScanLogVM.AddLog("ERROR", "Scan encountered an unhandled error", ex.Message);
            MessageBox.Show($"Scan failed: {ex.Message}", "ArborGraph", MessageBoxButton.OK, MessageBoxImage.Error);
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
                (StopScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
                ScanLogVM.AddLog("INFO", "Stop requested. Finalizing SQLite transaction and stopping scan safely...");
                OverviewVM.AddRecentDirectory("Stop requested by user. Terminating traversal safely...", force: true);
                _scanCts.Cancel();
                _dbService.CancelActiveOperations();
            }
        }
        catch (ObjectDisposedException) { }
    }

    private void RefreshAllViews()
    {
        try { OverviewVM.GenerateStorageExplanation(); } catch { }
        try { AnalyticsVM.RefreshData(); } catch { }
        try { _ = CleanupCenterVM.LoadCleanupCategoriesAsync(); } catch { }
        try { LargestFilesVM.RefreshData(); } catch { }
        try { LargestFoldersVM.RefreshData(OverviewVM.Stats.LogicalBytesIndexed); } catch { }
        try { FileTypesVM.RefreshData(); } catch { }
        try { OldFilesVM.RefreshData(); } catch { }
        try { PhotoshopVM.RefreshData(); } catch { }
        try { _ = DeveloperStorageVM.ScanIndexedStorageAsync(); } catch { }
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

    private async Task ExportHtmlReportAsync()
    {
        if (IsScanning) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
            FileName = $"arborgraph_audit_{DateTime.Now:yyyyMMdd_HHmmss}.html",
            Title = "Export Executive HTML Audit Report"
        };

        if (sfd.ShowDialog() == true)
        {
            var prevCursor = System.Windows.Input.Mouse.OverrideCursor;
            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                string targetRoots = !string.IsNullOrWhiteSpace(CustomScanPath)
                    ? CustomScanPath
                    : string.Join(", ", OverviewVM.Drives.Where(d => d.IsSelected).Select(d => d.Name));

                var report = await _exportService.BuildAuditReportAsync(
                    _dbService,
                    _junkCleanerService,
                    OverviewVM.Stats,
                    targetRoots,
                    DuplicatesVM.DuplicateGroups);

                await _exportService.ExportToHtmlAsync(report, sfd.FileName);

                System.Windows.Input.Mouse.OverrideCursor = prevCursor;

                var res = MessageBox.Show(
                    $"Executive HTML audit report saved successfully to:\n{sfd.FileName}\n\nWould you like to open it now in your browser?",
                    "Report Exported",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Input.Mouse.OverrideCursor = prevCursor;
                MessageBox.Show($"Failed to export HTML report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = prevCursor;
            }
        }
    }

    private async Task ExportJsonReportAsync()
    {
        if (IsScanning) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = $"arborgraph_audit_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            Title = "Export Structured JSON Audit Dump"
        };

        if (sfd.ShowDialog() == true)
        {
            var prevCursor = System.Windows.Input.Mouse.OverrideCursor;
            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                string targetRoots = !string.IsNullOrWhiteSpace(CustomScanPath)
                    ? CustomScanPath
                    : string.Join(", ", OverviewVM.Drives.Where(d => d.IsSelected).Select(d => d.Name));

                var report = await _exportService.BuildAuditReportAsync(
                    _dbService,
                    _junkCleanerService,
                    OverviewVM.Stats,
                    targetRoots,
                    DuplicatesVM.DuplicateGroups);

                await _exportService.ExportToJsonAsync(report, sfd.FileName);

                System.Windows.Input.Mouse.OverrideCursor = prevCursor;

                var res = MessageBox.Show(
                    $"JSON audit dump saved successfully to:\n{sfd.FileName}\n\nWould you like to open it now?",
                    "Export Successful",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Input.Mouse.OverrideCursor = prevCursor;
                MessageBox.Show($"Failed to export JSON: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = prevCursor;
            }
        }
    }

    private async Task ExportFilesCsvAsync()
    {
        if (IsScanning) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"arborgraph_files_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = "Export Indexed Files to CSV"
        };

        if (sfd.ShowDialog() == true)
        {
            var prevCursor = System.Windows.Input.Mouse.OverrideCursor;
            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                var files = _dbService.GetFilesPaged(0, 10000, sortBy: "size", sortDesc: true);
                await _exportService.ExportFilesToCsvAsync(files, sfd.FileName);

                System.Windows.Input.Mouse.OverrideCursor = prevCursor;

                var res = MessageBox.Show(
                    $"Exported {files.Count:N0} files to:\n{sfd.FileName}\n\nWould you like to open it now?",
                    "Export Successful",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Input.Mouse.OverrideCursor = prevCursor;
                MessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = prevCursor;
            }
        }
    }

    private async Task ExportDuplicatesCsvAsync()
    {
        if (IsScanning) return;

        if (DuplicatesVM.DuplicateGroups.Count == 0)
        {
            var prompt = MessageBox.Show(
                "Duplicate candidate groups have not been analyzed yet. Would you like to run duplicate analysis now?",
                "Duplicate Analysis Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (prompt == MessageBoxResult.Yes)
            {
                var prevCursor = System.Windows.Input.Mouse.OverrideCursor;
                try
                {
                    System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    await DuplicatesVM.RunAnalysisAsync();
                }
                finally
                {
                    System.Windows.Input.Mouse.OverrideCursor = prevCursor;
                }
            }
            else
            {
                return;
            }
        }

        if (DuplicatesVM.DuplicateGroups.Count == 0)
        {
            MessageBox.Show("No duplicate files found in the current index.", "No Duplicates", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await DuplicatesVM.ExportCsvAsync();
    }

    private async Task ExportJunkCsvAsync()
    {
        if (IsScanning) return;
        await JunkCleanerVM.ExportCsvAsync();
    }
}
