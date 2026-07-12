using System;
using System.Collections.Generic;
using System.Threading;

namespace UnityEngine.Profiling;

public static class Profiler
{
  private static long _sampleDepth;
  private static readonly HashSet<int> _profiledThreads = new();
  private static readonly object _threadLock = new();

  public static bool enabled
  {
    get => Unity.Profiling.Profiler.enabled;
    set => Unity.Profiling.Profiler.enabled = value;
  }

  public static bool supported => Unity.Profiling.Profiler.supported;
  public static string logFile => Unity.Profiling.Profiler.logFile;
  public static int enabledFrames
  {
    get => Unity.Profiling.Profiler.enabledFrames;
    set => Unity.Profiling.Profiler.enabledFrames = value;
  }
  public static int areaCount => Unity.Profiling.Profiler.areaCount;
  public static long usedHeapSizeLong => Unity.Profiling.Profiler.usedHeapSizeLong;

  public static bool enableBinaryLog
  {
    get => false;
    set => _ = value;
  }

  public static int maxUsedMemoryLevel
  {
    get => 0;
    set => _ = value;
  }

  public static void BeginSample(string name)
  {
    if (name is null)
    {
      throw new ArgumentNullException(nameof(name));
    }

    _sampleDepth++;
    Unity.Profiling.Profiler.BeginSample(name);
  }

  public static void EndSample()
  {
    if (_sampleDepth > 0)
    {
      _sampleDepth--;
    }

    Unity.Profiling.Profiler.EndSample();
  }

  public static long GetTotalAllocatedMemoryLong()
  {
    return Unity.Profiling.Profiler.GetTotalAllocatedMemoryLong();
  }

  public static long GetMonoUsedSizeLong()
  {
    return Unity.Profiling.Profiler.GetMonoUsedSizeLong();
  }

  public static long GetMonoHeapSizeLong()
  {
    return Unity.Profiling.Profiler.GetMonoHeapSizeLong();
  }

  public static long GetTotalReservedMemoryLong()
  {
    return Unity.Profiling.Profiler.GetTotalReservedMemoryLong();
  }

  public static long GetTotalUnusedReservedMemoryLong()
  {
    return Unity.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();
  }

  public static long GetTempAllocatorSize()
  {
    return Unity.Profiling.Profiler.GetTempAllocatorSize();
  }

  public static long GetAllocatedMemoryForGraphicsDriver()
  {
    return Unity.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver();
  }

  public static long residentMemorySizeLong => GetTotalReservedMemoryLong();
  public static long monoUsedSizeLong => GetMonoUsedSizeLong();

  public static void SetTempAllocatorRequestedSize(long size)
  {
    Unity.Profiling.Profiler.SetTempAllocatorRequestedSize(size);
  }

  public static void SetTempAllocatorSize(int size)
  {
    SetTempAllocatorRequestedSize(size);
  }

  public static void AddFrames(int count)
  {
    _ = count;
  }

  public static void BeginThreadProfiling(string threadGroupName, string threadName)
  {
    lock (_threadLock)
    {
      _profiledThreads.Add(Thread.CurrentThread.ManagedThreadId);
    }
    Unity.Profiling.Profiler.BeginThreadProfiling(threadGroupName, threadName);
  }

  public static void EndThreadProfiling()
  {
    lock (_threadLock)
    {
      _profiledThreads.Remove(Thread.CurrentThread.ManagedThreadId);
    }
    Unity.Profiling.Profiler.EndThreadProfiling();
  }

  public static void AddFramesFromFile(string filepath)
  {
    Unity.Profiling.Profiler.AddFramesFromFile(filepath);
  }

  public static void EmitFrameMeta()
  {
    Unity.Profiling.Profiler.EmitFrameMeta();
  }

  public static void SetAreaEnabled(ProfilerArea area, bool enabled)
  {
    Unity.Profiling.Profiler.SetAreaEnabled((Unity.Profiling.ProfilerArea)(int)area, enabled);
  }

  public static bool GetAreaEnabled(ProfilerArea area)
  {
    return Unity.Profiling.Profiler.GetAreaEnabled((Unity.Profiling.ProfilerArea)(int)area);
  }

  public static ProfilerMarker CreateMarker(string name, ProfilerCategory category = ProfilerCategory.Internal)
  {
    return new ProfilerMarker(name, category);
  }
}

public struct ProfilerRecorder : IDisposable
{
  private Unity.Profiling.ProfilerRecorder _recorder;
  private bool _disposed;

  public ProfilerRecorder(string categoryName, string statName, int capacity = 1)
  {
    _recorder = Unity.Profiling.ProfilerRecorder.StartNew(statName, capacity);
    _disposed = false;
    IsValid = true;
  }

  public ProfilerRecorder(ProfilerCategory category, string statName, int capacity = 1)
  {
    _recorder = Unity.Profiling.ProfilerRecorder.StartNew((Unity.Profiling.ProfilerCategory)(int)category, statName, capacity);
    _disposed = false;
    IsValid = true;
  }

  public bool IsValid { get; }
  public bool IsRunning => _recorder.IsRunning;
  public long Count => _recorder.Count;
  public float CurrentValue => _recorder.CurrentValue;
  public float LastValue => _recorder.LastValue;
  public float MaxValue => _recorder.MaxValue;
  public float MinValue => _recorder.MinValue;

  public static ProfilerRecorder StartNew(string markerName, int capacity = 1)
  {
    return new ProfilerRecorder(ProfilerCategory.Internal, markerName, capacity);
  }

  public static ProfilerRecorder StartNew(ProfilerCategory category, string statName, int capacity = 1)
  {
    return new ProfilerRecorder(category, statName, capacity);
  }

  public void Stop() => _recorder.Stop();
  public void Reset() => _recorder.Reset();
  public long SampleValues(List<float> dest) => _recorder.SampleValues(dest);

  public void Dispose()
  {
    if (!_disposed)
    {
      _recorder.Dispose();
      _disposed = true;
    }
  }
}

public struct ProfilerMarker : IDisposable
{
  private readonly Unity.Profiling.ProfilerMarker _marker;
  private bool _disposed;

  public ProfilerMarker(string name, ProfilerCategory category = ProfilerCategory.Internal)
  {
    _marker = new Unity.Profiling.ProfilerMarker(name, (Unity.Profiling.ProfilerCategory)(int)category);
    _disposed = false;
  }

  public ProfilerMarker(ProfilerCategory category, string name) : this(name, category) { }

  public ProfilerMarkerAutoScope Auto()
  {
    return new ProfilerMarkerAutoScope(_marker);
  }

  public static ProfilerMarkerAutoScope Auto(ProfilerMarker marker)
  {
    return marker.Auto();
  }

  public void Begin()
  {
    _marker.Begin();
  }

  public void End()
  {
    _marker.End();
  }

  public void Dispose()
  {
    if (!_disposed)
    {
      _marker.End();
      _disposed = true;
    }
  }
}

public readonly struct ProfilerMarkerAutoScope : IDisposable
{
  private readonly Unity.Profiling.ProfilerMarkerAutoScope _scope;

  public ProfilerMarkerAutoScope(Unity.Profiling.ProfilerMarker marker)
  {
    _scope = marker.Auto();
  }

  public void Dispose()
  {
    _scope.Dispose();
  }
}

public enum ProfilerArea
{
  Cpu = 0,
  Gpu = 1,
  Rendering = 2,
  Memory = 3,
  Audio = 4,
  GlobalIllumination = 5,
  Physics = 6
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
