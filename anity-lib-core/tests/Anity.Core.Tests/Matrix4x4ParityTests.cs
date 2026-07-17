using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

public sealed class Matrix4x4ParityTests
{
    private const float Tolerance = 0.002f;

    [Fact]
    public void PublicSurfaceCarriesUnityNativeMetadataAndFrustumFields()
    {
        Type type = typeof(Matrix4x4);
        string[] interfaces = type.GetInterfaces().Select(value => value.FullName!).OrderBy(value => value).ToArray();
        Assert.Contains("System.IEquatable`1[[UnityEngine.Matrix4x4, Anity.Core, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null]]", interfaces);
        Assert.Contains("System.IFormattable", interfaces);
        Assert.Contains(type.GetCustomAttributes(false), attribute => attribute.GetType().FullName == "UnityEngine.Bindings.NativeTypeAttribute");
        Assert.Contains(type.GetCustomAttributes(false), attribute => attribute.GetType().FullName == "UnityEngine.Scripting.RequiredByNativeCodeAttribute");
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            Assert.Contains(field.GetCustomAttributes(false), attribute => attribute.GetType().FullName == "UnityEngine.Bindings.NativeNameAttribute");

        Assert.True(typeof(FrustumPlanes).IsValueType);
        Assert.Equal(new[] { "bottom", "left", "right", "top", "zFar", "zNear" },
            typeof(FrustumPlanes).GetFields().Select(field => field.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void FrustumMatchesUnity2022Probe()
    {
        AssertMatrix(new Matrix4x4
        {
            m00 = 0.12f, m02 = 0.2f,
            m11 = 0.12f, m12 = 0.6f,
            m22 = -1.003005f, m23 = -0.600901f,
            m32 = -1f
        }, Matrix4x4.Frustum(-2, 3, -1, 4, 0.3f, 200f));
    }

    [Fact]
    public void PerspectiveMatchesUnity2022Probe()
    {
        AssertMatrix(new Matrix4x4
        {
            m00 = 0.917917f, m11 = 1.631852f,
            m22 = -1.003005f, m23 = -0.600901f,
            m32 = -1f
        }, Matrix4x4.Perspective(63, 16f / 9f, 0.3f, 200f));
    }

    [Fact]
    public void OrthoMatchesUnity2022Probe()
    {
        AssertMatrix(new Matrix4x4
        {
            m00 = 0.4f, m03 = -0.2f,
            m11 = 0.4f, m13 = -0.6f,
            m22 = -0.010015f, m23 = -1.003005f,
            m33 = 1f
        }, Matrix4x4.Ortho(-2, 3, -1, 4, 0.3f, 200f));
    }

    [Fact]
    public void LookAtProducesObjectPoseRatherThanViewMatrix()
    {
        AssertMatrix(new Matrix4x4
        {
            m00 = 0.316228f, m01 = -0.652929f, m02 = -0.688247f, m03 = 2f,
            m10 = 0f, m11 = 0.725476f, m12 = -0.688247f, m13 = 3f,
            m20 = 0.948683f, m21 = 0.217643f, m22 = 0.229416f, m23 = 4f,
            m33 = 1f
        }, Matrix4x4.LookAt(new Vector3(2, 3, 4), new Vector3(-1, 0, 5), Vector3.up));
    }

    [Fact]
    public void LookAtDegenerateDirectionOrUpReturnsTranslatedIdentity()
    {
        Matrix4x4 expected = Matrix4x4.Translate(new Vector3(2, 3, 4));
        AssertMatrix(expected, Matrix4x4.LookAt(new Vector3(2, 3, 4), new Vector3(2, 3, 4), Vector3.up));
        AssertMatrix(expected, Matrix4x4.LookAt(new Vector3(2, 3, 4), new Vector3(3, 3, 4), Vector3.right));
        AssertMatrix(expected, Matrix4x4.LookAt(new Vector3(2, 3, 4), new Vector3(3, 3, 4), Vector3.zero));
    }

    [Fact]
    public void RotationExtractsClosestProperRotationForSignedAndZeroScales()
    {
        Quaternion q = Quaternion.Euler(17, 41, -13);
        AssertQuaternion(new Quaternion(0.0983f, 0.3598f, -0.1563f, 0.9146f), Matrix4x4.TRS(Vector3.zero, q, new Vector3(2, 3, 4)).rotation);
        AssertQuaternion(new Quaternion(0.0983f, 0.3598f, -0.1563f, 0.9146f), Matrix4x4.TRS(Vector3.zero, q, new Vector3(-2, 3, 4)).rotation);
        AssertQuaternion(new Quaternion(0.3598f, -0.0983f, 0.9146f, 0.1563f), Matrix4x4.TRS(Vector3.zero, q, new Vector3(-2, -3, 4)).rotation);
        AssertQuaternion(new Quaternion(0.0983f, 0.3598f, -0.1563f, 0.9146f), Matrix4x4.TRS(Vector3.zero, q, new Vector3(0, 3, 4)).rotation);
    }

    [Fact]
    public void RotationUsesPolarClosestRotationForShearAndProjection()
    {
        Matrix4x4 positive = PositiveMatrix();
        Matrix4x4 shear = positive * Matrix4x4.TRS(new Vector3(1.25f, -0.75f, 2.5f), Quaternion.Euler(-22, 35, 61), new Vector3(1.2f, 0.8f, 2.1f));
        AssertQuaternion(new Quaternion(0.2882f, 0.5098f, 0.4179f, 0.6946f), shear.rotation);
        AssertQuaternion(new Quaternion(0.9032f, -0.3011f, 0f, -0.3058f), Matrix4x4.Frustum(-2, 3, -1, 4, 0.3f, 200f).rotation);
    }

    [Fact]
    public void ZeroMatrixRotationUsesUnityDeterministicFallback()
    {
        AssertQuaternion(new Quaternion(-0.1592f, -0.3844f, -0.3844f, 0.8241f), Matrix4x4.zero.rotation);
    }

    [Fact]
    public void ValidTRSOnlyValidatesExactAffineBottomRow()
    {
        Matrix4x4 nan = Matrix4x4.identity;
        nan.m00 = float.NaN;
        Assert.True(nan.ValidTRS());
        Assert.True(PositiveMatrix().ValidTRS());
        Matrix4x4 shear = PositiveMatrix() * Matrix4x4.TRS(Vector3.one, Quaternion.Euler(2, 7, 11), new Vector3(1, 2, 3));
        Assert.True(shear.ValidTRS());
        Assert.False(Matrix4x4.Perspective(63, 1.5f, 0.3f, 200f).ValidTRS());

        Matrix4x4 bottom = Matrix4x4.identity;
        bottom.m30 = 0.00000001f;
        Assert.False(bottom.ValidTRS());
        bottom = Matrix4x4.identity;
        bottom.m33 = 2f;
        Assert.False(bottom.ValidTRS());
    }

    [Fact]
    public void StaticDeterminantInverseTransposeAndPositionMatchUnityProbe()
    {
        Matrix4x4 matrix = PositiveMatrix();
        Assert.Equal(24f, Matrix4x4.Determinant(matrix), 4);
        Assert.Equal(matrix.determinant, Matrix4x4.Determinant(matrix));
        AssertVector(new Vector3(3.5f, -2.25f, 7.75f), matrix.GetPosition());
        AssertMatrix(new Matrix4x4
        {
            m00 = 0.346109f, m01 = -0.107561f, m02 = -0.344441f, m03 = 1.216021f,
            m10 = 0.118890f, m11 = 0.310598f, m12 = 0.022473f, m13 = 0.108565f,
            m20 = 0.156848f, m21 = -0.073093f, m22 = 0.180433f, m23 = -2.111784f,
            m33 = 1f
        }, Matrix4x4.Inverse(matrix));
        AssertMatrix(matrix.transpose, Matrix4x4.Transpose(matrix));
    }

    [Fact]
    public void Inverse3DAffineMatchesAffineProjectionAndSingularUnityCases()
    {
        Matrix4x4 result = Matrix4x4.identity;
        Assert.True(Matrix4x4.Inverse3DAffine(PositiveMatrix(), ref result));
        AssertMatrix(PositiveMatrix().inverse, result);

        Matrix4x4 singular = Matrix4x4.TRS(Vector3.one, Quaternion.Euler(17, 41, -13), new Vector3(0, 3, 4));
        result = Matrix4x4.identity;
        Assert.False(Matrix4x4.Inverse3DAffine(singular, ref result));
        AssertMatrix(Matrix4x4.zero, result);

        result = Matrix4x4.zero;
        Assert.True(Matrix4x4.Inverse3DAffine(Matrix4x4.Frustum(-2, 3, -1, 4, 0.3f, 200f), ref result));
        AssertMatrix(new Matrix4x4
        {
            m00 = 8.333334f, m02 = 1.661674f, m03 = 0.998502f,
            m11 = 8.333334f, m12 = 4.985023f, m13 = 2.995507f,
            m22 = -0.997005f, m23 = -0.599101f,
            m33 = 1f
        }, result);
    }

    [Fact]
    public void ProjectionDecompositionMatchesUnityProbe()
    {
        AssertFrustum(new FrustumPlanes { left = -2, right = 3, bottom = -1, top = 4, zNear = 0.3f, zFar = 199.9971f },
            Matrix4x4.Frustum(-2, 3, -1, 4, 0.3f, 200f).decomposeProjection);
        AssertFrustum(new FrustumPlanes { left = -0.32683f, right = 0.32683f, bottom = -0.18384f, top = 0.18384f, zNear = 0.3f, zFar = 199.9971f },
            Matrix4x4.Perspective(63, 16f / 9f, 0.3f, 200f).decomposeProjection);
        AssertFrustum(new FrustumPlanes { left = -2, right = 3, bottom = -1, top = 4, zNear = 0.3f, zFar = 200f },
            Matrix4x4.Ortho(-2, 3, -1, 4, 0.3f, 200f).decomposeProjection);
        FrustumPlanes zero = Matrix4x4.zero.decomposeProjection;
        Assert.True(float.IsNaN(zero.left) && float.IsNaN(zero.right) && float.IsNaN(zero.bottom) && float.IsNaN(zero.top));
        Assert.Equal(0f, zero.zNear);
        Assert.Equal(0f, zero.zFar);
    }

    [Fact]
    public void FrustumStructOverloadForwardsEveryField()
    {
        var planes = new FrustumPlanes { left = -2, right = 3, bottom = -1, top = 4, zNear = 0.3f, zFar = 200f };
        AssertMatrix(Matrix4x4.Frustum(-2, 3, -1, 4, 0.3f, 200f), Matrix4x4.Frustum(planes));
    }

    [Fact]
    public void TransformPlaneMatchesUnityInverseTransposeSemantics()
    {
        Plane plane = new Plane(new Vector3(1, 2, -3), new Vector3(4, -2, 7));
        Plane positive = PositiveMatrix().TransformPlane(plane);
        AssertVector(new Vector3(0.1011f, 0.6537f, -0.7499f), positive.normal);
        Assert.Equal(7.688705f, positive.distance, 3);

        Plane projection = Matrix4x4.Frustum(-2, 3, -1, 4, 0.3f, 200f).TransformPlane(plane);
        AssertVector(new Vector3(0.2104f, 0.4208f, -0.8824f), projection.normal);
        Assert.Equal(13.288f, projection.distance, 3);
    }

    [Fact]
    public void ToStringOverloadsMatchUnityRowsTabsAndTrailingNewline()
    {
        string expectedDefault = "1.38444\t1.07001\t2.50957\t3.50000\n-0.43024\t2.79538\t-1.16949\t-2.25000\n-1.37776\t0.20226\t2.88693\t7.75000\n0.00000\t0.00000\t0.00000\t1.00000\n";
        string expectedF1 = "1.4\t1.1\t2.5\t3.5\n-0.4\t2.8\t-1.2\t-2.3\n-1.4\t0.2\t2.9\t7.8\n0.0\t0.0\t0.0\t1.0\n";
        Assert.Equal(expectedDefault, PositiveMatrix().ToString());
        Assert.Equal(expectedF1, PositiveMatrix().ToString("F1"));
        Assert.Equal(expectedDefault, PositiveMatrix().ToString("F5", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EqualityIsApproximateWhileEqualsIsExact()
    {
        Matrix4x4 matrix = PositiveMatrix();
        Matrix4x4 near = matrix;
        near.m00 += 0.000001f;
        Matrix4x4 different = matrix;
        different.m00 += 0.001f;
        Assert.True(matrix == near);
        Assert.False(matrix.Equals(near));
        Assert.False(matrix == different);
        Assert.True(matrix != different);
    }

    [Fact]
    public void MultiplyPointPerformsUnconditionalHomogeneousDivide()
    {
        Matrix4x4 zeroW = Matrix4x4.zero;
        zeroW.m00 = zeroW.m11 = zeroW.m22 = 1f;
        Vector3 result = zeroW.MultiplyPoint(new Vector3(0, 2, -3));
        Assert.True(float.IsNaN(result.x));
        Assert.Equal(float.PositiveInfinity, result.y);
        Assert.Equal(float.NegativeInfinity, result.z);
    }

    [Fact]
    public void InvalidIndexExceptionsMatchUnityMessages()
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        Assert.Equal("Invalid matrix index!", Assert.Throws<IndexOutOfRangeException>(() => _ = matrix[-1]).Message);
        Assert.Equal("Invalid row index!", Assert.Throws<IndexOutOfRangeException>(() => matrix.GetRow(4)).Message);
        Assert.Equal("Invalid column index!", Assert.Throws<IndexOutOfRangeException>(() => matrix.GetColumn(-1)).Message);
        Assert.Equal("Invalid matrix index!", Assert.Throws<IndexOutOfRangeException>(() => matrix.SetColumn(-1, Vector4.zero)).Message);
        Assert.Equal("Invalid matrix index!", Assert.Throws<IndexOutOfRangeException>(() => matrix.SetRow(4, Vector4.zero)).Message);
    }

    [Fact]
    public void NativeInverseDeterminantAndTransposeExportsMatchManagedResults()
    {
        Matrix4x4 matrix = PositiveMatrix();
        bool determinantResolved = AnityNative.TryMatrixDeterminant(ToNative(matrix), out float determinant);
        bool inverseResolved = AnityNative.TryMatrixInverse(ToNative(matrix), out AnityNative.TransformMatrix4x4 inverse);
        bool transposeResolved = AnityNative.TryMatrixTranspose(ToNative(matrix), out AnityNative.TransformMatrix4x4 transpose);
        AssertNativeResolved(determinantResolved && inverseResolved && transposeResolved);
        if (!determinantResolved || !inverseResolved || !transposeResolved) return;
        Assert.Equal(24f, determinant, 4);
        AssertMatrix(matrix.inverse, FromNative(inverse));
        AssertMatrix(matrix.transpose, FromNative(transpose));
    }

    [Fact]
    public void NativeProjectionAndDecompositionExportsMatchUnityProbe()
    {
        bool projectionResolved = AnityNative.TryMatrixFrustum(-2, 3, -1, 4, 0.3f, 200f, out AnityNative.TransformMatrix4x4 projection);
        AnityNative.MatrixFrustumPlanes planes = default;
        bool valid = false;
        bool decompositionResolved = projectionResolved && AnityNative.TryMatrixDecomposeProjection(projection, out planes);
        bool validResolved = projectionResolved && AnityNative.TryMatrixValidTRS(projection, out valid);
        AssertNativeResolved(projectionResolved && decompositionResolved && validResolved);
        if (!projectionResolved || !decompositionResolved || !validResolved) return;
        Assert.False(valid);
        Assert.Equal(-2f, planes.left, 3);
        Assert.Equal(3f, planes.right, 3);
        Assert.Equal(0.3f, planes.zNear, 3);
        Assert.Equal(199.9971f, planes.zFar, 2);
    }

    [Fact]
    public void NativeTRSLookAtAndRotationExportsAreOperational()
    {
        Quaternion rotation = Quaternion.Euler(17, 41, -13);
        bool trsResolved = AnityNative.TryMatrixTRS(ToNative(new Vector3(3.5f, -2.25f, 7.75f)), ToNative(rotation), ToNative(new Vector3(2, 3, 4)), out AnityNative.TransformMatrix4x4 trs);
        AnityNative.TransformQuaternion extracted = default;
        bool rotationResolved = trsResolved && AnityNative.TryMatrixExtractRotation(trs, out extracted);
        bool lookResolved = AnityNative.TryMatrixLookAt(ToNative(new Vector3(2, 3, 4)), ToNative(new Vector3(-1, 0, 5)), ToNative(Vector3.up), out AnityNative.TransformMatrix4x4 look);
        AssertNativeResolved(trsResolved && rotationResolved && lookResolved);
        if (!trsResolved || !rotationResolved || !lookResolved) return;
        AssertQuaternion(new Quaternion(0.0983f, 0.3598f, -0.1563f, 0.9146f), new Quaternion(extracted.x, extracted.y, extracted.z, extracted.w));
        AssertMatrix(Matrix4x4.LookAt(new Vector3(2, 3, 4), new Vector3(-1, 0, 5), Vector3.up), FromNative(look));
    }

    private static Matrix4x4 PositiveMatrix()
        => Matrix4x4.TRS(new Vector3(3.5f, -2.25f, 7.75f), Quaternion.Euler(17, 41, -13), new Vector3(2, 3, 4));

    private static void AssertNativeResolved(bool resolved)
    {
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(resolved);
    }

    private static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (int index = 0; index < 16; index++)
            Assert.InRange(MathF.Abs(expected[index] - actual[index]), 0, Tolerance);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(MathF.Abs(expected.x - actual.x), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.y - actual.y), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.z - actual.z), 0, Tolerance);
    }

    private static void AssertQuaternion(Quaternion expected, Quaternion actual)
    {
        Assert.InRange(MathF.Abs(expected.x - actual.x), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.y - actual.y), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.z - actual.z), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.w - actual.w), 0, Tolerance);
    }

    private static void AssertFrustum(FrustumPlanes expected, FrustumPlanes actual)
    {
        Assert.InRange(MathF.Abs(expected.left - actual.left), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.right - actual.right), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.bottom - actual.bottom), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.top - actual.top), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.zNear - actual.zNear), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.zFar - actual.zFar), 0, 0.01f);
    }

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
}
