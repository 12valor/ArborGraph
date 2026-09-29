using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class TreemapViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly FileActionService _fileActionService;

    private string _currentPath = string.Empty;
    private TreemapRect? _selectedNode;
    private bool _isLoading;
    private double _canvasWidth = 1000;
    private double _canvasHeight = 600;
    private long _totalVisibleBytes;
    private int _totalVisibleItems;
    private List<TreemapItem> _currentItems = new();

    public TreemapViewModel(DatabaseService dbService, FileActionService fileActionService)
    {
        _dbService = dbService;
        _fileActionService = fileActionService;

        Rectangles = new ObservableCollection<TreemapRect>();
        Breadcrumbs = new ObservableCollection<BreadcrumbItem>();

        DrillDownCommand = new RelayCommand(param => DrillDown(param as TreemapRect));
        SelectNodeCommand = new RelayCommand(param => SelectNode(param as TreemapRect));
        NavigateBreadcrumbCommand = new RelayCommand(param => NavigateToBreadcrumb(param as BreadcrumbItem));
        NavigateUpCommand = new RelayCommand(_ => NavigateUp(), _ => !string.IsNullOrEmpty(CurrentPath));
        OpenFileLocationCommand = new RelayCommand(_ => OpenFileLocation(), _ => SelectedNode != null);
        CopyPathCommand = new RelayCommand(_ => CopyPath(), _ => SelectedNode != null);
        RefreshCommand = new RelayCommand(_ => LoadCurrentLevel());

        UpdateBreadcrumbs();
    }

    public ObservableCollection<TreemapRect> Rectangles { get; }
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; }

    public ICommand DrillDownCommand { get; }
    public ICommand SelectNodeCommand { get; }
    public ICommand NavigateBreadcrumbCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand RefreshCommand { get; }

    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            if (SetProperty(ref _currentPath, value))
            {
                UpdateBreadcrumbs();
                (NavigateUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public TreemapRect? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != null)
            {
                _selectedNode.IsSelected = false;
            }

            if (SetProperty(ref _selectedNode, value))
            {
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = true;
                }
                OnPropertyChanged(nameof(HasSelectedNode));
                (OpenFileLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyPathCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedNode => _selectedNode != null;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        set
        {
            if (SetProperty(ref _canvasWidth, Math.Max(100, value)))
            {
                RecomputeLayout();
            }
        }
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        set
        {
            if (SetProperty(ref _canvasHeight, Math.Max(100, value)))
            {
                RecomputeLayout();
            }
        }
    }

    public long TotalVisibleBytes
    {
        get => _totalVisibleBytes;
        set
        {
            if (SetProperty(ref _totalVisibleBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalBytes));
            }
        }
    }

    public int TotalVisibleItems
    {
        get => _totalVisibleItems;
        set => SetProperty(ref _totalVisibleItems, value);
    }

    public string FormattedTotalBytes => SizeFormatter.Format(TotalVisibleBytes);

    public void RefreshData()
    {
        LoadCurrentLevel();
    }

    public void UpdateCanvasDimensions(double width, double height)
    {
        if (width > 50 && height > 50)
        {
            _canvasWidth = width;
            _canvasHeight = height;
            RecomputeLayout();
        }
    }

    private void LoadCurrentLevel()
    {
        IsLoading = true;
        try
        {
            _currentItems = _dbService.GetTreemapItems(string.IsNullOrEmpty(CurrentPath) ? null : CurrentPath, 150);
            TotalVisibleBytes = _currentItems.Sum(i => i.Size);
            TotalVisibleItems = _currentItems.Count;
            RecomputeLayout();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading treemap level: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RecomputeLayout()
    {
        if (_canvasWidth <= 0 || _canvasHeight <= 0) return;

        var computed = TreemapLayoutEngine.ComputeLayout(_currentItems, _canvasWidth, _canvasHeight);

        Rectangles.Clear();
        foreach (var rect in computed)
        {
            Rectangles.Add(rect);
        }

        // Restore or clear selected node
        if (SelectedNode != null)
        {
            SelectedNode = Rectangles.FirstOrDefault(r => r.Item.Path == SelectedNode.Item.Path);
        }
    }

    private void DrillDown(TreemapRect? node)
    {
        if (node == null) return;

        if (node.Item.IsDirectory)
        {
            CurrentPath = node.Item.Path;
            SelectedNode = null;
            LoadCurrentLevel();
        }
        else
        {
            SelectNode(node);
        }
    }

    private void SelectNode(TreemapRect? node)
    {
        SelectedNode = node;
    }

    private void NavigateToBreadcrumb(BreadcrumbItem? crumb)
    {
        if (crumb == null) return;
        CurrentPath = crumb.FullPath;
        SelectedNode = null;
        LoadCurrentLevel();
    }

    private void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        string? parent = Path.GetDirectoryName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        CurrentPath = parent ?? string.Empty;
        SelectedNode = null;
        LoadCurrentLevel();
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem
        {
            Name = "Root (All Indexed)",
            FullPath = string.Empty,
            IsLast = string.IsNullOrEmpty(CurrentPath)
        });

        if (!string.IsNullOrEmpty(CurrentPath))
        {
            string clean = CurrentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts = clean.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

            string accumulated = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (i == 0 && clean.Contains(':'))
                {
                    accumulated = part + Path.DirectorySeparatorChar;
                }
                else
                {
                    accumulated = Path.Combine(accumulated, part);
                }

                Breadcrumbs.Add(new BreadcrumbItem
                {
                    Name = part,
                    FullPath = accumulated,
                    IsLast = i == parts.Length - 1
                });
            }
        }
    }

    private void OpenFileLocation()
    {
        if (SelectedNode == null) return;
        _fileActionService.OpenFileLocation(SelectedNode.Item.Path);
    }

    private void CopyPath()
    {
        if (SelectedNode == null) return;
        _fileActionService.CopyPath(SelectedNode.Item.Path);
    }
}
