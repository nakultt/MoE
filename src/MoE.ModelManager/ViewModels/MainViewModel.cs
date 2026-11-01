using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// The default thinking-token budget applied to reasoning-capable models.
    /// </summary>
    /// <remarks>
    /// An unbounded thinking channel can consume the entire response, leaving empty
    /// visible content, so a budget is applied by default rather than opted into.
    /// </remarks>
    public const double DefaultThinkingBudgetTokens = 512d;

    /// <summary>
    /// The highest selectable thinking-token budget.
    /// </summary>
    public const double MaximumThinkingBudgetTokens = 4096d;

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
    private readonly IChatHistoryService _chatHistoryService;
    private readonly IAppSettingsService _settingsService;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _generationCancellation;
    private CancellationTokenSource? _imageGenerationCancellation;
    private CancellationTokenSource? _audioOperationCancellation;
    private CancellationTokenSource? _videoGenerationCancellation;
    private ChatConversation? _currentConversation;
    private string _loadedModelId = string.Empty;
    private int _chatSessionVersion;
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
        IRequestTelemetryService telemetryService,
        IChatHistoryService chatHistoryService,
        IAppSettingsService settingsService)
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
        _chatHistoryService = chatHistoryService;
        _settingsService = settingsService;
        _uiContext = SynchronizationContext.Current;
        RuntimeStatusText = resources.GetString("StatusUnloaded");
        ImageRuntimeStatusText = resources.GetString("StatusUnloaded");
        SpeechToTextRuntimeStatusText = resources.GetString("StatusUnloaded");
        TextToSpeechRuntimeStatusText = resources.GetString("StatusUnloaded");
        VideoRuntimeStatusText = resources.GetString("StatusUnloaded");
        ApiStatusText = resources.GetString("ApiStopped");
        VideoPresets =
        [
            new()
            {
                DisplayName = resources.GetString("VideoPresetTurbo"),
                Width = 384,
                Height = 384,
                Frames = 25,
                FramesPerSecond = 12,
            },
            new()
            {
                DisplayName = resources.GetString("VideoPresetBalanced"),
                Width = 448,
                Height = 448,
                Frames = 49,
                FramesPerSecond = 16,
            },
            new()
            {
                DisplayName = resources.GetString("VideoPresetQuality"),
                Width = 512,
                Height = 512,
                Frames = 81,
                FramesPerSecond = 16,
            },
        ];
        SelectedVideoPreset = VideoPresets[1];
        IsChatHistoryEnabled = settingsService.Current.SaveChatHistory;
        ThemeIndex = settingsService.Current.Theme switch
        {
            AppSettings.LightTheme => 1,
            AppSettings.DarkTheme => 2,
            _ => 0,
        };
        Messages.CollectionChanged += OnMessagesCollectionChanged;
        Conversations.CollectionChanged += OnConversationsCollectionChanged;
    }

    public event Action<GeneratedImageResult>? ImageGenerated;

    public event Action<GeneratedSpeechResult>? SpeechGenerated;

    public event Action<GeneratedVideoResult>? VideoGenerated;

    public event Action? ChatAttachmentCleared;

    /// <summary>
    /// Occurs when the user picks a different app theme.
    /// </summary>
    public event Action<string>? ThemeChanged;

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
    [NotifyPropertyChangedFor(nameof(HasRuntimeDetail))]
    public partial string RuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImageRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImageRuntimeDetail))]
    public partial string ImageRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeechToTextRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeechToTextRuntimeDetail))]
    public partial string SpeechToTextRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TextToSpeechRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTextToSpeechRuntimeDetail))]
    public partial string TextToSpeechRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VideoRuntimeStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVideoRuntimeDetail))]
    public partial string VideoRuntimeDetail { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApiListening))]
    public partial string ApiStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetNetworkAccessCommand))]
    public partial bool IsNetworkAccessEnabled { get; set; }

    [ObservableProperty]
    public partial string NetworkApiStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChatAttachment))]
    public partial string SelectedChatAttachmentPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedChatAttachmentName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedChatAttachmentType { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReasoningButtonText))]
    public partial bool IsReasoningEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReasoningButtonText))]
    [NotifyPropertyChangedFor(nameof(ThinkingBudgetText))]
    public partial double ThinkingBudgetTokens { get; set; } = DefaultThinkingBudgetTokens;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextUsageText))]
    [NotifyPropertyChangedFor(nameof(ContextUsageShortText))]
    [NotifyPropertyChangedFor(nameof(ContextUsagePercent))]
    public partial int ContextUsedTokens { get; set; }

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
    [NotifyPropertyChangedFor(nameof(HasGeneratedImage))]
    [NotifyPropertyChangedFor(nameof(GeneratedImageFileName))]
    public partial string GeneratedImagePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedImageSummary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranscribeAudioCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedAudioFile))]
    [NotifyPropertyChangedFor(nameof(SelectedAudioFileName))]
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
    [NotifyPropertyChangedFor(nameof(SpeechSpeedText))]
    public partial double SpeechSpeed { get; set; } = 1d;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGeneratedSpeech))]
    public partial string GeneratedSpeechPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedSpeechSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VideoPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateVideoCommand))]
    public partial VideoGenerationPreset? SelectedVideoPreset { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGeneratedVideo))]
    [NotifyPropertyChangedFor(nameof(GeneratedVideoFileName))]
    public partial string GeneratedVideoPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneratedVideoSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether chat conversations are saved on this PC.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChatHistoryEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the chat history pane is shown.
    /// </summary>
    [ObservableProperty]
    public partial bool IsHistoryPaneOpen { get; set; } = true;

    /// <summary>
    /// Gets or sets the history entry for the conversation currently on screen.
    /// </summary>
    [ObservableProperty]
    public partial ChatConversationSummary? CurrentConversationSummary { get; set; }

    /// <summary>
    /// Gets or sets the last chat-history error, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistoryError))]
    public partial string HistoryErrorText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected theme: 0 follows Windows, 1 is light, 2 is dark.
    /// </summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    public ObservableCollection<ModelDefinition> Models { get; } = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ObservableCollection<RequestMetric> RequestMetrics { get; } = [];

    /// <summary>
    /// Gets the saved conversations, newest first.
    /// </summary>
    public ObservableCollection<ChatConversationSummary> Conversations { get; } = [];

    public bool CanAttachChatMedia => SelectedModel?.HasMultimodalInput == true;

    public bool CanConfigureReasoning => SelectedModel?.SupportsReasoning == true;

    public bool HasChatAttachment => !string.IsNullOrWhiteSpace(SelectedChatAttachmentPath);

    public bool HasNoMessages => Messages.Count == 0;

    public bool HasNoConversations => Conversations.Count == 0;

    public bool HasHistoryError => !string.IsNullOrWhiteSpace(HistoryErrorText);

    public string HistoryDirectory => _chatHistoryService.HistoryDirectory;

    /// <summary>
    /// Gets the loaded model's context window, in tokens.
    /// </summary>
    public int ContextLimitTokens => SelectedModel?.ContextSize ?? 0;

    /// <summary>
    /// Gets the share of the context window consumed by the conversation, from 0 to 100.
    /// </summary>
    public double ContextUsagePercent => ContextLimitTokens <= 0
        ? 0d
        : Math.Min(100d, ContextUsedTokens * 100d / ContextLimitTokens);

    /// <summary>
    /// Gets the full context usage description used for the tooltip and screen readers.
    /// </summary>
    public string ContextUsageText
    {
        get
        {
            if (ContextLimitTokens <= 0)
            {
                return _resources.GetString("ContextUsageEmpty");
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("ContextUsageFormat"),
                ContextUsedTokens,
                ContextLimitTokens,
                ContextUsagePercent);
        }
    }

    /// <summary>
    /// Gets the compact context usage caption shown in the message composer.
    /// </summary>
    public string ContextUsageShortText => ContextLimitTokens <= 0
        ? string.Empty
        : string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("ContextUsageShortFormat"),
            ContextUsedTokens,
            ContextLimitTokens);

    /// <summary>
    /// Gets the caption of the reasoning drop-down button.
    /// </summary>
    public string ReasoningButtonText
    {
        get
        {
            if (!CanConfigureReasoning)
            {
                return _resources.GetString("ReasoningButtonUnavailable");
            }

            return IsReasoningEnabled
                ? string.Format(CultureInfo.CurrentCulture, _resources.GetString("ReasoningButtonOnFormat"), ThinkingBudgetTokens)
                : _resources.GetString("ReasoningButtonOff");
        }
    }

    /// <summary>
    /// Gets the thinking budget caption shown beside the slider.
    /// </summary>
    public string ThinkingBudgetText => string.Format(
        CultureInfo.CurrentCulture,
        _resources.GetString("ThinkingBudgetValueFormat"),
        ThinkingBudgetTokens);

    /// <summary>
    /// Gets the placeholder shown in the message box.
    /// </summary>
    public string PromptPlaceholderText => SelectedModel is null
        ? _resources.GetString("PromptPlaceholderNoModel")
        : string.Format(CultureInfo.CurrentCulture, _resources.GetString("PromptPlaceholderFormat"), SelectedModel.DisplayName);

    public IReadOnlyList<string> ImageSizes { get; } = ["512x512", "768x768", "1024x1024"];

    public IReadOnlyList<VideoGenerationPreset> VideoPresets { get; }

    public ImageModelDefinition? ImageModel { get; private set; }

    public VideoModelDefinition? VideoModel { get; private set; }

    public AudioModelDefinition? AudioModel { get; private set; }

    public string ApiBaseUrl => _apiProxyService.BaseUrl;

    public string NetworkApiBaseUrl => _apiProxyService.NetworkBaseUrl;

    public bool IsApiListening => string.Equals(ApiStatusText, _resources.GetString("ApiReady"), StringComparison.Ordinal);

    public bool HasNoMetrics => RequestMetrics.Count == 0;

    public bool HasMetrics => RequestMetrics.Count > 0;

    public bool HasRuntimeDetail => !string.IsNullOrWhiteSpace(RuntimeDetail);

    public bool HasImageRuntimeDetail => !string.IsNullOrWhiteSpace(ImageRuntimeDetail);

    public bool HasVideoRuntimeDetail => !string.IsNullOrWhiteSpace(VideoRuntimeDetail);

    public bool HasSpeechToTextRuntimeDetail => !string.IsNullOrWhiteSpace(SpeechToTextRuntimeDetail);

    public bool HasTextToSpeechRuntimeDetail => !string.IsNullOrWhiteSpace(TextToSpeechRuntimeDetail);

    public bool HasGeneratedImage => !string.IsNullOrWhiteSpace(GeneratedImagePath);

    public string GeneratedImageFileName => Path.GetFileName(GeneratedImagePath);

    public bool HasGeneratedVideo => !string.IsNullOrWhiteSpace(GeneratedVideoPath);

    public string GeneratedVideoFileName => Path.GetFileName(GeneratedVideoPath);

    public bool HasGeneratedSpeech => !string.IsNullOrWhiteSpace(GeneratedSpeechPath);

    public bool HasSelectedAudioFile => !string.IsNullOrWhiteSpace(SelectedAudioFilePath);

    public string SelectedAudioFileName => Path.GetFileName(SelectedAudioFilePath);

    public string SpeechSpeedText => string.Format(CultureInfo.CurrentCulture, _resources.GetString("SpeechSpeedFormat"), SpeechSpeed);

    public string SelectedModelPath => SelectedModel?.ModelPath ?? string.Empty;

    public string SelectedVisionProjectorPath => SelectedModel?.VisionProjectorPath ?? string.Empty;

    public bool HasSelectedVisionProjector => SelectedModel?.HasVision == true;

    public string SelectedDraftModelPath => SelectedModel?.DraftModelPath ?? string.Empty;

    public bool HasSelectedDraftModel => SelectedModel?.HasDraftModel == true;

    public string SelectedDescription => SelectedModel?.Description ?? string.Empty;

    public string SelectedModelName => SelectedModel?.DisplayName ?? string.Empty;

    public Uri SelectedModelLogo => SelectedModel?.LogoUri ?? ModelBrandCatalog.FallbackLogo;

    public string SelectedModelPublisher => SelectedModel?.Publisher ?? string.Empty;

    /// <summary>
    /// Gets short capability tags for the selected language model.
    /// </summary>
    public IReadOnlyList<string> SelectedModelCapabilities
    {
        get
        {
            ModelDefinition? model = SelectedModel;
            if (model is null)
            {
                return [];
            }

            List<string> tags =
            [
                string.Format(CultureInfo.CurrentCulture, _resources.GetString("CapabilityContextFormat"), model.ContextSize / 1024),
            ];
            if (model.HasVision)
            {
                tags.Add(_resources.GetString("CapabilityVision"));
            }

            if (model.SupportsAudio)
            {
                tags.Add(_resources.GetString("CapabilityAudio"));
            }

            if (model.SupportsVideo)
            {
                tags.Add(_resources.GetString("CapabilityVideo"));
            }

            if (model.SupportsReasoning)
            {
                tags.Add(_resources.GetString("CapabilityReasoning"));
            }

            if (model.HasDraftModel)
            {
                tags.Add(_resources.GetString("CapabilitySpeculative"));
            }

            tags.Add(SelectedMeasuredSpeed);
            return tags;
        }
    }

    public string ImageModelName => ImageModel?.DisplayName ?? string.Empty;

    public string ImageModelDescription => ImageModel?.Description ?? string.Empty;

    public string ImageModelPath => ImageModel?.DiffusionModelPath ?? string.Empty;

    public string ImageTextEncoderPath => ImageModel?.TextEncoderPath ?? string.Empty;

    public string ImageVaePath => ImageModel?.VaePath ?? string.Empty;

    public Uri ImageModelLogo => ModelBrandCatalog.GetLogo(ImageModel?.Id);

    public string ImageModelPublisher => ModelBrandCatalog.GetPublisher(ImageModel?.Id);

    public string SpeechToTextModelName => AudioModel is null ? string.Empty : "NVIDIA Parakeet TDT 0.6B v3";

    public string SpeechToTextModelDescription => AudioModel is null
        ? string.Empty
        : _resources.GetString("SpeechToTextModelDescription");

    public string SpeechToTextModelPath => AudioModel?.ParakeetModelPath ?? string.Empty;

    public Uri SpeechToTextModelLogo => ModelBrandCatalog.GetLogo(AudioModel is null ? null : "parakeet");

    public string SpeechToTextModelPublisher => AudioModel is null ? string.Empty : ModelBrandCatalog.GetPublisher("parakeet");

    public string TextToSpeechModelName => AudioModel is null ? string.Empty : "Kokoro 82M";

    public string TextToSpeechModelDescription => AudioModel is null
        ? string.Empty
        : _resources.GetString("TextToSpeechModelDescription");

    public string TextToSpeechModelPath => AudioModel?.KokoroModelPath ?? string.Empty;

    public Uri TextToSpeechModelLogo => ModelBrandCatalog.GetLogo(AudioModel is null ? null : "kokoro");

    public string TextToSpeechModelPublisher => AudioModel is null ? string.Empty : ModelBrandCatalog.GetPublisher("kokoro");

    public IReadOnlyList<KokoroVoice> Voices => AudioModel?.Voices ?? [];

    public string VideoModelName => VideoModel?.DisplayName ?? string.Empty;

    public string VideoModelDescription => VideoModel?.Description ?? string.Empty;

    public string VideoModelPath => VideoModel?.ModelPath ?? string.Empty;

    public Uri VideoModelLogo => ModelBrandCatalog.GetLogo(VideoModel?.Id);

    public string VideoModelPublisher => ModelBrandCatalog.GetPublisher(VideoModel?.Id);

    public string SelectedMeasuredSpeed => SelectedModel is null
        ? string.Empty
        : string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("MeasuredSpeedFormat"),
            SelectedModel.MeasuredTokensPerSecond);

    /// <summary>
    /// Gets the display name of the language model that is currently loaded.
    /// </summary>
    public string LoadedModelName => Models.FirstOrDefault(model => model.Id == _loadedModelId)?.DisplayName
        ?? SelectedModel?.DisplayName
        ?? string.Empty;

    /// <summary>
    /// Gets the number of runtimes that are ready to serve requests.
    /// </summary>
    public int LoadedRuntimeCount => new[]
    {
        RuntimeStatus,
        ImageRuntimeStatus,
        VideoRuntimeStatus,
        SpeechToTextRuntimeStatus,
        TextToSpeechRuntimeStatus,
    }.Count(status => status == RuntimeStatus.Ready);

    public bool HasLoadedRuntimes => LoadedRuntimeCount > 0;

    /// <summary>
    /// Gets a comma-separated list of the models that are ready.
    /// </summary>
    public string LoadedModelsText
    {
        get
        {
            List<string> names = [];
            if (RuntimeStatus == RuntimeStatus.Ready)
            {
                names.Add(LoadedModelName);
            }

            if (ImageRuntimeStatus == RuntimeStatus.Ready)
            {
                names.Add(ImageModelName);
            }

            if (VideoRuntimeStatus == RuntimeStatus.Ready)
            {
                names.Add(VideoModelName);
            }

            if (SpeechToTextRuntimeStatus == RuntimeStatus.Ready)
            {
                names.Add(_resources.GetString("SpeechToTextShortName"));
            }

            if (TextToSpeechRuntimeStatus == RuntimeStatus.Ready)
            {
                names.Add(_resources.GetString("TextToSpeechShortName"));
            }

            return names.Count == 0
                ? _resources.GetString("NoModelsLoaded")
                : string.Join(CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ", names);
        }
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
        Messages.CollectionChanged -= OnMessagesCollectionChanged;
        Conversations.CollectionChanged -= OnConversationsCollectionChanged;
    }

    public void AttachChatMedia(string mediaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ModelDefinition? model = SelectedModel;
        if (model is null || !CanAttachChatMedia)
        {
            throw new InvalidOperationException(_resources.GetString("ErrorModelDoesNotSupportMedia"));
        }

        string extension = Path.GetExtension(mediaPath).ToLowerInvariant();
        bool isImage = extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif";
        bool isAudio = extension is ".wav" or ".mp3" or ".flac";
        bool isVideo = extension is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi";
        if ((isImage && !model.HasVision)
            || (isAudio && !model.SupportsAudio)
            || (isVideo && !model.SupportsVideo)
            || (!isImage && !isAudio && !isVideo))
        {
            throw new InvalidOperationException(_resources.GetString("ErrorModelDoesNotSupportMedia"));
        }

        SelectedChatAttachmentPath = mediaPath;
        SelectedChatAttachmentName = Path.GetFileName(mediaPath);
        SelectedChatAttachmentType = _resources.GetString(
            isVideo ? "AttachmentTypeVideo" : isAudio ? "AttachmentTypeAudio" : "AttachmentTypeImage");
    }

    public void ClearChatAttachment()
    {
        SelectedChatAttachmentPath = string.Empty;
        SelectedChatAttachmentName = string.Empty;
        SelectedChatAttachmentType = string.Empty;
        ChatAttachmentCleared?.Invoke();
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
        OnPropertyChanged(nameof(ImageTextEncoderPath));
        OnPropertyChanged(nameof(ImageVaePath));
        OnPropertyChanged(nameof(ImageModelLogo));
        OnPropertyChanged(nameof(ImageModelPublisher));
        OnPropertyChanged(nameof(SpeechToTextModelName));
        OnPropertyChanged(nameof(SpeechToTextModelDescription));
        OnPropertyChanged(nameof(SpeechToTextModelPath));
        OnPropertyChanged(nameof(SpeechToTextModelLogo));
        OnPropertyChanged(nameof(SpeechToTextModelPublisher));
        OnPropertyChanged(nameof(TextToSpeechModelName));
        OnPropertyChanged(nameof(TextToSpeechModelDescription));
        OnPropertyChanged(nameof(TextToSpeechModelPath));
        OnPropertyChanged(nameof(TextToSpeechModelLogo));
        OnPropertyChanged(nameof(TextToSpeechModelPublisher));
        OnPropertyChanged(nameof(Voices));
        SelectedVoice = Voices.Count > 0 ? Voices[0] : null;
        OnPropertyChanged(nameof(VideoModelName));
        OnPropertyChanged(nameof(VideoModelDescription));
        OnPropertyChanged(nameof(VideoModelPath));
        OnPropertyChanged(nameof(VideoModelLogo));
        OnPropertyChanged(nameof(VideoModelPublisher));
        LoadImageModelCommand.NotifyCanExecuteChanged();
        LoadSpeechToTextModelCommand.NotifyCanExecuteChanged();
        LoadTextToSpeechModelCommand.NotifyCanExecuteChanged();
        LoadVideoModelCommand.NotifyCanExecuteChanged();
        await LoadConversationsAsync(cancellationToken);
        await _apiProxyService.StartAsync(cancellationToken);
        ApiStatusText = _resources.GetString("ApiReady");
        IsNetworkAccessEnabled = _apiProxyService.IsNetworkAccessEnabled;
        NetworkApiStatusText = GetNetworkStatusText();
        _isInitialized = true;
    }

    /// <summary>
    /// Discards a reply and everything after it, then asks the model for a new answer.
    /// </summary>
    /// <param name="message">The assistant message to regenerate.</param>
    /// <returns>A task that completes when the new reply finishes streaming.</returns>
    public async Task RegenerateAsync(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ModelDefinition? model = SelectedModel;
        int index = Messages.IndexOf(message);
        if (model is null || !CanSendMessage() || !message.IsAssistant || index <= 0)
        {
            return;
        }

        while (Messages.Count > index)
        {
            Messages.RemoveAt(Messages.Count - 1);
        }

        await GenerateResponseAsync(model);
    }

    /// <summary>
    /// Renames a saved conversation.
    /// </summary>
    /// <param name="summary">The conversation to rename.</param>
    /// <param name="title">The new title.</param>
    /// <returns>A task that completes when the new title is saved.</returns>
    public async Task RenameConversationAsync(ChatConversationSummary summary, string title)
    {
        ArgumentNullException.ThrowIfNull(summary);
        string trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return;
        }

        try
        {
            ChatConversation? conversation = _currentConversation?.Id == summary.Id
                ? _currentConversation
                : await _chatHistoryService.LoadAsync(summary.Id, CancellationToken.None);
            if (conversation is null)
            {
                Conversations.Remove(summary);
                return;
            }

            conversation.Title = trimmed;
            await _chatHistoryService.SaveAsync(conversation, CancellationToken.None);
            ReplaceSummary(summary, CreateSummary(conversation));
            HistoryErrorText = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = $"{_resources.GetString("ErrorHistoryFailed")} {exception.Message}";
        }
    }

    /// <summary>
    /// Builds a Markdown transcript of a saved conversation.
    /// </summary>
    /// <param name="summary">The conversation to export.</param>
    /// <returns>The transcript, or <see langword="null"/> when the conversation no longer exists.</returns>
    public async Task<string?> ExportConversationMarkdownAsync(ChatConversationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ChatConversation? conversation = _currentConversation?.Id == summary.Id
            ? _currentConversation
            : await _chatHistoryService.LoadAsync(summary.Id, CancellationToken.None);
        return conversation is null
            ? null
            : ChatTranscriptFormatter.ToMarkdown(conversation, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Builds a Markdown transcript of the conversation on screen.
    /// </summary>
    /// <returns>The transcript and a suggested file name.</returns>
    public (string Markdown, string FileName) ExportCurrentChatMarkdown()
    {
        ChatConversation conversation = CreateConversationSnapshot(SelectedModel);
        return (
            ChatTranscriptFormatter.ToMarkdown(conversation, CultureInfo.CurrentCulture),
            ChatTranscriptFormatter.CreateFileName(conversation.Title));
    }

    private static ChatConversationSummary CreateSummary(ChatConversation conversation)
    {
        return new ChatConversationSummary
        {
            Id = conversation.Id,
            Title = conversation.Title,
            ModelName = conversation.ModelName,
            UpdatedAt = conversation.UpdatedAt,
        };
    }

    private static StoredChatMessage ToStored(ChatMessage message)
    {
        return new StoredChatMessage
        {
            Role = message.Role,
            Author = message.Author,
            Content = message.Content,
            Reasoning = message.Reasoning,
            ModelId = message.ModelId,
            AttachmentFileName = message.AttachmentFileName,
            AttachmentKind = message.AttachmentKind,
            Timestamp = message.Timestamp,
            TokensPerSecond = message.TokensPerSecond,
        };
    }

    private ChatMessage FromStored(StoredChatMessage stored)
    {
        ChatMessage message = new()
        {
            Role = stored.Role,
            Author = stored.Author,
            Content = stored.Content,
            Reasoning = stored.Reasoning,
            ModelId = stored.ModelId,
            AttachmentFileName = stored.AttachmentFileName,
            AttachmentKind = stored.AttachmentKind,
            Timestamp = stored.Timestamp,
        };
        SetMessageSpeed(message, stored.TokensPerSecond);
        return message;
    }

    private void SetMessageSpeed(ChatMessage message, double tokensPerSecond)
    {
        message.TokensPerSecond = tokensPerSecond > 0 ? tokensPerSecond : 0;
        message.SpeedText = tokensPerSecond > 0
            ? string.Format(CultureInfo.CurrentCulture, _resources.GetString("MessageSpeedFormat"), tokensPerSecond)
            : string.Empty;
    }

    [RelayCommand]
    private async Task SetNetworkAccessAsync()
    {
        try
        {
            await _apiProxyService.SetNetworkAccessAsync(IsNetworkAccessEnabled, CancellationToken.None);
            NetworkApiStatusText = GetNetworkStatusText();
            OnPropertyChanged(nameof(NetworkApiBaseUrl));
            ApiStatusText = _resources.GetString("ApiReady");
        }
        catch (Exception exception)
        {
            IsNetworkAccessEnabled = _apiProxyService.IsNetworkAccessEnabled;
            NetworkApiStatusText = exception.Message;
        }
    }

    [RelayCommand]
    private void NewChat()
    {
        _generationCancellation?.Cancel();
        _chatSessionVersion++;
        UserPrompt = string.Empty;
        ClearChatAttachment();
        Messages.Clear();
        ContextUsedTokens = 0;
        _currentConversation = null;
        CurrentConversationSummary = null;
    }

    [RelayCommand]
    private void ToggleHistoryPane()
    {
        IsHistoryPaneOpen = !IsHistoryPaneOpen;
    }

    [RelayCommand]
    private async Task OpenConversationAsync(ChatConversationSummary? summary)
    {
        if (summary is null || _currentConversation?.Id == summary.Id)
        {
            return;
        }

        ChatConversation? conversation;
        try
        {
            conversation = await _chatHistoryService.LoadAsync(summary.Id, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = $"{_resources.GetString("ErrorHistoryFailed")} {exception.Message}";
            return;
        }

        if (conversation is null)
        {
            Conversations.Remove(summary);
            return;
        }

        _generationCancellation?.Cancel();
        _chatSessionVersion++;
        UserPrompt = string.Empty;
        ClearChatAttachment();
        Messages.Clear();
        foreach (StoredChatMessage stored in conversation.Messages)
        {
            Messages.Add(FromStored(stored));
        }

        ContextUsedTokens = 0;
        _currentConversation = conversation;
        CurrentConversationSummary = summary;
        HistoryErrorText = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(ChatConversationSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        try
        {
            await _chatHistoryService.DeleteAsync(summary.Id, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = $"{_resources.GetString("ErrorHistoryFailed")} {exception.Message}";
            return;
        }

        Conversations.Remove(summary);
        if (_currentConversation?.Id == summary.Id)
        {
            NewChat();
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        try
        {
            await _chatHistoryService.DeleteAllAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = $"{_resources.GetString("ErrorHistoryFailed")} {exception.Message}";
            return;
        }

        Conversations.Clear();
        _currentConversation = null;
        CurrentConversationSummary = null;
        HistoryErrorText = string.Empty;
    }

    [RelayCommand]
    private void ClearActivity()
    {
        RequestMetrics.Clear();
        OnPropertyChanged(nameof(HasNoMetrics));
        OnPropertyChanged(nameof(HasMetrics));
    }

    private bool CanEjectAll()
    {
        return new[]
        {
            RuntimeStatus,
            ImageRuntimeStatus,
            VideoRuntimeStatus,
            SpeechToTextRuntimeStatus,
            TextToSpeechRuntimeStatus,
        }.Any(status => status is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Error);
    }

    [RelayCommand(CanExecute = nameof(CanEjectAll))]
    private async Task EjectAllAsync()
    {
        _generationCancellation?.Cancel();
        _imageGenerationCancellation?.Cancel();
        _videoGenerationCancellation?.Cancel();
        _audioOperationCancellation?.Cancel();
        await _runtimeService.EjectAsync(CancellationToken.None);
        await _imageRuntimeService.EjectAsync(CancellationToken.None);
        await _videoRuntimeService.EjectAsync(CancellationToken.None);
        await _audioRuntimeService.EjectSpeechToTextAsync(CancellationToken.None);
        await _audioRuntimeService.EjectTextToSpeechAsync(CancellationToken.None);
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

        MediaDataUrlFactory.MediaAttachment? attachment = null;
        if (!string.IsNullOrWhiteSpace(SelectedChatAttachmentPath))
        {
            try
            {
                attachment = await MediaDataUrlFactory.CreateAsync(
                    SelectedChatAttachmentPath,
                    CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Author = _resources.GetString("AuthorAssistant"),
                    Content = $"{_resources.GetString("ErrorChatMediaFailed")} {exception.Message}",
                });
                return;
            }
        }

        ChatMessage userMessage = new()
        {
            Role = "user",
            Author = _resources.GetString("AuthorYou"),
            Content = prompt,
            AttachmentDataUrl = attachment?.DataUrl,
            CompanionAudioDataUrl = attachment?.CompanionAudioDataUrl,
            VideoFrameDataUrls = attachment?.VideoFrameDataUrls,
            AttachmentFileName = attachment?.FileName,
            AttachmentKind = attachment?.ContentPartType,
        };
        Messages.Add(userMessage);
        UserPrompt = string.Empty;
        ClearChatAttachment();
        await GenerateResponseAsync(model);
    }

    private async Task GenerateResponseAsync(ModelDefinition model)
    {
        List<ChatMessage> requestMessages = Messages.ToList();
        ChatMessage assistantMessage = new()
        {
            Role = "assistant",
            Author = model.DisplayName,
            ModelId = model.Id,
        };
        Messages.Add(assistantMessage);
        int sessionVersion = _chatSessionVersion;
        IsGenerating = true;
        _generationCancellation = new CancellationTokenSource();

        // Decode speed comes from the runtime's timings; a stopped reply has none, so the
        // fallback counts streamed chunks (about one token each) from the first token onward.
        Stopwatch decodeStopwatch = new();
        int streamedChunks = 0;
        int completionTokens = 0;
        double reportedTokensPerSecond = 0;
        try
        {
            await foreach (ChatStreamDelta delta in _chatService.StreamResponseAsync(
                model.Id,
                requestMessages,
                model.SupportsReasoning ? IsReasoningEnabled : null,
                (int)ThinkingBudgetTokens,
                _generationCancellation.Token))
            {
                if (!string.IsNullOrEmpty(delta.Reasoning) || !string.IsNullOrEmpty(delta.Content))
                {
                    if (!decodeStopwatch.IsRunning)
                    {
                        decodeStopwatch.Start();
                    }
                    else
                    {
                        streamedChunks++;
                    }
                }

                if (!string.IsNullOrEmpty(delta.Reasoning))
                {
                    assistantMessage.Reasoning += delta.Reasoning;
                }

                if (!string.IsNullOrEmpty(delta.Content))
                {
                    assistantMessage.Content += delta.Content;
                }

                if (delta.HasUsage)
                {
                    ContextUsedTokens = delta.PromptTokens + delta.CompletionTokens;
                    completionTokens = delta.CompletionTokens;
                }

                if (delta.TokensPerSecond > 0)
                {
                    reportedTokensPerSecond = delta.TokensPerSecond;
                }
            }

            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                assistantMessage.Content = assistantMessage.HasReasoning
                    ? _resources.GetString("ErrorChatThinkingOnly")
                    : _resources.GetString("ErrorChatEmptyResponse");
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
            decodeStopwatch.Stop();
            _generationCancellation.Dispose();
            _generationCancellation = null;
            IsGenerating = false;
        }

        double decodeSeconds = decodeStopwatch.Elapsed.TotalSeconds;
        int decodedTokens = completionTokens > 1 ? completionTokens - 1 : streamedChunks;
        double tokensPerSecond = reportedTokensPerSecond > 0 ? reportedTokensPerSecond
            : decodeSeconds > 0 && decodedTokens > 0 ? decodedTokens / decodeSeconds
            : 0;
        SetMessageSpeed(assistantMessage, tokensPerSecond);

        // A new chat or a different saved conversation may have replaced this one while streaming.
        if (sessionVersion == _chatSessionVersion)
        {
            await SaveCurrentConversationAsync(model);
        }
    }

    private async Task SaveCurrentConversationAsync(ModelDefinition model)
    {
        if (!IsChatHistoryEnabled || Messages.Count == 0)
        {
            return;
        }

        ChatConversation conversation = _currentConversation ?? CreateConversationSnapshot(model);
        conversation.ModelId = model.Id;
        conversation.ModelName = model.DisplayName;
        conversation.UpdatedAt = DateTimeOffset.Now;
        conversation.Messages = Messages.Select(ToStored).ToList();
        try
        {
            await _chatHistoryService.SaveAsync(conversation, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = $"{_resources.GetString("ErrorHistoryFailed")} {exception.Message}";
            return;
        }

        _currentConversation = conversation;
        HistoryErrorText = string.Empty;
        ChatConversationSummary summary = CreateSummary(conversation);
        ChatConversationSummary? existing = Conversations.FirstOrDefault(item => item.Id == conversation.Id);
        if (existing is not null)
        {
            Conversations.Remove(existing);
        }

        Conversations.Insert(0, summary);
        CurrentConversationSummary = summary;
    }

    private ChatConversation CreateConversationSnapshot(ModelDefinition? model)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string firstPrompt = Messages.FirstOrDefault(message => !message.IsAssistant)?.Content ?? string.Empty;
        string title = ChatTranscriptFormatter.CreateTitle(firstPrompt);
        return new ChatConversation
        {
            Id = _currentConversation?.Id ?? ChatHistoryService.CreateId(),
            Title = _currentConversation?.Title ?? (title.Length == 0 ? _resources.GetString("UntitledChat") : title),
            ModelId = model?.Id ?? string.Empty,
            ModelName = model?.DisplayName ?? string.Empty,
            CreatedAt = _currentConversation?.CreatedAt ?? now,
            UpdatedAt = now,
            Messages = Messages.Select(ToStored).ToList(),
        };
    }

    private async Task LoadConversationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ChatConversationSummary> summaries = await _chatHistoryService.ListAsync(cancellationToken);
            Conversations.Clear();
            foreach (ChatConversationSummary summary in summaries)
            {
                Conversations.Add(summary);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = $"{_resources.GetString("ErrorHistoryFailed")} {exception.Message}";
        }
    }

    private void ReplaceSummary(ChatConversationSummary oldSummary, ChatConversationSummary newSummary)
    {
        int index = Conversations.IndexOf(oldSummary);
        if (index < 0)
        {
            index = Conversations.ToList().FindIndex(item => item.Id == oldSummary.Id);
        }

        if (index >= 0)
        {
            Conversations[index] = newSummary;
        }

        if (CurrentConversationSummary?.Id == newSummary.Id)
        {
            CurrentConversationSummary = newSummary;
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
        OnPropertyChanged(nameof(SelectedVisionProjectorPath));
        OnPropertyChanged(nameof(HasSelectedVisionProjector));
        OnPropertyChanged(nameof(SelectedDraftModelPath));
        OnPropertyChanged(nameof(HasSelectedDraftModel));
        OnPropertyChanged(nameof(SelectedDescription));
        OnPropertyChanged(nameof(SelectedModelName));
        OnPropertyChanged(nameof(SelectedModelLogo));
        OnPropertyChanged(nameof(SelectedModelPublisher));
        OnPropertyChanged(nameof(SelectedModelCapabilities));
        OnPropertyChanged(nameof(SelectedMeasuredSpeed));
        OnPropertyChanged(nameof(CanAttachChatMedia));
        OnPropertyChanged(nameof(CanConfigureReasoning));
        OnPropertyChanged(nameof(ReasoningButtonText));
        OnPropertyChanged(nameof(PromptPlaceholderText));
        OnPropertyChanged(nameof(ContextLimitTokens));
        OnPropertyChanged(nameof(ContextUsagePercent));
        OnPropertyChanged(nameof(ContextUsageText));
        OnPropertyChanged(nameof(ContextUsageShortText));
        if (value?.HasMultimodalInput != true)
        {
            ClearChatAttachment();
        }
    }

    partial void OnIsNetworkAccessEnabledChanged(bool value)
    {
        // The toggle applies immediately; the initial value from the service is not a user change.
        if (_isInitialized && value != _apiProxyService.IsNetworkAccessEnabled)
        {
            _ = SetNetworkAccessAsync();
        }
    }

    partial void OnIsChatHistoryEnabledChanged(bool value)
    {
        if (_settingsService.Current.SaveChatHistory == value)
        {
            return;
        }

        _settingsService.Current.SaveChatHistory = value;
        SaveSettings();
    }

    partial void OnThemeIndexChanged(int value)
    {
        string theme = value switch
        {
            1 => AppSettings.LightTheme,
            2 => AppSettings.DarkTheme,
            _ => AppSettings.SystemTheme,
        };
        if (theme == _settingsService.Current.Theme)
        {
            return;
        }

        _settingsService.Current.Theme = theme;
        SaveSettings();
        ThemeChanged?.Invoke(theme);
    }

    partial void OnRuntimeStatusChanged(RuntimeStatus value) => NotifyRuntimeSummaryChanged();

    partial void OnImageRuntimeStatusChanged(RuntimeStatus value) => NotifyRuntimeSummaryChanged();

    partial void OnVideoRuntimeStatusChanged(RuntimeStatus value) => NotifyRuntimeSummaryChanged();

    partial void OnSpeechToTextRuntimeStatusChanged(RuntimeStatus value) => NotifyRuntimeSummaryChanged();

    partial void OnTextToSpeechRuntimeStatusChanged(RuntimeStatus value) => NotifyRuntimeSummaryChanged();

    private void NotifyRuntimeSummaryChanged()
    {
        OnPropertyChanged(nameof(LoadedRuntimeCount));
        OnPropertyChanged(nameof(HasLoadedRuntimes));
        OnPropertyChanged(nameof(LoadedModelsText));
        EjectAllCommand.NotifyCanExecuteChanged();
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryErrorText = exception.Message;
        }
    }

    private string GetNetworkStatusText()
    {
        return _resources.GetString(IsNetworkAccessEnabled ? "NetworkApiOn" : "NetworkApiOff");
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoMessages));
    }

    private void OnConversationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoConversations));
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
        _loadedModelId = snapshot.Status == RuntimeStatus.Ready ? snapshot.ModelId ?? string.Empty : string.Empty;
        RuntimeStatus = snapshot.Status;
        RuntimeStatusText = _resources.GetString($"Status{snapshot.Status}");
        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            RuntimeDetail = snapshot.Detail;
        }
        else if (snapshot.Status is RuntimeStatus.Loading or RuntimeStatus.Ready)
        {
            RuntimeDetail = string.Empty;
        }

        OnPropertyChanged(nameof(LoadedModelName));
        OnPropertyChanged(nameof(LoadedModelsText));
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
            OnPropertyChanged(nameof(HasMetrics));
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
