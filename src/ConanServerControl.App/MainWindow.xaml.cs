using System.Windows;
using ConanServerControl.App.ViewModels;

namespace ConanServerControl.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
