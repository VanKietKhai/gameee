using System.Windows.Controls;
using ConanServerControl.App.ViewModels;

namespace ConanServerControl.App.Views;

public partial class ModsView : UserControl
{
    public ModsView(ModsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

public partial class BackupsView : UserControl
{
    public BackupsView(BackupsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

public partial class UpdatesView : UserControl
{
    public UpdatesView(UpdatesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

public partial class PlayersView : UserControl
{
    public PlayersView(PlayersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

public partial class ServerView : UserControl
{
    public ServerView(ServerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
