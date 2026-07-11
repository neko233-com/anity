using System;
using System.Numerics;

namespace UnityEngine;

public readonly struct Quaternion
{
  public readonly float x;
  public readonly float y;
  public readonly float z;
  public readonly float w;

  public Quaternion(float x, float y, float z, float w)
  {
    this.x = x; this.y = y; this.z = z; this.w = w;
  }

  public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

  public Vector3 eulerAngles
  {
    get
    {
      float sinp = 2f * (w * y - z * x);
      float pitch;
      if (MathF.Abs(sinp) >= 1f)
        pitch = Math.Sign(sinp) * (MathF.PI / 2f);
      else
        pitch = MathF.Asin(sinp);
      float siny_cosp = 2f * (w * z + x * y);
      float cosy_cosp = 1f - 2f * (y * y + z * z);
      float yaw = MathF.Atan2(siny_cosp, cosy_cosp);
      float sinx_cosp = 2f * (w * x + y * z);
      float cosx_cosp = 1f - 2f * (x * x + y * y);
      float roll = MathF.Atan2(sinx_cosp, cosx_cosp);
      return new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
    }
  }

  public static Quaternion Euler(float x, float y, float z)
  {
    var radX = x * (MathF.PI / 180f);
    var radY = y * (MathF.PI / 180f);
    var radZ = z * (MathF.PI / 180f);
    var q = QuaternionHelpers.FromEuler(radX, radY, radZ);
    return new Quaternion(q.X, q.Y, q.Z, q.W);
  }

  public static Quaternion Inverse(Quaternion rotation)
  {
    var norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
    if (norm < 1e-6f) return identity;
    var invNorm = 1f / norm;
    return new Quaternion(-rotation.x * invNorm, -rotation.y * invNorm, -rotation.z * invNorm, rotation.w * invNorm);
  }

  public static Quaternion operator *(Quaternion lhs, Quaternion rhs)
  {
    return new Quaternion(
      lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
      lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
      lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
      lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
  }

  public static Vector3 operator *(Quaternion rotation, Vector3 point)
  {
    var x = rotation.x * 2f;
    var y = rotation.y * 2f;
    var z = rotation.z * 2f;
    var xx = rotation.x * x;
    var yy = rotation.y * y;
    var zz = rotation.z * z;
    var xy = rotation.x * y;
    var xz = rotation.x * z;
    var yz = rotation.y * z;
    var wx = rotation.w * x;
    var wy = rotation.w * y;
    var wz = rotation.w * z;

    return new Vector3(
      (1f - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z,
      (xy + wz) * point.x + (1f - (xx + zz)) * point.y + (yz - wx) * point.z,
      (xz - wy) * point.x + (yz + wx) * point.y + (1f - (xx + yy)) * point.z);
  }

  public Quaternion normalized
  {
    get
    {
      var mag = MathF.Sqrt(x * x + y * y + z * z + w * w);
      if (mag < 1e-6f) return identity;
      var inv = 1f / mag;
      return new Quaternion(x * inv, y * inv, z * inv, w * inv);
    }
  }

  public static Quaternion AngleAxis(float angle, Vector3 axis)
  {
    if (axis.sqrMagnitude < 1e-6f) return identity;
    axis = axis.normalized;
    float rad = angle * (MathF.PI / 180f);
    float halfRad = rad * 0.5f;
    float s = MathF.Sin(halfRad);
    float c = MathF.Cos(halfRad);
    return new Quaternion(axis.x * s, axis.y * s, axis.z * s, c);
  }

  public static Quaternion LookRotation(Vector3 forward)
  {
    return LookRotation(forward, Vector3.up);
  }

  public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
  {
    forward = forward.normalized;
    if (forward.sqrMagnitude < 1e-6f) return identity;

    Vector3 right = Vector3.Cross(upwards, forward).normalized;
    if (right.sqrMagnitude < 1e-6f)
    {
      right = Vector3.Cross(Vector3.right, forward).normalized;
      if (right.sqrMagnitude < 1e-6f)
      {
        right = Vector3.Cross(Vector3.forward, forward).normalized;
      }
    }
    upwards = Vector3.Cross(forward, right).normalized;

    float m00 = right.x;
    float m01 = right.y;
    float m02 = right.z;
    float m10 = upwards.x;
    float m11 = upwards.y;
    float m12 = upwards.z;
    float m20 = forward.x;
    float m21 = forward.y;
    float m22 = forward.z;

    float trace = m00 + m11 + m22;
    float qx, qy, qz, qw;

    if (trace > 0f)
    {
      float s = MathF.Sqrt(trace + 1f) * 2f;
      qw = 0.25f * s;
      qx = (m21 - m12) / s;
      qy = (m02 - m20) / s;
      qz = (m10 - m01) / s;
    }
    else if (m00 > m11 && m00 > m22)
    {
      float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
      qw = (m21 - m12) / s;
      qx = 0.25f * s;
      qy = (m01 + m10) / s;
      qz = (m02 + m20) / s;
    }
    else if (m11 > m22)
    {
      float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
      qw = (m02 - m20) / s;
      qx = (m01 + m10) / s;
      qy = 0.25f * s;
      qz = (m12 + m21) / s;
    }
    else
    {
      float s = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
      qw = (m10 - m01) / s;
      qx = (m02 + m20) / s;
      qy = (m12 + m21) / s;
      qz = 0.25f * s;
    }

    return new Quaternion(qx, qy, qz, qw).normalized;
  }

  public static Quaternion FromToRotation(Vector3 fromDirection, Vector3 toDirection)
  {
    fromDirection = fromDirection.normalized;
    toDirection = toDirection.normalized;
    float d = Vector3.Dot(fromDirection, toDirection);
    if (d >= 1f - 1e-6f) return identity;
    if (d <= -1f + 1e-6f)
    {
      Vector3 right = Vector3.Cross(Vector3.right, fromDirection);
      if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.up, fromDirection);
      right = right.normalized;
      return AngleAxis(180f, right);
    }
    float f = MathF.Sqrt(2f * (1f + d));
    Vector3 cross = Vector3.Cross(fromDirection, toDirection);
    return new Quaternion(cross.x / f, cross.y / f, cross.z / f, f * 0.5f).normalized;
  }

  public static float Angle(Quaternion a, Quaternion b)
  {
    float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
    dot = Mathf.Clamp(MathF.Abs(dot), 0f, 1f);
    return Mathf.Acos(Mathf.Min(dot, 1f)) * 2f * Mathf.Rad2Deg;
  }

  public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
  {
    float num = Angle(from, to);
    if (num == 0f) return to;
    float t = Mathf.Min(1f, maxDegreesDelta / num);
    return SlerpUnclamped(from, to, t);
  }

  public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));

  public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
  {
    float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
    if (dot < 0f) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); dot = -dot; }
    if (dot > 0.9995f) return Lerp(a, b, t);
    float theta0 = MathF.Acos(dot);
    float theta = theta0 * t;
    float sinTheta = MathF.Sin(theta);
    float sinTheta0 = MathF.Sin(theta0);
    float s1 = sinTheta / sinTheta0;
    float s0 = MathF.Cos(theta) - dot * s1;
    return new Quaternion(
      s0 * a.x + s1 * b.x,
      s0 * a.y + s1 * b.y,
      s0 * a.z + s1 * b.z,
      s0 * a.w + s1 * b.w).normalized;
  }

  public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
  {
    t = Mathf.Clamp01(t);
    return LerpUnclamped(a, b, t).normalized;
  }

  public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t)
  {
    float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
    if (dot < 0f)
    {
      return new Quaternion(
        a.x + (-b.x - a.x) * t,
        a.y + (-b.y - a.y) * t,
        a.z + (-b.z - a.z) * t,
        a.w + (-b.w - a.w) * t);
    }
    return new Quaternion(
      a.x + (b.x - a.x) * t,
      a.y + (b.y - a.y) * t,
      a.z + (b.z - a.z) * t,
      a.w + (b.w - a.w) * t);
  }
}

internal static class QuaternionHelpers
{
  internal static System.Numerics.Quaternion FromEuler(float x, float y, float z)
  {
    return System.Numerics.Quaternion.CreateFromYawPitchRoll(y, x, z);
  }
}
