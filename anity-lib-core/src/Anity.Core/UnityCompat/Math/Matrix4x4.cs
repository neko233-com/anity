using System;
using System.Globalization;
using System.Text;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Math/MathScripting.h")]
[Bindings.NativeType(Header = "Runtime/Math/Matrix4x4.h")]
[NativeClass("Matrix4x4f")]
[Scripting.RequiredByNativeCode(Optional = true, GenerateProxy = true)]
[Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstruction]
public struct Matrix4x4 : IEquatable<Matrix4x4>, IFormattable
{
  [Bindings.NativeName("m_Data[0]")]
  public float m00;
  [Bindings.NativeName("m_Data[4]")]
  public float m01;
  [Bindings.NativeName("m_Data[8]")]
  public float m02;
  [Bindings.NativeName("m_Data[12]")]
  public float m03;
  [Bindings.NativeName("m_Data[1]")]
  public float m10;
  [Bindings.NativeName("m_Data[5]")]
  public float m11;
  [Bindings.NativeName("m_Data[9]")]
  public float m12;
  [Bindings.NativeName("m_Data[13]")]
  public float m13;
  [Bindings.NativeName("m_Data[2]")]
  public float m20;
  [Bindings.NativeName("m_Data[6]")]
  public float m21;
  [Bindings.NativeName("m_Data[10]")]
  public float m22;
  [Bindings.NativeName("m_Data[14]")]
  public float m23;
  [Bindings.NativeName("m_Data[3]")]
  public float m30;
  [Bindings.NativeName("m_Data[7]")]
  public float m31;
  [Bindings.NativeName("m_Data[11]")]
  public float m32;
  [Bindings.NativeName("m_Data[15]")]
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
        _ => throw new IndexOutOfRangeException("Invalid matrix index!")
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
        default: throw new IndexOutOfRangeException("Invalid matrix index!");
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

  public float determinant
  {
    get
    {
      if (AnityNative.TryMatrixDeterminant(ToNative(this), out float nativeDeterminant))
        return nativeDeterminant;
      return CalculateDeterminant();
    }
  }

  public Matrix4x4 inverse
  {
    get
    {
      if (AnityNative.TryMatrixInverse(ToNative(this), out AnityNative.TransformMatrix4x4 nativeInverse))
        return FromNative(nativeInverse);
      float det = determinant;
      if (det == 0f || float.IsNaN(det))
        return zero;
      return ScaleElements(Adjugate(), 1f / det);
    }
  }

  public Vector3 lossyScale
  {
    get
    {
      Vector3 result = new Vector3(
        new Vector3(m00, m10, m20).magnitude,
        new Vector3(m01, m11, m21).magnitude,
        new Vector3(m02, m12, m22).magnitude);
      if (determinant < 0f)
        result.x = -result.x;
      return result;
    }
  }

  public Quaternion rotation
  {
    get
    {
      if (AnityNative.TryMatrixExtractRotation(ToNative(this), out AnityNative.TransformQuaternion nativeRotation))
        return FromNative(nativeRotation);
      return ExtractClosestRotation();
    }
  }

  public FrustumPlanes decomposeProjection
  {
    get
    {
      if (AnityNative.TryMatrixDecomposeProjection(ToNative(this), out AnityNative.MatrixFrustumPlanes nativePlanes))
      {
        return new FrustumPlanes
        {
          left = nativePlanes.left, right = nativePlanes.right,
          bottom = nativePlanes.bottom, top = nativePlanes.top,
          zNear = nativePlanes.zNear, zFar = nativePlanes.zFar
        };
      }
      if (m33 == 0f)
      {
        float zNear = m23 / (m22 - 1f);
        float zFar = m23 / (m22 + 1f);
        return new FrustumPlanes
        {
          left = zNear * (m02 - 1f) / m00,
          right = zNear * (m02 + 1f) / m00,
          bottom = zNear * (m12 - 1f) / m11,
          top = zNear * (m12 + 1f) / m11,
          zNear = zNear,
          zFar = zFar
        };
      }

      return new FrustumPlanes
      {
        left = (-1f - m03) / m00,
        right = (1f - m03) / m00,
        bottom = (-1f - m13) / m11,
        top = (1f - m13) / m11,
        zNear = (1f + m23) / m22,
        zFar = (m23 - 1f) / m22
      };
    }
  }

