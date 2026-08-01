using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager;

/// <summary>
/// Hosts model controls, chat, and live request telemetry.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ViewModel.Messages.CollectionChanged += OnMessagesChanged;
    }

    public MainViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            ViewModel.RuntimeDetail = exception.Message;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Messages.CollectionChanged -= OnMessagesChanged;
        ViewModel.Dispose();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.Messages.Count > 0)
        {
            ChatList.ScrollIntoView(ViewModel.Messages[^1]);
        }
    }
}
