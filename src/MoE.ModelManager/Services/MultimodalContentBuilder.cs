using System.Text.Json.Nodes;

namespace MoE.ModelManager.Services;

internal static class MultimodalContentBuilder
{
    internal static JsonArray Build(
        string text,
        string attachmentKind,
        string attachmentDataUrl,
        string? companionAudioDataUrl = null)
    {
        JsonObject textPart = new()
        {
            ["type"] = "text",
            ["text"] = text,
        };
        JsonObject mediaPart = CreateMediaPart(attachmentKind, attachmentDataUrl);
        JsonArray content = [];

        if (attachmentKind == "image_url")
        {
            content.Add(textPart);
            content.Add(mediaPart);
            return content;
        }

        // NVIDIA's reference Nemotron format places audio/video before its question.
        content.Add(mediaPart);
        if (!string.IsNullOrWhiteSpace(companionAudioDataUrl))
        {
            content.Add(CreateMediaPart("input_audio", companionAudioDataUrl));
        }

        content.Add(textPart);
        return content;
    }

    private static JsonObject CreateMediaPart(string attachmentKind, string dataUrl)
    {
        if (attachmentKind == "input_audio")
        {
            return new JsonObject
            {
                ["type"] = "input_audio",
                ["input_audio"] = new JsonObject
                {
                    ["data"] = StripDataUrlPrefix(dataUrl),
                },
            };
        }

        if (attachmentKind == "input_video")
        {
            return new JsonObject
            {
                ["type"] = "input_video",
                ["input_video"] = new JsonObject
                {
                    ["data"] = StripDataUrlPrefix(dataUrl),
                },
            };
        }

        return new JsonObject
        {
            ["type"] = "image_url",
            ["image_url"] = new JsonObject
            {
                ["url"] = dataUrl,
            },
        };
    }

    private static string StripDataUrlPrefix(string value)
    {
        if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        int comma = value.IndexOf(',', StringComparison.Ordinal);
        return comma >= 0 ? value[(comma + 1)..] : value;
    }
}
