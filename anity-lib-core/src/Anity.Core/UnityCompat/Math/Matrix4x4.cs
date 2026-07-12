using System;

namespace UnityEngine;

public struct Matrix4x4 : IEquatable<Matrix4x4>
{
  public float m00;
  public float m01;
  public float m02;
  public float m03;
  public float m10;
  public float m11;
  public float m12;
  public float m13;
  public float m20;
  public float m21;
  public float m22;
  public float m23;
  public float m30;
  public float m31;
  public float m32;
  public float m33;

  public Matrix4x4(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
  {
    m00 = column0.x; m01 = column1.x; m02 = column2.x; m03 = column3.x;
    m10 = column0.y; m11 = column1.y; m12 = column2.y; m13 = column3.y;
    m20 = column0.z; m21 = column1.z; m22 = column2.z; m23 = column3.z;
    m30 = column0.w; m31 = column1.w; m32 = column2.w; m33 = column3.w;
  }

  public float this[int index]
  {
    get
    {
      return index switch
      {
        0 => m00, 1 => m10, 2 => m20, 3 => m30,
        4 => m01, 5 => m11, 6 => m21, 7 => m31,
        8 => m02, 9 => m12, 10 => m22, 11 => m32,
        12 => m03, 13 => m13, 14 => m23, 15 => m33,
        _ => throw new IndexOutOfRangeException()
      };
    }
    set
    {
      switch (index)
      {
        case 0: m00 = value; break; case 1: m10 = value; break; case 2: m20 = value; break; case 3: m30 = value; break;
        case 4: m01 = value; break; case 5: m11 = value; break; case 6: m21 = value; break; case 7: m31 = value; break;
        case 8: m02 = value; break; case 9: m12 = value; break; case 10: m22 = value; break; case 11: m32 = value; break;
        case 12: m03 = value; break; case 13: m13 = value; break; case 14: m23 = value; break; case 15: m33 = value; break;
        default: throw new IndexOutOfRangeException();
      }
    }
  }

  public float this[int row, int column]
  {
    get => this[row + column * 4];
    set => this[row + column * 4] = value;
  }

  public static Matrix4x4 zero => new Matrix4x4();

  public static Matrix4x4 identity => new Matrix4x4
  {
    m00 = 1f, m11 = 1f, m22 = 1f, m33 = 1f
  };

  public bool isIdentity
  {
    get
    {
      return m00 == 1f && m01 == 0f && m02 == 0f && m03 == 0f
          && m10 == 0f && m11 == 1f && m12 == 0f && m13 == 0f
          && m20 == 0f && m21 == 0f && m22 == 1f && m23 == 0f
          && m30 == 0f && m31 == 0f && m32 == 0f && m33 == 1f;
    }
  }

  public float determinant => CalculateDeterminant();

  public Matrix4x4 inverse
  {
    get
    {
      float det = determinant;
      if (MathF.Abs(det) < 1e-10f)
        return identity;
      return Adjugate() * (1f / det);
    }
  }

  public Matrix4x4 transpose
  {
    get
    {
      return new Matrix4x4
      {
        m00 = m00, m01 = m10, m02 = m20, m03 = m30,
        m10 = m01, m11 = m11, m12 = m21, m13 = m31,
        m20 = m02, m21 = m12, m22 = m22, m23 = m32,
        m30 = m03, m31 = m13, m32 = m23, m33 = m33
      };
    }
  }

  public Vector3 MultiplyPoint(Vector3 p)
  {
    var v = this * new Vector4(p.x, p.y, p.z, 1f);
    float w = v.w;
    if (MathF.Abs(w) > 1e-10f)
      return new Vector3(v.x / w, v.y / w, v.z / w);
    return new Vector3(v.x, v.y, v.z);
  }

  public Vector3 MultiplyPoint3x4(Vector3 p)
  {
    float x = m00 * p.x + m01 * p.y + m02 * p.z + m03;
    float y = m10 * p.x + m11 * p.y + m12 * p.z + m13;
    float z = m20 * p.x + m21 * p.y + m22 * p.z + m23;
    return new Vector3(x, y, z);
  }

  public Vector3 MultiplyVector(Vector3 v)
  {
    float x = m00 * v.x + m01 * v.y + m02 * v.z;
    float y = m10 * v.x + m11 * v.y + m12 * v.z;
    float z = m20 * v.x + m21 * v.y + m22 * v.z;
    return new Vector3(x, y, z);
  }

  public Vector4 GetColumn(int index)
  {
    return index switch
    {
      0 => new Vector4(m00, m10, m20, m30),
      1 => new Vector4(m01, m11, m21, m31),
      2 => new Vector4(m02, m12, m22, m32),
      3 => new Vector4(m03, m13, m23, m33),
      _ => throw new IndexOutOfRangeException()
    };
  }

  public Vector4 GetRow(int index)
  {
    return index switch
    {
      0 => new Vector4(m00, m01, m02, m03),
      1 => new Vector4(m10, m11, m12, m13),
      2 => new Vector4(m20, m21, m22, m23),
      3 => new Vector4(m30, m31, m32, m33),
      _ => throw new IndexOutOfRangeException()
    };
  }

  public void SetColumn(int index, Vector4 v)
  {
    switch (index)
    {
      case 0: m00 = v.x; m10 = v.y; m20 = v.z; m30 = v.w; break;
      case 1: m01 = v.x; m11 = v.y; m21 = v.z; m31 = v.w; break;
      case 2: m02 = v.x; m12 = v.y; m22 = v.z; m32 = v.w; break;
      case 3: m03 = v.x; m13 = v.y; m23 = v.z; m33 = v.w; break;
      default: throw new IndexOutOfRangeException();
    }
  }

  public void SetRow(int index, Vector4 v)
  {
    switch (index)
    {
      case 0: m00 = v.x; m01 = v.y; m02 = v.z; m03 = v.w; break;
      case 1: m10 = v.x; m11 = v.y; m12 = v.z; m13 = v.w; break;
      case 2: m20 = v.x; m21 = v.y; m22 = v.z; m23 = v.w; break;
      case 3: m30 = v.x; m31 = v.y; m32 = v.z; m33 = v.w; break;
      default: throw new IndexOutOfRangeException();
    }
  }

  public void SetTRS(Vector3 pos, Quaternion q, Vector3 s)
  {
    this = TRS(pos, q, s);
  }

  public bool ValidTRS()
  {
    Vector3 col0 = new Vector3(m00, m10, m20);
    Vector3 col1 = new Vector3(m01, m11, m21);
    Vector3 col2 = new Vector3(m02, m12, m22);
    float d0 = Vector3.Dot(col0, col0);
    float d1 = Vector3.Dot(col1, col1);
    float d2 = Vector3.Dot(col2, col2);
    return Mathf.Approximately(d0, d1) && Mathf.Approximately(d1, d2) && Mathf.Abs(m33 - 1f) < 1e-6f;
  }

  public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
  {
    var rot = RotationMatrix(q);
    var m = identity;
    m.m00 = rot.m00 * s.x; m.m01 = rot.m01 * s.y; m.m02 = rot.m02 * s.z; m.m03 = pos.x;
    m.m10 = rot.m10 * s.x; m.m11 = rot.m11 * s.y; m.m12 = rot.m12 * s.z; m.m13 = pos.y;
    m.m20 = rot.m20 * s.x; m.m21 = rot.m21 * s.y; m.m22 = rot.m22 * s.z; m.m23 = pos.z;
    return m;
  }

  public static Matrix4x4 Translate(Vector3 vector)
  {
    var m = identity;
    m.m03 = vector.x;
    m.m13 = vector.y;
    m.m23 = vector.z;
    return m;
  }

  public static Matrix4x4 Rotate(Quaternion q)
  {
    return RotationMatrix(q);
  }

  public static Matrix4x4 Scale(Vector3 vector)
  {
    var m = identity;
    m.m00 = vector.x;
    m.m11 = vector.y;
    m.m22 = vector.z;
    return m;
  }

  private static Matrix4x4 RotationMatrix(Quaternion q)
  {
    float x = q.x, y = q.y, z = q.z, w = q.w;
    float x2 = x + x, y2 = y + y, z2 = z + z;
    float xx = x * x2, xy = x * y2, xz = x * z2;
    float yy = y * y2, yz = y * z2, zz = z * z2;
    float wx = w * x2, wy = w * y2, wz = w * z2;

    var m = identity;
    m.m00 = 1f - (yy + zz); m.m01 = xy - wz;         m.m02 = xz + wy;
    m.m10 = xy + wz;         m.m11 = 1f - (xx + zz); m.m12 = yz - wx;
    m.m20 = xz - wy;         m.m21 = yz + wx;         m.m22 = 1f - (xx + yy);
    return m;
  }

  public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float near, float far)
  {
    var result = identity;
    result.m00 = 2f / (right - left);
    result.m11 = 2f / (top - bottom);
    result.m22 = -2f / (far - near);
    result.m03 = -(right + left) / (right - left);
    result.m13 = -(top + bottom) / (top - bottom);
    result.m23 = -(far + near) / (far - near);
    return result;
  }

