using System.Diagnostics;
using System.Net;
using System.Text;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class VideoRuntimeService : IVideoRuntimeService
{
    internal const int BackendPort = 8094;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lock _logLock = new();
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private Process? _process;
    private StreamWriter? _logWriter;
    private string? _distribution;
    private string? _runtimeScriptPath;
    private string? _pidFilePath;
    private bool _isStopping;

    public VideoRuntimeService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public event Action<RuntimeSnapshot>? StateChanged;

    public RuntimeSnapshot Current { get; private set; } = new()
    {
        Status = RuntimeStatus.Unloaded,
    };

    public async Task LoadAsync(VideoModelDefinition model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            await EjectCoreAsync(cancellationToken);
            ValidateFiles(model);
            await ValidateWslRuntimeAsync(model, cancellationToken);
            UpdateState(RuntimeStatus.Loading, model.Id, null, null);

            string dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MoEModelManager");
            string videoDirectory = Path.Combine(dataDirectory, "Videos");
            string runtimeDirectory = Path.Combine(dataDirectory, "VideoRuntime");
            string logDirectory = Path.Combine(dataDirectory, "Logs");
            Directory.CreateDirectory(videoDirectory);
            Directory.CreateDirectory(runtimeDirectory);
            Directory.CreateDirectory(logDirectory);

            string configPath = Path.Combine(runtimeDirectory, "fastvideo-serve.yaml");
            string config = VideoRuntimeConfigurationBuilder.Build(model, videoDirectory, BackendPort);
            await File.WriteAllTextAsync(configPath, config, new UTF8Encoding(false), cancellationToken);

            string scriptPath = Path.Combine(AppContext.BaseDirectory, "VideoRuntime", "video-runtime.sh");
            string pidPath = Path.Combine(runtimeDirectory, "fastvideo.pid");
            string logPath = Path.Combine(logDirectory, $"video-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            _logWriter = new StreamWriter(logPath, false, new UTF8Encoding(false))
            {
                AutoFlush = true,
            };

            _distribution = model.WslDistribution;
            _runtimeScriptPath = VideoRuntimeConfigurationBuilder.ToWslPath(scriptPath);
            _pidFilePath = VideoRuntimeConfigurationBuilder.ToWslPath(pidPath);
            ProcessStartInfo startInfo = CreateWslStartInfo(model.WslDistribution);
            startInfo.ArgumentList.Add("--exec");
            startInfo.ArgumentList.Add("/bin/bash");
            startInfo.ArgumentList.Add(_runtimeScriptPath);
            startInfo.ArgumentList.Add("start");
            startInfo.ArgumentList.Add(_pidFilePath);
            startInfo.ArgumentList.Add(model.FastVideoExecutable);
            startInfo.ArgumentList.Add("serve");
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(VideoRuntimeConfigurationBuilder.ToWslPath(configPath));

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
                throw new InvalidOperationException("The video runtime could not be started.");
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            UpdateState(RuntimeStatus.Loading, model.Id, null, _process.Id);
            await WaitUntilReadyAsync(model, cancellationToken);
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
        TryStopLinuxRuntime(TimeSpan.FromSeconds(5));
        Process? process = _process;
        if (process is not null && !process.HasExited)
        {
            process.Kill(true);
        }
    }

    private static ProcessStartInfo CreateWslStartInfo(string distribution)
    {
        ProcessStartInfo startInfo = new("wsl.exe")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("--distribution");
        startInfo.ArgumentList.Add(distribution);
        return startInfo;
    }

    private static void ValidateFiles(VideoModelDefinition model)
    {
        string scriptPath = Path.Combine(AppContext.BaseDirectory, "VideoRuntime", "video-runtime.sh");
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("The bundled video runtime launcher was not found.", scriptPath);
        }

        if (!Directory.Exists(model.ModelPath))
        {
            throw new DirectoryNotFoundException($"The configured video model directory was not found: {model.ModelPath}");
        }

        string indexPath = Path.Combine(model.ModelPath, "model_index.json");
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("The video model download is incomplete.", indexPath);
        }

        foreach (string component in new[] { "transformer", "text_encoder", "tokenizer", "vae" })
        {
            string componentPath = Path.Combine(model.ModelPath, component);
            if (!Directory.Exists(componentPath))
            {
                throw new DirectoryNotFoundException($"The video model component is missing: {componentPath}");
            }
        }

        string[] requiredWeightFiles =
        [
            Path.Combine("text_encoder", "model-00001-of-00003.safetensors"),
            Path.Combine("text_encoder", "model-00002-of-00003.safetensors"),
            Path.Combine("text_encoder", "model-00003-of-00003.safetensors"),
            Path.Combine("transformer", "diffusion_pytorch_model.safetensors"),
            Path.Combine("vae", "diffusion_pytorch_model.safetensors"),
        ];
        string? missingWeight = requiredWeightFiles
            .Select(relativePath => Path.Combine(model.ModelPath, relativePath))
            .FirstOrDefault(path => !File.Exists(path));
        if (missingWeight is not null)
        {
            throw new FileNotFoundException(
                "The video model is still downloading or is incomplete.",
                missingWeight);
        }
    }

    private static async Task ValidateWslRuntimeAsync(
        VideoModelDefinition model,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = CreateWslStartInfo(model.WslDistribution);
        startInfo.ArgumentList.Add("--exec");
        startInfo.ArgumentList.Add("/usr/bin/test");
        startInfo.ArgumentList.Add("-x");
        startInfo.ArgumentList.Add(model.FastVideoExecutable);
        using Process process = new() { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new FileNotFoundException(
                "The configured FastVideo executable was not found inside WSL.",
                model.FastVideoExecutable);
        }
    }

    private async Task WaitUntilReadyAsync(VideoModelDefinition model, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(model.StartupTimeoutSeconds));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        HttpClient client = _httpClientFactory.CreateClient("VideoBackend");
        Uri healthUri = new($"http://127.0.0.1:{BackendPort}/health");

        while (!linked.IsCancellationRequested)
        {
            if (_process is null || _process.HasExited)
            {
                throw new InvalidOperationException("The video runtime exited while loading the model.");
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

            await Task.Delay(TimeSpan.FromSeconds(1), linked.Token);
        }

        throw new TimeoutException($"Timed out while loading video model {model.Id}.");
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
        await StopLinuxRuntimeAsync(cancellationToken);
        if (!process.HasExited)
        {
            using CancellationTokenSource gracefulTimeout = new(TimeSpan.FromSeconds(10));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                gracefulTimeout.Token,
                cancellationToken);
            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (gracefulTimeout.IsCancellationRequested)
            {
            }

            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync(cancellationToken);
            }
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

        _distribution = null;
        _runtimeScriptPath = null;
        _pidFilePath = null;
        _isStopping = false;
        UpdateState(RuntimeStatus.Unloaded, null, null, null);
    }

    private async Task StopLinuxRuntimeAsync(CancellationToken cancellationToken)
    {
        Process? stopProcess = CreateStopProcess();
        if (stopProcess is null)
        {
            return;
        }

        using (stopProcess)
        {
            stopProcess.Start();
            await stopProcess.WaitForExitAsync(cancellationToken);
        }
    }

    private void TryStopLinuxRuntime(TimeSpan timeout)
    {
        try
        {
            using Process? stopProcess = CreateStopProcess();
            if (stopProcess is null)
            {
                return;
            }

            stopProcess.Start();
            stopProcess.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private Process? CreateStopProcess()
    {
        if (string.IsNullOrWhiteSpace(_distribution)
            || string.IsNullOrWhiteSpace(_runtimeScriptPath)
            || string.IsNullOrWhiteSpace(_pidFilePath))
        {
            return null;
        }

        ProcessStartInfo startInfo = CreateWslStartInfo(_distribution);
        startInfo.ArgumentList.Add("--exec");
        startInfo.ArgumentList.Add("/bin/bash");
        startInfo.ArgumentList.Add(_runtimeScriptPath);
        startInfo.ArgumentList.Add("stop");
        startInfo.ArgumentList.Add(_pidFilePath);
        return new Process { StartInfo = startInfo };
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
            UpdateState(RuntimeStatus.Error, Current.ModelId, "The video runtime exited unexpectedly.", null);
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
