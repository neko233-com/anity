using System;
using System.Collections.Generic;

namespace Unity.Profiling;

public static class Profiler
{
  private static bool _enabled = true;
  private static int _enabledFrames = -1;
  private static readonly Dictionary<string, ProfilerRecorder> _recorders = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Stack<string> _sampleStack = new();
  private static long _usedHeapSize;
  private static long _tempAllocatorSize = 16 * 1024 * 1024;

  public static bool enabled
  {
    get => _enabled;
    set => _enabled = value;
  }

  public static bool supported => true;
  public static string logFile => string.Empty;
  public static int enabledFrames
  {
    get => _enabledFrames;
    set => _enabledFrames = value;
  }
  public static int areaCount => Enum.GetValues(typeof(ProfilerArea)).Length;
  public static long usedHeapSizeLong => _usedHeapSize;

  public static void Start()
  {
    _enabled = true;
  }

  public static void EnableBinaryLog(string file, bool doCollect = true)
  {
    _ = file;
    _ = doCollect;
  }

  public static void SetAreaEnabled(ProfilerArea area, bool enabled)
  {
    _ = area;
    _ = enabled;
  }

  public static bool GetAreaEnabled(ProfilerArea area)
  {
    _ = area;
    return true;
  }

  public static long GetTotalAllocatedMemoryLong() => 0;
  public static long GetTotalReservedMemoryLong() => 0;
  public static long GetTotalUnusedReservedMemoryLong() => 0;
  public static long GetMonoHeapSizeLong() => 0;
  public static long GetMonoUsedSizeLong() => 0;
  public static long GetTempAllocatorSize() => _tempAllocatorSize;
  public static long GetAllocatedMemoryForGraphicsDriver() => 0;
  public static long GetUsedHeapSizeLong() => _usedHeapSize;
  public static long GetTargetAllocationSize() => 0;

  public static void SetTempAllocatorRequestedSize(long size)
  {
    _tempAllocatorSize = Math.Max(0, size);
  }

  public static ProfilerMarker CreateMarker(string name, ProfilerCategory category = ProfilerCategory.Internal)
  {
    return new ProfilerMarker(name, category);
  }

  public static ProfilerRecorder DefaultRecorder() => new(string.Empty);

  public static void BeginSample(string name)
  {
    _sampleStack.Push(name);
  }

  public static void EndSample()
  {
    if (_sampleStack.Count > 0)
    {
      _sampleStack.Pop();
    }
  }

  public static void AddFramesFromFile(string filepath)
  {
    _ = filepath;
  }

  private static int _frameMetaCount;
  public static void EmitFrameMeta()
  {
    _frameMetaCount++;
  }

  public static void SetAreaEnabled(ProfilerArea area, params string[] channels)
  {
    _ = area;
    _ = channels;
  }

  public static long GetRuntimeMemorySizeLong(object target) => 0;

  public static void BeginThreadProfiling(string threadGroupName, string threadName)
  {
    _ = threadGroupName;
    _ = threadName;
  }

  public static void EndThreadProfiling()
  {
  }
}

public enum ProfilerArea
{
  Cpu,
  Gpu,
  Rendering,
  Memory,
  Audio,
  GlobalIllumination,
  Physics
}

public enum ProfilerCategory
{
  Internal = 0,
  Scripts = 1,
  Rendering = 2,
  Animation = 3,
  Audio = 4,
  Physics = 5,
  Video = 6,
  Code,
  Render,
  Memory,
  VirtualTexturing,
  UI,
  AI,
  VFXFile,
  VFX,
  MasterServer,
  GameCenter,
  GUISystem,
  Gi,
  LightProbes,
  Network,
  Loading,
  Other,
  Particles,
  GarbageCollector,
  Physics2D,
  FileIO,
  Umbrella,
  VR,
  Terrain,
  PhysicsJobs,
  FileIOJobs,
  VideoSystems
}

