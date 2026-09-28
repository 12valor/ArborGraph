using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class LargestFoldersView : UserControl
{
    public LargestFoldersView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LargestFoldersViewModel vm && vm.SelectedFolder != null)
        {
            vm.OpenFolderCommand.Execute(null);
        }
    }
}
