using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class DiskDriveInfo : ObservableObject
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public string FileSystem { get; set; } = string.Empty;

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedPercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100.0 : 0.0;

    public string FormattedTotal => SizeFormatter.Format(TotalBytes);
    public string FormattedUsed => SizeFormatter.Format(UsedBytes);
    public string FormattedFree => SizeFormatter.Format(FreeBytes);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayName => string.IsNullOrWhiteSpace(VolumeLabel)
        ? $"{Name} [{FileSystem}]"
        : $"{Name} ({VolumeLabel}) [{FileSystem}]";
}
