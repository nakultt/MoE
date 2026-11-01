using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Persists preferences as a small JSON file next to the other app data.
/// </summary>
internal sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public AppSettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "settings.json"))
    {
    }

    public AppSettingsService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
        Current = Load(settingsPath);
    }

    public AppSettings Current { get; }

    public void Save()
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Current, SerializerOptions));
    }

    private static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), SerializerOptions);
                if (settings is not null)
                {
                    settings.Theme = NormalizeTheme(settings.Theme);
                    return settings;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall back to defaults; the next save rewrites the damaged file.
        }

        return new AppSettings();
    }

    private static string NormalizeTheme(string? theme)
    {
        return theme switch
        {
            AppSettings.LightTheme => AppSettings.LightTheme,
            AppSettings.DarkTheme => AppSettings.DarkTheme,
            _ => AppSettings.SystemTheme,
        };
    }
}
