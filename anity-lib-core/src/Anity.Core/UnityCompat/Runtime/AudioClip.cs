using System;
using UnityEngine.Audio;

namespace UnityEngine;

public delegate void PCMReaderCallback(float[] data);
public delegate void PCMSetPositionCallback(int position);

public class AudioClip : Object
{
    private float[] _sampleData = Array.Empty<float>();
    private bool _isLoaded;
    private AudioClipLoadType _loadType = AudioClipLoadType.DecompressOnLoad;

    public float length => samples > 0 && frequency > 0 ? (float)samples / frequency / channels : 0f;
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
}

public enum AudioClipLoadType
{
    DecompressOnLoad,
    CompressedInMemory,
    Streaming
}
