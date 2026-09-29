using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using DiskScope.Infrastructure;

namespace DiskScope.Models;

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

    public bool ShowLabel => Width >= 45 && Height >= 24;
    public bool ShowSubLabel => Width >= 70 && Height >= 40;

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
