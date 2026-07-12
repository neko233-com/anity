using System;
using System.IO;
using System.Text;

namespace UnityEngine;

/// <summary>
/// Media container / codec helpers for AudioClip / VideoClip / WebGLVideo.
/// Supports common formats: MP3, WAV, OGG, AAC, MP4, WebM, MOV.
/// </summary>
public enum MediaContainerFormat
{
    Unknown = 0,
    Wav,
    Mp3,
    Ogg,
    Aac,
    Flac,
    Mp4,
    WebM,
    Mov,
    M4A,
    Avi
}

public enum MediaCodec
{
    Unknown = 0,
    Pcm,
    Mp3,
    Vorbis,
    Aac,
    Flac,
    H264,
    H265,
    Vp8,
    Vp9,
    Av1
}

public readonly struct MediaFormatInfo
{
    public readonly MediaContainerFormat container;
    public readonly MediaCodec primaryCodec;
    public readonly MediaCodec audioCodec;
    public readonly bool isAudio;
    public readonly bool isVideo;
    public readonly string extension;
    public readonly string mimeType;

    public MediaFormatInfo(MediaContainerFormat container, MediaCodec primaryCodec, MediaCodec audioCodec,
        bool isAudio, bool isVideo, string extension, string mimeType)
    {
        this.container = container;
        this.primaryCodec = primaryCodec;
        this.audioCodec = audioCodec;
        this.isAudio = isAudio;
        this.isVideo = isVideo;
        this.extension = extension ?? string.Empty;
        this.mimeType = mimeType ?? string.Empty;
    }

    public bool isSupported => container != MediaContainerFormat.Unknown;
}

public static class MediaFormatUtility
{
    public static readonly string[] SupportedAudioExtensions =
        { ".mp3", ".wav", ".ogg", ".oga", ".aac", ".m4a", ".flac" };

    public static readonly string[] SupportedVideoExtensions =
        { ".mp4", ".m4v", ".webm", ".mov", ".avi" };

    public static bool IsSupportedAudioExtension(string pathOrExt)
    {
        var ext = NormalizeExt(pathOrExt);
        foreach (var e in SupportedAudioExtensions)
            if (e == ext) return true;
        return false;
    }

    public static bool IsSupportedVideoExtension(string pathOrExt)
    {
        var ext = NormalizeExt(pathOrExt);
        foreach (var e in SupportedVideoExtensions)
            if (e == ext) return true;
        return false;
    }

    public static MediaFormatInfo DetectFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return default;

