using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DiskScope.Models;

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

