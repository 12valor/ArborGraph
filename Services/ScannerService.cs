using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using DiskScope.Models;

namespace DiskScope.Services;

public class ScanProgressReport
{
    public long DirectoriesVisited { get; set; }
    public long DirectoriesProcessed { get; set; }
    public long DirectoriesSkipped { get; set; }

    public long FilesDiscovered { get; set; }
    public long FilesIndexed { get; set; }
    public long FilesSkipped { get; set; }

    public long LogicalBytesIndexed { get; set; }
    public TimeSpan Elapsed { get; set; }
    public double FilesPerSecond { get; set; }
    public double BytesPerSecond { get; set; }
    public string CurrentDirectory { get; set; } = string.Empty;
    public ScanState State { get; set; }
    public string? NewRecentDirectory { get; set; }
    public (string Path, string Reason)? SkippedDirectory { get; set; }
    public (string Path, string Reason)? SkippedFile { get; set; }
}

public class ScannerService
{
    private readonly DatabaseService _dbService;

    // Separate atomic counters
    private long _directoriesVisited;
    private long _directoriesProcessed;
    private long _directoriesSkipped;

    private long _filesDiscovered;
    private long _filesIndexed;
    private long _filesSkipped;

    private long _logicalBytesIndexed;

    private readonly ConcurrentQueue<string> _recentDirectories = new();
    private readonly ConcurrentBag<(string Path, string Reason)> _skippedDirs = new();
    private readonly ConcurrentBag<(string Path, string Reason)> _skippedFiles = new();

    public ScannerService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public IReadOnlyCollection<string> RecentDirectories => _recentDirectories;
    public IReadOnlyCollection<(string Path, string Reason)> SkippedDirectories => _skippedDirs;
    public IReadOnlyCollection<(string Path, string Reason)> SkippedFiles => _skippedFiles;

    public async Task<ScanStats> ScanDrivesAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        // Reset counters
        Interlocked.Exchange(ref _directoriesVisited, 0);
        Interlocked.Exchange(ref _directoriesProcessed, 0);
        Interlocked.Exchange(ref _directoriesSkipped, 0);
        Interlocked.Exchange(ref _filesDiscovered, 0);
        Interlocked.Exchange(ref _filesIndexed, 0);
        Interlocked.Exchange(ref _filesSkipped, 0);
        Interlocked.Exchange(ref _logicalBytesIndexed, 0);

        while (_recentDirectories.TryDequeue(out _)) { }
        _skippedDirs.Clear();
        _skippedFiles.Clear();

        _dbService.ClearIndex();

