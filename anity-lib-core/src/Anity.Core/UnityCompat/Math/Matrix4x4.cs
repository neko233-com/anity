using System;

namespace UnityEngine;

public struct Matrix4x4
{
  private readonly float[] _m;

  private Matrix4x4(float[] values)
  {
    _m = values;
  }

  public static Matrix4x4 zero => new(new float[16]
  {
    0f,0f,0f,0f,
    0f,0f,0f,0f,
    0f,0f,0f,0f,
    0f,0f,0f,0f
  });

  public static Matrix4x4 identity => new(new float[16]
  {
    1f,0f,0f,0f,
    0f,1f,0f,0f,
    0f,0f,1f,0f,
    0f,0f,0f,1f
  });

  public float this[int index]
  {
    get => _m[index];
    set => _m[index] = value;
  }

  public float this[int row, int column]
  {
    get => _m[row * 4 + column];
    set => _m[row * 4 + column] = value;
  }

  public float m00 { get => _m[0]; set => _m[0] = value; }
  public float m01 { get => _m[1]; set => _m[1] = value; }
  public float m02 { get => _m[2]; set => _m[2] = value; }
  public float m03 { get => _m[3]; set => _m[3] = value; }
  public float m10 { get => _m[4]; set => _m[4] = value; }
  public float m11 { get => _m[5]; set => _m[5] = value; }
  public float m12 { get => _m[6]; set => _m[6] = value; }
  public float m13 { get => _m[7]; set => _m[7] = value; }
  public float m20 { get => _m[8]; set => _m[8] = value; }
  public float m21 { get => _m[9]; set => _m[9] = value; }
  public float m22 { get => _m[10]; set => _m[10] = value; }
  public float m23 { get => _m[11]; set => _m[11] = value; }
  public float m30 { get => _m[12]; set => _m[12] = value; }
  public float m31 { get => _m[13]; set => _m[13] = value; }
  public float m32 { get => _m[14]; set => _m[14] = value; }
  public float m33 { get => _m[15]; set => _m[15] = value; }

  public float determinant
  {
    get
    {
      return 1f;
    }
  }

  public Matrix4x4 inverse
  {
    get
    {
      return identity;
    }
  }

  public Matrix4x4 transpose
  {
    get
    {
      var result = new float[16];
      for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
          result[i * 4 + j] = _m[j * 4 + i];
      return new Matrix4x4(result);
    }
  }

  public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
  {
    _ = q;
    var m = identity;
    m[0, 3] = pos.x;
    m[1, 3] = pos.y;
    m[2, 3] = pos.z;
    m[0, 0] = s.x;
    m[1, 1] = s.y;
    m[2, 2] = s.z;
    return m;
  }

  public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float near, float far)
  {
    _ = left; _ = right; _ = bottom; _ = top; _ = near; _ = far;
    return identity;
  }

  public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
  {
    float tanHalfFov = (float)Math.Tan(fov * 0.5f);
    var result = new float[16];
    result[0]  = 1f / (aspect * tanHalfFov);
    result[5]  = 1f / tanHalfFov;
    result[10] = -(zFar + zNear) / (zFar - zNear);
    result[11] = -1f;
    result[14] = -(2f * zFar * zNear) / (zFar - zNear);
    return new Matrix4x4(result);
  }

  public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up)
  {
    Vector3 f = (to - from).normalized;
    Vector3 s = Vector3.Cross(up, f).normalized;
    Vector3 u = Vector3.Cross(f, s);
    var result = new float[16];
    result[0]  = s.x;
    result[4]  = s.y;
    result[8]  = s.z;
    result[12] = -Vector3.Dot(s, from);
    result[1]  = u.x;
    result[5]  = u.y;
    result[9]  = u.z;
    result[13] = -Vector3.Dot(u, from);
    result[2]  = f.x;
    result[6]  = f.y;
    result[10] = f.z;
    result[14] = -Vector3.Dot(f, from);
    result[15] = 1f;
    return new Matrix4x4(result);
  }

  public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    var result = new float[16];
    for (int i = 0; i < 4; i++)
    {
      for (int j = 0; j < 4; j++)
      {
        float sum = 0f;
        for (int k = 0; k < 4; k++)
        {
          sum += lhs[i, k] * rhs[k, j];
        }
        result[i * 4 + j] = sum;
      }
    }
    return new Matrix4x4(result);
  }

  public static Vector4 operator *(Matrix4x4 lhs, Vector4 v)
  {
    return new Vector4(
      lhs[0,0]*v.x + lhs[0,1]*v.y + lhs[0,2]*v.z + lhs[0,3]*v.w,
      lhs[1,0]*v.x + lhs[1,1]*v.y + lhs[1,2]*v.z + lhs[1,3]*v.w,
      lhs[2,0]*v.x + lhs[2,1]*v.y + lhs[2,2]*v.z + lhs[2,3]*v.w,
      lhs[3,0]*v.x + lhs[3,1]*v.y + lhs[3,2]*v.z + lhs[3,3]*v.w
    );
  }

  public static bool operator ==(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    for (int i = 0; i < 16; i++)
    {
      if (lhs._m[i] != rhs._m[i]) return false;
    }
    return true;
  }

  public static bool operator !=(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    return !(lhs == rhs);
  }

  public override bool Equals(object? obj)
  {
    return obj is Matrix4x4 other && this == other;
  }

  public override int GetHashCode()
  {
    int hash = 17;
    for (int i = 0; i < 16; i++)
    {
      hash = hash * 31 + _m[i].GetHashCode();
    }
    return hash;
  }

  public override string ToString()
  {
    return $"[{_m[0]}, {_m[5]}, {_m[10]}, {_m[15]}]";
  }
}
