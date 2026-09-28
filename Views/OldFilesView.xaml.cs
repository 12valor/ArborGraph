using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class OldFilesView : UserControl
{
    public OldFilesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OldFilesViewModel vm && vm.SelectedFile != null)
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}
