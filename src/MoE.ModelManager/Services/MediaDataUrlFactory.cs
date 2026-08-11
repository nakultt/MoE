using System.Diagnostics;

namespace MoE.ModelManager.Services;

internal static class MediaDataUrlFactory
{
    internal const long MaximumImageBytes = 20L * 1024 * 1024;
    internal const long MaximumAudioBytes = 64L * 1024 * 1024;
    internal const long MaximumVideoBytes = 180L * 1024 * 1024;

    internal static async Task<MediaAttachment> CreateAsync(
        string mediaPath,
        CancellationToken cancellationToken,
        bool extractEmbeddedAudio = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        FileInfo file = new(mediaPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The selected chat media file was not found.", mediaPath);
        }

        (string mediaType, string contentPartType, long maximumBytes) = Path.GetExtension(mediaPath).ToLowerInvariant() switch
        {
            ".png" => ("image/png", "image_url", MaximumImageBytes),
            ".jpg" or ".jpeg" => ("image/jpeg", "image_url", MaximumImageBytes),
            ".webp" => ("image/webp", "image_url", MaximumImageBytes),
            ".bmp" => ("image/bmp", "image_url", MaximumImageBytes),
            ".gif" => ("image/gif", "image_url", MaximumImageBytes),
            ".wav" => ("audio/wav", "input_audio", MaximumAudioBytes),
            ".mp3" => ("audio/mpeg", "input_audio", MaximumAudioBytes),
            ".flac" => ("audio/flac", "input_audio", MaximumAudioBytes),
            ".mp4" => ("video/mp4", "input_video", MaximumVideoBytes),
            ".mkv" => ("video/x-matroska", "input_video", MaximumVideoBytes),
            ".webm" => ("video/webm", "input_video", MaximumVideoBytes),
            ".mov" => ("video/quicktime", "input_video", MaximumVideoBytes),
            ".avi" => ("video/x-msvideo", "input_video", MaximumVideoBytes),
            _ => throw new InvalidDataException("Select a supported image, WAV/MP3/FLAC audio, or MP4/MKV/WebM/MOV/AVI video."),
        };
        if (file.Length > maximumBytes)
        {
            throw new InvalidDataException($"The selected media exceeds the {maximumBytes / (1024 * 1024)} MB limit.");
        }

        byte[] data = await File.ReadAllBytesAsync(mediaPath, cancellationToken);
        string? companionAudioDataUrl = contentPartType == "input_video" && extractEmbeddedAudio
            ? await TryCreateEmbeddedAudioDataUrlAsync(mediaPath, cancellationToken)
            : null;
        return new MediaAttachment(
            $"data:{mediaType};base64,{Convert.ToBase64String(data)}",
            contentPartType,
            file.Name,
            companionAudioDataUrl);
    }

    private static async Task<string?> TryCreateEmbeddedAudioDataUrlAsync(
        string videoPath,
        CancellationToken cancellationToken)
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "MoE.ModelManager", "embedded-audio");
        Directory.CreateDirectory(temporaryDirectory);
        string wavePath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.wav");
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (string argument in new[]
            {
                "-nostdin", "-hide_banner", "-loglevel", "error",
                "-i", videoPath,
                "-map", "0:a:0?", "-vn", "-ac", "1", "-ar", "16000",
                "-c:a", "pcm_s16le", "-y", wavePath,
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new IOException("FFmpeg could not be started for the video's audio track.");
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(standardError, standardOutput);
            if (process.ExitCode != 0 || !File.Exists(wavePath))
            {
                return null;
            }

            FileInfo waveFile = new(wavePath);
            if (waveFile.Length == 0 || waveFile.Length > MaximumAudioBytes)
            {
                return null;
            }

            byte[] waveData = await File.ReadAllBytesAsync(wavePath, cancellationToken);
            return $"data:audio/wav;base64,{Convert.ToBase64String(waveData)}";
        }
        finally
        {
            if (File.Exists(wavePath))
            {
                File.Delete(wavePath);
            }
        }
    }

    internal sealed record MediaAttachment(
        string DataUrl,
        string ContentPartType,
        string FileName,
        string? CompanionAudioDataUrl);
}
