namespace UnityEngine;

public struct Bounds
{
  public Vector3 center;
  public Vector3 size;

  public Bounds(Vector3 center, Vector3 size)
  {
    this.center = center;
    this.size = size;
  }

  public Vector3 extents
  {
    get => size * 0.5f;
    set => size = value * 2f;
  }

  public Vector3 min => center - extents;
  public Vector3 max => center + extents;

  public bool Contains(Vector3 point)
  {
    var minP = min;
    var maxP = max;
    return point.x >= minP.x && point.x <= maxP.x &&
      point.y >= minP.y && point.y <= maxP.y &&
      point.z >= minP.z && point.z <= maxP.z;
  }

  public void Encapsulate(Vector3 point)
  {
    var currentMin = min;
    var currentMax = max;
    currentMin.x = Mathf.Min(currentMin.x, point.x);
    currentMin.y = Mathf.Min(currentMin.y, point.y);
    currentMin.z = Mathf.Min(currentMin.z, point.z);
    currentMax.x = Mathf.Max(currentMax.x, point.x);
    currentMax.y = Mathf.Max(currentMax.y, point.y);
    currentMax.z = Mathf.Max(currentMax.z, point.z);
    SetMinMax(currentMin, currentMax);
  }

  public void Encapsulate(Bounds bounds)
  {
    Encapsulate(bounds.min);
    Encapsulate(bounds.max);
  }

  public void SetMinMax(Vector3 min, Vector3 max)
  {
    center = (min + max) * 0.5f;
    size = max - min;
  }

  public override string ToString()
  {
    return $"Center={center}, Size={size}";
  }
}

