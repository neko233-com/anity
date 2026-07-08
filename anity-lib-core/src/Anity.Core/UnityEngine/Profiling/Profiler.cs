using System;

namespace UnityEngine.Profiling;

public static class Profiler
{
  private static long _sampleDepth;

  public static bool enabled
  {
    get => Unity.Profiling.Profiler.enabled;
    set => Unity.Profiling.Profiler.enabled = value;
  }

  public static bool supported => Unity.Profiling.Profiler.supported;
  public static string logFile => Unity.Profiling.Profiler.logFile;

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

  public static long GetTempAllocatorSize()
  {
    return Unity.Profiling.Profiler.GetTempAllocatorSize();
  }

  public static long GetAllocatedMemoryForGraphicsDriver()
  {
    return Unity.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver();
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

public readonly struct ProfilerMarker : IDisposable
{
  private readonly Unity.Profiling.ProfilerMarker _marker;

  public ProfilerMarker(string name, ProfilerCategory category = ProfilerCategory.Internal)
  {
    _marker = new Unity.Profiling.ProfilerMarker(name, (Unity.Profiling.ProfilerCategory)(int)category);
  }

  public ProfilerMarkerAutoScope Auto()
  {
    return new ProfilerMarkerAutoScope(_marker);
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
    // no-op for compatibility
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
  Video = 6
}
