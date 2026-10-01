using System.Collections.ObjectModel;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class StorageContributorItem : ObservableObject
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public double PercentageOfUsed { get; set; }
    public string CategoryOrType { get; set; } = string.Empty;
    public bool CanDrillDown { get; set; } = true;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string FormattedSize => SizeFormatter.Format(SizeBytes);
    public string FormattedPercentage => $"{PercentageOfUsed:F1}%";
}

public class StorageExplanation : ObservableObject
{
    private string _driveName = "C:";
    private long _totalBytes;
    private long _usedBytes;
    private long _freeBytes;
    private long _potentiallyReclaimableBytes;
    private bool _hasScanData;

    public string DriveName
    {
        get => _driveName;
        set => SetProperty(ref _driveName, value);
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (SetProperty(ref _totalBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotal));
                OnPropertyChanged(nameof(PercentUsed));
            }
        }
    }

    public long UsedBytes
    {
        get => _usedBytes;
        set
        {
            if (SetProperty(ref _usedBytes, value))
            {
                OnPropertyChanged(nameof(FormattedUsed));
                OnPropertyChanged(nameof(PercentUsed));
            }
        }
    }

    public long FreeBytes
    {
        get => _freeBytes;
        set
        {
            if (SetProperty(ref _freeBytes, value))
            {
                OnPropertyChanged(nameof(FormattedFree));
            }
        }
    }

    public long PotentiallyReclaimableBytes
    {
        get => _potentiallyReclaimableBytes;
        set
        {
            if (SetProperty(ref _potentiallyReclaimableBytes, value))
            {
                OnPropertyChanged(nameof(FormattedReclaimable));
            }
        }
    }

    public bool HasScanData
    {
        get => _hasScanData;
        set => SetProperty(ref _hasScanData, value);
    }

    public string FormattedTotal => SizeFormatter.Format(_totalBytes);
    public string FormattedUsed => SizeFormatter.Format(_usedBytes);
    public string FormattedFree => SizeFormatter.Format(_freeBytes);
    public string FormattedReclaimable => SizeFormatter.Format(_potentiallyReclaimableBytes);
    public double PercentUsed => _totalBytes > 0 ? (double)_usedBytes / _totalBytes * 100.0 : 0.0;

    public ObservableCollection<StorageContributorItem> LargestContributors { get; } = [];
}
