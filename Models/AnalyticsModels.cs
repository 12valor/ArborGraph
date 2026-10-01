using System.Collections.ObjectModel;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public class ScanHistoryItem
{
    public long Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime FinishTime { get; set; }
    public string Roots { get; set; } = string.Empty;
    public long DirectoriesVisited { get; set; }
    public long DirectoriesProcessed { get; set; }
    public long DirectoriesSkipped { get; set; }
    public long FilesDiscovered { get; set; }
    public long FilesIndexed { get; set; }
    public long FilesSkipped { get; set; }
    public long LogicalBytesIndexed { get; set; }
    public string ScanStatus { get; set; } = "Completed";

    public TimeSpan Duration => FinishTime > StartTime ? FinishTime - StartTime : TimeSpan.Zero;
    public string FormattedDuration => Duration.TotalMinutes >= 1
        ? $"{(int)Duration.TotalMinutes}m {Duration.Seconds:D2}s"
        : $"{Duration.TotalSeconds:F1}s";

    public string FormattedBytes => SizeFormatter.Format(LogicalBytesIndexed);
    public string FormattedDate => StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string FormattedFiles => SizeFormatter.FormatCount(FilesIndexed);

    public long GrowthBytes { get; set; }
    public string FormattedGrowth
    {
        get
        {
            if (GrowthBytes == 0) return "—";
            string sign = GrowthBytes > 0 ? "+" : "-";
            return $"{sign}{SizeFormatter.Format(Math.Abs(GrowthBytes))}";
        }
    }
}

public class FileAgeBucket
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Count { get; set; }
    public long TotalBytes { get; set; }
    public double PercentageOfTotal { get; set; }

    public string FormattedCount => SizeFormatter.FormatCount(Count);
    public string FormattedSize => SizeFormatter.Format(TotalBytes);
    public string FormattedPercentage => $"{PercentageOfTotal:F1}%";
}

public class ReclaimableItem
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public long FileCount { get; set; }
    public string FormattedBytes => SizeFormatter.Format(Bytes);
    public string FormattedCount => SizeFormatter.FormatCount(FileCount);
    public string ActionHint { get; set; } = string.Empty;
}

public class GrowthPoint
{
    public string DateLabel { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string FormattedBytes => SizeFormatter.Format(Bytes);
    public long Files { get; set; }
    public string FormattedFiles => SizeFormatter.FormatCount(Files);
    public double X { get; set; }
    public double Y { get; set; }
}

public class CategoryGrowthComparison
{
    public string Category { get; set; } = string.Empty;
    public long PreviousSizeBytes { get; set; }
    public long CurrentSizeBytes { get; set; }
    public long DeltaBytes => CurrentSizeBytes - PreviousSizeBytes;
    public long DeltaFiles { get; set; }
    public string FormattedPrevious => SizeFormatter.Format(PreviousSizeBytes);
    public string FormattedCurrent => SizeFormatter.Format(CurrentSizeBytes);
    public string FormattedDelta
    {
        get
        {
            if (DeltaBytes == 0) return "0 B";
            string sign = DeltaBytes > 0 ? "+" : "-";
            return $"{sign}{SizeFormatter.Format(Math.Abs(DeltaBytes))}";
        }
    }
    public bool IsGrowth => DeltaBytes > 0;
}

public class ScanComparisonResult : ObservableObject
{
    private ScanHistoryItem? _previousScan;
    private ScanHistoryItem? _currentScan;
    private long _deltaBytes;

    public ScanHistoryItem? PreviousScan
    {
        get => _previousScan;
        set => SetProperty(ref _previousScan, value);
    }

    public ScanHistoryItem? CurrentScan
    {
        get => _currentScan;
        set => SetProperty(ref _currentScan, value);
    }

    public long DeltaBytes
    {
        get => _deltaBytes;
        set
        {
            if (SetProperty(ref _deltaBytes, value))
            {
                OnPropertyChanged(nameof(FormattedDelta));
            }
        }
    }

    public string FormattedDelta
    {
        get
        {
            if (DeltaBytes == 0) return "0 B";
            string sign = DeltaBytes > 0 ? "+" : "-";
            return $"{sign}{SizeFormatter.Format(Math.Abs(DeltaBytes))}";
        }
    }

    public ObservableCollection<CategoryGrowthComparison> WhatGrew { get; } = [];
}

