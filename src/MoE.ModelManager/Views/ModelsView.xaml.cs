using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager.Views;

/// <summary>
/// Lists every local model runtime with its publisher, status, and files.
/// </summary>
public sealed partial class ModelsView : UserControl
{
    public ModelsView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }
}
