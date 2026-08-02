using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class OpenAiProxyService : IApiProxyService
{
    private const int PublicPort = 8081;
    private const int MaximumRequestBytes = 8 * 1024 * 1024;
    private readonly IAppResources _resources;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IModelRuntimeService _runtimeService;
    private readonly IImageRuntimeService _imageRuntimeService;
    private readonly IAudioRuntimeService _audioRuntimeService;
    private readonly IVideoRuntimeService _videoRuntimeService;
    private readonly IRequestTelemetryService _telemetryService;
    private WebApplication? _application;

    public OpenAiProxyService(
        IAppResources resources,
        IHttpClientFactory httpClientFactory,
        IModelRuntimeService runtimeService,
        IImageRuntimeService imageRuntimeService,
        IAudioRuntimeService audioRuntimeService,
        IVideoRuntimeService videoRuntimeService,
        IRequestTelemetryService telemetryService)
    {
        _resources = resources;
        _httpClientFactory = httpClientFactory;
        _runtimeService = runtimeService;
        _imageRuntimeService = imageRuntimeService;
        _audioRuntimeService = audioRuntimeService;
        _videoRuntimeService = videoRuntimeService;
        _telemetryService = telemetryService;
    }

    public string BaseUrl => $"http://127.0.0.1:{PublicPort}/v1";

    public bool IsRunning => _application is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_application is not null)
        {
            return;
        }

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, PublicPort));
        WebApplication application = builder.Build();
        application.MapMethods("/{**path}", ["GET", "POST", "DELETE", "OPTIONS"], HandleAsync);
        await application.StartAsync(cancellationToken);
        _application = application;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        WebApplication? application = _application;
        if (application is null)
        {
            return;
        }

        _application = null;
        await application.StopAsync(cancellationToken);
        await application.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private static bool IsStreamingRequest(ReadOnlySpan<byte> body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body.ToArray());
            return document.RootElement.TryGetProperty("stream", out JsonElement streamElement)
                && streamElement.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadModelId(ReadOnlySpan<byte> body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body.ToArray());
            return document.RootElement.TryGetProperty("model", out JsonElement modelElement)
                ? modelElement.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static byte[] EnsureStreamingUsage(ReadOnlySpan<byte> body)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(body);
            if (node is not JsonObject requestObject)
            {
                return body.ToArray();
            }

            requestObject["stream_options"] = new JsonObject
            {
                ["include_usage"] = true,
            };
            return JsonSerializer.SerializeToUtf8Bytes(requestObject);
        }
        catch (JsonException)
        {
            return body.ToArray();
        }
    }

    private static void ApplyCors(HttpResponse response)
    {
        response.Headers.AccessControlAllowOrigin = "*";
        response.Headers.AccessControlAllowHeaders = "authorization, content-type, x-moe-client";
        response.Headers.AccessControlAllowMethods = "GET, POST, DELETE, OPTIONS";
    }

    private static void CopyRequestHeaders(HttpRequest source, HttpRequestMessage destination)
    {
        foreach ((string key, Microsoft.Extensions.Primitives.StringValues value) in source.Headers)
        {
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                || key.Equals("X-MoE-Client", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            destination.Headers.TryAddWithoutValidation(key, value.ToArray());
        }
    }

    private static void CopyResponseHeaders(HttpResponseMessage source, HttpResponse destination)
    {
        foreach ((string key, IEnumerable<string> value) in source.Headers)
        {
            if (!key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                destination.Headers[key] = value.ToArray();
            }
        }

        foreach ((string key, IEnumerable<string> value) in source.Content.Headers)
        {
            if (!key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                destination.Headers[key] = value.ToArray();
            }
        }
    }

    private static async Task<byte[]> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestBytes)
        {
            throw new BadHttpRequestException("Request body is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        await using MemoryStream buffer = new();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MaximumRequestBytes)
        {
            throw new BadHttpRequestException("Request body is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        return buffer.ToArray();
    }

    private static async Task<Usage> CopyBufferedResponseAsync(
        HttpResponseMessage source,
        HttpResponse destination,
        CancellationToken cancellationToken)
    {
        byte[] body = await source.Content.ReadAsByteArrayAsync(cancellationToken);
        await destination.Body.WriteAsync(body, cancellationToken);
        return ParseUsage(body);
    }

    private static async Task<Usage> CopyStreamingResponseAsync(
        HttpResponseMessage source,
        HttpResponse destination,
        CancellationToken cancellationToken)
    {
        Usage usage = new();
        int observedContentChunks = 0;
        await using Stream stream = await source.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is string line)
        {
            await destination.WriteAsync(line + "\n", cancellationToken);
            await destination.Body.FlushAsync(cancellationToken);
            if (!line.StartsWith("data: ", StringComparison.Ordinal) || line.Equals("data: [DONE]", StringComparison.Ordinal))
            {
                continue;
            }

            Usage current = ParseUsage(Encoding.UTF8.GetBytes(line[6..]));
            if (current.CompletionTokens > 0)
            {
                usage = current;
            }
            else if (ContainsContentDelta(line.AsSpan(6)))
            {
                observedContentChunks++;
            }
        }

        if (usage.CompletionTokens == 0)
        {
            usage = usage with { CompletionTokens = observedContentChunks };
        }

        return usage;
    }

    private static bool ContainsContentDelta(ReadOnlySpan<char> json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json.ToString());
            if (!document.RootElement.TryGetProperty("choices", out JsonElement choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return false;
            }

            JsonElement choice = choices[0];
            return choice.TryGetProperty("delta", out JsonElement delta)
                && delta.TryGetProperty("content", out JsonElement content)
                && !string.IsNullOrEmpty(content.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Usage ParseUsage(ReadOnlySpan<byte> json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json.ToArray());
            JsonElement root = document.RootElement;
            int promptTokens = 0;
            int completionTokens = 0;
            if (root.TryGetProperty("usage", out JsonElement usage))
            {
                promptTokens = ReadInt32(usage, "prompt_tokens");
                completionTokens = ReadInt32(usage, "completion_tokens");
            }

            double tokensPerSecond = 0;
            double generationSeconds = 0;
            if (root.TryGetProperty("timings", out JsonElement timings))
            {
                tokensPerSecond = ReadDouble(timings, "predicted_per_second");
                generationSeconds = ReadDouble(timings, "predicted_ms") / 1000d;
            }

            int imageCount = root.TryGetProperty("data", out JsonElement data)
                && data.ValueKind == JsonValueKind.Array
                    ? data.GetArrayLength()
                    : 0;
            string videoStatus = root.TryGetProperty("status", out JsonElement statusElement)
                ? statusElement.GetString() ?? string.Empty
                : string.Empty;
            string videoSize = root.TryGetProperty("size", out JsonElement sizeElement)
                ? sizeElement.GetString() ?? string.Empty
                : string.Empty;
            string videoSeconds = root.TryGetProperty("seconds", out JsonElement secondsElement)
                ? secondsElement.ToString()
                : string.Empty;
            if (root.TryGetProperty("inference_time_s", out JsonElement inferenceElement)
                && inferenceElement.TryGetDouble(out double inferenceSeconds))
            {
                generationSeconds = inferenceSeconds;
            }

            int videoCount = videoStatus.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return new Usage(
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TokensPerSecond: tokensPerSecond,
                GenerationSeconds: generationSeconds,
                ImageCount: imageCount,
                VideoCount: videoCount,
                VideoStatus: videoStatus,
                VideoSize: videoSize,
                VideoSeconds: videoSeconds);
        }
        catch (JsonException)
        {
            return new Usage();
        }
    }

    private static int ReadInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt32(out int value)
            ? value
            : 0;
    }

    private static double ReadDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetDouble(out double value)
            ? value
            : 0;
    }

    private static async Task WriteOpenAiErrorAsync(HttpContext context, int statusCode, string message)
    {
        await WriteJsonAsync(context, statusCode, new { error = new { message, type = "server_error" } });
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, object value)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(value, context.RequestAborted);
    }

    private async Task HandleAsync(HttpContext context)
    {
        ApplyCors(context.Response);
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        string path = context.Request.Path.Value ?? string.Empty;
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status200OK,
                new
                {
                    status = _runtimeService.Current.Status == RuntimeStatus.Ready
                        || _imageRuntimeService.Current.Status == RuntimeStatus.Ready
                        || _audioRuntimeService.Current.Status == RuntimeStatus.Ready
                        || _videoRuntimeService.Current.Status == RuntimeStatus.Ready
                            ? "ready"
                            : "unloaded",
                    text_model = _runtimeService.Current.Status.ToString().ToLowerInvariant(),
                    image_model = _imageRuntimeService.Current.Status.ToString().ToLowerInvariant(),
                    audio_models = _audioRuntimeService.Current.Status.ToString().ToLowerInvariant(),
                    video_model = _videoRuntimeService.Current.Status.ToString().ToLowerInvariant(),
                });
            return;
        }

        if (!path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (path.StartsWith("/v1/audio", StringComparison.OrdinalIgnoreCase))
        {
            if (_audioRuntimeService.Current.Status != RuntimeStatus.Ready)
            {
                await WriteOpenAiErrorAsync(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    _resources.GetString("ApiNoAudioModel"));
                return;
            }

            if (path.Equals("/v1/audio/speech", StringComparison.OrdinalIgnoreCase)
                && HttpMethods.IsPost(context.Request.Method))
            {
                await HandleSpeechAsync(context);
                return;
            }

            if (path.Equals("/v1/audio/transcriptions", StringComparison.OrdinalIgnoreCase)
                && HttpMethods.IsPost(context.Request.Method))
            {
                await HandleTranscriptionAsync(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        bool isImageRequest = path.StartsWith("/v1/images", StringComparison.OrdinalIgnoreCase);
        bool isVideoRequest = path.StartsWith("/v1/videos", StringComparison.OrdinalIgnoreCase);
        bool isModelListRequest = path.Equals("/v1/models", StringComparison.OrdinalIgnoreCase);
        bool useImageRuntime = isImageRequest
            || (isModelListRequest
                && _runtimeService.Current.Status != RuntimeStatus.Ready
                && _imageRuntimeService.Current.Status == RuntimeStatus.Ready);
        bool useVideoRuntime = isVideoRequest
            || (isModelListRequest
                && _runtimeService.Current.Status != RuntimeStatus.Ready
                && _imageRuntimeService.Current.Status != RuntimeStatus.Ready
                && _videoRuntimeService.Current.Status == RuntimeStatus.Ready);
        RuntimeStatus requiredStatus = useVideoRuntime
            ? _videoRuntimeService.Current.Status
            : useImageRuntime
                ? _imageRuntimeService.Current.Status
                : _runtimeService.Current.Status;
        if (requiredStatus != RuntimeStatus.Ready)
        {
            string messageKey = useVideoRuntime
                ? "ApiNoVideoModel"
                : useImageRuntime
                    ? "ApiNoImageModel"
                    : "ApiNoModel";
            await WriteOpenAiErrorAsync(context, StatusCodes.Status503ServiceUnavailable, _resources.GetString(messageKey));
            return;
        }

        await ForwardAsync(context, path, useImageRuntime, useVideoRuntime);
    }

    private async Task HandleSpeechAsync(HttpContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] body = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
        SpeechRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SpeechRequest>(body);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            await WriteOpenAiErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                _resources.GetString("ApiInvalidRequest"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.ResponseFormat)
            && !request.ResponseFormat.Equals("wav", StringComparison.OrdinalIgnoreCase))
        {
            await WriteOpenAiErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                "This local runtime currently supports response_format=wav.");
            return;
        }

        KokoroVoice? voice = _audioRuntimeService.Voices.FirstOrDefault(
            item => item.Id.Equals(request.Voice, StringComparison.OrdinalIgnoreCase));
        voice ??= _audioRuntimeService.Voices.Count > 0 ? _audioRuntimeService.Voices[0] : null;
        if (voice is null)
        {
            await WriteOpenAiErrorAsync(context, StatusCodes.Status500InternalServerError, "No Kokoro voices are configured.");
            return;
        }

        GeneratedSpeechResult result = await _audioRuntimeService.SynthesizeAsync(
            request.Input,
            voice,
            request.Speed <= 0 ? 1f : request.Speed,
            context.RequestAborted);
        byte[] wave = await File.ReadAllBytesAsync(result.OutputPath, context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "audio/wav";
        context.Response.ContentLength = wave.Length;
        await context.Response.Body.WriteAsync(wave, context.RequestAborted);
        stopwatch.Stop();
        AddAudioMetric(
            context,
            "/v1/audio/speech",
            request.Model ?? "kokoro-82m",
            result.AudioDurationSeconds,
            stopwatch.Elapsed.TotalMilliseconds,
            "speech generated");
    }

    private async Task HandleTranscriptionAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            await WriteOpenAiErrorAsync(context, StatusCodes.Status400BadRequest, "Use multipart/form-data with a WAV file.");
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        IFormFile? file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            await WriteOpenAiErrorAsync(context, StatusCodes.Status400BadRequest, "The file field is required.");
            return;
        }

        string tempDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Temp");
        Directory.CreateDirectory(tempDirectory);
        string tempPath = Path.Combine(tempDirectory, $"transcription-{Guid.NewGuid():N}.wav");
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await file.CopyToAsync(stream, context.RequestAborted);
            }

            AudioTranscriptionResult result = await _audioRuntimeService.TranscribeAsync(
                tempPath,
                context.RequestAborted);
            string responseFormat = form["response_format"].ToString();
            if (responseFormat.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(result.Text, context.RequestAborted);
            }
            else
            {
                await WriteJsonAsync(context, StatusCodes.Status200OK, new
                {
                    text = result.Text,
                    duration = result.AudioDurationSeconds,
                    tokens = result.Tokens,
                    timestamps = result.Timestamps,
                });
            }

            stopwatch.Stop();
            AddAudioMetric(
                context,
                "/v1/audio/transcriptions",
                form["model"].FirstOrDefault() ?? "parakeet-tdt-0.6b-v3",
                result.AudioDurationSeconds,
                stopwatch.Elapsed.TotalMilliseconds,
                "audio transcribed");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private void AddAudioMetric(
        HttpContext context,
        string endpoint,
        string model,
        double audioSeconds,
        double durationMilliseconds,
        string action)
    {
        string source = context.Request.Headers.TryGetValue(
            "X-MoE-Client",
            out Microsoft.Extensions.Primitives.StringValues clientHeader)
            && clientHeader.ToString().Equals("desktop", StringComparison.OrdinalIgnoreCase)
                ? _resources.GetString("SourceDesktop")
                : _resources.GetString("SourceExternal");
        double realtimeFactor = audioSeconds / Math.Max(durationMilliseconds / 1000d, 0.001d);
        _telemetryService.Add(new RequestMetric
        {
            Timestamp = DateTimeOffset.Now,
            Source = source,
            Endpoint = endpoint,
            Model = model,
            StatusCode = StatusCodes.Status200OK,
            DurationMilliseconds = durationMilliseconds,
            QuantitySummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("AudioMetricSummaryFormat"),
                audioSeconds,
                action),
            PerformanceSummary = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("AudioPerformanceSummaryFormat"),
                durationMilliseconds / 1000d,
                realtimeFactor),
        });
    }

    private async Task ForwardAsync(
        HttpContext context,
        string path,
        bool useImageRuntime,
        bool useVideoRuntime)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] requestBody = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
        bool isStreaming = IsStreamingRequest(requestBody);
        if (isStreaming)
        {
            requestBody = EnsureStreamingUsage(requestBody);
        }

        string requestedModel = ReadModelId(requestBody);
        string source = context.Request.Headers.TryGetValue("X-MoE-Client", out Microsoft.Extensions.Primitives.StringValues clientHeader)
            && clientHeader.ToString().Equals("desktop", StringComparison.OrdinalIgnoreCase)
                ? _resources.GetString("SourceDesktop")
                : _resources.GetString("SourceExternal");
        int backendPort = useVideoRuntime
            ? VideoRuntimeService.BackendPort
            : useImageRuntime
                ? ImageRuntimeService.BackendPort
                : ModelRuntimeService.BackendPort;
        string backendPath = path.Equals("/v1/videos/generations", StringComparison.OrdinalIgnoreCase)
            ? "/v1/videos"
            : path;
        Uri backendUri = new($"http://127.0.0.1:{backendPort}{backendPath}{context.Request.QueryString}");
        using HttpRequestMessage backendRequest = new(new HttpMethod(context.Request.Method), backendUri);
        CopyRequestHeaders(context.Request, backendRequest);
        if (requestBody.Length > 0)
        {
            backendRequest.Content = new ByteArrayContent(requestBody);
            backendRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                context.Request.ContentType ?? "application/json");
        }

        string clientName = useVideoRuntime ? "VideoBackend" : useImageRuntime ? "ImageBackend" : "LlamaBackend";
        HttpClient client = _httpClientFactory.CreateClient(clientName);
        using HttpResponseMessage backendResponse = await client.SendAsync(
            backendRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);
        context.Response.StatusCode = (int)backendResponse.StatusCode;
        CopyResponseHeaders(backendResponse, context.Response);
        ApplyCors(context.Response);

        Usage usage = isStreaming
            ? await CopyStreamingResponseAsync(backendResponse, context.Response, context.RequestAborted)
            : await CopyBufferedResponseAsync(backendResponse, context.Response, context.RequestAborted);
        stopwatch.Stop();

        double generationSeconds = usage.GenerationSeconds > 0
            ? usage.GenerationSeconds
            : stopwatch.Elapsed.TotalSeconds;
        double tokensPerSecond = usage.TokensPerSecond > 0
            ? usage.TokensPerSecond
            : usage.CompletionTokens / Math.Max(generationSeconds, 0.001d);
        if (useVideoRuntime
            && (path.EndsWith("/content", StringComparison.OrdinalIgnoreCase)
                || (HttpMethods.IsGet(context.Request.Method)
                    && path.Equals("/v1/videos", StringComparison.OrdinalIgnoreCase))
                || (HttpMethods.IsGet(context.Request.Method)
                    && usage.VideoStatus is "queued" or "in_progress")))
        {
            return;
        }

        string quantitySummary = useVideoRuntime
            ? usage.VideoCount > 0
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    _resources.GetString("VideoMetricSummaryFormat"),
                    usage.VideoSize,
                    usage.VideoSeconds)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    _resources.GetString("VideoJobStatusFormat"),
                    string.IsNullOrWhiteSpace(usage.VideoStatus) ? "queued" : usage.VideoStatus)
            : useImageRuntime
                ? string.Format(CultureInfo.CurrentCulture, _resources.GetString("ImageMetricSummaryFormat"), usage.ImageCount)
                : string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("TokenMetricSummaryFormat"),
                usage.CompletionTokens,
                usage.PromptTokens);
        string performanceSummary = useVideoRuntime
            ? usage.VideoCount > 0
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    _resources.GetString("VideoPerformanceSummaryFormat"),
                    generationSeconds)
                : _resources.GetString("VideoQueuedPerformanceSummary")
            : useImageRuntime
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    _resources.GetString("ImagePerformanceSummaryFormat"),
                    stopwatch.Elapsed.TotalSeconds,
                    usage.ImageCount * 60d / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001d))
                : string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("TokenPerformanceSummaryFormat"),
                tokensPerSecond,
                stopwatch.Elapsed.TotalSeconds);
        _telemetryService.Add(new RequestMetric
        {
            Timestamp = DateTimeOffset.Now,
            Source = source,
            Endpoint = path,
            Model = string.IsNullOrWhiteSpace(requestedModel)
                ? (useVideoRuntime
                    ? _videoRuntimeService.Current.ModelId
                    : useImageRuntime
                        ? _imageRuntimeService.Current.ModelId
                        : _runtimeService.Current.ModelId)
                    ?? _resources.GetString("UnknownModel")
                : requestedModel,
            StatusCode = (int)backendResponse.StatusCode,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TokensPerSecond = tokensPerSecond,
            DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            IsStreaming = isStreaming,
            QuantitySummary = quantitySummary,
            PerformanceSummary = performanceSummary,
        });
    }

    private sealed record Usage(
        int PromptTokens = 0,
        int CompletionTokens = 0,
        double TokensPerSecond = 0,
        double GenerationSeconds = 0,
        int ImageCount = 0,
        int VideoCount = 0,
        string VideoStatus = "",
        string VideoSize = "",
        string VideoSeconds = "");

    private sealed class SpeechRequest
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("input")]
        public string Input { get; init; } = string.Empty;

        [JsonPropertyName("voice")]
        public string Voice { get; init; } = "af_heart";

        [JsonPropertyName("response_format")]
        public string ResponseFormat { get; init; } = "wav";

        [JsonPropertyName("speed")]
        public float Speed { get; init; } = 1f;
    }
}
