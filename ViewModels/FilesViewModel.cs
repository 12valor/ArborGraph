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
                OnSubTabChanged(value);
            }
        }
    }

    public ICommand SelectSubTabCommand { get; }

    private void OnSubTabChanged(string tab)
    {
        switch (tab)
        {
            case "LargestFiles":
                _ = LargestFilesVM.RefreshDataAsync();
                break;
            case "LargestFolders":
                _ = LargestFoldersVM.RefreshDataAsync(_mainVm.OverviewVM.Stats.LogicalBytesIndexed);
                break;
            case "FileTypes":
                _ = FileTypesVM.RefreshDataAsync();
                break;
            case "OldFiles":
                _ = OldFilesVM.RefreshDataAsync();
                break;
        }
    }
}
