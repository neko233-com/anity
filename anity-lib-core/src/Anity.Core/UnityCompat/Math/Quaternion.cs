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
}

internal static class QuaternionHelpers
{
  internal static System.Numerics.Quaternion FromEuler(float x, float y, float z)
  {
    return System.Numerics.Quaternion.CreateFromYawPitchRoll(y, x, z);
  }
}
