using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class FileRecord : ObservableObject
{
    private bool _isSelected;

    public long Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public long Size { get; set; }
    public double ModifiedTime { get; set; }
    public double CreatedTime { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string Category { get; set; } = FileCategory.Other;
    public int Accessible { get; set; } = 1;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public DateTime ModifiedDate => DateTimeOffset.FromUnixTimeSeconds((long)ModifiedTime).LocalDateTime;
    public DateTime CreatedDate => DateTimeOffset.FromUnixTimeSeconds((long)CreatedTime).LocalDateTime;
    public string FormattedSize => SizeFormatter.Format(Size);
}
