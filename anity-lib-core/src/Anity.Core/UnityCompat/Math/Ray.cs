using UnityEngine;

namespace UnityEngine;

public readonly struct Ray
{
  public Vector3 origin { get; }
  public Vector3 direction { get; }

  public Ray(Vector3 origin, Vector3 direction)
  {
    this.origin = origin;
    this.direction = direction;
  }

  public Vector3 GetPoint(float distance)
  {
    return origin + direction * distance;
  }
}

