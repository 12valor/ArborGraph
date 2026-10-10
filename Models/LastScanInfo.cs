using System;
using System.Collections.Generic;
using System.Linq;
using DiskScope.Models;

namespace DiskScope.Models;

public class LastScanInfo
{
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public string TargetDescription { get; set; } = string.Empty;
    public string CustomScanPath { get; set; } = string.Empty;
    public List<string> SelectedDrives { get; set; } = [];
    public List<string> ScannedRoots { get; set; } = [];
    public string StatusMessage { get; set; } = string.Empty;

    // Scan statistics fields
    public long DirectoriesVisited { get; set; }
    public long DirectoriesProcessed { get; set; }
    public long DirectoriesSkipped { get; set; }
    public long FilesDiscovered { get; set; }
    public long FilesIndexed { get; set; }
    public long FilesSkipped { get; set; }
    public long LogicalBytesIndexed { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public double FilesPerSecond { get; set; }
    public double BytesPerSecond { get; set; }
    public string CurrentDirectory { get; set; } = string.Empty;
    public string State { get; set; } = "Completed";

    public ScanStats ToScanStats()
    {
        return new ScanStats
        {
            DirectoriesVisited = DirectoriesVisited,
            DirectoriesProcessed = DirectoriesProcessed,
            DirectoriesSkipped = DirectoriesSkipped,
            FilesDiscovered = FilesDiscovered,
            FilesIndexed = FilesIndexed,
            FilesSkipped = FilesSkipped,
            LogicalBytesIndexed = LogicalBytesIndexed,
            Elapsed = TimeSpan.FromMilliseconds(Math.Max(0, ElapsedMilliseconds)),
            FilesPerSecond = FilesPerSecond,
            BytesPerSecond = BytesPerSecond,
            CurrentDirectory = !string.IsNullOrWhiteSpace(CurrentDirectory)
                ? CurrentDirectory
                : (!string.IsNullOrWhiteSpace(CustomScanPath) ? CustomScanPath : (SelectedDrives.Count > 0 ? SelectedDrives[0] : "C:\\")),
            State = ScanState.Completed
        };
    }

    public static LastScanInfo FromScanStats(
        ScanStats stats,
        IReadOnlyList<string> roots,
        string? customScanPath,
        IReadOnlyList<string>? selectedDrives,
        string? statusMessage)
    {
        string target = !string.IsNullOrWhiteSpace(customScanPath)
            ? customScanPath
            : (selectedDrives != null && selectedDrives.Count > 0 ? string.Join(", ", selectedDrives) : (roots.Count > 0 ? string.Join(", ", roots) : "C:\\"));

        return new LastScanInfo
        {
            CompletedAtUtc = DateTime.UtcNow,
            TargetDescription = target,
            CustomScanPath = customScanPath ?? string.Empty,
            SelectedDrives = selectedDrives?.ToList() ?? [],
            ScannedRoots = roots?.ToList() ?? [],
            StatusMessage = statusMessage ?? string.Empty,

            DirectoriesVisited = stats.DirectoriesVisited,
            DirectoriesProcessed = stats.DirectoriesProcessed,
            DirectoriesSkipped = stats.DirectoriesSkipped,
            FilesDiscovered = stats.FilesDiscovered,
            FilesIndexed = stats.FilesIndexed,
            FilesSkipped = stats.FilesSkipped,
            LogicalBytesIndexed = stats.LogicalBytesIndexed,
            ElapsedMilliseconds = (long)stats.Elapsed.TotalMilliseconds,
            FilesPerSecond = stats.FilesPerSecond,
            BytesPerSecond = stats.BytesPerSecond,
            CurrentDirectory = !string.IsNullOrWhiteSpace(stats.CurrentDirectory) ? stats.CurrentDirectory : target,
            State = ScanState.Completed.ToString()
        };
    }
}
