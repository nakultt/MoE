using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class VideoGenerationService : IVideoGenerationService
{
    private readonly IApiProxyService _apiProxyService;
    private readonly IHttpClientFactory _httpClientFactory;

    public VideoGenerationService(IApiProxyService apiProxyService, IHttpClientFactory httpClientFactory)
    {
        _apiProxyService = apiProxyService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GeneratedVideoResult> GenerateAsync(
        string modelId,
        string prompt,
        VideoGenerationPreset preset,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(preset);

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
        using HttpRequestMessage request = new(HttpMethod.Post, $"{_apiProxyService.BaseUrl}/videos")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-MoE-Client", "desktop");

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpClient client = _httpClientFactory.CreateClient("LocalApi");
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument queued = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        string jobId = queued.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidDataException("The video runtime returned an empty job identifier.");

        await WaitForCompletionAsync(client, jobId, cancellationToken);
        string outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Videos");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"{jobId}.mp4");
        if (!File.Exists(outputPath))
        {
            string contentUrl = $"{_apiProxyService.BaseUrl}/videos/{Uri.EscapeDataString(jobId)}/content";
            using HttpRequestMessage contentRequest = new(HttpMethod.Get, contentUrl);
            contentRequest.Headers.Add("X-MoE-Client", "desktop");
            using HttpResponseMessage contentResponse = await client.SendAsync(contentRequest, cancellationToken);
            contentResponse.EnsureSuccessStatusCode();
            byte[] videoData = await contentResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(outputPath, videoData, cancellationToken);
        }

        stopwatch.Stop();
        return new GeneratedVideoResult
        {
            JobId = jobId,
            OutputPath = outputPath,
            DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
        };
    }

    private async Task WaitForCompletionAsync(
        HttpClient client,
        string jobId,
        CancellationToken cancellationToken)
    {
        string statusUrl = $"{_apiProxyService.BaseUrl}/videos/{Uri.EscapeDataString(jobId)}";
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
                return;
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
}
