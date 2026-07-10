namespace UnityEngine.Audio;

/// <summary>
/// Audio mixer asset.
/// </summary>
public class AudioMixer : Object
{
    public string outputAudioMixerGroupName { get; set; } = string.Empty;
    public AudioMixerGroup? outputAudioMixerGroup { get; set; }
    public AudioMixerUpdateMode updateMode { get; set; } = AudioMixerUpdateMode.Normal;

    public float GetFloat(string name)
    {
        _ = name;
        return 0f;
    }

    public bool SetFloat(string name, float value)
    {
        _ = name;
        _ = value;
        return true;
    }

    public bool GetFloat(string name, out float value)
    {
        _ = name;
        value = 0f;
        return true;
    }

    public void TransitionToSnapshots(AudioMixerSnapshot[] snapshots, float[] weights, float timeToReach)
    {
        _ = snapshots;
        _ = weights;
        _ = timeToReach;
    }

    public AudioMixerSnapshot? FindSnapshot(string name)
    {
        _ = name;
        return null;
    }

    public void ClearFloat(string name) { _ = name; }
    public void Resume() { }
    public void Suspend() { }
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
