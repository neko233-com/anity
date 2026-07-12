using System;

namespace UnityEngine;

public struct Vector4 : IEquatable<Vector4>
{
  public float x;
  public float y;
  public float z;
  public float w;

  public Vector4(float x, float y, float z, float w)
  {
    this.x = x;
    this.y = y;
    this.z = z;
    this.w = w;
  }

  public Vector4(float x, float y)
  {
    this.x = x;
    this.y = y;
    this.z = 0f;
    this.w = 0f;
  }

  public Vector4(float x, float y, float z)
  {
    this.x = x;
    this.y = y;
    this.z = z;
    this.w = 0f;
  }

  public float this[int index]
  {
    get
    {
      return index switch
      {
        0 => x,
        1 => y,
        2 => z,
        3 => w,
        _ => throw new IndexOutOfRangeException()
      };
    }
    set
    {
      switch (index)
      {
        case 0: x = value; break;
        case 1: y = value; break;
        case 2: z = value; break;
        case 3: w = value; break;
        default: throw new IndexOutOfRangeException();
      }
    }
  }

  public static Vector4 zero => new(0f, 0f, 0f, 0f);
  public static Vector4 one => new(1f, 1f, 1f, 1f);
  public static Vector4 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
  public static Vector4 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

  public float sqrMagnitude => x * x + y * y + z * z + w * w;
  public float magnitude => MathF.Sqrt(sqrMagnitude);

  public Vector4 normalized
  {
    get
    {
      float m = magnitude;
      return m > 1e-6f ? this / m : zero;
    }
  }

  public void Normalize()
  {
    float m = magnitude;
    if (m > 1e-6f) { this /= m; }
    else { this = zero; }
  }

  public void Set(float newX, float newY, float newZ, float newW) { x = newX; y = newY; z = newZ; w = newW; }

  public void Scale(Vector4 scale)
  {
    x *= scale.x; y *= scale.y; z *= scale.z; w *= scale.w;
  }

  public static Vector4 Scale(Vector4 a, Vector4 b)
  {
    return new Vector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
  }

  public static float Distance(Vector4 a, Vector4 b) => (a - b).magnitude;
  public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

  public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
  {
    t = Mathf.Clamp01(t);
    return new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
  }

  public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t)
  {
    return new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
  }

  public static Vector4 MoveTowards(Vector4 current, Vector4 target, float maxDistanceDelta)
  {
    Vector4 toVector = target - current;
    float dist = toVector.magnitude;
    if (dist <= maxDistanceDelta || dist < 1e-6f) return target;
    return current + toVector / dist * maxDistanceDelta;
  }

  public static Vector4 operator +(Vector4 a, Vector4 b) => new(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
  public static Vector4 operator -(Vector4 a, Vector4 b) => new(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
  public static Vector4 operator *(Vector4 a, float d) => new(a.x * d, a.y * d, a.z * d, a.w * d);
  public static Vector4 operator *(float d, Vector4 a) => new(a.x * d, a.y * d, a.z * d, a.w * d);
  public static Vector4 operator *(Vector4 a, Vector4 b) => new(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
  public static Vector4 operator /(Vector4 a, float d) => new(a.x / d, a.y / d, a.z / d, a.w / d);
  public static Vector4 operator /(Vector4 a, Vector4 b) => new(a.x / b.x, a.y / b.y, a.z / b.z, a.w / b.w);
  public static Vector4 operator -(Vector4 a) => new(-a.x, -a.y, -a.z, -a.w);

  public static bool operator ==(Vector4 a, Vector4 b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
  public static bool operator !=(Vector4 a, Vector4 b) => !(a == b);

  public static implicit operator Vector4(Vector2 v) => new(v.x, v.y, 0f, 0f);
  public static implicit operator Vector4(Vector3 v) => new(v.x, v.y, v.z, 0f);

  public bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
  public override bool Equals(object? obj) => obj is Vector4 other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(x, y, z, w);
  public override string ToString() => $"({x:F2}, {y:F2}, {z:F2}, {w:F2})";
}
