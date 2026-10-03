using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class DeveloperStorageView : UserControl
{
    public DeveloperStorageView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DeveloperStorageViewModel vm && vm.SelectedItem != null)
        {
            vm.OpenLocationCommand.Execute(null);
        }
    }
}
