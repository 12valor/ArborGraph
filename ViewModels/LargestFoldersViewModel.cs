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
    private bool _isDeleting;
    private string _deletionStatus = string.Empty;

    public LargestFoldersViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;
        Folders = [];

        OpenFolderCommand = new RelayCommand(_ => { if (SelectedFolder != null) _fileActionService.OpenFileLocation(SelectedFolder.Path); });
        CopyFolderPathCommand = new RelayCommand(_ => { if (SelectedFolder != null) _fileActionService.CopyPath(SelectedFolder.Path); });
        RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync(), _ => !IsDeleting && !IsLoading);

        MoveToRecycleBinCommand = new RelayCommand(async _ => await MoveSelectedToRecycleBinAsync(), _ => SelectedFolder != null && !IsDeleting && !IsLoading);
        DeletePermanentlyCommand = new RelayCommand(async _ => await DeleteSelectedPermanentlyAsync(), _ => SelectedFolder != null && !IsDeleting && !IsLoading);
    }

    public ObservableCollection<DirectoryRecord> Folders { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveToRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsDeleting
    {
        get => _isDeleting;
        set
        {
            if (SetProperty(ref _isDeleting, value))
            {
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveToRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string DeletionStatus
    {
        get => _deletionStatus;
        set => SetProperty(ref _deletionStatus, value);
    }

    public DirectoryRecord? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                (MoveToRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public long TotalIndexedBytes
    {
        get => _totalIndexedBytes;
        set => SetProperty(ref _totalIndexedBytes, value);
    }

    public ICommand OpenFolderCommand { get; }
    public ICommand CopyFolderPathCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand MoveToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }

    private async Task MoveSelectedToRecycleBinAsync()
    {
        if (SelectedFolder == null || IsDeleting) return;
        var folderToRemove = SelectedFolder;
        string targetPath = folderToRemove.Path;

        if (_fileActionService.IsProtectedPath(targetPath))
        {
            System.Windows.MessageBox.Show($"Protected system or root path cannot be deleted:\n{targetPath}", "Safety Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        IsDeleting = true;
        DeletionStatus = $"Moving \"{folderToRemove.Name}\" to Recycle Bin...";
        try
        {
            string? err = null;
            bool success = await Task.Run(() => _fileActionService.MoveToRecycleBin(targetPath, out err));

            if (success)
            {
                await Task.Run(() => _dbService.RemoveDirectoryFromIndex(targetPath));
                Folders.Remove(folderToRemove);
                SelectedFolder = Folders.FirstOrDefault();
            }
            else if (!string.IsNullOrEmpty(err))
            {
                System.Windows.MessageBox.Show(err, "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error moving directory to Recycle Bin:\n{ex.Message}", "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsDeleting = false;
            DeletionStatus = string.Empty;
        }
    }

    private async Task DeleteSelectedPermanentlyAsync()
    {
        if (SelectedFolder == null || IsDeleting) return;
        var folderToRemove = SelectedFolder;
        string targetPath = folderToRemove.Path;

        if (_fileActionService.IsProtectedPath(targetPath))
        {
            System.Windows.MessageBox.Show($"Protected system or root path cannot be permanently deleted:\n{targetPath}", "Safety Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var res = System.Windows.MessageBox.Show(
            $"Are you sure you want to PERMANENTLY delete this directory?\n\n{targetPath}\n\nWARNING: This cannot be undone.",
            "Confirm Permanent Deletion",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (res != System.Windows.MessageBoxResult.Yes) return;

        IsDeleting = true;
        DeletionStatus = $"Permanently deleting \"{folderToRemove.Name}\"...";
        try
        {
            string? err = null;
            bool success = await Task.Run(() => _fileActionService.DeletePermanently(targetPath, out err, skipConfirmation: true));

            if (success)
            {
                await Task.Run(() => _dbService.RemoveDirectoryFromIndex(targetPath));
                Folders.Remove(folderToRemove);
                SelectedFolder = Folders.FirstOrDefault();
            }
            else if (!string.IsNullOrEmpty(err))
            {
                System.Windows.MessageBox.Show(err, "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error permanently deleting directory:\n{ex.Message}", "Cleanup Notice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsDeleting = false;
            DeletionStatus = string.Empty;
        }
    }

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
