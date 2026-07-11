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
  public static Vector3 down => new Vector3(0f, -1f, 0f);
  public static Vector3 right => new Vector3(1f, 0f, 0f);
  public static Vector3 left => new Vector3(-1f, 0f, 0f);
  public static Vector3 forward => new Vector3(0f, 0f, 1f);
  public static Vector3 back => new Vector3(0f, 0f, -1f);

  public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
  public float sqrMagnitude => x * x + y * y + z * z;

  public Vector3 normalized
  {
    get
    {
      var m = magnitude;
      return m > 1e-6f ? this / m : zero;
    }
  }

  public void Normalize()
  {
    float m = magnitude;
    if (m > 1e-6f) { this /= m; }
    else { this = zero; }
  }

  public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
  public static float DistanceSquared(Vector3 a, Vector3 b) => (a - b).sqrMagnitude;
  public static Vector3 Abs(Vector3 v) => new(MathF.Abs(v.x), MathF.Abs(v.y), MathF.Abs(v.z));
  public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  public static float Angle(Vector3 from, Vector3 to)
  {
    float d = Dot(from.normalized, to.normalized);
    d = Mathf.Clamp(d, -1f, 1f);
    return Mathf.Acos(d) * Mathf.Rad2Deg;
  }
  public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

  public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
  {
    Vector3 toVector = target - current;
    float dist = toVector.magnitude;
    if (dist <= maxDistanceDelta || dist < 1e-6f) return target;
    return current + toVector / dist * maxDistanceDelta;
  }

  public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed)
  {
    return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
  }

  public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime)
  {
    return SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.deltaTime);
  }

  public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
  {
    smoothTime = Mathf.Max(0.0001f, smoothTime);
    float omega = 2f / smoothTime;
    float x = omega * deltaTime;
    float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
    float cx = current.x; float cy = current.y; float cz = current.z;
    float tx = target.x; float ty = target.y; float tz = target.z;
    float cvx = currentVelocity.x; float cvy = currentVelocity.y; float cvz = currentVelocity.z;
    float maxChange = maxSpeed * smoothTime;
    float dx = Mathf.Clamp(cx - tx, -maxChange, maxChange);
    float dy = Mathf.Clamp(cy - ty, -maxChange, maxChange);
    float dz = Mathf.Clamp(cz - tz, -maxChange, maxChange);
    float otx = tx; float oty = ty; float otz = tz;
    tx = cx - dx; ty = cy - dy; tz = cz - dz;
    float tempx = (cvx + omega * dx) * deltaTime;
    float tempy = (cvy + omega * dy) * deltaTime;
    float tempz = (cvz + omega * dz) * deltaTime;
    cvx = (cvx - omega * tempx) * exp;
    cvy = (cvy - omega * tempy) * exp;
    cvz = (cvz - omega * tempz) * exp;
    float outx = tx + (dx + tempx) * exp;
    float outy = ty + (dy + tempy) * exp;
    float outz = tz + (dz + tempz) * exp;
    if ((otx - cx) > 0f == outx > otx) { outx = otx; cvx = (outx - otx) / deltaTime; }
    if ((oty - cy) > 0f == outy > oty) { outy = oty; cvy = (outy - oty) / deltaTime; }
    if ((otz - cz) > 0f == outz > otz) { outz = otz; cvz = (outz - otz) / deltaTime; }
    currentVelocity = new Vector3(cvx, cvy, cvz);
    return new Vector3(outx, outy, outz);
  }

  public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
  public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
  public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
  public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
  public static Vector3 operator *(float d, Vector3 a) => a * d;
  public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);

  public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
  public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

  public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);

  public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new(
    a.x + (b.x - a.x) * Mathf.Clamp01(t),
    a.y + (b.y - a.y) * Mathf.Clamp01(t),
    a.z + (b.z - a.z) * Mathf.Clamp01(t));

  public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new(
    a.x + (b.x - a.x) * t,
    a.y + (b.y - a.y) * t,
    a.z + (b.z - a.z) * t);

  public static float SqrMagnitude(Vector3 a) => a.x * a.x + a.y * a.y + a.z * a.z;

  public static Vector3 Max(Vector3 a, Vector3 b) => new(MathF.Max(a.x, b.x), MathF.Max(a.y, b.y), MathF.Max(a.z, b.z));
  public static Vector3 Min(Vector3 a, Vector3 b) => new(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y), MathF.Min(a.z, b.z));

  public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
  {
    return vector - Project(vector, planeNormal);
  }

  public static Vector3 Project(Vector3 vector, Vector3 onNormal)
  {
    float sqrMag = Dot(onNormal, onNormal);
    if (sqrMag < 1e-15f) return zero;
    float dot = Dot(vector, onNormal);
    return onNormal * (dot / sqrMag);
  }

  public float this[int index]
  {
    get => index switch { 0 => x, 1 => y, 2 => z, _ => throw new IndexOutOfRangeException() };
    set
    {
      switch (index)
      {
        case 0: x = value; break;
        case 1: y = value; break;
        case 2: z = value; break;
        default: throw new IndexOutOfRangeException();
      }
    }
  }

  public override bool Equals(object? obj) => obj is Vector3 other && x == other.x && y == other.y && z == other.z;
  public override int GetHashCode() => HashCode.Combine(x, y, z);

  public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
}
