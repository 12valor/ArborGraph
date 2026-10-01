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

    private bool _isLoading;

    public OldFilesViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;
        OldFiles = [];

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });
        RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());

        MoveToRecycleBinCommand = new RelayCommand(_ => MoveSelectedToRecycleBin(), _ => SelectedFile != null);
        DeletePermanentlyCommand = new RelayCommand(_ => DeleteSelectedPermanently(), _ => SelectedFile != null);
    }

    public ObservableCollection<FileRecord> OldFiles { get; }

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
    public ICommand MoveToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }

    private void MoveSelectedToRecycleBin()
    {
        if (SelectedFile == null) return;
        string targetPath = SelectedFile.Path;
        var fileToRemove = SelectedFile;

        if (_fileActionService.MoveToRecycleBin(targetPath, out string? err))
        {
            _dbService.RemoveFileFromIndex(targetPath);
            OldFiles.Remove(fileToRemove);
            SelectedFile = OldFiles.FirstOrDefault();
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
            OldFiles.Remove(fileToRemove);
            SelectedFile = OldFiles.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(err))
        {
            System.Windows.MessageBox.Show(err, "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
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
            int days = _selectedDays;
            var list = await Task.Run(() => _dbService.GetOldFiles(days, 300));
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OldFiles RefreshData error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