public readonly struct ProfilerMarker
{
  private readonly string _name;
  private readonly ProfilerCategory _category;

  public ProfilerMarker(string name, ProfilerCategory category)
  {
    _name = name;
    _category = category;
  }

  public ProfilerMarker(ProfilerCategory category, string name) : this(name, category) { }

  public void Begin()
  {
    Profiler.BeginSample(_name);
  }

  public void End()
  {
    Profiler.EndSample();
  }

  public ProfilerMarkerAutoScope Auto()
  {
    return new ProfilerMarkerAutoScope(_name);
  }

  public static ProfilerMarkerAutoScope Auto(ProfilerMarker marker)
  {
    return marker.Auto();
  }
}

public readonly struct ProfilerMarkerAutoScope : IDisposable
{
  private readonly string _name;

  public ProfilerMarkerAutoScope(string name)
  {
    _name = name;
    Profiler.BeginSample(name);
  }

  public void Dispose()
  {
    Profiler.EndSample();
  }
}

public struct ProfilerRecorder : IDisposable
{
  private readonly string _statisticName;
  private readonly ProfilerCategory _category;
  private readonly int _capacity;
  private readonly List<float> _samples;
  private float _currentValue;
  private float _lastValue;
  private float _maxValue;
  private float _minValue;
  private long _sampleCount;
  private bool _isRunning;
  private bool _disposed;

  public ProfilerRecorder(string statisticName, int capacity = 0) : this(ProfilerCategory.Internal, statisticName, capacity) { }

  public ProfilerRecorder(ProfilerCategory category, string statisticName, int capacity = 0)
  {
    _statisticName = statisticName;
    _category = category;
    _capacity = Math.Max(1, capacity);
    _samples = new List<float>(_capacity);
    _currentValue = 0;
    _lastValue = 0;
    _maxValue = float.MinValue;
    _minValue = float.MaxValue;
    _sampleCount = 0;
    _isRunning = false;
    _disposed = false;
    IsValid = !string.IsNullOrEmpty(statisticName);
  }

  public readonly bool IsValid { get; }
  public readonly long Count => _sampleCount;
  public readonly float CurrentValue => _currentValue;
  public readonly float LastValue => _lastValue;
  public readonly float MaxValue => _sampleCount > 0 ? _maxValue : 0f;
  public readonly float MinValue => _sampleCount > 0 ? _minValue : 0f;
  public readonly bool IsRunning => _isRunning;

  public static ProfilerRecorder StartNew(string markerName, int capacity = 1)
  {
    return StartNew(ProfilerCategory.Internal, markerName, capacity);
  }

  public static ProfilerRecorder StartNew(ProfilerCategory category, string statName, int capacity = 1)
  {
    var recorder = new ProfilerRecorder(category, statName, capacity);
    recorder.Start();
    return recorder;
  }

  public void Start()
  {
    _isRunning = true;
  }

  public void Stop()
  {
    _isRunning = false;
  }

  public void Reset()
  {
    _samples.Clear();
    _currentValue = 0;
    _lastValue = 0;
    _maxValue = float.MinValue;
    _minValue = float.MaxValue;
    _sampleCount = 0;
  }

  public long SampleValues(List<float> dest)
  {
    if (dest == null) return 0;
    dest.AddRange(_samples);
    return _samples.Count;
  }

  public float GetSample(int index)
  {
    if (index < 0 || index >= _samples.Count) return 0f;
    return _samples[index];
  }

  public long LastValueAsLong() => (long)_lastValue;

  internal void RecordValue(float value)
  {
    if (!_isRunning || _disposed) return;
    _currentValue = value;
    _lastValue = value;
    if (value > _maxValue) _maxValue = value;
    if (value < _minValue) _minValue = value;
    _sampleCount++;
    if (_capacity > 1)
    {
      if (_samples.Count >= _capacity)
        _samples.RemoveAt(0);
      _samples.Add(value);
    }
  }

  public void Dispose()
  {
    if (!_disposed)
    {
      Stop();
      _disposed = true;
    }
  }
}
