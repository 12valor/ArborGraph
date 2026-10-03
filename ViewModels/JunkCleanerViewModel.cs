using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class JunkCategoryGroup : ObservableObject
{
    private bool? _isSelected = true;

    public JunkCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public ObservableCollection<JunkTarget> Targets { get; set; } = new();

    public long TotalBytes => Targets.Sum(t => t.SizeInBytes);
    public int TotalFiles => Targets.Sum(t => t.FileCount);
    public string FormattedTotalBytes => SizeFormatter.Format(TotalBytes);

    public bool? IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                if (value.HasValue)
                {
                    foreach (var target in Targets)
                    {
                        target.IsSelected = value.Value;
                    }
                }
            }
        }
    }

    public void UpdateCategoryState()
    {
        OnPropertyChanged(nameof(TotalBytes));
        OnPropertyChanged(nameof(TotalFiles));
        OnPropertyChanged(nameof(FormattedTotalBytes));

        bool allSelected = Targets.All(t => t.IsSelected);
        bool noneSelected = Targets.All(t => !t.IsSelected);

        if (allSelected) _isSelected = true;
        else if (noneSelected) _isSelected = false;
        else _isSelected = null;

        OnPropertyChanged(nameof(IsSelected));
    }
}

public class JunkCleanerViewModel : ObservableObject
{
    private readonly JunkCleanerService _junkService;
    private readonly FileActionService _fileActionService;

    private bool _isScanning;
    private bool _isCleaning;
    private bool _hasScanned;
    private string _statusMessage = "Ready to scan for unnecessary caches and temporary files.";
    private double _progressPercentage;
    private JunkTarget? _selectedTarget;
    private string _lastResultSummary = string.Empty;
    private CancellationTokenSource? _cts;