  public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
  {
    float radFov = fov * (MathF.PI / 180f);
    float tanHalfFov = MathF.Tan(radFov * 0.5f);
    var result = zero;
    result.m00 = 1f / (aspect * tanHalfFov);
    result.m11 = 1f / tanHalfFov;
    result.m22 = -(zFar + zNear) / (zFar - zNear);
    result.m23 = -1f;
    result.m32 = -(2f * zFar * zNear) / (zFar - zNear);
    return result;
  }

  public static Matrix4x4 Frustum(float left, float right, float bottom, float top, float zNear, float zFar)
  {
    var result = zero;
    result.m00 = 2f * zNear / (right - left);
    result.m11 = 2f * zNear / (top - bottom);
    result.m02 = (right + left) / (right - left);
    result.m12 = (top + bottom) / (top - bottom);
    result.m22 = -(zFar + zNear) / (zFar - zNear);
    result.m23 = -1f;
    result.m32 = -(2f * zFar * zNear) / (zFar - zNear);
    return result;
  }

  public static Matrix4x4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
  {
    Vector3 zAxis = (eye - target).normalized;
    Vector3 xAxis = Vector3.Cross(up, zAxis).normalized;
    Vector3 yAxis = Vector3.Cross(zAxis, xAxis);
    var result = identity;
    result.m00 = xAxis.x;
    result.m10 = xAxis.y;
    result.m20 = xAxis.z;
    result.m03 = -Vector3.Dot(xAxis, eye);
    result.m01 = yAxis.x;
    result.m11 = yAxis.y;
    result.m21 = yAxis.z;
    result.m13 = -Vector3.Dot(yAxis, eye);
    result.m02 = zAxis.x;
    result.m12 = zAxis.y;
    result.m22 = zAxis.z;
    result.m23 = -Vector3.Dot(zAxis, eye);
    return result;
  }

