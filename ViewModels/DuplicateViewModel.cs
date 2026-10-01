using System.Collections.ObjectModel;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class DuplicateViewModel : ObservableObject
{
    private readonly DuplicateAnalyzer _analyzer;
    private readonly FileActionService _fileActionService;

    private bool _isAnalyzing;
    private string _statusMessage = "Ready to analyze duplicates across indexed files.";
    private DuplicateGroup? _selectedGroup;
    private FileRecord? _selectedFile;
    private long _totalWastedBytes;
    private int _totalDuplicateFiles;
    private bool _applyToAllGroups = false;
    private CancellationTokenSource? _cts;

    public DuplicateViewModel(DuplicateAnalyzer analyzer, FileActionService fileActionService)
    {
        _analyzer = analyzer;
        _fileActionService = fileActionService;
        DuplicateGroups = [];

        StartAnalysisCommand = new RelayCommand(async _ => await RunAnalysisAsync(), _ => !IsAnalyzing);
        CancelAnalysisCommand = new RelayCommand(_ => CancelAnalysis(), _ => IsAnalyzing);

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); }, _ => SelectedFile != null);
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); }, _ => SelectedFile != null);
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); }, _ => SelectedFile != null);
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); }, _ => SelectedFile != null);

        // Selection strategies
        KeepNewestCommand = new RelayCommand(_ => SelectDuplicates(SelectionStrategy.KeepNewest), _ => DuplicateGroups.Count > 0);
        KeepOldestCommand = new RelayCommand(_ => SelectDuplicates(SelectionStrategy.KeepOldest), _ => DuplicateGroups.Count > 0);
        KeepShortestPathCommand = new RelayCommand(_ => SelectDuplicates(SelectionStrategy.KeepShortestPath), _ => DuplicateGroups.Count > 0);
        DeselectAllCommand = new RelayCommand(_ => DeselectAll(), _ => DuplicateGroups.Count > 0);

        // Safe cleanup commands
        MoveSelectedToRecycleBinCommand = new RelayCommand(_ => MoveSelectedToRecycleBin(), _ => DuplicateGroups.Count > 0);
        DeleteSelectedPermanentlyCommand = new RelayCommand(_ => DeleteSelectedPermanently(), _ => DuplicateGroups.Count > 0);

        MoveFileToRecycleBinCommand = new RelayCommand(_ => MoveFileToRecycleBin(SelectedFile), _ => SelectedFile != null);
        DeleteFilePermanentlyCommand = new RelayCommand(_ => DeleteFilePermanently(SelectedFile), _ => SelectedFile != null);

        ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync(), _ => DuplicateGroups.Count > 0);
    }

    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; }
    public ICommand ExportCsvCommand { get; }

    public DuplicateGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                SelectedFile = _selectedGroup?.Files.FirstOrDefault();
            }
        }
    }

    public FileRecord? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                (StartAnalysisCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelAnalysisCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public long TotalWastedBytes
    {
        get => _totalWastedBytes;
        set
        {
            if (SetProperty(ref _totalWastedBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalWasted));
            }
        }
    }

    public int TotalDuplicateFiles
    {
        get => _totalDuplicateFiles;
        set => SetProperty(ref _totalDuplicateFiles, value);
    }

    public string FormattedTotalWasted => SizeFormatter.Format(_totalWastedBytes);

    public bool ApplyToAllGroups
    {
        get => _applyToAllGroups;
        set => SetProperty(ref _applyToAllGroups, value);
    }

    public ICommand StartAnalysisCommand { get; }
    public ICommand CancelAnalysisCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }

    public ICommand KeepNewestCommand { get; }
    public ICommand KeepOldestCommand { get; }
    public ICommand KeepShortestPathCommand { get; }
    public ICommand DeselectAllCommand { get; }

    public ICommand MoveSelectedToRecycleBinCommand { get; }
    public ICommand DeleteSelectedPermanentlyCommand { get; }

    public ICommand MoveFileToRecycleBinCommand { get; }
    public ICommand DeleteFilePermanentlyCommand { get; }

    private enum SelectionStrategy
    {
        KeepNewest,
        KeepOldest,
        KeepShortestPath
    }

    private void SelectDuplicates(SelectionStrategy strategy)
    {
        var targetGroups = ApplyToAllGroups 
            ? DuplicateGroups.ToList() 
            : (SelectedGroup != null ? [SelectedGroup] : Enumerable.Empty<DuplicateGroup>());

        foreach (var group in targetGroups)
        {
            if (group.Files.Count <= 1) continue;

            FileRecord keeper = strategy switch
            {
                SelectionStrategy.KeepNewest => group.Files.OrderByDescending(f => f.ModifiedDate).First(),
                SelectionStrategy.KeepOldest => group.Files.OrderBy(f => f.ModifiedDate).First(),
                SelectionStrategy.KeepShortestPath => group.Files.OrderBy(f => f.Path.Length).ThenBy(f => f.Path).First(),
                _ => group.Files.First()
            };

            foreach (var file in group.Files)
            {
                file.IsSelected = (file != keeper);
            }
        }
    }

    private void DeselectAll()
    {
        var targetGroups = ApplyToAllGroups 
            ? DuplicateGroups.ToList() 
            : (SelectedGroup != null ? [SelectedGroup] : Enumerable.Empty<DuplicateGroup>());

        foreach (var group in targetGroups)
        {
            foreach (var file in group.Files)
            {
                file.IsSelected = false;
            }
        }
    }

    private void MoveSelectedToRecycleBin()
    {
        var selectedFiles = DuplicateGroups
            .SelectMany(g => g.Files.Where(f => f.IsSelected))
            .ToList();

        if (selectedFiles.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "No duplicate files are currently selected for removal.\n\nUse the selection buttons (e.g. 'Keep Newest') or check individual files first.",
                "No Files Selected",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        long totalBytes = selectedFiles.Sum(f => f.Size);
        var confirm = System.Windows.MessageBox.Show(
            $"Move {selectedFiles.Count:N0} duplicate file(s) ({SizeFormatter.Format(totalBytes)}) to the Windows Recycle Bin?\n\nOne copy of each duplicate group will remain intact.",
            "Confirm Recycle Bin Cleanup",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        int successCount = 0;
        int failCount = 0;
        long recoveredBytes = 0;

        foreach (var file in selectedFiles)
        {
            if (_fileActionService.MoveToRecycleBin(file.Path, out _))
            {
                successCount++;
                recoveredBytes += file.Size;
                RemoveFileFromGroups(file);
            }
            else
            {
                failCount++;
            }
        }

        RefreshTotals();

        string msg = $"Cleaned {successCount:N0} duplicate file(s), recovering {SizeFormatter.Format(recoveredBytes)} to the Recycle Bin.";
        if (failCount > 0)
        {
            msg += $"\n{failCount} file(s) could not be removed (in use or access denied).";
        }

        StatusMessage = msg;
        System.Windows.MessageBox.Show(msg, "Duplicate Cleanup Complete", System.Windows.MessageBoxButton.OK,
            failCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
    }

    private void DeleteSelectedPermanently()
    {
        var selectedFiles = DuplicateGroups
            .SelectMany(g => g.Files.Where(f => f.IsSelected))
            .ToList();

        if (selectedFiles.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "No duplicate files are currently selected for permanent removal.",
                "No Files Selected",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        long totalBytes = selectedFiles.Sum(f => f.Size);
        var confirm = System.Windows.MessageBox.Show(
            $"WARNING: Are you sure you want to PERMANENTLY delete {selectedFiles.Count:N0} duplicate file(s) ({SizeFormatter.Format(totalBytes)})?\n\nThis CANNOT be undone and will bypass the Recycle Bin.",
            "Confirm Permanent Deletion",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        int successCount = 0;
        int failCount = 0;
        long recoveredBytes = 0;

        foreach (var file in selectedFiles)
        {
            if (_fileActionService.DeletePermanently(file.Path, out _))
            {
                successCount++;
                recoveredBytes += file.Size;
                RemoveFileFromGroups(file);
            }
            else
            {
                failCount++;
            }
        }

        RefreshTotals();

        string msg = $"Permanently removed {successCount:N0} duplicate file(s), recovering {SizeFormatter.Format(recoveredBytes)}.";
        if (failCount > 0)
        {
            msg += $"\n{failCount} file(s) could not be deleted (in use or access denied).";
        }

        StatusMessage = msg;
        System.Windows.MessageBox.Show(msg, "Permanent Deletion Complete", System.Windows.MessageBoxButton.OK,
            failCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
    }

    private void MoveFileToRecycleBin(FileRecord? file)
    {
        if (file == null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Move duplicate file to Recycle Bin?\n\n\"{file.Path}\" ({file.FormattedSize})",
            "Move to Recycle Bin",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        if (_fileActionService.MoveToRecycleBin(file.Path, out string? error))
        {
            RemoveFileFromGroups(file);
            RefreshTotals();
            StatusMessage = $"Moved {file.Name} to Recycle Bin.";
        }
        else
        {
            System.Windows.MessageBox.Show($"Failed to move file to Recycle Bin: {error}", "Recycle Bin Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void DeleteFilePermanently(FileRecord? file)
    {
        if (file == null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"WARNING: Are you sure you want to PERMANENTLY delete:\n\n\"{file.Path}\" ({file.FormattedSize})?\n\nThis CANNOT be undone.",
            "Confirm Permanent Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        if (_fileActionService.DeletePermanently(file.Path, out string? error))
        {
            RemoveFileFromGroups(file);
            RefreshTotals();
            StatusMessage = $"Permanently deleted {file.Name}.";
        }
        else
        {
            System.Windows.MessageBox.Show($"Failed to permanently delete file: {error}", "Delete Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RemoveFileFromGroups(FileRecord file)
    {
        for (int i = DuplicateGroups.Count - 1; i >= 0; i--)
        {
            var group = DuplicateGroups[i];
            if (group.Files.Remove(file))
            {
                group.NotifyCollectionChanged();
                if (group.Files.Count <= 1)
                {
                    DuplicateGroups.RemoveAt(i);
                    if (SelectedGroup == group)
                    {
                        SelectedGroup = DuplicateGroups.FirstOrDefault();
                    }
                }
            }
        }
    }

    private void RefreshTotals()
    {
        TotalWastedBytes = DuplicateGroups.Sum(g => g.WastedBytes);
        TotalDuplicateFiles = DuplicateGroups.Sum(g => g.FileCount);
        if (SelectedFile != null && SelectedGroup?.Files.Contains(SelectedFile) != true)
        {
            SelectedFile = SelectedGroup?.Files.FirstOrDefault();
        }
    }

    public async Task RunAnalysisAsync()
    {
        if (IsAnalyzing) return;

        IsAnalyzing = true;
        _cts = new CancellationTokenSource();
        DuplicateGroups.Clear();
        TotalWastedBytes = 0;
        TotalDuplicateFiles = 0;

        var progress = new Progress<string>(msg => StatusMessage = msg);

        try
        {
            var results = await _analyzer.FindDuplicatesAsync(
                minSize: 1024,
                maxCandidates: 1000,
                statusProgress: progress,
                cancellationToken: _cts.Token);

            foreach (var g in results)
            {
                DuplicateGroups.Add(g);
            }

            TotalWastedBytes = results.Sum(g => g.WastedBytes);
            TotalDuplicateFiles = results.Sum(g => g.FileCount);
            SelectedGroup = DuplicateGroups.FirstOrDefault();
            (ExportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Duplicate analysis was stopped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
        }
    }

    public void CancelAnalysis()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    public async Task ExportCsvAsync()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"diskscope_duplicates_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = "Export Duplicates to CSV"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var exporter = new ExportService();
                await exporter.ExportDuplicatesToCsvAsync(DuplicateGroups, sfd.FileName);
                var res = System.Windows.MessageBox.Show(
                    $"Exported {DuplicateGroups.Count:N0} duplicate groups to:\n{sfd.FileName}\n\nWould you like to open it now?",
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
}

