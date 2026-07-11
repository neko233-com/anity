using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.Audio;

public class AudioMixer : Object
{
    private readonly AudioMixerController _controller;
    private bool _trueSoundRestored;

    public AudioMixer()
    {
        _controller = new AudioMixerController(this);
    }

    public AudioMixer? outputAudioMixer { get; set; }

    public AudioMixerGroup[] FindMatchingGroups(string subPath)
    {
        return _controller.FindMatchingGroups(subPath);
    }

    public AudioMixerSnapshot? FindSnapshot(string name)
    {
        return _controller.FindSnapshot(name);
    }

    public bool SetFloat(string name, float value)
    {
        return _controller.SetFloat(name, value);
    }

    public bool GetFloat(string name, out float value)
    {
        return _controller.GetFloat(name, out value);
    }

    public void ClearFloat(string name)
    {
        _controller.ClearFloat(name);
    }

    public void RestoreTrueSound()
    {
        _trueSoundRestored = true;
    }

    public void TransitionToSnapshots(AudioMixerSnapshot[] snapshots, float[] weights, float timeToReach)
    {
        _controller.TransitionToSnapshots(snapshots, weights, timeToReach);
    }

    public void Update(float deltaTime)
    {
        _controller.Update(deltaTime);
    }

    internal AudioMixerController Controller => _controller;
}

public class AudioMixerGroup : Object
{
    private readonly List<AudioMixerGroupView> _views = new();

    public AudioMixerGroup()
    {
    }

    internal AudioMixerGroup(AudioMixer mixer, string groupName)
    {
        audioMixer = mixer;
        name = groupName;
    }

    public AudioMixer? audioMixer { get; internal set; }

    public AudioMixerGroupView[] audioMixerGroupViews => _views.ToArray();

    internal void AddView(AudioMixerGroupView view)
    {
        if (view != null) _views.Add(view);
    }
}

public class AudioMixerGroupView
{
    public string name { get; set; } = string.Empty;
    public AudioMixerGroup? group { get; set; }
}

public class AudioMixerSnapshot : Object
{
    private readonly Dictionary<string, float> _parameters = new();
    private AudioMixerSnapshotsPair? _snapshotsPair;

    public AudioMixerSnapshot()
    {
    }

    internal AudioMixerSnapshot(AudioMixer mixer, string snapshotName)
    {
        audioMixer = mixer;
        name = snapshotName;
    }

    public AudioMixer? audioMixer { get; internal set; }

    internal AudioMixerSnapshotsPair? audioMixerSnapshotsPair
    {
        get => _snapshotsPair;
        set => _snapshotsPair = value;
    }

    public void TransitionTo(float timeToReach)
    {
        if (audioMixer != null)
        {
            audioMixer.TransitionToSnapshots(new[] { this }, new[] { 1f }, timeToReach);
        }
    }

    internal bool SetParameterValue(string name, float value)
    {
        _parameters[name] = value;
        return true;
    }

    internal float GetParameterValue(string name, float defaultValue = 0f)
    {
        return _parameters.TryGetValue(name, out var value) ? value : defaultValue;
    }
}

internal struct SnapshotWeight
{
    public AudioMixerSnapshot snapshot;
    public float targetWeight;
    public float currentWeight;

    public SnapshotWeight(AudioMixerSnapshot snapshot, float weight)
    {
        this.snapshot = snapshot;
        targetWeight = weight;
        currentWeight = 0f;
    }
}

internal class AudioMixerSnapshotsPair
{
    public AudioMixerSnapshot[] snapshots;
    public float[] weights;

    public AudioMixerSnapshotsPair(AudioMixerSnapshot[] snapshots, float[] weights)
    {
        this.snapshots = snapshots;
        this.weights = weights;
    }
}

internal class AudioMixerController
{
    private readonly AudioMixer _mixer;
    internal readonly Dictionary<string, float> m_Parameters = new();
    internal readonly List<AudioMixerSnapshot> m_Snapshots = new();
    internal readonly List<AudioMixerGroup> m_Groups = new();
    internal SnapshotWeight[] m_TargetSnapshotWeights = Array.Empty<SnapshotWeight>();
    private float _transitionTime;
    private float _transitionElapsed;

