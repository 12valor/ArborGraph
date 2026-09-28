using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class DuplicateGroup
{
    public long ExactSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public List<FileRecord> Files { get; set; } = [];

    public int FileCount => Files.Count;
    public long WastedBytes => Math.Max(0, (FileCount - 1) * ExactSize);

    public string FormattedSize => SizeFormatter.Format(ExactSize);
    public string FormattedWasted => SizeFormatter.Format(WastedBytes);
    public string ShortHash => Sha256.Length > 12 ? Sha256[..12] : Sha256;
}
