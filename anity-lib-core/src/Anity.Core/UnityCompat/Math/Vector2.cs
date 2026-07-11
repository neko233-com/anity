using System;
using System.Numerics;

namespace UnityEngine;

public struct Vector2
{
  public float x;
  public float y;

  public Vector2(float x, float y)
  {
    this.x = x;
    this.y = y;
  }

  public static Vector2 zero => new Vector2(0f, 0f);
  public static Vector2 one => new Vector2(1f, 1f);
  public static Vector2 up => new Vector2(0f, 1f);
  public static Vector2 down => new Vector2(0f, -1f);
  public static Vector2 right => new Vector2(1f, 0f);
  public static Vector2 left => new Vector2(-1f, 0f);

  public float magnitude => MathF.Sqrt(x * x + y * y);
  public float sqrMagnitude => x * x + y * y;

  public Vector2 normalized
  {
    get
    {
      var m = magnitude;
      return m > 1e-6f ? this / m : zero;
    }
  }

  public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
  public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
  public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
  public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => a + (b - a) * t;
  public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
  {
    var sqr = vector.sqrMagnitude;
    if (sqr > maxLength * maxLength)
    {
      return vector.normalized * maxLength;
    }

    return vector;
  }

  public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
  public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
  public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);
  public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);
  public static Vector2 operator *(float d, Vector2 a) => a * d;
  public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);

  public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
  public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

  public float this[int index]
  {
    get => index switch { 0 => x, 1 => y, _ => throw new IndexOutOfRangeException() };
    set { switch (index) { case 0: x = value; break; case 1: y = value; break; default: throw new IndexOutOfRangeException(); } }
  }

  public override bool Equals(object? obj) => obj is Vector2 other && x == other.x && y == other.y;
  public override int GetHashCode() => HashCode.Combine(x, y);

  public override string ToString() => $"({x:F2}, {y:F2})";
}
