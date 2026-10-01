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
    private string _selectedAgeOption = "Any Age";
    private string _extensionFilter = string.Empty;
    private string _locationPrefix = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedSortOption = "Size (Largest First)";

    private FileRecord? _selectedFile;
    private bool _isLoading;

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

        AgeOptions =
        [
            "Any Age",
            "> 30 Days",
            "> 90 Days",
            "> 180 Days",
            "> 1 Year",
            "> 2 Years"
        ];

        SortOptions =
        [
            "Size (Largest First)",
            "Size (Smallest First)",
            "Date (Newest First)",
            "Date (Oldest First)",
            "Name (A to Z)",
            "Path (A to Z)"
        ];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });

        MoveToRecycleBinCommand = new RelayCommand(_ => MoveSelectedToRecycleBin(), _ => SelectedFile != null);
        DeletePermanentlyCommand = new RelayCommand(_ => DeleteSelectedPermanently(), _ => SelectedFile != null);

        NextPageCommand = new RelayCommand(_ => { CurrentPage++; RefreshData(); }, _ => CurrentPage < TotalPages);
        PrevPageCommand = new RelayCommand(_ => { CurrentPage--; RefreshData(); }, _ => CurrentPage > 1);
        RefreshCommand = new RelayCommand(_ => { CurrentPage = 1; RefreshData(); });
        ClearFiltersCommand = new RelayCommand(_ => ClearAllFilters());
        ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync(), _ => Files.Count > 0);
    }

    public ObservableCollection<FileRecord> Files { get; }
    public IReadOnlyList<string> MinSizeOptions { get; }
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> AgeOptions { get; }
    public IReadOnlyList<string> SortOptions { get; }

    public ICommand ExportCsvCommand { get; }
    public ICommand MoveToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }

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

    public string SelectedAgeOption
    {
        get => _selectedAgeOption;
        set
        {
            if (SetProperty(ref _selectedAgeOption, value))
            {
                CurrentPage = 1;
                RefreshData();
            }
        }
    }

    public string ExtensionFilter
    {
        get => _extensionFilter;
        set
        {
            if (SetProperty(ref _extensionFilter, value))
            {
                CurrentPage = 1;
                RefreshData();
            }
        }
    }

    public string LocationPrefix
    {
        get => _locationPrefix;
        set
        {
            if (SetProperty(ref _locationPrefix, value))
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

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
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

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public void ClearAllFilters()
    {
        _selectedMinSizeOption = "All files";
        _selectedCategory = "All";
        _selectedAgeOption = "Any Age";
        _extensionFilter = string.Empty;
        _locationPrefix = string.Empty;
        _searchText = string.Empty;
        _selectedSortOption = "Size (Largest First)";

        OnPropertyChanged(nameof(SelectedMinSizeOption));
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(SelectedAgeOption));
        OnPropertyChanged(nameof(ExtensionFilter));
        OnPropertyChanged(nameof(LocationPrefix));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedSortOption));

        CurrentPage = 1;
        RefreshData();
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
            int? minDaysOld = ParseMinDaysOld(_selectedAgeOption);
            string ext = string.IsNullOrWhiteSpace(_extensionFilter) ? null! : _extensionFilter.Trim();
            string loc = string.IsNullOrWhiteSpace(_locationPrefix) ? null! : _locationPrefix.Trim();
            var (sortBy, sortDesc) = ParseSortOption(_selectedSortOption);

            int page = CurrentPage;
            int offset = Math.Max(0, (page - 1) * PageSize);
            string cat = _selectedCategory;
            string search = _searchText;

            var (count, list) = await Task.Run(() =>
            {
                long c = _dbService.GetFilteredFileCount(minBytes, long.MaxValue, cat, search, minDaysOld, ext, loc);
                var l = _dbService.GetFilesPaged(offset, PageSize, minBytes, long.MaxValue, cat, search, sortBy, sortDesc, minDaysOld, ext, loc);
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

    private void MoveSelectedToRecycleBin()
    {
        if (SelectedFile == null) return;
        string targetPath = SelectedFile.Path;
        var fileToRemove = SelectedFile;

        if (_fileActionService.MoveToRecycleBin(targetPath, out string? err))
        {
            _dbService.RemoveFileFromIndex(targetPath);
            Files.Remove(fileToRemove);
            TotalMatchingFiles = Math.Max(0, TotalMatchingFiles - 1);
            SelectedFile = Files.FirstOrDefault();
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
            Files.Remove(fileToRemove);
            TotalMatchingFiles = Math.Max(0, TotalMatchingFiles - 1);
            SelectedFile = Files.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            System.Windows.MessageBox.Show(err, "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    public async Task ExportCsvAsync()
    {
        bool exportAll = false;

        if (TotalMatchingFiles > Files.Count)
        {
            var choice = System.Windows.MessageBox.Show(
                $"Would you like to export ALL matching results or only the CURRENT PAGE?\n\n" +
                $"• Total Matching: {TotalMatchingFiles:N0} files\n" +
                $"• Current Page: {Files.Count:N0} files\n\n" +
                $"Click [Yes] to export ALL matching results (streamed without loading all records into memory).\n" +
                $"Click [No] to export only the CURRENT PAGE ({Files.Count:N0} files).\n" +
                $"Click [Cancel] to abort.",
                "Choose Export Scope",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            if (choice == System.Windows.MessageBoxResult.Cancel) return;
            exportAll = (choice == System.Windows.MessageBoxResult.Yes);
        }

        string defaultName = exportAll
            ? $"diskscope_all_matching_files_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            : $"diskscope_largest_files_page{CurrentPage}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = defaultName,
            Title = exportAll ? "Export All Matching Results to CSV (Streamed)" : "Export Current Page to CSV"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var exporter = new ExportService();
                if (exportAll)
                {
                    long minBytes = ParseMinSize(SelectedMinSizeOption);
                    int? minDaysOld = ParseMinDaysOld(SelectedAgeOption);
                    string ext = string.IsNullOrWhiteSpace(ExtensionFilter) ? null! : ExtensionFilter.Trim();
                    string loc = string.IsNullOrWhiteSpace(LocationPrefix) ? null! : LocationPrefix.Trim();
                    var (sortBy, sortDesc) = ParseSortOption(SelectedSortOption);

                    await exporter.StreamQueryToCsvAsync(
                        _dbService,
                        sfd.FileName,
                        minBytes,
                        maxSize: long.MaxValue,
                        SelectedCategory,
                        SearchText,
                        sortBy,
                        sortDesc,
                        minDaysOld,
                        ext,
                        loc);

                    var res = System.Windows.MessageBox.Show(
                        $"Successfully exported ALL matching results ({TotalMatchingFiles:N0} files) to:\n{sfd.FileName}\n\n[Export Mode: Full Query Streaming]\n\nWould you like to open it now?",
                        "Export Successful",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (res == System.Windows.MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                    }
                }
                else
                {
                    await exporter.ExportFilesToCsvAsync(Files, sfd.FileName);
                    var res = System.Windows.MessageBox.Show(
                        $"Successfully exported CURRENT PAGE ({Files.Count:N0} files) to:\n{sfd.FileName}\n\n[Export Mode: Current Page View]\n\nWould you like to open it now?",
                        "Export Successful",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (res == System.Windows.MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                    }
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

    private static int? ParseMinDaysOld(string option)
    {
        return option switch
        {
            "> 30 Days" => 30,
            "> 90 Days" => 90,
            "> 180 Days" => 180,
            "> 1 Year" => 365,
            "> 2 Years" => 730,
            _ => null
        };
    }

    private static (string SortBy, bool SortDesc) ParseSortOption(string option)
    {
        return option switch
        {
            "Size (Smallest First)" => ("size", false),
            "Date (Newest First)" => ("modified_time", true),
            "Date (Oldest First)" => ("modified_time", false),
            "Name (A to Z)" => ("name", false),
            "Path (A to Z)" => ("path", false),
            _ => ("size", true)
        };
    }
}
