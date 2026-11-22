using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using MoE.ModelManager.Services;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager;

/// <summary>
/// Configures application services and owns the main window lifetime.
/// </summary>
public partial class App : Application
{
    private const string MainInstanceKey = "MoE.ModelManager.Main";
    private Window? _window;
    private DispatcherQueue? _dispatcherQueue;
    private bool _isShuttingDown;

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

    /// <summary>
    /// Ejects every runtime and exits the process. Safe to call more than once.
    /// </summary>
    internal async Task ExitAppAsync()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        try
        {
            await ShutdownAsync();
        }
        finally
        {
            Exit();
        }
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // One instance owns port 8081. A second launch hands its activation to the first and exits,
        // so opening the app while it runs headless shows a window instead of failing to bind the port.
        AppInstance mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        // The window no longer decides the app lifetime: headless mode has none, and closing the
        // window must finish ejecting runtimes before the process exits.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        mainInstance.Activated += OnMainInstanceActivated;
        Services.GetRequiredService<IModelControlService>().ShutdownRequested += OnShutdownRequested;
        if (ModelControlRules.IsHeadless(Environment.GetCommandLineArgs()))
        {
            await StartHeadlessAsync();
            return;
        }

        ShowMainWindow();
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
        services.AddSingleton<IModelControlService, ModelControlService>();
        services.AddSingleton<IApiProxyService, OpenAiProxyService>();
        services.AddSingleton<IOpenAiChatService, OpenAiChatService>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<IVideoGenerationService, VideoGenerationService>();
        services.AddSingleton<IChatHistoryService, ChatHistoryService>(_ => new ChatHistoryService());
        services.AddSingleton<IAppSettingsService, AppSettingsService>(_ => new AppSettingsService());

        // Every workspace binds to one shared view model so state survives navigation.
        services.AddSingleton(provider => new MainViewModel(
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
            provider.GetRequiredService<IRequestTelemetryService>(),
            provider.GetRequiredService<IChatHistoryService>(),
            provider.GetRequiredService<IAppSettingsService>()));
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
        WriteLog("app-crash.log", e.Exception.ToString());
    }

    private static void WriteLog(string fileName, string text)
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Logs");
        Directory.CreateDirectory(logDirectory);
        File.WriteAllText(Path.Combine(logDirectory, fileName), text);
    }

    private void ShowMainWindow()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _window ??= new MainWindow();
        _window.Activate();
    }

    private async Task StartHeadlessAsync()
    {
        try
        {
            await Services.GetRequiredService<IApiProxyService>().StartAsync(CancellationToken.None);
        }
        catch (IOException exception)
        {
            WriteLog("headless.log", exception.ToString());
            await ExitAppAsync();
        }
    }

    private void OnMainInstanceActivated(object? sender, AppActivationArguments args)
    {
        bool isHeadless = args.Kind == ExtendedActivationKind.Launch
            && args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch
            && ModelControlRules.IsHeadless(launch.Arguments);
        _dispatcherQueue?.TryEnqueue(async () =>
        {
            if (isHeadless)
            {
                await StartHeadlessAsync();
            }
            else
            {
                ShowMainWindow();
            }
        });
    }

    private void OnShutdownRequested()
    {
        _dispatcherQueue?.TryEnqueue(async () => await ExitAppAsync());
    }
}
