namespace MoE.ModelManager.Services;

/// <summary>
/// Maps model identifiers to the publisher logo and name shown beside them.
/// </summary>
internal static class ModelBrandCatalog
{
    private const string BrandAssetRoot = "ms-appx:///Assets/Brands/";

    private static readonly (string Keyword, string Asset, string Publisher)[] Brands =
    [
        ("qwen", "qwen.png", "Alibaba Cloud"),
        ("gemma", "gemma.png", "Google"),
        ("nemotron", "nvidia.png", "NVIDIA"),
        ("parakeet", "nvidia.png", "NVIDIA"),
        ("nvidia", "nvidia.png", "NVIDIA"),
        ("flux", "flux.png", "Black Forest Labs"),
        ("wan", "wan.png", "Wan"),
        ("kokoro", "kokoro.png", "hexgrad"),
    ];

    /// <summary>
    /// Gets the app logo used when a model has no known publisher.
    /// </summary>
    public static Uri FallbackLogo { get; } = new("ms-appx:///Assets/Square44x44Logo.targetsize-48_altform-unplated.png");

    /// <summary>
    /// Gets the publisher logo for a model identifier or name.
    /// </summary>
    /// <param name="modelKey">A model identifier, display name, or brand keyword.</param>
    /// <returns>The packaged logo URI.</returns>
    public static Uri GetLogo(string? modelKey)
    {
        int index = Find(modelKey);
        return index < 0 ? FallbackLogo : new Uri(BrandAssetRoot + Brands[index].Asset);
    }

    /// <summary>
    /// Gets the publisher name for a model identifier or name.
    /// </summary>
    /// <param name="modelKey">A model identifier, display name, or brand keyword.</param>
    /// <returns>The publisher name, or an empty string when unknown.</returns>
    public static string GetPublisher(string? modelKey)
    {
        int index = Find(modelKey);
        return index < 0 ? string.Empty : Brands[index].Publisher;
    }

    private static int Find(string? modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return -1;
        }

        for (int index = 0; index < Brands.Length; index++)
        {
            if (modelKey.Contains(Brands[index].Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
