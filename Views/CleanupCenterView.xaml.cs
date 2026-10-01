using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.Models;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class CleanupCenterView : UserControl
{
    public CleanupCenterView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CleanupCenterViewModel vm && vm.SelectedItem != null)
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}
