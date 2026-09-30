using System.Collections.ObjectModel;
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
    private FileRecord? _selectedFile;

    private bool _isLoading;

    public PhotoshopViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;
        PhotoshopFiles = [];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });
        RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());
    }

    public ObservableCollection<FileRecord> PhotoshopFiles { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public FileRecord? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
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
        set => SetProperty(ref _totalPhotoshopBytes, value);
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

    public string FormattedTotalBytes => SizeFormatter.Format(_totalPhotoshopBytes);
    public string FormattedPsdBytes => SizeFormatter.Format(_psdBytes);
    public string FormattedPsbBytes => SizeFormatter.Format(_psbBytes);

    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand RefreshCommand { get; }

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
            var (stats, files) = await Task.Run(() =>
            {
                var s = _dbService.GetPhotoshopStats();
                var f = _dbService.GetPhotoshopFiles(300);
                return (s, f);
            });

            PsdCount = stats.PsdCount;
            PsbCount = stats.PsbCount;
            OtherCount = stats.OtherCount;
            TotalPhotoshopBytes = stats.TotalBytes;
            PsdBytes = stats.PsdBytes;
            PsbBytes = stats.PsbBytes;

            OnPropertyChanged(nameof(FormattedTotalBytes));
            OnPropertyChanged(nameof(FormattedPsdBytes));
            OnPropertyChanged(nameof(FormattedPsbBytes));

            PhotoshopFiles.Clear();
            foreach (var item in files)
            {
                PhotoshopFiles.Add(item);
            }

            if (SelectedFile == null || !PhotoshopFiles.Contains(SelectedFile))
            {
                SelectedFile = PhotoshopFiles.FirstOrDefault();
            }
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
}
