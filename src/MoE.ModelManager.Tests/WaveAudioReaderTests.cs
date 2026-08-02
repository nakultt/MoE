using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class WaveAudioReaderTests
{
    [TestMethod]
    public void Read_ConvertsStereoPcm16ToMono()
    {
        string path = Path.Combine(Path.GetTempPath(), $"moe-wave-{Guid.NewGuid():N}.wav");
        try
        {
            short[] samples = [short.MaxValue, short.MinValue, 16384, 16384];
            using (FileStream stream = File.Create(path))
            using (BinaryWriter writer = new(stream))
            {
                writer.Write("RIFF"u8.ToArray());
                writer.Write(36 + (samples.Length * 2));
                writer.Write("WAVEfmt "u8.ToArray());
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)2);
                writer.Write(16000);
                writer.Write(16000 * 4);
                writer.Write((ushort)4);
                writer.Write((ushort)16);
                writer.Write("data"u8.ToArray());
                writer.Write(samples.Length * 2);
                foreach (short sample in samples)
                {
                    writer.Write(sample);
                }
            }

            WaveAudioData result = WaveAudioReader.Read(path);

            Assert.AreEqual(16000, result.SampleRate);
            Assert.HasCount(2, result.Samples);
            Assert.AreEqual(0f, result.Samples[0], 0.001f);
            Assert.AreEqual(0.5f, result.Samples[1], 0.001f);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
