using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class OpenAiChatService : IOpenAiChatService
{
    private readonly IApiProxyService _apiProxyService;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiChatService(IApiProxyService apiProxyService, IHttpClientFactory httpClientFactory)
    {
        _apiProxyService = apiProxyService;
        _httpClientFactory = httpClientFactory;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        string modelId,
        IReadOnlyList<ChatMessage> messages,
        bool? enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(messages);

        Dictionary<string, object> payload = new()
        {
            ["model"] = modelId,
            ["messages"] = messages.Select(CreateMessagePayload).ToArray(),
            ["stream"] = true,
            ["stream_options"] = new { include_usage = true },
            ["temperature"] = enableThinking == true ? 0.6 : 0.2,
        };
        if (enableThinking.HasValue)
        {
            payload["chat_template_kwargs"] = new { enable_thinking = enableThinking.Value };
        }

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"{_apiProxyService.BaseUrl}/chat/completions")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-MoE-Client", "desktop");

        HttpClient client = _httpClientFactory.CreateClient("LocalApi");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(responseStream, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is string line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal)
                || line.Equals("data: [DONE]", StringComparison.Ordinal))
            {
                continue;
            }

            string? content = ReadContentDelta(line[6..]);
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    private static object CreateMessagePayload(ChatMessage message)
    {
        object content = string.IsNullOrWhiteSpace(message.AttachmentDataUrl)
            ? message.Content
            : CreateMultimodalContent(message);
        return new
        {
            role = message.Role,
            content,
        };
    }

    private static object[] CreateMultimodalContent(ChatMessage message)
    {
        object mediaPart = message.AttachmentKind switch
        {
            "input_audio" => new
            {
                type = "input_audio",
                input_audio = new { data = StripDataUrlPrefix(message.AttachmentDataUrl) },
            },
            "input_video" => new
            {
                type = "input_video",
                input_video = new { data = StripDataUrlPrefix(message.AttachmentDataUrl) },
            },
            _ => new
            {
                type = "image_url",
                image_url = new { url = message.AttachmentDataUrl },
            },
        };
        List<object> content =
        [
            new
            {
                type = "text",
                text = message.Content,
            },
            mediaPart,
        ];
        if (!string.IsNullOrWhiteSpace(message.CompanionAudioDataUrl))
        {
            content.Add(new
            {
                type = "input_audio",
                input_audio = new { data = StripDataUrlPrefix(message.CompanionAudioDataUrl) },
            });
        }

        return [.. content];
    }

    private static string? StripDataUrlPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        int comma = value.IndexOf(',', StringComparison.Ordinal);
        return comma >= 0 ? value[(comma + 1)..] : value;
    }

    private static string? ReadContentDelta(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("choices", out JsonElement choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement choice = choices[0];
            return choice.TryGetProperty("delta", out JsonElement delta)
                && delta.TryGetProperty("content", out JsonElement content)
                    ? content.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
