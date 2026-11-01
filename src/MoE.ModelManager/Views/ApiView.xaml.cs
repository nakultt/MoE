using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager.Views;

/// <summary>
/// Shows the OpenAI-compatible endpoints, network access, and recent request activity.
/// </summary>
public sealed partial class ApiView : UserControl
{
    public ApiView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }

    private void OnCopyLocalUrlClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.CopyText(ViewModel.ApiBaseUrl);
    }

    private void OnCopyNetworkUrlClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.CopyText(ViewModel.NetworkApiBaseUrl);
    }
}
