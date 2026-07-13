using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline;

/// <summary>UnityEditor.Timeline.TimelineWindow — editor UI for TimelineAsset / PlayableDirector.</summary>
public class TimelineWindow : EditorWindow
{
    private TimelineAsset _asset;
    private PlayableDirector _director;
    private double _playhead;
    private Vector2 _scroll;
    private TrackAsset _selectedTrack;
    private string _status = string.Empty;
    private bool _isPlaying;
    private readonly List<string> _log = new();

    public TimelineAsset timeline => _asset;
    public PlayableDirector director => _director;
    public double playhead => _playhead;
    public bool isPlaying => _isPlaying;
    public TrackAsset selectedTrack => _selectedTrack;
    public IReadOnlyList<string> log => _log;
    public string statusMessage => _status;

    [MenuItem("Window/Sequencing/Timeline")]
    public static TimelineWindow ShowWindow()
    {
        var w = GetWindow<TimelineWindow>();
        w.titleContent = new GUIContent("Timeline");
        w.minSize = new Vector2(400, 240);
        return w;
    }

    public static TimelineWindow Open(TimelineAsset asset, PlayableDirector director = null)
    {
        var w = ShowWindow();
        w.SetTimeline(asset, director);
        return w;
    }

    public void SetTimeline(TimelineAsset asset, PlayableDirector director = null)
    {
        _asset = asset;
        _director = director;
        _playhead = director != null ? director.time : 0;
        _selectedTrack = null;
        _status = asset != null ? $"Timeline '{asset.name}' tracks={asset.outputTrackCount}" : "No timeline";
        Log(_status);
    }

    public void SetPlayhead(double time)
    {
        _playhead = Math.Max(0, time);
        if (_director != null)
            _director.time = _playhead;
    }

    public void Play()
    {
        _isPlaying = true;
        if (_director != null)
        {
            _director.timeUpdateMode = DirectorUpdateMode.Manual;
            _director.Play();
        }
        Log("Play");
    }

    public void Pause()
    {
        _isPlaying = false;
        _director?.Pause();
        Log("Pause");
    }

    public void Stop()
    {
        _isPlaying = false;
        _playhead = 0;
        if (_director != null)
        {
            _director.Stop();
            _director.time = 0;
        }
        Log("Stop");
    }

    /// <summary>Advance preview/playhead by delta (editor scrub / play).</summary>
    public void Tick(float deltaTime)
    {
        if (!_isPlaying) return;
        double dur = _asset != null ? _asset.duration : (_director != null ? _director.duration : 5);
        _playhead += deltaTime;
        if (dur > 0 && _playhead > dur)
        {
            if (_director != null && _director.extrapolationMode == DirectorWrapMode.Loop)
                _playhead %= dur;
            else
            {
                _playhead = dur;
                _isPlaying = false;
            }
        }
        if (_director != null)
        {
            _director.timeUpdateMode = DirectorUpdateMode.Manual;
            if (_director.state != PlayState.Playing)
                _director.Play();
            // Director Evaluate advances from its own time — sync then step
            double prev = _director.time;
            _director.time = prev;
            float step = (float)(_playhead - prev);
            if (step < 0) step = deltaTime;
            _director.Evaluate(Math.Max(0.0001f, step));
            _playhead = _director.time;
        }
    }

    public TrackAsset AddTrack<T>(string trackName = null) where T : TrackAsset, new()
    {
        if (_asset == null) return null;
        var t = _asset.CreateTrack<T>(trackName);
        _selectedTrack = t;
        Log($"AddTrack {typeof(T).Name}");
        return t;
    }

    public SignalEmitter AddSignal(double time, SignalAsset signal)
    {
        if (_asset == null) return null;
        SignalTrack st = null;
        foreach (var t in _asset.GetOutputTracks())
        {
            if (t is SignalTrack s) { st = s; break; }
        }
        st ??= _asset.CreateTrack<SignalTrack>("Signals");
        var e = st.CreateMarker(time, signal);
        Log($"Signal @ {time:F2} '{signal?.signalName}'");
        return e;
    }

    public TimelineClip AddDefaultClip(TrackAsset track, double start, double duration)
    {
        if (track == null) return null;
        var c = track.CreateDefaultClip();
        c.start = start;
        c.duration = duration;
        return c;
    }

    public int GetTrackCount() => _asset != null ? _asset.outputTrackCount : 0;

    public IEnumerable<TimelineClip> GetClipsAtPlayhead()
    {
        if (_asset == null) yield break;
        foreach (var c in _asset.GetClipsAt(_playhead))
            yield return c;
    }

    public void SelectTrack(TrackAsset track) => _selectedTrack = track;

    protected override void OnGUI()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button(_isPlaying ? "❚❚" : "▶", EditorStyles.toolbarButton, GUILayout.Width(28)))
        {
            if (_isPlaying) Pause(); else Play();
        }
        if (GUILayout.Button("■", EditorStyles.toolbarButton, GUILayout.Width(24)))
            Stop();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"t={_playhead:F2}", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();

        if (_asset == null)
        {
            GUILayout.Label("No TimelineAsset assigned", EditorStyles.helpBox);
            return;
        }

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (var track in _asset.GetOutputTracks())
        {
            if (track == null) continue;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(track.muted ? $"[M] {track.name}" : track.name, GUILayout.Width(140)))
                _selectedTrack = track;
            GUILayout.Label($"clips={track.clipCount}", EditorStyles.miniLabel);
            if (track is SignalTrack st)
                GUILayout.Label($"markers={st.emitterCount}", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_status))
            GUILayout.Label(_status, EditorStyles.miniLabel);
    }

    private void Log(string msg)
    {
        _log.Add(msg);
        if (_log.Count > 64) _log.RemoveAt(0);
        _status = msg;
    }
}
