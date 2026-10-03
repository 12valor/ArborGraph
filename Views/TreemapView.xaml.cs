using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.Models;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class TreemapView : UserControl
{
    public TreemapView()
    {
        InitializeComponent();
        Loaded += TreemapView_Loaded;
        SizeChanged += TreemapView_SizeChanged;
    }

    private void TreemapView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateDimensions();
    }

    private void TreemapView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDimensions();
    }

    private void UpdateDimensions()
    {
        try
        {
            if (TreemapContainer != null && DataContext is TreemapViewModel vm && TreemapContainer.ActualWidth > 50 && TreemapContainer.ActualHeight > 50)
            {
                vm.UpdateCanvasDimensions(TreemapContainer.ActualWidth, TreemapContainer.ActualHeight);
            }
        }
        catch { }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is TreemapRect rect && DataContext is TreemapViewModel vm)
        {
            vm.SelectNodeCommand.Execute(rect);

            if (e.ClickCount == 2)
            {
                vm.DrillDownCommand.Execute(rect);
                e.Handled = true;
            }
        }
    }
}
