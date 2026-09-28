using System.Windows.Controls;
using System.Windows.Input;
using DiskScope.ViewModels;

namespace DiskScope.Views;

public partial class PhotoshopView : UserControl
{
    public PhotoshopView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PhotoshopViewModel vm && vm.SelectedFile != null)
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}
