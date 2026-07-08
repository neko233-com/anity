using System;

namespace Unity.Profiling.LowLevel;

public static class ProfilerUnsafeUtility
{
  private static ulong _nextId;
  private static readonly object _lock = new();

  public const ushort kInvalidProfilerCategoryId = 0;
  public const ushort kInvalidProfilerMarkerId = 0;
  public static readonly int kProfilerTagVersion = 1;

  public static bool IsEnabled()
  {
    return Unity.Profiling.Profiler.enabled;
  }

  public static void SetEnabled(bool enabled)
  {
    Unity.Profiling.Profiler.enabled = enabled;
  }

  public static void RegisterProfilerCounter()
  {
    // compatibility stub: marker for allocation counters on platforms requiring init
  }

  public static ulong CreateMarker(string name, ushort categoryId, byte flags, int metadataCount)
  {
    _ = name;
    _ = categoryId;
    _ = flags;
    _ = metadataCount;
    lock (_lock)
    {
      checked { _nextId++; }
      return _nextId;
    }
  }

  public static void DestroyMarker(ulong markerId)
  {
    _ = markerId;
  }

  public static string GetMarkerName(ulong markerId)
  {
    _ = markerId;
    return string.Empty;
  }

  public static void SetCounterValue(long markerId, long value)
  {
    _ = markerId;
    _ = value;
  }

  public static void SetCounterValue(ulong markerId, int value)
  {
    _ = markerId;
    _ = value;
  }

  public static void SetCounterValue(ulong markerId, float value)
  {
    _ = markerId;
    _ = value;
  }

  public static void SetMarkerMetadata(ulong markerId, int metadataIndex, uint size, IntPtr data)
  {
    _ = markerId;
    _ = metadataIndex;
    _ = size;
    _ = data;
  }

  public static int GetMarkerMetadataCount(ulong markerId)
  {
    _ = markerId;
    return 0;
  }

  public static bool IsValidId(ulong markerId)
  {
    return markerId != 0;
  }
}

public readonly struct ProfilerCounterValue
{
  private readonly long _value;

  public ProfilerCounterValue(long value)
  {
    _value = value;
  }

  public long ToInt64() => _value;
  public float ToSingle() => (float)_value;
}

public enum ProfilerMarkerDataType : ushort
{
  Invalid,
  Int32,
  UInt32,
  Int64,
  UInt64,
  Float,
  Double,
  String
}

public readonly struct ProfilerMarkerDataTypeDesc
{
  public readonly ProfilerMarkerDataType type;
  public readonly string name;
  public readonly int size;

  public ProfilerMarkerDataTypeDesc(ProfilerMarkerDataType type, string name, int size)
  {
    this.type = type;
    this.name = name;
    this.size = size;
  }
}

public readonly struct MarkerFlags
{
  private readonly ushort _value;

  public MarkerFlags(ushort value)
  {
    _value = value;
  }

  public ushort Value => _value;
}
