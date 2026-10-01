using System.Collections.ObjectModel;
using System.Windows.Input;
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
    private readonly FileActionService _fileActionService;
    private long _totalIndexedBytes;
    private long _totalIndexedFiles;

    private bool _isLoading;
    private bool _isLoadingFiles;
    private FileTypeItem? _selectedCategory;
    private FileRecord? _selectedFile;
    private string _filterSearchText = string.Empty;
    private string _sortBy = "size";
    private bool _sortDesc = true;

    public FileTypesViewModel(DatabaseService dbService, FileActionService? fileActionService = null)
    {
        _dbService = dbService;
        _fileActionService = fileActionService ?? new FileActionService();
        Categories = [];
        CategoryFiles = [];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });

        MoveToRecycleBinCommand = new RelayCommand(_ => MoveSelectedToRecycleBin(), _ => SelectedFile != null);
        DeletePermanentlyCommand = new RelayCommand(_ => DeleteSelectedPermanently(), _ => SelectedFile != null);
    }

    public ObservableCollection<FileTypeItem> Categories { get; }
    public ObservableCollection<FileRecord> CategoryFiles { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsLoadingFiles
    {
        get => _isLoadingFiles;
        set => SetProperty(ref _isLoadingFiles, value);
    }

    public FileTypeItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                _ = LoadCategoryFilesAsync();
            }
        }
    }

    public FileRecord? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                (MoveToRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string FilterSearchText
    {
        get => _filterSearchText;
        set
        {
            if (SetProperty(ref _filterSearchText, value))
            {
                _ = LoadCategoryFilesAsync();
            }
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (SetProperty(ref _sortBy, value))
            {
                _ = LoadCategoryFilesAsync();
            }
        }
    }

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

    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand MoveToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }

    public void RefreshData()
    {
        _ = RefreshDataAsync();
    }

    public async Task RefreshDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var breakdown = await Task.Run(() => _dbService.GetCategoryBreakdown());
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

            if (SelectedCategory == null || !Categories.Any(c => c.Category == SelectedCategory.Category))
            {
                SelectedCategory = Categories.FirstOrDefault(c => c.LogicalBytes > 0) ?? Categories.FirstOrDefault();
            }
            else
            {
                _ = LoadCategoryFilesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileTypes RefreshData error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadCategoryFilesAsync()
    {
        if (SelectedCategory == null)
        {
            CategoryFiles.Clear();
            SelectedFile = null;
            return;
        }

        IsLoadingFiles = true;
        try
        {
            string cat = SelectedCategory.Category;
            string search = FilterSearchText;
            string sort = SortBy;

            var files = await Task.Run(() => _dbService.GetFilesPaged(
                offset: 0,
                limit: 300,
                minSize: 0,
                maxSize: long.MaxValue,
                category: cat,
                search: search,
                sortBy: sort,
                sortDesc: _sortDesc));

            CategoryFiles.Clear();
            foreach (var f in files)
            {
                CategoryFiles.Add(f);
            }

            SelectedFile = CategoryFiles.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadCategoryFiles error: {ex.Message}");
        }
        finally
        {
            IsLoadingFiles = false;
        }
    }

    private void MoveSelectedToRecycleBin()
    {
        if (SelectedFile == null) return;
        string targetPath = SelectedFile.Path;
        var fileToRemove = SelectedFile;

        if (_fileActionService.MoveToRecycleBin(targetPath, out string? err))
        {
            _dbService.RemoveFileFromIndex(targetPath);
            CategoryFiles.Remove(fileToRemove);
            if (SelectedCategory != null)
            {
                SelectedCategory.FileCount = Math.Max(0, SelectedCategory.FileCount - 1);
                SelectedCategory.LogicalBytes = Math.Max(0, SelectedCategory.LogicalBytes - fileToRemove.Size);
            }
            SelectedFile = CategoryFiles.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            System.Windows.MessageBox.Show(err, "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private void DeleteSelectedPermanently()
    {
        if (SelectedFile == null) return;
        string targetPath = SelectedFile.Path;
        var fileToRemove = SelectedFile;

        if (_fileActionService.DeletePermanently(targetPath, out string? err))
        {
            _dbService.RemoveFileFromIndex(targetPath);
            CategoryFiles.Remove(fileToRemove);
            if (SelectedCategory != null)
            {
                SelectedCategory.FileCount = Math.Max(0, SelectedCategory.FileCount - 1);
                SelectedCategory.LogicalBytes = Math.Max(0, SelectedCategory.LogicalBytes - fileToRemove.Size);
            }
            SelectedFile = CategoryFiles.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            System.Windows.MessageBox.Show(err, "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}
