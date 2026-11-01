using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.Services;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager.Views;

/// <summary>
/// App preferences: theme, chat history, and version information.
/// </summary>
public sealed partial class SettingsView : UserControl
{
    private readonly IAppResources _resources;

    public SettingsView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        _resources = App.Services.GetRequiredService<IAppResources>();
        InitializeComponent();
        VersionText.Text = string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("VersionFormat"),
            GetAppVersion());
    }

    public MainViewModel ViewModel { get; }

    private static string GetAppVersion()
    {
        try
        {
            Windows.ApplicationModel.PackageVersion version = Windows.ApplicationModel.Package.Current.Id.Version;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return typeof(App).Assembly.GetName().Version?.ToString() ?? string.Empty;
        }
    }

    private async void OnDeleteHistoryClicked(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = ShellActions.CreateDialog(this);
        dialog.Title = _resources.GetString("DeleteHistoryDialogTitle");
        dialog.Content = new TextBlock
        {
            Text = _resources.GetString("DeleteHistoryDialogMessage"),
            TextWrapping = TextWrapping.Wrap,
        };
        dialog.PrimaryButtonText = _resources.GetString("DeleteHistoryDialogPrimary");
        dialog.CloseButtonText = _resources.GetString("DialogCancel");
        dialog.DefaultButton = ContentDialogButton.Close;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ClearHistoryCommand.ExecuteAsync(null);
        }
    }
}
