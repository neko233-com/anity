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

  public Vector3 min
  {
    get => center - extents;
    set => SetMinMax(value, max);
  }
  public Vector3 max
  {
    get => center + extents;
    set => SetMinMax(min, value);
  }

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

  public bool Intersects(Bounds bounds)
  {
    var minP = min;
    var maxP = max;
    var bMin = bounds.min;
    var bMax = bounds.max;
    return minP.x <= bMax.x && maxP.x >= bMin.x &&
           minP.y <= bMax.y && maxP.y >= bMin.y &&
           minP.z <= bMax.z && maxP.z >= bMin.z;
  }

  public Vector3 ClosestPoint(Vector3 point)
  {
    var minP = min;
    var maxP = max;
    return new Vector3(
      Mathf.Clamp(point.x, minP.x, maxP.x),
      Mathf.Clamp(point.y, minP.y, maxP.y),
      Mathf.Clamp(point.z, minP.z, maxP.z)
    );
  }

  public float SqrDistance(Vector3 point)
  {
    var closest = ClosestPoint(point);
    return (point - closest).sqrMagnitude;
  }

  public bool IntersectRay(Ray ray)
  {
    return IntersectRay(ray, out _);
  }

  public bool IntersectRay(Ray ray, out float distance)
  {
    distance = 0f;
    var tmin = float.NegativeInfinity;
    var tmax = float.PositiveInfinity;
    var minP = min;
    var maxP = max;

    for (int i = 0; i < 3; i++)
    {
      float origin = i == 0 ? ray.origin.x : i == 1 ? ray.origin.y : ray.origin.z;
      float dir = i == 0 ? ray.direction.x : i == 1 ? ray.direction.y : ray.direction.z;
      float minB = i == 0 ? minP.x : i == 1 ? minP.y : minP.z;
      float maxB = i == 0 ? maxP.x : i == 1 ? maxP.y : maxP.z;

      if (Mathf.Abs(dir) < Mathf.Epsilon)
      {
        if (origin < minB || origin > maxB)
          return false;
      }
      else
      {
        float t1 = (minB - origin) / dir;
        float t2 = (maxB - origin) / dir;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tmin = Mathf.Max(tmin, t1);
        tmax = Mathf.Min(tmax, t2);
        if (tmin > tmax)
          return false;
      }
    }

    distance = tmin;
    return true;
  }

  public void Expand(float amount)
  {
    amount *= 0.5f;
    extents += new Vector3(amount, amount, amount);
  }

  public void Expand(Vector3 amount)
  {
    extents += amount * 0.5f;
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