    public JunkCleanerViewModel(JunkCleanerService junkService, FileActionService fileActionService)
    {
        _junkService = junkService;
        _fileActionService = fileActionService;

        Targets = new ObservableCollection<JunkTarget>(_junkService.GetDefaultTargets());
        foreach (var target in Targets)
        {
            target.PropertyChanged += Target_PropertyChanged;
        }

        CategoryGroups = new ObservableCollection<JunkCategoryGroup>
        {
            new()
            {
                Category = JunkCategory.System,
                Title = "System & Windows Junk",
                Targets = new ObservableCollection<JunkTarget>(Targets.Where(t => t.Category == JunkCategory.System))
            },
            new()
            {
                Category = JunkCategory.Browser,
                Title = "Web Browsers & Electron",
                Targets = new ObservableCollection<JunkTarget>(Targets.Where(t => t.Category == JunkCategory.Browser))
            },
            new()
            {
                Category = JunkCategory.Developer,
                Title = "Developer & Package Caches",
                Targets = new ObservableCollection<JunkTarget>(Targets.Where(t => t.Category == JunkCategory.Developer))
            }
        };

        ScanCommand = new RelayCommand(async _ => await ScanAllAsync(), _ => !IsBusy);
        CleanCommand = new RelayCommand(async _ => await CleanSelectedAsync(), _ => !IsBusy && TotalPotentialBytes > 0);
        CancelCommand = new RelayCommand(_ => CancelOperation(), _ => IsBusy);
        SelectAllCommand = new RelayCommand(_ => SetAllSelection(true));
        DeselectAllCommand = new RelayCommand(_ => SetAllSelection(false));
        InspectTargetCommand = new RelayCommand(param => InspectTarget(param as JunkTarget));
        OpenTargetFolderCommand = new RelayCommand(param => OpenTargetFolder(param as JunkTarget));
        ClosePreviewCommand = new RelayCommand(_ => SelectedTarget = null);
        ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync(), _ => Targets.Count > 0);
    }

    public ObservableCollection<JunkTarget> Targets { get; }
    public ObservableCollection<JunkCategoryGroup> CategoryGroups { get; }

    public ICommand ScanCommand { get; }
    public ICommand CleanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand InspectTargetCommand { get; }
    public ICommand OpenTargetFolderCommand { get; }
    public ICommand ClosePreviewCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommandStates();
            }
        }
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        set
        {
            if (SetProperty(ref _isCleaning, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy => _isScanning || _isCleaning;

    public bool HasScanned
    {
        get => _hasScanned;
        set => SetProperty(ref _hasScanned, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public JunkTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                OnPropertyChanged(nameof(HasSelectedTarget));
            }
        }
    }

    public bool HasSelectedTarget => _selectedTarget != null;

    public string LastResultSummary
    {
        get => _lastResultSummary;
        set => SetProperty(ref _lastResultSummary, value);
    }

    public long TotalPotentialBytes => Targets.Where(t => t.IsSelected).Sum(t => t.SizeInBytes);
    public int TotalPotentialFiles => Targets.Where(t => t.IsSelected).Sum(t => t.FileCount);
    public string FormattedPotentialBytes => SizeFormatter.Format(TotalPotentialBytes);

    public long TotalFoundBytes => Targets.Sum(t => t.SizeInBytes);
    public int TotalFoundFiles => Targets.Sum(t => t.FileCount);
    public string FormattedFoundBytes => SizeFormatter.Format(TotalFoundBytes);

    public void RefreshData()
    {
        if (!HasScanned && !IsBusy)
        {
            _ = ScanAllAsync();
        }
    }

    public async Task ScanAllAsync()
    {
        if (IsBusy) return;

        IsScanning = true;
        StatusMessage = "Starting junk scan across system, browser, and developer caches...";
        ProgressPercentage = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<JunkScanProgress>(p =>
            {
                StatusMessage = $"Scanning {p.CurrentTargetName}... ({p.CompletedTargets}/{p.TotalTargets})";
                ProgressPercentage = p.Percentage;
            });

            await _junkService.ScanAllAsync(Targets, progress, _cts.Token);
            HasScanned = true;
            UpdateTotals();
            StatusMessage = $"Scan completed. Found {FormattedFoundBytes} across {TotalFoundFiles:N0} cleanable files.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Junk scan was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan encountered an error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            ProgressPercentage = 100;
        }
    }

    public async Task CleanSelectedAsync()
    {
        if (IsBusy) return;

        var selectedTargets = Targets.Where(t => t.IsSelected && t.SizeInBytes > 0).ToList();
        if (selectedTargets.Count == 0)
        {
            MessageBox.Show("No junk items are currently selected or have files to clean.", "Junk Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string confirmMsg = $"Are you sure you want to clean {selectedTargets.Count} selected junk targets?\n\n" +
                            $"Estimated space to free: {FormattedPotentialBytes} ({TotalPotentialFiles:N0} files)\n\n" +
                            "Locked or in-use files will be safely skipped automatically.";

        var confirm = MessageBox.Show(confirmMsg, "Confirm Clean", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsCleaning = true;
        StatusMessage = "Cleaning selected caches and temporary files...";
        ProgressPercentage = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<JunkCleanProgress>(p =>
            {
                if (p.TotalFiles > 0)
                {
                    ProgressPercentage = (double)p.CompletedFiles / p.TotalFiles * 100;
                }
                StatusMessage = $"Cleaning: {p.CurrentFileName} ({p.CompletedFiles}/{p.TotalFiles})";
            });

            var result = await _junkService.CleanTargetsAsync(selectedTargets, progress, _cts.Token);

            UpdateTotals();
            LastResultSummary = $"Successfully cleaned {result.FormattedBytesFreed} ({result.FilesDeleted:N0} files removed in {result.Elapsed.TotalSeconds:F1}s).";
            if (result.FilesSkipped > 0)
            {
                LastResultSummary += $" Note: {result.FilesSkipped:N0} files were skipped because they are currently locked by running applications.";
            }

            StatusMessage = LastResultSummary;
            MessageBox.Show(LastResultSummary, "Junk Cleanup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cleanup was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during cleanup: {ex.Message}";
            MessageBox.Show($"Error during cleanup: {ex.Message}", "Cleanup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsCleaning = false;
            ProgressPercentage = 100;
        }
    }

    public void CancelOperation()
    {
        _cts?.Cancel();
    }

    public void SetAllSelection(bool isSelected)
    {
        foreach (var target in Targets)
        {
            target.IsSelected = isSelected;
        }
        UpdateTotals();
    }

    private void InspectTarget(JunkTarget? target)
    {
        SelectedTarget = target;
    }

    private void OpenTargetFolder(JunkTarget? target)
    {
        if (target == null) return;
        var existingDir = target.TargetDirectories.FirstOrDefault(Directory.Exists);
        if (!string.IsNullOrEmpty(existingDir))
        {
            _fileActionService.OpenFileLocation(existingDir);
        }
        else
        {
            MessageBox.Show("Target directory does not exist or has not been created yet.", "Junk Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Target_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JunkTarget.IsSelected) ||
            e.PropertyName == nameof(JunkTarget.SizeInBytes) ||
            e.PropertyName == nameof(JunkTarget.FileCount))
        {
            UpdateTotals();
        }
    }

    private void UpdateTotals()
    {
        foreach (var group in CategoryGroups)
        {
            group.UpdateCategoryState();
        }

        OnPropertyChanged(nameof(TotalPotentialBytes));
        OnPropertyChanged(nameof(TotalPotentialFiles));
        OnPropertyChanged(nameof(FormattedPotentialBytes));
        OnPropertyChanged(nameof(TotalFoundBytes));
        OnPropertyChanged(nameof(TotalFoundFiles));
        OnPropertyChanged(nameof(FormattedFoundBytes));

        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CleanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public async Task ExportCsvAsync()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"arborgraph_junk_targets_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = "Export Junk Targets to CSV"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var exporter = new ExportService();
                await exporter.ExportJunkToCsvAsync(Targets, sfd.FileName);
                var res = MessageBox.Show(
                    $"Exported {Targets.Count:N0} junk targets to:\n{sfd.FileName}\n\nWould you like to open it now?",
                    "Export Successful",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
