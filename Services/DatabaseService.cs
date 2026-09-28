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
            string folder = Path.Combine(appData, "DiskScopePro");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "scan_index.db");
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
                CREATE INDEX IF NOT EXISTS idx_files_modified ON files(modified_time DESC);
                CREATE INDEX IF NOT EXISTS idx_files_extension ON files(extension);
                CREATE INDEX IF NOT EXISTS idx_files_category ON files(category);
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
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public void ClearIndex()
    {
        lock (_lock)
        {
            EnsureOpen();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM files;";
            cmd.ExecuteNonQuery();
        }
    }

    public void InsertBatch(IReadOnlyList<FileRecord> records)
    {
        if (records.Count == 0) return;

        lock (_lock)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            using var cmd = _connection.CreateCommand();
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
    }

    public void SaveScanMetadata(ScanStats stats, string roots, DateTime start, DateTime finish)
    {
        lock (_lock)
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
        bool sortDesc = true)
    {
        lock (_lock)
        {
            EnsureOpen();
            var list = new List<FileRecord>();
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

            string validSort = sortBy.ToLowerInvariant() switch
            {
                "name" => "name",
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

            return list;
        }
    }

    public long GetFilteredFileCount(
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? category = null,
        string? search = null)
    {
        lock (_lock)
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

            cmd.CommandText = $"SELECT COUNT(*) FROM files {whereClause};";
            object? result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt64(result) : 0;
        }
    }

    public List<DirectoryRecord> GetLargestFolders(int limit = 100)
    {
        lock (_lock)
        {
            EnsureOpen();
            var list = new List<DirectoryRecord>();
            using var cmd = _connection!.CreateCommand();

            cmd.CommandText = @"
                SELECT parent, SUM(size) as total_size, COUNT(id) as file_count
                FROM files
                GROUP BY parent
                ORDER BY total_size DESC
                LIMIT $limit;
            ";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string path = reader.GetString(0);
                long size = reader.GetInt64(1);
                long count = reader.GetInt64(2);

                string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(name)) name = path;

                list.Add(new DirectoryRecord
                {
                    Path = path,
                    Name = name,
                    Parent = Path.GetDirectoryName(path) ?? string.Empty,
                    Size = size,
                    FileCount = count
                });
            }

            return list;
        }
    }

    public Dictionary<string, (long Count, long TotalSize)> GetCategoryBreakdown()
    {
        lock (_lock)
        {
            EnsureOpen();
            var dict = new Dictionary<string, (long Count, long TotalSize)>(StringComparer.OrdinalIgnoreCase);

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

            return dict;
        }
    }

    public List<FileRecord> GetOldFiles(int daysOld = 180, int limit = 500)
    {
        lock (_lock)
        {
            EnsureOpen();
            var list = new List<FileRecord>();
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

            return list;
        }
    }

    public List<FileRecord> GetPhotoshopFiles(int limit = 500)
    {
        lock (_lock)
        {
            EnsureOpen();
            var list = new List<FileRecord>();

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

            return list;
        }
    }

    public (long PsdCount, long PsbCount, long OtherCount, long TotalBytes, long PsdBytes, long PsbBytes) GetPhotoshopStats()
    {
        lock (_lock)
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

            return (0, 0, 0, 0, 0, 0);
        }
    }

    public List<(long Size, int Count)> GetDuplicateSizeCandidates(int minCount = 2, long minSize = 1024, int limit = 500)
    {
        lock (_lock)
        {
            EnsureOpen();
            var candidates = new List<(long Size, int Count)>();
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

            return candidates;
        }
    }

    public List<FileRecord> GetFilesBySize(long size)
    {
        lock (_lock)
        {
            EnsureOpen();
            var list = new List<FileRecord>();
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

            return list;
        }
    }

    public bool CheckIntegrity()
    {
        lock (_lock)
        {
            EnsureOpen();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            object? res = cmd.ExecuteScalar();
            return string.Equals(res?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
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
            _connection.Open();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
        GC.SuppressFinalize(this);
    }
}
