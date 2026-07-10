using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine;

[AddComponentMenu("Animation/Animation")]
public class Animation : Behaviour, IEnumerable
{
    private AnimationClip? _clip;
    private bool _playAutomatically = true;
    private WrapMode _wrapMode = WrapMode.Default;
    private readonly Dictionary<string, AnimationState> _states = new();

    public AnimationClip? clip
    {
        get => _clip;
        set => _clip = value;
    }

    public bool playAutomatically
    {
        get => _playAutomatically;
        set => _playAutomatically = value;
    }

    public WrapMode wrapMode
    {
        get => _wrapMode;
        set => _wrapMode = value;
    }

    public AnimationState? this[string name] => GetState(name);

    public bool isPlaying { get; private set; }

    public void Stop() => isPlaying = false;
    public void Stop(string name) { _ = name; isPlaying = false; }

    public void Rewind() { }
    public void Rewind(string name) { _ = name; }

    public void Play() => isPlaying = true;
    public bool Play(PlayMode mode) { _ = mode; return PlayInternal(); }
    public bool Play(string name, PlayMode mode = PlayMode.StopSameLayer) { _ = name; _ = mode; return PlayInternal(); }

    private bool PlayInternal()
    {
        isPlaying = true;
        return true;
    }

    public void CrossFade(string name, float fadeLength, PlayMode mode = PlayMode.StopSameLayer) { _ = name; _ = fadeLength; _ = mode; }
    public void Blend(string name, float targetWeight = 1f, float fadeLength = 0.3f) { _ = name; _ = targetWeight; _ = fadeLength; }

    public AnimationState? GetState(string name) => _states.TryGetValue(name, out var state) ? state : null;
    public bool IsPlaying(string name) { _ = name; return isPlaying; }

    public void AddClip(AnimationClip clip, string newName)
    {
        if (clip is null || string.IsNullOrEmpty(newName)) return;
        _states[newName] = new AnimationState(clip, newName);
    }

    public void RemoveClip(AnimationClip clip)
    {
        if (clip is null) return;
        foreach (var pair in _states)
        {
            if (pair.Value.clip == clip)
            {
                _ = _states.Remove(pair.Key);
                return;
            }
        }
    }

    public void RemoveClip(string name) => _ = _states.Remove(name);

    public IEnumerator GetEnumerator() => _states.Values.GetEnumerator();

    public int GetClipCount() => _states.Count;

    public void Sample() { }

    public AnimationClip[] GetClips()
    {
        var clips = new List<AnimationClip>();
        foreach (var state in _states.Values)
        {
            if (state.clip is not null)
            {
                clips.Add(state.clip);
            }
        }

        return clips.ToArray();
    }
}

public class AnimationState
{
    public AnimationClip? clip { get; set; }
    public string name { get; set; } = string.Empty;
    public float time { get; set; }
    public float normalizedTime { get; set; }
    public float speed { get; set; } = 1f;
    public float length => clip?.length ?? 0f;
    public WrapMode wrapMode { get; set; } = WrapMode.Default;
    public AnimationBlendMode blendMode { get; set; } = AnimationBlendMode.Blend;
    public float weight { get; set; } = 1f;
    public bool enabled { get; set; }

    public AnimationState(AnimationClip clip, string name)
    {
        this.clip = clip;
        this.name = name;
    }
}

public enum PlayMode
{
    StopSameLayer = 0,
    StopAll = 4
}

public enum WrapMode
{
    Once = 1,
    Loop = 2,
    PingPong = 4,
    Default = 0,
    ClampForever = 8,
    Clamp = 1
}

public enum AnimationBlendMode
{
    Blend = 0,
    Additive = 1
}
