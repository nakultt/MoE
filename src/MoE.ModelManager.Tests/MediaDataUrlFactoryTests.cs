using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class MediaDataUrlFactoryTests
{
    [DataRow(".png", "data:image/png;base64,", "image_url")]
    [DataRow(".wav", "data:audio/wav;base64,", "input_audio")]
    [DataRow(".mp4", "data:video/mp4;base64,", "input_video")]
    [TestMethod]
    public async Task CreateAsyncMapsSupportedMediaToOpenAiContentType(
        string extension,
        string expectedPrefix,
        string expectedContentPartType)
    {
        string path = Path.Combine(Path.GetTempPath(), $"moe-media-{Guid.NewGuid():N}{extension}");
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

            MediaDataUrlFactory.MediaAttachment result =
                await MediaDataUrlFactory.CreateAsync(path, CancellationToken.None, extractEmbeddedAudio: false);

            StringAssert.StartsWith(result.DataUrl, expectedPrefix);
            Assert.AreEqual(expectedContentPartType, result.ContentPartType);
            Assert.AreEqual(Path.GetFileName(path), result.FileName);
            Assert.IsNull(result.CompanionAudioDataUrl);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task CreateAsyncRejectsUnsupportedExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), $"moe-media-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(path, "not media");

            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => MediaDataUrlFactory.CreateAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
