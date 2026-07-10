using System;

namespace UnityEngine;

public struct Vector3
{
  public float x;
  public float y;
  public float z;

  public Vector3(float x, float y, float z)
  {
    this.x = x;
    this.y = y;
    this.z = z;
  }

  public static Vector3 zero => new Vector3(0f, 0f, 0f);
  public static Vector3 one => new Vector3(1f, 1f, 1f);
  public static Vector3 up => new Vector3(0f, 1f, 0f);
  public static Vector3 right => new Vector3(1f, 0f, 0f);
  public static Vector3 forward => new Vector3(0f, 0f, 1f);

  public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
  public Vector3 normalized
  {
    get
    {
      var m = magnitude;
      return m > 1e-6f ? this / m : zero;
    }
  }

  public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
  public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
  public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
  public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
  public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
  public static Vector3 operator *(float d, Vector3 a) => a * d;
  public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);

  public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
  public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

  public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new(
    a.x + (b.x - a.x) * t,
    a.y + (b.y - a.y) * t,
    a.z + (b.z - a.z) * t);

  public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new(
    a.x + (b.x - a.x) * t,
    a.y + (b.y - a.y) * t,
    a.z + (b.z - a.z) * t);

  public static float SqrMagnitude(Vector3 a) => a.x * a.x + a.y * a.y + a.z * a.z;
  public float sqrMagnitude => x * x + y * y + z * z;

  public static Vector3 Max(Vector3 a, Vector3 b) => new(MathF.Max(a.x, b.x), MathF.Max(a.y, b.y), MathF.Max(a.z, b.z));
  public static Vector3 Min(Vector3 a, Vector3 b) => new(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y), MathF.Min(a.z, b.z));

  public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
  {
    return vector - Project(vector, planeNormal);
  }

  public static Vector3 Project(Vector3 vector, Vector3 onNormal)
  {
    float dot = Dot(vector, onNormal);
    return new(onNormal.x * dot, onNormal.y * dot, onNormal.z * dot);
  }

  public override bool Equals(object? obj) => obj is Vector3 other && x == other.x && y == other.y && z == other.z;
  public override int GetHashCode() => HashCode.Combine(x, y, z);

  public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
}
