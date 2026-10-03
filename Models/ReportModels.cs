namespace DiskScope.Models;

public class CategoryDistributionItem
{
    public string Category { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public double PercentageOfTotal { get; set; }

    public string FormattedSize => FormatSize(TotalBytes);

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {suffixes[order]}";
    }
}

public class AuditReport
{
    public string Product { get; set; } = "ArborGraph";
    public string Generator { get; set; } = "ArborGraph Filesystem Analytics & Visualization";
    public string Version { get; set; } = "1.0.0";
    public string GeneratedAtUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    public string TargetRoots { get; set; } = string.Empty;
    public long TotalFilesIndexed { get; set; }
    public long TotalLogicalBytes { get; set; }
    public string FormattedTotalSize => FormatSize(TotalLogicalBytes);
    public string ScanDuration { get; set; } = string.Empty;
    public double FilesPerSecond { get; set; }

    public List<CategoryDistributionItem> Categories { get; set; } = [];
    public List<FileRecord> TopLargestFiles { get; set; } = [];
    public List<DuplicateGroup> DuplicateGroups { get; set; } = [];
    public long TotalDuplicateWastedBytes { get; set; }
    public string FormattedDuplicateWasted => FormatSize(TotalDuplicateWastedBytes);

    public List<JunkTarget> JunkTargets { get; set; } = [];
    public long TotalCleanableJunkBytes { get; set; }
    public string FormattedCleanableJunk => FormatSize(TotalCleanableJunkBytes);

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {suffixes[order]}";
    }
}
