using System;
using System.Collections.Generic;

namespace UnityEngine.Playables;

public enum DirectorUpdateMode
{
    DSPClock = 0,
    GameTime = 1,
    UnscaledGameTime = 2,
    Manual = 3
}

public enum PlayState
{
    Paused = 0,
    Playing = 1,
    Delayed = 2
}

public enum DirectorWrapMode
{
    Hold = 0,
    Loop = 1,
    None = 2
}

public struct FrameData
{
    public ulong frameId;
    public float deltaTime;
    public float weight;
    public float effectiveWeight;
    public double effectiveParentSpeed;
    public double effectiveSpeed;
    public FrameData.Flags flags;

    [Flags]
    public enum Flags
    {
        Evaluate = 1,
        SeekOccured = 2,
        Loop = 4,
        Hold = 8
    }
}

public interface IPlayable
{
    PlayableHandle GetHandle();
}

public struct PlayableHandle : IEquatable<PlayableHandle>
{
    internal int id;
    internal PlayableGraph graph;
    public bool IsValid() => id != 0 && graph != null && graph.IsValidHandle(this);
    public bool Equals(PlayableHandle other) => id == other.id && ReferenceEquals(graph, other.graph);
    public static PlayableHandle Null => default;
}

public struct Playable : IPlayable, IEquatable<Playable>
{
    private PlayableHandle _handle;
    public Playable(PlayableHandle handle) => _handle = handle;
    public PlayableHandle GetHandle() => _handle;
    public bool IsValid() => _handle.IsValid();
    public bool Equals(Playable other) => _handle.Equals(other._handle);
    public static Playable Null => default;
}

public class PlayableGraph
{
    private static int s_nextId = 1;
    private readonly Dictionary<int, PlayableNode> _nodes = new();
    private int _rootId;
    private bool _valid = true;
    private bool _playing;
    private double _time;
    private DirectorUpdateMode _updateMode = DirectorUpdateMode.GameTime;

    public string get_resolver() => "AnityPlayableGraph";
    public bool IsValid() => _valid;
    public bool IsPlaying() => _playing;
    public bool IsDone() => false;

    public static PlayableGraph Create(string name = "Graph")
    {
        _ = name;
        return new PlayableGraph();
    }

    public void Destroy()
    {
        _valid = false;
        _nodes.Clear();
    }

    public void Play() => _playing = true;
    public void Stop() { _playing = false; _time = 0; }
    public void Evaluate() => Evaluate(0);
    public void Evaluate(float deltaTime)
    {
        if (!_valid) return;
        if (_playing || deltaTime > 0)
            _time += deltaTime;
        foreach (var n in _nodes.Values)
            n.time = _time;
    }

    public double GetTime() => _time;
    public void SetTime(double time) => _time = time;

    public DirectorUpdateMode GetTimeUpdateMode() => _updateMode;
    public void SetTimeUpdateMode(DirectorUpdateMode value) => _updateMode = value;

    internal PlayableHandle CreateHandle(string typeName)
    {
        int id = s_nextId++;
        _nodes[id] = new PlayableNode { typeName = typeName, time = 0 };
        if (_rootId == 0) _rootId = id;
        return new PlayableHandle { id = id, graph = this };
    }

    internal bool IsValidHandle(PlayableHandle h) => h.id != 0 && _nodes.ContainsKey(h.id);

    internal sealed class PlayableNode
    {
        public string typeName = string.Empty;
        public double time;
        public double duration = double.MaxValue;
        public float weight = 1f;
    }
}

public class ScriptPlayableOutput
{
    public static ScriptPlayableOutput Create(PlayableGraph graph, string name)
    {
        _ = graph; _ = name;
        return new ScriptPlayableOutput();
    }
    public void SetSourcePlayable(Playable playable) => Source = playable;
    public Playable Source { get; private set; }
}

public struct ScriptPlayable<T> : IPlayable where T : class, new()
{
    private PlayableHandle _handle;
    private T _behavior;
    public static ScriptPlayable<T> Create(PlayableGraph graph, int inputCount = 0)
    {
        _ = inputCount;
        var h = graph.CreateHandle(typeof(T).Name);
        return new ScriptPlayable<T> { _handle = h, _behavior = new T() };
    }
    public PlayableHandle GetHandle() => _handle;
    public bool IsValid() => _handle.IsValid();
    public T GetBehaviour() => _behavior;
    public static implicit operator Playable(ScriptPlayable<T> p) => new Playable(p._handle);
}

