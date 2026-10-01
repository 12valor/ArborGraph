using System.Collections.ObjectModel;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

public enum DeveloperEcosystem
{
    NodeJs,
    DotNet,
    Python,
    Rust,
    GradleJava,
    Other
}

public enum DeveloperJunkType
{
    ProjectDependency,   // node_modules
    BuildArtifact,       // bin, obj, target, build
    CompilerCache,       // __pycache__
    PackageCache         // npm cache, nuget, pip, cargo, gradle
}

public class DeveloperJunkItem : ObservableObject
{
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DeveloperEcosystem Ecosystem { get; set; }
    public DeveloperJunkType JunkType { get; set; }
    public string Name { get; set; } = string.Empty;              // e.g. "node_modules", "bin", "target"
    public string Path { get; set; } = string.Empty;              // Absolute path
    public string ProjectName { get; set; } = string.Empty;       // Identified project name
    public string ProjectPath { get; set; } = string.Empty;       // Path to parent project
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public DateTime LastModified { get; set; }
    public string ContextReason { get; set; } = string.Empty;     // e.g. "Confirmed via package.json"
    public bool IsSafeToClean { get; set; } = true;
    public bool IsGlobalCache { get; set; } = false;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string FormattedSize => SizeFormatter.Format(SizeBytes);
    public string FormattedLastModified => LastModified == DateTime.MinValue ? "—" : LastModified.ToString("yyyy-MM-dd HH:mm");

    public string EcosystemDisplay => Ecosystem switch
    {
        DeveloperEcosystem.NodeJs => "Node.js",
        DeveloperEcosystem.DotNet => ".NET",
        DeveloperEcosystem.Python => "Python",
        DeveloperEcosystem.Rust => "Rust",
        DeveloperEcosystem.GradleJava => "Gradle / Java",
        _ => "Other"
    };

    public string TypeDisplay => JunkType switch
    {
        DeveloperJunkType.ProjectDependency => "Dependencies",
        DeveloperJunkType.BuildArtifact => "Build Output",
        DeveloperJunkType.CompilerCache => "Compiler Cache",
        DeveloperJunkType.PackageCache => "Package Cache",
        _ => "Cache"
    };
}

public class DeveloperSubcategorySummary : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public int ItemCount { get; set; }
    public string FormattedTotal => SizeFormatter.Format(TotalBytes);
}

public class DeveloperEcosystemSummary : ObservableObject
{
    private long _totalBytes;
    private int _itemCount;

    public DeveloperEcosystem Ecosystem { get; set; }
    public string Name { get; set; } = string.Empty;

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (SetProperty(ref _totalBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotal));
            }
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    public ObservableCollection<DeveloperJunkItem> Items { get; set; } = [];
    public ObservableCollection<DeveloperSubcategorySummary> Subcategories { get; set; } = [];
    public string FormattedTotal => SizeFormatter.Format(TotalBytes);

    public void RefreshTotals()
    {
        TotalBytes = Items.Sum(i => i.SizeBytes);
        ItemCount = Items.Count;
        RefreshSubcategories();
    }

    public void RefreshSubcategories()
    {
        Subcategories.Clear();
        var groups = Items
            .GroupBy(i => i.IsGlobalCache ? i.Name : i.Name.ToLowerInvariant())
            .OrderByDescending(g => g.Sum(x => x.SizeBytes));

        foreach (var g in groups)
        {
            Subcategories.Add(new DeveloperSubcategorySummary
            {
                Name = g.Key,
                TotalBytes = g.Sum(x => x.SizeBytes),
                ItemCount = g.Count()
            });
        }
    }
}
