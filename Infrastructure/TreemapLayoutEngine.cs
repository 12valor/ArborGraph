using System.Windows.Media;
using DiskScope.Models;

namespace DiskScope.Infrastructure;

public static class TreemapLayoutEngine
{
    private static Brush CreateFrozenBrush(string hex, double opacity = 1.0)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
        {
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }

    private static Brush CreateFrozenLinearGradient(string hexStart, string hexEnd, double opacity = 1.0)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0.85, 1.0),
            GradientStops =
            [
                new GradientStop((Color)ColorConverter.ConvertFromString(hexStart), 0.0),
                new GradientStop((Color)ColorConverter.ConvertFromString(hexEnd), 1.0)
            ],
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }

    // Diverse, rich jewel-toned folder gradients so directory blocks have depth and distinction
    private static readonly Brush[] FolderGradients =
    [
        CreateFrozenLinearGradient("#1E3A8A", "#0F172A"), // Royal Sapphire
        CreateFrozenLinearGradient("#312E81", "#1E1B4B"), // Deep Indigo
        CreateFrozenLinearGradient("#064E3B", "#022C22"), // Emerald Forest
        CreateFrozenLinearGradient("#701A75", "#4A044E"), // Deep Plum
        CreateFrozenLinearGradient("#1E293B", "#0F172A"), // Sleek Obsidian Slate
        CreateFrozenLinearGradient("#14532D", "#052E16"), // Deep Forest
        CreateFrozenLinearGradient("#581C87", "#3B0764"), // Violet Night
        CreateFrozenLinearGradient("#164E63", "#083344"), // Deep Cyan
    ];

    private static readonly Brush FolderBorderBrush = CreateFrozenBrush("#38BDF8", 0.50);

    // Curated high-contrast modern category gradients and glowing borders
    private static readonly Dictionary<string, (Brush Fill, Brush Border)> CategoryPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Code", (CreateFrozenLinearGradient("#0D9488", "#115E59"), CreateFrozenBrush("#2DD4BF", 0.55)) },
        { "Developer", (CreateFrozenLinearGradient("#0D9488", "#115E59"), CreateFrozenBrush("#2DD4BF", 0.55)) },
        { "Video", (CreateFrozenLinearGradient("#8B5CF6", "#6D28D9"), CreateFrozenBrush("#C4B5FD", 0.55)) },
        { "Videos", (CreateFrozenLinearGradient("#8B5CF6", "#6D28D9"), CreateFrozenBrush("#C4B5FD", 0.55)) },
        { "Audio", (CreateFrozenLinearGradient("#EC4899", "#BE185D"), CreateFrozenBrush("#F472B6", 0.55)) },
        { "Images", (CreateFrozenLinearGradient("#F97316", "#C2410C"), CreateFrozenBrush("#FB923C", 0.55)) },
        { "Image", (CreateFrozenLinearGradient("#F97316", "#C2410C"), CreateFrozenBrush("#FB923C", 0.55)) },
        { "Photoshop", (CreateFrozenLinearGradient("#0284C7", "#0369A1"), CreateFrozenBrush("#38BDF8", 0.55)) },
        { "Executables", (CreateFrozenLinearGradient("#EF4444", "#B91C1C"), CreateFrozenBrush("#FCA5A5", 0.55)) },
        { "Executable", (CreateFrozenLinearGradient("#EF4444", "#B91C1C"), CreateFrozenBrush("#FCA5A5", 0.55)) },
        { "Archives", (CreateFrozenLinearGradient("#F59E0B", "#B45309"), CreateFrozenBrush("#FCD34D", 0.55)) },
        { "Archive", (CreateFrozenLinearGradient("#F59E0B", "#B45309"), CreateFrozenBrush("#FCD34D", 0.55)) },
        { "Compressed", (CreateFrozenLinearGradient("#F59E0B", "#B45309"), CreateFrozenBrush("#FCD34D", 0.55)) },
        { "Documents", (CreateFrozenLinearGradient("#3B82F6", "#1D4ED8"), CreateFrozenBrush("#93C5FD", 0.55)) },
        { "Document", (CreateFrozenLinearGradient("#3B82F6", "#1D4ED8"), CreateFrozenBrush("#93C5FD", 0.55)) },
        { "Database", (CreateFrozenLinearGradient("#6366F1", "#4338CA"), CreateFrozenBrush("#A5B4FC", 0.55)) },
        { "System", (CreateFrozenLinearGradient("#475569", "#334155"), CreateFrozenBrush("#94A3B8", 0.55)) },
        { "Other", (CreateFrozenLinearGradient("#64748B", "#475569"), CreateFrozenBrush("#CBD5E1", 0.45)) }
    };

    // Heatmap mode tiers based on size
    private static readonly (Brush Fill, Brush Border) HeatmapTierUltra = (CreateFrozenLinearGradient("#DC2626", "#991B1B"), CreateFrozenBrush("#F87171", 0.65)); // >= 5 GB
    private static readonly (Brush Fill, Brush Border) HeatmapTierHigh = (CreateFrozenLinearGradient("#EA580C", "#9A3412"), CreateFrozenBrush("#FB923C", 0.65)); // >= 1 GB
    private static readonly (Brush Fill, Brush Border) HeatmapTierMedHigh = (CreateFrozenLinearGradient("#D97706", "#78350F"), CreateFrozenBrush("#FBBF24", 0.65)); // >= 250 MB
    private static readonly (Brush Fill, Brush Border) HeatmapTierMed = (CreateFrozenLinearGradient("#7C3AED", "#4C1D95"), CreateFrozenBrush("#A78BFA", 0.65)); // >= 50 MB
    private static readonly (Brush Fill, Brush Border) HeatmapTierLowMed = (CreateFrozenLinearGradient("#2563EB", "#1E3A8A"), CreateFrozenBrush("#60A5FA", 0.65)); // >= 10 MB
    private static readonly (Brush Fill, Brush Border) HeatmapTierLow = (CreateFrozenLinearGradient("#0D9488", "#134E4A"), CreateFrozenBrush("#2DD4BF", 0.65)); // < 10 MB

    // Depth mode tiers based on % share of current view
    private static readonly (Brush Fill, Brush Border) DepthTier1 = (CreateFrozenLinearGradient("#059669", "#047857"), CreateFrozenBrush("#34D399", 0.65)); // >= 25%
    private static readonly (Brush Fill, Brush Border) DepthTier2 = (CreateFrozenLinearGradient("#0D9488", "#0F766E"), CreateFrozenBrush("#2DD4BF", 0.65)); // >= 10%
    private static readonly (Brush Fill, Brush Border) DepthTier3 = (CreateFrozenLinearGradient("#0284C7", "#0369A1"), CreateFrozenBrush("#38BDF8", 0.65)); // >= 5%
    private static readonly (Brush Fill, Brush Border) DepthTier4 = (CreateFrozenLinearGradient("#2563EB", "#1D4ED8"), CreateFrozenBrush("#60A5FA", 0.65)); // >= 2%
    private static readonly (Brush Fill, Brush Border) DepthTier5 = (CreateFrozenLinearGradient("#1E3A8A", "#172554"), CreateFrozenBrush("#93C5FD", 0.65)); // < 2%

    public static Brush GetBrushForCategory(string category, bool isDirectory)
    {
        if (isDirectory)
        {
            return FolderGradients[0];
        }

        if (CategoryPalette.TryGetValue(category, out var pair))
        {
            return pair.Fill;
        }

        return CategoryPalette["Other"].Fill;
    }

    public static (Brush Fill, Brush Border) GetBrushesForItem(TreemapItem item, TreemapColorMode colorMode, int itemIndex)
    {
        if (colorMode == TreemapColorMode.Heatmap)
        {
            long size = item.Size;
            if (size >= 5L * 1024 * 1024 * 1024) return HeatmapTierUltra;
            if (size >= 1L * 1024 * 1024 * 1024) return HeatmapTierHigh;
            if (size >= 250L * 1024 * 1024) return HeatmapTierMedHigh;
            if (size >= 50L * 1024 * 1024) return HeatmapTierMed;
            if (size >= 10L * 1024 * 1024) return HeatmapTierLowMed;
            return HeatmapTierLow;
        }

        if (colorMode == TreemapColorMode.Depth)
        {
            double pct = item.Percentage;
            if (pct >= 25.0) return DepthTier1;
            if (pct >= 10.0) return DepthTier2;
            if (pct >= 5.0) return DepthTier3;
            if (pct >= 2.0) return DepthTier4;
            return DepthTier5;
        }

        // Category Mode (Default)
        if (item.IsDirectory)
        {
            int folderIdx = Math.Abs(item.Name.GetHashCode()) % FolderGradients.Length;
            return (FolderGradients[folderIdx], FolderBorderBrush);
        }

        if (CategoryPalette.TryGetValue(item.Category, out var pair))
        {
            return pair;
        }

        return CategoryPalette["Other"];
    }

    /// <summary>
    /// Computes squarified treemap rectangles using Bruls, Huizing, van Wijk algorithm.
    /// </summary>
    public static List<TreemapRect> ComputeLayout(
        IReadOnlyList<TreemapItem> items,
        double width,
        double height,
        TreemapColorMode colorMode = TreemapColorMode.Category)
    {
        var result = new List<TreemapRect>();
        if (items.Count == 0 || width <= 0 || height <= 0) return result;

        // Filter out zero size items and sort descending
        var validItems = items.Where(i => i.Size > 0).OrderByDescending(i => i.Size).ToList();
        if (validItems.Count == 0) return result;

        long totalSize = validItems.Sum(i => i.Size);
        if (totalSize <= 0) return result;

        double totalArea = width * height;

        // Convert sizes to target areas
        var areas = new List<(TreemapItem Item, double Area, int Index)>(validItems.Count);
        for (int i = 0; i < validItems.Count; i++)
        {
            var item = validItems[i];
            double itemArea = (double)item.Size / totalSize * totalArea;
            item.Percentage = (double)item.Size / totalSize * 100.0;
            areas.Add((item, itemArea, i));
        }

        // Run squarified layout
        SquarifyRecursive(areas, 0, 0, width, height, colorMode, result);
        return result;
    }

    private static void SquarifyRecursive(
        List<(TreemapItem Item, double Area, int Index)> children,
        double x,
        double y,
        double w,
        double h,
        TreemapColorMode colorMode,
        List<TreemapRect> output)
    {
        if (children.Count == 0 || w <= 0 || h <= 0) return;

        if (children.Count == 1)
        {
            output.Add(CreateRect(children[0].Item, x, y, w, h, colorMode, children[0].Index));
            return;
        }

        var row = new List<(TreemapItem Item, double Area, int Index)>();
        double side = Math.Min(w, h);
        int index = 0;

        while (index < children.Count)
        {
            var nextChild = children[index];
            var testRow = new List<(TreemapItem Item, double Area, int Index)>(row) { nextChild };

            if (row.Count == 0 || WorstAspectRatio(testRow, side) <= WorstAspectRatio(row, side))
            {
                row.Add(nextChild);
                index++;
            }
            else
            {
                break;
            }
        }

        // Layout the completed row
        double rowArea = row.Sum(r => r.Area);
        double rowThickness = side > 0 ? rowArea / side : 0;

        if (w <= h)
        {
            // Row is horizontal: fills width w, height is rowThickness
            double currentX = x;
            foreach (var elem in row)
            {
                double elemW = rowThickness > 0 ? elem.Area / rowThickness : 0;
                output.Add(CreateRect(elem.Item, currentX, y, elemW, rowThickness, colorMode, elem.Index));
                currentX += elemW;
            }

            // Recurse into remaining rectangle below row
            var remaining = children.Skip(index).ToList();
            SquarifyRecursive(remaining, x, y + rowThickness, w, Math.Max(0, h - rowThickness), colorMode, output);
        }
        else
        {
            // Row is vertical: fills height h, width is rowThickness
            double currentY = y;
            foreach (var elem in row)
            {
                double elemH = rowThickness > 0 ? elem.Area / rowThickness : 0;
                output.Add(CreateRect(elem.Item, x, currentY, rowThickness, elemH, colorMode, elem.Index));
                currentY += elemH;
            }

            // Recurse into remaining rectangle to right of row
            var remaining = children.Skip(index).ToList();
            SquarifyRecursive(remaining, x + rowThickness, y, Math.Max(0, w - rowThickness), h, colorMode, output);
        }
    }

    private static double WorstAspectRatio(List<(TreemapItem Item, double Area, int Index)> row, double side)
    {
        if (row.Count == 0 || side <= 0) return double.MaxValue;

        double sum = row.Sum(r => r.Area);
        if (sum <= 0) return double.MaxValue;

        double sideSq = side * side;
        double sumSq = sum * sum;

        double maxWorst = 0;
        foreach (var elem in row)
        {
            double r = elem.Area;
            if (r <= 0) continue;
            double aspect = Math.Max((sideSq * r) / sumSq, sumSq / (sideSq * r));
            if (aspect > maxWorst) maxWorst = aspect;
        }

        return maxWorst;
    }

    private static TreemapRect CreateRect(
        TreemapItem item,
        double x,
        double y,
        double w,
        double h,
        TreemapColorMode colorMode,
        int itemIndex)
    {
        // Avoid tiny sub-pixel artifacts
        double finalW = Math.Max(0, w);
        double finalH = Math.Max(0, h);

        var (fill, border) = GetBrushesForItem(item, colorMode, itemIndex);

        return new TreemapRect
        {
            Item = item,
            X = x,
            Y = y,
            Width = finalW,
            Height = finalH,
            FillBrush = fill,
            BorderBrush = border
        };
    }
}

