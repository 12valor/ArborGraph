namespace DiskScope.Infrastructure;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";

        int unitIndex = 0;
        double size = bytes;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex switch
        {
            1 => $"{size:F1} KB",
            2 => $"{size:F2} MB",
            3 => $"{size:F2} GB",
            _ => $"{size:F2} {Units[unitIndex]}"
        };
    }

    public static string FormatExact(long bytes)
    {
        return $"{bytes:N0} bytes ({Format(bytes)})";
    }

    public static string FormatCount(long count)
    {
        return $"{count:N0}";
    }

    public static string FormatSpeed(double itemsPerSec)
    {
        return $"{itemsPerSec:N0} files/sec";
    }

    public static string FormatStorageSpeed(double bytesPerSec)
    {
        return $"{Format((long)bytesPerSec)}/sec";
    }

    public static string FormatTime(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
        }
        if (span.TotalMinutes >= 1)
        {
            return $"{span.Minutes}m {span.Seconds:D2}s";
        }
        return $"{span.Seconds}.{span.Milliseconds / 100}s";
    }
}
