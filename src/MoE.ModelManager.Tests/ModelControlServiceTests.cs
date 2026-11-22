using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ModelControlServiceTests
{
    private readonly List<string> _calls = [];
    private FakeTextRuntime _text = null!;
    private FakeImageRuntime _image = null!;
    private FakeVideoRuntime _video = null!;
    private FakeAudioRuntime _audio = null!;
    private ModelControlService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _text = new FakeTextRuntime(_calls);
        _image = new FakeImageRuntime(_calls);
        _video = new FakeVideoRuntime(_calls);
        _audio = new FakeAudioRuntime(_calls);
        _service = new ModelControlService(
            new FakeCatalogs(),
            new FakeCatalogs(),
            new FakeCatalogs(),
            new FakeCatalogs(),
            _text,
            _image,
            _audio,
            _video);
    }

    [TestMethod]
    public async Task GetModelsAsync_Configured_ListsEveryRuntimeUnloaded()
    {
        IReadOnlyList<ControlledModel> models = await _service.GetModelsAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "gemma", "qwen", "flux", "wan", ModelControlRules.SpeechToTextModelId, ModelControlRules.TextToSpeechModelId },
            models.Select(model => model.Id).ToArray());
        Assert.IsTrue(models.All(model => model.Status == RuntimeStatus.Unloaded));
    }

    [TestMethod]
    public async Task LoadAsync_TextModel_EjectsImageAndVideoFirst()
    {
        ControlledModel loaded = await _service.LoadAsync("QWEN");

        CollectionAssert.AreEqual(new[] { "eject image", "eject video", "load text qwen" }, _calls);
        Assert.AreEqual("qwen", loaded.Id);
        Assert.AreEqual(RuntimeStatus.Ready, loaded.Status);
    }

    [TestMethod]
    public async Task LoadAsync_ImageModel_EjectsTextAndVideoFirst()
    {
        await _service.LoadAsync("flux");

        CollectionAssert.AreEqual(new[] { "eject text", "eject video", "load image" }, _calls);
    }

    [TestMethod]
    public async Task LoadAsync_SpeechModel_LeavesGpuRuntimesAlone()
    {
        await _service.LoadAsync(ModelControlRules.SpeechToTextModelId);

        CollectionAssert.AreEqual(new[] { "load speech-to-text" }, _calls);
    }

    [TestMethod]
    public async Task LoadAsync_AlreadyReady_DoesNothing()
    {
        await _service.LoadAsync("gemma");
        _calls.Clear();

        await _service.LoadAsync("gemma");

        Assert.AreEqual(0, _calls.Count);
    }

    [TestMethod]
    public async Task GetModelsAsync_TextModelLoaded_OnlyThatTextModelIsReady()
    {
        await _service.LoadAsync("gemma");

        IReadOnlyList<ControlledModel> models = await _service.GetModelsAsync(CancellationToken.None);

        Assert.AreEqual(RuntimeStatus.Ready, models.Single(model => model.Id == "gemma").Status);
        Assert.AreEqual(RuntimeStatus.Unloaded, models.Single(model => model.Id == "qwen").Status);
    }

    [TestMethod]
    public async Task LoadAsync_UnknownModel_ThrowsKeyNotFound()
    {
        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(() => _service.LoadAsync("missing"));
    }

    [TestMethod]
    public async Task LoadAsync_RuntimeFails_ReportsErrorWithDetail()
    {
        _text.FailWith = new IOException("weights missing");

        await Assert.ThrowsExactlyAsync<IOException>(() => _service.LoadAsync("gemma"));
        ControlledModel? model = await _service.FindModelAsync("gemma", CancellationToken.None);

        Assert.AreEqual(RuntimeStatus.Error, model?.Status);
        Assert.AreEqual("weights missing", model?.Detail);
    }

    [TestMethod]
    public async Task EjectAllAsync_Always_EjectsEveryRuntime()
    {
        await _service.EjectAllAsync(CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { "eject text", "eject image", "eject video", "eject speech-to-text", "eject text-to-speech" },
            _calls);
    }

    [TestMethod]
    public void RequestShutdown_Subscribed_RaisesEvent()
    {
        bool raised = false;
        _service.ShutdownRequested += () => raised = true;

        _service.RequestShutdown();

        Assert.IsTrue(raised);
    }

    private static RuntimeSnapshot Ready(string id) => new() { Status = RuntimeStatus.Ready, ModelId = id };

    private sealed class FakeCatalogs
        : IModelCatalogService, IImageModelCatalogService, IAudioModelCatalogService, IVideoModelCatalogService
    {
        Task<IReadOnlyList<ModelDefinition>> IModelCatalogService.LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ModelDefinition>>(
            [
                new ModelDefinition { Id = "gemma", DisplayName = "Gemma", Description = string.Empty, ModelPath = "g.gguf", CacheType = "f16" },
                new ModelDefinition { Id = "qwen", DisplayName = "Qwen", Description = string.Empty, ModelPath = "q.gguf", CacheType = "f16" },
            ]);
        }

        Task<ImageModelDefinition> IImageModelCatalogService.LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ImageModelDefinition
            {
                Id = "flux",
                DisplayName = "Flux",
                Description = string.Empty,
                DiffusionModelPath = "d",
                TextEncoderPath = "t",
                VaePath = "v",
            });
        }

        Task<AudioModelDefinition> IAudioModelCatalogService.LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AudioModelDefinition
            {
                Id = "audio",
                DisplayName = "Audio",
                Description = string.Empty,
                ParakeetModelPath = "p",
                KokoroModelPath = "k",
            });
        }

        Task<VideoModelDefinition> IVideoModelCatalogService.LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new VideoModelDefinition { Id = "wan", DisplayName = "Wan", Description = string.Empty, ModelPath = "w" });
        }
    }

    private abstract class FakeRuntime(List<string> calls, string name)
    {
        public event Action<RuntimeSnapshot>? StateChanged;

        public RuntimeSnapshot Current { get; private set; } = new();

        public Exception? FailWith { get; set; }

        public Task EjectAsync(CancellationToken cancellationToken)
        {
            calls.Add($"eject {name}");
            Current = new RuntimeSnapshot();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        protected Task LoadCoreAsync(string id, string call)
        {
            if (FailWith is not null)
            {
                return Task.FromException(FailWith);
            }

            calls.Add(call);
            Current = Ready(id);
            StateChanged?.Invoke(Current);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTextRuntime(List<string> calls) : FakeRuntime(calls, "text"), IModelRuntimeService
    {
        public Task LoadAsync(ModelDefinition model, CancellationToken cancellationToken) => LoadCoreAsync(model.Id, $"load text {model.Id}");
    }

    private sealed class FakeImageRuntime(List<string> calls) : FakeRuntime(calls, "image"), IImageRuntimeService
    {
        public Task LoadAsync(ImageModelDefinition model, CancellationToken cancellationToken) => LoadCoreAsync(model.Id, "load image");
    }

    private sealed class FakeVideoRuntime(List<string> calls) : FakeRuntime(calls, "video"), IVideoRuntimeService
    {
        public Task LoadAsync(VideoModelDefinition model, CancellationToken cancellationToken) => LoadCoreAsync(model.Id, "load video");
    }

    private sealed class FakeAudioRuntime(List<string> calls) : IAudioRuntimeService
    {
        public event Action<RuntimeSnapshot>? SpeechToTextStateChanged;

        public event Action<RuntimeSnapshot>? TextToSpeechStateChanged;

        public RuntimeSnapshot SpeechToTextCurrent { get; private set; } = new();

        public RuntimeSnapshot TextToSpeechCurrent { get; private set; } = new();

        public IReadOnlyList<KokoroVoice> Voices => [];

        public Task LoadSpeechToTextAsync(AudioModelDefinition model, CancellationToken cancellationToken)
        {
            calls.Add("load speech-to-text");
            SpeechToTextCurrent = Ready(model.Id);
            SpeechToTextStateChanged?.Invoke(SpeechToTextCurrent);
            return Task.CompletedTask;
        }

        public Task LoadTextToSpeechAsync(AudioModelDefinition model, CancellationToken cancellationToken)
        {
            calls.Add("load text-to-speech");
            TextToSpeechCurrent = Ready(model.Id);
            TextToSpeechStateChanged?.Invoke(TextToSpeechCurrent);
            return Task.CompletedTask;
        }

        public Task EjectSpeechToTextAsync(CancellationToken cancellationToken)
        {
            calls.Add("eject speech-to-text");
            SpeechToTextCurrent = new RuntimeSnapshot();
            return Task.CompletedTask;
        }

        public Task EjectTextToSpeechAsync(CancellationToken cancellationToken)
        {
            calls.Add("eject text-to-speech");
            TextToSpeechCurrent = new RuntimeSnapshot();
            return Task.CompletedTask;
        }

        public Task<AudioTranscriptionResult> TranscribeAsync(string wavePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GeneratedSpeechResult> SynthesizeAsync(
            string text,
            KokoroVoice voice,
            float speed,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
