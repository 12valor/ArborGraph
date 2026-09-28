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
    private ScanStats _stats = new();

    private long _totalDriveBytes;
    private long _usedDriveBytes;
    private long _freeDriveBytes;
    private double _overallPercentUsed;
    private bool _isIntegrityCheckPassed = true;
    private string _integrityStatus = "Ready for scan";

    public OverviewViewModel(DatabaseService dbService, DiskService diskService)
    {
        _dbService = dbService;
        _diskService = diskService;
        RecentDirectories = [];
        Drives = [];
        RefreshDrives();
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

    private DateTime _lastRecentDirTime = DateTime.MinValue;

    public void AddRecentDirectory(string dir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            if ((DateTime.UtcNow - _lastRecentDirTime).TotalMilliseconds < 150) return;
            _lastRecentDirTime = DateTime.UtcNow;

            if (RecentDirectories.Count >= 50)
            {
                RecentDirectories.RemoveAt(0);
            }
            RecentDirectories.Add(dir);
        }
        catch { }
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
}
