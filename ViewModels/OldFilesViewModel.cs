using System.Collections.ObjectModel;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class OldFilesViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly FileActionService _fileActionService;
    private FileRecord? _selectedFile;
    private int _selectedDays = 180;

    public OldFilesViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;
        OldFiles = [];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });
        RefreshCommand = new RelayCommand(_ => RefreshData());
    }

    public ObservableCollection<FileRecord> OldFiles { get; }

    public FileRecord? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public int SelectedDays
    {
        get => _selectedDays;
        set
        {
            if (SetProperty(ref _selectedDays, value))
            {
                RefreshData();
            }
        }
    }

    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand RefreshCommand { get; }

    public void RefreshData()
    {
        try
        {
            var list = _dbService.GetOldFiles(_selectedDays, 300);
            OldFiles.Clear();
            foreach (var item in list)
            {
                OldFiles.Add(item);
            }

            if (SelectedFile == null || !OldFiles.Contains(SelectedFile))
            {
                SelectedFile = OldFiles.FirstOrDefault();
            }
        }
        catch { }
    }
}
