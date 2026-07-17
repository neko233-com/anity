using System;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class TransformAffineParityTests
{
    private const float Tolerance = 0.002f;

    [Fact]
    public void ChildLocalToWorldPreservesHierarchyShearLikeUnity2022()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            AssertMatrix(hierarchy.Child.transform.localToWorldMatrix, new Matrix4x4
            {
                m00 = -0.256851f, m01 = 0.184397f, m02 = 6.390552f, m03 = 10.701960f,
                m10 = 3.379128f, m11 = 0.957526f, m12 = -0.146725f, m13 = -7.808060f,
                m20 = -2.042177f, m21 = 1.792245f, m22 = 3.224957f, m23 = 13.093430f,
                m33 = 1f
            });
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void GrandchildLocalToWorldMultipliesEveryAffineLevel()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            Matrix4x4 expected = hierarchy.Parent.transform.localToWorldMatrix
                * Matrix4x4.TRS(hierarchy.Child.transform.localPosition, hierarchy.Child.transform.localRotation, hierarchy.Child.transform.localScale)
                * Matrix4x4.TRS(hierarchy.Grandchild.transform.localPosition, hierarchy.Grandchild.transform.localRotation, hierarchy.Grandchild.transform.localScale);

            AssertMatrix(hierarchy.Grandchild.transform.localToWorldMatrix, expected);
            AssertMatrix(hierarchy.Grandchild.transform.localToWorldMatrix, new Matrix4x4
            {
                m00 = 2.062685f, m01 = 0.454243f, m02 = 3.401214f, m03 = 12.750720f,
                m10 = 2.090479f, m11 = -0.481227f, m12 = -1.090165f, m13 = -7.858634f,
                m20 = 0.330925f, m21 = 3.204344f, m22 = 2.033270f, m23 = 18.057180f,
                m33 = 1f
            });
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void TransformPointUsesShearedMatrixAndRoundTrips()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            Vector3 point = new Vector3(0.25f, -1.5f, 2.75f);
            Vector3 world = hierarchy.Child.transform.TransformPoint(point);

            AssertVector(new Vector3(27.9352f, -8.8031f, 18.7632f), world);
            AssertVector(world, hierarchy.Child.transform.localToWorldMatrix.MultiplyPoint3x4(point));
            AssertVector(point, hierarchy.Child.transform.InverseTransformPoint(world));
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void TransformVectorUsesShearAndRoundTrips()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            Vector3 vector = new Vector3(-2.5f, 0.75f, 1.25f);
            Vector3 world = hierarchy.Child.transform.TransformVector(vector);

            AssertVector(new Vector3(8.7686f, -7.9131f, 10.4808f), world);
            AssertVector(world, hierarchy.Child.transform.localToWorldMatrix.MultiplyVector(vector));
            AssertVector(vector, hierarchy.Child.transform.InverseTransformVector(world));
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void TransformDirectionIgnoresScaleAndShear()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            Vector3 vector = new Vector3(-2.5f, 0.75f, 1.25f);
            AssertVector(new Vector3(0.8551f, -1.7911f, 2.1062f), hierarchy.Child.transform.TransformDirection(vector));
            AssertVector(vector, hierarchy.Child.transform.InverseTransformDirection(hierarchy.Child.transform.TransformDirection(vector)));
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void TransformLossyScaleUsesRotationProjectionNotColumnMagnitude()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            AssertVector(new Vector3(3.9058f, 1.9609f, 6.9175f), hierarchy.Child.transform.lossyScale);
            AssertVector(new Vector3(3.9566f, 2.0403f, 7.1597f), hierarchy.Child.transform.localToWorldMatrix.lossyScale);
            Assert.NotEqual(hierarchy.Child.transform.lossyScale, hierarchy.Child.transform.localToWorldMatrix.lossyScale);
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void GrandchildLossyScaleProjectsFullAncestry()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            AssertVector(new Vector3(2.8241f, 2.9619f, 3.8828f), hierarchy.Grandchild.transform.lossyScale);
            AssertVector(new Vector3(2.9554f, 3.2720f, 4.1099f), hierarchy.Grandchild.transform.localToWorldMatrix.lossyScale);
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void TwoNegativeScaleAxesKeepPositiveProjectedLossyScale()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            hierarchy.Parent.transform.localScale = new Vector3(-2, 3, 4);
            hierarchy.Child.transform.localScale = new Vector3(-1.2f, 0.8f, 2.1f);

            AssertVector(new Vector3(3.9058f, 1.9609f, 6.9175f), hierarchy.Child.transform.lossyScale);
            Assert.Equal(48.38401f, hierarchy.Child.transform.localToWorldMatrix.determinant, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void SingularHierarchyUsesUnityZeroReciprocalWorldToLocalChain()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            hierarchy.Parent.transform.localScale = new Vector3(0, 2, 3);
            Matrix4x4 inverse = hierarchy.Child.transform.worldToLocalMatrix;

            AssertMatrix(inverse, new Matrix4x4
            {
                m00 = 0.025280f, m01 = 0.359224f, m02 = -0.086775f, m03 = 2.819649f,
                m10 = 0.192455f, m11 = 0.218791f, m12 = 0.125063f, m13 = -0.549719f,
                m20 = 0.107448f, m21 = 0.047861f, m22 = 0.093023f, m23 = -2.076244f,
                m33 = 1f
            });
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void SingularHierarchyProjectsUnrecoverablePointLikeUnity()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            hierarchy.Parent.transform.localScale = new Vector3(0, 2, 3);
            Vector3 point = new Vector3(0.25f, -1.5f, 2.75f);
            Vector3 world = hierarchy.Child.transform.TransformPoint(point);

            AssertVector(new Vector3(16.1523f, -5.6927f, 21.5386f), world);
            AssertVector(new Vector3(-0.6860f, 4.0071f, 1.3904f), hierarchy.Child.transform.InverseTransformPoint(world));
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void SetParentTruePreservesProjectedPoseButNotShearLikeUnity()
    {
        var parentA = new GameObject("shear-parent-a");
        var parentB = new GameObject("shear-parent-b");
        var moved = new GameObject("shear-moved");
        ConfigureReparentFixture(parentA, parentB, moved);
        try
        {
            Matrix4x4 before = moved.transform.localToWorldMatrix;
            Vector3 position = moved.transform.position;
            Quaternion rotation = moved.transform.rotation;
            Vector3 scale = moved.transform.lossyScale;

            moved.transform.SetParent(parentB.transform, true);

            AssertVector(position, moved.transform.position);
            Assert.True(Quaternion.Angle(rotation, moved.transform.rotation) < Tolerance);
            AssertVector(scale, moved.transform.lossyScale);
            AssertVector(new Vector3(-10.5521f, -6.1338f, -3.4085f), moved.transform.localPosition);
            AssertVector(new Vector3(1.6116f, 0.9661f, -12.0430f), moved.transform.localScale);
            Assert.False(MatrixApproximately(before, moved.transform.localToWorldMatrix));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parentA);
            UnityEngine.Object.DestroyImmediate(parentB);
        }
    }

    [Fact]
    public void UnparentTrueUsesProjectedWorldScaleAsLocalScale()
    {
        var parentA = new GameObject("shear-parent-a");
        var parentB = new GameObject("shear-parent-b");
        var moved = new GameObject("shear-moved");
        ConfigureReparentFixture(parentA, parentB, moved);
        try
        {
            moved.transform.SetParent(parentB.transform, true);
            Vector3 scale = moved.transform.lossyScale;
            moved.transform.SetParent(null, true);

            AssertVector(new Vector3(3.6043f, 1.6803f, 6.3135f), scale);
            AssertVector(scale, moved.transform.localScale);
            AssertVector(scale, moved.transform.lossyScale);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parentA);
            UnityEngine.Object.DestroyImmediate(parentB);
            UnityEngine.Object.DestroyImmediate(moved);
        }
    }

    [Fact]
    public void ZeroScaleParentUsesRepresentableProjectionDuringWorldStaysReparent()
    {
        var parent = new GameObject("zero-parent");
        var moved = new GameObject("zero-moved");
        parent.transform.SetPositionAndRotation(new Vector3(3, -2, 7), Quaternion.Euler(12, 38, -7));
        parent.transform.localScale = new Vector3(0, 2, 3);
        moved.transform.SetPositionAndRotation(new Vector3(8, 4, -5), Quaternion.Euler(27, -33, 48));
        moved.transform.localScale = new Vector3(1.1f, 0.7f, 1.9f);
        try
        {
            moved.transform.SetParent(parent.transform, true);

            AssertVector(new Vector3(-0.1942f, 5.2743f, 1.7458f), moved.transform.position);
            AssertVector(new Vector3(1.1f, 0.7f, 1.9f), moved.transform.lossyScale);
            AssertVector(new Vector3(0.4850f, 0.3814f, 2.1190f), moved.transform.localScale);
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    [Fact]
    public void MatrixLossyScaleCarriesSingleNegativeDeterminantOnX()
    {
        Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(17, 41, -13), new Vector3(-2, 3, 4));
        AssertVector(new Vector3(-2, 3, 4), matrix.lossyScale);
    }

    [Fact]
    public void SingularMatrixInverseIsZeroLikeUnity()
    {
        Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(3, -2, 7), Quaternion.Euler(12, 38, -7), new Vector3(0, 2, 3));
        AssertMatrix(Matrix4x4.zero, matrix.inverse);
    }

    [Fact]
    public void NativeTransformCompositionMatchesManagedAffineProduct()
    {
        Matrix4x4 parent = Matrix4x4.TRS(new Vector3(3.5f, -2.25f, 7.75f), Quaternion.Euler(17, 41, -13), new Vector3(2, 3, 4));
        Vector3 position = new Vector3(1.25f, -0.75f, 2.5f);
        Quaternion rotation = Quaternion.Euler(-22, 35, 61);
        Vector3 scale = new Vector3(1.2f, 0.8f, 2.1f);

        bool resolved = AnityNative.TryComposeTransformLocalToWorld(
            ToNative(parent), ToNative(position), ToNative(rotation), ToNative(scale), out AnityNative.TransformMatrix4x4 native);

        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(resolved);
        if (!resolved) return;
        AssertMatrix(parent * Matrix4x4.TRS(position, rotation, scale), FromNative(native));
    }

    [Fact]
    public void NativeTransformProjectionMatchesOfficialLossyScale()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            Matrix4x4 matrix = hierarchy.Child.transform.localToWorldMatrix;
            Quaternion rotation = hierarchy.Child.transform.rotation;
            bool resolved = AnityNative.TryProjectTransformLossyScale(
                ToNative(matrix), ToNative(rotation), out AnityNative.TransformVector3 native);

            if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(resolved);
            if (!resolved) return;
            AssertVector(new Vector3(3.9058f, 1.9609f, 6.9175f), new Vector3(native.x, native.y, native.z));
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    [Fact]
    public void NativeWorldToLocalCompositionUsesUnityZeroReciprocalChain()
    {
        var hierarchy = CreateAffineHierarchy();
        try
        {
            hierarchy.Parent.transform.localScale = new Vector3(0, 2, 3);
            bool resolved = AnityNative.TryComposeTransformWorldToLocal(
                ToNative(hierarchy.Parent.transform.worldToLocalMatrix),
                ToNative(hierarchy.Child.transform.localPosition),
                ToNative(hierarchy.Child.transform.localRotation),
                ToNative(hierarchy.Child.transform.localScale),
                out AnityNative.TransformMatrix4x4 native);

            if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(resolved);
            if (!resolved) return;
            AssertMatrix(new Matrix4x4
            {
                m00 = 0.025280f, m01 = 0.359224f, m02 = -0.086775f, m03 = 2.819649f,
                m10 = 0.192455f, m11 = 0.218791f, m12 = 0.125063f, m13 = -0.549719f,
                m20 = 0.107448f, m21 = 0.047861f, m22 = 0.093023f, m23 = -2.076244f,
                m33 = 1f
            }, FromNative(native));
        }
        finally { UnityEngine.Object.DestroyImmediate(hierarchy.Parent); }
    }

    private static (GameObject Parent, GameObject Child, GameObject Grandchild) CreateAffineHierarchy()
    {
        var parent = new GameObject("affine-parent");
        var child = new GameObject("affine-child");
        var grandchild = new GameObject("affine-grandchild");
        parent.transform.SetPositionAndRotation(new Vector3(3.5f, -2.25f, 7.75f), Quaternion.Euler(17, 41, -13));
        parent.transform.localScale = new Vector3(2, 3, 4);
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = new Vector3(1.25f, -0.75f, 2.5f);
        child.transform.localRotation = Quaternion.Euler(-22, 35, 61);
        child.transform.localScale = new Vector3(1.2f, 0.8f, 2.1f);
        grandchild.transform.SetParent(child.transform, false);
        grandchild.transform.localPosition = new Vector3(-0.5f, 1.75f, 0.25f);
        grandchild.transform.localRotation = Quaternion.Euler(11, -27, 19);
        grandchild.transform.localScale = new Vector3(0.7f, 1.3f, 0.6f);
        return (parent, child, grandchild);
    }

    private static void ConfigureReparentFixture(GameObject parentA, GameObject parentB, GameObject moved)
    {
        parentA.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(12, 38, -7));
        parentA.transform.localScale = new Vector3(2, 3, 4);
        parentB.transform.SetPositionAndRotation(new Vector3(-6, 5, 1), Quaternion.Euler(-19, 73, 28));
        parentB.transform.localScale = new Vector3(0.5f, 1.75f, -2.25f);
        moved.transform.SetParent(parentA.transform, false);
        moved.transform.localPosition = new Vector3(1.5f, -2, 0.75f);
        moved.transform.localRotation = Quaternion.Euler(27, -33, 48);
        moved.transform.localScale = new Vector3(1.1f, 0.7f, 1.9f);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.True(MathF.Abs(expected.x - actual.x) <= Tolerance, $"x expected={expected.x} actual={actual.x}; vector actual={actual}");
        Assert.True(MathF.Abs(expected.y - actual.y) <= Tolerance, $"y expected={expected.y} actual={actual.y}; vector actual={actual}");
        Assert.True(MathF.Abs(expected.z - actual.z) <= Tolerance, $"z expected={expected.z} actual={actual.z}; vector actual={actual}");
    }

    private static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (int index = 0; index < 16; index++)
            Assert.InRange(MathF.Abs(expected[index] - actual[index]), 0, Tolerance);
    }

    private static bool MatrixApproximately(Matrix4x4 left, Matrix4x4 right)
    {
        for (int index = 0; index < 16; index++)
            if (MathF.Abs(left[index] - right[index]) > Tolerance) return false;
        return true;
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
