using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Pure rules shared by the control API: kind names, GPU exclusivity, and launch options.
/// </summary>
internal static class ModelControlRules
{
    /// <summary>The model identifier that loads the Parakeet speech-to-text runtime.</summary>
    public const string SpeechToTextModelId = "parakeet-tdt-0.6b-v3";

    /// <summary>The model identifier that loads the Kokoro text-to-speech runtime.</summary>
    public const string TextToSpeechModelId = "kokoro-82m";

    /// <summary>The command-line switch that starts the app without a window.</summary>
    public const string HeadlessSwitch = "--headless";

    private static readonly (ModelKind Kind, string Name)[] KindNames =
    [
        (ModelKind.Text, "text"),
        (ModelKind.Image, "image"),
        (ModelKind.Video, "video"),
        (ModelKind.SpeechToText, "speech-to-text"),
        (ModelKind.TextToSpeech, "text-to-speech"),
    ];

    /// <summary>
    /// Gets the API name of a model kind.
    /// </summary>
    public static string ToApiName(ModelKind kind)
    {
        foreach ((ModelKind candidate, string name) in KindNames)
        {
            if (candidate == kind)
            {
                return name;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }

    /// <summary>
    /// Parses an API kind name such as <c>text</c> or <c>speech-to-text</c>.
    /// </summary>
    public static bool TryParseKind(string? value, out ModelKind kind)
    {
        foreach ((ModelKind candidate, string name) in KindNames)
        {
            if (name.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// Gets the runtimes that must be ejected before a model of <paramref name="kind"/> loads.
    /// Text, image, and video share the GPU, so only one of them may be loaded at a time.
    /// Speech runs on the CPU and never conflicts.
    /// </summary>
    public static IReadOnlyList<ModelKind> GetConflictingKinds(ModelKind kind)
    {
        return kind switch
        {
            ModelKind.Text => [ModelKind.Image, ModelKind.Video],
            ModelKind.Image => [ModelKind.Text, ModelKind.Video],
            ModelKind.Video => [ModelKind.Text, ModelKind.Image],
            _ => [],
        };
    }

    /// <summary>
    /// Gets a value indicating whether launch arguments ask for headless mode.
    /// </summary>
    public static bool IsHeadless(IEnumerable<string>? arguments)
    {
        return arguments?.Any(argument => argument.Trim().Trim('"').Equals(
            HeadlessSwitch,
            StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>
    /// Gets a value indicating whether a raw launch argument string asks for headless mode.
    /// </summary>
    public static bool IsHeadless(string? arguments)
    {
        return !string.IsNullOrWhiteSpace(arguments)
            && IsHeadless(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
