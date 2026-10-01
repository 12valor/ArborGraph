using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class DiskScopeSettings : ObservableObject
{
    // Scanning Settings
    private int _workerCount = 0; // 0 = Automatic
    private bool _followJunctions = false;
    private bool _includeHiddenFiles = true;
    private bool _includeSystemFiles = true;
    private List<string> _excludedPaths = [];

    // Cleanup & Safety Settings
    private bool _defaultToRecycleBin = true;
    private bool _requireConfirmationForRecycleBin = true;
    private bool _requireConfirmationForPermanent = true;

    // History Retention Settings
    private int _retentionDays = 90;
    private int _maxScanSessionsToKeep = 50;

    // Diagnostics Settings
    private string _logLevel = "Information";

    public int WorkerCount
    {
        get => _workerCount;
        set => SetProperty(ref _workerCount, value);
    }

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

    public List<string> ExcludedPaths
    {
        get => _excludedPaths;
        set => SetProperty(ref _excludedPaths, value);
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
}