  public Matrix4x4 transpose
  {
    get
    {
      if (AnityNative.TryMatrixTranspose(ToNative(this), out AnityNative.TransformMatrix4x4 nativeTranspose))
        return FromNative(nativeTranspose);
      return new Matrix4x4
      {
        m00 = m00, m01 = m10, m02 = m20, m03 = m30,
        m10 = m01, m11 = m11, m12 = m21, m13 = m31,
        m20 = m02, m21 = m12, m22 = m22, m23 = m32,
        m30 = m03, m31 = m13, m32 = m23, m33 = m33
      };
    }
  }

  public Vector3 MultiplyPoint(Vector3 point)
  {
    var result = this * new Vector4(point.x, point.y, point.z, 1f);
    float reciprocalW = 1f / result.w;
    return new Vector3(result.x * reciprocalW, result.y * reciprocalW, result.z * reciprocalW);
  }

  public Vector3 MultiplyPoint3x4(Vector3 point)
  {
    float x = m00 * point.x + m01 * point.y + m02 * point.z + m03;
    float y = m10 * point.x + m11 * point.y + m12 * point.z + m13;
    float z = m20 * point.x + m21 * point.y + m22 * point.z + m23;
    return new Vector3(x, y, z);
  }

  public Vector3 MultiplyVector(Vector3 vector)
  {
    float x = m00 * vector.x + m01 * vector.y + m02 * vector.z;
    float y = m10 * vector.x + m11 * vector.y + m12 * vector.z;
    float z = m20 * vector.x + m21 * vector.y + m22 * vector.z;
    return new Vector3(x, y, z);
  }

  public Vector3 GetPosition() => new Vector3(m03, m13, m23);

  public Vector4 GetColumn(int index)
  {
    return index switch
    {
      0 => new Vector4(m00, m10, m20, m30),
      1 => new Vector4(m01, m11, m21, m31),
      2 => new Vector4(m02, m12, m22, m32),
      3 => new Vector4(m03, m13, m23, m33),
      _ => throw new IndexOutOfRangeException("Invalid column index!")
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
      _ => throw new IndexOutOfRangeException("Invalid row index!")
    };
  }

  public void SetColumn(int index, Vector4 column)
  {
    switch (index)
    {
      case 0: m00 = column.x; m10 = column.y; m20 = column.z; m30 = column.w; break;
      case 1: m01 = column.x; m11 = column.y; m21 = column.z; m31 = column.w; break;
      case 2: m02 = column.x; m12 = column.y; m22 = column.z; m32 = column.w; break;
      case 3: m03 = column.x; m13 = column.y; m23 = column.z; m33 = column.w; break;
      default: this[0, index] = column.x; break;
    }
  }

  public void SetRow(int index, Vector4 row)
  {
    switch (index)
    {
      case 0: m00 = row.x; m01 = row.y; m02 = row.z; m03 = row.w; break;
      case 1: m10 = row.x; m11 = row.y; m12 = row.z; m13 = row.w; break;
      case 2: m20 = row.x; m21 = row.y; m22 = row.z; m23 = row.w; break;
      case 3: m30 = row.x; m31 = row.y; m32 = row.z; m33 = row.w; break;
      default: throw new IndexOutOfRangeException("Invalid matrix index!");
    }
  }

  public void SetTRS(Vector3 pos, Quaternion q, Vector3 s)
  {
    this = TRS(pos, q, s);
  }

  [Bindings.ThreadSafe]
  public bool ValidTRS()
  {
    if (AnityNative.TryMatrixValidTRS(ToNative(this), out bool nativeValid))
      return nativeValid;
    return m30 == 0f && m31 == 0f && m32 == 0f && m33 == 1f;
  }

