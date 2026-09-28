using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class DuplicatesView : UserControl
{
    public DuplicatesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DuplicateViewModel vm && vm.SelectedFile != null)
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}
