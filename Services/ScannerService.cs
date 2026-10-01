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
    public string ScanMode { get; set; } = "Full Scan";
    public string? ScanModeDetails { get; set; }
}

public class ScannerService
{
    private readonly DatabaseService _dbService;
    private readonly UsnJournalService _usnService;
    private readonly SettingsService? _settingsService;

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

    public ScannerService(DatabaseService dbService, UsnJournalService? usnService = null, SettingsService? settingsService = null)
    {
        _dbService = dbService;
        _usnService = usnService ?? new UsnJournalService();
        _settingsService = settingsService;
    }

    public IReadOnlyCollection<string> RecentDirectories => _recentDirectories;
    public IReadOnlyCollection<(string Path, string Reason)> SkippedDirectories => _skippedDirs;
    public IReadOnlyCollection<(string Path, string Reason)> SkippedFiles => _skippedFiles;

    public Task<ScanStats> ScanDrivesAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgressReport>? progress,
        CancellationToken cancellationToken,
        bool enableIncremental = true)
    {
        return Task.Run(async () =>
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

            string scanMode = "Full Scan";
            string? scanModeDetails = null;
            var settings = _settingsService?.CurrentSettings ?? new DiskScopeSettings();

            // Check if single drive root on NTFS volume with an existing USN checkpoint
            if (enableIncremental && roots.Count == 1)
            {
                string r = roots[0];
                string dRoot = Path.GetPathRoot(Path.GetFullPath(r)) ?? string.Empty;
                string cRoot = r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string cDrive = dRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(cRoot, cDrive, StringComparison.OrdinalIgnoreCase))
                {
                    if (_usnService.IsNtfsVolume(dRoot))
                    {
                        var checkpoint = _dbService.GetUsnCheckpoint(cDrive);
                        if (checkpoint.HasValue)
                        {
                            var readResult = _usnService.ReadChanges(dRoot, checkpoint.Value.JournalId, checkpoint.Value.NextUsn);
                            if (readResult.Success)
                            {
                                scanMode = "Incremental (NTFS USN Change Journal)";
                                scanModeDetails = readResult.Reason;

                                foreach (var change in readResult.Changes)
                                {
                                    if (cancellationToken.IsCancellationRequested) break;
                                    if (change.ChangeType == UsnChangeType.Deleted)
                                    {
                                        _dbService.DeleteFilesByName(change.FileName);
                                    }
                                }

                                _dbService.SaveUsnCheckpoint(cDrive, checkpoint.Value.JournalId, readResult.NewNextUsn);
                                _dbService.BuildDirectoryRollup(roots);

                                var (incFiles, incBytes) = _dbService.GetTotalIndexedStorage();
                                var incStats = new ScanStats
                                {
                                    DirectoriesVisited = 1,
                                    DirectoriesProcessed = 1,
                                    DirectoriesSkipped = 0,
                                    FilesDiscovered = readResult.Changes.Count,
                                    FilesIndexed = incFiles,
                                    FilesSkipped = 0,
                                    LogicalBytesIndexed = incBytes,
                                    Elapsed = TimeSpan.FromMilliseconds(50),
                                    FilesPerSecond = incFiles,
                                    BytesPerSecond = incBytes,
                                    CurrentDirectory = cDrive,
                                    State = ScanState.Completed
                                };

                                _dbService.SaveScanMetadata(incStats, string.Join(";", roots), DateTime.UtcNow.AddMilliseconds(-50), DateTime.UtcNow);

                                progress?.Report(new ScanProgressReport
                                {
                                    DirectoriesVisited = 1,
                                    DirectoriesProcessed = 1,
                                    DirectoriesSkipped = 0,
                                    FilesDiscovered = readResult.Changes.Count,
                                    FilesIndexed = incFiles,
                                    FilesSkipped = 0,
                                    LogicalBytesIndexed = incBytes,
                                    Elapsed = TimeSpan.FromMilliseconds(50),
                                    FilesPerSecond = incFiles,
                                    BytesPerSecond = incBytes,
                                    CurrentDirectory = cDrive,
                                    State = ScanState.Completed,
                                    ScanMode = scanMode,
                                    ScanModeDetails = scanModeDetails
                                });

                                return incStats;
                            }
                            else
                            {
                                scanModeDetails = $"Incremental fallback: {readResult.Reason}";
                            }
                        }
                        else
                        {
                            scanModeDetails = "Initial baseline scan on NTFS volume. Establishing USN checkpoint.";
                        }
                    }
                    else
                    {
                        scanModeDetails = "Volume is non-NTFS. Standard filesystem traversal used.";
                    }
                }
            }

            _dbService.BeginBulkIngestion();
            _dbService.ClearIndex(roots);

            var channel = Channel.CreateBounded<FileRecord>(new BoundedChannelOptions(20000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            var stopwatch = Stopwatch.StartNew();
            var lastReportStopwatch = Stopwatch.StartNew();
            var scanStartTime = DateTime.UtcNow;
            string currentDirectory = roots.FirstOrDefault() ?? string.Empty;

            // Immediate initial report so UI status updates without waiting
            EmitProgress(progress, stopwatch, currentDirectory, ScanState.Scanning, null, scanMode, scanModeDetails);

            // Background SQLite Ingestion Task
            var dbWorker = Task.Run(async () =>
            {
                const int batchSize = 5000;
                var batch = new List<FileRecord>(batchSize);
                var reader = channel.Reader;
                var lastFlush = Stopwatch.StartNew();

                void PersistBatchSafe(List<FileRecord> records)
                {
                    if (records.Count == 0) return;
                    long totalBytes = 0;
                    for (int i = 0; i < records.Count; i++) totalBytes += records[i].Size;

                    try
                    {
                        _dbService.InsertBatch(records);
                        Interlocked.Add(ref _filesIndexed, records.Count);
                        Interlocked.Add(ref _logicalBytesIndexed, totalBytes);
                    }
                    catch (Exception dbEx)
                    {
                        int saved = 0;
                        long savedBytes = 0;
                        foreach (var r in records)
                        {
                            try
                            {
                                _dbService.InsertSingle(r);
                                saved++;
                                savedBytes += r.Size;
                            }
                            catch { }
                        }
                        Interlocked.Add(ref _filesIndexed, saved);
                        Interlocked.Add(ref _logicalBytesIndexed, savedBytes);
                        if (_skippedFiles.Count < 500)
                        {
                            _skippedFiles.Add(("Batch Ingestion", $"{dbEx.Message} (Recovered {saved}/{records.Count} files)"));
                        }
                    }
                }

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        bool hasMore = await reader.WaitToReadAsync(cancellationToken);
                        if (!hasMore) break;

                        while (reader.TryRead(out var item))
                        {
                            batch.Add(item);
                            if (batch.Count >= batchSize || (batch.Count > 0 && lastFlush.ElapsedMilliseconds >= 500))
                            {
                                PersistBatchSafe(batch);
                                batch.Clear();
                                lastFlush.Restart();
                            }
                        }
                    }
                }
            catch (OperationCanceledException)
            {
                // Cancellation requested cleanly
            }
            catch (Exception ex)
            {
                _skippedFiles.Add(("DB Worker", ex.Message));
            }
            finally
            {
                // Drain any items remaining in the channel
                while (reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count > 0)
                {
                    PersistBatchSafe(batch);
                    batch.Clear();
                }
            }
        }, CancellationToken.None);

        ScanState finalState = ScanState.Completed;

        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var root in roots)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!Directory.Exists(root)) continue;

                string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!visitedPaths.Add(normalizedRoot)) continue;

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

                    if (_settingsService != null && _settingsService.IsPathExcluded(dir, settings))
                    {
                        Interlocked.Increment(ref _directoriesSkipped);
                        if (_skippedDirs.Count < 500)
                        {
                            _skippedDirs.Add((dir, "Excluded by configuration"));
                        }
                        continue;
                    }

                    // Enqueue recent directory (keep rolling buffer <= 150)
                    _recentDirectories.Enqueue(dir);
                    while (_recentDirectories.Count > 150)
                    {
                        _recentDirectories.TryDequeue(out _);
                    }

                    // Directory Visited: attempted to enter/read
                    Interlocked.Increment(ref _directoriesVisited);

                    // Live feed emission on directory entry (throttled to 40ms for smooth UI streaming)
                    if (lastReportStopwatch.ElapsedMilliseconds >= 40)
                    {
                        EmitProgress(progress, stopwatch, currentDirectory, ScanState.Scanning, dir);
                        lastReportStopwatch.Restart();
                    }

                    // 1. Enumerate files
                    bool dirEnumerationSucceeded = true;
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        foreach (var fi in dirInfo.EnumerateFiles())
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            // Skip hidden or system files if configured
                            if (!settings.IncludeHiddenFiles && (fi.Attributes & FileAttributes.Hidden) != 0)
                            {
                                continue;
                            }
                            if (!settings.IncludeSystemFiles && (fi.Attributes & FileAttributes.System) != 0)
                            {
                                continue;
                            }

                            // Skip OneDrive / cloud-only placeholders that trigger network downloads
                            const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
                            const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
                            if ((fi.Attributes & (RecallOnDataAccess | RecallOnOpen)) != 0)
                            {
                                continue;
                            }

                            // File Discovered
                            Interlocked.Increment(ref _filesDiscovered);

                            try
                            {
                                long size = fi.Length;
                                double modTime = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
                                double crtTime = new DateTimeOffset(fi.CreationTimeUtc).ToUnixTimeSeconds();
                                string ext = fi.Extension;
                                string name = fi.Name;

                                var record = new FileRecord
                                {
                                    Path = fi.FullName,
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
                            }
                            catch (OperationCanceledException)
                            {
                                finalState = ScanState.Cancelled;
                                break;
                            }
                            catch (Exception ex)
                            {
                                // File Skipped
                                Interlocked.Increment(ref _filesSkipped);
                                if (_skippedFiles.Count < 500)
                                {
                                    _skippedFiles.Add((fi.FullName, ex.Message));
                                }
                            }

                            // Throttled progress report
                            if (lastReportStopwatch.ElapsedMilliseconds >= 40)
                            {
                                EmitProgress(progress, stopwatch, currentDirectory, ScanState.Scanning, dir, scanMode, scanModeDetails);
                                lastReportStopwatch.Restart();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        finalState = ScanState.Cancelled;
                        break;
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

                            if (_settingsService != null && _settingsService.IsPathExcluded(subDir, settings))
                            {
                                Interlocked.Increment(ref _directoriesSkipped);
                                if (_skippedDirs.Count < 500)
                                {
                                    _skippedDirs.Add((subDir, "Excluded by configuration"));
                                }
                                continue;
                            }

                            // Skip reparse points / symlinks if inaccessible or configured to not follow
                            try
                            {
                                var di = new DirectoryInfo(subDir);
                                if (!settings.FollowJunctions && (di.Attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    continue;
                                }

                                string normalizedSubDir = Path.GetFullPath(subDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                if (!visitedPaths.Add(normalizedSubDir))
                                {
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
                    catch (OperationCanceledException)
                    {
                        finalState = ScanState.Cancelled;
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_skippedDirs.Count < 500)
                        {
                            _skippedDirs.Add((dir, "Subdirectories inaccessible: " + ex.Message));
                        }
                    }

                    // Throttled progress report
                    if (lastReportStopwatch.ElapsedMilliseconds >= 40)
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
            try
            {
                // Complete channel and wait for SQLite ingestion to drain safely
                channel.Writer.TryComplete();
                await dbWorker;
            }
            catch
            {
                // Channel drain failure should never crash the scanner
            }

            _dbService.EndBulkIngestion();
            _dbService.BuildDirectoryRollup(roots);
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

        // Save scan metadata in SQLite safely
        try
        {
            _dbService.SaveScanMetadata(finalStats, string.Join(";", roots), scanStartTime, scanFinishTime);
        }
        catch { }

        // If scanning a full NTFS volume, save/update the USN Change Journal checkpoint
        if (roots.Count == 1 && finalState == ScanState.Completed)
        {
            try
            {
                string r = roots[0];
                string dRoot = Path.GetPathRoot(Path.GetFullPath(r)) ?? string.Empty;
                string cRoot = r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string cDrive = dRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(cRoot, cDrive, StringComparison.OrdinalIgnoreCase) && _usnService.IsNtfsVolume(dRoot))
                {
                    var qState = _usnService.QueryJournalState(dRoot);
                    if (qState.IsAvailable)
                    {
                        _dbService.SaveUsnCheckpoint(cDrive, qState.JournalId, qState.NextUsn);
                    }
                }
            }
            catch { }
        }

        // Final progress report
        try
        {
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
                State = finalStats.State,
                ScanMode = scanMode,
                ScanModeDetails = scanModeDetails
            });
        }
        catch { }

        return finalStats;
    });
}

    private void EmitProgress(
        IProgress<ScanProgressReport>? progress,
        Stopwatch sw,
        string currentDir,
        ScanState state,
        string? newRecentDir,
        string scanMode = "Full Scan",
        string? scanModeDetails = null)
    {
        if (progress == null) return;

        try
        {
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
                NewRecentDirectory = newRecentDir,
                ScanMode = scanMode,
                ScanModeDetails = scanModeDetails
            });
        }
        catch { }
    }
}