  [Bindings.FreeFunction("MatrixScripting::TRS", IsThreadSafe = true)]
  public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
  {
    if (AnityNative.TryMatrixTRS(ToNative(pos), ToNative(q), ToNative(s), out AnityNative.TransformMatrix4x4 nativeMatrix))
      return FromNative(nativeMatrix);
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

  [Bindings.FreeFunction("MatrixScripting::Ortho", IsThreadSafe = true)]
  public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar)
  {
    if (AnityNative.TryMatrixOrtho(left, right, bottom, top, zNear, zFar, out AnityNative.TransformMatrix4x4 nativeMatrix))
      return FromNative(nativeMatrix);
    var result = identity;
    result.m00 = 2f / (right - left);
    result.m11 = 2f / (top - bottom);
    result.m22 = -2f / (zFar - zNear);
    result.m03 = -(right + left) / (right - left);
    result.m13 = -(top + bottom) / (top - bottom);
    result.m23 = -(zFar + zNear) / (zFar - zNear);
    return result;
  }

  [Bindings.FreeFunction("MatrixScripting::Perspective", IsThreadSafe = true)]
  public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
  {
    if (AnityNative.TryMatrixPerspective(fov, aspect, zNear, zFar, out AnityNative.TransformMatrix4x4 nativeMatrix))
      return FromNative(nativeMatrix);
    float radFov = fov * (MathF.PI / 180f);
    float tanHalfFov = MathF.Tan(radFov * 0.5f);
    var result = zero;
    result.m00 = 1f / (aspect * tanHalfFov);
    result.m11 = 1f / tanHalfFov;
    result.m22 = -(zFar + zNear) / (zFar - zNear);
    result.m23 = -(2f * zFar * zNear) / (zFar - zNear);
    result.m32 = -1f;
    return result;
  }

  [Bindings.FreeFunction("MatrixScripting::Frustum", IsThreadSafe = true)]
  public static Matrix4x4 Frustum(float left, float right, float bottom, float top, float zNear, float zFar)
  {
    if (AnityNative.TryMatrixFrustum(left, right, bottom, top, zNear, zFar, out AnityNative.TransformMatrix4x4 nativeMatrix))
      return FromNative(nativeMatrix);
    var result = zero;
    result.m00 = 2f * zNear / (right - left);
    result.m11 = 2f * zNear / (top - bottom);
    result.m02 = (right + left) / (right - left);
    result.m12 = (top + bottom) / (top - bottom);
    result.m22 = -(zFar + zNear) / (zFar - zNear);
    result.m23 = -(2f * zFar * zNear) / (zFar - zNear);
    result.m32 = -1f;
    return result;
  }

  public static Matrix4x4 Frustum(FrustumPlanes fp)
    => Frustum(fp.left, fp.right, fp.bottom, fp.top, fp.zNear, fp.zFar);

