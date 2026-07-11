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

  public float determinant => CalculateDeterminant(_m);

  public Matrix4x4 inverse
  {
    get
    {
      float det = determinant;
      if (MathF.Abs(det) < 1e-10f)
        return identity;
      return Adjugate(_m) * (1f / det);
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

  public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
  {
    var rot = RotationMatrix(q);
    var m = identity;
    m.m00 = rot.m00 * s.x; m.m01 = rot.m01 * s.y; m.m02 = rot.m02 * s.z; m.m03 = pos.x;
    m.m10 = rot.m10 * s.x; m.m11 = rot.m11 * s.y; m.m12 = rot.m12 * s.z; m.m13 = pos.y;
    m.m20 = rot.m20 * s.x; m.m21 = rot.m21 * s.y; m.m22 = rot.m22 * s.z; m.m23 = pos.z;
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
    var result = new float[16];
    result[0]  = 2f / (right - left);
    result[5]  = 2f / (top - bottom);
    result[10] = -2f / (far - near);
    result[12] = -(right + left) / (right - left);
    result[13] = -(top + bottom) / (top - bottom);
    result[14] = -(far + near) / (far - near);
    result[15] = 1f;
    return new Matrix4x4(result);
  }

  public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
  {
    float radFov = fov * (MathF.PI / 180f);
    float tanHalfFov = MathF.Tan(radFov * 0.5f);
    var result = new float[16];
    result[0]  = 1f / (aspect * tanHalfFov);
    result[5]  = 1f / tanHalfFov;
    result[10] = -(zFar + zNear) / (zFar - zNear);
    result[11] = -1f;
    result[14] = -(2f * zFar * zNear) / (zFar - zNear);
    return new Matrix4x4(result);
  }

  public static Matrix4x4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
  {
    Vector3 zAxis = (eye - target).normalized;
    Vector3 xAxis = Vector3.Cross(up, zAxis).normalized;
    Vector3 yAxis = Vector3.Cross(zAxis, xAxis);
    var result = new float[16];
    result[0]  = xAxis.x;
    result[4]  = xAxis.y;
    result[8]  = xAxis.z;
    result[12] = -Vector3.Dot(xAxis, eye);
    result[1]  = yAxis.x;
    result[5]  = yAxis.y;
    result[9]  = yAxis.z;
    result[13] = -Vector3.Dot(yAxis, eye);
    result[2]  = zAxis.x;
    result[6]  = zAxis.y;
    result[10] = zAxis.z;
    result[14] = -Vector3.Dot(zAxis, eye);
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
          sum += lhs[i, k] * rhs[k, j];
        result[i * 4 + j] = sum;
      }
    }
    return new Matrix4x4(result);
  }

  public static Matrix4x4 operator *(Matrix4x4 lhs, float s)
  {
    var result = new float[16];
    for (int i = 0; i < 16; i++)
      result[i] = lhs._m[i] * s;
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
      hash = hash * 31 + _m[i].GetHashCode();
    return hash;
  }

  public override string ToString() => $"[{_m[0]:F2}, {_m[5]:F2}, {_m[10]:F2}, {_m[15]:F2}]";

  public Vector4 GetColumn(int index)
  {
    return new Vector4(_m[index], _m[index + 4], _m[index + 8], _m[index + 12]);
  }

  public Vector4 GetRow(int index)
  {
    int i = index * 4;
    return new Vector4(_m[i], _m[i+1], _m[i+2], _m[i+3]);
  }

  public void SetColumn(int index, Vector4 v)
  {
    _m[index] = v.x;
    _m[index + 4] = v.y;
    _m[index + 8] = v.z;
    _m[index + 12] = v.w;
  }

  public void SetRow(int index, Vector4 v)
  {
    int i = index * 4;
    _m[i] = v.x;
    _m[i+1] = v.y;
    _m[i+2] = v.z;
    _m[i+3] = v.w;
  }

  private static float CalculateDeterminant(float[] m)
  {
    float a0 = m[0]*m[5] - m[1]*m[4];
    float a1 = m[0]*m[6] - m[2]*m[4];
    float a2 = m[0]*m[7] - m[3]*m[4];
    float a3 = m[1]*m[6] - m[2]*m[5];
    float a4 = m[1]*m[7] - m[3]*m[5];
    float a5 = m[2]*m[7] - m[3]*m[6];
    float b0 = m[8]*m[13] - m[9]*m[12];
    float b1 = m[8]*m[14] - m[10]*m[12];
    float b2 = m[8]*m[15] - m[11]*m[12];
    float b3 = m[9]*m[14] - m[10]*m[13];
    float b4 = m[9]*m[15] - m[11]*m[13];
    float b5 = m[10]*m[15] - m[11]*m[14];
    return a0*b5 - a1*b4 + a2*b3 + a3*b2 - a4*b1 + a5*b0;
  }

  private static Matrix4x4 Adjugate(float[] m)
  {
    float[] adj = new float[16];
    adj[0]  =  (m[5]*(m[10]*m[15]-m[11]*m[14]) - m[6]*(m[9]*m[15]-m[11]*m[13]) + m[7]*(m[9]*m[14]-m[10]*m[13]));
    adj[1]  = -(m[1]*(m[10]*m[15]-m[11]*m[14]) - m[2]*(m[9]*m[15]-m[11]*m[13]) + m[3]*(m[9]*m[14]-m[10]*m[13]));
    adj[2]  =  (m[1]*(m[6]*m[15]-m[7]*m[14]) - m[2]*(m[5]*m[15]-m[7]*m[13]) + m[3]*(m[5]*m[14]-m[6]*m[13]));
    adj[3]  = -(m[1]*(m[6]*m[11]-m[7]*m[10]) - m[2]*(m[5]*m[11]-m[7]*m[9]) + m[3]*(m[5]*m[10]-m[6]*m[9]));
    adj[4]  = -(m[4]*(m[10]*m[15]-m[11]*m[14]) - m[6]*(m[8]*m[15]-m[11]*m[12]) + m[7]*(m[8]*m[14]-m[10]*m[12]));
    adj[5]  =  (m[0]*(m[10]*m[15]-m[11]*m[14]) - m[2]*(m[8]*m[15]-m[11]*m[12]) + m[3]*(m[8]*m[14]-m[10]*m[12]));
    adj[6]  = -(m[0]*(m[6]*m[15]-m[7]*m[14]) - m[2]*(m[4]*m[15]-m[7]*m[12]) + m[3]*(m[4]*m[14]-m[6]*m[12]));
    adj[7]  =  (m[0]*(m[6]*m[11]-m[7]*m[10]) - m[2]*(m[4]*m[11]-m[7]*m[8]) + m[3]*(m[4]*m[10]-m[6]*m[8]));
    adj[8]  =  (m[4]*(m[9]*m[15]-m[11]*m[13]) - m[5]*(m[8]*m[15]-m[11]*m[12]) + m[7]*(m[8]*m[13]-m[9]*m[12]));
    adj[9]  = -(m[0]*(m[9]*m[15]-m[11]*m[13]) - m[1]*(m[8]*m[15]-m[11]*m[12]) + m[3]*(m[8]*m[13]-m[9]*m[12]));
    adj[10] =  (m[0]*(m[5]*m[15]-m[7]*m[13]) - m[1]*(m[4]*m[15]-m[7]*m[12]) + m[3]*(m[4]*m[13]-m[5]*m[12]));
    adj[11] = -(m[0]*(m[5]*m[11]-m[7]*m[9]) - m[1]*(m[4]*m[11]-m[7]*m[8]) + m[3]*(m[4]*m[9]-m[5]*m[8]));
    adj[12] = -(m[4]*(m[9]*m[14]-m[10]*m[13]) - m[5]*(m[8]*m[14]-m[10]*m[12]) + m[6]*(m[8]*m[13]-m[9]*m[12]));
    adj[13] =  (m[0]*(m[9]*m[14]-m[10]*m[13]) - m[1]*(m[8]*m[14]-m[10]*m[12]) + m[2]*(m[8]*m[13]-m[9]*m[12]));
    adj[14] = -(m[0]*(m[5]*m[14]-m[6]*m[13]) - m[1]*(m[4]*m[14]-m[6]*m[12]) + m[2]*(m[4]*m[13]-m[5]*m[12]));
    adj[15] =  (m[0]*(m[5]*m[10]-m[6]*m[9]) - m[1]*(m[4]*m[10]-m[6]*m[8]) + m[2]*(m[4]*m[9]-m[5]*m[8]));
    return new Matrix4x4(adj);
  }
}
