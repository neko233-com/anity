using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine;

[AddComponentMenu("Animation/Animation")]
public class Animation : Behaviour, IEnumerable
{
    private AnimationClip _clip;
    private bool _playAutomatically = true;
    private WrapMode _wrapMode = WrapMode.Default;
    private readonly Dictionary<string, AnimationState> _states = new();
    private AnimationState _playingState;
    private float _time;
    private bool _isPlaying;
    private bool _animatePhysics;
    private AnimationCullingType _cullingType;

    public AnimationClip clip
    {
        get => _clip;
        set
        {
            _clip = value;
            if (value != null && !_states.ContainsKey(value.name))
            {
                AddClip(value, value.name);
            }
        }
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

    public bool animatePhysics
    {
        get => _animatePhysics;
        set => _animatePhysics = value;
    }

    public AnimationCullingType cullingType
    {
        get => _cullingType;
        set => _cullingType = value;
    }

    public bool isPlaying => _isPlaying;

    public AnimationState this[string name] => GetState(name);

    public void Stop()
    {
        _isPlaying = false;
        _playingState = null;
    }

    public void Stop(string name)
    {
        if (_playingState != null && _playingState.name == name)
        {
            Stop();
        }
    }

    public void Rewind()
    {
        _time = 0f;
        if (_playingState != null)
        {
            _playingState.time = 0f;
            _playingState.normalizedTime = 0f;
        }
    }

    public void Rewind(string name)
    {
        var state = GetState(name);
        if (state != null)
        {
            state.time = 0f;
            state.normalizedTime = 0f;
        }
    }

    public void Play()
    {
        if (_clip != null)
        {
            Play(_clip.name);
        }
    }

    public bool Play(PlayMode mode)
    {
        _ = mode;
        return PlayInternal();
    }

    public bool Play(string name, PlayMode mode = PlayMode.StopSameLayer)
    {
        _ = mode;
        var state = GetState(name);
        if (state == null) return false;
        _playingState = state;
        state.enabled = true;
        _time = state.time;
        return PlayInternal();
    }

    private bool PlayInternal()
    {
        _isPlaying = true;
        return true;
    }

    public void CrossFade(string name, float fadeLength, PlayMode mode = PlayMode.StopSameLayer)
    {
        _ = fadeLength;
        _ = mode;
        Play(name, mode);
    }

    public void CrossFade(string name, float fadeLength)
    {
        CrossFade(name, fadeLength, PlayMode.StopSameLayer);
    }

    public void CrossFadeQueued(string name, float fadeLength, QueueMode queueMode = QueueMode.CompleteOthers, PlayMode mode = PlayMode.StopSameLayer)
    {
        _ = queueMode;
        CrossFade(name, fadeLength, mode);
    }

    public void Blend(string name, float targetWeight = 1f, float fadeLength = 0.3f)
    {
        _ = targetWeight;
        _ = fadeLength;
        var state = GetState(name);
        if (state != null)
        {
            state.weight = targetWeight;
        }
    }

    public AnimationState GetState(string name)
    {
        _states.TryGetValue(name, out var state);
        return state;
    }

    public bool IsPlaying(string name)
    {
        return _isPlaying && _playingState != null && _playingState.name == name;
    }

    public void AddClip(AnimationClip clip, string newName)
    {
        if (clip == null || string.IsNullOrEmpty(newName)) return;
        if (_states.ContainsKey(newName))
        {
            _states[newName].clip = clip;
        }
        else
        {
            _states[newName] = new AnimationState(clip, newName);
        }
    }

    public void AddClip(AnimationClip clip, string newName, int firstFrame, int lastFrame)
    {
        _ = firstFrame;
        _ = lastFrame;
        AddClip(clip, newName);
    }

    public void RemoveClip(AnimationClip clip)
    {
        if (clip == null) return;
        string toRemove = null;
        foreach (var pair in _states)
        {
            if (pair.Value.clip == clip)
            {
                toRemove = pair.Key;
                break;
            }
        }
        if (toRemove != null) _states.Remove(toRemove);
    }

    public void RemoveClip(string name)
    {
        _states.Remove(name);
    }

    public IEnumerator GetEnumerator() => _states.Values.GetEnumerator();

    public int GetClipCount() => _states.Count;

    public void Sample()
    {
        if (gameObject == null) return;
        if (_playingState != null && _playingState.clip != null)
        {
            _playingState.clip.SampleAnimation(gameObject, _playingState.time);
        }
        else if (_clip != null)
        {
            _clip.SampleAnimation(gameObject, _time);
        }
    }

    public void Update(float deltaTime)
    {
        if (!_isPlaying || gameObject == null) return;

        var state = _playingState;
        if (state == null || state.clip == null) return;

        float speed = state.speed;
        float length = Math.Max(0.001f, state.length);
        WrapMode wrap = state.wrapMode != WrapMode.Default ? state.wrapMode : _wrapMode;

        state.time += deltaTime * speed;
        state.normalizedTime = state.time / length;
        _time = state.time;

        float wrappedTime = WrapTime(state.time, length, wrap);
        state.clip.SampleAnimation(gameObject, wrappedTime);

        if (wrap == WrapMode.Once && state.time >= length)
        {
            Stop();
        }
    }

    private float WrapTime(float time, float duration, WrapMode mode)
    {
        if (duration <= 0f) return time;

        switch (mode)
        {
            case WrapMode.Loop:
                var t1 = time % duration;
                if (t1 < 0f) t1 += duration;
                return t1;
            case WrapMode.PingPong:
                var cycle = time / duration;
                var floor = MathF.Floor(cycle);
                var t2 = cycle - floor;
                if ((int)floor % 2 == 1) t2 = 1f - t2;
                return t2 * duration;
            case WrapMode.ClampForever:
                return Math.Clamp(time, 0f, duration);
            case WrapMode.Default:
            case WrapMode.Once:
            default:
                return Math.Clamp(time, 0f, duration);
        }
    }

    public AnimationClip[] GetClips()
    {
        var clips = new List<AnimationClip>();
        foreach (var state in _states.Values)
        {
            if (state.clip != null) clips.Add(state.clip);
        }
        return clips.ToArray();
    }

    public void StopLayer(int layer)
    {
        bool hasLayer = false;
        foreach (var state in _states.Values)
        {
            if (state.layer == layer)
            {
                state.enabled = false;
                state.time = 0f;
                state.normalizedTime = 0f;
                hasLayer = true;
                if (_playingState == state)
                {
                    _playingState = null;
                }
            }
        }
        if (layer == 0 || !hasLayer)
        {
            if (_playingState != null && _playingState.layer == layer)
            {
                _playingState = null;
                _isPlaying = false;
            }
        }
    }

    public void RewindLayer(int layer)
    {
        foreach (var state in _states.Values)
        {
            if (state.layer == layer)
            {
                state.time = 0f;
                state.normalizedTime = 0f;
            }
        }
        if ((_playingState == null || _playingState.layer == layer) && layer == 0)
        {
            _time = 0f;
        }
    }
    public bool IsPlaying(int layer) { _ = layer; return isPlaying; }
}

public class AnimationState
{
    private AnimationClip _clip;
    private string _name;

