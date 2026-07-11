namespace UnityEngine;

public class BoxCollider : Collider
{
    private Vector3 _center;
    private Vector3 _size = Vector3.one;

    public Vector3 center
    {
        get => _center;
        set => _center = value;
    }

    public Vector3 size
    {
        get => _size;
        set => _size = value;
    }

    public override Bounds bounds
    {
        get
        {
            if (transform == null) return new Bounds(_center, _size);
            Vector3 scale = transform.lossyScale;
            Vector3 worldCenter = transform.TransformPoint(_center);
            Vector3 worldSize = new Vector3(
                MathF.Abs(_size.x * scale.x),
                MathF.Abs(_size.y * scale.y),
                MathF.Abs(_size.z * scale.z));
            return new Bounds(worldCenter, worldSize);
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
        if (transform == null)
            return new ColliderShape(ColliderShapeType.Box, _center, _size, 0f, 0f, 0);

        Vector3 scale = transform.lossyScale;
        Vector3 worldCenter = transform.TransformPoint(_center);
        Vector3 worldSize = new Vector3(
            MathF.Abs(_size.x * scale.x),
            MathF.Abs(_size.y * scale.y),
            MathF.Abs(_size.z * scale.z));
        return new ColliderShape(ColliderShapeType.Box, worldCenter, worldSize, 0f, 0f, 0);
    }
}
