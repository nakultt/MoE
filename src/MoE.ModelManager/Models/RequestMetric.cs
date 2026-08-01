using System.Globalization;

namespace MoE.ModelManager.Models;

/// <summary>
/// Represents telemetry for one completed OpenAI-compatible API request.
/// </summary>
public sealed class RequestMetric
{
    /// <summary>
    /// Gets the request completion time.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the request source label.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the requested endpoint.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Gets the requested model identifier.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets the HTTP response status code.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the prompt token count.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Gets the generated token count.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Gets the sustained generation rate.
    /// </summary>
    public double TokensPerSecond { get; init; }

    /// <summary>
    /// Gets the total request duration.
    /// </summary>
    public double DurationMilliseconds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request streamed tokens.
    /// </summary>
    public bool IsStreaming { get; init; }

    /// <summary>
    /// Gets a culture-aware timestamp for display.
    /// </summary>
    public string TimeSummary => Timestamp.ToLocalTime().ToString("T", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets the culture-aware generation rate number for display.
    /// </summary>
    public string TokensPerSecondText => TokensPerSecond.ToString("N2", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets the culture-aware duration in seconds for display.
    /// </summary>
    public string DurationSecondsText => (DurationMilliseconds / 1000d).ToString("N2", CultureInfo.CurrentCulture);
}