        var channel = Channel.CreateBounded<FileRecord>(new BoundedChannelOptions(20000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var stopwatch = Stopwatch.StartNew();
        var lastReportStopwatch = Stopwatch.StartNew();
        var scanStartTime = DateTime.UtcNow;
        string currentDirectory = string.Empty;

        // Background SQLite Ingestion Task
        var dbWorker = Task.Run(async () =>
        {
            const int batchSize = 5000;
            var batch = new List<FileRecord>(batchSize);
            var reader = channel.Reader;

            while (await reader.WaitToReadAsync(CancellationToken.None))
            {
                while (reader.TryRead(out var item))
                {
                    batch.Add(item);
                    if (batch.Count >= batchSize)
                    {
                        _dbService.InsertBatch(batch);
                        batch.Clear();
                    }
                }
            }

            if (batch.Count > 0)
            {
                _dbService.InsertBatch(batch);
                batch.Clear();
            }
        });

        ScanState finalState = ScanState.Completed;

        try
        {
            foreach (var root in roots)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!Directory.Exists(root)) continue;

                var dirQueue = new Queue<string>();
                dirQueue.Enqueue(root);

                while (dirQueue.Count > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        finalState = ScanState.Cancelled;
                        break;
                    }

                    string dir = dirQueue.Dequeue();
                    currentDirectory = dir;

                    // Enqueue recent directory (keep rolling buffer <= 150)
                    _recentDirectories.Enqueue(dir);
                    while (_recentDirectories.Count > 150)
                    {
                        _recentDirectories.TryDequeue(out _);
                    }

                    // Directory Visited: attempted to enter/read
                    Interlocked.Increment(ref _directoriesVisited);

                    // 1. Enumerate files
                    bool dirEnumerationSucceeded = true;
                    try
                    {
                        foreach (var filePath in Directory.EnumerateFiles(dir))
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            // File Discovered
                            Interlocked.Increment(ref _filesDiscovered);

                            try
                            {
                                var fi = new FileInfo(filePath);
                                long size = fi.Length;
                                double modTime = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
                                double crtTime = new DateTimeOffset(fi.CreationTimeUtc).ToUnixTimeSeconds();
                                string ext = Path.GetExtension(filePath);
                                string name = fi.Name;

                                var record = new FileRecord
                                {
                                    Path = filePath,
                                    Name = name,
                                    Parent = dir,
                                    Size = size,
                                    ModifiedTime = modTime,
                                    CreatedTime = crtTime,
                                    Extension = ext,
                                    Category = FileCategory.FromExtension(ext),
                                    Accessible = 1
                                };

                                await channel.Writer.WriteAsync(record, cancellationToken);

                                // File Indexed
                                Interlocked.Increment(ref _filesIndexed);
                                Interlocked.Add(ref _logicalBytesIndexed, size);
                            }
                            catch (Exception ex)
                            {
                                // File Skipped
                                Interlocked.Increment(ref _filesSkipped);
                                if (_skippedFiles.Count < 500)
                                {
                                    _skippedFiles.Add((filePath, ex.Message));
                                }
                            }

                            // Throttled progress report
                            if (lastReportStopwatch.ElapsedMilliseconds >= 75)
                            {
                                EmitProgress(progress, stopwatch, currentDirectory, ScanState.Scanning, dir);
                                lastReportStopwatch.Restart();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        dirEnumerationSucceeded = false;
                        // Directory Skipped: enumeration failed
                        Interlocked.Increment(ref _directoriesSkipped);
                        if (_skippedDirs.Count < 500)
                        {
                            _skippedDirs.Add((dir, ex.Message));
                        }
                    }

                    if (dirEnumerationSucceeded)
                    {
                        // Directory Processed: successfully enumerated
                        Interlocked.Increment(ref _directoriesProcessed);
                    }

                    // 2. Enumerate subdirectories
                    try
                    {
                        foreach (var subDir in Directory.EnumerateDirectories(dir))
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            // Skip reparse points / symlinks if inaccessible or recursive loops
                            try
                            {
                                var di = new DirectoryInfo(subDir);
                                if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    // By default, do not follow symlinks/reparse points to avoid infinite recursion
                                    continue;
                                }
                            }
                            catch
                            {
                                // Inaccessible attributes, skip
                                continue;
                            }

                            dirQueue.Enqueue(subDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_skippedDirs.Count < 500)
                        {
                            _skippedDirs.Add((dir, "Subdirectories inaccessible: " + ex.Message));
                        }
                    }

                    // Throttled progress report
                    if (lastReportStopwatch.ElapsedMilliseconds >= 75)
                    {
                        EmitProgress(progress, stopwatch, currentDirectory, ScanState.Scanning, dir);
                        lastReportStopwatch.Restart();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            finalState = ScanState.Cancelled;
        }
        catch (Exception ex)
        {
            finalState = ScanState.Failed;
            _skippedDirs.Add((roots.FirstOrDefault() ?? "Root", ex.Message));
        }
        finally
        {
            // Complete channel and wait for SQLite ingestion to drain
            channel.Writer.Complete();
            await dbWorker;

            stopwatch.Stop();
        }

        if (cancellationToken.IsCancellationRequested && finalState != ScanState.Failed)
        {
            finalState = ScanState.Cancelled;
        }

        var scanFinishTime = DateTime.UtcNow;

        var finalStats = new ScanStats
        {
            DirectoriesVisited = Volatile.Read(ref _directoriesVisited),
            DirectoriesProcessed = Volatile.Read(ref _directoriesProcessed),
            DirectoriesSkipped = Volatile.Read(ref _directoriesSkipped),
            FilesDiscovered = Volatile.Read(ref _filesDiscovered),
            FilesIndexed = Volatile.Read(ref _filesIndexed),
            FilesSkipped = Volatile.Read(ref _filesSkipped),
            LogicalBytesIndexed = Volatile.Read(ref _logicalBytesIndexed),
            Elapsed = stopwatch.Elapsed,
            FilesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? Volatile.Read(ref _filesIndexed) / stopwatch.Elapsed.TotalSeconds : 0,
            BytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? Volatile.Read(ref _logicalBytesIndexed) / stopwatch.Elapsed.TotalSeconds : 0,
            CurrentDirectory = currentDirectory,
            State = finalState
        };

        // Save scan metadata in SQLite
        _dbService.SaveScanMetadata(finalStats, string.Join(";", roots), scanStartTime, scanFinishTime);

        // Final progress report
        progress?.Report(new ScanProgressReport
        {
            DirectoriesVisited = finalStats.DirectoriesVisited,
            DirectoriesProcessed = finalStats.DirectoriesProcessed,
            DirectoriesSkipped = finalStats.DirectoriesSkipped,
            FilesDiscovered = finalStats.FilesDiscovered,
            FilesIndexed = finalStats.FilesIndexed,
            FilesSkipped = finalStats.FilesSkipped,
            LogicalBytesIndexed = finalStats.LogicalBytesIndexed,
            Elapsed = finalStats.Elapsed,
            FilesPerSecond = finalStats.FilesPerSecond,
            BytesPerSecond = finalStats.BytesPerSecond,
            CurrentDirectory = finalStats.CurrentDirectory,
            State = finalStats.State
        });

        return finalStats;
    }

    private void EmitProgress(
        IProgress<ScanProgressReport>? progress,
        Stopwatch sw,
        string currentDir,
        ScanState state,
        string? newRecentDir)
    {
        if (progress == null) return;

        long indexed = Volatile.Read(ref _filesIndexed);
        long bytes = Volatile.Read(ref _logicalBytesIndexed);
        double sec = sw.Elapsed.TotalSeconds;

        progress.Report(new ScanProgressReport
        {
            DirectoriesVisited = Volatile.Read(ref _directoriesVisited),
            DirectoriesProcessed = Volatile.Read(ref _directoriesProcessed),
            DirectoriesSkipped = Volatile.Read(ref _directoriesSkipped),
            FilesDiscovered = Volatile.Read(ref _filesDiscovered),
            FilesIndexed = indexed,
            FilesSkipped = Volatile.Read(ref _filesSkipped),
            LogicalBytesIndexed = bytes,
            Elapsed = sw.Elapsed,
            FilesPerSecond = sec > 0 ? indexed / sec : 0,
            BytesPerSecond = sec > 0 ? bytes / sec : 0,
            CurrentDirectory = currentDir,
            State = state,
            NewRecentDirectory = newRecentDir
        });
    }
}
