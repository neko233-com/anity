using System;

namespace UnityEngine;

[Serializable]
public struct FrustumPlanes
{
    public float left;
    public float right;
    public float bottom;
    public float top;
    public float zNear;
    public float zFar;
}

public static class GeometryUtility
{
    public static Bounds CalculateBounds(Collider[] colliders)
    {
        if (colliders == null || colliders.Length == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            bounds.Encapsulate(colliders[i].bounds);
        }
        return bounds;
    }

    public static Plane[] CalculateFrustumPlanes(Camera camera)
    {
        return CalculateFrustumPlanes(camera.projectionMatrix * camera.worldToCameraMatrix);
    }

    public static Plane[] CalculateFrustumPlanes(Matrix4x4 worldToProjectionMatrix)
    {
        Plane[] planes = new Plane[6];
        CalculateFrustumPlanes(worldToProjectionMatrix, planes);
        return planes;
    }

    public static void CalculateFrustumPlanes(Matrix4x4 worldToProjectionMatrix, Plane[] planes)
    {
        if (planes == null || planes.Length < 6)
            return;

        Vector3 a = new Vector3(
            worldToProjectionMatrix.m03 - worldToProjectionMatrix.m00,
            worldToProjectionMatrix.m13 - worldToProjectionMatrix.m10,
            worldToProjectionMatrix.m23 - worldToProjectionMatrix.m20);
        planes[0] = new Plane(a, worldToProjectionMatrix.m33 - worldToProjectionMatrix.m30);

        Vector3 b = new Vector3(
            worldToProjectionMatrix.m03 + worldToProjectionMatrix.m00,
            worldToProjectionMatrix.m13 + worldToProjectionMatrix.m10,
            worldToProjectionMatrix.m23 + worldToProjectionMatrix.m20);
        planes[1] = new Plane(b, worldToProjectionMatrix.m33 + worldToProjectionMatrix.m30);

        Vector3 c = new Vector3(
            worldToProjectionMatrix.m03 - worldToProjectionMatrix.m01,
            worldToProjectionMatrix.m13 - worldToProjectionMatrix.m11,
            worldToProjectionMatrix.m23 - worldToProjectionMatrix.m21);
        planes[2] = new Plane(c, worldToProjectionMatrix.m33 - worldToProjectionMatrix.m31);

        Vector3 d = new Vector3(
            worldToProjectionMatrix.m03 + worldToProjectionMatrix.m01,
            worldToProjectionMatrix.m13 + worldToProjectionMatrix.m11,
            worldToProjectionMatrix.m23 + worldToProjectionMatrix.m21);
        planes[3] = new Plane(d, worldToProjectionMatrix.m33 + worldToProjectionMatrix.m31);

        Vector3 e = new Vector3(
            worldToProjectionMatrix.m03 - worldToProjectionMatrix.m02,
            worldToProjectionMatrix.m13 - worldToProjectionMatrix.m12,
            worldToProjectionMatrix.m23 - worldToProjectionMatrix.m22);
        planes[4] = new Plane(e, worldToProjectionMatrix.m33 - worldToProjectionMatrix.m32);

        Vector3 f = new Vector3(
            worldToProjectionMatrix.m03 + worldToProjectionMatrix.m02,
            worldToProjectionMatrix.m13 + worldToProjectionMatrix.m12,
            worldToProjectionMatrix.m23 + worldToProjectionMatrix.m22);
        planes[5] = new Plane(f, worldToProjectionMatrix.m33 + worldToProjectionMatrix.m32);
    }

    public static bool TestPlanesAABB(Plane[] planes, Bounds bounds)
    {
        if (planes == null) return false;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int i = 0; i < planes.Length; i++)
        {
            Vector3 normal = planes[i].normal;
            float dist = planes[i].distance;

            Vector3 positive = min;
            if (normal.x >= 0) positive.x = max.x;
            if (normal.y >= 0) positive.y = max.y;
            if (normal.z >= 0) positive.z = max.z;

            float dot = Vector3.Dot(normal, positive);
            if (dot + dist < 0)
                return false;
        }

        return true;
    }

    public static bool TryCreatePlaneFromPolygon(Vector3[] vertices, out Plane plane)
    {
        plane = default;
        if (vertices == null || vertices.Length < 3)
            return false;

        plane = new Plane(vertices[0], vertices[1], vertices[2]);
        return true;
    }

    public static bool IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 matrix, out float distance)
    {
        distance = 0;
        return false;
    }
}

public struct SphericalHarmonicsL2
{
    private float[] _coefficients;

    public float this[int coefficient, int row]
    {
        get
        {
            if (_coefficients == null)
                _coefficients = new float[27];
            return _coefficients[row * 9 + coefficient];
        }
        set
        {
            if (_coefficients == null)
                _coefficients = new float[27];
            _coefficients[row * 9 + coefficient] = value;
        }
    }

    public void Clear()
    {
        if (_coefficients != null)
            Array.Clear(_coefficients, 0, _coefficients.Length);
    }

    public void AddAmbientLight(Color color)
    {
        if (_coefficients == null) _coefficients = new float[27];
        for (int c = 0; c < 3; c++)
        {
            float v = c == 0 ? color.r : c == 1 ? color.g : color.b;
            _coefficients[c * 9 + 0] += v;
        }
    }

    public void AddDirectionalLight(Vector3 direction, Color color, float intensity)
    {
        if (_coefficients == null) _coefficients = new float[27];
        for (int c = 0; c < 3; c++)
        {
            float v = (c == 0 ? color.r : c == 1 ? color.g : color.b) * intensity;
            int offset = c * 9;
            _coefficients[offset + 0] += v;
            _coefficients[offset + 1] += v * direction.y;
            _coefficients[offset + 2] += v * direction.z;
            _coefficients[offset + 3] += v * direction.x;
        }
    }

    public static SphericalHarmonicsL2 operator +(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs)
    {
        SphericalHarmonicsL2 result = new SphericalHarmonicsL2();
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 3; j++)
                result[i, j] = lhs[i, j] + rhs[i, j];
        return result;
    }

    public static SphericalHarmonicsL2 operator -(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs)
    {
        SphericalHarmonicsL2 result = new SphericalHarmonicsL2();
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 3; j++)
                result[i, j] = lhs[i, j] - rhs[i, j];
        return result;
    }
}
