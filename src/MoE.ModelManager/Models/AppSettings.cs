namespace MoE.ModelManager.Models;

/// <summary>
/// Holds user preferences that persist between sessions.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// The theme value that follows the Windows app mode.
    /// </summary>
    public const string SystemTheme = "System";

    /// <summary>
    /// The light theme value.
    /// </summary>
    public const string LightTheme = "Light";

    /// <summary>
    /// The dark theme value.
    /// </summary>
    public const string DarkTheme = "Dark";

    /// <summary>
    /// Gets or sets a value indicating whether chat conversations are saved on this PC.
    /// </summary>
    public bool SaveChatHistory { get; set; } = true;

    /// <summary>
    /// Gets or sets the app theme: <see cref="SystemTheme"/>, <see cref="LightTheme"/>, or <see cref="DarkTheme"/>.
    /// </summary>
    public string Theme { get; set; } = SystemTheme;
}
