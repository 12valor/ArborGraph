namespace DiskScope.Models;

public class UpdateInfo
{
    public Version TargetVersion { get; set; } = new(1, 0, 0);
    public string TagName { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long DownloadSizeBytes { get; set; }
    public string ChecksumManifestUrl { get; set; } = string.Empty;
    public string? ExpectedSha256 { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public bool IsNewer { get; set; }
}

public enum UpdateCheckResult
{
    UpToDate,
    UpdateAvailable,
    NetworkError,
    RateLimited,
    InvalidMetadata,
    NoAssetAvailable
}

public class UpdateCheckResponse
{
    public UpdateCheckResult Result { get; set; }
    public UpdateInfo? Update { get; set; }
    public string Message { get; set; } = string.Empty;
}
