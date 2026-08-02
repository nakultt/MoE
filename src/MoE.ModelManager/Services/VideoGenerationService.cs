using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class VideoGenerationService : IVideoGenerationService
{
    private static readonly Uri BackendBaseUri = new($"http://127.0.0.1:{VideoRuntimeService.BackendPort}/v1/");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRequestTelemetryService _telemetryService;
    private readonly IAppResources _resources;
    private readonly object _generationSync = new();
    private Task<GeneratedVideoResult>? _activeGeneration;

    public VideoGenerationService(
        IHttpClientFactory httpClientFactory,
        IRequestTelemetryService telemetryService,
        IAppResources resources)
    {
        _httpClientFactory = httpClientFactory;
        _telemetryService = telemetryService;
        _resources = resources;
    }

    public Task<GeneratedVideoResult> GenerateAsync(
        string modelId,
        string prompt,
        VideoGenerationPreset preset,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(preset);

        lock (_generationSync)
        {
            // WinUI can dispatch the same command again while the first async
            // invocation is crossing the loopback HTTP boundary. Reuse the
            // active operation so one click can never create two FastVideo jobs.
            if (_activeGeneration is { IsCompleted: false })
            {
                return _activeGeneration;
            }

            Task<GeneratedVideoResult> generation = GenerateCoreAsync(
                modelId,
                prompt,
                preset,
                cancellationToken);
            _activeGeneration = generation;
            _ = generation.ContinueWith(
                completed => ClearActiveGeneration(completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return generation;
        }
    }

    private async Task<GeneratedVideoResult> GenerateCoreAsync(
        string modelId,
        string prompt,
        VideoGenerationPreset preset,
        CancellationToken cancellationToken)
    {
        object payload = new
        {
            model = modelId,
            prompt,
            size = preset.Size,
            fps = preset.FramesPerSecond,
            num_frames = preset.Frames,
            num_inference_steps = preset.Steps,
            guidance_scale = 1.0,
            seed = Random.Shared.Next(1, int.MaxValue),
        };
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(BackendBaseUri, "videos"))
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-MoE-Client", "desktop");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpClient client = _httpClientFactory.CreateClient("VideoBackend");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument queued = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        string jobId = queued.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidDataException("The video runtime returned an empty job identifier.");

        double inferenceSeconds = await WaitForCompletionAsync(client, jobId, cancellationToken);
        string outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Videos");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"{jobId}.mp4");
        if (!File.Exists(outputPath))
        {
            Uri contentUrl = new(BackendBaseUri, $"videos/{Uri.EscapeDataString(jobId)}/content");
            using HttpRequestMessage contentRequest = new(HttpMethod.Get, contentUrl);
            contentRequest.Headers.Add("X-MoE-Client", "desktop");
            using HttpResponseMessage contentResponse = await client.SendAsync(
                contentRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            contentResponse.EnsureSuccessStatusCode();
            await using Stream source = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream destination = new(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            await source.CopyToAsync(destination, cancellationToken);
        }

        stopwatch.Stop();
        AddCompletedMetric(modelId, preset, stopwatch.Elapsed.TotalMilliseconds, inferenceSeconds);
        return new GeneratedVideoResult
        {
            JobId = jobId,
            OutputPath = outputPath,
            DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "This polling operation is part of the stateful video generation workflow.")]
    private async Task<double> WaitForCompletionAsync(
        HttpClient client,
        string jobId,
        CancellationToken cancellationToken)
    {
        Uri statusUrl = new(BackendBaseUri, $"videos/{Uri.EscapeDataString(jobId)}");
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            using HttpRequestMessage request = new(HttpMethod.Get, statusUrl);
            request.Headers.Add("X-MoE-Client", "desktop");
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidDataException("The video generation job was not found.");
            }

            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsByteArrayAsync(cancellationToken));
            string status = document.RootElement.GetProperty("status").GetString() ?? string.Empty;
            if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                return document.RootElement.TryGetProperty("inference_time_s", out JsonElement inferenceElement)
                    && inferenceElement.TryGetDouble(out double inferenceSeconds)
                        ? inferenceSeconds
                        : 0;
            }

            if (status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            {
                string error = document.RootElement.TryGetProperty("error", out JsonElement errorElement)
                    ? errorElement.ToString()
                    : "Unknown FastVideo error.";
                throw new InvalidDataException($"Video generation failed: {error}");
            }
        }
    }

    private void ClearActiveGeneration(Task<GeneratedVideoResult> completed)
    {
        lock (_generationSync)
        {
            if (ReferenceEquals(_activeGeneration, completed))
            {
                _activeGeneration = null;
            }
        }
    }

    private void AddCompletedMetric(
        string modelId,
        VideoGenerationPreset preset,
        double durationMilliseconds,
        double inferenceSeconds)
    {
        double videoSeconds = preset.FramesPerSecond > 0
            ? (double)preset.Frames / preset.FramesPerSecond
            : 0;
        double generationSeconds = inferenceSeconds > 0
            ? inferenceSeconds
            : durationMilliseconds / 1000d;
        _telemetryService.Add(new RequestMetric
        {
            Timestamp = DateTimeOffset.Now,
            Source = _resources.GetString("SourceDesktop"),
            Endpoint = "/v1/videos",
            Model = modelId,
            StatusCode = (int)HttpStatusCode.OK,
            DurationMilliseconds = durationMilliseconds,
            QuantitySummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("VideoMetricSummaryFormat"),
                preset.Size,
                videoSeconds.ToString("0.##", CultureInfo.CurrentCulture)),
            PerformanceSummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("VideoPerformanceSummaryFormat"),
                generationSeconds),
        });
    }
}
