namespace UnityEngine;

public class SphereCollider : Collider
{
    private Vector3 _center;
    private float _radius = 0.5f;

    public Vector3 center
    {
        get => _center;
        set => _center = value;
    }

    public float radius
    {
        get => _radius;
        set => _radius = MathF.Max(0f, value);
    }

    public override Bounds bounds
    {
        get
        {
            if (transform == null) return new Bounds(_center, Vector3.one * (_radius * 2f));
            Vector3 scale = transform.lossyScale;
            float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Max(MathF.Abs(scale.y), MathF.Abs(scale.z)));
            Vector3 worldCenter = transform.TransformPoint(_center);
            return new Bounds(worldCenter, Vector3.one * (worldRadius * 2f));
        }
    }

    public override Vector3 ClosestPoint(Vector3 position)
    {
        if (transform == null) return position;
        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Max(MathF.Abs(scale.y), MathF.Abs(scale.z)));
        Vector3 worldCenter = transform.TransformPoint(_center);
        Vector3 dir = position - worldCenter;
        float dist = dir.magnitude;
        if (dist <= worldRadius) return position;
        return worldCenter + dir.normalized * worldRadius;
    }

    public override ColliderShape GetShape()
    {
        if (transform == null)
            return new ColliderShape(ColliderShapeType.Sphere, _center, Vector3.one, _radius, 0f, 0);

        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Max(MathF.Abs(scale.y), MathF.Abs(scale.z)));
        Vector3 worldCenter = transform.TransformPoint(_center);
        return new ColliderShape(ColliderShapeType.Sphere, worldCenter, Vector3.one, worldRadius, 0f, 0);
    }
}
