using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private readonly IRequestTelemetryService _telemetryService;
    private WebApplication? _application;

    public OpenAiProxyService(
        IAppResources resources,
        IHttpClientFactory httpClientFactory,
        IModelRuntimeService runtimeService,
        IRequestTelemetryService telemetryService)
    {
        _resources = resources;
        _httpClientFactory = httpClientFactory;
        _runtimeService = runtimeService;
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
        application.MapMethods("/{**path}", ["GET", "POST", "OPTIONS"], HandleAsync);
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
        response.Headers.AccessControlAllowMethods = "GET, POST, OPTIONS";
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

            return new Usage(promptTokens, completionTokens, tokensPerSecond, generationSeconds);
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
                new { status = _runtimeService.Current.Status.ToString().ToLowerInvariant() });
            return;
        }

        if (!path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (_runtimeService.Current.Status != RuntimeStatus.Ready)
        {
            await WriteOpenAiErrorAsync(context, StatusCodes.Status503ServiceUnavailable, _resources.GetString("ApiNoModel"));
            return;
        }

        await ForwardAsync(context, path);
    }

    private async Task ForwardAsync(HttpContext context, string path)
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
        Uri backendUri = new($"http://127.0.0.1:{ModelRuntimeService.BackendPort}{path}{context.Request.QueryString}");
        using HttpRequestMessage backendRequest = new(new HttpMethod(context.Request.Method), backendUri);
        CopyRequestHeaders(context.Request, backendRequest);
        if (requestBody.Length > 0)
        {
            backendRequest.Content = new ByteArrayContent(requestBody);
            backendRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                context.Request.ContentType ?? "application/json");
        }

        HttpClient client = _httpClientFactory.CreateClient("LlamaBackend");
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
        _telemetryService.Add(new RequestMetric
        {
            Timestamp = DateTimeOffset.Now,
            Source = source,
            Endpoint = path,
            Model = string.IsNullOrWhiteSpace(requestedModel)
                ? _runtimeService.Current.ModelId ?? _resources.GetString("UnknownModel")
                : requestedModel,
            StatusCode = (int)backendResponse.StatusCode,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TokensPerSecond = tokensPerSecond,
            DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            IsStreaming = isStreaming,
        });
    }

    private sealed record Usage(
        int PromptTokens = 0,
        int CompletionTokens = 0,
        double TokensPerSecond = 0,
        double GenerationSeconds = 0);
}
