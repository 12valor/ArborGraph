using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class AnalyticsViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly DiskService _diskService;
    private readonly FileActionService _fileActionService;

    private bool _isLoading;
    private string _lastRefreshedText = "Not yet refreshed";

    // Storage Usage
    private long _totalDriveBytes;
    private long _usedDriveBytes;
    private long _freeDriveBytes;
    private long _indexedBytes;
    private long _indexedFilesCount;
    private double _overallPercentUsed;
    private double _indexedPercentOfUsed;

    // Duplicates
    private int _duplicateGroupsCount;
    private long _duplicateFilesCount;
    private long _duplicateWastedBytes;

    // Photoshop & Cache
    private long _psdCount;
    private long _psbCount;
    private long _otherPhotoshopCount;
    private long _photoshopTotalBytes;
    private long _adobeCacheFilesCount;
    private long _adobeCacheTotalBytes;

    // Reclaimable
    private long _totalReclaimableBytes;
    private double _reclaimablePercentOfIndexed;

    // Growth Chart
    private string _growthTrendMessage = "Record scans to view storage growth trends.";
    private PointCollection _polylinePoints = new();
    private PointCollection _polygonPoints = new();

    public AnalyticsViewModel(DatabaseService dbService, DiskService diskService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _diskService = diskService;
        _fileActionService = fileActionService;

        ScanHistory = [];
        GrowthPoints = [];
        CategoryBreakdown = [];
        TopFolders = [];
        AgeBuckets = [];
        ReclaimableBreakdown = [];
        LargestPhotoshopFiles = [];

        RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());
        OpenFileCommand = new RelayCommand(param => { if (param is string p) _fileActionService.OpenFile(p); });
        OpenFileLocationCommand = new RelayCommand(param => { if (param is string p) _fileActionService.OpenFileLocation(p); });
        CopyPathCommand = new RelayCommand(param => { if (param is string p) _fileActionService.CopyPath(p); });
        ShowPropertiesCommand = new RelayCommand(param => { if (param is string p) _fileActionService.ShowProperties(p); });
    }

    public ObservableCollection<ScanHistoryItem> ScanHistory { get; }
    public ObservableCollection<GrowthPoint> GrowthPoints { get; }
    public ObservableCollection<FileTypeItem> CategoryBreakdown { get; }
    public ObservableCollection<DirectoryRecord> TopFolders { get; }
    public ObservableCollection<FileAgeBucket> AgeBuckets { get; }
    public ObservableCollection<ReclaimableItem> ReclaimableBreakdown { get; }
    public ObservableCollection<FileRecord> LargestPhotoshopFiles { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string LastRefreshedText
    {
        get => _lastRefreshedText;
        set => SetProperty(ref _lastRefreshedText, value);
    }

    public long TotalDriveBytes
    {
        get => _totalDriveBytes;
        set => SetProperty(ref _totalDriveBytes, value);
    }

    public long UsedDriveBytes
    {
        get => _usedDriveBytes;
        set => SetProperty(ref _usedDriveBytes, value);
    }

    public long FreeDriveBytes
    {
        get => _freeDriveBytes;
        set => SetProperty(ref _freeDriveBytes, value);
    }

    public long IndexedBytes
    {
        get => _indexedBytes;
        set => SetProperty(ref _indexedBytes, value);
    }

    public long IndexedFilesCount
    {
        get => _indexedFilesCount;
        set => SetProperty(ref _indexedFilesCount, value);
    }

    public double OverallPercentUsed
    {
        get => _overallPercentUsed;
        set => SetProperty(ref _overallPercentUsed, value);
    }

    public double IndexedPercentOfUsed
    {
        get => _indexedPercentOfUsed;
        set => SetProperty(ref _indexedPercentOfUsed, value);
    }

    public string FormattedTotalDrive => SizeFormatter.Format(_totalDriveBytes);
    public string FormattedUsedDrive => SizeFormatter.Format(_usedDriveBytes);
    public string FormattedFreeDrive => SizeFormatter.Format(_freeDriveBytes);
    public string FormattedIndexedBytes => SizeFormatter.Format(_indexedBytes);
    public string FormattedIndexedFiles => SizeFormatter.FormatCount(_indexedFilesCount);

    // Duplicates
    public int DuplicateGroupsCount
    {
        get => _duplicateGroupsCount;
        set => SetProperty(ref _duplicateGroupsCount, value);
    }

    public long DuplicateFilesCount
    {
        get => _duplicateFilesCount;
        set => SetProperty(ref _duplicateFilesCount, value);
    }

    public long DuplicateWastedBytes
    {
        get => _duplicateWastedBytes;
        set
        {
            if (SetProperty(ref _duplicateWastedBytes, value))
            {
                OnPropertyChanged(nameof(FormattedDuplicateWasted));
            }
        }
    }

    public string FormattedDuplicateWasted => SizeFormatter.Format(_duplicateWastedBytes);

    // Photoshop & Cache
    public long PsdCount
    {
        get => _psdCount;
        set => SetProperty(ref _psdCount, value);
    }

    public long PsbCount
    {
        get => _psbCount;
        set => SetProperty(ref _psbCount, value);
    }

    public long OtherPhotoshopCount
    {
        get => _otherPhotoshopCount;
        set => SetProperty(ref _otherPhotoshopCount, value);
    }

    public long PhotoshopTotalBytes
    {
        get => _photoshopTotalBytes;
        set
        {
            if (SetProperty(ref _photoshopTotalBytes, value))
            {
                OnPropertyChanged(nameof(FormattedPhotoshopBytes));
            }
        }
    }

    public long AdobeCacheFilesCount
    {
        get => _adobeCacheFilesCount;
        set => SetProperty(ref _adobeCacheFilesCount, value);
    }

    public long AdobeCacheTotalBytes
    {
        get => _adobeCacheTotalBytes;
        set
        {
            if (SetProperty(ref _adobeCacheTotalBytes, value))
            {
                OnPropertyChanged(nameof(FormattedAdobeCacheBytes));
            }
        }
    }

    public string FormattedPhotoshopBytes => SizeFormatter.Format(_photoshopTotalBytes);
    public string FormattedAdobeCacheBytes => SizeFormatter.Format(_adobeCacheTotalBytes);

    // Reclaimable
    public long TotalReclaimableBytes
    {
        get => _totalReclaimableBytes;
        set
        {
            if (SetProperty(ref _totalReclaimableBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalReclaimable));
            }
        }
    }

    public double ReclaimablePercentOfIndexed
    {
        get => _reclaimablePercentOfIndexed;
        set => SetProperty(ref _reclaimablePercentOfIndexed, value);
    }

    public string FormattedTotalReclaimable => SizeFormatter.Format(_totalReclaimableBytes);

    // Growth Chart
    public string GrowthTrendMessage
    {
        get => _growthTrendMessage;
        set => SetProperty(ref _growthTrendMessage, value);
    }

    public PointCollection PolylinePoints
    {
        get => _polylinePoints;
        set => SetProperty(ref _polylinePoints, value);
    }

    public PointCollection PolygonPoints
    {
        get => _polygonPoints;
        set => SetProperty(ref _polygonPoints, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }

    public void RefreshData()
    {
        Task.Run(async () => await RefreshDataAsync());
    }

    public async Task RefreshDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            await Task.Run(() =>
            {
                // 1. Storage Usage
                long totalD = 0, usedD = 0, freeD = 0;
                foreach (var d in _diskService.GetSystemDrives())
                {
                    totalD += d.TotalBytes;
                    usedD += d.UsedBytes;
                    freeD += d.FreeBytes;
                }

                var (totalIndexedFiles, totalIndexedBytes) = _dbService.GetTotalIndexedStorage();

                // 2. Scan History
                var history = _dbService.GetScanHistory(20);

                // 3. File Types Breakdown
                var catMap = _dbService.GetCategoryBreakdown();
                var catList = new List<FileTypeItem>();
                long catSumBytes = catMap.Values.Sum(v => v.TotalSize);
                foreach (var categoryName in FileCategory.AllCategories)
                {
                    if (catMap.TryGetValue(categoryName, out var data))
                    {
                        double pct = catSumBytes > 0 ? (double)data.TotalSize / catSumBytes * 100.0 : 0.0;
                        catList.Add(new FileTypeItem
                        {
                            Category = categoryName,
                            FileCount = data.Count,
                            LogicalBytes = data.TotalSize,
                            StoragePercentage = pct
                        });
                    }
                    else
                    {
                        catList.Add(new FileTypeItem
                        {
                            Category = categoryName,
                            FileCount = 0,
                            LogicalBytes = 0,
                            StoragePercentage = 0.0
                        });
                    }
                }
                catList = catList.OrderByDescending(c => c.LogicalBytes).ToList();

                // 4. Largest Folders (top 8)
                var rawFolders = _dbService.GetLargestFolders(8);
                long maxFolder = Math.Max(1L, rawFolders.Count > 0 ? rawFolders.Max(f => f.Size) : 1L);
                foreach (var f in rawFolders)
                {
                    f.PercentageOfTotal = (double)f.Size / maxFolder * 100.0;
                }

                // 5. File Age Buckets
                var ageList = _dbService.GetFileAgeBreakdown();

                // 6. Duplicate Overview
                var (dupGroups, dupFiles, dupWasted) = _dbService.GetDuplicateOverview();

                // 7. Photoshop & Cache
                var psStats = _dbService.GetPhotoshopStats();
                var (cacheFiles, cacheBytes) = _dbService.GetAdobeCacheAndTempStats();
                var largestPs = _dbService.GetLargestPhotoshopFiles(5);

                // 8. Reclaimable Storage
                var (reclaimItems, totalReclaim) = _dbService.GetReclaimableStorageBreakdown();

                // Dispatch to UI Thread
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    TotalDriveBytes = totalD;
                    UsedDriveBytes = usedD;
                    FreeDriveBytes = freeD;
                    IndexedBytes = totalIndexedBytes;
                    IndexedFilesCount = totalIndexedFiles;

                    OverallPercentUsed = totalD > 0 ? (double)usedD / totalD * 100.0 : 0.0;
                    IndexedPercentOfUsed = usedD > 0 ? (double)totalIndexedBytes / usedD * 100.0 : 0.0;

                    OnPropertyChanged(nameof(FormattedTotalDrive));
                    OnPropertyChanged(nameof(FormattedUsedDrive));
                    OnPropertyChanged(nameof(FormattedFreeDrive));
                    OnPropertyChanged(nameof(FormattedIndexedBytes));
                    OnPropertyChanged(nameof(FormattedIndexedFiles));

                    DuplicateGroupsCount = dupGroups;
                    DuplicateFilesCount = dupFiles;
                    DuplicateWastedBytes = dupWasted;

                    PsdCount = psStats.PsdCount;
                    PsbCount = psStats.PsbCount;
                    OtherPhotoshopCount = psStats.OtherCount;
                    PhotoshopTotalBytes = psStats.TotalBytes;
                    AdobeCacheFilesCount = cacheFiles;
                    AdobeCacheTotalBytes = cacheBytes;

                    TotalReclaimableBytes = totalReclaim;
                    ReclaimablePercentOfIndexed = totalIndexedBytes > 0
                        ? Math.Min(100.0, (double)totalReclaim / totalIndexedBytes * 100.0)
                        : 0.0;

                    // Collections
                    ScanHistory.Clear();
                    foreach (var h in history.OrderByDescending(x => x.Id))
                    {
                        ScanHistory.Add(h);
                    }

                    CategoryBreakdown.Clear();
                    foreach (var c in catList)
                    {
                        CategoryBreakdown.Add(c);
                    }

                    TopFolders.Clear();
                    foreach (var f in rawFolders)
                    {
                        TopFolders.Add(f);
                    }

                    AgeBuckets.Clear();
                    foreach (var a in ageList)
                    {
                        AgeBuckets.Add(a);
                    }

                    ReclaimableBreakdown.Clear();
                    foreach (var r in reclaimItems)
                    {
                        ReclaimableBreakdown.Add(r);
                    }

                    LargestPhotoshopFiles.Clear();
                    foreach (var p in largestPs)
                    {
                        LargestPhotoshopFiles.Add(p);
                    }

                    // Build Growth Points for Chart
                    BuildGrowthChart(history, totalIndexedBytes, totalIndexedFiles);

                    LastRefreshedText = $"Updated {DateTime.Now:HH:mm:ss}";
                });
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Analytics Refresh error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildGrowthChart(List<ScanHistoryItem> history, long currentIndexedBytes, long currentFiles)
    {
        GrowthPoints.Clear();

        var pts = new List<GrowthPoint>();

        if (history.Count == 0)
        {
            // If no recorded history yet but we have current indexed data, add it
            if (currentIndexedBytes > 0)
            {
                pts.Add(new GrowthPoint
                {
                    DateLabel = "Current Scan",
                    Bytes = currentIndexedBytes,
                    Files = currentFiles
                });
            }
        }
        else
        {
            foreach (var h in history)
            {
                pts.Add(new GrowthPoint
                {
                    DateLabel = h.StartTime.ToLocalTime().ToString("MM/dd HH:mm"),
                    Bytes = h.LogicalBytesIndexed,
                    Files = h.FilesIndexed
                });
            }
        }

        if (pts.Count == 0)
        {
            GrowthTrendMessage = "No scans completed yet. Run a scan from the top toolbar to start tracking growth trends.";
            PolylinePoints = new PointCollection();
            PolygonPoints = new PointCollection();
            return;
        }

        // Layout bounds inside Canvas: width 520, height 120, padding 20
        const double chartWidth = 520.0;
        const double chartHeight = 120.0;
        const double padX = 30.0;
        const double padY = 16.0;

        long minBytes = pts.Min(p => p.Bytes);
        long maxBytes = pts.Max(p => p.Bytes);
        double range = Math.Max(1L, maxBytes - minBytes);

        var poly = new PointCollection();
        var polyFill = new PointCollection();

        // If only 1 point, center it
        if (pts.Count == 1)
        {
            pts[0].X = chartWidth / 2.0;
            pts[0].Y = chartHeight / 2.0;
            poly.Add(new Point(padX, chartHeight / 2.0));
            poly.Add(new Point(chartWidth - padX, chartHeight / 2.0));

            polyFill.Add(new Point(padX, chartHeight - padY));
            polyFill.Add(new Point(padX, chartHeight / 2.0));
            polyFill.Add(new Point(chartWidth - padX, chartHeight / 2.0));
            polyFill.Add(new Point(chartWidth - padX, chartHeight - padY));

            GrowthTrendMessage = $"Baseline scan indexed {SizeFormatter.Format(pts[0].Bytes)} across {SizeFormatter.FormatCount(pts[0].Files)} files.";
        }
        else
        {
            double stepX = (chartWidth - 2 * padX) / (pts.Count - 1);

            polyFill.Add(new Point(padX, chartHeight - padY));

            for (int i = 0; i < pts.Count; i++)
            {
                double px = padX + i * stepX;
                double py = (chartHeight - padY) - ((pts[i].Bytes - minBytes) / range) * (chartHeight - 2 * padY);

                pts[i].X = px;
                pts[i].Y = py;

                var pt = new Point(px, py);
                poly.Add(pt);
                polyFill.Add(pt);
            }

            polyFill.Add(new Point(padX + (pts.Count - 1) * stepX, chartHeight - padY));

            long netGrowth = pts[^1].Bytes - pts[0].Bytes;
            string sign = netGrowth >= 0 ? "+" : "";
            GrowthTrendMessage = $"Storage indexed has changed by {sign}{SizeFormatter.Format(netGrowth)} ({pts.Count} scan checkpoints tracked).";
        }

        foreach (var p in pts)
        {
            GrowthPoints.Add(p);
        }

        PolylinePoints = poly;
        PolygonPoints = polyFill;
    }
}
