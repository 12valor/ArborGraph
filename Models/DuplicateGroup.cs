using System.Collections.ObjectModel;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class DuplicateGroup : ObservableObject
{
    private long _exactSize;
    private string _sha256 = string.Empty;

    public long ExactSize
    {
        get => _exactSize;
        set
        {
            if (SetProperty(ref _exactSize, value))
            {
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

    public ObservableCollection<FileRecord> Files { get; set; } = [];

    public int FileCount => Files.Count;
    public long WastedBytes => Math.Max(0, (FileCount - 1) * ExactSize);

    public string FormattedSize => SizeFormatter.Format(ExactSize);
    public string FormattedWasted => SizeFormatter.Format(WastedBytes);
    public string ShortHash => Sha256.Length > 12 ? Sha256[..12] : Sha256;

    public void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(WastedBytes));
        OnPropertyChanged(nameof(FormattedWasted));
    }
}

