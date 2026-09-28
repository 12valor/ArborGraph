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

    public PhotoshopViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;
        PhotoshopFiles = [];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });
        RefreshCommand = new RelayCommand(_ => RefreshData());
    }

    public ObservableCollection<FileRecord> PhotoshopFiles { get; }

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
        var (psdCnt, psbCnt, otherCnt, totalBytes, psdBytes, psbBytes) = _dbService.GetPhotoshopStats();

        PsdCount = psdCnt;
        PsbCount = psbCnt;
        OtherCount = otherCnt;
        TotalPhotoshopBytes = totalBytes;
        PsdBytes = psdBytes;
        PsbBytes = psbBytes;

        OnPropertyChanged(nameof(FormattedTotalBytes));
        OnPropertyChanged(nameof(FormattedPsdBytes));
        OnPropertyChanged(nameof(FormattedPsbBytes));

        var files = _dbService.GetPhotoshopFiles(300);
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
}
