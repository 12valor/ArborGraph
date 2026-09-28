using System.Collections.ObjectModel;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class FileTypeItem
{
    public string Category { get; set; } = string.Empty;
    public long FileCount { get; set; }
    public long LogicalBytes { get; set; }
    public double StoragePercentage { get; set; }

    public string FormattedCount => SizeFormatter.FormatCount(FileCount);
    public string FormattedSize => SizeFormatter.Format(LogicalBytes);
    public string FormattedPercentage => $"{StoragePercentage:F1}%";
}

public class FileTypesViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private long _totalIndexedBytes;
    private long _totalIndexedFiles;

    public FileTypesViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        Categories = [];
    }

    public ObservableCollection<FileTypeItem> Categories { get; }

    public long TotalIndexedBytes
    {
        get => _totalIndexedBytes;
        set => SetProperty(ref _totalIndexedBytes, value);
    }

    public long TotalIndexedFiles
    {
        get => _totalIndexedFiles;
        set => SetProperty(ref _totalIndexedFiles, value);
    }

    public string FormattedTotalBytes => SizeFormatter.Format(_totalIndexedBytes);
    public string FormattedTotalFiles => SizeFormatter.FormatCount(_totalIndexedFiles);

    public void RefreshData()
    {
        try
        {
            var breakdown = _dbService.GetCategoryBreakdown();
            Categories.Clear();

            long sumBytes = breakdown.Values.Sum(v => v.TotalSize);
            long sumFiles = breakdown.Values.Sum(v => v.Count);

            TotalIndexedBytes = sumBytes;
            TotalIndexedFiles = sumFiles;
            OnPropertyChanged(nameof(FormattedTotalBytes));
            OnPropertyChanged(nameof(FormattedTotalFiles));

            foreach (var categoryName in FileCategory.AllCategories)
            {
                if (breakdown.TryGetValue(categoryName, out var data))
                {
                    double pct = sumBytes > 0 ? (double)data.TotalSize / sumBytes * 100.0 : 0.0;
                    Categories.Add(new FileTypeItem
                    {
                        Category = categoryName,
                        FileCount = data.Count,
                        LogicalBytes = data.TotalSize,
                        StoragePercentage = pct
                    });
                }
                else
                {
                    Categories.Add(new FileTypeItem
                    {
                        Category = categoryName,
                        FileCount = 0,
                        LogicalBytes = 0,
                        StoragePercentage = 0.0
                    });
                }
            }
        }
        catch { }
    }
}
