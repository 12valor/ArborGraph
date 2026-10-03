using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class OverviewViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly DiskService _diskService;
    private readonly ProcessMonitorService _monitorService;
    private ScanStats _stats = new();

    private long _totalDriveBytes;
    private long _usedDriveBytes;
    private long _freeDriveBytes;
    private double _overallPercentUsed;
    private bool _isIntegrityCheckPassed = true;
    private string _integrityStatus = "Ready for scan";

    private string _currentCpuText = "—";
    private string _currentRamText = "—";
    private string _cpuPeakText = "Peak: 0.0%";
    private string _ramCeilingText = "Scale: 0–256 MB";
    private double _dynamicRamMaxCeiling = 256.0;
    private IReadOnlyList<double> _cpuHistory = Array.Empty<double>();
    private IReadOnlyList<double> _ramHistory = Array.Empty<double>();
    private bool _isViewActive = true;

    public OverviewViewModel(DatabaseService dbService, DiskService diskService, ProcessMonitorService? monitorService = null)
    {
        _dbService = dbService;
        _diskService = diskService;
        _monitorService = monitorService ?? new ProcessMonitorService(historyCapacity: 60, intervalMs: 1000);
        _monitorService.SampleTaken += OnProcessSampleTaken;

        RecentDirectories = [];
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(RecentDirectories, _recentDirsLock);
        RecentDirectories.Add("Ready. Click 'Start Scan' to begin real-time filesystem traversal.");

        Drives = [];
        RefreshDrives();

        DrilldownCommand = new RelayCommand(param =>
        {
            if (param is StorageContributorItem item && !string.IsNullOrEmpty(item.Path))
            {
                DrillInto(item.Path);
            }
        });
        DrillUpCommand = new RelayCommand(_ => DrillUp(), _ => Breadcrumbs.Count > 1);
        ResetDrilldownCommand = new RelayCommand(_ => ResetDrilldown());

        // Initial sample
        var initialSample = _monitorService.CaptureSample();
        ApplySample(initialSample);
    }

    public StorageExplanation Explanation { get; } = new();
    public ObservableCollection<StorageContributorItem> DrilldownItems { get; } = [];
    public ObservableCollection<string> Breadcrumbs { get; } = [];
    public string CurrentDrilldownPath { get; private set; } = string.Empty;

    public ICommand DrilldownCommand { get; }
    public ICommand DrillUpCommand { get; }
    public ICommand ResetDrilldownCommand { get; }
    public ICommand? ReviewCleanupCommand { get; set; }
    public ICommand? StartScanCommand { get; set; }

    public ScanStats Stats
    {
        get => _stats;
        set => SetProperty(ref _stats, value);
    }

    public ObservableCollection<DiskDriveInfo> Drives { get; }
    public ObservableCollection<string> RecentDirectories { get; }

    public long TotalDriveBytes
    {
        get => _totalDriveBytes;
        set => SetProperty(ref _totalDriveBytes, value);
    }

    public long UsedDriveBytes
    {
        get => _usedDriveBytes;
        set => SetProperty(ref _usedDriveBytes, value);
    }

    public long FreeDriveBytes
    {
        get => _freeDriveBytes;
        set => SetProperty(ref _freeDriveBytes, value);
    }

    public double OverallPercentUsed
    {
        get => _overallPercentUsed;
        set => SetProperty(ref _overallPercentUsed, value);
    }

    public bool IsIntegrityCheckPassed
    {
        get => _isIntegrityCheckPassed;
        set => SetProperty(ref _isIntegrityCheckPassed, value);
    }

    public string IntegrityStatus
    {
        get => _integrityStatus;
        set => SetProperty(ref _integrityStatus, value);
    }

    public string FormattedTotalDrive => SizeFormatter.Format(_totalDriveBytes);
    public string FormattedUsedDrive => SizeFormatter.Format(_usedDriveBytes);
    public string FormattedFreeDrive => SizeFormatter.Format(_freeDriveBytes);

    public void RefreshDrives()
    {
        try
        {
            Drives.Clear();
            long total = 0;
            long used = 0;
            long free = 0;

            foreach (var d in _diskService.GetSystemDrives())
            {
                Drives.Add(d);
                total += d.TotalBytes;
                used += d.UsedBytes;
                free += d.FreeBytes;
            }

            TotalDriveBytes = total;
            UsedDriveBytes = used;
            FreeDriveBytes = free;
            OverallPercentUsed = total > 0 ? (double)used / total * 100.0 : 0.0;
            OnPropertyChanged(nameof(FormattedTotalDrive));
            OnPropertyChanged(nameof(FormattedUsedDrive));
            OnPropertyChanged(nameof(FormattedFreeDrive));
        }
        catch { }
    }

    public void GenerateStorageExplanation()
    {
        try
        {
            var targetDrive = Drives.FirstOrDefault(d => d.IsSelected) ?? Drives.FirstOrDefault();
            string driveLetter = targetDrive?.Name ?? "C:\\";
            string rootClean = driveLetter.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + "\\";

            Explanation.DriveName = rootClean;
            Explanation.TotalBytes = targetDrive?.TotalBytes ?? TotalDriveBytes;
            Explanation.UsedBytes = targetDrive?.UsedBytes ?? UsedDriveBytes;
            Explanation.FreeBytes = targetDrive?.FreeBytes ?? FreeDriveBytes;

            // Deterministic reclaimable total from actual scan data
            var (_, totalReclaimable) = _dbService.GetReclaimableStorageBreakdown();
            Explanation.PotentiallyReclaimableBytes = totalReclaimable;

            Explanation.LargestContributors.Clear();
            DrilldownItems.Clear();
            Breadcrumbs.Clear();
            Breadcrumbs.Add(rootClean);
            CurrentDrilldownPath = rootClean;

            // Top subdirectories under drive root
            var topDirs = _dbService.GetSubdirectories(rootClean, limit: 10);
            long usedB = Math.Max(1L, Explanation.UsedBytes);

            if (topDirs.Count > 0)
            {
                foreach (var d in topDirs)
                {
                    var contrib = new StorageContributorItem
                    {
                        Name = d.Name,
                        Path = d.Path,
                        SizeBytes = d.Size,
                        PercentageOfUsed = (double)d.Size / usedB * 100.0,
                        CategoryOrType = "Folder",
                        CanDrillDown = d.SubfolderCount > 0 || d.FileCount > 0
                    };
                    Explanation.LargestContributors.Add(contrib);
                    DrilldownItems.Add(contrib);
                }
                Explanation.HasScanData = true;
            }
            else
            {
                // Fall back to category distribution if no directory rollups
                var catDict = _dbService.GetCategoryBreakdown();
                foreach (var (cat, (cnt, sz)) in catDict.OrderByDescending(kv => kv.Value.TotalSize).Take(6))
                {
                    if (sz <= 0) continue;
                    var contrib = new StorageContributorItem
                    {
                        Name = cat,
                        Path = string.Empty,
                        SizeBytes = sz,
                        PercentageOfUsed = (double)sz / usedB * 100.0,
                        CategoryOrType = "Category",
                        CanDrillDown = false
                    };
                    Explanation.LargestContributors.Add(contrib);
                    DrilldownItems.Add(contrib);
                }
                Explanation.HasScanData = Explanation.LargestContributors.Count > 0;
            }

            (DrillUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GenerateStorageExplanation error: {ex.Message}");
        }
    }

    public void DrillInto(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var subdirs = _dbService.GetSubdirectories(path, limit: 50);
            DrilldownItems.Clear();
            string folderName = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(folderName)) folderName = path;
            Breadcrumbs.Add(folderName);
            CurrentDrilldownPath = path;

            long parentSize = subdirs.Sum(d => d.Size);
            long baseSize = Math.Max(1L, parentSize > 0 ? parentSize : Explanation.UsedBytes);

            foreach (var d in subdirs)
            {
                DrilldownItems.Add(new StorageContributorItem
                {
                    Name = d.Name,
                    Path = d.Path,
                    SizeBytes = d.Size,
                    PercentageOfUsed = (double)d.Size / baseSize * 100.0,
                    CategoryOrType = "Subfolder",
                    CanDrillDown = d.SubfolderCount > 0
                });
            }

            (DrillUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch { }
    }

    public void DrillUp()
    {
        if (Breadcrumbs.Count <= 1) return;
        try
        {
            string parentPath = System.IO.Path.GetDirectoryName(CurrentDrilldownPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) ?? string.Empty;
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);

            if (string.IsNullOrEmpty(parentPath) || Breadcrumbs.Count <= 1)
            {
                ResetDrilldown();
            }
            else
            {
                CurrentDrilldownPath = parentPath;
                var subdirs = _dbService.GetSubdirectories(parentPath, limit: 50);
                DrilldownItems.Clear();
                long baseSize = Math.Max(1L, Explanation.UsedBytes);

                foreach (var d in subdirs)
                {
                    DrilldownItems.Add(new StorageContributorItem
                    {
                        Name = d.Name,
                        Path = d.Path,
                        SizeBytes = d.Size,
                        PercentageOfUsed = (double)d.Size / baseSize * 100.0,
                        CategoryOrType = "Subfolder",
                        CanDrillDown = d.SubfolderCount > 0
                    });
                }
            }

            (DrillUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch { }
    }

    public void ResetDrilldown()
    {
        GenerateStorageExplanation();
    }

    private readonly object _recentDirsLock = new();
    private DateTime _lastRecentDirTime = DateTime.MinValue;

    public void ClearRecentDirectories()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.Invoke(() => ClearRecentDirectories());
                return;
            }
            catch { }
        }

        lock (_recentDirsLock)
        {
            RecentDirectories.Clear();
        }
    }

    public void AddRecentDirectory(string dir, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.BeginInvoke(DispatcherPriority.Normal, () => AddRecentDirectory(dir, force));
                return;
            }
            catch { }
        }

        try
        {
            if (!force)
            {
                if ((DateTime.UtcNow - _lastRecentDirTime).TotalMilliseconds < 35) return;
                _lastRecentDirTime = DateTime.UtcNow;
            }

            lock (_recentDirsLock)
            {
                if (RecentDirectories.Count >= 60)
                {
                    RecentDirectories.RemoveAt(RecentDirectories.Count - 1);
                }
                RecentDirectories.Insert(0, dir);
            }
        }
        catch { }
    }

    public string CurrentCpuText
    {
        get => _currentCpuText;
        private set => SetProperty(ref _currentCpuText, value);
    }

    public string CurrentRamText
    {
        get => _currentRamText;
        private set => SetProperty(ref _currentRamText, value);
    }

    public string CpuPeakText
    {
        get => _cpuPeakText;
        private set => SetProperty(ref _cpuPeakText, value);
    }

    public string RamCeilingText
    {
        get => _ramCeilingText;
        private set => SetProperty(ref _ramCeilingText, value);
    }

    public double DynamicRamMaxCeiling
    {
        get => _dynamicRamMaxCeiling;
        private set => SetProperty(ref _dynamicRamMaxCeiling, value);
    }

    public IReadOnlyList<double> CpuHistory
    {
        get => _cpuHistory;
        private set => SetProperty(ref _cpuHistory, value);
    }

    public IReadOnlyList<double> RamHistory
    {
        get => _ramHistory;
        private set => SetProperty(ref _ramHistory, value);
    }

    public void OnViewLoaded()
    {
        _isViewActive = true;
        // Refresh with latest snapshot immediately
        RefreshMetricViews();
    }

    public void OnViewUnloaded()
    {
        _isViewActive = false;
    }

    private void OnProcessSampleTaken(ProcessResourceSample sample)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.HasShutdownStarted)
        {
            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                ApplySample(sample);
            });
        }
        else
        {
            ApplySample(sample);
        }
    }

    private void ApplySample(ProcessResourceSample sample)
    {
        if (sample.IsValid)
        {
            CurrentCpuText = $"{sample.CpuPercentage:F1}%";
            CurrentRamText = $"{sample.RamMegabytes:F0} MB";
            double peak = _monitorService.GetCpuPeak();
            CpuPeakText = $"Peak: {peak:F1}%";
            double ceiling = _monitorService.DynamicRamCeiling;
            DynamicRamMaxCeiling = ceiling;
            RamCeilingText = $"Scale: 0–{ceiling:F0} MB";
        }
        else
        {
            CurrentCpuText = "—";
            CurrentRamText = "—";
            CpuPeakText = "Peak: —";
            RamCeilingText = "Scale: —";
        }

        // Only trigger heavy visual graph updates if view is currently active/visible
        if (_isViewActive)
        {
            RefreshMetricViews();
        }
    }

    private void RefreshMetricViews()
    {
        CpuHistory = _monitorService.GetCpuHistory();
        RamHistory = _monitorService.GetRamHistory();
    }

    public void VerifyIntegrity()
    {
        try
        {
            bool dbOk = _dbService.CheckIntegrity();
            IsIntegrityCheckPassed = dbOk;
            IntegrityStatus = dbOk
                ? "Authoritative SQLite Index Verified (PRAGMA integrity_check = OK)"
                : "Warning: SQLite integrity verification reported an issue";
        }
        catch
        {
            IsIntegrityCheckPassed = false;
            IntegrityStatus = "Integrity check could not complete.";
        }
    }

    public void Dispose()
    {
        _monitorService.SampleTaken -= OnProcessSampleTaken;
        _monitorService.Dispose();
    }
}
