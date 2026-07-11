namespace UnityEngine;

public class MeshCollider : Collider
{
    private Mesh _sharedMesh;
    private bool _convex;
    private bool _inflateMesh;
    private float _skinWidth = 0.01f;
    private MeshColliderCookingOptions _cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.WeldColocatedVertices;

    public Mesh sharedMesh
    {
        get => _sharedMesh;
        set => _sharedMesh = value;
    }

    public bool convex
    {
        get => _convex;
        set => _convex = value;
    }

    public bool inflateMesh
    {
        get => _inflateMesh;
        set => _inflateMesh = value;
    }

    public float skinWidth
    {
        get => _skinWidth;
        set => _skinWidth = MathF.Max(0f, value);
    }

    public MeshColliderCookingOptions cookingOptions
    {
        get => _cookingOptions;
        set => _cookingOptions = value;
    }

    public override Bounds bounds
    {
        get
        {
            if (_sharedMesh != null)
            {
                Bounds meshBounds = _sharedMesh.bounds;
                if (transform != null)
                {
                    Vector3 scale = transform.lossyScale;
                    Vector3 worldCenter = transform.TransformPoint(meshBounds.center);
                    Vector3 worldSize = new Vector3(
                        MathF.Abs(meshBounds.size.x * scale.x),
                        MathF.Abs(meshBounds.size.y * scale.y),
                        MathF.Abs(meshBounds.size.z * scale.z));
                    return new Bounds(worldCenter, worldSize);
                }
                return meshBounds;
            }
            if (transform != null)
                return new Bounds(transform.position, Vector3.one);
            return new Bounds(Vector3.zero, Vector3.one);
        }
    }

    public override Vector3 ClosestPoint(Vector3 position)
    {
        Bounds b = bounds;
        return new Vector3(
            Math.Clamp(position.x, b.min.x, b.max.x),
            Math.Clamp(position.y, b.min.y, b.max.y),
            Math.Clamp(position.z, b.min.z, b.max.z));
    }

    public override ColliderShape GetShape()
    {
        Bounds b = bounds;
        return new ColliderShape(ColliderShapeType.Box, b.center, b.size, 0f, 0f, 0);
    }
}
