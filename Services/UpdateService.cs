using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using DiskScope.Models;

namespace DiskScope.Services;

public interface IUpdateService
{
    Version CurrentVersion { get; }
    Task<UpdateCheckResponse> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<bool> DownloadAndVerifyUpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    string? GetStagedExecutablePath(UpdateInfo update);
    bool ApplyUpdateAndRestart(UpdateInfo update);
}

public class UpdateService : IUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/12valor/ArborGraph/releases/latest";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly string _updatesDirectory;
    private readonly Version _currentVersion;

    public Version CurrentVersion => _currentVersion;

    public UpdateService(Version? currentVersion = null, string? customUpdatesDir = null)
    {
        _currentVersion = currentVersion ?? typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);

        _updatesDirectory = customUpdatesDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArborGraph",
            "updates");

        try
        {
            Directory.CreateDirectory(_updatesDirectory);
        }
        catch { }
    }

    public async Task<UpdateCheckResponse> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ArborGraph", _currentVersion.ToString(3)));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) &&
                    remainingValues.FirstOrDefault() == "0")
                {
                    return new UpdateCheckResponse
                    {
                        Result = UpdateCheckResult.RateLimited,
                        Message = "GitHub API rate limit exceeded. Please try again later."
                    };
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResponse
                {
                    Result = UpdateCheckResult.NetworkError,
                    Message = $"GitHub API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
                };
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            // Drafts and prereleases are skipped by /releases/latest, but verify explicitly
            bool isDraft = root.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean();
            bool isPrerelease = root.TryGetProperty("prerelease", out var preProp) && preProp.GetBoolean();
            if (isDraft || isPrerelease)
            {
                return new UpdateCheckResponse
                {
                    Result = UpdateCheckResult.UpToDate,
                    Message = "Latest release is marked as draft or prerelease. Ignoring."
                };
            }

            string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            string releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
            string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
            DateTimeOffset publishedAt = root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTimeOffset(out var dt) ? dt : DateTimeOffset.UtcNow;

            string cleanVersion = tagName.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(cleanVersion, out var remoteVersion))
            {
                return new UpdateCheckResponse
                {
                    Result = UpdateCheckResult.InvalidMetadata,
                    Message = $"Could not parse remote version from tag '{tagName}'."
                };
            }

            // Asset discovery: Find ArborGraph.exe and SHA256SUMS.txt
            string exeDownloadUrl = "";
            long exeSize = 0;
            string checksumUrl = "";

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                    string downloadUrl = asset.TryGetProperty("browser_download_url", out var aUrl) ? aUrl.GetString() ?? "" : "";
                    long size = asset.TryGetProperty("size", out var aSize) ? aSize.GetInt64() : 0;

                    if (string.Equals(assetName, "ArborGraph.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        exeDownloadUrl = downloadUrl;
                        exeSize = size;
                    }
                    else if (string.Equals(assetName, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        checksumUrl = downloadUrl;
                    }
                }
            }

            if (string.IsNullOrEmpty(exeDownloadUrl))
            {
                return new UpdateCheckResponse
                {
                    Result = UpdateCheckResult.NoAssetAvailable,
                    Message = $"Release {tagName} exists but does not contain an ArborGraph.exe binary asset."
                };
            }

            // Compare versions (Major, Minor, Build)
            bool isNewer = CompareVersions(remoteVersion, _currentVersion) > 0;

            var updateInfo = new UpdateInfo
            {
                TargetVersion = remoteVersion,
                TagName = tagName,
                ReleaseName = releaseName,
                ReleaseNotes = CleanReleaseNotes(releaseNotes),
                DownloadUrl = exeDownloadUrl,
                DownloadSizeBytes = exeSize,
                ChecksumManifestUrl = checksumUrl,
                PublishedAt = publishedAt,
                IsNewer = isNewer
            };

            return new UpdateCheckResponse
            {
                Result = isNewer ? UpdateCheckResult.UpdateAvailable : UpdateCheckResult.UpToDate,
                Update = updateInfo,
                Message = isNewer ? $"Version {cleanVersion} is available." : "ArborGraph is up to date."
            };
        }
        catch (HttpRequestException ex)
        {
            return new UpdateCheckResponse
            {
                Result = UpdateCheckResult.NetworkError,
                Message = $"Network error checking for updates: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResponse
            {
                Result = UpdateCheckResult.NetworkError,
                Message = "Update check timed out."
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResponse
            {
                Result = UpdateCheckResult.InvalidMetadata,
                Message = $"Error checking updates: {ex.Message}"
            };
        }
    }

    public async Task<bool> DownloadAndVerifyUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string downloadFilePath = Path.Combine(_updatesDirectory, $"ArborGraph-v{update.TargetVersion}.exe.download");
        string stagedFilePath = Path.Combine(_updatesDirectory, $"ArborGraph-v{update.TargetVersion}.exe.staged");

        try
        {
            // 1. Download SHA256SUMS.txt manifest if available to obtain expected hash
            string? expectedHash = update.ExpectedSha256;
            if (string.IsNullOrEmpty(expectedHash) && !string.IsNullOrEmpty(update.ChecksumManifestUrl))
            {
                expectedHash = await FetchExpectedHashAsync(update.ChecksumManifestUrl, cancellationToken);
                update.ExpectedSha256 = expectedHash;
            }

            // 2. Download executable binary
            using (var response = await HttpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? update.DownloadSizeBytes;
                long totalRead = 0;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(downloadFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double pct = (double)totalRead / totalBytes * 100.0;
                        progress?.Report(Math.Min(100.0, pct));
                    }
                }
            }

            // 3. Cryptographic integrity verification
            if (!string.IsNullOrEmpty(expectedHash))
            {
                string computedHash = await ComputeSha256Async(downloadFilePath, cancellationToken);
                if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(downloadFilePath); } catch { }
                    throw new InvalidDataException($"SHA-256 integrity mismatch. Expected: {expectedHash}, Computed: {computedHash}");
                }
            }

            // 4. Promote to staged executable
            if (File.Exists(stagedFilePath))
            {
                File.Delete(stagedFilePath);
            }
            File.Move(downloadFilePath, stagedFilePath);

            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(downloadFilePath))
                {
                    File.Delete(downloadFilePath);
                }
            }
            catch { }
            throw;
        }
    }

    public string? GetStagedExecutablePath(UpdateInfo update)
    {
        string stagedFilePath = Path.Combine(_updatesDirectory, $"ArborGraph-v{update.TargetVersion}.exe.staged");
        return File.Exists(stagedFilePath) ? stagedFilePath : null;
    }

    public bool ApplyUpdateAndRestart(UpdateInfo update)
    {
        string? stagedPath = GetStagedExecutablePath(update);
        if (string.IsNullOrEmpty(stagedPath) || !File.Exists(stagedPath))
        {
            return false;
        }

        string? currentProcessPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentProcessPath) || !File.Exists(currentProcessPath))
        {
            currentProcessPath = Path.Combine(AppContext.BaseDirectory, "ArborGraph.exe");
        }

        if (!File.Exists(currentProcessPath))
        {
            return false;
        }

        int currentPid = Environment.ProcessId;

        // Check if write permissions exist on the target directory
        string? targetDir = Path.GetDirectoryName(currentProcessPath);
        bool requiresElevation = false;
        if (!string.IsNullOrEmpty(targetDir))
        {
            try
            {
                string testProbeFile = Path.Combine(targetDir, $".perm_probe_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testProbeFile, "ok");
                File.Delete(testProbeFile);
            }
            catch (UnauthorizedAccessException)
            {
                requiresElevation = true;
            }
            catch
            {
                requiresElevation = true;
            }
        }

        // PowerShell detached replacement script
        string psScript = 
$@"param([int]$waitPid, [string]$source, [string]$target)
try {{
    Wait-Process -Id $waitPid -Timeout 30 -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
    Copy-Item -LiteralPath $target -Destination ""$target.bak"" -Force
    Move-Item -LiteralPath $source -Destination $target -Force
    Remove-Item -LiteralPath ""$target.bak"" -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath $target
}} catch {{
    if (Test-Path ""$target.bak"") {{
        Move-Item -LiteralPath ""$target.bak"" -Destination $target -Force
    }}
    Start-Process -FilePath $target
}}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{EscapeForPowerShellCommand(psScript, currentPid, stagedPath, currentProcessPath)}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (requiresElevation)
        {
            startInfo.Verb = "runas";
        }

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to spawn updater process: {ex.Message}");
            return false;
        }

        // Terminate current application cleanly
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Application.Current.Shutdown(0);
        });

        return true;
    }

    private static string EscapeForPowerShellCommand(string script, int pid, string source, string target)
    {
        // Replace parameters inline in script to avoid shell quotation escaping issues
        string inlineScript = script
            .Replace("$waitPid", pid.ToString())
            .Replace("$source", $"'{source.Replace("'", "''")}'")
            .Replace("$target", $"'{target.Replace("'", "''")}'");

        // Convert to single line
        return inlineScript.Replace("\r\n", "; ").Replace("\n", "; ");
    }

    private static async Task<string?> FetchExpectedHashAsync(string manifestUrl, CancellationToken ct)
    {
        try
        {
            string manifestText = await HttpClient.GetStringAsync(manifestUrl, ct);
            using var reader = new StringReader(manifestText);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                line = line.Trim();
                if (line.Contains("ArborGraph.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split([' ', '\t', '*'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (part.Length == 64 && IsHexString(part))
                        {
                            return part.ToUpperInvariant();
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var sha256 = SHA256.Create();
        byte[] hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToUpperInvariant();
    }

    public static int CompareVersions(Version a, Version b)
    {
        int aMajor = Math.Max(0, a.Major);
        int bMajor = Math.Max(0, b.Major);
        if (aMajor != bMajor) return aMajor.CompareTo(bMajor);

        int aMinor = Math.Max(0, a.Minor);
        int bMinor = Math.Max(0, b.Minor);
        if (aMinor != bMinor) return aMinor.CompareTo(bMinor);

        int aBuild = Math.Max(0, a.Build);
        int bBuild = Math.Max(0, b.Build);
        if (aBuild != bBuild) return aBuild.CompareTo(bBuild);

        int aRevision = Math.Max(0, a.Revision);
        int bRevision = Math.Max(0, b.Revision);
        return aRevision.CompareTo(bRevision);
    }

    private static bool IsHexString(string s)
    {
        return s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    private static string CleanReleaseNotes(string rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes)) return "No release notes provided.";
        return rawNotes.Trim();
    }
}
