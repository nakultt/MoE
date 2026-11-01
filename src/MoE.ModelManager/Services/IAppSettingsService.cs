using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Loads and saves user preferences.
/// </summary>
internal interface IAppSettingsService
{
    /// <summary>
    /// Gets the current preferences.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Writes the current preferences to disk.
    /// </summary>
    void Save();
}
