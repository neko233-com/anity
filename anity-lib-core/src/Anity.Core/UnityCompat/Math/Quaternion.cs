using System;
using System.Numerics;

namespace UnityEngine;

public readonly struct Quaternion
{
  public readonly float x;
  public readonly float y;
  public readonly float z;
  public readonly float w;

  private Quaternion(float x, float y, float z, float w)
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
}

internal static class QuaternionHelpers
{
  internal static System.Numerics.Quaternion FromEuler(float x, float y, float z)
  {
    return System.Numerics.Quaternion.CreateFromYawPitchRoll(y, x, z);
  }
}
