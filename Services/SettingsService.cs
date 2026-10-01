using System.IO;
using System.Text.Json;
using DiskScope.Models;

namespace DiskScope.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private DiskScopeSettings _cachedSettings;

    public SettingsService(string? customSettingsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customSettingsPath))
        {
            _settingsFilePath = customSettingsPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDir = Path.Combine(appData, "DiskScope");
            Directory.CreateDirectory(appDir);
            _settingsFilePath = Path.Combine(appDir, "settings.json");
        }

        _cachedSettings = LoadSettings();
    }

    public DiskScopeSettings CurrentSettings
    {
        get
        {
            lock (_lock)
            {
                return _cachedSettings;
            }
        }
    }

    public DiskScopeSettings LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<DiskScopeSettings>(json);
                    if (loaded != null)
                    {
                        EnsureDefaultExclusions(loaded);
                        _cachedSettings = loaded;
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall back to defaults on parse/read error
            }

            var defaults = CreateDefaultSettings();
            _cachedSettings = defaults;
            return defaults;
        }
    }

    public void SaveSettings(DiskScopeSettings settings)
    {
        lock (_lock)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
                _cachedSettings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }

    public DiskScopeSettings ResetToDefaults()
    {
        lock (_lock)
        {
            var defaults = CreateDefaultSettings();
            SaveSettings(defaults);
            return defaults;
        }
    }

    public bool IsPathExcluded(string path, DiskScopeSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        settings ??= CurrentSettings;
        if (settings.ExcludedPaths.Count == 0) return false;

        try
        {
            string normalizedTarget = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var excluded in settings.ExcludedPaths)
            {
                if (string.IsNullOrWhiteSpace(excluded)) continue;

                string normalizedExclusion = Path.GetFullPath(excluded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Exact match
                if (string.Equals(normalizedTarget, normalizedExclusion, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Descendant check (e.g. C:\Windows\System32 is under C:\Windows)
                if (normalizedTarget.StartsWith(normalizedExclusion + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // In case of invalid path syntax
        }

        return false;
    }

    public bool AddExclusion(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        lock (_lock)
        {
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!_cachedSettings.ExcludedPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                _cachedSettings.ExcludedPaths.Add(normalized);
                SaveSettings(_cachedSettings);
                return true;
            }
            return false;
        }
    }

    public bool RemoveExclusion(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        lock (_lock)
        {
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int removed = _cachedSettings.ExcludedPaths.RemoveAll(p => string.Equals(p.TrimEnd('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SaveSettings(_cachedSettings);
                return true;
            }
            return false;
        }
    }

    private static DiskScopeSettings CreateDefaultSettings()
    {
        var settings = new DiskScopeSettings
        {
            WorkerCount = 0, // Automatic
            FollowJunctions = false,
            IncludeHiddenFiles = true,
            IncludeSystemFiles = true,
            DefaultToRecycleBin = true,
            RequireConfirmationForRecycleBin = true,
            RequireConfirmationForPermanent = true,
            RetentionDays = 90,
            MaxScanSessionsToKeep = 50,
            LogLevel = "Information"
        };

        EnsureDefaultExclusions(settings);
        return settings;
    }

    private static void EnsureDefaultExclusions(DiskScopeSettings settings)
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (!string.IsNullOrEmpty(winDir) && !settings.ExcludedPaths.Contains(winDir, StringComparer.OrdinalIgnoreCase))
        {
            settings.ExcludedPaths.Add(winDir);
        }

        string recycleBin = @"C:\$Recycle.Bin";
        if (!settings.ExcludedPaths.Contains(recycleBin, StringComparer.OrdinalIgnoreCase))
        {
            settings.ExcludedPaths.Add(recycleBin);
        }
    }
}
