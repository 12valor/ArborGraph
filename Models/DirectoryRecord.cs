using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class DirectoryRecord
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public long Size { get; set; }
    public long FileCount { get; set; }
    public long SubfolderCount { get; set; }
    public double PercentageOfTotal { get; set; }

    public string FormattedSize => SizeFormatter.Format(Size);
    public string FormattedFileCount => SizeFormatter.FormatCount(FileCount);
}
