using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public enum TreemapColorMode
{
    Category,
    Heatmap,
    Depth
}

public static class TreemapVisualHelper
{
    public static string GetCategoryIcon(string? category, bool isDirectory)
    {
        if (isDirectory) return "📁";
        return (category?.Trim().ToLowerInvariant()) switch
        {
            "video" or "videos" => "🎬",
            "audio" => "🎵",
            "images" or "image" => "🖼️",
            "photoshop" => "🎨",
            "code" or "developer" => "💻",
            "executables" or "executable" => "⚡",
            "archives" or "archive" or "compressed" => "📦",
            "documents" or "document" => "📄",
            "database" => "🗄️",
            "system" => "⚙️",
            _ => "📄"
        };
    }

    public static string GetCategoryHexColor(string? category, bool isDirectory)
    {
        if (isDirectory) return "#0284C7";
        return (category?.Trim().ToLowerInvariant()) switch
        {
            "video" or "videos" => "#8B5CF6",
            "audio" => "#EC4899",
            "images" or "image" => "#F97316",
            "photoshop" => "#06B6D4",
            "code" or "developer" => "#10B981",
            "executables" or "executable" => "#EF4444",
            "archives" or "archive" or "compressed" => "#F59E0B",
            "documents" or "document" => "#3B82F6",
            "database" => "#6366F1",
            "system" => "#64748B",
            _ => "#64748B"
        };
    }
}

public class TreemapItem
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string FormattedSize => SizeFormatter.Format(Size);
    public bool IsDirectory { get; set; }
    public string Category { get; set; } = "Other";
    public string Extension { get; set; } = string.Empty;
    public int ChildCount { get; set; }
    public double Percentage { get; set; }
    public DateTime LastModified { get; set; }

    public string CategoryIcon => TreemapVisualHelper.GetCategoryIcon(Category, IsDirectory);
    public string CategoryBadgeColor => TreemapVisualHelper.GetCategoryHexColor(Category, IsDirectory);
    public string FormattedPercentage => $"{Percentage:F1}%";
}

public class TreemapRect : INotifyPropertyChanged
{
    private bool _isSelected;

    public TreemapItem Item { get; set; } = new();
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public Brush FillBrush { get; set; } = Brushes.Gray;
    public Brush BorderBrush { get; set; } = Brushes.Transparent;

    public bool ShowLabel => Width >= 42 && Height >= 20;
    public bool ShowIcon => Width >= 52 && Height >= 26;
    public bool ShowSubLabel => Width >= 65 && Height >= 36;
    public bool ShowBadge => Width >= 105 && Height >= 48;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsLast { get; set; }
}

