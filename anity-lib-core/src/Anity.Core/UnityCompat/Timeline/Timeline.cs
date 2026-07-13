using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace UnityEngine.Timeline;

public enum ClipCaps
{
    None = 0,
    Looping = 1,
    Extrapolation = 2,
    ClipIn = 4,
    SpeedMultiplier = 8,
    Blending = 16,
    AutoScale = 32,
    All = ~0
}

[Serializable]
public class TimelineClip
{
    public string displayName = "Clip";
    public double start;
    public double duration = 1;
    public double clipIn;
    public double timeScale = 1;
    public UnityEngine.Object? asset;
    public ClipCaps clipCaps = ClipCaps.Blending;

    public double end => start + duration;
    public bool ContainsTime(double t) => t >= start && t < end;
}

public abstract class TrackAsset : ScriptableObject
{
    private readonly List<TimelineClip> _clips = new();
    public string name { get; set; } = "Track";
    public bool muted { get; set; }
    public IEnumerable<TimelineClip> GetClips() => _clips;
    public int clipCount => _clips.Count;

    public TimelineClip CreateClip<T>() where T : UnityEngine.Object, new()
    {
        var clip = new TimelineClip { asset = new T(), displayName = typeof(T).Name };
        _clips.Add(clip);
        return clip;
    }

    public TimelineClip CreateDefaultClip()
    {
        var clip = new TimelineClip();
        _clips.Add(clip);
        return clip;
    }

    public void DeleteClip(TimelineClip clip)
    {
        if (clip != null) _clips.Remove(clip);
    }
}

public class ActivationTrack : TrackAsset { }
public class AnimationTrack : TrackAsset { }
public class AudioTrack : TrackAsset { }
public class ControlTrack : TrackAsset { }
public class SignalTrack : TrackAsset { }
public class PlayableTrack : TrackAsset { }

/// <summary>UnityEngine.Timeline.TimelineAsset</summary>
public class TimelineAsset : PlayableAsset
{
    private readonly List<TrackAsset> _tracks = new();
    private double _duration = 5.0;

    public override double duration
    {
        get
        {
            double maxEnd = _duration;
            foreach (var track in _tracks)
            {
                if (track == null || track.muted) continue;
                foreach (var clip in track.GetClips())
                {
                    if (clip != null && clip.end > maxEnd)
                        maxEnd = clip.end;
                }
            }
            return maxEnd;
        }
    }

    public void SetDuration(double value) => _duration = Math.Max(0, value);

    public int outputTrackCount => _tracks.Count;
    public IEnumerable<TrackAsset> GetOutputTracks() => _tracks;

    public T CreateTrack<T>(string trackName = null) where T : TrackAsset, new()
    {
        var t = new T { name = trackName ?? typeof(T).Name };
        _tracks.Add(t);
        return t;
    }

    public void DeleteTrack(TrackAsset track)
    {
        if (track != null) _tracks.Remove(track);
    }

    /// <summary>Active clips at time t across non-muted tracks.</summary>
    public IEnumerable<TimelineClip> GetClipsAt(double t)
    {
        foreach (var track in _tracks)
        {
            if (track == null || track.muted) continue;
            foreach (var clip in track.GetClips())
            {
                if (clip != null && clip.ContainsTime(t))
                    yield return clip;
            }
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        _ = owner;
        var handle = graph.CreateHandle("Timeline");
        return new Playable(handle);
    }
}