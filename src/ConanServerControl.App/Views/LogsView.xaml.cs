using System.Windows.Controls;
using ConanServerControl.App.ViewModels;

namespace ConanServerControl.App.Views;

public partial class LogsView : UserControl
{
    public LogsView(LogsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