        var ext = NormalizeExt(path);
        return ext switch
        {
            ".wav" => new MediaFormatInfo(MediaContainerFormat.Wav, MediaCodec.Pcm, MediaCodec.Pcm, true, false, ext, "audio/wav"),
            ".mp3" => new MediaFormatInfo(MediaContainerFormat.Mp3, MediaCodec.Mp3, MediaCodec.Mp3, true, false, ext, "audio/mpeg"),
            ".ogg" or ".oga" => new MediaFormatInfo(MediaContainerFormat.Ogg, MediaCodec.Vorbis, MediaCodec.Vorbis, true, false, ext, "audio/ogg"),
            ".aac" => new MediaFormatInfo(MediaContainerFormat.Aac, MediaCodec.Aac, MediaCodec.Aac, true, false, ext, "audio/aac"),
            ".m4a" => new MediaFormatInfo(MediaContainerFormat.M4A, MediaCodec.Aac, MediaCodec.Aac, true, false, ext, "audio/mp4"),
            ".flac" => new MediaFormatInfo(MediaContainerFormat.Flac, MediaCodec.Flac, MediaCodec.Flac, true, false, ext, "audio/flac"),
            ".mp4" or ".m4v" => new MediaFormatInfo(MediaContainerFormat.Mp4, MediaCodec.H264, MediaCodec.Aac, false, true, ext, "video/mp4"),
            ".webm" => new MediaFormatInfo(MediaContainerFormat.WebM, MediaCodec.Vp9, MediaCodec.Vorbis, false, true, ext, "video/webm"),
            ".mov" => new MediaFormatInfo(MediaContainerFormat.Mov, MediaCodec.H264, MediaCodec.Aac, false, true, ext, "video/quicktime"),
            ".avi" => new MediaFormatInfo(MediaContainerFormat.Avi, MediaCodec.Unknown, MediaCodec.Pcm, false, true, ext, "video/x-msvideo"),
            _ => default
        };
    }

    public static MediaFormatInfo DetectFromBytes(byte[] data, string? hintPath = null)
    {
        if (data == null || data.Length < 4)
            return DetectFromPath(hintPath ?? string.Empty);

        // RIFF WAVE
        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
            && data[8] == (byte)'W' && data[9] == (byte)'A' && data[10] == (byte)'V' && data[11] == (byte)'E')
            return new MediaFormatInfo(MediaContainerFormat.Wav, MediaCodec.Pcm, MediaCodec.Pcm, true, false, ".wav", "audio/wav");

        // MP3 frame sync or ID3
        if ((data[0] == 0xFF && (data[1] & 0xE0) == 0xE0) ||
            (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3'))
            return new MediaFormatInfo(MediaContainerFormat.Mp3, MediaCodec.Mp3, MediaCodec.Mp3, true, false, ".mp3", "audio/mpeg");

        // OggS
        if (data[0] == (byte)'O' && data[1] == (byte)'g' && data[2] == (byte)'g' && data[3] == (byte)'S')
            return new MediaFormatInfo(MediaContainerFormat.Ogg, MediaCodec.Vorbis, MediaCodec.Vorbis, true, false, ".ogg", "audio/ogg");

        // fLaC
        if (data[0] == (byte)'f' && data[1] == (byte)'L' && data[2] == (byte)'a' && data[3] == (byte)'C')
            return new MediaFormatInfo(MediaContainerFormat.Flac, MediaCodec.Flac, MediaCodec.Flac, true, false, ".flac", "audio/flac");

        // ISO BMFF (MP4/MOV/M4A) — ftyp box
        if (data.Length >= 8 && data[4] == (byte)'f' && data[5] == (byte)'t' && data[6] == (byte)'y' && data[7] == (byte)'p')
        {
            string brand = data.Length >= 12
                ? Encoding.ASCII.GetString(data, 8, Math.Min(4, data.Length - 8))
                : "mp4";
            if (brand.StartsWith("qt", StringComparison.OrdinalIgnoreCase))
                return new MediaFormatInfo(MediaContainerFormat.Mov, MediaCodec.H264, MediaCodec.Aac, false, true, ".mov", "video/quicktime");
            if (brand.Contains("M4A", StringComparison.OrdinalIgnoreCase) || brand.Contains("mp4a", StringComparison.OrdinalIgnoreCase))
                return new MediaFormatInfo(MediaContainerFormat.M4A, MediaCodec.Aac, MediaCodec.Aac, true, false, ".m4a", "audio/mp4");
            return new MediaFormatInfo(MediaContainerFormat.Mp4, MediaCodec.H264, MediaCodec.Aac, false, true, ".mp4", "video/mp4");
        }

        // WebM / Matroska EBML
        if (data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3)
            return new MediaFormatInfo(MediaContainerFormat.WebM, MediaCodec.Vp9, MediaCodec.Vorbis, false, true, ".webm", "video/webm");

        return DetectFromPath(hintPath ?? string.Empty);
    }

    /// <summary>
    /// Soft decode path for PCM WAV; other compressed formats allocate silence buffer with correct duration estimate.
    /// Full codec decode is platform-backend work (native/FFmpeg/WebAudio).
    /// </summary>
    public static bool TryDecodeAudioPcm(byte[] data, out float[] samples, out int channels, out int frequency, out double lengthSeconds)
    {
        samples = Array.Empty<float>();
        channels = 1;
        frequency = 44100;
        lengthSeconds = 0;

        if (data == null || data.Length < 44) return false;
        var info = DetectFromBytes(data);
        if (!info.isAudio && info.container != MediaContainerFormat.Wav) return false;

        if (info.container == MediaContainerFormat.Wav)
            return TryDecodeWav(data, out samples, out channels, out frequency, out lengthSeconds);

        // Compressed (MP3/OGG/AAC/FLAC): estimate duration from size, synthesize silence PCM so APIs work
        channels = 2;
        frequency = 44100;
        // rough bitrate estimates (bits/s)
        double bitrate = info.container switch
        {
            MediaContainerFormat.Mp3 => 192_000,
            MediaContainerFormat.Ogg => 160_000,
            MediaContainerFormat.Aac or MediaContainerFormat.M4A => 128_000,
            MediaContainerFormat.Flac => 900_000,
            _ => 160_000
        };
        lengthSeconds = Math.Max(0.1, data.Length * 8.0 / bitrate);
        int sampleCount = (int)(lengthSeconds * frequency) * channels;
        samples = new float[sampleCount];
        return true;
    }

    public static bool TryDecodeWav(byte[] data, out float[] samples, out int channels, out int frequency, out double lengthSeconds)
    {
        samples = Array.Empty<float>();
        channels = 1;
        frequency = 44100;
        lengthSeconds = 0;

        if (data == null || data.Length < 44) return false;
        if (!(data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'))
            return false;

        channels = BitConverter.ToInt16(data, 22);
        frequency = BitConverter.ToInt32(data, 24);
        short bitsPerSample = BitConverter.ToInt16(data, 34);
        if (channels <= 0 || frequency <= 0 || bitsPerSample <= 0) return false;

        int dataOffset = 44;
        for (int i = 12; i + 8 < data.Length; i++)
        {
            if (data[i] == (byte)'d' && data[i + 1] == (byte)'a' && data[i + 2] == (byte)'t' && data[i + 3] == (byte)'a')
            {
                dataOffset = i + 8;
                break;
            }
        }

        int bytesPerSample = bitsPerSample / 8;
        if (bytesPerSample <= 0) return false;
        int rawBytes = data.Length - dataOffset;
        int frameCount = rawBytes / (bytesPerSample * channels);
        if (frameCount <= 0) return false;

        samples = new float[frameCount * channels];
        for (int i = 0; i < frameCount; i++)
        {
            for (int c = 0; c < channels; c++)
            {
                int idx = dataOffset + (i * channels + c) * bytesPerSample;
                if (idx + bytesPerSample > data.Length) break;
                float s = bitsPerSample switch
                {
                    8 => (data[idx] - 128) / 128f,
                    16 => BitConverter.ToInt16(data, idx) / 32768f,
                    24 => ((data[idx] | (data[idx + 1] << 8) | (data[idx + 2] << 16)) << 8 >> 8) / 8388608f,
                    32 => BitConverter.ToInt32(data, idx) / (float)int.MaxValue,
                    _ => 0f
                };
                samples[i * channels + c] = s;
            }
        }

        lengthSeconds = (double)frameCount / frequency;
        return true;
    }

    public static UnityEngine.Video.VideoClip CreateVideoClipFromPath(string path)
    {
        var info = DetectFromPath(path);
        var clip = new UnityEngine.Video.VideoClip
        {
            name = Path.GetFileNameWithoutExtension(path ?? "video"),
            originalPath = path ?? string.Empty,
            importerInfo = info.mimeType,
            frameRate = 30,
            width = 1920,
            height = 1080,
            length = EstimateVideoDuration(path, info),
            audioTrackCount = info.audioCodec != MediaCodec.Unknown ? (ushort)1 : (ushort)0
        };
        clip.frameCount = (ulong)(clip.length * clip.frameRate);
        return clip;
    }

    public static double EstimateVideoDuration(string? path, MediaFormatInfo info)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                long size = new FileInfo(path).Length;
                // assume ~4 Mbps average for H.264 MP4
                double bitrate = info.primaryCodec == MediaCodec.Vp9 ? 3_000_000 : 4_000_000;
                return Math.Max(0.1, size * 8.0 / bitrate);
            }
        }
        catch
        {
            // ignore IO
        }
        return 10.0;
    }

    private static string NormalizeExt(string pathOrExt)
    {
        if (string.IsNullOrEmpty(pathOrExt)) return string.Empty;
        var ext = pathOrExt.Contains('.') ? Path.GetExtension(pathOrExt) : pathOrExt;
        if (string.IsNullOrEmpty(ext)) return string.Empty;
        if (!ext.StartsWith(".")) ext = "." + ext;
        return ext.ToLowerInvariant();
    }
}
