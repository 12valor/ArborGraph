using System.Text.Json;
using DiskScope.Models;
using DiskScope.Services;

namespace ArborGraph.EngineHost;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? dataDir = null;
        if (args.Length == 2 && args[0] == "--data-dir")
        {
            dataDir = Path.GetFullPath(args[1]);
        }
        else if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: ArborGraph.EngineHost [--data-dir <directory>]");
            return 2;
        }

        try
        {
            // The supported WPF app and this host share the default SQLite index.
            using var instanceMutex = dataDir == null
                ? new Mutex(initiallyOwned: false, @"Local\ArborGraph.SingleInstance")
                : null;
            bool ownsMutex = false;
            if (instanceMutex != null)
            {
                try { ownsMutex = instanceMutex.WaitOne(0); }
                catch (AbandonedMutexException) { ownsMutex = true; }
                if (!ownsMutex)
                {
                    Console.Error.WriteLine("ArborGraph is already using the default data directory.");
                    return 3;
                }
            }

            try
            {
                using var host = new EngineHost(Console.In, Console.Out, dataDir);
                host.RunAsync().GetAwaiter().GetResult();
                return 0;
            }
            finally
            {
                if (ownsMutex) instanceMutex!.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed class EngineHost : IDisposable
{
    private const int MaxRequestChars = 65_536;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly object _writeLock = new();
    private readonly DatabaseService _database;
    private readonly ScannerService _scanner;
    private readonly SettingsService _settings;
    private readonly DiskService _disks = new();
    private CancellationTokenSource? _scanCancellation;
    private Task? _scanTask;
    private CancellationTokenSource? _duplicateCancellation;
    private Task? _duplicateTask;
    private readonly object _duplicateLock = new();
    private List<DuplicateGroup> _duplicateGroups = [];
    private bool _duplicateHasRun;
    private long _eventSequence;

    public EngineHost(TextReader input, TextWriter output, string? dataDir)
    {
        _input = input;
        _output = output;
        string? databasePath = dataDir == null ? null : Path.Combine(dataDir, "scan_index.db");
        string? settingsPath = dataDir == null ? null : Path.Combine(dataDir, "settings.json");
        _database = new DatabaseService(databasePath);
        _database.Initialize();
        _settings = new SettingsService(settingsPath);
        _scanner = new ScannerService(_database, settingsService: _settings);
    }

    public async Task RunAsync()
    {
        while (true)
        {
            (string? line, bool oversized) = await ReadBoundedLineAsync();
            if (line == null && !oversized) break;
            if (oversized)
            {
                Write(new { type = "response", id = (string?)null, ok = false, error = new { code = "MESSAGE_TOO_LARGE", message = "Request exceeds 65536 characters." } });
                continue;
            }

            string? id = null;
            try
            {
                using var document = JsonDocument.Parse(line!);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String ||
                    !root.TryGetProperty("command", out var commandValue) || commandValue.ValueKind != JsonValueKind.String)
                {
                    RespondError(null, "INVALID_REQUEST", "Expected an object with string id and command.");
                    continue;
                }

                id = idValue.GetString();
                string? command = commandValue.GetString();
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || string.IsNullOrWhiteSpace(command))
                {
                    RespondError(id, "INVALID_REQUEST", "Invalid id or command.");
                    continue;
                }

                if (!_settings.CurrentSettings.HasAcceptedEula &&
                    command is not ("ping" or "getLegal" or "acceptEula" or "shutdown"))
                {
                    RespondError(id, "EULA_REQUIRED", "Accept the license agreement before using ArborGraph.");
                    continue;
                }

                switch (command)
                {
                    case "ping":
                        Respond(id, new { protocolVersion = 1, engine = "ArborGraph" });
                        break;
                    case "getLegal":
                        using (var stream = typeof(EngineHost).Assembly.GetManifestResourceStream("ArborGraph.EngineHost.LICENSE.md")
                            ?? throw new InvalidOperationException("License text is missing."))
                        using (var reader = new StreamReader(stream))
                        {
                            var settings = _settings.CurrentSettings;
                            Respond(id, new { text = reader.ReadToEnd(), version = "1.0",
                                accepted = settings.HasAcceptedEula,
                                acceptedAt = settings.EulaAcceptedDate });
                        }
                        break;
                    case "acceptEula":
                        if (!root.TryGetProperty("accepted", out var consent) || consent.ValueKind != JsonValueKind.True)
                            throw new ArgumentException("accepted must be true.");
                        var current = _settings.CurrentSettings;
                        current.HasAcceptedEula = true;
                        current.EulaAcceptedVersion = "1.0";
                        current.EulaAcceptedDate = DateTime.UtcNow;
                        _settings.SaveSettings(current);
                        if (!_settings.LoadSettings().HasAcceptedEula)
                            throw new IOException("License acceptance could not be saved.");
                        Respond(id, new { accepted = true, version = "1.0" });
                        break;
                    case "getSettings":
                        RespondSettings(id);
                        break;
                    case "setScanSettings":
                        SetScanSettings(id, root);
                        break;
                    case "addExclusion":
                        ChangeExclusion(id, root, add: true);
                        break;
                    case "removeExclusion":
                        ChangeExclusion(id, root, add: false);
                        break;
                    case "listDrives":
                        Respond(id, _disks.GetSystemDrives().Select(d => new
                        {
                            path = d.Name, label = d.VolumeLabel, totalBytes = d.TotalBytes,
                            freeBytes = d.FreeBytes, usedBytes = d.UsedBytes, fileSystem = d.FileSystem
                        }).ToList());
                        break;
                    case "getOverview":
                        var (count, bytes) = _database.GetTotalIndexedStorage();
                        Respond(id, new { indexedFiles = count, indexedLogicalBytes = bytes,
                            categories = _database.GetCategoryBreakdown().Select(x => new
                            { category = x.Key, fileCount = x.Value.Count, logicalBytes = x.Value.TotalSize }).ToList() });
                        break;
                    case "getFiles":
                        GetFiles(id, root);
                        break;
                    case "getLargestFolders":
                        int folderLimit = ReadInt(root, "limit", 30, 1, 100);
                        Respond(id, _database.GetLargestFolders(folderLimit).Select(folder => new
                        {
                            path = folder.Path, name = folder.Name, parent = folder.Parent,
                            logicalBytes = folder.Size, fileCount = folder.FileCount,
                            subfolderCount = folder.SubfolderCount
                        }).ToList());
                        break;
                    case "getDirectoryContents":
                        GetDirectoryContents(id, root);
                        break;
                    case "getScanHistory":
                        GetScanHistory(id, root);
                        break;
                    case "compareScans":
                        CompareScans(id, root);
                        break;
                    case "getFileAgeInsights":
                        GetFileAgeInsights(id, root);
                        break;
                    case "getPhotoshopInsights":
                        GetPhotoshopInsights(id, root);
                        break;
                    case "getDuplicateOverview":
                        var overview = _database.GetDuplicateOverview();
                        Respond(id, new { candidateGroups = overview.CandidateGroups,
                            candidateFiles = overview.CandidateFiles,
                            potentialRedundantBytes = overview.PotentialWastedBytes });
                        break;
                    case "getDuplicateStatus":
                        lock (_duplicateLock) Respond(id, new { running = _duplicateCancellation != null,
                            hasResults = _duplicateHasRun });
                        break;
                    case "startDuplicateAnalysis":
                        StartDuplicateAnalysis(id);
                        break;
                    case "cancelDuplicateAnalysis":
                        if (_duplicateCancellation == null) RespondError(id, "NO_ACTIVE_ANALYSIS", "No duplicate analysis is running.");
                        else { _duplicateCancellation.Cancel(); Respond(id, new { cancelling = true }); }
                        break;
                    case "getDuplicateResults":
                        GetDuplicateResults(id, root);
                        break;
                    case "startScan":
                        StartScan(id, root);
                        break;
                    case "cancelScan":
                        if (_scanCancellation == null) RespondError(id, "NO_ACTIVE_SCAN", "No scan is running.");
                        else { _scanCancellation.Cancel(); Respond(id, new { cancelling = true }); }
                        break;
                    case "shutdown":
                        Respond(id, new { stopping = true });
                        await StopScanAsync();
                        await StopDuplicateAnalysisAsync();
                        return;
                    default:
                        RespondError(id, "UNKNOWN_COMMAND", "Command is not allowed.");
                        break;
                }
            }
            catch (JsonException)
            {
                RespondError(id, "INVALID_JSON", "Request is not valid JSON.");
            }
            catch (ArgumentException ex)
            {
                RespondError(id, "INVALID_ARGUMENT", ex.Message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                RespondError(id, "ENGINE_ERROR", "The engine could not complete the request.");
            }
        }

        await StopScanAsync();
    }

    private static int ReadInt(JsonElement root, string name, int fallback, int min, int max)
    {
        if (!root.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result < min || result > max)
            throw new ArgumentException($"{name} must be between {min} and {max}.");
        return result;
    }

    private static string? ReadText(JsonElement root, string name, int maxLength)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text || text.Length > maxLength)
            throw new ArgumentException($"{name} must be a string of at most {maxLength} characters.");
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool ReadBool(JsonElement root, string name, bool fallback)
    {
        if (!root.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException($"{name} must be a boolean.");
        return value.GetBoolean();
    }

    private void RespondSettings(string id)
    {
        var settings = _settings.CurrentSettings;
        Respond(id, new { workerCount = settings.WorkerCount, followJunctions = settings.FollowJunctions,
            includeHiddenFiles = settings.IncludeHiddenFiles, includeSystemFiles = settings.IncludeSystemFiles,
            excludedPaths = settings.ExcludedPaths.ToList(), protectedExclusions = ProtectedExclusions(), retentionDays = settings.RetentionDays,
            maxScanSessionsToKeep = settings.MaxScanSessionsToKeep, logLevel = settings.LogLevel,
            defaultToRecycleBin = settings.DefaultToRecycleBin,
            requireConfirmationForRecycleBin = settings.RequireConfirmationForRecycleBin,
            requireConfirmationForPermanent = settings.RequireConfirmationForPermanent });
    }

    private void SetScanSettings(string id, JsonElement root)
    {
        if (_scanCancellation != null) throw new ArgumentException("Wait for the active scan before changing settings.");
        var settings = _settings.CurrentSettings;
        settings.WorkerCount = ReadInt(root, "workerCount", settings.WorkerCount, 0, 64);
        settings.FollowJunctions = ReadBool(root, "followJunctions", settings.FollowJunctions);
        settings.IncludeHiddenFiles = ReadBool(root, "includeHiddenFiles", settings.IncludeHiddenFiles);
        settings.IncludeSystemFiles = ReadBool(root, "includeSystemFiles", settings.IncludeSystemFiles);
        _settings.SaveSettings(settings);
        var saved = _settings.LoadSettings();
        if (saved.WorkerCount != settings.WorkerCount || saved.FollowJunctions != settings.FollowJunctions ||
            saved.IncludeHiddenFiles != settings.IncludeHiddenFiles || saved.IncludeSystemFiles != settings.IncludeSystemFiles ||
            !saved.HasAcceptedEula)
            throw new IOException("Scan settings could not be saved.");
        RespondSettings(id);
    }

    private void ChangeExclusion(string id, JsonElement root, bool add)
    {
        if (_scanCancellation != null) throw new ArgumentException("Wait for the active scan before changing exclusions.");
        string path = ReadText(root, "path", 32767) ?? throw new ArgumentException("path is required.");
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("path must be absolute.");
        if (add && !Directory.Exists(path)) throw new ArgumentException("Choose an existing folder to exclude.");
        if (!add && ProtectedExclusions().Any(item => string.Equals(item.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Built-in safety exclusions cannot be removed.");
        bool changed = add ? _settings.AddExclusion(path) : _settings.RemoveExclusion(path);
        bool present = _settings.LoadSettings().ExcludedPaths.Any(existing =>
            string.Equals(existing.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (changed && present != add)
            throw new IOException("Exclusion change could not be saved.");
        RespondSettings(id);
    }

    private static List<string> ProtectedExclusions()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
        return string.IsNullOrEmpty(windows) ? [@"C:\$Recycle.Bin"] : [windows, @"C:\$Recycle.Bin"];
    }

    private void GetFiles(string id, JsonElement root)
    {
        int offset = ReadInt(root, "offset", 0, 0, 10_000_000);
        int limit = ReadInt(root, "limit", 50, 1, 200);
        string? search = ReadText(root, "search", 200);
        string? category = ReadText(root, "category", 80);
        string? extension = ReadText(root, "extension", 32);
        string? location = ReadText(root, "locationPrefix", 32767);
        string sortBy = ReadText(root, "sortBy", 32) ?? "size";
        if (sortBy is not ("size" or "name" or "path" or "modified_time" or "category" or "extension"))
            throw new ArgumentException("sortBy is not supported.");
        bool sortDesc = ReadBool(root, "sortDesc", true);
        long total = _database.GetFilteredFileCount(category: category, search: search,
            extension: extension, locationPrefix: location);
        var files = _database.GetFilesPaged(offset, limit, category: category, search: search,
            sortBy: sortBy, sortDesc: sortDesc, extension: extension, locationPrefix: location)
            .Select(file => new
            {
                path = file.Path, name = file.Name, parent = file.Parent, logicalBytes = file.Size,
                modifiedTime = file.ModifiedTime, extension = file.Extension,
                category = file.Category, accessible = file.Accessible == 1
            }).ToList();
        Respond(id, new { total, offset, limit, files });
    }

    private void GetDirectoryContents(string id, JsonElement root)
    {
        string? path = ReadText(root, "path", 32767);
        int limit = ReadInt(root, "limit", 100, 1, 150);
        if (path != null && !Path.IsPathFullyQualified(path))
            throw new ArgumentException("path must be an absolute indexed directory.");
        var contents = _database.GetDirectoryContents(path, limit);
        var items = contents.Folders.Select(folder => new
        {
            path = folder.Path, name = folder.Name, logicalBytes = folder.Size,
            isDirectory = true, fileCount = folder.FileCount, category = "Folder"
        }).Concat(contents.Files.Select(file => new
        {
            path = file.Path, name = file.Name, logicalBytes = file.Size,
            isDirectory = false, fileCount = 1L, category = file.Category
        })).OrderByDescending(item => item.logicalBytes).Take(limit).ToList();
        Respond(id, new { path, logicalBytes = contents.TotalBytes, totalItems = contents.TotalItems, items });
    }

    private static long ReadPositiveLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long result) || result < 1)
            throw new ArgumentException($"{name} must be a positive scan ID.");
        return result;
    }

    private void GetScanHistory(string id, JsonElement root)
    {
        int limit = ReadInt(root, "limit", 30, 1, 100);
        Respond(id, _database.GetScanHistory(limit).OrderByDescending(item => item.Id).Select(item => new
        {
            id = item.Id, startedAt = new DateTimeOffset(item.StartTime, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            finishedAt = new DateTimeOffset(item.FinishTime, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            roots = item.Roots, state = item.ScanStatus,
            filesDiscovered = item.FilesDiscovered, filesIndexed = item.FilesIndexed,
            filesSkipped = item.FilesSkipped, directoriesProcessed = item.DirectoriesProcessed,
            directoriesSkipped = item.DirectoriesSkipped, indexedLogicalBytes = item.LogicalBytesIndexed
        }).ToList());
    }

    private void CompareScans(string id, JsonElement root)
    {
        long olderId = ReadPositiveLong(root, "olderId");
        long newerId = ReadPositiveLong(root, "newerId");
        var history = _database.GetScanHistory(100);
        var older = history.FirstOrDefault(item => item.Id == olderId);
        var newer = history.FirstOrDefault(item => item.Id == newerId);
        if (older == null || newer == null || older.Id >= newer.Id || older.ScanStatus != "Completed" ||
            newer.ScanStatus != "Completed" || !string.Equals(older.Roots, newer.Roots, StringComparison.OrdinalIgnoreCase))
        {
            RespondError(id, "NOT_COMPARABLE", "Choose two completed scans with the same targets, oldest first.");
            return;
        }
        var before = _database.GetScanCategories(older.Id);
        var after = _database.GetScanCategories(newer.Id);
        var categories = before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).Select(category =>
        {
            before.TryGetValue(category, out var oldValue);
            after.TryGetValue(category, out var newValue);
            return new { category, previousBytes = oldValue.Size, currentBytes = newValue.Size,
                deltaBytes = newValue.Size - oldValue.Size, previousFiles = oldValue.Count,
                currentFiles = newValue.Count, deltaFiles = newValue.Count - oldValue.Count };
        }).OrderByDescending(item => Math.Abs(item.deltaBytes)).ToList();
        bool coverageWarning = older.FilesSkipped != newer.FilesSkipped ||
            older.DirectoriesSkipped != newer.DirectoriesSkipped || older.FilesSkipped > 0 || newer.FilesSkipped > 0 ||
            older.DirectoriesSkipped > 0 || newer.DirectoriesSkipped > 0;
        Respond(id, new { olderId, newerId, roots = newer.Roots, coverageWarning,
            previousBytes = older.LogicalBytesIndexed, currentBytes = newer.LogicalBytesIndexed,
            deltaBytes = newer.LogicalBytesIndexed - older.LogicalBytesIndexed,
            previousFiles = older.FilesIndexed, currentFiles = newer.FilesIndexed,
            deltaFiles = newer.FilesIndexed - older.FilesIndexed,
            categories });
    }

    private void GetFileAgeInsights(string id, JsonElement root)
    {
        int daysOld = ReadInt(root, "daysOld", 180, 1, 3650);
        int limit = ReadInt(root, "limit", 100, 1, 200);
        Respond(id, BuildFileAgeInsights(daysOld, limit));
    }

    private object BuildFileAgeInsights(int daysOld, int limit)
    {
        var ageBuckets = _database.GetFileAgeBreakdown().Select(bucket => new
        {
            name = bucket.Name, description = bucket.Description,
            fileCount = bucket.Count, logicalBytes = bucket.TotalBytes
        }).ToList();
        var oldFiles = _database.GetOldFiles(daysOld, limit).Select(file => new
        {
            path = file.Path, name = file.Name, logicalBytes = file.Size,
            modifiedTime = file.ModifiedTime, category = file.Category
        }).ToList();
        return new { daysOld, ageBuckets, oldFiles };
    }

    private void GetPhotoshopInsights(string id, JsonElement root)
    {
        int limit = ReadInt(root, "limit", 100, 1, 200);
        Respond(id, BuildPhotoshopInsights(limit));
    }

    private object BuildPhotoshopInsights(int limit)
    {
        var photoshop = _database.GetPhotoshopDetailedItems(limit).Select(item => new
        {
            path = item.File.Path, name = item.File.Name, logicalBytes = item.File.Size,
            type = item.TypeLabel, modifiedTime = item.File.ModifiedTime
        }).ToList();
        var psStats = _database.GetPhotoshopStats();
        return new
        {
            photoshop,
            photoshopSummary = new
            {
                psdCount = psStats.PsdCount, psbCount = psStats.PsbCount,
                otherCount = psStats.OtherCount, identifiedLogicalBytes = psStats.TotalBytes,
                psdBytes = psStats.PsdBytes, psbBytes = psStats.PsbBytes
            }
        };
    }

    private void StartScan(string id, JsonElement request)
    {
        if (_duplicateCancellation != null)
        {
            RespondError(id, "ANALYSIS_IN_PROGRESS", "Finish duplicate analysis before scanning.");
            return;
        }
        if (_scanCancellation != null)
        {
            RespondError(id, "SCAN_IN_PROGRESS", "A scan is already running.");
            return;
        }
        if (!request.TryGetProperty("roots", out var rootsValue) || rootsValue.ValueKind != JsonValueKind.Array ||
            rootsValue.GetArrayLength() is < 1 or > 16)
        {
            RespondError(id, "INVALID_TARGETS", "Select between 1 and 16 scan roots.");
            return;
        }
        var roots = new List<string>();
        foreach (var item in rootsValue.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { Length: > 0 and <= 32767 } path)
            {
                RespondError(id, "INVALID_TARGETS", "Every target must be a path string.");
                return;
            }
            string canonical = Path.GetFullPath(path);
            if (!Path.IsPathFullyQualified(canonical) || !Directory.Exists(canonical))
            {
                RespondError(id, "INVALID_TARGETS", "A selected directory is unavailable.");
                return;
            }
            roots.Add(canonical);
        }
        if (roots.Distinct(StringComparer.OrdinalIgnoreCase).Count() != roots.Count)
        {
            RespondError(id, "INVALID_TARGETS", "Scan roots must be distinct.");
            return;
        }

        var cancellation = new CancellationTokenSource();
        lock (_duplicateLock) { _duplicateGroups = []; _duplicateHasRun = false; }
        _scanCancellation = cancellation;
        Respond(id, new { accepted = true, roots });
        var progress = new InlineProgress<ScanProgressReport>(p => Write(new
        {
            type = "event", eventName = "scanProgress", sequence = Interlocked.Increment(ref _eventSequence),
            filesDiscovered = p.FilesDiscovered, filesIndexed = p.FilesIndexed,
            directoriesProcessed = p.DirectoriesProcessed, directoriesSkipped = p.DirectoriesSkipped,
            filesSkipped = p.FilesSkipped, indexedLogicalBytes = p.LogicalBytesIndexed,
            elapsedMilliseconds = (long)p.Elapsed.TotalMilliseconds, currentDirectory = p.CurrentDirectory,
            state = p.State.ToString()
        }));
        _scanTask = RunScanAsync(roots, progress, cancellation);
    }

    private async Task RunScanAsync(List<string> roots, IProgress<ScanProgressReport> progress, CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _scanner.ScanDrivesAsync(roots, progress, cancellation.Token, enableIncremental: false);
            Write(new { type = "event", eventName = "scanFinished", sequence = Interlocked.Increment(ref _eventSequence),
                state = result.State.ToString(), filesDiscovered = result.FilesDiscovered,
                filesIndexed = result.FilesIndexed, directoriesProcessed = result.DirectoriesProcessed,
                directoriesSkipped = result.DirectoriesSkipped, filesSkipped = result.FilesSkipped,
                indexedLogicalBytes = result.LogicalBytesIndexed, elapsedMilliseconds = (long)result.Elapsed.TotalMilliseconds });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Write(new { type = "event", eventName = "scanFailed", sequence = Interlocked.Increment(ref _eventSequence),
                code = "SCAN_FAILED", message = ex.Message });
        }
        finally
        {
            _scanCancellation = null;
            cancellation.Dispose();
        }
    }

    private async Task StopScanAsync()
    {
        _scanCancellation?.Cancel();
        if (_scanTask != null) await _scanTask;
    }

    private void StartDuplicateAnalysis(string id)
    {
        if (_scanCancellation != null || _duplicateCancellation != null)
        {
            RespondError(id, "WORK_IN_PROGRESS", "Finish the active scan or analysis first.");
            return;
        }
        var cancellation = new CancellationTokenSource();
        _duplicateCancellation = cancellation;
        lock (_duplicateLock) { _duplicateGroups = []; _duplicateHasRun = false; }
        Respond(id, new { accepted = true });
        _duplicateTask = RunDuplicateAnalysisAsync(cancellation);
    }

    private async Task RunDuplicateAnalysisAsync(CancellationTokenSource cancellation)
    {
        try
        {
            var analyzer = new DuplicateAnalyzer(_database);
            var progress = new InlineProgress<string>(message => Write(new
            {
                type = "event", eventName = "duplicateProgress",
                sequence = Interlocked.Increment(ref _eventSequence), message
            }));
            var results = await analyzer.FindDuplicatesAsync(statusProgress: progress,
                cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            lock (_duplicateLock) { _duplicateGroups = results; _duplicateHasRun = true; }
            Write(new { type = "event", eventName = "duplicateFinished",
                sequence = Interlocked.Increment(ref _eventSequence), state = "Completed",
                groupCount = results.Count, duplicateFiles = results.Sum(group => group.FileCount),
                potentialRedundantBytes = results.Sum(group => group.WastedBytes) });
        }
        catch (OperationCanceledException)
        {
            Write(new { type = "event", eventName = "duplicateFinished",
                sequence = Interlocked.Increment(ref _eventSequence), state = "Cancelled" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Write(new { type = "event", eventName = "duplicateFailed",
                sequence = Interlocked.Increment(ref _eventSequence), message = "Duplicate analysis failed." });
        }
        finally
        {
            _duplicateCancellation = null;
            cancellation.Dispose();
        }
    }

    private void GetDuplicateResults(string id, JsonElement root)
    {
        if (_duplicateCancellation != null)
        {
            RespondError(id, "ANALYSIS_IN_PROGRESS", "Results are available after analysis completes.");
            return;
        }
        int offset = ReadInt(root, "offset", 0, 0, 10_000);
        int limit = ReadInt(root, "limit", 25, 1, 50);
        lock (_duplicateLock)
        {
            int remainingPathCharacters = 500_000;
            var groups = _duplicateGroups.Skip(offset).Take(limit).Select(group =>
            {
                var files = new List<object>();
                foreach (var file in group.Files.Take(100))
                {
                    if (file.Path.Length > remainingPathCharacters) break;
                    remainingPathCharacters -= file.Path.Length;
                    files.Add(new { path = file.Path, name = file.Name, modifiedTime = file.ModifiedTime });
                }
                return new
                {
                    exactSize = group.ExactSize, sha256 = group.Sha256,
                    fileCount = group.FileCount, potentialRedundantBytes = group.WastedBytes,
                    filesTruncated = group.FileCount > files.Count,
                    files
                };
            }).ToList();
            Respond(id, new { total = _duplicateGroups.Count, offset, limit,
                groups });
        }
    }

    private async Task StopDuplicateAnalysisAsync()
    {
        _duplicateCancellation?.Cancel();
        if (_duplicateTask != null) await _duplicateTask;
    }

    private async Task<(string? Line, bool Oversized)> ReadBoundedLineAsync()
    {
        var buffer = new char[1];
        var line = new System.Text.StringBuilder();
        bool oversized = false;
        while (true)
        {
            int read = await _input.ReadAsync(buffer, 0, 1);
            if (read == 0) return line.Length == 0 && !oversized ? (null, false) : (line.ToString(), oversized);
            if (buffer[0] == '\n') return (line.ToString().TrimEnd('\r'), oversized);
            if (line.Length < MaxRequestChars) line.Append(buffer[0]);
            else oversized = true;
        }
    }

    private void Respond(string id, object data) => Write(new { type = "response", id, ok = true, data });
    private void RespondError(string? id, string code, string message) => Write(new { type = "response", id, ok = false, error = new { code, message } });

    private void Write(object value)
    {
        lock (_writeLock)
        {
            _output.WriteLine(JsonSerializer.Serialize(value));
            _output.Flush();
        }
    }

    public void Dispose()
    {
        _scanCancellation?.Cancel();
        _duplicateCancellation?.Cancel();
        _database.Dispose();
    }
}

internal sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
