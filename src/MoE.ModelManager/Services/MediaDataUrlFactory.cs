using System.Diagnostics;
using System.Globalization;

namespace MoE.ModelManager.Services;

internal static class MediaDataUrlFactory
{
    internal const long MaximumImageBytes = 20L * 1024 * 1024;
    internal const long MaximumAudioBytes = 64L * 1024 * 1024;
    internal const long MaximumVideoBytes = 180L * 1024 * 1024;
    internal const int MaximumVideoFrames = 4;
    private const double LlamaVideoFramesPerSecond = 4d;

    internal static async Task<MediaAttachment> CreateAsync(
        string mediaPath,
        CancellationToken cancellationToken,
        bool extractEmbeddedAudio = true,
        bool optimizeVideo = true)
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

        IReadOnlyList<string>? videoFrameDataUrls = contentPartType == "input_video" && optimizeVideo
            ? await CreateVideoFrameDataUrlsAsync(mediaPath, cancellationToken)
            : null;
        string dataUrl = videoFrameDataUrls is { Count: > 0 }
            ? videoFrameDataUrls[0]
            : $"data:{mediaType};base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(mediaPath, cancellationToken))}";
        string? companionAudioDataUrl = contentPartType == "input_video" && extractEmbeddedAudio
            ? await TryCreateEmbeddedAudioDataUrlAsync(mediaPath, cancellationToken)
            : null;
        return new MediaAttachment(
            dataUrl,
            contentPartType,
            file.Name,
            companionAudioDataUrl,
            videoFrameDataUrls);
    }

    internal static double CalculateVideoSamplingRate(double durationSeconds)
    {
        if (durationSeconds <= 0 || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        return Math.Min(LlamaVideoFramesPerSecond, MaximumVideoFrames / durationSeconds);
    }

    private static async Task<IReadOnlyList<string>> CreateVideoFrameDataUrlsAsync(
        string videoPath,
        CancellationToken cancellationToken)
    {
        double durationSeconds = await ReadVideoDurationAsync(videoPath, cancellationToken);
        double samplingRate = CalculateVideoSamplingRate(durationSeconds);
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "MoE.ModelManager",
            "video-frames",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(temporaryDirectory);
        string framePattern = Path.Combine(temporaryDirectory, "frame-%02d.jpg");
        string filter = string.Format(
            CultureInfo.InvariantCulture,
            "fps={0:F6},trim=end_frame={1},scale=384:384:force_original_aspect_ratio=decrease,pad=384:384:(ow-iw)/2:(oh-ih)/2",
            samplingRate,
            MaximumVideoFrames);
        try
        {
            ProcessStartInfo startInfo = CreateProcessStartInfo("ffmpeg");
            foreach (string argument in new[]
            {
                "-nostdin", "-hide_banner", "-loglevel", "error",
                "-i", videoPath,
                "-an", "-vf", filter,
                "-frames:v", MaximumVideoFrames.ToString(CultureInfo.InvariantCulture),
                "-q:v", "3", "-y", framePattern,
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            (int exitCode, string error) = await RunProcessAsync(startInfo, cancellationToken);
            string[] framePaths = Directory.GetFiles(temporaryDirectory, "frame-*.jpg")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (exitCode != 0 || framePaths.Length == 0)
            {
                throw new IOException($"FFmpeg could not sample the video for local analysis. {error}".Trim());
            }

            List<string> dataUrls = new(framePaths.Length);
            foreach (string framePath in framePaths)
            {
                byte[] frameData = await File.ReadAllBytesAsync(framePath, cancellationToken);
                dataUrls.Add($"data:image/jpeg;base64,{Convert.ToBase64String(frameData)}");
            }

            return dataUrls;
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static async Task<double> ReadVideoDurationAsync(
        string videoPath,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = CreateProcessStartInfo("ffprobe");
        foreach (string argument in new[]
        {
            "-v", "error", "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1", videoPath,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        (int exitCode, string error, string output) = await RunProcessWithOutputAsync(startInfo, cancellationToken);
        if (exitCode != 0
            || !double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double duration)
            || duration <= 0)
        {
            throw new IOException($"FFprobe could not determine the video duration. {error}".Trim());
        }

        return duration;
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
            ProcessStartInfo startInfo = CreateProcessStartInfo("ffmpeg");
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

            (int exitCode, _) = await RunProcessAsync(startInfo, cancellationToken);
            if (exitCode != 0 || !File.Exists(wavePath))
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

    private static ProcessStartInfo CreateProcessStartInfo(string fileName)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
    }

    private static async Task<(int ExitCode, string Error)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        (int exitCode, string error, _) = await RunProcessWithOutputAsync(startInfo, cancellationToken);
        return (exitCode, error);
    }

    private static async Task<(int ExitCode, string Error, string Output)> RunProcessWithOutputAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new IOException($"{startInfo.FileName} could not be started.");
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(standardError, standardOutput);
            return (process.ExitCode, await standardError, await standardOutput);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new IOException($"{startInfo.FileName} is required for video analysis but was not found.", exception);
        }
    }

    internal sealed record MediaAttachment(
        string DataUrl,
        string ContentPartType,
        string FileName,
        string? CompanionAudioDataUrl,
        IReadOnlyList<string>? VideoFrameDataUrls);
}
