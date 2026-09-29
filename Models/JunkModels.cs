using System.ComponentModel;
using System.Runtime.CompilerServices;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public enum JunkCategory
{
    System,
    Browser,
    Developer
}

public class JunkFileItem
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string FormattedSize => SizeFormatter.Format(SizeInBytes);
    public DateTime LastModified { get; set; }
}

public class JunkTarget : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private long _sizeInBytes;
    private int _fileCount;
    private string _status = "Ready";
    private bool _isBusy;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JunkCategory Category { get; set; }
    public List<string> TargetDirectories { get; set; } = new();
    public string SearchPattern { get; set; } = "*.*";
    public bool Recursive { get; set; } = true;
    public List<JunkFileItem> DiscoveredFiles { get; set; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public long SizeInBytes
    {
        get => _sizeInBytes;
        set
        {
            if (_sizeInBytes != value)
            {
                _sizeInBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedSize));
            }
        }
    }

    public int FileCount
    {
        get => _fileCount;
        set
        {
            if (_fileCount != value)
            {
                _fileCount = value;
                OnPropertyChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    public string FormattedSize => SizeFormatter.Format(SizeInBytes);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class JunkCleanResult
{
    public long BytesFreed { get; set; }
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public TimeSpan Elapsed { get; set; }
    public List<string> SkippedReasons { get; set; } = new();

    public string FormattedBytesFreed => SizeFormatter.Format(BytesFreed);
}

public class JunkScanProgress
{
    public string CurrentTargetName { get; set; } = string.Empty;
    public int CompletedTargets { get; set; }
    public int TotalTargets { get; set; }
    public double Percentage => TotalTargets > 0 ? (double)CompletedTargets / TotalTargets * 100 : 0;
}

public class JunkCleanProgress
{
    public string CurrentFileName { get; set; } = string.Empty;
    public int CompletedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long BytesFreedSoFar { get; set; }
}
