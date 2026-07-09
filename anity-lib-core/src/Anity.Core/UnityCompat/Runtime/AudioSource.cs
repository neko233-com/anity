using System;

namespace UnityEngine;

/// <summary>
/// Unity AudioSource component for playing audio.
/// </summary>
[AddComponentMenu("Audio/Audio Source")]
public class AudioSource : Behaviour
{
    private AudioClip? _clip;
    private float _volume = 1.0f;
    private float _pitch = 1.0f;
    private bool _loop;
    private bool _playOnAwake = true;
    private float _spatialBlend;
    private float _reverbZoneMix = 1.0f;
    private float _dopplerLevel = 1.0f;
    private float _spread;
    private int _priority = 128;
    private float _minDistance = 1.0f;
    private float _maxDistance = 500.0f;
    private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;

    public AudioClip? clip
    {
        get => _clip;
        set => _clip = value;
    }

    public float volume
    {
        get => _volume;
        set => _volume = Mathf.Clamp01(value);
    }

    public float pitch
    {
        get => _pitch;
        set => _pitch = value;
    }

    public bool loop
    {
        get => _loop;
        set => _loop = value;
    }

    public bool playOnAwake
    {
        get => _playOnAwake;
        set => _playOnAwake = value;
    }

    public bool isPlaying { get; private set; }

    public float time { get; set; }

    public float spatialBlend
    {
        get => _spatialBlend;
        set => _spatialBlend = Mathf.Clamp01(value);
    }

    public float reverbZoneMix
    {
        get => _reverbZoneMix;
        set => _reverbZoneMix = value;
    }

    public float dopplerLevel
    {
        get => _dopplerLevel;
        set => _dopplerLevel = value;
    }

    public float spread
    {
        get => _spread;
        set => _spread = Mathf.Clamp(value, 0f, 360f);
    }

    public int priority
    {
        get => _priority;
        set => _priority = (int)Mathf.Clamp(value, 0, 256);
    }

    public float minDistance
    {
        get => _minDistance;
        set => _minDistance = Mathf.Max(0f, value);
    }

    public float maxDistance
    {
        get => _maxDistance;
        set => _maxDistance = Mathf.Max(0f, value);
    }

    public AudioRolloffMode rolloffMode
    {
        get => _rolloffMode;
        set => _rolloffMode = value;
    }

    public void Play()
    {
        if (_clip != null)
        {
            isPlaying = true;
        }
    }

    public void Play(ulong delay)
    {
        Play();
    }

    public void PlayDelayed(float delay)
    {
        Play();
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
        {
            isPlaying = true;
        }
    }

    public void PlayOneShot(AudioClip clip, float volumeScale)
    {
        PlayOneShot(clip);
    }

    public void Stop()
    {
        isPlaying = false;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void UnPause()
    {
        isPlaying = true;
    }

    public void SetScheduledStartTime(double time) { }
    public void SetScheduledEndTime(double time) { }

    public bool GetOutputData(float[] samples, int channel)
    {
        if (samples == null) return false;
        Array.Clear(samples, 0, samples.Length);
        return true;
    }

    public bool GetSpectrumData(float[] samples, int channel, FFTWindow window)
    {
        if (samples == null) return false;
        Array.Clear(samples, 0, samples.Length);
        return true;
    }
}

/// <summary>
/// Audio rolloff mode for 3D audio.
/// </summary>
public enum AudioRolloffMode
{
    Logarithmic,
    Linear,
    Custom
}

/// <summary>
/// FFT window for spectrum analysis.
/// </summary>
public enum FFTWindow
{
    Rectangular,
    Triangle,
    Hamming,
    Hanning,
    Blackman,
    BlackmanHarris
}
