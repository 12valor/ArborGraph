using System.Collections.ObjectModel;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class CleanupCategory : ObservableObject
{
    private bool _isSelected;
    private long _totalBytes;
    private int _itemCount;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string SafetyBadge { get; set; } = "Safe to Clean";

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (SetProperty(ref _totalBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotal));
            }
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ObservableCollection<CleanupCandidateItem> Items { get; set; } = [];
    public string FormattedTotal => SizeFormatter.Format(TotalBytes);

    public void RefreshTotals()
    {
        TotalBytes = Items.Sum(i => i.SizeBytes);
        ItemCount = Items.Count;
    }
}

public class CleanupCandidateItem : ObservableObject
{
    private bool _isSelected = true;

    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public DateTime LastModified { get; set; }
    public string Reason { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string FormattedSize => SizeFormatter.Format(SizeBytes);
    public string FormattedLastModified => LastModified == DateTime.MinValue ? "—" : LastModified.ToString("yyyy-MM-dd HH:mm");
}

public class CleanupResultSummary
{
    public int TotalProcessed { get; set; }
    public int SuccessfullyRemoved { get; set; }
    public int Failed { get; set; }
    public long RecoveredBytes { get; set; }
    public string FormattedRecovered => SizeFormatter.Format(RecoveredBytes);
    public List<string> FailureMessages { get; set; } = [];
}
