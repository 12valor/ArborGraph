using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class DeveloperStorageViewModel : ObservableObject
{
    private readonly DeveloperStorageService _devService;
    private readonly FileActionService _fileActionService;

    private DeveloperEcosystemSummary? _selectedEcosystem;
    private DeveloperJunkItem? _selectedItem;
    private bool _isScanning;
    private string _statusText = "Ready to scan developer storage.";
    private string _customWorkspacePath = string.Empty;
    private long _totalDeveloperStorageBytes;
    private int _totalItemsCount;

    public DeveloperStorageViewModel(DeveloperStorageService devService, FileActionService fileActionService)
    {
        _devService = devService;
        _fileActionService = fileActionService;

        Ecosystems = [];
        DetailedItems = [];

        ScanIndexedCommand = new RelayCommand(async _ => await ScanIndexedStorageAsync(), _ => !IsScanning);
        RefreshCommand = new RelayCommand(async _ => await ScanIndexedStorageAsync(), _ => !IsScanning);
        BrowseWorkspaceCommand = new RelayCommand(_ => BrowseWorkspace(), _ => !IsScanning);
        ScanCustomWorkspaceCommand = new RelayCommand(async _ => await ScanCustomWorkspaceAsync(), _ => !IsScanning && !string.IsNullOrWhiteSpace(CustomWorkspacePath));

        OpenLocationCommand = new RelayCommand(_ =>
        {
            if (SelectedItem != null) _fileActionService.OpenFileLocation(SelectedItem.Path);
        }, _ => SelectedItem != null);

        CopyPathCommand = new RelayCommand(_ =>
        {
            if (SelectedItem != null) _fileActionService.CopyPath(SelectedItem.Path);
        }, _ => SelectedItem != null);

        MoveToRecycleBinCommand = new RelayCommand(async _ => await MoveSelectedToRecycleBinAsync(), _ => SelectedItem != null && !IsScanning);
        DeletePermanentlyCommand = new RelayCommand(async _ => await DeleteSelectedPermanentlyAsync(), _ => SelectedItem != null && !IsScanning);
        CleanEcosystemSafeCommand = new RelayCommand(async _ => await CleanSelectedEcosystemSafeAsync(), _ => SelectedEcosystem != null && SelectedEcosystem.TotalBytes > 0 && !IsScanning);
    }

    public ObservableCollection<DeveloperEcosystemSummary> Ecosystems { get; }
    public ObservableCollection<DeveloperJunkItem> DetailedItems { get; }

    public DeveloperEcosystemSummary? SelectedEcosystem
    {
        get => _selectedEcosystem;
        set
        {
            if (SetProperty(ref _selectedEcosystem, value))
            {
                UpdateDetailedItems();
                (CleanEcosystemSafeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public DeveloperJunkItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                (OpenLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyPathCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveToRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                (ScanIndexedCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (BrowseWorkspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ScanCustomWorkspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveToRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CleanEcosystemSafeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CustomWorkspacePath
    {
        get => _customWorkspacePath;
        set
        {
            if (SetProperty(ref _customWorkspacePath, value))
            {
                (ScanCustomWorkspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public long TotalDeveloperStorageBytes
    {
        get => _totalDeveloperStorageBytes;
        set
        {
            if (SetProperty(ref _totalDeveloperStorageBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalStorage));
            }
        }
    }

    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    public string FormattedTotalStorage => SizeFormatter.Format(TotalDeveloperStorageBytes);

    public ICommand ScanIndexedCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand BrowseWorkspaceCommand { get; }
    public ICommand ScanCustomWorkspaceCommand { get; }
    public ICommand OpenLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand MoveToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }
    public ICommand CleanEcosystemSafeCommand { get; }

    public async Task ScanIndexedStorageAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        StatusText = "Scanning indexed developer storage (node_modules, bin, obj, target, caches)...";

        try
        {
            var summaries = await _devService.ScanDeveloperStorageAsync();
            Ecosystems.Clear();
            long total = 0;
            int count = 0;

            foreach (var s in summaries)
            {
                Ecosystems.Add(s);
                total += s.TotalBytes;
                count += s.ItemCount;
            }

            TotalDeveloperStorageBytes = total;
            TotalItemsCount = count;

            SelectedEcosystem = Ecosystems.FirstOrDefault(e => e.TotalBytes > 0) ?? Ecosystems.FirstOrDefault();
            StatusText = $"Developer scan complete: {FormattedTotalStorage} detected across {TotalItemsCount:N0} folders/caches.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void BrowseWorkspace()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Developer Workspace or Project Folder"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            CustomWorkspacePath = dialog.FolderName;
            _ = ScanCustomWorkspaceAsync();
        }
    }

    public async Task ScanCustomWorkspaceAsync()
    {
        if (IsScanning || string.IsNullOrWhiteSpace(CustomWorkspacePath) || !Directory.Exists(CustomWorkspacePath)) return;

        IsScanning = true;
        StatusText = $"Scanning workspace: {CustomWorkspacePath}...";

        try
        {
            var progress = new Progress<string>(s => StatusText = s);
            var summaries = await _devService.ScanWorkspaceAsync(CustomWorkspacePath, progress);

            Ecosystems.Clear();
            long total = 0;
            int count = 0;

            foreach (var s in summaries)
            {
                Ecosystems.Add(s);
                total += s.TotalBytes;
                count += s.ItemCount;
            }

            TotalDeveloperStorageBytes = total;
            TotalItemsCount = count;

            SelectedEcosystem = Ecosystems.FirstOrDefault(e => e.TotalBytes > 0) ?? Ecosystems.FirstOrDefault();
            StatusText = $"Workspace scan complete: {FormattedTotalStorage} found in {Path.GetFileName(CustomWorkspacePath)}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Workspace scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void UpdateDetailedItems()
    {
        DetailedItems.Clear();
        if (SelectedEcosystem != null)
        {
            foreach (var item in SelectedEcosystem.Items)
            {
                DetailedItems.Add(item);
            }
        }
        SelectedItem = DetailedItems.FirstOrDefault();
    }

    private async Task MoveSelectedToRecycleBinAsync()
    {
        if (SelectedItem == null) return;
        var item = SelectedItem;

        var confirm = MessageBox.Show(
            $"Move \"{item.Name}\" ({item.FormattedSize}) to the Windows Recycle Bin?\n\nPath: {item.Path}\nReason: {item.ContextReason}",
            "Move to Recycle Bin",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        string? error = null;
        bool success = await Task.Run(() => _fileActionService.MoveToRecycleBin(item.Path, out error));
        if (success)
        {
            RemoveItemFromUI(item);
            MessageBox.Show($"Successfully moved to Recycle Bin:\n{item.Path}\n\nSpace recovered: {item.FormattedSize}", "Cleanup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Failed to move to Recycle Bin:\n{error}", "Operation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteSelectedPermanentlyAsync()
    {
        if (SelectedItem == null) return;
        var item = SelectedItem;

        var confirm = MessageBox.Show(
            $"PERMANENTLY DELETE \"{item.Name}\" ({item.FormattedSize})?\n\nPath: {item.Path}\n\nWARNING: This cannot be undone. Files will NOT be in the Recycle Bin.",
            "Confirm Permanent Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        string? error = null;
        bool success = await Task.Run(() => _fileActionService.DeletePermanently(item.Path, out error));
        if (success)
        {
            RemoveItemFromUI(item);
            MessageBox.Show($"Permanently deleted:\n{item.Path}\n\nSpace recovered: {item.FormattedSize}", "Permanent Deletion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Failed to delete:\n{error}", "Operation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task CleanSelectedEcosystemSafeAsync()
    {
        if (SelectedEcosystem == null || SelectedEcosystem.Items.Count == 0) return;

        var safeItems = SelectedEcosystem.Items.Where(i => i.IsSafeToClean).ToList();
        if (safeItems.Count == 0)
        {
            MessageBox.Show("No safe-to-clean build artifacts found in this ecosystem.", "Nothing to Clean", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long totalBytes = safeItems.Sum(i => i.SizeBytes);
        var confirm = MessageBox.Show(
            $"Move {safeItems.Count} build artifacts / caches in {SelectedEcosystem.Name} to the Recycle Bin?\n\nTotal reclaimable: {SizeFormatter.Format(totalBytes)}\n\nMethod: Windows Recycle Bin (Safe Deletion)",
            "Clean Ecosystem Artifacts",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        int successCount = 0;
        long recoveredBytes = 0;

        await Task.Run(() =>
        {
            foreach (var item in safeItems)
            {
                if (_fileActionService.MoveToRecycleBin(item.Path, out _))
                {
                    successCount++;
                    recoveredBytes += item.SizeBytes;
                    Application.Current.Dispatcher.Invoke(() => RemoveItemFromUI(item));
                }
            }
        });

        MessageBox.Show(
            $"Cleanup Complete!\n\nSuccessfully removed: {successCount} of {safeItems.Count} items\nSpace recovered: {SizeFormatter.Format(recoveredBytes)}",
            "Ecosystem Cleanup Finished",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RemoveItemFromUI(DeveloperJunkItem item)
    {
        DetailedItems.Remove(item);
        if (SelectedEcosystem != null)
        {
            SelectedEcosystem.Items.Remove(item);
            SelectedEcosystem.RefreshTotals();
        }
        TotalDeveloperStorageBytes = Math.Max(0, TotalDeveloperStorageBytes - item.SizeBytes);
        TotalItemsCount = Math.Max(0, TotalItemsCount - 1);
        SelectedItem = DetailedItems.FirstOrDefault();
    }
}
