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
}

internal static class QuaternionHelpers
{
  internal static System.Numerics.Quaternion FromEuler(float x, float y, float z)
  {
    return System.Numerics.Quaternion.CreateFromYawPitchRoll(y, x, z);
  }
}
