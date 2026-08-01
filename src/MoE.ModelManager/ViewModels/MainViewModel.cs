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
    private readonly IModelRuntimeService _runtimeService;
    private readonly IOpenAiChatService _chatService;
    private readonly IRequestTelemetryService _telemetryService;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _generationCancellation;
    private bool _isInitialized;

    internal MainViewModel(
        IApiProxyService apiProxyService,
        IAppResources resources,
        IModelCatalogService modelCatalogService,
        IModelRuntimeService runtimeService,
        IOpenAiChatService chatService,
        IRequestTelemetryService telemetryService)
    {
        _apiProxyService = apiProxyService;
        _resources = resources;
        _modelCatalogService = modelCatalogService;
        _runtimeService = runtimeService;
        _chatService = chatService;
        _telemetryService = telemetryService;
        _uiContext = SynchronizationContext.Current;
        RuntimeStatusText = resources.GetString("StatusUnloaded");
        ApiStatusText = resources.GetString("ApiStopped");
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    public partial ModelDefinition? SelectedModel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(EjectModelCommand))]
    public partial RuntimeStatus RuntimeStatus { get; set; }

    [ObservableProperty]
    public partial string RuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelGenerationCommand))]
    public partial bool IsGenerating { get; set; }

    public ObservableCollection<ModelDefinition> Models { get; } = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ObservableCollection<RequestMetric> RequestMetrics { get; } = [];

    public string ApiBaseUrl => _apiProxyService.BaseUrl;

    public bool HasNoMetrics => RequestMetrics.Count == 0;

    public string SelectedModelPath => SelectedModel?.ModelPath ?? string.Empty;

    public string SelectedDescription => SelectedModel?.Description ?? string.Empty;

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
        _telemetryService.MetricAdded += OnMetricAdded;
        IReadOnlyList<ModelDefinition> models = await _modelCatalogService.LoadAsync(cancellationToken);
        foreach (ModelDefinition model in models)
        {
            Models.Add(model);
        }

        SelectedModel = Models.FirstOrDefault();
        await _apiProxyService.StartAsync(cancellationToken);
        ApiStatusText = _resources.GetString("ApiReady");
        _isInitialized = true;
    }

    public void Dispose()
    {
        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        _runtimeService.StateChanged -= OnRuntimeStateChanged;
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

    private void ApplyRuntimeSnapshot(RuntimeSnapshot snapshot)
    {
        RuntimeStatus = snapshot.Status;
        RuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            RuntimeDetail = snapshot.Detail;
        }

        SendMessageCommand.NotifyCanExecuteChanged();
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
