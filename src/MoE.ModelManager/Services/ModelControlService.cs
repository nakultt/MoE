using System.Collections.Concurrent;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Loads and ejects models for the local control API, applying the same GPU exclusivity as the user interface.
/// </summary>
internal sealed class ModelControlService : IModelControlService, IDisposable
{
    private const string SpeechToTextName = "NVIDIA Parakeet TDT 0.6B v3";
    private const string TextToSpeechName = "Kokoro 82M";
    private readonly IModelCatalogService _modelCatalogService;
    private readonly IImageModelCatalogService _imageModelCatalogService;
    private readonly IAudioModelCatalogService _audioModelCatalogService;
    private readonly IVideoModelCatalogService _videoModelCatalogService;
    private readonly IModelRuntimeService _runtimeService;
    private readonly IImageRuntimeService _imageRuntimeService;
    private readonly IAudioRuntimeService _audioRuntimeService;
    private readonly IVideoRuntimeService _videoRuntimeService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _catalogGate = new(1, 1);
    private readonly ConcurrentDictionary<ModelKind, string> _pendingLoads = new();
    private readonly ConcurrentDictionary<string, string> _loadErrors = new(StringComparer.OrdinalIgnoreCase);
    private Catalog? _catalog;

    public ModelControlService(
        IModelCatalogService modelCatalogService,
        IImageModelCatalogService imageModelCatalogService,
        IAudioModelCatalogService audioModelCatalogService,
        IVideoModelCatalogService videoModelCatalogService,
        IModelRuntimeService runtimeService,
        IImageRuntimeService imageRuntimeService,
        IAudioRuntimeService audioRuntimeService,
        IVideoRuntimeService videoRuntimeService)
    {
        _modelCatalogService = modelCatalogService;
        _imageModelCatalogService = imageModelCatalogService;
        _audioModelCatalogService = audioModelCatalogService;
        _videoModelCatalogService = videoModelCatalogService;
        _runtimeService = runtimeService;
        _imageRuntimeService = imageRuntimeService;
        _audioRuntimeService = audioRuntimeService;
        _videoRuntimeService = videoRuntimeService;
    }

    public event Action? ShutdownRequested;

    public async Task<IReadOnlyList<ControlledModel>> GetModelsAsync(CancellationToken cancellationToken)
    {
        Catalog catalog = await GetCatalogAsync(cancellationToken);
        return catalog.Entries.Select(entry => Describe(entry.Id, entry.Name, entry.Kind)).ToList();
    }

    public async Task<ControlledModel?> FindModelAsync(string modelId, CancellationToken cancellationToken)
    {
        Catalog catalog = await GetCatalogAsync(cancellationToken);
        CatalogEntry? entry = catalog.Find(modelId);
        return entry is null ? null : Describe(entry.Id, entry.Name, entry.Kind);
    }

