using System.Collections.ObjectModel;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class LargestFoldersViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly FileActionService _fileActionService;
    private DirectoryRecord? _selectedFolder;
    private long _totalIndexedBytes;

    private bool _isLoading;

    public LargestFoldersViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;
        Folders = [];

        OpenFolderCommand = new RelayCommand(_ => { if (SelectedFolder != null) _fileActionService.OpenFileLocation(SelectedFolder.Path); });
        CopyFolderPathCommand = new RelayCommand(_ => { if (SelectedFolder != null) _fileActionService.CopyPath(SelectedFolder.Path); });
        RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());
    }

    public ObservableCollection<DirectoryRecord> Folders { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public DirectoryRecord? SelectedFolder
    {
        get => _selectedFolder;
        set => SetProperty(ref _selectedFolder, value);
    }

    public long TotalIndexedBytes
    {
        get => _totalIndexedBytes;
        set => SetProperty(ref _totalIndexedBytes, value);
    }

    public ICommand OpenFolderCommand { get; }
    public ICommand CopyFolderPathCommand { get; }
    public ICommand RefreshCommand { get; }

    public void RefreshData(long totalBytes = 0)
    {
        _ = RefreshDataAsync(totalBytes);
    }

    public async Task RefreshDataAsync(long totalBytes = 0)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            if (totalBytes > 0)
            {
                TotalIndexedBytes = totalBytes;
            }

            var list = await Task.Run(() => _dbService.GetLargestFolders(150));
            Folders.Clear();

            long maxFolderSize = Math.Max(1L, list.Count > 0 ? list.Max(f => f.Size) : 1L);

            foreach (var folder in list)
            {
                folder.PercentageOfTotal = (double)folder.Size / maxFolderSize * 100.0;
                Folders.Add(folder);
            }

            if (SelectedFolder == null || !Folders.Contains(SelectedFolder))
            {
                SelectedFolder = Folders.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LargestFolders RefreshData error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
