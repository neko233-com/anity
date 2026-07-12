using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.Audio;

namespace UnityEngine;

public delegate void PCMReaderCallback(float[] data);
public delegate void PCMSetPositionCallback(int position);

public class AudioClip : Object
{
    private float[] _sampleData = Array.Empty<float>();
    private bool _isLoaded;
    private AudioClipLoadType _loadType = AudioClipLoadType.DecompressOnLoad;
    private string _originalPath = string.Empty;
    private MediaContainerFormat _containerFormat = MediaContainerFormat.Unknown;
    private MediaCodec _codec = MediaCodec.Unknown;

    public float length => samples > 0 && frequency > 0 ? (float)samples / frequency / Math.Max(1, channels) : 0f;
    public int samples { get; private set; }
    public int channels { get; private set; }
    public int frequency { get; private set; }
    public bool ambisonic { get; set; }
    public bool preloadAudioData { get; set; } = true;
    public bool loadInBackground { get; set; }
    public AudioClipLoadType loadType
    {
        get => _loadType;
        private set => _loadType = value;
    }
    public bool loadState => _isLoaded;
    public string originalPath => _originalPath;
    public MediaContainerFormat containerFormat => _containerFormat;
    public MediaCodec codec => _codec;
    /// <summary>True for MP3/OGG/AAC/FLAC compressed imports (soft-decoded or streaming).</summary>
    public bool isCompressedFormat =>
        _containerFormat is MediaContainerFormat.Mp3 or MediaContainerFormat.Ogg
            or MediaContainerFormat.Aac or MediaContainerFormat.M4A or MediaContainerFormat.Flac;

    public bool GetData(float[] data, int offsetSamples)
    {
        if (data == null || _sampleData.Length == 0) return false;
        int copyLength = Math.Min(data.Length, _sampleData.Length - offsetSamples * channels);
        if (copyLength <= 0) return false;
        Array.Copy(_sampleData, offsetSamples * channels, data, 0, copyLength);
        if (copyLength < data.Length)
            Array.Clear(data, copyLength, data.Length - copyLength);
        return true;
    }

    public bool SetData(float[] data, int offsetSamples)
    {
        if (data == null) return false;
        if (_sampleData.Length == 0 && offsetSamples == 0)
        {
            _sampleData = new float[data.Length];
        }
        if (offsetSamples * channels + data.Length > _sampleData.Length)
        {
            Array.Resize(ref _sampleData, offsetSamples * channels + data.Length);
        }
        Array.Copy(data, 0, _sampleData, offsetSamples * channels, data.Length);
        samples = _sampleData.Length / Math.Max(1, channels);
        return true;
    }

    public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream)
    {
        return Create(name, lengthSamples, channels, frequency, stream, false, AudioClipLoadType.DecompressOnLoad, null, null);
    }

    public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream, bool threeD)
    {
        return Create(name, lengthSamples, channels, frequency, stream, threeD, AudioClipLoadType.DecompressOnLoad, null, null);
    }

    public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream, bool threeD, AudioClipLoadType loadType)
    {
        return Create(name, lengthSamples, channels, frequency, stream, threeD, loadType, null, null);
    }

    public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream, PCMReaderCallback pcmreadercallback, PCMSetPositionCallback pcmsetpositioncallback)
    {
        return Create(name, lengthSamples, channels, frequency, stream, false, AudioClipLoadType.DecompressOnLoad, pcmreadercallback, pcmsetpositioncallback);
    }

    public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream, bool threeD, AudioClipLoadType loadType, PCMReaderCallback pcmreadercallback, PCMSetPositionCallback pcmsetpositioncallback)
    {
        _ = threeD;
        _ = pcmreadercallback;
        _ = pcmsetpositioncallback;
        var clip = new AudioClip
        {
            name = name,
            samples = lengthSamples,
            channels = Math.Max(1, channels),
            frequency = Math.Max(1, frequency),
            _sampleData = new float[lengthSamples * channels],
            _isLoaded = true,
            _loadType = loadType
        };
        return clip;
    }

    public bool LoadAudioData()
    {
        _isLoaded = true;
        return true;
    }

    public bool UnloadAudioData()
    {
        _isLoaded = false;
        return true;
    }

    /// <summary>
    /// Load audio from path. Supports .mp3, .wav, .ogg, .aac, .m4a, .flac.
    /// WAV is fully PCM-decoded; compressed formats use soft decode (duration + PCM buffer).
    /// </summary>
    public static AudioClip? CreateFromFile(string path, bool stream = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        // Prefer anity-native C++ decoder (Unity audio backend parity)
        if (Anity.Core.Runtime.Native.AnityNative.Available)
        {
            try
            {
                if (Anity.Core.Runtime.Native.AnityNative.Audio_DecodeFile(path, out var ptr, out int sc, out int ch, out int freq)
                    == Anity.Core.Runtime.Native.AnityNative.Result.Ok && ptr != IntPtr.Zero && sc > 0)
                {
                    var samples = new float[sc];
                    System.Runtime.InteropServices.Marshal.Copy(ptr, samples, 0, sc);
                    Anity.Core.Runtime.Native.AnityNative.Audio_FreeSamples(ptr);
                    var info = MediaFormatUtility.DetectFromPath(path);
                    return new AudioClip
                    {
                        name = Path.GetFileNameWithoutExtension(path),
                        samples = sc / Math.Max(1, ch),
                        channels = Math.Max(1, ch),
                        frequency = Math.Max(1, freq),
                        _sampleData = samples,
                        _isLoaded = !stream,
                        _loadType = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
                        _originalPath = path,
                        _containerFormat = info.container,
                        _codec = info.primaryCodec
                    };
                }
            }
            catch
            {
                Anity.Core.Runtime.Native.AnityNative.MarkUnavailable();
            }
        }

        byte[] data;
        try { data = File.ReadAllBytes(path); }
        catch { return null; }

        return CreateFromBytes(data, Path.GetFileNameWithoutExtension(path), path, stream);
    }

    public static AudioClip? CreateFromBytes(byte[] data, string clipName, string? sourcePath = null, bool stream = false)
    {
        if (data == null || data.Length == 0) return null;

        var info = MediaFormatUtility.DetectFromBytes(data, sourcePath);
        if (!info.isAudio && info.container == MediaContainerFormat.Unknown)
        {
            // still try path extension
            info = MediaFormatUtility.DetectFromPath(sourcePath ?? clipName);
            if (!info.isAudio) return null;
        }

        if (!MediaFormatUtility.TryDecodeAudioPcm(data, out var samples, out int channels, out int frequency, out _))
        {
            // fallback empty clip
            channels = 2;
            frequency = 44100;
            samples = new float[frequency * channels]; // 1s silence
        }

        var clip = new AudioClip
        {
            name = clipName ?? "AudioClip",
            samples = samples.Length / Math.Max(1, channels),
            channels = Math.Max(1, channels),
            frequency = Math.Max(1, frequency),
            _sampleData = samples,
            _isLoaded = !stream,
            _loadType = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
            _originalPath = sourcePath ?? string.Empty,
            _containerFormat = info.container,
            _codec = info.primaryCodec
        };
        return clip;
    }

    /// <summary>Unity-style load via Resources / custom path resolution for .mp3/.wav etc.</summary>
    public static AudioClip? Load(string path) => CreateFromFile(path);
}

public enum AudioClipLoadType
{
    DecompressOnLoad,
    CompressedInMemory,
    Streaming
}
