using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class CleanupCenterViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly DeveloperStorageService _devService;
    private readonly FileActionService _fileActionService;
    private readonly Action? _requestRescanAction;

    private bool _isLoading;
    private string _statusMessage = "Ready to analyze reclaimable space.";
    private long _potentiallyReclaimableBytes;
    private CleanupCategory? _selectedCategory;
    private CleanupCandidateItem? _selectedItem;
    private CleanupResultSummary? _lastResult;
    private bool _showCompletionSummary;

    public CleanupCenterViewModel(
        DatabaseService dbService,
        DeveloperStorageService devService,
        FileActionService fileActionService,
        Action? requestRescanAction = null)
    {
        _dbService = dbService;
        _devService = devService;
        _fileActionService = fileActionService;
        _requestRescanAction = requestRescanAction;

        Categories = [];

        RefreshCommand = new RelayCommand(async _ => await LoadCleanupCategoriesAsync(), _ => !IsLoading);
        SelectAllCommand = new RelayCommand(_ => SetSelectedOnItems(true), _ => SelectedCategory?.Items.Count > 0);
        DeselectAllCommand = new RelayCommand(_ => SetSelectedOnItems(false), _ => SelectedCategory?.Items.Count > 0);

        CleanSelectedRecycleBinCommand = new RelayCommand(async _ => await CleanSelectedAsync(useRecycleBin: true), _ => HasSelectedItems());
        CleanSelectedPermanentlyCommand = new RelayCommand(async _ => await CleanSelectedAsync(useRecycleBin: false), _ => HasSelectedItems());

        OpenFileCommand = new RelayCommand(_ => { if (SelectedItem != null) _fileActionService.OpenFile(SelectedItem.Path); }, _ => SelectedItem != null);
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedItem != null) _fileActionService.OpenFileLocation(SelectedItem.Path); }, _ => SelectedItem != null);
        CopyPathCommand = new RelayCommand(_ => { if (SelectedItem != null) _fileActionService.CopyPath(SelectedItem.Path); }, _ => SelectedItem != null);
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedItem != null) _fileActionService.ShowProperties(SelectedItem.Path); }, _ => SelectedItem != null);

        RescanCommand = new RelayCommand(_ => _requestRescanAction?.Invoke());
        DismissSummaryCommand = new RelayCommand(_ => ShowCompletionSummary = false);
    }

    public ObservableCollection<CleanupCategory> Categories { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public long PotentiallyReclaimableBytes
    {
        get => _potentiallyReclaimableBytes;
        set
        {
            if (SetProperty(ref _potentiallyReclaimableBytes, value))
            {
                OnPropertyChanged(nameof(FormattedPotentiallyReclaimable));
            }
        }
    }

    public string FormattedPotentiallyReclaimable => SizeFormatter.Format(_potentiallyReclaimableBytes);

    public CleanupCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                SelectedItem = _selectedCategory?.Items.FirstOrDefault();
                (SelectAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeselectAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CleanSelectedRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CleanSelectedPermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public CleanupCandidateItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public CleanupResultSummary? LastResult
    {
        get => _lastResult;
        set => SetProperty(ref _lastResult, value);
    }

    public bool ShowCompletionSummary
    {
        get => _showCompletionSummary;
        set => SetProperty(ref _showCompletionSummary, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand CleanSelectedRecycleBinCommand { get; }
    public ICommand CleanSelectedPermanentlyCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand DismissSummaryCommand { get; }

    public async Task LoadCleanupCategoriesAsync(CancellationToken ct = default)
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusMessage = "Analyzing scan data for reclaimable storage opportunities...";
        ShowCompletionSummary = false;

        try
        {
            var loadedCategories = await Task.Run(async () =>
            {
                var list = new List<CleanupCategory>();
                var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                bool IsClaimed(string path)
                {
                    if (claimedPaths.Contains(path)) return true;
                    foreach (var p in claimedPaths)
                    {
                        if (path.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase) ||
                            path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                // 1. Developer Caches & Artifacts
                var devSummaries = await _devService.ScanDeveloperStorageAsync(ct);
                var devCat = new CleanupCategory
                {
                    Id = "dev_junk",
                    Name = "Developer Caches & Artifacts",
                    Description = "node_modules, .NET bin/obj, target, Gradle & global package caches",
                    Icon = "⚙",
                    SafetyBadge = "Safe to Clean"
                };
                foreach (var summary in devSummaries)
                {
                    foreach (var item in summary.Items)
                    {
                        devCat.Items.Add(new CleanupCandidateItem
                        {
                            Path = item.Path,
                            Name = item.Name,
                            IsDirectory = true,
                            SizeBytes = item.SizeBytes,
                            FileCount = item.FileCount,
                            LastModified = item.LastModified,
                            Reason = item.ContextReason,
                            IsSelected = true
                        });
                        claimedPaths.Add(item.Path);
                    }
                }
                devCat.RefreshTotals();
                if (devCat.Items.Count > 0) list.Add(devCat);

                // 2. Duplicate Redundancy (Non-overlapping copies)
                var dupCandidates = _dbService.GetDuplicateSizeCandidates(minCount: 2, minSize: 1024, limit: 150);
                var dupCat = new CleanupCategory
                {
                    Id = "duplicates",
                    Name = "Duplicate Files Redundancy",
                    Description = "Identical copies of files wasting disk space",
                    Icon = "📑",
                    SafetyBadge = "Safe to Clean"
                };
                foreach (var (size, _) in dupCandidates)
                {
                    if (ct.IsCancellationRequested) break;
                    var dupFiles = _dbService.GetFilesBySize(size);
                    if (dupFiles.Count >= 2)
                    {
                        // First copy is keeper, subsequent copies are redundant candidates
                        for (int i = 1; i < dupFiles.Count; i++)
                        {
                            var f = dupFiles[i];
                            if (IsClaimed(f.Path)) continue;

                            dupCat.Items.Add(new CleanupCandidateItem
                            {
                                Path = f.Path,
                                Name = f.Name,
                                IsDirectory = false,
                                SizeBytes = f.Size,
                                FileCount = 1,
                                LastModified = f.ModifiedDate,
                                Reason = $"Redundant duplicate copy (Keeper: {dupFiles[0].Name})",
                                IsSelected = true
                            });
                            claimedPaths.Add(f.Path);
                        }
                    }
                }
                dupCat.RefreshTotals();
                if (dupCat.Items.Count > 0) list.Add(dupCat);

                // 3. Temporary & Log Files (Excluding items inside developer or claimed folders)
                var tempCat = new CleanupCategory
                {
                    Id = "temp_logs",
                    Name = "Temporary & Log Files",
                    Description = "Residual .tmp, .log, .bak, and crash dump files",
                    Icon = "🗑",
                    SafetyBadge = "Safe to Clean"
                };
                _dbService.StreamFilteredFiles(
                    onRecord: f =>
                    {
                        if (IsClaimed(f.Path)) return;
                        tempCat.Items.Add(new CleanupCandidateItem
                        {
                            Path = f.Path,
                            Name = f.Name,
                            IsDirectory = false,
                            SizeBytes = f.Size,
                            FileCount = 1,
                            LastModified = f.ModifiedDate,
                            Reason = "Temporary file",
                            IsSelected = true
                        });
                        claimedPaths.Add(f.Path);
                    },
                    minSize: 1024,
                    search: ".tmp",
                    ct: ct);

                _dbService.StreamFilteredFiles(
                    onRecord: f =>
                    {
                        if (IsClaimed(f.Path)) return;
                        tempCat.Items.Add(new CleanupCandidateItem
                        {
                            Path = f.Path,
                            Name = f.Name,
                            IsDirectory = false,
                            SizeBytes = f.Size,
                            FileCount = 1,
                            LastModified = f.ModifiedDate,
                            Reason = "Log / Dump file",
                            IsSelected = true
                        });
                        claimedPaths.Add(f.Path);
                    },
                    minSize: 1024,
                    search: ".log",
                    ct: ct);

                tempCat.RefreshTotals();
                if (tempCat.Items.Count > 0) list.Add(tempCat);

                // 4. Photoshop Cache & Scratch Files (Excluding items already claimed)
                var psFiles = _dbService.GetPhotoshopFiles(limit: 200)
                    .Where(f => f.Path.Contains("Temp", StringComparison.OrdinalIgnoreCase)
                             || f.Path.Contains("AutoRecover", StringComparison.OrdinalIgnoreCase)
                             || f.Path.Contains("Media Cache", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var psCat = new CleanupCategory
                {
                    Id = "photoshop_cache",
                    Name = "Photoshop / Adobe Cache & Scratch",
                    Description = "AutoRecover snapshots and temp scratch caches",
                    Icon = "🎨",
                    SafetyBadge = "Safe to Clean"
                };
                foreach (var f in psFiles)
                {
                    if (IsClaimed(f.Path)) continue;
                    psCat.Items.Add(new CleanupCandidateItem
                    {
                        Path = f.Path,
                        Name = f.Name,
                        IsDirectory = false,
                        SizeBytes = f.Size,
                        FileCount = 1,
                        LastModified = f.ModifiedDate,
                        Reason = "Adobe Photoshop temporary scratch / AutoRecover cache",
                        IsSelected = true
                    });
                    claimedPaths.Add(f.Path);
                }
                psCat.RefreshTotals();
                if (psCat.Items.Count > 0) list.Add(psCat);

                return list;
            }, ct);

            Categories.Clear();
            foreach (var c in loadedCategories)
            {
                Categories.Add(c);
            }

            PotentiallyReclaimableBytes = Categories.Sum(c => c.TotalBytes);
            SelectedCategory = Categories.FirstOrDefault();
            StatusMessage = $"Analysis complete. {SizeFormatter.Format(PotentiallyReclaimableBytes)} potentially reclaimable across {Categories.Count} categories.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cleanup analysis was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error analyzing cleanup: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetSelectedOnItems(bool select)
    {
        if (SelectedCategory == null) return;
        foreach (var item in SelectedCategory.Items)
        {
            item.IsSelected = select;
        }
        RaiseCleanupCanExecute();
    }

    private bool HasSelectedItems()
    {
        return SelectedCategory?.Items.Any(i => i.IsSelected) == true;
    }

    private void RaiseCleanupCanExecute()
    {
        (CleanSelectedRecycleBinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CleanSelectedPermanentlyCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task CleanSelectedAsync(bool useRecycleBin)
    {
        if (SelectedCategory == null) return;

        var itemsToClean = SelectedCategory.Items.Where(i => i.IsSelected).ToList();
        if (itemsToClean.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "No items are currently selected for cleanup in this category.",
                "No Items Selected",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        long totalBytes = itemsToClean.Sum(i => i.SizeBytes);
        string methodDesc = useRecycleBin ? "Move to Windows Recycle Bin (Safe)" : "PERMANENTLY DELETE (Bypasses Recycle Bin)";

        string confirmMsg = $"WHAT WILL BE REMOVED\n\n" +
                            $"Category: {SelectedCategory.Name}\n" +
                            $"Items:    {itemsToClean.Count:N0}\n" +
                            $"Space:    {SizeFormatter.Format(totalBytes)}\n" +
                            $"Method:   {methodDesc}\n\n";

        if (!useRecycleBin)
        {
            confirmMsg += "WARNING: This cannot be undone!\n\nAre you absolutely sure?";
        }
        else
        {
            confirmMsg += "Files will be sent to the Recycle Bin and can be restored if needed.\n\nProceed?";
        }

        var res = System.Windows.MessageBox.Show(
            confirmMsg,
            useRecycleBin ? "Confirm Safe Cleanup" : "CONFIRM PERMANENT DELETION",
            System.Windows.MessageBoxButton.YesNo,
            useRecycleBin ? System.Windows.MessageBoxImage.Question : System.Windows.MessageBoxImage.Warning);

        if (res != System.Windows.MessageBoxResult.Yes) return;

        IsLoading = true;
        StatusMessage = $"Cleaning {itemsToClean.Count:N0} items...";

        var result = new CleanupResultSummary { TotalProcessed = itemsToClean.Count };

        await Task.Run(() =>
        {
            foreach (var item in itemsToClean)
            {
                bool success;
                string? error;

                if (useRecycleBin)
                {
                    success = _fileActionService.MoveToRecycleBin(item.Path, out error);
                }
                else
                {
                    success = _fileActionService.DeletePermanently(item.Path, out error);
                }

                if (success)
                {
                    result.SuccessfullyRemoved++;
                    result.RecoveredBytes += item.SizeBytes;

                    // Remove from database index
                    if (item.IsDirectory)
                    {
                        _dbService.RemoveDirectoryFromIndex(item.Path);
                    }
                    else
                    {
                        _dbService.RemoveFileFromIndex(item.Path);
                    }
                }
                else
                {
                    result.Failed++;
                    result.FailureMessages.Add($"{Path.GetFileName(item.Path)}: {error ?? "In use or permission denied"}");
                }
            }
        });

        // Remove successfully cleaned items from view model collection
        var removedPaths = itemsToClean.Take(result.SuccessfullyRemoved).Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = SelectedCategory.Items.Count - 1; i >= 0; i--)
        {
            if (removedPaths.Contains(SelectedCategory.Items[i].Path))
            {
                SelectedCategory.Items.RemoveAt(i);
            }
        }

        SelectedCategory.RefreshTotals();
        PotentiallyReclaimableBytes = Categories.Sum(c => c.TotalBytes);
        LastResult = result;
        ShowCompletionSummary = true;
        IsLoading = false;

        StatusMessage = $"Cleanup complete: {result.SuccessfullyRemoved:N0} removed, {result.FormattedRecovered} recovered.";
    }
}
