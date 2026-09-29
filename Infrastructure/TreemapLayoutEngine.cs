using System.Windows.Media;
using DiskScope.Models;

namespace DiskScope.Infrastructure;

public static class TreemapLayoutEngine
{
    private static readonly Dictionary<string, Brush> CategoryBrushes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Video", CreateFrozenBrush("#8B5CF6") },       // Violet
        { "Videos", CreateFrozenBrush("#8B5CF6") },
        { "Images", CreateFrozenBrush("#10B981") },      // Emerald
        { "Image", CreateFrozenBrush("#10B981") },
        { "Photoshop", CreateFrozenBrush("#059669") },   // Darker Emerald
        { "Executables", CreateFrozenBrush("#F43F5E") },  // Rose
        { "Executable", CreateFrozenBrush("#F43F5E") },
        { "Archives", CreateFrozenBrush("#F59E0B") },    // Amber
        { "Archive", CreateFrozenBrush("#F59E0B") },
        { "Compressed", CreateFrozenBrush("#F59E0B") },
        { "Documents", CreateFrozenBrush("#0284C7") },   // Sky Blue
        { "Document", CreateFrozenBrush("#0284C7") },
        { "Code", CreateFrozenBrush("#06B6D4") },        // Cyan
        { "Audio", CreateFrozenBrush("#EC4899") },       // Pink
        { "Folder", CreateFrozenBrush("#334155") },      // Slate Dark
        { "Directory", CreateFrozenBrush("#334155") },
        { "Other", CreateFrozenBrush("#64748B") }        // Slate Muted
    };

    private static readonly Brush DefaultBrush = CreateFrozenBrush("#64748B");
    private static readonly Brush BorderBrush = CreateFrozenBrush("#FFFFFF", 0.15);
    private static readonly Brush FolderBorderBrush = CreateFrozenBrush("#38BDF8", 0.5);

    private static Brush CreateFrozenBrush(string hex, double opacity = 1.0)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
        {
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }

    public static Brush GetBrushForCategory(string category, bool isDirectory)
    {
        if (isDirectory)
        {
            return CategoryBrushes["Folder"];
        }

        if (CategoryBrushes.TryGetValue(category, out var brush))
        {
            return brush;
        }

        return DefaultBrush;
    }

    /// <summary>
    /// Computes squarified treemap rectangles using Bruls, Huizing, van Wijk algorithm.
    /// </summary>
    public static List<TreemapRect> ComputeLayout(
        IReadOnlyList<TreemapItem> items,
        double width,
        double height)
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
        var areas = new List<(TreemapItem Item, double Area)>(validItems.Count);
        foreach (var item in validItems)
        {
            double itemArea = (double)item.Size / totalSize * totalArea;
            item.Percentage = (double)item.Size / totalSize * 100.0;
            areas.Add((item, itemArea));
        }

        // Run squarified layout
        SquarifyRecursive(areas, 0, 0, width, height, result);
        return result;
    }

    private static void SquarifyRecursive(
        List<(TreemapItem Item, double Area)> children,
        double x,
        double y,
        double w,
        double h,
        List<TreemapRect> output)
    {
        if (children.Count == 0 || w <= 0 || h <= 0) return;

        if (children.Count == 1)
        {
            output.Add(CreateRect(children[0].Item, x, y, w, h));
            return;
        }

        var row = new List<(TreemapItem Item, double Area)>();
        double side = Math.Min(w, h);
        int index = 0;

        while (index < children.Count)
        {
            var nextChild = children[index];
            var testRow = new List<(TreemapItem Item, double Area)>(row) { nextChild };

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
                output.Add(CreateRect(elem.Item, currentX, y, elemW, rowThickness));
                currentX += elemW;
            }

            // Recurse into remaining rectangle below row
            var remaining = children.Skip(index).ToList();
            SquarifyRecursive(remaining, x, y + rowThickness, w, Math.Max(0, h - rowThickness), output);
        }
        else
        {
            // Row is vertical: fills height h, width is rowThickness
            double currentY = y;
            foreach (var elem in row)
            {
                double elemH = rowThickness > 0 ? elem.Area / rowThickness : 0;
                output.Add(CreateRect(elem.Item, x, currentY, rowThickness, elemH));
                currentY += elemH;
            }

            // Recurse into remaining rectangle to right of row
            var remaining = children.Skip(index).ToList();
            SquarifyRecursive(remaining, x + rowThickness, y, Math.Max(0, w - rowThickness), h, output);
        }
    }

    private static double WorstAspectRatio(List<(TreemapItem Item, double Area)> row, double side)
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

    private static TreemapRect CreateRect(TreemapItem item, double x, double y, double w, double h)
    {
        // Avoid tiny sub-pixel artifacts
        double finalW = Math.Max(0, w);
        double finalH = Math.Max(0, h);

        return new TreemapRect
        {
            Item = item,
            X = x,
            Y = y,
            Width = finalW,
            Height = finalH,
            FillBrush = GetBrushForCategory(item.Category, item.IsDirectory),
            BorderBrush = item.IsDirectory ? FolderBorderBrush : BorderBrush
        };
    }
}
