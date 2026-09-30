using System.Collections.ObjectModel;
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

        // Initial sample
        var initialSample = _monitorService.CaptureSample();
        ApplySample(initialSample);
    }

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
