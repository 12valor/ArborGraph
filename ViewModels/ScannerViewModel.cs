using System.Collections.ObjectModel;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;

namespace DiskScope.ViewModels;

public class ScannerViewModel : ObservableObject
{
    private readonly MainViewModel _mainVm;
    private bool _showTechnicalDetails;

    public ScannerViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
        ExploreFilesCommand = new RelayCommand(_ => _mainVm.NavigateTo("Files"));
        ReviewCleanupCommand = new RelayCommand(_ => _mainVm.NavigateTo("CleanupCenter"));

        // Propagate changes from MainViewModel and OverviewVM so ScannerView updates immediately
        _mainVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsScanning))
            {
                OnPropertyChanged(nameof(IsScanning));
                OnPropertyChanged(nameof(Stats));
                OnPropertyChanged(nameof(CurrentTargetDescription));
                CommandManager.InvalidateRequerySuggested();
            }
            else if (e.PropertyName == nameof(MainViewModel.CustomScanPath))
            {
                OnPropertyChanged(nameof(CustomScanPath));
                OnPropertyChanged(nameof(CurrentTargetDescription));
            }
        };

        _mainVm.OverviewVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(OverviewViewModel.Stats))
            {
                OnPropertyChanged(nameof(Stats));
            }
        };
    }

    public OverviewViewModel OverviewVM => _mainVm.OverviewVM;
    public ScanStats Stats => _mainVm.OverviewVM.Stats;
    public ObservableCollection<string> RecentDirectories => _mainVm.OverviewVM.RecentDirectories;
    public bool IsScanning => _mainVm.IsScanning;

    public string CustomScanPath
    {
        get => _mainVm.CustomScanPath;
        set
        {
            if (_mainVm.CustomScanPath != value)
            {
                _mainVm.CustomScanPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTargetDescription));
            }
        }
    }

    public ICommand BrowseCustomFolderCommand => _mainVm.BrowseCustomFolderCommand;
    public ICommand StartScanCommand => _mainVm.StartScanCommand;
    public ICommand StopScanCommand => _mainVm.StopScanCommand;
    public ICommand ExploreFilesCommand { get; }
    public ICommand ReviewCleanupCommand { get; }

    public bool ShowTechnicalDetails
    {
        get => _showTechnicalDetails;
        set => SetProperty(ref _showTechnicalDetails, value);
    }

    public string CurrentTargetDescription
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_mainVm.CustomScanPath))
            {
                return _mainVm.CustomScanPath;
            }

            var selected = _mainVm.OverviewVM.Drives.Where(d => d.IsSelected).Select(d => d.Name).ToList();
            if (selected.Count > 0)
            {
                return string.Join(", ", selected);
            }

            return "C:\\";
        }
    }
}
