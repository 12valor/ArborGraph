using System.Windows.Input;
using DiskScope.Infrastructure;

namespace DiskScope.ViewModels;

public class FilesViewModel : ObservableObject
{
    private readonly MainViewModel _mainVm;
    private string _selectedSubTab = "LargestFiles";

    public FilesViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
        SelectSubTabCommand = new RelayCommand(param =>
        {
            if (param is string subTab)
            {
                SelectedSubTab = subTab;
            }
        });
    }

    public LargestFilesViewModel LargestFilesVM => _mainVm.LargestFilesVM;
    public LargestFoldersViewModel LargestFoldersVM => _mainVm.LargestFoldersVM;
    public FileTypesViewModel FileTypesVM => _mainVm.FileTypesVM;
    public OldFilesViewModel OldFilesVM => _mainVm.OldFilesVM;

    public string SelectedSubTab
    {
        get => _selectedSubTab;
        set
        {
            if (SetProperty(ref _selectedSubTab, value))
            {
                OnPropertyChanged(nameof(IsLargestFilesSelected));
                OnPropertyChanged(nameof(IsLargestFoldersSelected));
                OnPropertyChanged(nameof(IsFileTypesSelected));
                OnPropertyChanged(nameof(IsOldFilesSelected));
                OnSubTabChanged(value);
            }
        }
    }

    public bool IsLargestFilesSelected
    {
        get => SelectedSubTab == "LargestFiles";
        set { if (value) SelectedSubTab = "LargestFiles"; }
    }

    public bool IsLargestFoldersSelected
    {
        get => SelectedSubTab == "LargestFolders";
        set { if (value) SelectedSubTab = "LargestFolders"; }
    }

    public bool IsFileTypesSelected
    {
        get => SelectedSubTab == "FileTypes";
        set { if (value) SelectedSubTab = "FileTypes"; }
    }

    public bool IsOldFilesSelected
    {
        get => SelectedSubTab == "OldFiles";
        set { if (value) SelectedSubTab = "OldFiles"; }
    }

    public ICommand SelectSubTabCommand { get; }

    public async Task RefreshCurrentSubTabAsync()
    {
        switch (SelectedSubTab)
        {
            case "LargestFiles":
                await LargestFilesVM.RefreshDataAsync();
                break;
            case "LargestFolders":
                await LargestFoldersVM.RefreshDataAsync(_mainVm.OverviewVM.Stats.LogicalBytesIndexed);
                break;
            case "FileTypes":
                await FileTypesVM.RefreshDataAsync();
                break;
            case "OldFiles":
                await OldFilesVM.RefreshDataAsync();
                break;
        }
    }

    private void OnSubTabChanged(string tab)
    {
        _ = RefreshCurrentSubTabAsync();
    }
}
