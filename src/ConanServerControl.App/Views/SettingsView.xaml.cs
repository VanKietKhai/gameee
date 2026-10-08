using System.Windows;
using System.Windows.Controls;
using ConanServerControl.App.ViewModels;

namespace ConanServerControl.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.DiscordWebhookInput)
                && string.IsNullOrEmpty(viewModel.DiscordWebhookInput)
                && DiscordWebhookBox.Password.Length > 0)
            {
                DiscordWebhookBox.Clear();
            }
        };
    }

    private void DiscordWebhookBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.DiscordWebhookInput = DiscordWebhookBox.Password;
        }
    }
}
