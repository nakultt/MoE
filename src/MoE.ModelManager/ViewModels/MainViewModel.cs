using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IApiProxyService _apiProxyService;
    private readonly IAppResources _resources;
    private readonly IModelCatalogService _modelCatalogService;
    private readonly IImageModelCatalogService _imageModelCatalogService;
    private readonly IModelRuntimeService _runtimeService;
    private readonly IImageRuntimeService _imageRuntimeService;
    private readonly IOpenAiChatService _chatService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IRequestTelemetryService _telemetryService;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _generationCancellation;
    private CancellationTokenSource? _imageGenerationCancellation;
    private bool _isInitialized;

    internal MainViewModel(
        IApiProxyService apiProxyService,
        IAppResources resources,
        IModelCatalogService modelCatalogService,
        IImageModelCatalogService imageModelCatalogService,
        IModelRuntimeService runtimeService,
        IImageRuntimeService imageRuntimeService,
        IOpenAiChatService chatService,
        IImageGenerationService imageGenerationService,
        IRequestTelemetryService telemetryService)
    {
        _apiProxyService = apiProxyService;
        _resources = resources;
        _modelCatalogService = modelCatalogService;
        _imageModelCatalogService = imageModelCatalogService;
        _runtimeService = runtimeService;
        _imageRuntimeService = imageRuntimeService;
        _chatService = chatService;
        _imageGenerationService = imageGenerationService;
        _telemetryService = telemetryService;
        _uiContext = SynchronizationContext.Current;
        RuntimeStatusText = resources.GetString("StatusUnloaded");
        ImageRuntimeStatusText = resources.GetString("StatusUnloaded");
        ApiStatusText = resources.GetString("ApiStopped");
    }

    public event Action<GeneratedImageResult>? ImageGenerated;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    public partial ModelDefinition? SelectedModel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(EjectModelCommand))]
    public partial RuntimeStatus RuntimeStatus { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadImageModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(EjectImageModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateImageCommand))]
    public partial RuntimeStatus ImageRuntimeStatus { get; set; }

    [ObservableProperty]
    public partial string RuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImageRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImageRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelGenerationCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelImageGenerationCommand))]
    public partial bool IsImageGenerating { get; set; }

    [ObservableProperty]
    public partial string ImagePrompt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedImageSize { get; set; } = "512x512";

    [ObservableProperty]
    public partial string GeneratedImagePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedImageSummary { get; set; } = string.Empty;

    public ObservableCollection<ModelDefinition> Models { get; } = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ObservableCollection<RequestMetric> RequestMetrics { get; } = [];

    public IReadOnlyList<string> ImageSizes { get; } = ["512x512", "768x768", "1024x1024"];

    public ImageModelDefinition? ImageModel { get; private set; }

    public string ApiBaseUrl => _apiProxyService.BaseUrl;

    public bool HasNoMetrics => RequestMetrics.Count == 0;

    public string SelectedModelPath => SelectedModel?.ModelPath ?? string.Empty;

    public string SelectedDescription => SelectedModel?.Description ?? string.Empty;

    public string ImageModelName => ImageModel?.DisplayName ?? string.Empty;

    public string ImageModelDescription => ImageModel?.Description ?? string.Empty;

    public string ImageModelPath => ImageModel?.DiffusionModelPath ?? string.Empty;

    public string SelectedMeasuredSpeed => SelectedModel is null
        ? string.Empty
        : string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("MeasuredSpeedFormat"),
            SelectedModel.MeasuredTokensPerSecond);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        _runtimeService.StateChanged += OnRuntimeStateChanged;
        _imageRuntimeService.StateChanged += OnImageRuntimeStateChanged;
        _telemetryService.MetricAdded += OnMetricAdded;
        IReadOnlyList<ModelDefinition> models = await _modelCatalogService.LoadAsync(cancellationToken);
        foreach (ModelDefinition model in models)
        {
            Models.Add(model);
        }

        SelectedModel = Models.FirstOrDefault();
        ImageModel = await _imageModelCatalogService.LoadAsync(cancellationToken);
        OnPropertyChanged(nameof(ImageModelName));
        OnPropertyChanged(nameof(ImageModelDescription));
        OnPropertyChanged(nameof(ImageModelPath));
        LoadImageModelCommand.NotifyCanExecuteChanged();
        await _apiProxyService.StartAsync(cancellationToken);
        ApiStatusText = _resources.GetString("ApiReady");
        _isInitialized = true;
    }

    public void Dispose()
    {
        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        _imageGenerationCancellation?.Cancel();
        _imageGenerationCancellation?.Dispose();
        _runtimeService.StateChanged -= OnRuntimeStateChanged;
        _imageRuntimeService.StateChanged -= OnImageRuntimeStateChanged;
        _telemetryService.MetricAdded -= OnMetricAdded;
    }

    private bool CanLoadModel()
    {
        return SelectedModel is not null
            && RuntimeStatus is RuntimeStatus.Unloaded or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanLoadModel))]
    private async Task LoadModelAsync()
    {
        ModelDefinition? model = SelectedModel;
        if (model is null)
        {
            return;
        }

        RuntimeDetail = string.Empty;
        try
        {
            _imageGenerationCancellation?.Cancel();
            await _imageRuntimeService.EjectAsync(CancellationToken.None);
            await _runtimeService.LoadAsync(model, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            RuntimeDetail = $"{_resources.GetString("ErrorLoadFailed")} {exception.Message}";
            ApplyRuntimeSnapshot(new RuntimeSnapshot { Status = RuntimeStatus.Error, ModelId = model.Id });
        }
    }

    private bool CanEjectModel()
    {
        return RuntimeStatus is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanEjectModel))]
    private async Task EjectModelAsync()
    {
        _generationCancellation?.Cancel();
        await _runtimeService.EjectAsync(CancellationToken.None);
    }

    private bool CanSendMessage()
    {
        return RuntimeStatus == RuntimeStatus.Ready
            && SelectedModel is not null
            && !IsGenerating;
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        string prompt = UserPrompt.Trim();
        ModelDefinition? model = SelectedModel;
        if (prompt.Length == 0 || model is null)
        {
            return;
        }

        ChatMessage userMessage = new()
        {
            Role = "user",
            Author = _resources.GetString("AuthorYou"),
            Content = prompt,
        };
        Messages.Add(userMessage);
        List<ChatMessage> requestMessages = Messages.ToList();
        ChatMessage assistantMessage = new()
        {
            Role = "assistant",
            Author = _resources.GetString("AuthorAssistant"),
        };
        Messages.Add(assistantMessage);
        UserPrompt = string.Empty;
        IsGenerating = true;
        _generationCancellation = new CancellationTokenSource();

        try
        {
            await foreach (string chunk in _chatService.StreamResponseAsync(
                model.Id,
                requestMessages,
                _generationCancellation.Token))
            {
                assistantMessage.Content += chunk;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException exception)
        {
            assistantMessage.Content = $"{_resources.GetString("ErrorChatFailed")} {exception.Message}";
        }
        finally
        {
            _generationCancellation.Dispose();
            _generationCancellation = null;
            IsGenerating = false;
        }
    }

    private bool CanCancelGeneration()
    {
        return IsGenerating;
    }

    [RelayCommand(CanExecute = nameof(CanCancelGeneration))]
    private void CancelGeneration()
    {
        _generationCancellation?.Cancel();
    }

    private bool CanLoadImageModel()
    {
        return ImageModel is not null
            && ImageRuntimeStatus is RuntimeStatus.Unloaded or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanLoadImageModel))]
    private async Task LoadImageModelAsync()
    {
        ImageModelDefinition? model = ImageModel;
        if (model is null)
        {
            return;
        }

        ImageRuntimeDetail = string.Empty;
        try
        {
            _generationCancellation?.Cancel();
            await _runtimeService.EjectAsync(CancellationToken.None);
            await _imageRuntimeService.LoadAsync(model, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            ImageRuntimeDetail = $"{_resources.GetString("ErrorImageLoadFailed")} {exception.Message}";
            ApplyImageRuntimeSnapshot(new RuntimeSnapshot { Status = RuntimeStatus.Error, ModelId = model.Id });
        }
    }

    private bool CanEjectImageModel()
    {
        return ImageRuntimeStatus is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanEjectImageModel))]
    private async Task EjectImageModelAsync()
    {
        _imageGenerationCancellation?.Cancel();
        await _imageRuntimeService.EjectAsync(CancellationToken.None);
    }

    private bool CanGenerateImage()
    {
        return ImageRuntimeStatus == RuntimeStatus.Ready
            && ImageModel is not null
            && !IsImageGenerating;
    }

    [RelayCommand(CanExecute = nameof(CanGenerateImage))]
    private async Task GenerateImageAsync()
    {
        string prompt = ImagePrompt.Trim();
        ImageModelDefinition? model = ImageModel;
        if (prompt.Length == 0 || model is null)
        {
            return;
        }

        IsImageGenerating = true;
        GeneratedImageSummary = _resources.GetString("ImageGenerating");
        _imageGenerationCancellation = new CancellationTokenSource();
        try
        {
            GeneratedImageResult result = await _imageGenerationService.GenerateAsync(
                model.Id,
                prompt,
                SelectedImageSize,
                _imageGenerationCancellation.Token);
            GeneratedImagePath = result.OutputPath;
            GeneratedImageSummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("ImageResultFormat"),
                result.DurationMilliseconds / 1000d);
            ImageGenerated?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            GeneratedImageSummary = _resources.GetString("ImageGenerationCanceled");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or FormatException)
        {
            GeneratedImageSummary = $"{_resources.GetString("ErrorImageGenerationFailed")} {exception.Message}";
        }
        finally
        {
            _imageGenerationCancellation.Dispose();
            _imageGenerationCancellation = null;
            IsImageGenerating = false;
        }
    }

    private bool CanCancelImageGeneration()
    {
        return IsImageGenerating;
    }

    [RelayCommand(CanExecute = nameof(CanCancelImageGeneration))]
    private void CancelImageGeneration()
    {
        _imageGenerationCancellation?.Cancel();
    }

    partial void OnSelectedModelChanged(ModelDefinition? value)
    {
        OnPropertyChanged(nameof(SelectedModelPath));
        OnPropertyChanged(nameof(SelectedDescription));
        OnPropertyChanged(nameof(SelectedMeasuredSpeed));
    }

    private void OnRuntimeStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyRuntimeSnapshot(snapshot));
    }

    private void OnImageRuntimeStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyImageRuntimeSnapshot(snapshot));
    }

    private void ApplyRuntimeSnapshot(RuntimeSnapshot snapshot)
    {
        RuntimeStatus = snapshot.Status;
        RuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            RuntimeDetail = snapshot.Detail;
        }

        SendMessageCommand.NotifyCanExecuteChanged();
        LoadImageModelCommand.NotifyCanExecuteChanged();
    }

    private void ApplyImageRuntimeSnapshot(RuntimeSnapshot snapshot)
    {
        ImageRuntimeStatus = snapshot.Status;
        ImageRuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            ImageRuntimeDetail = snapshot.Detail;
        }

        GenerateImageCommand.NotifyCanExecuteChanged();
        LoadModelCommand.NotifyCanExecuteChanged();
    }

    private void OnMetricAdded(RequestMetric metric)
    {
        RunOnUiThread(() =>
        {
            RequestMetrics.Insert(0, metric);
            while (RequestMetrics.Count > 100)
            {
                RequestMetrics.RemoveAt(RequestMetrics.Count - 1);
            }

            OnPropertyChanged(nameof(HasNoMetrics));
        });
    }

    private void RunOnUiThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
    }
}
