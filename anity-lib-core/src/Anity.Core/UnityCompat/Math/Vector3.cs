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

  public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
}
