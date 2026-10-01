using Microsoft.Win32;
using System.Windows;

namespace ConanServerControl.App.Services;

public interface IUiDialogs
{
    string? PickFile(string filter, string title);

    string? PickFolder(string title);

    void Alert(string title, string message);

    bool Confirm(string title, string message);
}

public sealed class WpfDialogService : IUiDialogs
{
    public string? PickFile(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void Alert(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
