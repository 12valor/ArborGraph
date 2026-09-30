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
        ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync(), _ => Files.Count > 0);
    }

    public ObservableCollection<FileRecord> Files { get; }
    public IReadOnlyList<string> MinSizeOptions { get; }
    public IReadOnlyList<string> Categories { get; }
    public ICommand ExportCsvCommand { get; }

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

    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
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
            long minBytes = ParseMinSize(_selectedMinSizeOption);
            int page = CurrentPage;
            int offset = Math.Max(0, (page - 1) * PageSize);
            string cat = _selectedCategory;
            string search = _searchText;
            string sort = _sortBy;
            bool desc = _sortDesc;

            var (count, list) = await Task.Run(() =>
            {
                long c = _dbService.GetFilteredFileCount(minBytes, long.MaxValue, cat, search);
                var l = _dbService.GetFilesPaged(offset, PageSize, minBytes, long.MaxValue, cat, search, sort, desc);
                return (c, l);
            });

            TotalMatchingFiles = count;
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
            (ExportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LargestFiles RefreshData error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ExportCsvAsync()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"diskscope_largest_files_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = "Export Largest Files to CSV"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var exporter = new ExportService();
                await exporter.ExportFilesToCsvAsync(Files, sfd.FileName);
                var res = System.Windows.MessageBox.Show(
                    $"Exported {Files.Count:N0} files to:\n{sfd.FileName}\n\nWould you like to open it now?",
                    "Export Successful",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (res == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
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