  [Bindings.FreeFunction("MatrixScripting::LookAt", IsThreadSafe = true)]
  public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up)
  {
    if (AnityNative.TryMatrixLookAt(ToNative(from), ToNative(to), ToNative(up), out AnityNative.TransformMatrix4x4 nativeMatrix))
      return FromNative(nativeMatrix);
    Vector3 forward = (to - from).normalized;
    if (forward.sqrMagnitude < 1e-6f || Vector3.Cross(up, forward).sqrMagnitude < 1e-6f)
      return Translate(from);
    return TRS(from, Quaternion.LookRotation(forward, up), Vector3.one);
  }

  [Bindings.FreeFunction("MatrixScripting::Determinant", IsThreadSafe = true)]
  public static float Determinant(Matrix4x4 m) => m.determinant;

  [Bindings.FreeFunction("MatrixScripting::Inverse", IsThreadSafe = true)]
  public static Matrix4x4 Inverse(Matrix4x4 m) => m.inverse;

  [Bindings.FreeFunction("MatrixScripting::Transpose", IsThreadSafe = true)]
  public static Matrix4x4 Transpose(Matrix4x4 m) => m.transpose;

  [Bindings.FreeFunction("MatrixScripting::Inverse3DAffine", IsThreadSafe = true)]
  public static bool Inverse3DAffine(Matrix4x4 input, ref Matrix4x4 result)
  {
    if (AnityNative.TryMatrixInverse3DAffine(
          ToNative(input), out AnityNative.TransformMatrix4x4 nativeResult, out bool nativeInvertible))
    {
      result = FromNative(nativeResult);
      return nativeInvertible;
    }
    float determinant3x3 = input.m00 * (input.m11 * input.m22 - input.m12 * input.m21)
                         - input.m01 * (input.m10 * input.m22 - input.m12 * input.m20)
                         + input.m02 * (input.m10 * input.m21 - input.m11 * input.m20);
    if (determinant3x3 == 0f || float.IsNaN(determinant3x3))
    {
      result = zero;
      return false;
    }

    float reciprocal = 1f / determinant3x3;
    Matrix4x4 inverse = identity;
    inverse.m00 = (input.m11 * input.m22 - input.m12 * input.m21) * reciprocal;
    inverse.m01 = (input.m02 * input.m21 - input.m01 * input.m22) * reciprocal;
    inverse.m02 = (input.m01 * input.m12 - input.m02 * input.m11) * reciprocal;
    inverse.m10 = (input.m12 * input.m20 - input.m10 * input.m22) * reciprocal;
    inverse.m11 = (input.m00 * input.m22 - input.m02 * input.m20) * reciprocal;
    inverse.m12 = (input.m02 * input.m10 - input.m00 * input.m12) * reciprocal;
    inverse.m20 = (input.m10 * input.m21 - input.m11 * input.m20) * reciprocal;
    inverse.m21 = (input.m01 * input.m20 - input.m00 * input.m21) * reciprocal;
    inverse.m22 = (input.m00 * input.m11 - input.m01 * input.m10) * reciprocal;
    inverse.m03 = -(inverse.m00 * input.m03 + inverse.m01 * input.m13 + inverse.m02 * input.m23);
    inverse.m13 = -(inverse.m10 * input.m03 + inverse.m11 * input.m13 + inverse.m12 * input.m23);
    inverse.m23 = -(inverse.m20 * input.m03 + inverse.m21 * input.m13 + inverse.m22 * input.m23);
    result = inverse;
    return true;
  }

  public Plane TransformPlane(Plane plane)
  {
    Matrix4x4 inverse = this.inverse;
    float x = inverse.m00 * plane.normal.x + inverse.m10 * plane.normal.y + inverse.m20 * plane.normal.z + inverse.m30 * plane.distance;
    float y = inverse.m01 * plane.normal.x + inverse.m11 * plane.normal.y + inverse.m21 * plane.normal.z + inverse.m31 * plane.distance;
    float z = inverse.m02 * plane.normal.x + inverse.m12 * plane.normal.y + inverse.m22 * plane.normal.z + inverse.m32 * plane.distance;
    float distance = inverse.m03 * plane.normal.x + inverse.m13 * plane.normal.y + inverse.m23 * plane.normal.z + inverse.m33 * plane.distance;
    return new Plane(new Vector3(x, y, z), distance);
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

  public static Vector4 operator *(Matrix4x4 lhs, Vector4 vector)
  {
    return new Vector4(
      lhs.m00 * vector.x + lhs.m01 * vector.y + lhs.m02 * vector.z + lhs.m03 * vector.w,
      lhs.m10 * vector.x + lhs.m11 * vector.y + lhs.m12 * vector.z + lhs.m13 * vector.w,
      lhs.m20 * vector.x + lhs.m21 * vector.y + lhs.m22 * vector.z + lhs.m23 * vector.w,
      lhs.m30 * vector.x + lhs.m31 * vector.y + lhs.m32 * vector.z + lhs.m33 * vector.w
    );
  }

  public static bool operator ==(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    return Approximately(lhs.GetColumn(0), rhs.GetColumn(0))
        && Approximately(lhs.GetColumn(1), rhs.GetColumn(1))
        && Approximately(lhs.GetColumn(2), rhs.GetColumn(2))
        && Approximately(lhs.GetColumn(3), rhs.GetColumn(3));
  }

  public static bool operator !=(Matrix4x4 lhs, Matrix4x4 rhs)
  {
    return !(lhs == rhs);
  }

  public override bool Equals(object? other)
  {
    return other is Matrix4x4 matrix && Equals(matrix);
  }

  public bool Equals(Matrix4x4 other)
  {
    return GetColumn(0).Equals(other.GetColumn(0))
        && GetColumn(1).Equals(other.GetColumn(1))
        && GetColumn(2).Equals(other.GetColumn(2))
        && GetColumn(3).Equals(other.GetColumn(3));
  }

  public override int GetHashCode()
  {
    return VectorHash(GetColumn(0))
         ^ (VectorHash(GetColumn(1)) << 2)
         ^ (VectorHash(GetColumn(2)) >> 2)
         ^ (VectorHash(GetColumn(3)) >> 1);
  }

  public override string ToString() => ToString("F5", CultureInfo.InvariantCulture.NumberFormat);

  public string ToString(string format) => ToString(format, CultureInfo.InvariantCulture.NumberFormat);

  public string ToString(string format, IFormatProvider formatProvider)
  {
    var builder = new StringBuilder();
    for (int row = 0; row < 4; row++)
    {
      builder.Append(FormatFloat(this[row, 0], format, formatProvider)).Append('\t');
      builder.Append(FormatFloat(this[row, 1], format, formatProvider)).Append('\t');
      builder.Append(FormatFloat(this[row, 2], format, formatProvider)).Append('\t');
      builder.Append(FormatFloat(this[row, 3], format, formatProvider)).Append('\n');
    }
    return builder.ToString();
  }

  private static string FormatFloat(float value, string format, IFormatProvider formatProvider)
  {
    if (!float.IsNaN(value) && !float.IsInfinity(value)
        && format.Length > 0 && (format[0] == 'F' || format[0] == 'f')
        && int.TryParse(format.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int decimals)
        && decimals >= 0 && decimals <= 15)
    {
      double rounded = Math.Round((double)value, decimals, MidpointRounding.AwayFromZero);
      return rounded.ToString(format, formatProvider);
    }
    return value.ToString(format, formatProvider);
  }

  private static bool Approximately(Vector4 left, Vector4 right)
  {
    float x = left.x - right.x;
    float y = left.y - right.y;
    float z = left.z - right.z;
    float w = left.w - right.w;
    return x * x + y * y + z * z + w * w < 9.99999944E-11f;
  }

  private static int VectorHash(Vector4 value)
    => value.x.GetHashCode()
     ^ (value.y.GetHashCode() << 2)
     ^ (value.z.GetHashCode() >> 2)
     ^ (value.w.GetHashCode() >> 1);

  private static AnityNative.TransformVector3 ToNative(Vector3 value)
    => new AnityNative.TransformVector3(value.x, value.y, value.z);

  private static AnityNative.TransformQuaternion ToNative(Quaternion value)
    => new AnityNative.TransformQuaternion(value.x, value.y, value.z, value.w);

  private static AnityNative.TransformMatrix4x4 ToNative(Matrix4x4 value)
    => new AnityNative.TransformMatrix4x4
    {
      m00 = value.m00, m01 = value.m01, m02 = value.m02, m03 = value.m03,
      m10 = value.m10, m11 = value.m11, m12 = value.m12, m13 = value.m13,
      m20 = value.m20, m21 = value.m21, m22 = value.m22, m23 = value.m23,
      m30 = value.m30, m31 = value.m31, m32 = value.m32, m33 = value.m33
    };

  private static Matrix4x4 FromNative(AnityNative.TransformMatrix4x4 value)
    => new Matrix4x4
    {
      m00 = value.m00, m01 = value.m01, m02 = value.m02, m03 = value.m03,
      m10 = value.m10, m11 = value.m11, m12 = value.m12, m13 = value.m13,
      m20 = value.m20, m21 = value.m21, m22 = value.m22, m23 = value.m23,
      m30 = value.m30, m31 = value.m31, m32 = value.m32, m33 = value.m33
    };

  private static Quaternion FromNative(AnityNative.TransformQuaternion value)
    => new Quaternion(value.x, value.y, value.z, value.w);

  private static Matrix4x4 ScaleElements(Matrix4x4 matrix, float scalar)
  {
    Matrix4x4 result = zero;
    for (int index = 0; index < 16; index++)
      result[index] = matrix[index] * scalar;
    return result;
  }

  private Quaternion ExtractClosestRotation()
  {
    if (m00 == 0f && m01 == 0f && m02 == 0f
        && m10 == 0f && m11 == 0f && m12 == 0f
        && m20 == 0f && m21 == 0f && m22 == 0f)
      return new Quaternion(-0.1592f, -0.3844f, -0.3844f, 0.8241f).normalized;

    float[,] k = new float[4, 4];
    float trace = m00 + m11 + m22;
    k[0, 0] = 2f * m00 - trace;
    k[1, 1] = 2f * m11 - trace;
    k[2, 2] = 2f * m22 - trace;
    k[3, 3] = trace;
    k[0, 1] = k[1, 0] = m01 + m10;
    k[0, 2] = k[2, 0] = m02 + m20;
    k[1, 2] = k[2, 1] = m12 + m21;
    k[0, 3] = k[3, 0] = m21 - m12;
    k[1, 3] = k[3, 1] = m02 - m20;
    k[2, 3] = k[3, 2] = m10 - m01;

    float[,] eigenvectors = new float[4, 4];
    for (int index = 0; index < 4; index++) eigenvectors[index, index] = 1f;
    for (int sweep = 0; sweep < 32; sweep++)
    {
      int p = 0;
      int q = 1;
      float largest = MathF.Abs(k[p, q]);
      for (int row = 0; row < 4; row++)
      {
        for (int column = row + 1; column < 4; column++)
        {
          float candidate = MathF.Abs(k[row, column]);
          if (candidate > largest) { largest = candidate; p = row; q = column; }
        }
      }
      if (largest <= 1e-7f) break;

      float angle = 0.5f * MathF.Atan2(2f * k[p, q], k[q, q] - k[p, p]);
      float sine = MathF.Sin(angle);
      float cosine = MathF.Cos(angle);
      for (int index = 0; index < 4; index++)
      {
        if (index == p || index == q) continue;
        float kip = k[index, p];
        float kiq = k[index, q];
        k[index, p] = k[p, index] = cosine * kip - sine * kiq;
        k[index, q] = k[q, index] = sine * kip + cosine * kiq;
      }
      float app = k[p, p];
      float aqq = k[q, q];
      float apq = k[p, q];
      k[p, p] = cosine * cosine * app - 2f * sine * cosine * apq + sine * sine * aqq;
      k[q, q] = sine * sine * app + 2f * sine * cosine * apq + cosine * cosine * aqq;
      k[p, q] = k[q, p] = 0f;

      for (int row = 0; row < 4; row++)
      {
        float vip = eigenvectors[row, p];
        float viq = eigenvectors[row, q];
        eigenvectors[row, p] = cosine * vip - sine * viq;
        eigenvectors[row, q] = sine * vip + cosine * viq;
      }
    }

    int largestEigenvalue = 0;
    for (int index = 1; index < 4; index++)
      if (k[index, index] > k[largestEigenvalue, largestEigenvalue]) largestEigenvalue = index;
    Quaternion rotation = new Quaternion(
      eigenvectors[0, largestEigenvalue], eigenvectors[1, largestEigenvalue],
      eigenvectors[2, largestEigenvalue], eigenvectors[3, largestEigenvalue]).normalized;
    float[] components = { rotation.x, rotation.y, rotation.z, rotation.w };
    int canonical = 0;
    for (int index = 1; index < 4; index++)
      if (MathF.Abs(components[index]) > MathF.Abs(components[canonical])) canonical = index;
    if (components[canonical] < 0f)
      rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
    return rotation;
  }

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
