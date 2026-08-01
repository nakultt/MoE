using System.Diagnostics;
using System.Net;
using System.Text;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class ModelRuntimeService : IModelRuntimeService
{
    internal const int BackendPort = 8092;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lock _logLock = new();
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private Process? _process;
    private StreamWriter? _logWriter;
    private bool _isStopping;

    public ModelRuntimeService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public event Action<RuntimeSnapshot>? StateChanged;

    public RuntimeSnapshot Current { get; private set; } = new()
    {
        Status = RuntimeStatus.Unloaded,
    };

    public async Task LoadAsync(ModelDefinition model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            await EjectCoreAsync(cancellationToken);
            ValidateFiles(model);
            UpdateState(RuntimeStatus.Loading, model.Id, null, null);

            string runtimePath = Path.Combine(AppContext.BaseDirectory, "Runtime", "llama-server.exe");
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MoEModelManager",
                "Logs");
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, $"llama-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            _logWriter = new StreamWriter(logPath, false, new UTF8Encoding(false))
            {
                AutoFlush = true,
            };

            ProcessStartInfo startInfo = new(runtimePath)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            foreach (string argument in LlamaArgumentsBuilder.Build(model, BackendPort))
            {
                startInfo.ArgumentList.Add(argument);
            }

            _isStopping = false;
            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };
            _process.OutputDataReceived += OnRuntimeOutput;
            _process.ErrorDataReceived += OnRuntimeOutput;
            _process.Exited += OnRuntimeExited;

            if (!_process.Start())
            {
                throw new InvalidOperationException("llama-server could not be started.");
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            UpdateState(RuntimeStatus.Loading, model.Id, null, _process.Id);
            await WaitUntilReadyAsync(model.Id, cancellationToken);
            UpdateState(RuntimeStatus.Ready, model.Id, null, _process.Id);
        }
        catch
        {
            await EjectCoreAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task EjectAsync(CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            await EjectCoreAsync(cancellationToken);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await EjectAsync(CancellationToken.None);
        _stateGate.Dispose();
    }

    internal void StopImmediately()
    {
        Process? process = _process;
        if (process is not null && !process.HasExited)
        {
            process.Kill(true);
        }
    }

    private static void ValidateFiles(ModelDefinition model)
    {
        string runtimePath = Path.Combine(AppContext.BaseDirectory, "Runtime", "llama-server.exe");
        if (!File.Exists(runtimePath))
        {
            throw new FileNotFoundException("The bundled llama-server runtime was not found.", runtimePath);
        }

        if (!File.Exists(model.ModelPath))
        {
            throw new FileNotFoundException("The target GGUF model was not found.", model.ModelPath);
        }

        if (model.HasDraftModel && !File.Exists(model.DraftModelPath))
        {
            throw new FileNotFoundException("The configured MTP draft model was not found.", model.DraftModelPath);
        }
    }

    private async Task WaitUntilReadyAsync(string modelId, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(6));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            cancellationToken);
        HttpClient client = _httpClientFactory.CreateClient("LlamaBackend");
        Uri healthUri = new($"http://127.0.0.1:{BackendPort}/health");

        while (!linked.IsCancellationRequested)
        {
            if (_process is null || _process.HasExited)
            {
                throw new InvalidOperationException("llama-server exited while loading the model.");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(healthUri, linked.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), linked.Token);
        }

        throw new TimeoutException($"Timed out while loading model {modelId}.");
    }

    private async Task EjectCoreAsync(CancellationToken cancellationToken)
    {
        Process? process = _process;
        if (process is null)
        {
            UpdateState(RuntimeStatus.Unloaded, null, null, null);
            return;
        }

        _isStopping = true;
        UpdateState(RuntimeStatus.Ejecting, Current.ModelId, null, process.HasExited ? null : process.Id);
        if (!process.HasExited)
        {
            process.Kill(true);
            await process.WaitForExitAsync(cancellationToken);
        }

        process.OutputDataReceived -= OnRuntimeOutput;
        process.ErrorDataReceived -= OnRuntimeOutput;
        process.Exited -= OnRuntimeExited;
        process.Dispose();
        _process = null;
        lock (_logLock)
        {
            _logWriter?.Dispose();
            _logWriter = null;
        }

        _isStopping = false;
        UpdateState(RuntimeStatus.Unloaded, null, null, null);
    }

    private void OnRuntimeOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
        {
            return;
        }

        lock (_logLock)
        {
            _logWriter?.WriteLine(e.Data);
        }
    }

    private void OnRuntimeExited(object? sender, EventArgs e)
    {
        if (!_isStopping)
        {
            UpdateState(RuntimeStatus.Error, Current.ModelId, "llama-server exited unexpectedly.", null);
        }
    }

    private void UpdateState(RuntimeStatus status, string? modelId, string? detail, int? processId)
    {
        Current = new RuntimeSnapshot
        {
            Status = status,
            ModelId = modelId,
            Detail = detail,
            ProcessId = processId,
        };
        StateChanged?.Invoke(Current);
    }
}
