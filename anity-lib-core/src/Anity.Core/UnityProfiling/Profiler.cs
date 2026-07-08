using System;
using System.Collections.Generic;

namespace Unity.Profiling;

public static class Profiler
{
  private static bool _enabled = true;
  private static readonly Dictionary<string, ProfilerRecorder> _recorders = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Stack<string> _sampleStack = new();

  public static bool enabled
  {
    get => _enabled;
    set => _enabled = value;
  }

  public static bool supported => true;
  public static string logFile => string.Empty;

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
  public static long GetTempAllocatorSize() => 0;
  public static long GetAllocatedMemoryForGraphicsDriver() => 0;
  public static long GetUsedHeapSizeLong() => 0;
  public static long GetTargetAllocationSize() => 0;

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

  public static void EmitFrameMeta()
  {
  }

  public static void SetAreaEnabled(ProfilerArea area, params string[] channels)
  {
    _ = area;
    _ = channels;
  }

  public static long GetRuntimeMemorySizeLong(object target) => 0;
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
  Internal,
  Scripts,
  Rendering,
  Animation,
  Audio,
  Physics,
  Video
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

public readonly struct ProfilerRecorder : IDisposable
{
  private readonly string _statisticName;
  public readonly bool IsValid;

  public ProfilerRecorder(string statisticName, int capacity = 0)
  {
    _statisticName = statisticName;
    IsValid = true;
    sampleCount = 0;
    _capacity = Math.Max(1, capacity);
    _lastValue = 0;
  }

  private readonly int _capacity;
  private readonly long _lastValue;
  public long sampleCount { get; }
  public long LastValue => _lastValue;

  public static ProfilerRecorder StartNew(ProfilerCategory category, string statName, int capacity = 0)
  {
    _ = category;
    var recorder = new ProfilerRecorder(statName, capacity);
    return recorder;
  }

  public long LastValueAsLong() => _lastValue;
  public long GetSample(int index) => index < 0 ? 0 : _lastValue;
  public void Dispose() {}
}
