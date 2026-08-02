using System.Text;

namespace MoE.ModelManager.Services;

internal static class WaveAudioReader
{
    public static WaveAudioData Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException("Only RIFF WAV audio is currently supported.");
        }

        _ = reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException("The selected file is not a WAV audio file.");
        }

        ushort format = 0;
        ushort channels = 0;
        int sampleRate = 0;
        ushort bitsPerSample = 0;
        byte[]? audio = null;
        while (stream.Position + 8 <= stream.Length)
        {
            string chunkId = new(reader.ReadChars(4));
            uint chunkSize = reader.ReadUInt32();
            long next = stream.Position + chunkSize + (chunkSize & 1);
            if (chunkId == "fmt ")
            {
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                audio = reader.ReadBytes(checked((int)chunkSize));
            }

            stream.Position = Math.Min(next, stream.Length);
        }

        if (audio is null || channels == 0 || sampleRate <= 0)
        {
            throw new InvalidDataException("The WAV file does not contain readable audio samples.");
        }

        int bytesPerSample = bitsPerSample / 8;
        if ((format != 1 || bitsPerSample is not (16 or 24 or 32))
            && (format != 3 || bitsPerSample != 32))
        {
            throw new InvalidDataException("Use PCM 16/24/32-bit or IEEE float WAV audio.");
        }

        int frameCount = audio.Length / (bytesPerSample * channels);
        float[] mono = new float[frameCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            float sum = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                int offset = ((frame * channels) + channel) * bytesPerSample;
                sum += ReadSample(audio, offset, format, bitsPerSample);
            }

            mono[frame] = sum / channels;
        }

        return new WaveAudioData(sampleRate, mono);
    }

    private static float ReadSample(byte[] data, int offset, ushort format, ushort bitsPerSample)
    {
        if (format == 3)
        {
            return BitConverter.ToSingle(data, offset);
        }

        return bitsPerSample switch
        {
            16 => BitConverter.ToInt16(data, offset) / 32768f,
            24 => ReadInt24(data, offset) / 8388608f,
            32 => BitConverter.ToInt32(data, offset) / 2147483648f,
            _ => 0,
        };
    }

    private static int ReadInt24(byte[] data, int offset)
    {
        int value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        return (value & 0x800000) != 0 ? value | unchecked((int)0xff000000) : value;
    }
}
