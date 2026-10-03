using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class FileTypesView : UserControl
{
    public FileTypesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FileTypesViewModel vm && vm.SelectedFile != null)
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}
