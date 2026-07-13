using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace UnityEngine.Timeline;

/// <summary>Named signal asset (UnityEngine.Timeline.SignalAsset).</summary>
public class SignalAsset : ScriptableObject
{
    [SerializeField] private string _signalName = "Signal";

    public string signalName
    {
        get => string.IsNullOrEmpty(_signalName) ? name : _signalName;
        set => _signalName = value ?? string.Empty;
    }
}

/// <summary>Marker that emits a SignalAsset at a time on a SignalTrack.</summary>
[Serializable]
public class SignalEmitter
{
    public double time;
    public SignalAsset asset;
    public bool retroactive;
    public bool emitOnce = true;

    [NonSerialized] internal bool emitted;
}

/// <summary>Reaction entry on SignalReceiver.</summary>
[Serializable]
public class SignalReaction
{
    public SignalAsset signal;
    public UnityEngine.Events.UnityEvent reaction;

    public SignalReaction()
    {
        reaction = new UnityEngine.Events.UnityEvent();
    }
}

/// <summary>Receives timeline signals and invokes bound reactions.</summary>
[AddComponentMenu("Timeline/Signal Receiver")]
public class SignalReceiver : MonoBehaviour
{
    private readonly List<SignalReaction> _reactions = new();
    private readonly List<Action<SignalAsset>> _listeners = new();
    private int _receiveCount;

    public int reactionCount => _reactions.Count;
    public int receiveCount => _receiveCount;

    public event Action<SignalAsset> signalReceived
    {
        add => _listeners.Add(value);
        remove => _listeners.Remove(value);
    }

    public void AddReaction(SignalAsset signal, Action callback)
    {
        if (signal == null) return;
        var r = new SignalReaction { signal = signal };
        if (callback != null)
            r.reaction.AddListener(() => callback());
        _reactions.Add(r);
    }

    public void AddReaction(SignalReaction reaction)
    {
        if (reaction != null) _reactions.Add(reaction);
    }

    public void RemoveReaction(SignalAsset signal)
    {
        _reactions.RemoveAll(r => r != null && r.signal == signal);
    }

    public void OnNotify(SignalAsset signal)
    {
        if (signal == null) return;
        _receiveCount++;
        for (int i = 0; i < _reactions.Count; i++)
        {
            var r = _reactions[i];
            if (r?.signal == signal)
                r.reaction?.Invoke();
        }
        for (int i = 0; i < _listeners.Count; i++)
            _listeners[i]?.Invoke(signal);
    }

    public void Clear()
    {
        _reactions.Clear();
        _listeners.Clear();
        _receiveCount = 0;
    }
}

/// <summary>Signal track holding time-based emitters.</summary>
public class SignalTrack : TrackAsset
{
    private readonly List<SignalEmitter> _emitters = new();

    public int emitterCount => _emitters.Count;
    public IEnumerable<SignalEmitter> GetMarkers() => _emitters;

    public SignalEmitter CreateMarker(double time, SignalAsset asset)
    {
        var e = new SignalEmitter
        {
            time = Math.Max(0, time),
            asset = asset,
            emitOnce = true
        };
        _emitters.Add(e);
        return e;
    }

    public void DeleteMarker(SignalEmitter emitter)
    {
        if (emitter != null) _emitters.Remove(emitter);
    }

    public void ResetEmittedFlags()
    {
        foreach (var e in _emitters)
            if (e != null) e.emitted = false;
    }

    /// <summary>Emit signals in (fromExclusive, toInclusive] range.</summary>
    public int EmitInRange(double fromExclusive, double toInclusive, SignalReceiver receiver, Action<SignalAsset> alsoNotify = null)
    {
        if (muted) return 0;
        int n = 0;
        foreach (var e in _emitters)
        {
            if (e?.asset == null) continue;
            if (e.emitted && e.emitOnce) continue;
            bool hit = e.time > fromExclusive && e.time <= toInclusive;
            if (!hit && e.retroactive && toInclusive >= e.time && fromExclusive < 0)
                hit = true;
            if (!hit) continue;
            e.emitted = true;
            receiver?.OnNotify(e.asset);
            alsoNotify?.Invoke(e.asset);
            n++;
        }
        return n;
    }
}

/// <summary>Static hub for director-driven signal evaluation.</summary>
public static class SignalUtility
{
    public static int EvaluateTimelineSignals(
        TimelineAsset timeline,
        double previousTime,
        double currentTime,
        SignalReceiver receiver,
        bool looping = false)
    {
        if (timeline == null) return 0;
        int total = 0;
        double dur = timeline.duration;
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is not SignalTrack st) continue;
            if (looping && currentTime < previousTime)
            {
                // Loop wrap: emit remaining markers to duration, reset, then [0, current]
                total += st.EmitInRange(previousTime, dur > 0 ? dur : double.MaxValue, receiver);
                st.ResetEmittedFlags();
                total += st.EmitInRange(-1e-9, currentTime, receiver);
            }
            else
            {
                total += st.EmitInRange(previousTime, currentTime, receiver);
            }
        }
        return total;
    }
}
