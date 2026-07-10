using System.Collections.Generic;

namespace UnityEngine.Audio;

/// <summary>
/// Audio mixer asset.
/// </summary>
public class AudioMixer : Object
{
    private readonly Dictionary<string, float> _floats = new();
    private AudioMixerSnapshot[]? _transitionSnapshots;
    private float[]? _transitionWeights;
    private float _transitionTargetTime;
    private float _transitionElapsed;

    public string outputAudioMixerGroupName { get; set; } = string.Empty;
    public AudioMixerGroup? outputAudioMixerGroup { get; set; }
    public AudioMixerUpdateMode updateMode { get; set; } = AudioMixerUpdateMode.Normal;

    public float GetFloat(string name)
    {
        return _floats.TryGetValue(name, out var value) ? value : 0f;
    }

    public bool SetFloat(string name, float value)
    {
        _floats[name] = value;
        return true;
    }

    public bool GetFloat(string name, out float value)
    {
        value = GetFloat(name);
        return true;
    }

    public void TransitionToSnapshots(AudioMixerSnapshot[] snapshots, float[] weights, float timeToReach)
    {
        _transitionSnapshots = snapshots;
        _transitionWeights = weights;
        _transitionTargetTime = timeToReach;
        _transitionElapsed = 0f;
    }

    public AudioMixerSnapshot? FindSnapshot(string name)
    {
        if (_transitionSnapshots is null || name is null) return null;
        foreach (var s in _transitionSnapshots)
        {
            if (s is not null && s.name == name) return s;
        }

        return null;
    }

    public void ClearFloat(string name) { _floats.Remove(name); }
    public void Resume() { }
    public void Suspend() { }

    public void Update(float deltaTime)
    {
        if (_transitionTargetTime <= 0f || _transitionSnapshots is null) return;
        _transitionElapsed += deltaTime;
        var t = Math.Clamp(_transitionElapsed / _transitionTargetTime, 0f, 1f);
        if (t >= 1f)
        {
            _transitionTargetTime = 0f;
        }
    }

    public float GetCurrentSnapshotWeight(int index)
    {
        if (_transitionWeights is null || index < 0 || index >= _transitionWeights.Length) return 0f;
        var t = _transitionTargetTime > 0f ? Math.Clamp(_transitionElapsed / _transitionTargetTime, 0f, 1f) : 1f;
        return _transitionWeights[index] * t;
    }
}

public class AudioMixerGroup : Object
{
    public AudioMixer? audioMixer { get; protected set; }
}

public class AudioMixerSnapshot : Object
{
    public AudioMixer? audioMixer { get; protected set; }
    public void TransitionTo(float timeToReach) { _ = timeToReach; }
}

public enum AudioMixerUpdateMode
{
    Normal,
    UnscaledTime
}