  public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    var result = zero;
    for (int i = 0; i < 4; i++)
    {
      for (int j = 0; j < 4; j++)
      {
        float sum = 0f;
        for (int k = 0; k < 4; k++)
          sum += lhs[i, k] * rhs[k, j];
        result[i, j] = sum;
      }
    }
    return result;
  }

  public static Matrix4x4 operator *(Matrix4x4 lhs, float s)
  {
    var result = new Matrix4x4();
    for (int i = 0; i < 16; i++)
      result[i] = lhs[i] * s;
    return result;
  }

  public static Vector4 operator *(Matrix4x4 lhs, Vector4 v)
  {
    return new Vector4(
      lhs.m00 * v.x + lhs.m01 * v.y + lhs.m02 * v.z + lhs.m03 * v.w,
      lhs.m10 * v.x + lhs.m11 * v.y + lhs.m12 * v.z + lhs.m13 * v.w,
      lhs.m20 * v.x + lhs.m21 * v.y + lhs.m22 * v.z + lhs.m23 * v.w,
      lhs.m30 * v.x + lhs.m31 * v.y + lhs.m32 * v.z + lhs.m33 * v.w
    );
  }

  public static bool operator ==(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    for (int i = 0; i < 16; i++)
    {
      if (lhs[i] != rhs[i]) return false;
    }
    return true;
  }

  public static bool operator !=(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    return !(lhs == rhs);
  }

  public override bool Equals(object? obj)
  {
    return obj is Matrix4x4 other && Equals(other);
  }

  public bool Equals(Matrix4x4 other)
  {
    return this == other;
  }

  public override int GetHashCode()
  {
    var hash = new HashCode();
    for (int i = 0; i < 16; i++)
      hash.Add(this[i]);
    return hash.ToHashCode();
  }

  public override string ToString() => $"[{m00:F2}, {m11:F2}, {m22:F2}, {m33:F2}]";

  private float CalculateDeterminant()
  {
    float a0 = m00 * m11 - m01 * m10;
    float a1 = m00 * m12 - m02 * m10;
    float a2 = m00 * m13 - m03 * m10;
    float a3 = m01 * m12 - m02 * m11;
    float a4 = m01 * m13 - m03 * m11;
    float a5 = m02 * m13 - m03 * m12;
    float b0 = m20 * m31 - m21 * m30;
    float b1 = m20 * m32 - m22 * m30;
    float b2 = m20 * m33 - m23 * m30;
    float b3 = m21 * m32 - m22 * m31;
    float b4 = m21 * m33 - m23 * m31;
    float b5 = m22 * m33 - m23 * m32;
    return a0 * b5 - a1 * b4 + a2 * b3 + a3 * b2 - a4 * b1 + a5 * b0;
  }

  private Matrix4x4 Adjugate()
  {
    var adj = new Matrix4x4();
    adj.m00 = (m11 * (m22 * m33 - m23 * m32) - m12 * (m21 * m33 - m23 * m31) + m13 * (m21 * m32 - m22 * m31));
    adj.m01 = -(m01 * (m22 * m33 - m23 * m32) - m02 * (m21 * m33 - m23 * m31) + m03 * (m21 * m32 - m22 * m31));
    adj.m02 = (m01 * (m12 * m33 - m13 * m32) - m02 * (m11 * m33 - m13 * m31) + m03 * (m11 * m32 - m12 * m31));
    adj.m03 = -(m01 * (m12 * m23 - m13 * m22) - m02 * (m11 * m23 - m13 * m21) + m03 * (m11 * m22 - m12 * m21));
    adj.m10 = -(m10 * (m22 * m33 - m23 * m32) - m12 * (m20 * m33 - m23 * m30) + m13 * (m20 * m32 - m22 * m30));
    adj.m11 = (m00 * (m22 * m33 - m23 * m32) - m02 * (m20 * m33 - m23 * m30) + m03 * (m20 * m32 - m22 * m30));
    adj.m12 = -(m00 * (m12 * m33 - m13 * m32) - m02 * (m10 * m33 - m13 * m30) + m03 * (m10 * m32 - m12 * m30));
    adj.m13 = (m00 * (m12 * m23 - m13 * m22) - m02 * (m10 * m23 - m13 * m20) + m03 * (m10 * m22 - m12 * m20));
    adj.m20 = (m10 * (m21 * m33 - m23 * m31) - m11 * (m20 * m33 - m23 * m30) + m13 * (m20 * m31 - m21 * m30));
    adj.m21 = -(m00 * (m21 * m33 - m23 * m31) - m01 * (m20 * m33 - m23 * m30) + m03 * (m20 * m31 - m21 * m30));
    adj.m22 = (m00 * (m11 * m33 - m13 * m31) - m01 * (m10 * m33 - m13 * m30) + m03 * (m10 * m31 - m11 * m30));
    adj.m23 = -(m00 * (m11 * m23 - m13 * m21) - m01 * (m10 * m23 - m13 * m20) + m03 * (m10 * m21 - m11 * m20));
    adj.m30 = -(m10 * (m21 * m32 - m22 * m31) - m11 * (m20 * m32 - m22 * m30) + m12 * (m20 * m31 - m21 * m30));
    adj.m31 = (m00 * (m21 * m32 - m22 * m31) - m01 * (m20 * m32 - m22 * m30) + m02 * (m20 * m31 - m21 * m30));
    adj.m32 = -(m00 * (m11 * m32 - m12 * m31) - m01 * (m10 * m32 - m12 * m30) + m02 * (m10 * m31 - m11 * m30));
    adj.m33 = (m00 * (m11 * m22 - m12 * m21) - m01 * (m10 * m22 - m12 * m20) + m02 * (m10 * m21 - m11 * m20));
    return adj;
  }
}
