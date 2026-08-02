using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class ImageGenerationService : IImageGenerationService
{
    private readonly IApiProxyService _apiProxyService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ImageGenerationService(IApiProxyService apiProxyService, IHttpClientFactory httpClientFactory)
    {
        _apiProxyService = apiProxyService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GeneratedImageResult> GenerateAsync(
        string modelId,
        string prompt,
        string size,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(size);

        object payload = new
        {
            model = modelId,
            prompt,
            n = 1,
            size,
            output_format = "png",
        };
        using HttpRequestMessage request = new(HttpMethod.Post, $"{_apiProxyService.BaseUrl}/images/generations")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-MoE-Client", "desktop");

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpClient client = _httpClientFactory.CreateClient("LocalApi");
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        byte[] responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        stopwatch.Stop();

        using JsonDocument document = JsonDocument.Parse(responseBody);
        JsonElement data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0)
        {
            throw new InvalidDataException("The image runtime returned no image.");
        }

        string base64 = data[0].GetProperty("b64_json").GetString()
            ?? throw new InvalidDataException("The image runtime returned empty image data.");
        byte[] imageData = Convert.FromBase64String(base64);
        string outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Images");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"flux-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png");
        await File.WriteAllBytesAsync(outputPath, imageData, cancellationToken);

        return new GeneratedImageResult
        {
            Data = imageData,
            MediaType = "image/png",
            OutputPath = outputPath,
            DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
        };
    }
}
