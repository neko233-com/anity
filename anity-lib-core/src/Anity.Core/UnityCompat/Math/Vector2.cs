using System;

namespace UnityEngine;

public struct Vector2 : IEquatable<Vector2>
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
  public static Vector2 positiveInfinity => new Vector2(float.PositiveInfinity, float.PositiveInfinity);
  public static Vector2 negativeInfinity => new Vector2(float.NegativeInfinity, float.NegativeInfinity);

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

  public void Normalize()
  {
    float m = magnitude;
    if (m > 1e-6f) { this /= m; }
    else { this = zero; }
  }

  public void Set(float newX, float newY)
  {
    x = newX; y = newY;
  }

  public void Scale(Vector2 scale)
  {
    x *= scale.x; y *= scale.y;
  }

  public static Vector2 Scale(Vector2 a, Vector2 b)
  {
    return new Vector2(a.x * b.x, a.y * b.y);
  }

  public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
  public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;

  public static float Angle(Vector2 from, Vector2 to)
  {
    float d = Dot(from.normalized, to.normalized);
    d = Mathf.Clamp(d, -1f, 1f);
    return Mathf.Acos(d) * Mathf.Rad2Deg;
  }

  public static float SignedAngle(Vector2 from, Vector2 to)
  {
    float unsignedAngle = Angle(from, to);
    float sign = MathF.Sign(from.x * to.y - from.y * to.x);
    return unsignedAngle * sign;
  }

  public static Vector2 Perpendicular(Vector2 inDirection)
  {
    return new Vector2(-inDirection.y, inDirection.x);
  }

  public static Vector2 Reflect(Vector2 inDirection, Vector2 inNormal)
  {
    float factor = -2f * Dot(inNormal, inDirection);
    return new Vector2(factor * inNormal.x + inDirection.x, factor * inNormal.y + inDirection.y);
  }

  public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
  {
    t = Mathf.Clamp01(t);
    return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
  }

  public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => a + (b - a) * t;

  public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
  {
    Vector2 toVector = target - current;
    float dist = toVector.magnitude;
    if (dist <= maxDistanceDelta || dist < 1e-6f) return target;
    return current + toVector / dist * maxDistanceDelta;
  }

  public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed)
  {
    return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
  }

  public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime)
  {
    return SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.deltaTime);
  }

  public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
  {
    smoothTime = Mathf.Max(0.0001f, smoothTime);
    float omega = 2f / smoothTime;
    float x = omega * deltaTime;
    float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
    float cx = current.x; float cy = current.y;
    float tx = target.x; float ty = target.y;
    float cvx = currentVelocity.x; float cvy = currentVelocity.y;
    float maxChange = maxSpeed * smoothTime;
    float dx = Mathf.Clamp(cx - tx, -maxChange, maxChange);
    float dy = Mathf.Clamp(cy - ty, -maxChange, maxChange);
    float otx = tx; float oty = ty;
    tx = cx - dx; ty = cy - dy;
    float tempx = (cvx + omega * dx) * deltaTime;
    float tempy = (cvy + omega * dy) * deltaTime;
    cvx = (cvx - omega * tempx) * exp;
    cvy = (cvy - omega * tempy) * exp;
    float outx = tx + (dx + tempx) * exp;
    float outy = ty + (dy + tempy) * exp;
    if ((otx - cx) > 0f == outx > otx) { outx = otx; cvx = (outx - otx) / deltaTime; }
    if ((oty - cy) > 0f == outy > oty) { outy = oty; cvy = (outy - oty) / deltaTime; }
    currentVelocity = new Vector2(cvx, cvy);
    return new Vector2(outx, outy);
  }

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
  public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
  public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);
  public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.x / b.x, a.y / b.y);

  public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
  public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

  public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);

  public float this[int index]
  {
    get => index switch { 0 => x, 1 => y, _ => throw new IndexOutOfRangeException() };
    set { switch (index) { case 0: x = value; break; case 1: y = value; break; default: throw new IndexOutOfRangeException(); } }
  }

  public bool Equals(Vector2 other) => x == other.x && y == other.y;
  public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(x, y);
  public override string ToString() => $"({x:F2}, {y:F2})";
}
