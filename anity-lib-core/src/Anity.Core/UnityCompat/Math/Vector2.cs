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
  public override string ToString() => $"({x:F2}, {y:F2})";
}
