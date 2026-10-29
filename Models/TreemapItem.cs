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

