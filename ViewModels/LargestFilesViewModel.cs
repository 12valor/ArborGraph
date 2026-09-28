using System.Collections.ObjectModel;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class LargestFilesViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly FileActionService _fileActionService;

    private int _currentPage = 1;
    private const int PageSize = 100;
    private long _totalMatchingFiles;
    private string _selectedMinSizeOption = "All files";
    private string _selectedCategory = "All";
    private string _searchText = string.Empty;
    private string _sortBy = "size";
    private bool _sortDesc = true;
    private FileRecord? _selectedFile;

    public LargestFilesViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;

        Files = [];
        MinSizeOptions =
        [
            "All files",
            "1 KB",
            "100 KB",
            "1 MB",
            "10 MB",
            "50 MB",
            "100 MB",
            "500 MB",
            "1 GB"
        ];

        Categories =
        [
            "All",
            FileCategory.Photoshop,
            FileCategory.Images,
            FileCategory.Video,
            FileCategory.Audio,
            FileCategory.Archives,
            FileCategory.Documents,
            FileCategory.Code,
            FileCategory.Executables,
            FileCategory.Other
        ];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });

        NextPageCommand = new RelayCommand(_ => { CurrentPage++; RefreshData(); }, _ => CurrentPage < TotalPages);
        PrevPageCommand = new RelayCommand(_ => { CurrentPage--; RefreshData(); }, _ => CurrentPage > 1);
        RefreshCommand = new RelayCommand(_ => { CurrentPage = 1; RefreshData(); });
    }

    public ObservableCollection<FileRecord> Files { get; }
    public IReadOnlyList<string> MinSizeOptions { get; }
    public IReadOnlyList<string> Categories { get; }

    public FileRecord? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public string SelectedMinSizeOption
    {
        get => _selectedMinSizeOption;
        set
        {
            if (SetProperty(ref _selectedMinSizeOption, value))
            {
                CurrentPage = 1;
                RefreshData();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                CurrentPage = 1;
                RefreshData();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                CurrentPage = 1;
                RefreshData();
            }
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public long TotalMatchingFiles
    {
        get => _totalMatchingFiles;
        private set
        {
            if (SetProperty(ref _totalMatchingFiles, value))
            {
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageSummary));
            }
        }
    }

    public int TotalPages => (int)Math.Max(1, Math.Ceiling((double)_totalMatchingFiles / PageSize));
    public string PageSummary => $"Page {_currentPage} of {TotalPages} ({_totalMatchingFiles:N0} total matching files)";

    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand RefreshCommand { get; }

    public void RefreshData()
    {
        try
        {
            long minBytes = ParseMinSize(_selectedMinSizeOption);
            int offset = Math.Max(0, (CurrentPage - 1) * PageSize);

            TotalMatchingFiles = _dbService.GetFilteredFileCount(minBytes, long.MaxValue, _selectedCategory, _searchText);

            var list = _dbService.GetFilesPaged(
                offset,
                PageSize,
                minBytes,
                long.MaxValue,
                _selectedCategory,
                _searchText,
                _sortBy,
                _sortDesc);

            Files.Clear();
            foreach (var item in list)
            {
                Files.Add(item);
            }

            if (SelectedFile == null || !Files.Contains(SelectedFile))
            {
                SelectedFile = Files.FirstOrDefault();
            }

            (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrevPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch { }
    }

    private static long ParseMinSize(string option)
    {
        return option switch
        {
            "1 KB" => 1024L,
            "100 KB" => 100 * 1024L,
            "1 MB" => 1024 * 1024L,
            "10 MB" => 10 * 1024 * 1024L,
            "50 MB" => 50 * 1024 * 1024L,
            "100 MB" => 100 * 1024 * 1024L,
            "500 MB" => 500 * 1024 * 1024L,
            "1 GB" => 1024 * 1024 * 1024L,
            _ => 0L
        };
    }
}
