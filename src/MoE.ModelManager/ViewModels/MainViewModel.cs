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
    private readonly IAudioModelCatalogService _audioModelCatalogService;
    private readonly IVideoModelCatalogService _videoModelCatalogService;
    private readonly IModelRuntimeService _runtimeService;
    private readonly IImageRuntimeService _imageRuntimeService;
    private readonly IAudioRuntimeService _audioRuntimeService;
    private readonly IVideoRuntimeService _videoRuntimeService;
    private readonly IOpenAiChatService _chatService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVideoGenerationService _videoGenerationService;
    private readonly IRequestTelemetryService _telemetryService;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _generationCancellation;
    private CancellationTokenSource? _imageGenerationCancellation;
    private CancellationTokenSource? _audioOperationCancellation;
    private CancellationTokenSource? _videoGenerationCancellation;
    private bool _isInitialized;

    internal MainViewModel(
        IApiProxyService apiProxyService,
        IAppResources resources,
        IModelCatalogService modelCatalogService,
        IImageModelCatalogService imageModelCatalogService,
        IAudioModelCatalogService audioModelCatalogService,
        IVideoModelCatalogService videoModelCatalogService,
        IModelRuntimeService runtimeService,
        IImageRuntimeService imageRuntimeService,
        IAudioRuntimeService audioRuntimeService,
        IVideoRuntimeService videoRuntimeService,
        IOpenAiChatService chatService,
        IImageGenerationService imageGenerationService,
        IVideoGenerationService videoGenerationService,
        IRequestTelemetryService telemetryService)
    {
        _apiProxyService = apiProxyService;
        _resources = resources;
        _modelCatalogService = modelCatalogService;
        _imageModelCatalogService = imageModelCatalogService;
        _audioModelCatalogService = audioModelCatalogService;
        _videoModelCatalogService = videoModelCatalogService;
        _runtimeService = runtimeService;
        _imageRuntimeService = imageRuntimeService;
        _audioRuntimeService = audioRuntimeService;
        _videoRuntimeService = videoRuntimeService;
        _chatService = chatService;
        _imageGenerationService = imageGenerationService;
        _videoGenerationService = videoGenerationService;
        _telemetryService = telemetryService;
        _uiContext = SynchronizationContext.Current;
        RuntimeStatusText = resources.GetString("StatusUnloaded");
        ImageRuntimeStatusText = resources.GetString("StatusUnloaded");
        SpeechToTextRuntimeStatusText = resources.GetString("StatusUnloaded");
        TextToSpeechRuntimeStatusText = resources.GetString("StatusUnloaded");
        VideoRuntimeStatusText = resources.GetString("StatusUnloaded");
        ApiStatusText = resources.GetString("ApiStopped");
        SelectedVideoPreset = VideoPresets[1];
    }

    public event Action<GeneratedImageResult>? ImageGenerated;

    public event Action<GeneratedSpeechResult>? SpeechGenerated;

    public event Action<GeneratedVideoResult>? VideoGenerated;

    public event Action? ChatImageAttachmentCleared;

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
    [NotifyCanExecuteChangedFor(nameof(LoadSpeechToTextModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(EjectSpeechToTextModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(TranscribeAudioCommand))]
    public partial RuntimeStatus SpeechToTextRuntimeStatus { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadTextToSpeechModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(EjectTextToSpeechModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SynthesizeSpeechCommand))]
    public partial RuntimeStatus TextToSpeechRuntimeStatus { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadVideoModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(EjectVideoModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateVideoCommand))]
    public partial RuntimeStatus VideoRuntimeStatus { get; set; }

    [ObservableProperty]
    public partial string RuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImageRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImageRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeechToTextRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeechToTextRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TextToSpeechRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TextToSpeechRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VideoRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VideoRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedChatImagePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedChatImageName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelGenerationCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelImageGenerationCommand))]
    public partial bool IsImageGenerating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranscribeAudioCommand))]
    [NotifyCanExecuteChangedFor(nameof(SynthesizeSpeechCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAudioOperationCommand))]
    public partial bool IsAudioOperating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelVideoGenerationCommand))]
    public partial bool IsVideoGenerating { get; set; }

    [ObservableProperty]
    public partial string ImagePrompt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedImageSize { get; set; } = "512x512";

    [ObservableProperty]
    public partial string GeneratedImagePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedImageSummary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranscribeAudioCommand))]
    public partial string SelectedAudioFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TranscriptionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TranscriptionSummary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SynthesizeSpeechCommand))]
    public partial string SpeechText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SynthesizeSpeechCommand))]
    public partial KokoroVoice? SelectedVoice { get; set; }

    [ObservableProperty]
    public partial double SpeechSpeed { get; set; } = 1d;

    [ObservableProperty]
    public partial string GeneratedSpeechPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedSpeechSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VideoPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateVideoCommand))]
    public partial VideoGenerationPreset? SelectedVideoPreset { get; set; }

    [ObservableProperty]
    public partial string GeneratedVideoPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedVideoSummary { get; set; } = string.Empty;

    public ObservableCollection<ModelDefinition> Models { get; } = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ObservableCollection<RequestMetric> RequestMetrics { get; } = [];

    public bool CanAttachChatImage => SelectedModel?.HasVision == true;

    public IReadOnlyList<string> ImageSizes { get; } = ["512x512", "768x768", "1024x1024"];

    public IReadOnlyList<VideoGenerationPreset> VideoPresets { get; } =
    [
        new()
        {
            DisplayName = "Turbo · 384×384 · 2 seconds",
            Width = 384,
            Height = 384,
            Frames = 25,
            FramesPerSecond = 12,
        },
        new()
        {
            DisplayName = "Balanced · 448×448 · 3 seconds",
            Width = 448,
            Height = 448,
            Frames = 49,
            FramesPerSecond = 16,
        },
        new()
        {
            DisplayName = "Quality · 512×512 · 5 seconds",
            Width = 512,
            Height = 512,
            Frames = 81,
            FramesPerSecond = 16,
        },
    ];

    public ImageModelDefinition? ImageModel { get; private set; }

    public VideoModelDefinition? VideoModel { get; private set; }

    public AudioModelDefinition? AudioModel { get; private set; }

    public string ApiBaseUrl => _apiProxyService.BaseUrl;

    public bool HasNoMetrics => RequestMetrics.Count == 0;

    public string SelectedModelPath => SelectedModel?.ModelPath ?? string.Empty;

    public string SelectedDescription => SelectedModel?.Description ?? string.Empty;

    public string ImageModelName => ImageModel?.DisplayName ?? string.Empty;

    public string ImageModelDescription => ImageModel?.Description ?? string.Empty;

    public string ImageModelPath => ImageModel?.DiffusionModelPath ?? string.Empty;

    public string SpeechToTextModelName => AudioModel is null ? string.Empty : "NVIDIA Parakeet TDT 0.6B v3";

    public string SpeechToTextModelDescription => AudioModel is null
        ? string.Empty
        : "Fast multilingual transcription with punctuation and timestamps.";

    public string SpeechToTextModelPath => AudioModel?.ParakeetModelPath ?? string.Empty;

    public string TextToSpeechModelName => AudioModel is null ? string.Empty : "Kokoro 82M";

    public string TextToSpeechModelDescription => AudioModel is null
        ? string.Empty
        : "Natural local speech generation with selectable voices and speed.";

    public string TextToSpeechModelPath => AudioModel?.KokoroModelPath ?? string.Empty;

    public IReadOnlyList<KokoroVoice> Voices => AudioModel?.Voices ?? [];

    public string VideoModelName => VideoModel?.DisplayName ?? string.Empty;

    public string VideoModelDescription => VideoModel?.Description ?? string.Empty;

    public string VideoModelPath => VideoModel?.ModelPath ?? string.Empty;

    public string SelectedMeasuredSpeed => SelectedModel is null
        ? string.Empty
        : string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("MeasuredSpeedFormat"),
            SelectedModel.MeasuredTokensPerSecond);

    public void AttachChatImage(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        if (!CanAttachChatImage)
        {
            throw new InvalidOperationException(_resources.GetString("ErrorModelDoesNotSupportImages"));
        }

        SelectedChatImagePath = imagePath;
        SelectedChatImageName = Path.GetFileName(imagePath);
    }

    public void ClearChatImageAttachment()
    {
        SelectedChatImagePath = string.Empty;
        SelectedChatImageName = string.Empty;
        ChatImageAttachmentCleared?.Invoke();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        _runtimeService.StateChanged += OnRuntimeStateChanged;
        _imageRuntimeService.StateChanged += OnImageRuntimeStateChanged;
        _audioRuntimeService.SpeechToTextStateChanged += OnSpeechToTextStateChanged;
        _audioRuntimeService.TextToSpeechStateChanged += OnTextToSpeechStateChanged;
        _videoRuntimeService.StateChanged += OnVideoRuntimeStateChanged;
        _telemetryService.MetricAdded += OnMetricAdded;
        IReadOnlyList<ModelDefinition> models = await _modelCatalogService.LoadAsync(cancellationToken);
        foreach (ModelDefinition model in models)
        {
            Models.Add(model);
        }

        SelectedModel = Models.FirstOrDefault();
        ImageModel = await _imageModelCatalogService.LoadAsync(cancellationToken);
        AudioModel = await _audioModelCatalogService.LoadAsync(cancellationToken);
        VideoModel = await _videoModelCatalogService.LoadAsync(cancellationToken);
        OnPropertyChanged(nameof(ImageModelName));
        OnPropertyChanged(nameof(ImageModelDescription));
        OnPropertyChanged(nameof(ImageModelPath));
        OnPropertyChanged(nameof(SpeechToTextModelName));
        OnPropertyChanged(nameof(SpeechToTextModelDescription));
        OnPropertyChanged(nameof(SpeechToTextModelPath));
        OnPropertyChanged(nameof(TextToSpeechModelName));
        OnPropertyChanged(nameof(TextToSpeechModelDescription));
        OnPropertyChanged(nameof(TextToSpeechModelPath));
        OnPropertyChanged(nameof(Voices));
        SelectedVoice = Voices.Count > 0 ? Voices[0] : null;
        OnPropertyChanged(nameof(VideoModelName));
        OnPropertyChanged(nameof(VideoModelDescription));
        OnPropertyChanged(nameof(VideoModelPath));
        LoadImageModelCommand.NotifyCanExecuteChanged();
        LoadSpeechToTextModelCommand.NotifyCanExecuteChanged();
        LoadTextToSpeechModelCommand.NotifyCanExecuteChanged();
        LoadVideoModelCommand.NotifyCanExecuteChanged();
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
        _audioOperationCancellation?.Cancel();
        _audioOperationCancellation?.Dispose();
        _videoGenerationCancellation?.Cancel();
        _videoGenerationCancellation?.Dispose();
        _runtimeService.StateChanged -= OnRuntimeStateChanged;
        _imageRuntimeService.StateChanged -= OnImageRuntimeStateChanged;
        _audioRuntimeService.SpeechToTextStateChanged -= OnSpeechToTextStateChanged;
        _audioRuntimeService.TextToSpeechStateChanged -= OnTextToSpeechStateChanged;
        _videoRuntimeService.StateChanged -= OnVideoRuntimeStateChanged;
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
            _videoGenerationCancellation?.Cancel();
            await _imageRuntimeService.EjectAsync(CancellationToken.None);
            await _videoRuntimeService.EjectAsync(CancellationToken.None);
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

        string? imageDataUrl = null;
        string? imageFileName = null;
        if (!string.IsNullOrWhiteSpace(SelectedChatImagePath))
        {
            if (!model.HasVision)
            {
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Author = _resources.GetString("AuthorAssistant"),
                    Content = _resources.GetString("ErrorModelDoesNotSupportImages"),
                });
                return;
            }

            try
            {
                imageDataUrl = await ImageDataUrlFactory.CreateAsync(
                    SelectedChatImagePath,
                    CancellationToken.None);
                imageFileName = SelectedChatImageName;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Author = _resources.GetString("AuthorAssistant"),
                    Content = $"{_resources.GetString("ErrorChatImageFailed")} {exception.Message}",
                });
                return;
            }
        }

        ChatMessage userMessage = new()
        {
            Role = "user",
            Author = _resources.GetString("AuthorYou"),
            Content = prompt,
            ImageDataUrl = imageDataUrl,
            ImageFileName = imageFileName,
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
        ClearChatImageAttachment();
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
            _videoGenerationCancellation?.Cancel();
            await _runtimeService.EjectAsync(CancellationToken.None);
            await _videoRuntimeService.EjectAsync(CancellationToken.None);
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

    private bool CanLoadSpeechToTextModel()
    {
        return AudioModel is not null
            && SpeechToTextRuntimeStatus is RuntimeStatus.Unloaded or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanLoadSpeechToTextModel))]
    private async Task LoadSpeechToTextModelAsync()
    {
        AudioModelDefinition? model = AudioModel;
        if (model is null)
        {
            return;
        }

        SpeechToTextRuntimeDetail = string.Empty;
        try
        {
            await _audioRuntimeService.LoadSpeechToTextAsync(model, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            SpeechToTextRuntimeDetail = $"{_resources.GetString("ErrorSpeechToTextLoadFailed")} {exception.Message}";
        }
    }

    private bool CanEjectSpeechToTextModel()
    {
        return SpeechToTextRuntimeStatus is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanEjectSpeechToTextModel))]
    private async Task EjectSpeechToTextModelAsync()
    {
        _audioOperationCancellation?.Cancel();
        await _audioRuntimeService.EjectSpeechToTextAsync(CancellationToken.None);
    }

    private bool CanLoadTextToSpeechModel()
    {
        return AudioModel is not null
            && TextToSpeechRuntimeStatus is RuntimeStatus.Unloaded or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanLoadTextToSpeechModel))]
    private async Task LoadTextToSpeechModelAsync()
    {
        AudioModelDefinition? model = AudioModel;
        if (model is null)
        {
            return;
        }

        TextToSpeechRuntimeDetail = string.Empty;
        try
        {
            await _audioRuntimeService.LoadTextToSpeechAsync(model, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            TextToSpeechRuntimeDetail = $"{_resources.GetString("ErrorTextToSpeechLoadFailed")} {exception.Message}";
        }
    }

    private bool CanEjectTextToSpeechModel()
    {
        return TextToSpeechRuntimeStatus is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanEjectTextToSpeechModel))]
    private async Task EjectTextToSpeechModelAsync()
    {
        _audioOperationCancellation?.Cancel();
        await _audioRuntimeService.EjectTextToSpeechAsync(CancellationToken.None);
    }

    private bool CanTranscribeAudio()
    {
        return SpeechToTextRuntimeStatus == RuntimeStatus.Ready
            && !string.IsNullOrWhiteSpace(SelectedAudioFilePath)
            && !IsAudioOperating;
    }

    [RelayCommand(CanExecute = nameof(CanTranscribeAudio))]
    private async Task TranscribeAudioAsync()
    {
        IsAudioOperating = true;
        TranscriptionSummary = _resources.GetString("AudioTranscribing");
        _audioOperationCancellation = new CancellationTokenSource();
        try
        {
            AudioTranscriptionResult result = await _audioRuntimeService.TranscribeAsync(
                SelectedAudioFilePath,
                _audioOperationCancellation.Token);
            TranscriptionText = result.Text;
            double realtimeFactor = result.AudioDurationSeconds /
                Math.Max(result.DurationMilliseconds / 1000d, 0.001d);
            TranscriptionSummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("TranscriptionResultFormat"),
                result.DurationMilliseconds / 1000d,
                realtimeFactor);
        }
        catch (OperationCanceledException)
        {
            TranscriptionSummary = _resources.GetString("AudioOperationCanceled");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            TranscriptionSummary = $"{_resources.GetString("ErrorTranscriptionFailed")} {exception.Message}";
        }
        finally
        {
            _audioOperationCancellation.Dispose();
            _audioOperationCancellation = null;
            IsAudioOperating = false;
        }
    }

    private bool CanSynthesizeSpeech()
    {
        return TextToSpeechRuntimeStatus == RuntimeStatus.Ready
            && !string.IsNullOrWhiteSpace(SpeechText)
            && SelectedVoice is not null
            && !IsAudioOperating;
    }

    [RelayCommand(CanExecute = nameof(CanSynthesizeSpeech))]
    private async Task SynthesizeSpeechAsync()
    {
        KokoroVoice? voice = SelectedVoice;
        if (voice is null)
        {
            return;
        }

        IsAudioOperating = true;
        GeneratedSpeechSummary = _resources.GetString("SpeechGenerating");
        _audioOperationCancellation = new CancellationTokenSource();
        try
        {
            GeneratedSpeechResult result = await _audioRuntimeService.SynthesizeAsync(
                SpeechText.Trim(),
                voice,
                (float)SpeechSpeed,
                _audioOperationCancellation.Token);
            GeneratedSpeechPath = result.OutputPath;
            double realtimeFactor = result.AudioDurationSeconds /
                Math.Max(result.DurationMilliseconds / 1000d, 0.001d);
            GeneratedSpeechSummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("SpeechResultFormat"),
                result.DurationMilliseconds / 1000d,
                realtimeFactor);
            SpeechGenerated?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            GeneratedSpeechSummary = _resources.GetString("AudioOperationCanceled");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            GeneratedSpeechSummary = $"{_resources.GetString("ErrorSpeechGenerationFailed")} {exception.Message}";
        }
        finally
        {
            _audioOperationCancellation.Dispose();
            _audioOperationCancellation = null;
            IsAudioOperating = false;
        }
    }

    private bool CanCancelAudioOperation()
    {
        return IsAudioOperating;
    }

    [RelayCommand(CanExecute = nameof(CanCancelAudioOperation))]
    private void CancelAudioOperation()
    {
        _audioOperationCancellation?.Cancel();
    }

    private bool CanLoadVideoModel()
    {
        return VideoModel is not null
            && VideoRuntimeStatus is RuntimeStatus.Unloaded or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanLoadVideoModel))]
    private async Task LoadVideoModelAsync()
    {
        VideoModelDefinition? model = VideoModel;
        if (model is null)
        {
            return;
        }

        VideoRuntimeDetail = string.Empty;
        try
        {
            _generationCancellation?.Cancel();
            _imageGenerationCancellation?.Cancel();
            await _runtimeService.EjectAsync(CancellationToken.None);
            await _imageRuntimeService.EjectAsync(CancellationToken.None);
            await _videoRuntimeService.LoadAsync(model, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            VideoRuntimeDetail = $"{_resources.GetString("ErrorVideoLoadFailed")} {exception.Message}";
            ApplyVideoRuntimeSnapshot(new RuntimeSnapshot { Status = RuntimeStatus.Error, ModelId = model.Id });
        }
    }

    private bool CanEjectVideoModel()
    {
        return VideoRuntimeStatus is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Error;
    }

    [RelayCommand(CanExecute = nameof(CanEjectVideoModel))]
    private async Task EjectVideoModelAsync()
    {
        _videoGenerationCancellation?.Cancel();
        await _videoRuntimeService.EjectAsync(CancellationToken.None);
    }

    private bool CanGenerateVideo()
    {
        return VideoRuntimeStatus == RuntimeStatus.Ready
            && VideoModel is not null
            && SelectedVideoPreset is not null
            && !IsVideoGenerating;
    }

    [RelayCommand(CanExecute = nameof(CanGenerateVideo))]
    private async Task GenerateVideoAsync()
    {
        string prompt = VideoPrompt.Trim();
        VideoModelDefinition? model = VideoModel;
        VideoGenerationPreset? preset = SelectedVideoPreset;
        if (prompt.Length == 0 || model is null || preset is null)
        {
            return;
        }

        IsVideoGenerating = true;
        GeneratedVideoSummary = _resources.GetString("VideoGenerating");
        _videoGenerationCancellation = new CancellationTokenSource();
        try
        {
            GeneratedVideoResult result = await _videoGenerationService.GenerateAsync(
                model.Id,
                prompt,
                preset,
                _videoGenerationCancellation.Token);
            GeneratedVideoPath = result.OutputPath;
            GeneratedVideoSummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("VideoResultFormat"),
                result.DurationMilliseconds / 1000d);
            VideoGenerated?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            GeneratedVideoSummary = _resources.GetString("VideoGenerationCanceled");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or FormatException)
        {
            GeneratedVideoSummary = $"{_resources.GetString("ErrorVideoGenerationFailed")} {exception.Message}";
        }
        finally
        {
            _videoGenerationCancellation.Dispose();
            _videoGenerationCancellation = null;
            IsVideoGenerating = false;
        }
    }

    private bool CanCancelVideoGeneration()
    {
        return IsVideoGenerating;
    }

    [RelayCommand(CanExecute = nameof(CanCancelVideoGeneration))]
    private async Task CancelVideoGenerationAsync()
    {
        _videoGenerationCancellation?.Cancel();
        await _videoRuntimeService.EjectAsync(CancellationToken.None);
    }

    partial void OnSelectedModelChanged(ModelDefinition? value)
    {
        OnPropertyChanged(nameof(SelectedModelPath));
        OnPropertyChanged(nameof(SelectedDescription));
        OnPropertyChanged(nameof(SelectedMeasuredSpeed));
        OnPropertyChanged(nameof(CanAttachChatImage));
        if (value?.HasVision != true)
        {
            ClearChatImageAttachment();
        }
    }

    private void OnRuntimeStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyRuntimeSnapshot(snapshot));
    }

    private void OnImageRuntimeStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyImageRuntimeSnapshot(snapshot));
    }

    private void OnSpeechToTextStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplySpeechToTextSnapshot(snapshot));
    }

    private void OnTextToSpeechStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyTextToSpeechSnapshot(snapshot));
    }

    private void OnVideoRuntimeStateChanged(RuntimeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyVideoRuntimeSnapshot(snapshot));
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
        LoadVideoModelCommand.NotifyCanExecuteChanged();
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
        LoadVideoModelCommand.NotifyCanExecuteChanged();
    }

    private void ApplySpeechToTextSnapshot(RuntimeSnapshot snapshot)
    {
        SpeechToTextRuntimeStatus = snapshot.Status;
        SpeechToTextRuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            SpeechToTextRuntimeDetail = snapshot.Detail;
        }

        TranscribeAudioCommand.NotifyCanExecuteChanged();
    }

    private void ApplyTextToSpeechSnapshot(RuntimeSnapshot snapshot)
    {
        TextToSpeechRuntimeStatus = snapshot.Status;
        TextToSpeechRuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            TextToSpeechRuntimeDetail = snapshot.Detail;
        }

        SynthesizeSpeechCommand.NotifyCanExecuteChanged();
    }

    private void ApplyVideoRuntimeSnapshot(RuntimeSnapshot snapshot)
    {
        VideoRuntimeStatus = snapshot.Status;
        VideoRuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            VideoRuntimeDetail = snapshot.Detail;
        }

        GenerateVideoCommand.NotifyCanExecuteChanged();
        LoadModelCommand.NotifyCanExecuteChanged();
        LoadImageModelCommand.NotifyCanExecuteChanged();
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
