using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MoE.ModelManager.Services;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager;

/// <summary>
/// Configures application services and owns the main window lifetime.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the application and its dependency container.
    /// </summary>
    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
        Services = ConfigureServices();
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    internal static IServiceProvider Services { get; private set; } = null!;

    internal Window? MainWindow => _window;

    internal static async Task ShutdownAsync()
    {
        IApiProxyService proxy = Services.GetRequiredService<IApiProxyService>();
        IModelRuntimeService runtime = Services.GetRequiredService<IModelRuntimeService>();
        IImageRuntimeService imageRuntime = Services.GetRequiredService<IImageRuntimeService>();
        IAudioRuntimeService audioRuntime = Services.GetRequiredService<IAudioRuntimeService>();
        IVideoRuntimeService videoRuntime = Services.GetRequiredService<IVideoRuntimeService>();
        await proxy.StopAsync(CancellationToken.None);
        await runtime.EjectAsync(CancellationToken.None);
        await imageRuntime.EjectAsync(CancellationToken.None);
        await audioRuntime.EjectSpeechToTextAsync(CancellationToken.None);
        await audioRuntime.EjectTextToSpeechAsync(CancellationToken.None);
        await videoRuntime.EjectAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();
        services.AddHttpClient("LlamaBackend", client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient("ImageBackend", client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient("VideoBackend", client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient("LocalApi", client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<IAppResources, AppResources>();
        services.AddSingleton<IModelCatalogService, ModelCatalogService>();
        services.AddSingleton<IImageModelCatalogService, ImageModelCatalogService>();
        services.AddSingleton<IAudioModelCatalogService, AudioModelCatalogService>();
        services.AddSingleton<IVideoModelCatalogService, VideoModelCatalogService>();
        services.AddSingleton<IModelRuntimeService, ModelRuntimeService>();
        services.AddSingleton<IImageRuntimeService, ImageRuntimeService>();
        services.AddSingleton<IAudioRuntimeService, AudioRuntimeService>();
        services.AddSingleton<IVideoRuntimeService, VideoRuntimeService>();
        services.AddSingleton<IRequestTelemetryService, RequestTelemetryService>();
        services.AddSingleton<IApiProxyService, OpenAiProxyService>();
        services.AddSingleton<IOpenAiChatService, OpenAiChatService>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<IVideoGenerationService, VideoGenerationService>();
        services.AddTransient(provider => new MainViewModel(
            provider.GetRequiredService<IApiProxyService>(),
            provider.GetRequiredService<IAppResources>(),
            provider.GetRequiredService<IModelCatalogService>(),
            provider.GetRequiredService<IImageModelCatalogService>(),
            provider.GetRequiredService<IAudioModelCatalogService>(),
            provider.GetRequiredService<IVideoModelCatalogService>(),
            provider.GetRequiredService<IModelRuntimeService>(),
            provider.GetRequiredService<IImageRuntimeService>(),
            provider.GetRequiredService<IAudioRuntimeService>(),
            provider.GetRequiredService<IVideoRuntimeService>(),
            provider.GetRequiredService<IOpenAiChatService>(),
            provider.GetRequiredService<IImageGenerationService>(),
            provider.GetRequiredService<IVideoGenerationService>(),
            provider.GetRequiredService<IRequestTelemetryService>()));
        return services.BuildServiceProvider();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (Services.GetService<IModelRuntimeService>() is ModelRuntimeService runtime)
        {
            runtime.StopImmediately();
        }

        if (Services.GetService<IImageRuntimeService>() is ImageRuntimeService imageRuntime)
        {
            imageRuntime.StopImmediately();
        }

        if (Services.GetService<IAudioRuntimeService>() is AudioRuntimeService audioRuntime)
        {
            audioRuntime.StopImmediately();
        }

        if (Services.GetService<IVideoRuntimeService>() is VideoRuntimeService videoRuntime)
        {
            videoRuntime.StopImmediately();
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Logs");
        Directory.CreateDirectory(logDirectory);
        File.WriteAllText(Path.Combine(logDirectory, "app-crash.log"), e.Exception.ToString());
    }
}
