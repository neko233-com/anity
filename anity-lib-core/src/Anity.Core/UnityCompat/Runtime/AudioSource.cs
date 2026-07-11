using System;
using System.Collections.Generic;

namespace UnityEngine;

[AddComponentMenu("Audio/Audio Source")]
public class AudioSource : Behaviour
{
    private AudioClip? _clip;
    private float _volume = 1.0f;
    private float _pitch = 1.0f;
    private float _panStereo;
    private float _spatialBlend;
    private float _reverbZoneMix = 1.0f;
    private float _dopplerLevel = 1.0f;
    private float _spread;
    private int _priority = 128;
    private float _minDistance = 1.0f;
    private float _maxDistance = 500.0f;
    private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;
    private bool _loop;
    private bool _playOnAwake = true;
    private bool _mute;
    private bool _bypassEffects;
    private bool _bypassListenerEffects;
    private bool _bypassReverbZones;
    private bool _ignoreListenerPause;
    private bool _ignoreListenerVolume;
    private bool _isPlaying;
    private bool _isVirtual;
    private float _time;
    private int _timeSamples;
    private readonly List<AudioClip> _oneShotClips = new();

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

    public float panStereo
    {
        get => _panStereo;
        set => _panStereo = Mathf.Clamp(value, -1f, 1f);
    }

    public float spatialBlend
    {
        get => _spatialBlend;
        set => _spatialBlend = Mathf.Clamp01(value);
    }

    public float panLevel
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

    public bool mute
    {
        get => _mute;
        set => _mute = value;
    }

    public bool bypassEffects
    {
        get => _bypassEffects;
        set => _bypassEffects = value;
    }

    public bool bypassListenerEffects
    {
        get => _bypassListenerEffects;
        set => _bypassListenerEffects = value;
    }

    public bool bypassReverbZones
    {
        get => _bypassReverbZones;
        set => _bypassReverbZones = value;
    }

    public bool ignoreListenerPause
    {
        get => _ignoreListenerPause;
        set => _ignoreListenerPause = value;
    }

    public bool ignoreListenerVolume
    {
        get => _ignoreListenerVolume;
        set => _ignoreListenerVolume = value;
    }

    public bool isPlaying => _isPlaying;
    public bool isVirtual => _isVirtual;

    public float time
    {
        get => _time;
        set => _time = Mathf.Max(0f, value);
    }

    public int timeSamples
    {
        get => _timeSamples;
        set => _timeSamples = Math.Max(0, value);
    }

    public AudioVelocityUpdateMode velocityUpdateMode { get; set; } = AudioVelocityUpdateMode.Auto;

    public void Play()
    {
        if (_clip != null)
        {
            _isPlaying = true;
            _time = 0f;
            _oneShotClips.Clear();
        }
    }

    public void Play(ulong delay)
    {
        _ = delay;
        Play();
    }

    public void PlayDelayed(float delay)
    {
        _ = delay;
        Play();
    }

    public void PlayOneShot(AudioClip clip)
    {
        PlayOneShot(clip, 1f);
    }

    public void PlayOneShot(AudioClip clip, float volumeScale)
    {
        _ = volumeScale;
        if (clip != null)
        {
            _oneShotClips.Add(clip);
            _isPlaying = true;
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _time = 0f;
        _oneShotClips.Clear();
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void UnPause()
    {
        if (_clip != null || _oneShotClips.Count > 0)
        {
            _isPlaying = true;
        }
    }

    public void SetScheduledStartTime(double time) { _ = time; }
    public void SetScheduledEndTime(double time) { _ = time; }

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

    public static void PlayClipAtPoint(AudioClip clip, Vector3 position)
    {
        PlayClipAtPoint(clip, position, 1f);
    }

    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volumeScale)
    {
        _ = position;
        _ = volumeScale;
    }
}

public enum AudioRolloffMode
{
    Logarithmic,
    Linear,
    Custom
}

public enum FFTWindow
{
    Rectangular,
    Triangle,
    Hamming,
    Hanning,
    Blackman,
    BlackmanHarris
}
