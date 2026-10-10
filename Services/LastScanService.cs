using System;
using System.IO;
using System.Text.Json;
using DiskScope.Models;

namespace DiskScope.Services;

public class LastScanService
{
    private readonly string _metadataFilePath;
    private readonly string _backupDbPath;
    private readonly object _lock = new();

    public LastScanService(string? customAppDataDir = null)
    {
        string dir;
        if (!string.IsNullOrWhiteSpace(customAppDataDir))
        {
            dir = customAppDataDir;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dir = Path.Combine(appData, "ArborGraph");
        }

        Directory.CreateDirectory(dir);
        _metadataFilePath = Path.Combine(dir, "last_scan.json");
        _backupDbPath = Path.Combine(dir, "last_scan.db");
    }

    public string MetadataFilePath => _metadataFilePath;
    public string BackupDbPath => _backupDbPath;

    public bool HasLastScan()
    {
        lock (_lock)
        {
            try
            {
                return File.Exists(_metadataFilePath);
            }
            catch
            {
                return false;
            }
        }
    }

    public LastScanInfo? LoadLastScan()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_metadataFilePath)) return null;

                string json = File.ReadAllText(_metadataFilePath);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var info = JsonSerializer.Deserialize<LastScanInfo>(json, options);
                if (info == null || info.FilesIndexed < 0) return null;

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LastScanService.LoadLastScan error: {ex.Message}");
                return null;
            }
        }
    }

    public bool SaveLastScan(LastScanInfo info)
    {
        if (info == null) return false;

        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(info, options);
                string tempFile = _metadataFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _metadataFilePath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LastScanService.SaveLastScan error: {ex.Message}");
                return false;
            }
        }
    }

    public void ClearLastScan()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_metadataFilePath)) File.Delete(_metadataFilePath);
            }
            catch { }

            try
            {
                if (File.Exists(_backupDbPath)) File.Delete(_backupDbPath);
            }
            catch { }
        }
    }
}
