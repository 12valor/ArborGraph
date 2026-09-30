using System.Windows;
using System.Windows.Controls;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
        Loaded += OverviewView_Loaded;
        Unloaded += OverviewView_Unloaded;
    }

    private void OverviewView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OverviewViewModel vm)
        {
            vm.OnViewLoaded();
        }
    }

    private void OverviewView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OverviewViewModel vm)
        {
            vm.OnViewUnloaded();
        }
    }

    private void ResourceGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ResourceGrid == null || CpuCard == null || RamCard == null) return;

        double width = e.NewSize.Width;
        if (width < 520)
        {
            // Stack vertically on narrow viewports
            Col0.Width = new GridLength(1, GridUnitType.Star);
            Col1.Width = new GridLength(0);
            Col2.Width = new GridLength(0);

            RowGap.Height = new GridLength(10);
            Row1.Height = new GridLength(1, GridUnitType.Auto);

            Grid.SetRow(CpuCard, 0);
            Grid.SetColumn(CpuCard, 0);
            Grid.SetColumnSpan(CpuCard, 1);

            Grid.SetRow(RamCard, 2);
            Grid.SetColumn(RamCard, 0);
            Grid.SetColumnSpan(RamCard, 1);
        }
        else
        {
            // Side-by-side on standard desktop viewports
            Col0.Width = new GridLength(1, GridUnitType.Star);
            Col1.Width = new GridLength(14);
            Col2.Width = new GridLength(1, GridUnitType.Star);

            RowGap.Height = new GridLength(0);
            Row1.Height = new GridLength(0);

            Grid.SetRow(CpuCard, 0);
            Grid.SetColumn(CpuCard, 0);
            Grid.SetColumnSpan(CpuCard, 1);

            Grid.SetRow(RamCard, 0);
            Grid.SetColumn(RamCard, 2);
            Grid.SetColumnSpan(RamCard, 1);
        }
    }
}
