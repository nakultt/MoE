using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class AppSettingsServiceTests
{
    [TestMethod]
    public void Constructor_MissingFile_UsesDefaults()
    {
        string path = CreatePath();

        AppSettingsService service = new(path);

        Assert.IsTrue(service.Current.SaveChatHistory);
        Assert.AreEqual(AppSettings.SystemTheme, service.Current.Theme);
    }

    [TestMethod]
    public void Save_ThenReload_PersistsValues()
    {
        string path = CreatePath();
        try
        {
            AppSettingsService service = new(path);
            service.Current.SaveChatHistory = false;
            service.Current.Theme = AppSettings.DarkTheme;

            service.Save();
            AppSettingsService reloaded = new(path);

            Assert.IsFalse(reloaded.Current.SaveChatHistory);
            Assert.AreEqual(AppSettings.DarkTheme, reloaded.Current.Theme);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_DamagedFileOrUnknownTheme_FallsBackSafely()
    {
        string path = CreatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "{ broken");
            Assert.AreEqual(AppSettings.SystemTheme, new AppSettingsService(path).Current.Theme);

            File.WriteAllText(path, "{\"theme\":\"Neon\",\"saveChatHistory\":false}");
            AppSettingsService service = new(path);
            Assert.AreEqual(AppSettings.SystemTheme, service.Current.Theme);
            Assert.IsFalse(service.Current.SaveChatHistory);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    private static string CreatePath()
    {
        return Path.Combine(Path.GetTempPath(), $"moe-settings-{Guid.NewGuid():N}", "settings.json");
    }
}