/// <summary>Base for assets that produce playables (UnityEngine.Playables.PlayableAsset).</summary>
public abstract class PlayableAsset : ScriptableObject
{
    public virtual double duration => 0;
    public abstract Playable CreatePlayable(PlayableGraph graph, GameObject owner);
}

/// <summary>UnityEngine.Playables.PlayableDirector — drives a PlayableGraph / Timeline.</summary>
[AddComponentMenu("Playables/Playable Director")]
public class PlayableDirector : MonoBehaviour
{
    private PlayableGraph _graph;
    private double _time;
    private double _duration = 5.0;
    private PlayState _state = PlayState.Paused;
    private DirectorWrapMode _wrap = DirectorWrapMode.Hold;
    private DirectorUpdateMode _updateMode = DirectorUpdateMode.GameTime;
    private PlayableAsset _playableAsset;
    private bool _graphReady;

    public PlayableAsset playableAsset
    {
        get => _playableAsset;
        set
        {
            _playableAsset = value;
            if (_graphReady && _playableAsset != null)
                RebuildGraph();
        }
    }

    public DirectorUpdateMode timeUpdateMode
    {
        get => _updateMode;
        set => _updateMode = value;
    }

    public DirectorWrapMode extrapolationMode
    {
        get => _wrap;
        set => _wrap = value;
    }

    public double time
    {
        get => _time;
        set => _time = Math.Max(0, value);
    }

    public double initialTime { get; set; }
    public double duration
    {
        get
        {
            if (_playableAsset != null)
                return _playableAsset.duration;
            return _duration;
        }
        set => _duration = Math.Max(0, value);
    }

    public PlayState state => _state;
    public bool playOnAwake { get; set; } = true;

    public event Action<PlayableDirector> played;
    public event Action<PlayableDirector> paused;
    public event Action<PlayableDirector> stopped;

    protected override void Awake()
    {
        base.Awake();
        EnsureGraph();
        if (playOnAwake) Play();
    }

    private void EnsureGraph()
    {
        if (_graphReady && _graph != null && _graph.IsValid()) return;
        _graph = PlayableGraph.Create(gameObject != null ? gameObject.name : "Director");
        if (_playableAsset != null)
        {
            var root = _playableAsset.CreatePlayable(_graph, gameObject);
            _ = root;
        }
        _graphReady = true;
    }

    public void Play()
    {
        EnsureGraph();
        _state = PlayState.Playing;
        if (_time <= 0 && initialTime > 0)
            _time = initialTime;
        if (_graph != null && _graph.IsValid())
            _graph.Play();
        played?.Invoke(this);
    }

    public void Pause()
    {
        _state = PlayState.Paused;
        paused?.Invoke(this);
    }

    public void Stop()
    {
        _state = PlayState.Paused;
        _time = 0;
        if (_graph != null && _graph.IsValid())
            _graph.Stop();
        stopped?.Invoke(this);
    }

    public void Evaluate()
    {
        EnsureGraph();
        float dt = _updateMode == DirectorUpdateMode.Manual ? 0f : Time.deltaTime;
        Evaluate(dt);
    }

    public void Evaluate(float deltaTime)
    {
        EnsureGraph();
        if (_state == PlayState.Playing)
        {
            _time += deltaTime;
            double dur = duration;
            if (dur > 0 && _time >= dur)
            {
                switch (_wrap)
                {
                    case DirectorWrapMode.Loop:
                        _time = _time % dur;
                        break;
                    case DirectorWrapMode.None:
                        _time = dur;
                        _state = PlayState.Paused;
                        stopped?.Invoke(this);
                        break;
                    default: // Hold
                        _time = dur;
                        break;
                }
            }
        }
        if (_graph != null && _graph.IsValid())
        {
            _graph.SetTime(_time);
            _graph.Evaluate(deltaTime);
        }
    }

    public void RebuildGraph()
    {
        if (_graph != null && _graph.IsValid())
            _graph.Destroy();
        _graphReady = false;
        EnsureGraph();
    }

    public void SetGenericBinding(UnityEngine.Object key, UnityEngine.Object value)
    {
        _ = key; _ = value;
    }

    public UnityEngine.Object GetGenericBinding(UnityEngine.Object key)
    {
        _ = key;
        return null;
    }

    public PlayableGraph playableGraph
    {
        get
        {
            EnsureGraph();
            return _graph;
        }
    }

    protected override void OnDestroy()
    {
        if (_graph != null && _graph.IsValid())
            _graph.Destroy();
        base.OnDestroy();
    }
}
