using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class PhotoshopViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly FileActionService _fileActionService;

    private long _psdCount;
    private long _psbCount;
    private long _otherCount;
    private long _totalPhotoshopBytes;
    private long _psdBytes;
    private long _psbBytes;
    private long _reclaimableBytes;
    private long _cacheAndScratchCount;

    private FileRecord? _selectedFile;
    private PhotoshopItem? _selectedPhotoshopItem;
    private string _selectedFilter = "All Items";
    private bool _isLoading;

    private readonly List<PhotoshopItem> _allDetailedItems = [];

    public PhotoshopViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;

        PhotoshopFiles = [];
        PhotoshopItems = [];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });
        RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());

        MoveToRecycleBinCommand = new RelayCommand(_ => MoveSelectedToRecycleBin(), _ => SelectedFile != null);
        DeletePermanentlyCommand = new RelayCommand(_ => DeleteSelectedPermanently(), _ => SelectedFile != null);
        CleanSafeCachesCommand = new RelayCommand(_ => CleanSafeCaches(), _ => ReclaimableBytes > 0);
    }

    public ObservableCollection<FileRecord> PhotoshopFiles { get; }
    public ObservableCollection<PhotoshopItem> PhotoshopItems { get; }
    public ObservableCollection<string> FilterOptions { get; } =
    [
        "All Items",
        "User Documents (.PSD / .PSB)",
        "Scratch & Temp Files (Reclaimable)",
        "Adobe Caches (Reclaimable)",
        "Presets & Assets"
    ];

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
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

    public PhotoshopItem? SelectedPhotoshopItem
    {
        get => _selectedPhotoshopItem;
        set
        {
            if (SetProperty(ref _selectedPhotoshopItem, value))
            {
                SelectedFile = value?.File;
            }
        }
    }

    public long PsdCount
    {
        get => _psdCount;
        set => SetProperty(ref _psdCount, value);
    }

    public long PsbCount
    {
        get => _psbCount;
        set => SetProperty(ref _psbCount, value);
    }

    public long OtherCount
    {
        get => _otherCount;
        set => SetProperty(ref _otherCount, value);
    }

    public long TotalPhotoshopBytes
    {
        get => _totalPhotoshopBytes;
        set
        {
            if (SetProperty(ref _totalPhotoshopBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalBytes));
            }
        }
    }

    public long PsdBytes
    {
        get => _psdBytes;
        set => SetProperty(ref _psdBytes, value);
    }

    public long PsbBytes
    {
        get => _psbBytes;
        set => SetProperty(ref _psbBytes, value);
    }

    public long ReclaimableBytes
    {
        get => _reclaimableBytes;
        set
        {
            if (SetProperty(ref _reclaimableBytes, value))
            {
                OnPropertyChanged(nameof(FormattedReclaimableBytes));
                (CleanSafeCachesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public long CacheAndScratchCount
    {
        get => _cacheAndScratchCount;
        set => SetProperty(ref _cacheAndScratchCount, value);
    }

    public string FormattedTotalBytes => SizeFormatter.Format(_totalPhotoshopBytes);
    public string FormattedPsdBytes => SizeFormatter.Format(_psdBytes);
    public string FormattedPsbBytes => SizeFormatter.Format(_psbBytes);
    public string FormattedReclaimableBytes => SizeFormatter.Format(_reclaimableBytes);
    public string FormattedUserDocumentsBytes => SizeFormatter.Format(_psdBytes + _psbBytes);

    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand MoveToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }
    public ICommand CleanSafeCachesCommand { get; }

    private void MoveSelectedToRecycleBin()
    {
        if (SelectedFile == null) return;
        string targetPath = SelectedFile.Path;
        var fileToRemove = SelectedFile;
        var itemToRemove = SelectedPhotoshopItem;

        if (_fileActionService.MoveToRecycleBin(targetPath, out string? err))
        {
            _dbService.RemoveFileFromIndex(targetPath);
            PhotoshopFiles.Remove(fileToRemove);
            if (itemToRemove != null)
            {
                PhotoshopItems.Remove(itemToRemove);
                _allDetailedItems.Remove(itemToRemove);
            }
            TotalPhotoshopBytes = Math.Max(0, TotalPhotoshopBytes - fileToRemove.Size);
            SelectedPhotoshopItem = PhotoshopItems.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            MessageBox.Show(err, "Cleanup Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeleteSelectedPermanently()
    {
        if (SelectedFile == null) return;
        string targetPath = SelectedFile.Path;
        var fileToRemove = SelectedFile;
        var itemToRemove = SelectedPhotoshopItem;

        if (_fileActionService.DeletePermanently(targetPath, out string? err))
        {
            _dbService.RemoveFileFromIndex(targetPath);
            PhotoshopFiles.Remove(fileToRemove);
            if (itemToRemove != null)
            {
                PhotoshopItems.Remove(itemToRemove);
                _allDetailedItems.Remove(itemToRemove);
            }
            TotalPhotoshopBytes = Math.Max(0, TotalPhotoshopBytes - fileToRemove.Size);
            SelectedPhotoshopItem = PhotoshopItems.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            MessageBox.Show(err, "Cleanup Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CleanSafeCaches()
    {
        var reclaimableList = _allDetailedItems.Where(i => i.IsReclaimable).ToList();
        if (reclaimableList.Count == 0) return;

        long totalBytesToClean = reclaimableList.Sum(i => i.File.Size);
        var dlgResult = MessageBox.Show(
            $"Are you sure you want to clean Photoshop scratch and Adobe cache files?\n\n" +
            $"• Files to clean: {reclaimableList.Count}\n" +
            $"• Space to recover: {SizeFormatter.Format(totalBytesToClean)}\n" +
            $"• Destination: Windows Recycle Bin\n\n" +
            $"Note: Active creative artwork (.PSD / .PSB) will NOT be touched. Please ensure Photoshop is closed.",
            "Confirm Scratch & Cache Cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (dlgResult != MessageBoxResult.Yes) return;

        int cleaned = 0;
        int failed = 0;
        long recovered = 0;

        foreach (var item in reclaimableList)
        {
            if (_fileActionService.MoveToRecycleBin(item.File.Path, out _))
            {
                _dbService.RemoveFileFromIndex(item.File.Path);
                cleaned++;
                recovered += item.File.Size;
                _allDetailedItems.Remove(item);
                PhotoshopItems.Remove(item);
                PhotoshopFiles.Remove(item.File);
            }
            else
            {
                failed++;
            }
        }

        TotalPhotoshopBytes = Math.Max(0, TotalPhotoshopBytes - recovered);
        ReclaimableBytes = Math.Max(0, ReclaimableBytes - recovered);
        CacheAndScratchCount = Math.Max(0, CacheAndScratchCount - cleaned);

        MessageBox.Show(
            $"Cleanup Complete!\n\n" +
            $"• Cleaned: {cleaned} files\n" +
            $"• Recovered: {SizeFormatter.Format(recovered)}\n" +
            $"• Failed / In Use: {failed} files",
            "Photoshop Cache Cleanup Result",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        SelectedPhotoshopItem = PhotoshopItems.FirstOrDefault();
    }

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
            var (stats, detailedItems) = await Task.Run(() =>
            {
                var s = _dbService.GetPhotoshopStats();
                var items = _dbService.GetPhotoshopDetailedItems(500);
                return (s, items);
            });

            PsdCount = stats.PsdCount;
            PsbCount = stats.PsbCount;
            OtherCount = stats.OtherCount;
            TotalPhotoshopBytes = stats.TotalBytes;
            PsdBytes = stats.PsdBytes;
            PsbBytes = stats.PsbBytes;

            _allDetailedItems.Clear();
            _allDetailedItems.AddRange(detailedItems);

            var reclaimable = _allDetailedItems.Where(i => i.IsReclaimable).ToList();
            ReclaimableBytes = reclaimable.Sum(i => i.File.Size);
            CacheAndScratchCount = reclaimable.Count;

            ApplyFilter();

            OnPropertyChanged(nameof(FormattedTotalBytes));
            OnPropertyChanged(nameof(FormattedPsdBytes));
            OnPropertyChanged(nameof(FormattedPsbBytes));
            OnPropertyChanged(nameof(FormattedReclaimableBytes));
            OnPropertyChanged(nameof(FormattedUserDocumentsBytes));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Photoshop RefreshData error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        PhotoshopItems.Clear();
        PhotoshopFiles.Clear();

        IEnumerable<PhotoshopItem> query = _allDetailedItems;

        switch (SelectedFilter)
        {
            case "User Documents (.PSD / .PSB)":
                query = query.Where(i => i.ItemType == PhotoshopItemType.UserDocument);
                break;
            case "Scratch & Temp Files (Reclaimable)":
                query = query.Where(i => i.ItemType == PhotoshopItemType.ScratchAndTemp);
                break;
            case "Adobe Caches (Reclaimable)":
                query = query.Where(i => i.ItemType == PhotoshopItemType.AdobeCache);
                break;
            case "Presets & Assets":
                query = query.Where(i => i.ItemType == PhotoshopItemType.PresetOrAsset);
                break;
        }

        foreach (var item in query)
        {
            PhotoshopItems.Add(item);
            PhotoshopFiles.Add(item.File);
        }

        if (SelectedPhotoshopItem == null || !PhotoshopItems.Contains(SelectedPhotoshopItem))
        {
            SelectedPhotoshopItem = PhotoshopItems.FirstOrDefault();
        }
    }
}
