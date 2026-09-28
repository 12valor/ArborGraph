using DiskScope.Infrastructure;

namespace DiskScope.Models;

public enum ScanState
{
    Ready,
    Scanning,
    Stopping,
    Completed,
    Cancelled,
    Failed
}

public class ScanStats : ObservableObject
{
    private long _directoriesVisited;
    private long _directoriesProcessed;
    private long _directoriesSkipped;

    private long _filesDiscovered;
    private long _filesIndexed;
    private long _filesSkipped;

    private long _logicalBytesIndexed;
    private TimeSpan _elapsed;
    private double _filesPerSecond;
    private double _bytesPerSecond;
    private string _currentDirectory = string.Empty;
    private ScanState _state = ScanState.Ready;

    // Directories
    public long DirectoriesVisited
    {
        get => _directoriesVisited;
        set => SetProperty(ref _directoriesVisited, value);
    }

    public long DirectoriesProcessed
    {
        get => _directoriesProcessed;
        set => SetProperty(ref _directoriesProcessed, value);
    }

    public long DirectoriesSkipped
    {
        get => _directoriesSkipped;
        set => SetProperty(ref _directoriesSkipped, value);
    }

    // Files
    public long FilesDiscovered
    {
        get => _filesDiscovered;
        set => SetProperty(ref _filesDiscovered, value);
    }

    public long FilesIndexed
    {
        get => _filesIndexed;
        set => SetProperty(ref _filesIndexed, value);
    }

    public long FilesSkipped
    {
        get => _filesSkipped;
        set => SetProperty(ref _filesSkipped, value);
    }

    // Storage
    public long LogicalBytesIndexed
    {
        get => _logicalBytesIndexed;
        set => SetProperty(ref _logicalBytesIndexed, value);
    }

    // Timing and Speed
    public TimeSpan Elapsed
    {
        get => _elapsed;
        set => SetProperty(ref _elapsed, value);
    }

    public double FilesPerSecond
    {
        get => _filesPerSecond;
        set => SetProperty(ref _filesPerSecond, value);
    }

    public double BytesPerSecond
    {
        get => _bytesPerSecond;
        set => SetProperty(ref _bytesPerSecond, value);
    }

    public string CurrentDirectory
    {
        get => _currentDirectory;
        set => SetProperty(ref _currentDirectory, value);
    }

    public ScanState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    // Formatted strings
    public string FormattedLogicalBytes => SizeFormatter.Format(_logicalBytesIndexed);
    public string FormattedFilesPerSecond => SizeFormatter.FormatSpeed(_filesPerSecond);
    public string FormattedBytesPerSecond => SizeFormatter.FormatStorageSpeed(_bytesPerSecond);
    public string FormattedElapsed => SizeFormatter.FormatTime(_elapsed);

    public void Reset()
    {
        DirectoriesVisited = 0;
        DirectoriesProcessed = 0;
        DirectoriesSkipped = 0;
        FilesDiscovered = 0;
        FilesIndexed = 0;
        FilesSkipped = 0;
        LogicalBytesIndexed = 0;
        Elapsed = TimeSpan.Zero;
        FilesPerSecond = 0;
        BytesPerSecond = 0;
        CurrentDirectory = string.Empty;
        State = ScanState.Ready;
    }
}
