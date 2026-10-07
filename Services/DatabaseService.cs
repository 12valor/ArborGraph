using System.IO;
using DiskScope.Models;
using Microsoft.Data.Sqlite;

namespace DiskScope.Services;

public class DatabaseService : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public DatabaseService(string? customDbPath = null)
    {
        if (string.IsNullOrWhiteSpace(customDbPath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "ArborGraph");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "scan_index.db");

            // Migration from legacy DiskScope / DiskScopePro if ArborGraph database does not exist yet
            if (!File.Exists(_dbPath))
            {
                string legacyFolder = Path.Combine(appData, "DiskScope");
                if (!Directory.Exists(legacyFolder) && Directory.Exists(Path.Combine(appData, "DiskScopePro")))
                {
                    legacyFolder = Path.Combine(appData, "DiskScopePro");
                }

                string legacyDb = Path.Combine(legacyFolder, "scan_index.db");
                if (File.Exists(legacyDb))
                {
                    try
                    {
                        File.Copy(legacyDb, _dbPath, overwrite: false);
                        string legacyWal = legacyDb + "-wal";
                        string legacyShm = legacyDb + "-shm";
                        if (File.Exists(legacyWal)) File.Copy(legacyWal, _dbPath + "-wal", overwrite: false);
                        if (File.Exists(legacyShm)) File.Copy(legacyShm, _dbPath + "-shm", overwrite: false);
                    }
                    catch { }
                }
            }
        }
        else
        {
            _dbPath = customDbPath;
            string? dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DatabasePath => _dbPath;

    public void Initialize()
    {
        lock (_lock)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;
                PRAGMA cache_size = -64000;

                CREATE TABLE IF NOT EXISTS files (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    path TEXT NOT NULL UNIQUE,
                    name TEXT NOT NULL,
                    parent TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    modified_time REAL NOT NULL,
                    created_time REAL NOT NULL,
                    extension TEXT,
                    category TEXT,
                    accessible INTEGER NOT NULL DEFAULT 1
                );

                CREATE INDEX IF NOT EXISTS idx_files_size ON files(size DESC);
                CREATE INDEX IF NOT EXISTS idx_files_parent ON files(parent);
                CREATE INDEX IF NOT EXISTS idx_files_parent_size ON files(parent, size);
                CREATE INDEX IF NOT EXISTS idx_files_modified ON files(modified_time DESC);
                CREATE INDEX IF NOT EXISTS idx_files_modified_size ON files(modified_time, size);
                CREATE INDEX IF NOT EXISTS idx_files_extension ON files(extension);
                CREATE INDEX IF NOT EXISTS idx_files_category ON files(category);
                CREATE INDEX IF NOT EXISTS idx_files_category_size ON files(category, size);
                CREATE INDEX IF NOT EXISTS idx_files_name ON files(name);

                CREATE TABLE IF NOT EXISTS scan_metadata (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    scan_start REAL,
                    scan_finish REAL,
                    roots TEXT,
                    directories_visited INTEGER,
                    directories_processed INTEGER,
                    directories_skipped INTEGER,
                    files_discovered INTEGER,
                    files_indexed INTEGER,
                    files_skipped INTEGER,
                    logical_bytes_indexed INTEGER,
                    scan_status TEXT
                );

                CREATE TABLE IF NOT EXISTS directories (
                    path TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    parent TEXT NOT NULL,
                    total_size INTEGER NOT NULL,
                    direct_size INTEGER NOT NULL,
                    total_files INTEGER NOT NULL,
                    direct_files INTEGER NOT NULL,
                    subfolder_count INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_directories_total_size ON directories(total_size DESC);
                CREATE INDEX IF NOT EXISTS idx_directories_parent ON directories(parent);
                CREATE INDEX IF NOT EXISTS idx_directories_name ON directories(name);

                CREATE TABLE IF NOT EXISTS scan_history_categories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    scan_id INTEGER NOT NULL,
                    category TEXT NOT NULL,
                    total_size INTEGER NOT NULL,
                    file_count INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_scan_hist_cat ON scan_history_categories(scan_id);

                CREATE TABLE IF NOT EXISTS duplicate_groups (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    exact_size INTEGER NOT NULL,
                    file_count INTEGER NOT NULL,
                    wasted_bytes INTEGER NOT NULL,
                    sha256 TEXT
                );

                CREATE TABLE IF NOT EXISTS duplicate_files (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    group_id INTEGER NOT NULL,
                    path TEXT NOT NULL,
                    name TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    modified_time REAL NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_dup_files_group ON duplicate_files(group_id);

                CREATE TABLE IF NOT EXISTS drive_usn_checkpoints (
                    drive_letter TEXT PRIMARY KEY,
                    journal_id INTEGER NOT NULL,
                    next_usn INTEGER NOT NULL,
                    last_scan_time REAL NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public void ClearIndex(IReadOnlyList<string>? roots = null)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var tx = _connection!.BeginTransaction();
                try
                {
                    bool isFullClear = roots == null || roots.Count == 0 ||
                                       roots.Any(r => string.Equals(r?.Trim(), "ALL", StringComparison.OrdinalIgnoreCase));

                    if (isFullClear)
                    {
                        using var cmd = _connection.CreateCommand();
                        _activeCommand = cmd;
                        cmd.Transaction = tx;
                        cmd.CommandText = "DELETE FROM files; DELETE FROM directories;";
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        using var cmd = _connection.CreateCommand();
                        _activeCommand = cmd;
                        cmd.Transaction = tx;
                        // Use direct B-tree index range scan on path instead of unindexed OR/COLLATE NOCASE full-table scan
                        cmd.CommandText = @"
                            DELETE FROM files WHERE path >= $prefix AND path < $prefixUpper;
                            DELETE FROM files WHERE path = $root;
                            DELETE FROM directories WHERE path >= $prefix AND path < $prefixUpper;
                            DELETE FROM directories WHERE path = $root;
                        ";
                        var pRoot = cmd.Parameters.Add("$root", SqliteType.Text);
                        var pPrefix = cmd.Parameters.Add("$prefix", SqliteType.Text);
                        var pPrefixUpper = cmd.Parameters.Add("$prefixUpper", SqliteType.Text);

                        foreach (var root in roots!)
                        {
                            if (string.IsNullOrWhiteSpace(root)) continue;
                            string cleanRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            if (cleanRoot.Length >= 2 && cleanRoot[1] == ':')
                            {
                                cleanRoot = char.ToUpperInvariant(cleanRoot[0]) + cleanRoot.Substring(1);
                            }
                            string prefix = cleanRoot + Path.DirectorySeparatorChar;
                            string prefixUpper = cleanRoot + (char)(Path.DirectorySeparatorChar + 1);

                            pRoot.Value = cleanRoot;
                            pPrefix.Value = prefix;
                            pPrefixUpper.Value = prefixUpper;
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
                finally
                {
                    _activeCommand = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearIndex exception: {ex.Message}");
            }
        }
    }

    private SqliteCommand? _activeCommand;

    public void CancelActiveOperations()
    {
        try
        {
            _activeCommand?.Cancel();
        }
        catch { }
    }

    public void BeginBulkIngestion()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    PRAGMA synchronous = OFF;
                    PRAGMA temp_store = MEMORY;
                    PRAGMA cache_size = -64000;

                    DROP INDEX IF EXISTS idx_files_size;
                    DROP INDEX IF EXISTS idx_files_parent;
                    DROP INDEX IF EXISTS idx_files_parent_size;
                    DROP INDEX IF EXISTS idx_files_modified;
                    DROP INDEX IF EXISTS idx_files_modified_size;
                    DROP INDEX IF EXISTS idx_files_extension;
                    DROP INDEX IF EXISTS idx_files_category;
                    DROP INDEX IF EXISTS idx_files_category_size;
                    DROP INDEX IF EXISTS idx_files_name;
                ";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BeginBulkIngestion error: {ex.Message}");
            }
        }
    }

    public void EndBulkIngestion()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    CREATE INDEX IF NOT EXISTS idx_files_size ON files(size DESC);
                    CREATE INDEX IF NOT EXISTS idx_files_parent ON files(parent);
                    CREATE INDEX IF NOT EXISTS idx_files_parent_size ON files(parent, size);
                    CREATE INDEX IF NOT EXISTS idx_files_modified ON files(modified_time DESC);
                    CREATE INDEX IF NOT EXISTS idx_files_modified_size ON files(modified_time, size);
                    CREATE INDEX IF NOT EXISTS idx_files_extension ON files(extension);
                    CREATE INDEX IF NOT EXISTS idx_files_category ON files(category);
                    CREATE INDEX IF NOT EXISTS idx_files_category_size ON files(category, size);
                    CREATE INDEX IF NOT EXISTS idx_files_name ON files(name);

                    PRAGMA synchronous = NORMAL;
                    PRAGMA wal_checkpoint(PASSIVE);
                ";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EndBulkIngestion error: {ex.Message}");
            }
        }
    }

    public void InsertBatch(IReadOnlyList<FileRecord> records)
    {
        if (records.Count == 0) return;

        lock (_lock)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            try
            {
                using var cmd = _connection.CreateCommand();
                _activeCommand = cmd;
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO files 
                    (path, name, parent, size, modified_time, created_time, extension, category, accessible)
                    VALUES 
                    ($path, $name, $parent, $size, $modified, $created, $extension, $category, $accessible);
                ";

                var pPath = cmd.Parameters.Add("$path", SqliteType.Text);
                var pName = cmd.Parameters.Add("$name", SqliteType.Text);
                var pParent = cmd.Parameters.Add("$parent", SqliteType.Text);
                var pSize = cmd.Parameters.Add("$size", SqliteType.Integer);
                var pModified = cmd.Parameters.Add("$modified", SqliteType.Real);
                var pCreated = cmd.Parameters.Add("$created", SqliteType.Real);
                var pExtension = cmd.Parameters.Add("$extension", SqliteType.Text);
                var pCategory = cmd.Parameters.Add("$category", SqliteType.Text);
                var pAccessible = cmd.Parameters.Add("$accessible", SqliteType.Integer);

                foreach (var r in records)
                {
                    pPath.Value = r.Path;
                    pName.Value = r.Name;
                    pParent.Value = r.Parent;
                    pSize.Value = r.Size;
                    pModified.Value = r.ModifiedTime;
                    pCreated.Value = r.CreatedTime;
                    pExtension.Value = r.Extension;
                    pCategory.Value = r.Category;
                    pAccessible.Value = r.Accessible;

                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
            finally
            {
                _activeCommand = null;
            }
        }
    }

    public void InsertSingle(FileRecord r)
    {
        lock (_lock)
        {
            EnsureOpen();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO files 
                (path, name, parent, size, modified_time, created_time, extension, category, accessible)
                VALUES 
                ($path, $name, $parent, $size, $modified, $created, $extension, $category, $accessible);
            ";
            cmd.Parameters.AddWithValue("$path", r.Path);
            cmd.Parameters.AddWithValue("$name", r.Name);
            cmd.Parameters.AddWithValue("$parent", r.Parent);
            cmd.Parameters.AddWithValue("$size", r.Size);
            cmd.Parameters.AddWithValue("$modified", r.ModifiedTime);
            cmd.Parameters.AddWithValue("$created", r.CreatedTime);
            cmd.Parameters.AddWithValue("$extension", r.Extension);
            cmd.Parameters.AddWithValue("$category", r.Category);
            cmd.Parameters.AddWithValue("$accessible", r.Accessible);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteSingle(string path)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM files WHERE path = $path;";
                cmd.Parameters.AddWithValue("$path", path);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteSingle error: {ex.Message}");
            }
        }
    }

    public void DeleteFilesByName(string fileName)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM files WHERE name = $name;";
                cmd.Parameters.AddWithValue("$name", fileName);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteFilesByName error: {ex.Message}");
            }
        }
    }

    public void SaveScanMetadata(ScanStats stats, string roots, DateTime start, DateTime finish)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO scan_metadata 
                    (scan_start, scan_finish, roots, directories_visited, directories_processed, directories_skipped,
                     files_discovered, files_indexed, files_skipped, logical_bytes_indexed, scan_status)
                    VALUES
                    ($start, $finish, $roots, $dv, $dp, $ds, $fd, $fi, $fs, $bytes, $status);
                ";

                cmd.Parameters.AddWithValue("$start", new DateTimeOffset(start).ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("$finish", new DateTimeOffset(finish).ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("$roots", roots);
                cmd.Parameters.AddWithValue("$dv", stats.DirectoriesVisited);
                cmd.Parameters.AddWithValue("$dp", stats.DirectoriesProcessed);
                cmd.Parameters.AddWithValue("$ds", stats.DirectoriesSkipped);
                cmd.Parameters.AddWithValue("$fd", stats.FilesDiscovered);
                cmd.Parameters.AddWithValue("$fi", stats.FilesIndexed);
                cmd.Parameters.AddWithValue("$fs", stats.FilesSkipped);
                cmd.Parameters.AddWithValue("$bytes", stats.LogicalBytesIndexed);
                cmd.Parameters.AddWithValue("$status", stats.State.ToString());
                cmd.ExecuteNonQuery();

                // Get scanId and snapshot category distribution
                using var idCmd = _connection!.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                long scanId = Convert.ToInt64(idCmd.ExecuteScalar());

                if (scanId > 0)
                {
                    using var catCmd = _connection!.CreateCommand();
                    catCmd.CommandText = @"
                        INSERT INTO scan_history_categories (scan_id, category, total_size, file_count)
                        SELECT $scanId, category, SUM(size), COUNT(id)
                        FROM files
                        GROUP BY category;
                    ";
                    catCmd.Parameters.AddWithValue("$scanId", scanId);
                    catCmd.ExecuteNonQuery();

                    // Prune scan history beyond 50 sessions (retention strategy)
                    using var pruneCmd = _connection!.CreateCommand();
                    pruneCmd.CommandText = @"
                        DELETE FROM scan_history_categories 
                        WHERE scan_id NOT IN (SELECT id FROM scan_metadata ORDER BY id DESC LIMIT 50);

                        DELETE FROM scan_metadata 
                        WHERE id NOT IN (SELECT id FROM scan_metadata ORDER BY id DESC LIMIT 50);
                    ";
                    pruneCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveScanMetadata error: {ex.Message}");
            }
        }
    }

    public List<FileRecord> GetFilesPaged(
        int offset,
        int limit,
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? category = null,
        string? search = null,
        string sortBy = "size",
        bool sortDesc = true,
        int? minDaysOld = null,
        string? extension = null,
        string? locationPrefix = null)
    {
        lock (_lock)
        {
            var list = new List<FileRecord>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();

                string whereClause = "WHERE size >= $minSize";
                cmd.Parameters.AddWithValue("$minSize", minSize);

                if (maxSize < long.MaxValue && maxSize > 0)
                {
                    whereClause += " AND size <= $maxSize";
                    cmd.Parameters.AddWithValue("$maxSize", maxSize);
                }

                if (!string.IsNullOrWhiteSpace(category) && category != "All")
                {
                    whereClause += " AND category = $category";
                    cmd.Parameters.AddWithValue("$category", category);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClause += " AND (name LIKE $search OR path LIKE $search)";
                    cmd.Parameters.AddWithValue("$search", $"%{search}%");
                }

                if (minDaysOld.HasValue && minDaysOld.Value > 0)
                {
                    double cutoff = DateTimeOffset.UtcNow.AddDays(-minDaysOld.Value).ToUnixTimeSeconds();
                    whereClause += " AND modified_time <= $modCutoff";
                    cmd.Parameters.AddWithValue("$modCutoff", cutoff);
                }

                if (!string.IsNullOrWhiteSpace(extension))
                {
                    string ext = extension.Trim();
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    whereClause += " AND LOWER(extension) = $ext";
                    cmd.Parameters.AddWithValue("$ext", ext.ToLowerInvariant());
                }

                if (!string.IsNullOrWhiteSpace(locationPrefix))
                {
                    string cleanLoc = locationPrefix.Trim().TrimEnd('\\', '/');
                    if (cleanLoc.Length >= 2 && cleanLoc[1] == ':')
                    {
                        cleanLoc = char.ToUpperInvariant(cleanLoc[0]) + cleanLoc.Substring(1);
                    }
                    if (!string.IsNullOrEmpty(cleanLoc))
                    {
                        whereClause += " AND (path LIKE $loc OR path LIKE $locFwd)";
                        cmd.Parameters.AddWithValue("$loc", $"{cleanLoc}\\%");
                        cmd.Parameters.AddWithValue("$locFwd", $"{cleanLoc.Replace('\\', '/')}/%");
                    }
                }

                string validSort = sortBy.ToLowerInvariant() switch
                {
                    "name" => "name",
                    "path" => "path",
                    "modified_time" or "modified" or "date" => "modified_time",
                    "category" => "category",
                    "extension" or "ext" => "extension",
                    _ => "size"
                };

                string direction = sortDesc ? "DESC" : "ASC";

                cmd.CommandText = $@"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    {whereClause}
                    ORDER BY {validSort} {direction}
                    LIMIT $limit OFFSET $offset;
                ";

                cmd.Parameters.AddWithValue("$limit", limit);
                cmd.Parameters.AddWithValue("$offset", offset);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadRecord(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFilesPaged error: {ex.Message}");
            }

            return list;
        }
    }

    public long GetFilteredFileCount(
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? category = null,
        string? search = null,
        int? minDaysOld = null,
        string? extension = null,
        string? locationPrefix = null)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();

                string whereClause = "WHERE size >= $minSize";
                cmd.Parameters.AddWithValue("$minSize", minSize);

                if (maxSize < long.MaxValue && maxSize > 0)
                {
                    whereClause += " AND size <= $maxSize";
                    cmd.Parameters.AddWithValue("$maxSize", maxSize);
                }

                if (!string.IsNullOrWhiteSpace(category) && category != "All")
                {
                    whereClause += " AND category = $category";
                    cmd.Parameters.AddWithValue("$category", category);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClause += " AND (name LIKE $search OR path LIKE $search)";
                    cmd.Parameters.AddWithValue("$search", $"%{search}%");
                }

                if (minDaysOld.HasValue && minDaysOld.Value > 0)
                {
                    double cutoff = DateTimeOffset.UtcNow.AddDays(-minDaysOld.Value).ToUnixTimeSeconds();
                    whereClause += " AND modified_time <= $modCutoff";
                    cmd.Parameters.AddWithValue("$modCutoff", cutoff);
                }

                if (!string.IsNullOrWhiteSpace(extension))
                {
                    string ext = extension.Trim();
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    whereClause += " AND LOWER(extension) = $ext";
                    cmd.Parameters.AddWithValue("$ext", ext.ToLowerInvariant());
                }

                if (!string.IsNullOrWhiteSpace(locationPrefix))
                {
                    string cleanLoc = locationPrefix.Trim().TrimEnd('\\', '/');
                    if (cleanLoc.Length >= 2 && cleanLoc[1] == ':')
                    {
                        cleanLoc = char.ToUpperInvariant(cleanLoc[0]) + cleanLoc.Substring(1);
                    }
                    if (!string.IsNullOrEmpty(cleanLoc))
                    {
                        whereClause += " AND (path LIKE $loc OR path LIKE $locFwd)";
                        cmd.Parameters.AddWithValue("$loc", $"{cleanLoc}\\%");
                        cmd.Parameters.AddWithValue("$locFwd", $"{cleanLoc.Replace('\\', '/')}/%");
                    }
                }

                cmd.CommandText = $"SELECT COUNT(*) FROM files {whereClause};";
                object? result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt64(result) : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFilteredFileCount error: {ex.Message}");
                return 0;
            }
        }
    }

    private class FolderRollupNode
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Parent { get; set; } = string.Empty;
        public long DirectSize { get; set; }
        public long DirectFiles { get; set; }
        public long TotalSize { get; set; }
        public long TotalFiles { get; set; }
        public long SubfolderCount { get; set; }
    }

    private static string NormalizeDirPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
        {
            return trimmed + Path.DirectorySeparatorChar;
        }
        return trimmed;
    }

    private static string GetParentDirPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string normalized = NormalizeDirPath(path);
        if (normalized.Length <= 3 && normalized.EndsWith(":\\"))
        {
            return string.Empty;
        }
        string? parent = Path.GetDirectoryName(normalized);
        return parent != null ? NormalizeDirPath(parent) : string.Empty;
    }

    public void BuildDirectoryRollup(IReadOnlyList<string>? roots = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();

                using var fetchCmd = _connection!.CreateCommand();
                fetchCmd.CommandText = "SELECT parent, SUM(size) as d_size, COUNT(id) as d_count FROM files GROUP BY parent;";

                var nodes = new Dictionary<string, FolderRollupNode>(StringComparer.OrdinalIgnoreCase);

                using (var reader = fetchCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        string rawParent = reader.GetString(0);
                        long dSize = reader.GetInt64(1);
                        long dCount = reader.GetInt64(2);

                        string norm = NormalizeDirPath(rawParent);
                        if (string.IsNullOrEmpty(norm)) continue;

                        if (!nodes.TryGetValue(norm, out var node))
                        {
                            string name = Path.GetFileName(norm);
                            if (string.IsNullOrEmpty(name)) name = norm;
                            node = new FolderRollupNode
                            {
                                Path = norm,
                                Name = name,
                                Parent = GetParentDirPath(norm),
                                DirectSize = dSize,
                                DirectFiles = dCount
                            };
                            nodes[norm] = node;
                        }
                        else
                        {
                            node.DirectSize += dSize;
                            node.DirectFiles += dCount;
                        }

                        // Ensure all ancestor paths are in dictionary
                        string curr = norm;
                        while (true)
                        {
                            string p = GetParentDirPath(curr);
                            if (string.IsNullOrEmpty(p)) break;

                            if (!nodes.TryGetValue(p, out var pNode))
                            {
                                string ancestorName = Path.GetFileName(p);
                                if (string.IsNullOrEmpty(ancestorName)) ancestorName = p;
                                pNode = new FolderRollupNode
                                {
                                    Path = p,
                                    Name = ancestorName,
                                    Parent = GetParentDirPath(p),
                                    DirectSize = 0,
                                    DirectFiles = 0
                                };
                                nodes[p] = pNode;
                            }
                            curr = p;
                        }
                    }
                }

                if (cancellationToken.IsCancellationRequested) return;

                // Initialize totals with direct files
                foreach (var node in nodes.Values)
                {
                    node.TotalSize = node.DirectSize;
                    node.TotalFiles = node.DirectFiles;
                }

                // Bottom-up rollup: sort by directory depth descending (leaves first)
                var sorted = nodes.Values
                    .OrderByDescending(n => n.Path.Count(c => c == '\\' || c == '/'))
                    .ThenByDescending(n => n.Path.Length)
                    .ToList();

                foreach (var node in sorted)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    if (!string.IsNullOrEmpty(node.Parent) && nodes.TryGetValue(node.Parent, out var parentNode))
                    {
                        parentNode.TotalSize += node.TotalSize;
                        parentNode.TotalFiles += node.TotalFiles;
                        parentNode.SubfolderCount++;
                    }
                }

                if (cancellationToken.IsCancellationRequested) return;

                // Temporarily drop secondary indexes on directories to accelerate bulk insertion
                using (var dropIndexCmd = _connection.CreateCommand())
                {
                    dropIndexCmd.CommandText = @"
                        DROP INDEX IF EXISTS idx_directories_total_size;
                        DROP INDEX IF EXISTS idx_directories_parent;
                        DROP INDEX IF EXISTS idx_directories_name;
                    ";
                    dropIndexCmd.ExecuteNonQuery();
                }

                // Persist rolled up hierarchy to directories table
                using var tx = _connection.BeginTransaction();
                try
                {
                    using var clearCmd = _connection.CreateCommand();
                    clearCmd.Transaction = tx;
                    clearCmd.CommandText = "DELETE FROM directories;";
                    clearCmd.ExecuteNonQuery();

                    using var insertCmd = _connection.CreateCommand();
                    _activeCommand = insertCmd;
                    insertCmd.Transaction = tx;
                    insertCmd.CommandText = @"
                        INSERT INTO directories (path, name, parent, total_size, direct_size, total_files, direct_files, subfolder_count)
                        VALUES ($path, $name, $parent, $total_size, $direct_size, $total_files, $direct_files, $subfolder_count);
                    ";

                    var pPath = insertCmd.Parameters.Add("$path", SqliteType.Text);
                    var pName = insertCmd.Parameters.Add("$name", SqliteType.Text);
                    var pParent = insertCmd.Parameters.Add("$parent", SqliteType.Text);
                    var pTotalSize = insertCmd.Parameters.Add("$total_size", SqliteType.Integer);
                    var pDirectSize = insertCmd.Parameters.Add("$direct_size", SqliteType.Integer);
                    var pTotalFiles = insertCmd.Parameters.Add("$total_files", SqliteType.Integer);
                    var pDirectFiles = insertCmd.Parameters.Add("$direct_files", SqliteType.Integer);
                    var pSubfolders = insertCmd.Parameters.Add("$subfolder_count", SqliteType.Integer);

                    foreach (var node in nodes.Values)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            try { tx.Rollback(); } catch { }
                            return;
                        }

                        pPath.Value = node.Path;
                        pName.Value = node.Name;
                        pParent.Value = node.Parent;
                        pTotalSize.Value = node.TotalSize;
                        pDirectSize.Value = node.DirectSize;
                        pTotalFiles.Value = node.TotalFiles;
                        pDirectFiles.Value = node.DirectFiles;
                        pSubfolders.Value = node.SubfolderCount;
                        insertCmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
                finally
                {
                    _activeCommand = null;
                }

                // Recreate secondary indexes on directories
                using (var reindexCmd = _connection.CreateCommand())
                {
                    reindexCmd.CommandText = @"
                        CREATE INDEX IF NOT EXISTS idx_directories_total_size ON directories(total_size DESC);
                        CREATE INDEX IF NOT EXISTS idx_directories_parent ON directories(parent);
                        CREATE INDEX IF NOT EXISTS idx_directories_name ON directories(name);
                    ";
                    reindexCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BuildDirectoryRollup error: {ex.Message}");
            }
        }
    }

    public List<DirectoryRecord> GetLargestFolders(int limit = 100, bool excludeDriveRoots = true)
    {
        lock (_lock)
        {
            var list = new List<DirectoryRecord>();
            try
            {
                EnsureOpen();

                // If directories table is not yet populated, compute rollup automatically
                using (var countCmd = _connection!.CreateCommand())
                {
                    countCmd.CommandText = "SELECT COUNT(path) FROM directories;";
                    long count = Convert.ToInt64(countCmd.ExecuteScalar());
                    if (count == 0)
                    {
                        BuildDirectoryRollup();
                    }
                }

                string filterClause = excludeDriveRoots ? "WHERE NOT (length(path) <= 3 AND path LIKE '_:\\')" : "";

                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = $@"
                    SELECT path, name, parent, total_size, total_files, subfolder_count
                    FROM directories
                    {filterClause}
                    ORDER BY total_size DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$limit", limit);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string path = reader.GetString(0);
                        string name = reader.GetString(1);
                        string parent = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        long totalSize = reader.GetInt64(3);
                        long totalFiles = reader.GetInt64(4);
                        long subfolders = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);

                        list.Add(new DirectoryRecord
                        {
                            Path = path,
                            Name = name,
                            Parent = parent,
                            Size = totalSize,
                            FileCount = totalFiles,
                            SubfolderCount = subfolders
                        });
                    }
                }

                // If filtering drive roots resulted in empty list, fall back to returning all directories
                if (list.Count == 0 && excludeDriveRoots)
                {
                    return GetLargestFolders(limit, excludeDriveRoots: false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLargestFolders error: {ex.Message}");
            }

            return list;
        }
    }

    public List<DirectoryRecord> GetDirectoriesByNames(IEnumerable<string> names)
    {
        lock (_lock)
        {
            var list = new List<DirectoryRecord>();
            var nameList = names.ToList();
            if (nameList.Count == 0) return list;

            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                var paramNames = new List<string>();
                for (int i = 0; i < nameList.Count; i++)
                {
                    string p = $"$n{i}";
                    paramNames.Add(p);
                    cmd.Parameters.AddWithValue(p, nameList[i]);
                }

                cmd.CommandText = $@"
                    SELECT path, name, parent, total_size, total_files, subfolder_count
                    FROM directories
                    WHERE name IN ({string.Join(",", paramNames)})
                    ORDER BY total_size DESC;
                ";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new DirectoryRecord
                    {
                        Path = reader.GetString(0),
                        Name = reader.GetString(1),
                        Parent = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Size = reader.GetInt64(3),
                        FileCount = reader.GetInt64(4),
                        SubfolderCount = reader.IsDBNull(5) ? 0 : reader.GetInt64(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDirectoriesByNames error: {ex.Message}");
            }

            return list;
        }
    }

    public List<DirectoryRecord> GetSubdirectories(string parentPath, int limit = 50)
    {
        lock (_lock)
        {
            var list = new List<DirectoryRecord>();
            try
            {
                EnsureOpen();
                string cleanParent = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT path, name, parent, total_size, total_files, subfolder_count
                    FROM directories
                    WHERE parent = $parent OR parent = $parentClean
                    ORDER BY total_size DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$parent", parentPath);
                cmd.Parameters.AddWithValue("$parentClean", cleanParent);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new DirectoryRecord
                    {
                        Path = reader.GetString(0),
                        Name = reader.GetString(1),
                        Parent = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Size = reader.GetInt64(3),
                        FileCount = reader.GetInt64(4),
                        SubfolderCount = reader.IsDBNull(5) ? 0 : reader.GetInt64(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSubdirectories error: {ex.Message}");
            }

            return list;
        }
    }

    public bool RemoveFileFromIndex(string filePath)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "DELETE FROM files WHERE path = $path COLLATE NOCASE;";
                cmd.Parameters.AddWithValue("$path", filePath);
                int rows = cmd.ExecuteNonQuery();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveFileFromIndex error: {ex.Message}");
                return false;
            }
        }
    }

    public bool RemoveDirectoryFromIndex(string dirPath)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                string clean = NormalizeDirPath(dirPath);
                string prefix = clean + Path.DirectorySeparatorChar;
                string prefixUpper = clean + (char)(Path.DirectorySeparatorChar + 1);

                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM files 
                    WHERE path = $dir COLLATE NOCASE 
                       OR parent = $dir COLLATE NOCASE 
                       OR (path >= $prefix AND path < $prefixUpper);
                    DELETE FROM directories 
                    WHERE path = $dir COLLATE NOCASE 
                       OR (path >= $prefix AND path < $prefixUpper);
                ";
                cmd.Parameters.AddWithValue("$dir", clean);
                cmd.Parameters.AddWithValue("$prefix", prefix);
                cmd.Parameters.AddWithValue("$prefixUpper", prefixUpper);
                int rows = cmd.ExecuteNonQuery();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveDirectoryFromIndex error: {ex.Message}");
                return false;
            }
        }
    }

    public void StreamFilteredFiles(
        Action<FileRecord> onRecord,
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? category = null,
        string? search = null,
        string sortBy = "size",
        bool sortDesc = true,
        int? minDaysOld = null,
        string? extension = null,
        string? locationPrefix = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();

                string whereClause = "WHERE size >= $minSize";
                cmd.Parameters.AddWithValue("$minSize", minSize);

                if (maxSize < long.MaxValue && maxSize > 0)
                {
                    whereClause += " AND size <= $maxSize";
                    cmd.Parameters.AddWithValue("$maxSize", maxSize);
                }

                if (!string.IsNullOrWhiteSpace(category) && category != "All")
                {
                    whereClause += " AND category = $category";
                    cmd.Parameters.AddWithValue("$category", category);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClause += " AND (name LIKE $search OR path LIKE $search)";
                    cmd.Parameters.AddWithValue("$search", $"%{search}%");
                }

                if (minDaysOld.HasValue && minDaysOld.Value > 0)
                {
                    double cutoff = DateTimeOffset.UtcNow.AddDays(-minDaysOld.Value).ToUnixTimeSeconds();
                    whereClause += " AND modified_time <= $modCutoff";
                    cmd.Parameters.AddWithValue("$modCutoff", cutoff);
                }

                if (!string.IsNullOrWhiteSpace(extension))
                {
                    string ext = extension.Trim();
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    whereClause += " AND LOWER(extension) = $ext";
                    cmd.Parameters.AddWithValue("$ext", ext.ToLowerInvariant());
                }

                if (!string.IsNullOrWhiteSpace(locationPrefix))
                {
                    string cleanLoc = locationPrefix.Trim().TrimEnd('\\', '/');
                    if (cleanLoc.Length >= 2 && cleanLoc[1] == ':')
                    {
                        cleanLoc = char.ToUpperInvariant(cleanLoc[0]) + cleanLoc.Substring(1);
                    }
                    if (!string.IsNullOrEmpty(cleanLoc))
                    {
                        whereClause += " AND (path LIKE $loc OR path LIKE $locFwd)";
                        cmd.Parameters.AddWithValue("$loc", $"{cleanLoc}\\%");
                        cmd.Parameters.AddWithValue("$locFwd", $"{cleanLoc.Replace('\\', '/')}/%");
                    }
                }

                string validSort = sortBy.ToLowerInvariant() switch
                {
                    "name" => "name",
                    "path" => "path",
                    "modified_time" or "modified" or "date" => "modified_time",
                    "category" => "category",
                    "extension" or "ext" => "extension",
                    _ => "size"
                };

                string direction = sortDesc ? "DESC" : "ASC";

                cmd.CommandText = $@"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    {whereClause}
                    ORDER BY {validSort} {direction};
                ";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (ct.IsCancellationRequested) break;
                    onRecord(ReadRecord(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StreamFilteredFiles error: {ex.Message}");
            }
        }
    }

    public List<TreemapItem> GetTreemapItems(string? parentPath = null, int limit = 150)
    {
        lock (_lock)
        {
            var items = new List<TreemapItem>();
            try
            {
                EnsureOpen();

                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    // Root view: Return top largest folders
                    using var cmdFolders = _connection!.CreateCommand();
                    cmdFolders.CommandText = @"
                        SELECT parent, SUM(size) as total_size, COUNT(id) as file_count
                        FROM files
                        GROUP BY parent
                        ORDER BY total_size DESC
                        LIMIT $limit;
                    ";
                    cmdFolders.Parameters.AddWithValue("$limit", limit);

                    using var readerFolders = cmdFolders.ExecuteReader();
                    while (readerFolders.Read())
                    {
                        string path = readerFolders.GetString(0);
                        long size = readerFolders.GetInt64(1);
                        int count = readerFolders.GetInt32(2);

                        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (string.IsNullOrEmpty(name)) name = path;

                        items.Add(new TreemapItem
                        {
                            Path = path,
                            Name = name,
                            Size = size,
                            IsDirectory = true,
                            Category = "Folder",
                            ChildCount = count
                        });
                    }
                }
                else
                {
                    // 1. Direct child files in parentPath
                    using var cmdFiles = _connection!.CreateCommand();
                    cmdFiles.CommandText = @"
                        SELECT name, path, size, category, extension, modified_time
                        FROM files
                        WHERE parent = $parent
                        ORDER BY size DESC;
                    ";
                    cmdFiles.Parameters.AddWithValue("$parent", parentPath);

                    using var readerFiles = cmdFiles.ExecuteReader();
                    while (readerFiles.Read())
                    {
                        string name = readerFiles.GetString(0);
                        string path = readerFiles.GetString(1);
                        long size = readerFiles.GetInt64(2);
                        string category = readerFiles.IsDBNull(3) ? "Other" : readerFiles.GetString(3);
                        string ext = readerFiles.IsDBNull(4) ? string.Empty : readerFiles.GetString(4);
                        double mod = readerFiles.IsDBNull(5) ? 0 : readerFiles.GetDouble(5);

                        items.Add(new TreemapItem
                        {
                            Name = name,
                            Path = path,
                            Size = size,
                            IsDirectory = false,
                            Category = category,
                            Extension = ext,
                            LastModified = mod > 0 ? DateTime.UnixEpoch.AddSeconds(mod) : DateTime.MinValue
                        });
                    }

                    // 2. Immediate subdirectories under parentPath
                    string normalizedParent = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string prefix = normalizedParent + Path.DirectorySeparatorChar + "%";

                    using var cmdSubdirs = _connection!.CreateCommand();
                    cmdSubdirs.CommandText = @"
                        SELECT parent, SUM(size) as total_size, COUNT(id) as file_count
                        FROM files
                        WHERE parent LIKE $prefix AND parent != $parent
                        GROUP BY parent;
                    ";
                    cmdSubdirs.Parameters.AddWithValue("$prefix", prefix);
                    cmdSubdirs.Parameters.AddWithValue("$parent", parentPath);

                    var subDirAggregates = new Dictionary<string, (long Size, int Count)>(StringComparer.OrdinalIgnoreCase);

                    using var readerSubdirs = cmdSubdirs.ExecuteReader();
                    while (readerSubdirs.Read())
                    {
                        string p = readerSubdirs.GetString(0);
                        long s = readerSubdirs.GetInt64(1);
                        int c = readerSubdirs.GetInt32(2);

                        if (p.Length > normalizedParent.Length)
                        {
                            string relative = p.Substring(normalizedParent.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            int slashIndex = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                            string directChildName = slashIndex >= 0 ? relative.Substring(0, slashIndex) : relative;

                            if (!string.IsNullOrEmpty(directChildName))
                            {
                                string directChildFullPath = Path.Combine(normalizedParent, directChildName);
                                if (subDirAggregates.TryGetValue(directChildFullPath, out var current))
                                {
                                    subDirAggregates[directChildFullPath] = (current.Size + s, current.Count + c);
                                }
                                else
                                {
                                    subDirAggregates[directChildFullPath] = (s, c);
                                }
                            }
                        }
                    }

                    foreach (var kvp in subDirAggregates)
                    {
                        items.Add(new TreemapItem
                        {
                            Path = kvp.Key,
                            Name = Path.GetFileName(kvp.Key),
                            Size = kvp.Value.Size,
                            IsDirectory = true,
                            Category = "Folder",
                            ChildCount = kvp.Value.Count
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTreemapItems error: {ex.Message}");
            }

            return items.OrderByDescending(i => i.Size).Take(limit).ToList();
        }
    }

    public Dictionary<string, (long Count, long TotalSize)> GetCategoryBreakdown()
    {
        lock (_lock)
        {
            var dict = new Dictionary<string, (long Count, long TotalSize)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT category, COUNT(id), SUM(size)
                    FROM files
                    GROUP BY category
                    ORDER BY SUM(size) DESC;
                ";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string cat = reader.IsDBNull(0) ? FileCategory.Other : reader.GetString(0);
                    long count = reader.GetInt64(1);
                    long size = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    dict[cat] = (count, size);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCategoryBreakdown error: {ex.Message}");
            }

            return dict;
        }
    }

    public List<FileRecord> GetOldFiles(int daysOld = 180, int limit = 500)
    {
        lock (_lock)
        {
            var list = new List<FileRecord>();
            try
            {
                EnsureOpen();
                double cutoff = DateTimeOffset.Now.AddDays(-daysOld).ToUnixTimeSeconds();

                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    WHERE modified_time <= $cutoff AND size > 0
                    ORDER BY size DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$cutoff", cutoff);
                cmd.Parameters.AddWithValue("$limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadRecord(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetOldFiles error: {ex.Message}");
            }

            return list;
        }
    }

    public List<FileRecord> GetPhotoshopFiles(int limit = 500)
    {
        lock (_lock)
        {
            var list = new List<FileRecord>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    WHERE category = 'Photoshop' 
                       OR extension IN ('.psd', '.psb', '.pdd', '.abr', '.asl', '.atn', '.pat')
                       OR path LIKE '%Adobe%Photoshop%'
                    ORDER BY size DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadRecord(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPhotoshopFiles error: {ex.Message}");
            }

            return list;
        }
    }

    public List<PhotoshopItem> GetPhotoshopDetailedItems(int limit = 500)
    {
        lock (_lock)
        {
            var list = new List<PhotoshopItem>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    WHERE category = 'Photoshop' 
                       OR extension IN ('.psd', '.psb', '.pdd', '.abr', '.asl', '.atn', '.pat', '.grd')
                       OR path LIKE '%Adobe%Photoshop%'
                       OR path LIKE '%Photoshop%Temp%'
                       OR path LIKE '%Photoshop%Scratch%'
                       OR path LIKE '%Adobe%CameraRaw%'
                       OR path LIKE '%Adobe%Media Cache%'
                    ORDER BY size DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var file = ReadRecord(reader);
                    string ext = (file.Extension ?? string.Empty).ToLowerInvariant();
                    string path = file.Path;

                    PhotoshopItemType type;
                    if (ext == ".psd" || ext == ".psb" || ext == ".pdd")
                    {
                        type = PhotoshopItemType.UserDocument;
                    }
                    else if (path.Contains("Temp", StringComparison.OrdinalIgnoreCase) || 
                             path.Contains("Scratch", StringComparison.OrdinalIgnoreCase) || 
                             ext == ".tmp" || ext == ".dmp")
                    {
                        type = PhotoshopItemType.ScratchAndTemp;
                    }
                    else if (path.Contains("CameraRaw", StringComparison.OrdinalIgnoreCase) || 
                             path.Contains("Media Cache", StringComparison.OrdinalIgnoreCase) || 
                             path.Contains("AutoRecover", StringComparison.OrdinalIgnoreCase) ||
                             path.Contains("Cache", StringComparison.OrdinalIgnoreCase))
                    {
                        type = PhotoshopItemType.AdobeCache;
                    }
                    else if (ext == ".abr" || ext == ".asl" || ext == ".atn" || ext == ".pat" || ext == ".grd")
                    {
                        type = PhotoshopItemType.PresetOrAsset;
                    }
                    else
                    {
                        type = PhotoshopItemType.UserDocument;
                    }

                    list.Add(new PhotoshopItem
                    {
                        File = file,
                        ItemType = type
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPhotoshopDetailedItems error: {ex.Message}");
            }

            return list;
        }
    }

    public (long PsdCount, long PsbCount, long OtherCount, long TotalBytes, long PsdBytes, long PsbBytes) GetPhotoshopStats()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT 
                        SUM(CASE WHEN LOWER(extension) = '.psd' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN LOWER(extension) = '.psb' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN LOWER(extension) NOT IN ('.psd', '.psb') AND (category = 'Photoshop' OR path LIKE '%Adobe%Photoshop%') THEN 1 ELSE 0 END),
                        SUM(size),
                        SUM(CASE WHEN LOWER(extension) = '.psd' THEN size ELSE 0 END),
                        SUM(CASE WHEN LOWER(extension) = '.psb' THEN size ELSE 0 END)
                    FROM files
                    WHERE category = 'Photoshop' 
                       OR extension IN ('.psd', '.psb', '.pdd', '.abr', '.asl', '.atn', '.pat')
                       OR path LIKE '%Adobe%Photoshop%';
                ";

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long psdCount = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    long psbCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    long otherCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    long totalBytes = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                    long psdBytes = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                    long psbBytes = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);

                    return (psdCount, psbCount, otherCount, totalBytes, psdBytes, psbBytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPhotoshopStats error: {ex.Message}");
            }

            return (0, 0, 0, 0, 0, 0);
        }
    }

    public List<(long Size, int Count)> GetDuplicateSizeCandidates(int minCount = 2, long minSize = 1024, int limit = 500)
    {
        lock (_lock)
        {
            var candidates = new List<(long Size, int Count)>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();

                cmd.CommandText = @"
                    SELECT size, COUNT(id) as cnt
                    FROM files
                    WHERE size >= $minSize
                    GROUP BY size
                    HAVING cnt >= $minCount
                    ORDER BY (size * (cnt - 1)) DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$minSize", minSize);
                cmd.Parameters.AddWithValue("$minCount", minCount);
                cmd.Parameters.AddWithValue("$limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    candidates.Add((reader.GetInt64(0), reader.GetInt32(1)));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDuplicateSizeCandidates error: {ex.Message}");
            }

            return candidates;
        }
    }

    public List<FileRecord> GetFilesBySize(long size)
    {
        lock (_lock)
        {
            var list = new List<FileRecord>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();

                cmd.CommandText = @"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    WHERE size = $size
                    ORDER BY path ASC;
                ";
                cmd.Parameters.AddWithValue("$size", size);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadRecord(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFilesBySize error: {ex.Message}");
            }

            return list;
        }
    }

    public List<ScanHistoryItem> GetScanHistory(int limit = 50)
    {
        lock (_lock)
        {
            var list = new List<ScanHistoryItem>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, scan_start, scan_finish, roots, directories_visited, directories_processed,
                           directories_skipped, files_discovered, files_indexed, files_skipped,
                           logical_bytes_indexed, scan_status
                    FROM scan_metadata
                    ORDER BY id ASC;
                ";

                using var reader = cmd.ExecuteReader();
                long prevBytes = 0;
                bool isFirst = true;

                while (reader.Read())
                {
                    long startSec = reader.IsDBNull(1) ? 0 : (long)reader.GetDouble(1);
                    long finishSec = reader.IsDBNull(2) ? 0 : (long)reader.GetDouble(2);
                    long bytes = reader.IsDBNull(10) ? 0 : reader.GetInt64(10);

                    long growth = isFirst ? 0 : (bytes - prevBytes);
                    prevBytes = bytes;
                    isFirst = false;

                    list.Add(new ScanHistoryItem
                    {
                        Id = reader.GetInt64(0),
                        StartTime = DateTimeOffset.FromUnixTimeSeconds(startSec).UtcDateTime,
                        FinishTime = DateTimeOffset.FromUnixTimeSeconds(finishSec).UtcDateTime,
                        Roots = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        DirectoriesVisited = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                        DirectoriesProcessed = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                        DirectoriesSkipped = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                        FilesDiscovered = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                        FilesIndexed = reader.IsDBNull(8) ? 0 : reader.GetInt64(8),
                        FilesSkipped = reader.IsDBNull(9) ? 0 : reader.GetInt64(9),
                        LogicalBytesIndexed = bytes,
                        ScanStatus = reader.IsDBNull(11) ? "Completed" : reader.GetString(11),
                        GrowthBytes = growth
                    });
                }

                if (list.Count > limit)
                {
                    list = list.TakeLast(limit).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScanHistory error: {ex.Message}");
            }

            return list;
        }
    }

    public Dictionary<string, (long Size, long Count)> GetScanCategories(long scanId)
    {
        lock (_lock)
        {
            var dict = new Dictionary<string, (long Size, long Count)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT category, total_size, file_count FROM scan_history_categories WHERE scan_id = $id;";
                cmd.Parameters.AddWithValue("$id", scanId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string cat = reader.GetString(0);
                    long size = reader.GetInt64(1);
                    long count = reader.GetInt64(2);
                    dict[cat] = (size, count);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScanCategories error: {ex.Message}");
            }
            return dict;
        }
    }

    public ScanComparisonResult CompareScans(long olderScanId, long newerScanId)
    {
        var result = new ScanComparisonResult();
        var history = GetScanHistory(100);
        result.PreviousScan = history.FirstOrDefault(h => h.Id == olderScanId);
        result.CurrentScan = history.FirstOrDefault(h => h.Id == newerScanId);

        if (result.PreviousScan != null && result.CurrentScan != null)
        {
            result.DeltaBytes = result.CurrentScan.LogicalBytesIndexed - result.PreviousScan.LogicalBytesIndexed;

            var olderCats = GetScanCategories(olderScanId);
            var newerCats = GetScanCategories(newerScanId);

            var allCategories = olderCats.Keys.Union(newerCats.Keys).ToList();
            var diffs = new List<CategoryGrowthComparison>();

            foreach (var cat in allCategories)
            {
                olderCats.TryGetValue(cat, out var oldVal);
                newerCats.TryGetValue(cat, out var newVal);

                diffs.Add(new CategoryGrowthComparison
                {
                    Category = cat,
                    PreviousSizeBytes = oldVal.Size,
                    CurrentSizeBytes = newVal.Size,
                    DeltaFiles = newVal.Count - oldVal.Count
                });
            }

            foreach (var d in diffs.OrderByDescending(d => d.DeltaBytes))
            {
                result.WhatGrew.Add(d);
            }
        }

        return result;
    }

    public bool DeleteScanHistory(long? scanId = null)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                if (scanId.HasValue)
                {
                    cmd.CommandText = @"
                        DELETE FROM scan_history_categories WHERE scan_id = $id;
                        DELETE FROM scan_metadata WHERE id = $id;
                    ";
                    cmd.Parameters.AddWithValue("$id", scanId.Value);
                }
                else
                {
                    cmd.CommandText = @"
                        DELETE FROM scan_history_categories;
                        DELETE FROM scan_metadata;
                    ";
                }
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteScanHistory error: {ex.Message}");
                return false;
            }
        }
    }

    public (ulong JournalId, long NextUsn)? GetUsnCheckpoint(string driveLetter)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT journal_id, next_usn FROM drive_usn_checkpoints WHERE drive_letter = $drive;";
                cmd.Parameters.AddWithValue("$drive", driveLetter.ToUpperInvariant().TrimEnd('\\'));
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    ulong jId = (ulong)reader.GetInt64(0);
                    long nextUsn = reader.GetInt64(1);
                    return (jId, nextUsn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUsnCheckpoint error: {ex.Message}");
            }
            return null;
        }
    }

    public void SaveUsnCheckpoint(string driveLetter, ulong journalId, long nextUsn)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO drive_usn_checkpoints (drive_letter, journal_id, next_usn, last_scan_time)
                    VALUES ($drive, $jid, $usn, $time)
                    ON CONFLICT(drive_letter) DO UPDATE SET
                        journal_id = excluded.journal_id,
                        next_usn = excluded.next_usn,
                        last_scan_time = excluded.last_scan_time;
                ";
                cmd.Parameters.AddWithValue("$drive", driveLetter.ToUpperInvariant().TrimEnd('\\'));
                cmd.Parameters.AddWithValue("$jid", (long)journalId);
                cmd.Parameters.AddWithValue("$usn", nextUsn);
                cmd.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveUsnCheckpoint error: {ex.Message}");
            }
        }
    }

    public void DeleteUsnCheckpoint(string? driveLetter = null)
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                if (!string.IsNullOrEmpty(driveLetter))
                {
                    cmd.CommandText = "DELETE FROM drive_usn_checkpoints WHERE drive_letter = $drive;";
                    cmd.Parameters.AddWithValue("$drive", driveLetter.ToUpperInvariant().TrimEnd('\\'));
                }
                else
                {
                    cmd.CommandText = "DELETE FROM drive_usn_checkpoints;";
                }
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteUsnCheckpoint error: {ex.Message}");
            }
        }
    }

    public List<FileAgeBucket> GetFileAgeBreakdown()
    {
        lock (_lock)
        {
            var buckets = new List<FileAgeBucket>();
            try
            {
                EnsureOpen();
                double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                double d7 = now - (7 * 86400.0);
                double d30 = now - (30 * 86400.0);
                double d90 = now - (90 * 86400.0);
                double d365 = now - (365 * 86400.0);

                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT 
                        SUM(CASE WHEN modified_time >= $d7 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d7 THEN size ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d30 AND modified_time < $d7 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d30 AND modified_time < $d7 THEN size ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d90 AND modified_time < $d30 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d90 AND modified_time < $d30 THEN size ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d365 AND modified_time < $d90 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN modified_time >= $d365 AND modified_time < $d90 THEN size ELSE 0 END),
                        SUM(CASE WHEN modified_time < $d365 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN modified_time < $d365 THEN size ELSE 0 END)
                    FROM files;
                ";
                cmd.Parameters.AddWithValue("$d7", d7);
                cmd.Parameters.AddWithValue("$d30", d30);
                cmd.Parameters.AddWithValue("$d90", d90);
                cmd.Parameters.AddWithValue("$d365", d365);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long c7 = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    long s7 = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    long c30 = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    long s30 = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                    long c90 = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                    long s90 = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);
                    long c365 = reader.IsDBNull(6) ? 0 : reader.GetInt64(6);
                    long s365 = reader.IsDBNull(7) ? 0 : reader.GetInt64(7);
                    long cArch = reader.IsDBNull(8) ? 0 : reader.GetInt64(8);
                    long sArch = reader.IsDBNull(9) ? 0 : reader.GetInt64(9);

                    long totalBytes = s7 + s30 + s90 + s365 + sArch;
                    double totalDbl = Math.Max(1L, totalBytes);

                    buckets.Add(new FileAgeBucket { Name = "< 7 Days", Description = "Active & Recent", Count = c7, TotalBytes = s7, PercentageOfTotal = (double)s7 / totalDbl * 100.0 });
                    buckets.Add(new FileAgeBucket { Name = "7–30 Days", Description = "Past Month", Count = c30, TotalBytes = s30, PercentageOfTotal = (double)s30 / totalDbl * 100.0 });
                    buckets.Add(new FileAgeBucket { Name = "30–90 Days", Description = "Past Quarter", Count = c90, TotalBytes = s90, PercentageOfTotal = (double)s90 / totalDbl * 100.0 });
                    buckets.Add(new FileAgeBucket { Name = "90–365 Days", Description = "Past Year", Count = c365, TotalBytes = s365, PercentageOfTotal = (double)s365 / totalDbl * 100.0 });
                    buckets.Add(new FileAgeBucket { Name = "1 Year+", Description = "Archival / Dormant", Count = cArch, TotalBytes = sArch, PercentageOfTotal = (double)sArch / totalDbl * 100.0 });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFileAgeBreakdown error: {ex.Message}");
            }

            return buckets;
        }
    }

    public (int CandidateGroups, long CandidateFiles, long PotentialWastedBytes) GetDuplicateOverview()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*), SUM(cnt), SUM((cnt - 1) * size)
                    FROM (
                        SELECT size, COUNT(id) as cnt
                        FROM files
                        WHERE size >= 1024
                        GROUP BY size
                        HAVING cnt >= 2
                    );
                ";

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int groups = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    long files = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    long wasted = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    return (groups, files, wasted);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDuplicateOverview error: {ex.Message}");
            }

            return (0, 0, 0);
        }
    }

    public (long FileCount, long TotalBytes) GetAdobeCacheAndTempStats()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(id), SUM(size)
                    FROM files
                    WHERE path LIKE '%Photoshop%Temp%' 
                       OR path LIKE '%Adobe%AutoRecover%'
                       OR path LIKE '%Adobe%Media Cache%'
                       OR path LIKE '%Adobe%CameraRaw%'
                       OR path LIKE '%Photoshop%Scratch%'
                       OR (extension IN ('.tmp', '.dmp') AND (path LIKE '%Adobe%' OR path LIKE '%Photoshop%'));
                ";

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long count = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    long bytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    return (count, bytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAdobeCacheAndTempStats error: {ex.Message}");
            }

            return (0, 0);
        }
    }

    public (List<ReclaimableItem> Items, long TotalReclaimableBytes) GetReclaimableStorageBreakdown()
    {
        lock (_lock)
        {
            var list = new List<ReclaimableItem>();
            long total = 0;
            try
            {
                EnsureOpen();

                // 1. Old files (180+ days)
                double cutoff180 = DateTimeOffset.UtcNow.AddDays(-180).ToUnixTimeSeconds();
                using (var cmd = _connection!.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(id), SUM(size) FROM files WHERE modified_time <= $cutoff AND size > 0;";
                    cmd.Parameters.AddWithValue("$cutoff", cutoff180);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        long c = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                        long b = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                        list.Add(new ReclaimableItem
                        {
                            Category = "Dormant Files (180d+)",
                            Description = "Files not modified in over 6 months",
                            Bytes = b,
                            FileCount = c,
                            ActionHint = "Review in Old Files tab for archival"
                        });
                        total += b;
                    }
                }

                // 2. Potential Duplicates (Exact size matches)
                var dup = GetDuplicateOverview();
                if (dup.PotentialWastedBytes > 0)
                {
                    list.Add(new ReclaimableItem
                    {
                        Category = "Duplicate Redundancy",
                        Description = $"{dup.CandidateGroups:N0} collision groups sharing identical size",
                        Bytes = dup.PotentialWastedBytes,
                        FileCount = dup.CandidateFiles,
                        ActionHint = "Verify cryptographically in Duplicates tab"
                    });
                    total += dup.PotentialWastedBytes;
                }

                // 3. Adobe Cache & Scratch Files
                var adobe = GetAdobeCacheAndTempStats();
                if (adobe.TotalBytes > 0)
                {
                    list.Add(new ReclaimableItem
                    {
                        Category = "Adobe & Photoshop Cache / Temp",
                        Description = "AutoRecover snapshots, media cache, and scratch files",
                        Bytes = adobe.TotalBytes,
                        FileCount = adobe.FileCount,
                        ActionHint = "Inspect Photoshop Intelligence tab"
                    });
                    total += adobe.TotalBytes;
                }

                // 4. General Temporary & Log files
                using (var cmd = _connection!.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COUNT(id), SUM(size)
                        FROM files
                        WHERE extension IN ('.tmp', '.log', '.bak', '.old', '.dmp', '.chk', '.wbk')
                           OR name LIKE 'temp_%'
                           OR name LIKE 'cache_%';
                    ";
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        long c = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                        long b = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                        if (b > 0)
                        {
                            list.Add(new ReclaimableItem
                            {
                                Category = "Temporary, Log & Backup Files",
                                Description = "Transient files with .tmp, .log, .bak, .old extensions",
                                Bytes = b,
                                FileCount = c,
                                ActionHint = "Filter in Largest Files tab"
                            });
                            total += b;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetReclaimableStorageBreakdown error: {ex.Message}");
            }

            return (list, total);
        }
    }

    public List<FileRecord> GetLargestPhotoshopFiles(int limit = 5)
    {
        lock (_lock)
        {
            var list = new List<FileRecord>();
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, path, name, parent, size, modified_time, created_time, extension, category, accessible
                    FROM files
                    WHERE LOWER(extension) IN ('.psd', '.psb', '.pdd')
                    ORDER BY size DESC
                    LIMIT $limit;
                ";
                cmd.Parameters.AddWithValue("$limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadRecord(reader));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLargestPhotoshopFiles error: {ex.Message}");
            }

            return list;
        }
    }

    public (long TotalFiles, long TotalBytes) GetTotalIndexedStorage()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT COUNT(id), SUM(size) FROM files;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long count = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    long bytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    return (count, bytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTotalIndexedStorage error: {ex.Message}");
            }

            return (0, 0);
        }
    }

    public bool CheckIntegrity()
    {
        lock (_lock)
        {
            try
            {
                EnsureOpen();
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                object? res = cmd.ExecuteScalar();
                return string.Equals(res?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckIntegrity error: {ex.Message}");
                return false;
            }
        }
    }

    private static FileRecord ReadRecord(SqliteDataReader reader)
    {
        return new FileRecord
        {
            Id = reader.GetInt64(0),
            Path = reader.GetString(1),
            Name = reader.GetString(2),
            Parent = reader.GetString(3),
            Size = reader.GetInt64(4),
            ModifiedTime = reader.GetDouble(5),
            CreatedTime = reader.GetDouble(6),
            Extension = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            Category = reader.IsDBNull(8) ? FileCategory.Other : reader.GetString(8),
            Accessible = reader.GetInt32(9)
        };
    }

    private void EnsureOpen()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            try
            {
                _connection.Open();
            }
            catch
            {
                try { _connection.Dispose(); } catch { }
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch { }
            finally
            {
                _connection = null;
            }
        }
        GC.SuppressFinalize(this);
    }
}
