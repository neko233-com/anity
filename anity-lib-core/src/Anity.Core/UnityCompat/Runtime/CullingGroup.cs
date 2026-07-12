using System;

namespace UnityEngine;

public struct BoundingSphere
{
  public Vector3 position;
  public float radius;

  public BoundingSphere(Vector3 pos, float rad)
  {
    position = pos;
    radius = rad;
  }
}

public struct CullingGroupEvent
{
  public int index { get; internal set; }
  public bool isVisible { get; internal set; }
  public bool wasVisible { get; internal set; }
  public bool hasBecomeVisible => isVisible && !wasVisible;
  public bool hasBecomeInvisible => !isVisible && wasVisible;
  public byte currentDistance { get; internal set; }
  public byte previousDistance { get; internal set; }
}

public sealed class CullingGroup : IDisposable
{
  private BoundingSphere[] _boundingSpheres = Array.Empty<BoundingSphere>();
  private int _boundingSphereCount;
  private float[] _boundingDistances = Array.Empty<float>();
  private bool[] _visibility;
  private byte[] _distanceBands;
  private bool[] _wasVisible;
  private bool _enabled = true;
  private bool _disposed;

  public CullingGroup()
  {
  }

  public bool enabled
  {
    get => _enabled;
    set => _enabled = value;
  }

  public Camera? targetCamera { get; set; }

  public Action<CullingGroupEvent>? onStateChanged { get; set; }

  public void Dispose()
  {
    if (!_disposed)
    {
      _disposed = true;
      _boundingSpheres = Array.Empty<BoundingSphere>();
      _boundingDistances = Array.Empty<float>();
      _visibility = null;
      _distanceBands = null;
      _wasVisible = null;
      onStateChanged = null;
      targetCamera = null;
    }
  }

  public void SetBoundingSpheres(BoundingSphere[]? array)
  {
    _boundingSpheres = array ?? Array.Empty<BoundingSphere>();
    _boundingSphereCount = _boundingSpheres.Length;
    _visibility = new bool[_boundingSphereCount];
    _distanceBands = new byte[_boundingSphereCount];
    _wasVisible = new bool[_boundingSphereCount];
  }

  public void SetBoundingSphereCount(int count)
  {
    _boundingSphereCount = Math.Min(count, _boundingSpheres.Length);
    if (_visibility == null || _visibility.Length != _boundingSpheres.Length)
    {
      _visibility = new bool[_boundingSpheres.Length];
      _distanceBands = new byte[_boundingSpheres.Length];
      _wasVisible = new bool[_boundingSpheres.Length];
    }
  }

  public void SetBoundingDistances(float[]? distances)
  {
    _boundingDistances = distances ?? Array.Empty<float>();
  }

  public bool IsVisible(int index)
  {
    if (index < 0 || index >= _boundingSphereCount) return false;
    return _visibility != null && _visibility[index];
  }

  public int GetDistance(int index)
  {
    if (index < 0 || index >= _boundingSphereCount) return -1;
    return _distanceBands != null ? _distanceBands[index] : (byte)0;
  }

  public int[] QueryIndices(bool visible, int[]? result = null, int firstIndex = 0)
  {
    int count = 0;
    for (int i = firstIndex; i < _boundingSphereCount; i++)
    {
      if ((_visibility != null && _visibility[i]) == visible)
        count++;
    }
    if (result == null || result.Length < count)
      result = new int[count];
    int idx = 0;
    for (int i = firstIndex; i < _boundingSphereCount && idx < count; i++)
    {
      if ((_visibility != null && _visibility[i]) == visible)
        result[idx++] = i;
    }
    return result;
  }

  public int[] QueryIndices(int distanceBand, int[]? result = null, int firstIndex = 0)
  {
    int count = 0;
    for (int i = firstIndex; i < _boundingSphereCount; i++)
    {
      if (_distanceBands != null && _distanceBands[i] == distanceBand)
        count++;
    }
    if (result == null || result.Length < count)
      result = new int[count];
    int idx = 0;
    for (int i = firstIndex; i < _boundingSphereCount && idx < count; i++)
    {
      if (_distanceBands != null && _distanceBands[i] == distanceBand)
        result[idx++] = i;
    }
    return result;
  }

  public int[] QueryIndices(bool visible, int distanceBand, int[]? result = null, int firstIndex = 0)
  {
    int count = 0;
    for (int i = firstIndex; i < _boundingSphereCount; i++)
    {
      bool vis = _visibility != null && _visibility[i];
      byte dist = _distanceBands != null ? _distanceBands[i] : (byte)0;
      if (vis == visible && dist == distanceBand)
        count++;
    }
    if (result == null || result.Length < count)
      result = new int[count];
    int idx = 0;
    for (int i = firstIndex; i < _boundingSphereCount && idx < count; i++)
    {
      bool vis = _visibility != null && _visibility[i];
      byte dist = _distanceBands != null ? _distanceBands[i] : (byte)0;
      if (vis == visible && dist == distanceBand)
        result[idx++] = i;
    }
    return result;
  }

  internal void SetVisibility(int index, bool visible, byte distanceBand = 0)
  {
    if (index < 0 || index >= _boundingSphereCount) return;
    if (_visibility == null || _wasVisible == null || _distanceBands == null) return;

    _wasVisible[index] = _visibility[index];
    _visibility[index] = visible;
    byte prevDist = _distanceBands[index];
    _distanceBands[index] = distanceBand;

    if (_wasVisible[index] != visible || prevDist != distanceBand)
    {
      onStateChanged?.Invoke(new CullingGroupEvent
      {
        index = index,
        isVisible = visible,
        wasVisible = _wasVisible[index],
        currentDistance = distanceBand,
        previousDistance = prevDist
      });
    }
  }
}
