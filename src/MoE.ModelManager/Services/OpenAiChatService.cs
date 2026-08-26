using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public async IAsyncEnumerable<ChatStreamDelta> StreamResponseAsync(
        string modelId,
        IReadOnlyList<ChatMessage> messages,
        bool? enableThinking,
        int thinkingBudgetTokens,
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
            if (enableThinking.Value && thinkingBudgetTokens > 0)
            {
                payload["reasoning_budget_tokens"] = thinkingBudgetTokens;
            }
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

            ChatStreamDelta? delta = ReadDelta(line[6..]);
            if (delta is not null)
            {
                yield return delta.Value;
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

    private static JsonArray CreateMultimodalContent(ChatMessage message)
    {
        return MultimodalContentBuilder.Build(
            message.Content,
            message.AttachmentKind ?? "image_url",
            message.AttachmentDataUrl!,
            message.CompanionAudioDataUrl,
            message.VideoFrameDataUrls);
    }

    private static ChatStreamDelta? ReadDelta(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? content = null;
            string? reasoning = null;
            if (root.TryGetProperty("choices", out JsonElement choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("delta", out JsonElement delta))
            {
                content = ReadDeltaText(delta, "content");
                reasoning = ReadDeltaText(delta, "reasoning_content");
            }

            int promptTokens = 0;
            int completionTokens = 0;
            if (root.TryGetProperty("usage", out JsonElement usage)
                && usage.ValueKind == JsonValueKind.Object)
            {
                promptTokens = ReadTokenCount(usage, "prompt_tokens");
                completionTokens = ReadTokenCount(usage, "completion_tokens");
            }

            return string.IsNullOrEmpty(content)
                && string.IsNullOrEmpty(reasoning)
                && promptTokens == 0
                && completionTokens == 0
                    ? null
                    : new ChatStreamDelta(content, reasoning, promptTokens, completionTokens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadDeltaText(JsonElement delta, string propertyName)
    {
        return delta.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static int ReadTokenCount(JsonElement usage, string propertyName)
    {
        return usage.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int tokens)
                ? tokens
                : 0;
    }
}