    public async Task<ControlledModel> LoadAsync(string modelId)
    {
        Catalog catalog = await GetCatalogAsync(CancellationToken.None);
        CatalogEntry entry = catalog.Find(modelId)
            ?? throw new KeyNotFoundException($"Unknown model '{modelId}'.");
        _pendingLoads[entry.Kind] = entry.Id;
        _loadErrors.TryRemove(entry.Id, out _);
        await _loadGate.WaitAsync();
        try
        {
            if (Describe(entry.Id, entry.Name, entry.Kind).Status != RuntimeStatus.Ready)
            {
                foreach (ModelKind conflictingKind in ModelControlRules.GetConflictingKinds(entry.Kind))
                {
                    await EjectAsync(conflictingKind, CancellationToken.None);
                }

                await LoadEntryAsync(catalog, entry);
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or InvalidDataException
            or TimeoutException
            or UnauthorizedAccessException)
        {
            _loadErrors[entry.Id] = exception.Message;
            throw;
        }
        finally
        {
            _pendingLoads.TryRemove(entry.Kind, out _);
            _loadGate.Release();
        }

        return Describe(entry.Id, entry.Name, entry.Kind);
    }

    public Task EjectAsync(ModelKind kind, CancellationToken cancellationToken)
    {
        return kind switch
        {
            ModelKind.Text => _runtimeService.EjectAsync(cancellationToken),
            ModelKind.Image => _imageRuntimeService.EjectAsync(cancellationToken),
            ModelKind.Video => _videoRuntimeService.EjectAsync(cancellationToken),
            ModelKind.SpeechToText => _audioRuntimeService.EjectSpeechToTextAsync(cancellationToken),
            ModelKind.TextToSpeech => _audioRuntimeService.EjectTextToSpeechAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public async Task EjectAllAsync(CancellationToken cancellationToken)
    {
        foreach (ModelKind kind in Enum.GetValues<ModelKind>())
        {
            await EjectAsync(kind, cancellationToken);
        }
    }

    public void RequestShutdown()
    {
        ShutdownRequested?.Invoke();
    }

    public void Dispose()
    {
        _loadGate.Dispose();
        _catalogGate.Dispose();
    }

    private Task LoadEntryAsync(Catalog catalog, CatalogEntry entry)
    {
        return entry.Kind switch
        {
            ModelKind.Text => _runtimeService.LoadAsync(
                catalog.TextModels.First(model => model.Id == entry.Id),
                CancellationToken.None),
            ModelKind.Image => _imageRuntimeService.LoadAsync(
                catalog.ImageModels.First(model => model.Id == entry.Id),
                CancellationToken.None),
            ModelKind.Video => _videoRuntimeService.LoadAsync(catalog.VideoModel, CancellationToken.None),
            ModelKind.SpeechToText => _audioRuntimeService.LoadSpeechToTextAsync(catalog.AudioModel, CancellationToken.None),
            ModelKind.TextToSpeech => _audioRuntimeService.LoadTextToSpeechAsync(catalog.AudioModel, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, null),
        };
    }

    private ControlledModel Describe(string id, string name, ModelKind kind)
    {
        RuntimeSnapshot snapshot = kind switch
        {
            ModelKind.Text => _runtimeService.Current,
            ModelKind.Image => _imageRuntimeService.Current,
            ModelKind.Video => _videoRuntimeService.Current,
            ModelKind.SpeechToText => _audioRuntimeService.SpeechToTextCurrent,
            _ => _audioRuntimeService.TextToSpeechCurrent,
        };

        // The text and image runtimes serve one of several models; the others report unloaded.
        bool ownsSnapshot = kind is not (ModelKind.Text or ModelKind.Image)
            || string.Equals(snapshot.ModelId, id, StringComparison.OrdinalIgnoreCase);
        RuntimeStatus status = ownsSnapshot ? snapshot.Status : RuntimeStatus.Unloaded;
        string? detail = ownsSnapshot ? snapshot.Detail : null;
        if (status is RuntimeStatus.Unloaded or RuntimeStatus.Error
            && _pendingLoads.TryGetValue(kind, out string? pendingId)
            && pendingId.Equals(id, StringComparison.OrdinalIgnoreCase))
        {
            status = RuntimeStatus.Loading;
        }
        else if (status is RuntimeStatus.Unloaded && _loadErrors.TryGetValue(id, out string? error))
        {
            status = RuntimeStatus.Error;
            detail = error;
        }

        return new ControlledModel(id, name, kind, status, string.IsNullOrWhiteSpace(detail) ? null : detail);
    }

    private async Task<Catalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog is not null)
        {
            return _catalog;
        }

        await _catalogGate.WaitAsync(cancellationToken);
        try
        {
            _catalog ??= new Catalog(
                await _modelCatalogService.LoadAsync(cancellationToken),
                await _imageModelCatalogService.LoadAsync(cancellationToken),
                await _videoModelCatalogService.LoadAsync(cancellationToken),
                await _audioModelCatalogService.LoadAsync(cancellationToken));
            return _catalog;
        }
        finally
        {
            _catalogGate.Release();
        }
    }

    private sealed record CatalogEntry(string Id, string Name, ModelKind Kind);

    private sealed class Catalog
    {
        public Catalog(
            IReadOnlyList<ModelDefinition> textModels,
            IReadOnlyList<ImageModelDefinition> imageModels,
            VideoModelDefinition videoModel,
            AudioModelDefinition audioModel)
        {
            TextModels = textModels;
            ImageModels = imageModels;
            VideoModel = videoModel;
            AudioModel = audioModel;
            Entries =
            [
                .. textModels.Select(model => new CatalogEntry(model.Id, model.DisplayName, ModelKind.Text)),
                .. imageModels.Select(model => new CatalogEntry(model.Id, model.DisplayName, ModelKind.Image)),
                new CatalogEntry(videoModel.Id, videoModel.DisplayName, ModelKind.Video),
                new CatalogEntry(ModelControlRules.SpeechToTextModelId, SpeechToTextName, ModelKind.SpeechToText),
                new CatalogEntry(ModelControlRules.TextToSpeechModelId, TextToSpeechName, ModelKind.TextToSpeech),
            ];
        }

        public IReadOnlyList<ModelDefinition> TextModels { get; }

        public IReadOnlyList<ImageModelDefinition> ImageModels { get; }

        public VideoModelDefinition VideoModel { get; }

        public AudioModelDefinition AudioModel { get; }

        public IReadOnlyList<CatalogEntry> Entries { get; }

        public CatalogEntry? Find(string modelId)
        {
            return Entries.FirstOrDefault(entry => entry.Id.Equals(modelId?.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
