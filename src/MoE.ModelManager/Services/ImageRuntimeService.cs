using System.Diagnostics;
using System.Net;
using System.Text;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class ImageRuntimeService : IImageRuntimeService
{
    internal const int BackendPort = 8093;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lock _logLock = new();
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private Process? _process;
    private StreamWriter? _logWriter;
    private bool _isStopping;

    public ImageRuntimeService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public event Action<RuntimeSnapshot>? StateChanged;

    public RuntimeSnapshot Current { get; private set; } = new()
    {
        Status = RuntimeStatus.Unloaded,
    };

    public async Task LoadAsync(ImageModelDefinition model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            await EjectCoreAsync(cancellationToken);
            ValidateFiles(model);
            UpdateState(RuntimeStatus.Loading, model.Id, null, null);

            string runtimePath = Path.Combine(AppContext.BaseDirectory, "ImageRuntime", "sd-server.exe");
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MoEModelManager",
                "Logs");
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, $"image-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
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
            foreach (string argument in ImageRuntimeArgumentsBuilder.Build(model, BackendPort))
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
                throw new InvalidOperationException("The image runtime could not be started.");
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

    private static void ValidateFiles(ImageModelDefinition model)
    {
        string runtimePath = Path.Combine(AppContext.BaseDirectory, "ImageRuntime", "sd-server.exe");
        if (!File.Exists(runtimePath))
        {
            throw new FileNotFoundException("The bundled image runtime was not found.", runtimePath);
        }

        foreach (string path in new[] { model.DiffusionModelPath, model.TextEncoderPath, model.VaePath })
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("A configured image model file was not found.", path);
            }
        }
    }

    private async Task WaitUntilReadyAsync(string modelId, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(8));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        HttpClient client = _httpClientFactory.CreateClient("ImageBackend");
        Uri modelsUri = new($"http://127.0.0.1:{BackendPort}/v1/models");

        while (!linked.IsCancellationRequested)
        {
            if (_process is null || _process.HasExited)
            {
                throw new InvalidOperationException("The image runtime exited while loading the model.");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(modelsUri, linked.Token);
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

        throw new TimeoutException($"Timed out while loading image model {modelId}.");
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
            UpdateState(RuntimeStatus.Error, Current.ModelId, "The image runtime exited unexpectedly.", null);
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
