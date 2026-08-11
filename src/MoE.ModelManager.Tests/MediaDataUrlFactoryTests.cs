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
                await MediaDataUrlFactory.CreateAsync(
                    path,
                    CancellationToken.None,
                    extractEmbeddedAudio: false,
                    optimizeVideo: false);

            StringAssert.StartsWith(result.DataUrl, expectedPrefix);
            Assert.AreEqual(expectedContentPartType, result.ContentPartType);
            Assert.AreEqual(Path.GetFileName(path), result.FileName);
            Assert.IsNull(result.CompanionAudioDataUrl);
            Assert.IsNull(result.VideoFrameDataUrls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataRow(2d, 2d)]
    [DataRow(6d, 0.6666666666666666d)]
    [DataRow(24d, 0.16666666666666666d)]
    [DataRow(120d, 0.03333333333333333d)]
    [TestMethod]
    public void CalculateVideoSamplingRateLimitsMediaToFourFrames(
        double durationSeconds,
        double expectedFramesPerSecond)
    {
        double actual = MediaDataUrlFactory.CalculateVideoSamplingRate(durationSeconds);

        Assert.AreEqual(expectedFramesPerSecond, actual, 0.000001d);
        Assert.IsLessThanOrEqualTo(
            MediaDataUrlFactory.MaximumVideoFrames + 0.000001d,
            actual * durationSeconds);
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
