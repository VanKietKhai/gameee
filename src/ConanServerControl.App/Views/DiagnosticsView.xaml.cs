using System.Windows.Controls;
using ConanServerControl.App.ViewModels;

namespace ConanServerControl.App.Views;

public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
