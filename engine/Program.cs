using System.Text.Json;
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
    private readonly DiskService _disks = new();
    private CancellationTokenSource? _scanCancellation;
    private Task? _scanTask;
    private long _eventSequence;

    public EngineHost(TextReader input, TextWriter output, string? dataDir)
    {
        _input = input;
        _output = output;
        string? databasePath = dataDir == null ? null : Path.Combine(dataDir, "scan_index.db");
        string? settingsPath = dataDir == null ? null : Path.Combine(dataDir, "settings.json");
        _database = new DatabaseService(databasePath);
        _database.Initialize();
        _scanner = new ScannerService(_database, settingsService: new SettingsService(settingsPath));
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

                switch (command)
                {
                    case "ping":
                        Respond(id, new { protocolVersion = 1, engine = "ArborGraph" });
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
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                RespondError(id, "ENGINE_ERROR", "The engine could not complete the request.");
            }
        }

        await StopScanAsync();
    }

    private void StartScan(string id, JsonElement request)
    {
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
                state = result.State.ToString(), filesIndexed = result.FilesIndexed,
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
        _database.Dispose();
    }
}

internal sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