    public AnimationClip clip
    {
        get => _clip;
        set => _clip = value;
    }

    public string name
    {
        get => _name;
        set => _name = value;
    }

    public float time { get; set; }
    public float normalizedTime { get; set; }
    public float speed { get; set; } = 1f;
    public float length => _clip != null ? _clip.length : 0f;
    public WrapMode wrapMode { get; set; } = WrapMode.Default;
    public AnimationBlendMode blendMode { get; set; } = AnimationBlendMode.Blend;
    public float weight { get; set; } = 1f;
    public bool enabled { get; set; }
    public int layer { get; set; }

    public AnimationState(AnimationClip clip, string name)
    {
        _clip = clip;
        _name = name;
        if (clip != null) wrapMode = clip.wrapMode;
    }

    public void AddMixingTransform(Transform mix)
    {
        _ = mix;
    }

    public void AddMixingTransform(Transform mix, bool recursive)
    {
        _ = mix;
        _ = recursive;
    }

    public void RemoveMixingTransform(Transform mix)
    {
        _ = mix;
    }
}

public enum PlayMode
{
    StopSameLayer = 0,
    StopAll = 4
}

public enum QueueMode
{
    PlayNow = 0,
    CompleteOthers = 2
}

public enum WrapMode
{
    Once = 1,
    Clamp = 1,
    Loop = 2,
    PingPong = 4,
    Default = 0,
    ClampForever = 8
}

public enum AnimationBlendMode
{
    Blend = 0,
    Additive = 1
}

public enum AnimationCullingType
{
    AlwaysAnimate = 0,
    BasedOnRenderers = 1,
    BasedOnClipBounds = 2,
    BasedOnUserBounds = 3
}
