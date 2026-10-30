using System.Collections.ObjectModel;
using DiskScope.Infrastructure;
using DiskScope.Services;

namespace DiskScope.Models;

public class DuplicateGroup : ObservableObject
{
    private long _exactSize;
    private string _sha256 = string.Empty;
    private long? _wastedBytes;
    private bool _allocationCalculated;
    private ObservableCollection<FileRecord> _files = [];

    public long ExactSize
    {
        get => _exactSize;
        set
        {
            if (SetProperty(ref _exactSize, value))
            {
                _wastedBytes = null;
                _allocationCalculated = false;
                OnPropertyChanged(nameof(FormattedSize));
                OnPropertyChanged(nameof(WastedBytes));
                OnPropertyChanged(nameof(FormattedWasted));
            }
        }
    }

    public string Sha256
    {
        get => _sha256;
        set
        {
            if (SetProperty(ref _sha256, value))
            {
                OnPropertyChanged(nameof(ShortHash));
            }
        }
    }

    public ObservableCollection<FileRecord> Files
    {
        get => _files;
        set { _files = value; _wastedBytes = null; _allocationCalculated = false; }
    }

    public int FileCount => Files.Count;
    private void CalculateAllocation()
    {
        if (_allocationCalculated) return;
        _wastedBytes = DuplicateStorageAccounting.Calculate(Files, ExactSize);
        _allocationCalculated = true;
    }
    public bool AllocationKnown { get { CalculateAllocation(); return _wastedBytes.HasValue; } }
    public long WastedBytes { get { CalculateAllocation(); return _wastedBytes ?? 0; } }

    public string FormattedSize => SizeFormatter.Format(ExactSize);
    public string FormattedWasted => AllocationKnown ? SizeFormatter.Format(WastedBytes) : "Unavailable";
    public string ShortHash => Sha256.Length > 12 ? Sha256[..12] : Sha256;

    public void NotifyCollectionChanged()
    {
        _wastedBytes = null;
        _allocationCalculated = false;
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(WastedBytes));
        OnPropertyChanged(nameof(FormattedWasted));
    }
}