    public AudioMixerController(AudioMixer mixer)
    {
        _mixer = mixer;
    }

    public AudioMixerGroup[] FindMatchingGroups(string subPath)
    {
        if (string.IsNullOrEmpty(subPath))
            return m_Groups.ToArray();
        return m_Groups.Where(g => g.name.StartsWith(subPath, StringComparison.Ordinal)).ToArray();
    }

    public AudioMixerSnapshot? FindSnapshot(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return m_Snapshots.FirstOrDefault(s => s.name == name);
    }

    public bool SetFloat(string name, float value)
    {
        m_Parameters[name] = value;
        return true;
    }

    public bool GetFloat(string name, out float value)
    {
        return m_Parameters.TryGetValue(name, out value);
    }

    public void ClearFloat(string name)
    {
        m_Parameters.Remove(name);
    }

    public void TransitionToSnapshots(AudioMixerSnapshot[] snapshots, float[] weights, float timeToReach)
    {
        if (snapshots == null || weights == null || snapshots.Length != weights.Length)
            return;

        _transitionTime = Math.Max(0f, timeToReach);
        _transitionElapsed = 0f;

        var targetList = new List<SnapshotWeight>();
        for (int i = 0; i < snapshots.Length; i++)
        {
            if (snapshots[i] != null)
            {
                targetList.Add(new SnapshotWeight(snapshots[i], Math.Clamp(weights[i], 0f, 1f)));
            }
        }
        m_TargetSnapshotWeights = targetList.ToArray();

        if (_transitionTime <= 0f)
        {
            ApplySnapshotWeights(1f);
            m_TargetSnapshotWeights = Array.Empty<SnapshotWeight>();
        }
    }

    public void Update(float deltaTime)
    {
        if (m_TargetSnapshotWeights.Length == 0 || _transitionTime <= 0f)
            return;

        _transitionElapsed += deltaTime;
        float t = Math.Clamp(_transitionElapsed / _transitionTime, 0f, 1f);

        ApplySnapshotWeights(t);

        if (t >= 1f)
        {
            m_TargetSnapshotWeights = Array.Empty<SnapshotWeight>();
        }
    }

    private void ApplySnapshotWeights(float t)
    {
        var parameterValues = new Dictionary<string, (float totalWeight, float totalValue)>();

        for (int i = 0; i < m_TargetSnapshotWeights.Length; i++)
        {
            var sw = m_TargetSnapshotWeights[i];
            float weight = sw.currentWeight + (sw.targetWeight - sw.currentWeight) * t;
            m_TargetSnapshotWeights[i].currentWeight = weight;

            if (sw.snapshot == null) continue;

            foreach (var param in m_Parameters.Keys.ToList())
            {
                float val = sw.snapshot.GetParameterValue(param, m_Parameters.TryGetValue(param, out var existing) ? existing : 0f);
                if (!parameterValues.ContainsKey(param))
                {
                    parameterValues[param] = (0f, 0f);
                }
                parameterValues[param] = (parameterValues[param].totalWeight + weight,
                    parameterValues[param].totalValue + val * weight);
            }
        }

        foreach (var kvp in parameterValues)
        {
            if (kvp.Value.totalWeight > 0f)
            {
                m_Parameters[kvp.Key] = kvp.Value.totalValue / kvp.Value.totalWeight;
            }
        }
    }

    internal AudioMixerSnapshot CreateSnapshot(string name)
    {
        var snapshot = new AudioMixerSnapshot(_mixer, name);
        m_Snapshots.Add(snapshot);
        return snapshot;
    }

    internal AudioMixerGroup CreateGroup(string name)
    {
        var group = new AudioMixerGroup(_mixer, name);
        m_Groups.Add(group);
        return group;
    }
}

public enum AudioMixerUpdateMode
{
    Normal,
    UnscaledTime
}
