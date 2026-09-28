using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class LargestFilesView : UserControl
{
    public LargestFilesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LargestFilesViewModel vm && vm.SelectedFile != null)
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}
