using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace MoE.ModelManager.Helpers;

/// <summary>
/// Opens files, reveals them in File Explorer, copies text, and shows dialogs.
/// </summary>
internal static class ShellActions
{
    /// <summary>
    /// Places plain text on the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    public static void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    /// <summary>
    /// Opens a file in its default app.
    /// </summary>
    /// <param name="path">The full file path.</param>
    /// <returns>A task that completes when the launch request is sent.</returns>
    public static async Task OpenFileAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        await Launcher.LaunchFileAsync(file);
    }

    /// <summary>
    /// Opens File Explorer with the file selected, or opens the folder itself.
    /// </summary>
    /// <param name="path">A file or folder path.</param>
    public static void ShowInFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string arguments;
        if (File.Exists(path))
        {
            arguments = $"/select,\"{path}\"";
        }
        else if (Directory.Exists(path))
        {
            arguments = $"\"{path}\"";
        }
        else
        {
            string? parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            {
                return;
            }

            arguments = $"\"{parent}\"";
        }

        using Process? process = Process.Start(new ProcessStartInfo("explorer.exe", arguments)
        {
            UseShellExecute = true,
        });
    }

    /// <summary>
    /// Associates a WinRT picker with the main window.
    /// </summary>
    /// <param name="picker">The picker to initialize.</param>
    /// <returns><see langword="true"/> when the main window was available.</returns>
    public static bool InitializeWithMainWindow(object picker)
    {
        Window? window = ((App)Application.Current).MainWindow;
        if (window is null)
        {
            return false;
        }

        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        return true;
    }

    /// <summary>
    /// Creates a Fluent-styled dialog that follows the app theme.
    /// </summary>
    /// <param name="owner">An element in the window that shows the dialog.</param>
    /// <returns>A dialog ready for its title, content, and buttons.</returns>
    public static ContentDialog CreateDialog(FrameworkElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ContentDialog dialog = new()
        {
            XamlRoot = owner.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            DefaultButton = ContentDialogButton.Primary,
        };
        if (owner.XamlRoot?.Content is FrameworkElement root)
        {
            dialog.RequestedTheme = root.RequestedTheme;
        }

        return dialog;
    }
}
