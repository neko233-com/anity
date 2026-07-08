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

  public override string ToString()
  {
    return $"Center={center}, Size={size}";
  }
}

