using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly DatabaseService _dbService;

    private bool _followJunctions;
    private bool _includeHiddenFiles;
    private bool _includeSystemFiles;
    private bool _defaultToRecycleBin;
    private bool _requireConfirmationForRecycleBin;
    private bool _requireConfirmationForPermanent;
    private int _retentionDays;
    private int _maxScanSessionsToKeep;
    private string _logLevel = "Information";
    private int _workerCount;

    private string? _selectedExclusion;
    private string _newExclusionPath = string.Empty;
    private string _statusMessage = string.Empty;

    private string _selectedSection = "General";

    public SettingsViewModel(SettingsService settingsService, DatabaseService dbService)
    {
        _settingsService = settingsService;
        _dbService = dbService;

        ExcludedPaths = [];
        LogLevelOptions = ["Information", "Debug", "Warning", "Error"];

        LoadFromService();

        SelectSectionCommand = new RelayCommand(p =>
        {
            if (p is string s) SelectedSection = s;
        });
        AddExclusionCommand = new RelayCommand(_ => AddExclusion(), _ => !string.IsNullOrWhiteSpace(NewExclusionPath));
        BrowseExclusionFolderCommand = new RelayCommand(_ => BrowseExclusionFolder());
        RemoveExclusionCommand = new RelayCommand(_ => RemoveExclusion(), _ => !string.IsNullOrWhiteSpace(SelectedExclusion));
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
        ClearAllHistoryCommand = new RelayCommand(_ => ClearAllHistory());
        ViewEulaCommand = new RelayCommand(_ => ViewEula());
    }

    public string SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }

    public ICommand SelectSectionCommand { get; }
    public ScanLogViewModel? ScanLogVM { get; set; }
    public ICommand ViewEulaCommand { get; }

    public ObservableCollection<string> ExcludedPaths { get; }
    public IReadOnlyList<string> LogLevelOptions { get; }

    public bool FollowJunctions
    {
        get => _followJunctions;
        set => SetProperty(ref _followJunctions, value);
    }

    public bool IncludeHiddenFiles
    {
        get => _includeHiddenFiles;
        set => SetProperty(ref _includeHiddenFiles, value);
    }

    public bool IncludeSystemFiles
    {
        get => _includeSystemFiles;
        set => SetProperty(ref _includeSystemFiles, value);
    }

    public bool DefaultToRecycleBin
    {
        get => _defaultToRecycleBin;
        set => SetProperty(ref _defaultToRecycleBin, value);
    }

    public bool RequireConfirmationForRecycleBin
    {
        get => _requireConfirmationForRecycleBin;
        set => SetProperty(ref _requireConfirmationForRecycleBin, value);
    }

    public bool RequireConfirmationForPermanent
    {
        get => _requireConfirmationForPermanent;
        set => SetProperty(ref _requireConfirmationForPermanent, value);
    }

    public int RetentionDays
    {
        get => _retentionDays;
        set => SetProperty(ref _retentionDays, value);
    }

    public int MaxScanSessionsToKeep
    {
        get => _maxScanSessionsToKeep;
        set => SetProperty(ref _maxScanSessionsToKeep, value);
    }

    public string LogLevel
    {
        get => _logLevel;
        set => SetProperty(ref _logLevel, value);
    }

    public int WorkerCount
    {
        get => _workerCount;
        set => SetProperty(ref _workerCount, value);
    }

    public string? SelectedExclusion
    {
        get => _selectedExclusion;
        set
        {
            if (SetProperty(ref _selectedExclusion, value))
            {
                (RemoveExclusionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewExclusionPath
    {
        get => _newExclusionPath;
        set
        {
            if (SetProperty(ref _newExclusionPath, value))
            {
                (AddExclusionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AddExclusionCommand { get; }
    public ICommand BrowseExclusionFolderCommand { get; }
    public ICommand RemoveExclusionCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand ClearAllHistoryCommand { get; }

    public void LoadFromService()
    {
        var s = _settingsService.CurrentSettings;
        _followJunctions = s.FollowJunctions;
        _includeHiddenFiles = s.IncludeHiddenFiles;
        _includeSystemFiles = s.IncludeSystemFiles;
        _defaultToRecycleBin = s.DefaultToRecycleBin;
        _requireConfirmationForRecycleBin = s.RequireConfirmationForRecycleBin;
        _requireConfirmationForPermanent = s.RequireConfirmationForPermanent;
        _retentionDays = s.RetentionDays;
        _maxScanSessionsToKeep = s.MaxScanSessionsToKeep;
        _logLevel = s.LogLevel;
        _workerCount = s.WorkerCount;

        ExcludedPaths.Clear();
        foreach (var p in s.ExcludedPaths)
        {
            ExcludedPaths.Add(p);
        }

        OnPropertyChanged(string.Empty);
    }

    public void SaveSettings()
    {
        var s = new DiskScopeSettings
        {
            FollowJunctions = FollowJunctions,
            IncludeHiddenFiles = IncludeHiddenFiles,
            IncludeSystemFiles = IncludeSystemFiles,
            DefaultToRecycleBin = DefaultToRecycleBin,
            RequireConfirmationForRecycleBin = RequireConfirmationForRecycleBin,
            RequireConfirmationForPermanent = RequireConfirmationForPermanent,
            RetentionDays = RetentionDays,
            MaxScanSessionsToKeep = MaxScanSessionsToKeep,
            LogLevel = LogLevel,
            WorkerCount = WorkerCount,
            ExcludedPaths = ExcludedPaths.ToList()
        };

        _settingsService.SaveSettings(s);
        StatusMessage = "Settings saved successfully.";
    }

    public void ResetDefaults()
    {
        var res = MessageBox.Show(
            "Reset all settings and exclusions to default values?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            _settingsService.ResetToDefaults();
            LoadFromService();
            StatusMessage = "Settings restored to defaults.";
        }
    }

    public void AddExclusion()
    {
        if (string.IsNullOrWhiteSpace(NewExclusionPath)) return;
        string path = NewExclusionPath.Trim();

        try
        {
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!ExcludedPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                ExcludedPaths.Add(normalized);
                _settingsService.AddExclusion(normalized);
                NewExclusionPath = string.Empty;
                StatusMessage = $"Added exclusion: {normalized}";
            }
            else
            {
                StatusMessage = "Path is already in the exclusions list.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Invalid directory path: {ex.Message}", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void BrowseExclusionFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Directory to Exclude from Scans and Cleanup"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            NewExclusionPath = dialog.FolderName;
            AddExclusion();
        }
    }

    public void RemoveExclusion()
    {
        if (string.IsNullOrWhiteSpace(SelectedExclusion)) return;
        string path = SelectedExclusion;

        ExcludedPaths.Remove(path);
        _settingsService.RemoveExclusion(path);
        StatusMessage = $"Removed exclusion: {path}";
        SelectedExclusion = null;
    }

    public void ClearAllHistory()
    {
        var res = MessageBox.Show(
            "Clear all persistent scan history sessions and category growth baselines?\n\nThis cannot be undone.",
            "Confirm Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (res == MessageBoxResult.Yes)
        {
            _dbService.DeleteScanHistory();
            StatusMessage = "All scan history cleared.";
            MessageBox.Show("All scan history sessions were successfully removed.", "History Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void ViewEula()
    {
        var dlg = new Views.EulaDialog(isReviewMode: true)
        {
            Owner = Application.Current?.MainWindow
        };
        dlg.ShowDialog();
    }
}
