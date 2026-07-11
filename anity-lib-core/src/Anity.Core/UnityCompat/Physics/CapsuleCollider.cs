namespace UnityEngine;

public class CapsuleCollider : Collider
{
    private Vector3 _center;
    private float _radius = 0.5f;
    private float _height = 2f;
    private int _direction = 1;

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

    public float height
    {
        get => _height;
        set => _height = MathF.Max(0f, value);
    }

    public int direction
    {
        get => _direction;
        set => _direction = Math.Clamp(value, 0, 2);
    }

    public override Bounds bounds
    {
        get
        {
            if (transform == null) return new Bounds(_center, new Vector3(_radius * 2f, _height, _radius * 2f));
            Vector3 scale = transform.lossyScale;
            Vector3 worldCenter = transform.TransformPoint(_center);
            float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
            float axisScale = _direction == 0 ? MathF.Abs(scale.x) : _direction == 1 ? MathF.Abs(scale.y) : MathF.Abs(scale.z);
            float worldHeight = MathF.Max(_height * axisScale, worldRadius * 2f);
            Vector3 size;
            switch (_direction)
            {
                case 0:
                    size = new Vector3(worldHeight, worldRadius * 2f, worldRadius * 2f);
                    break;
                case 2:
                    size = new Vector3(worldRadius * 2f, worldRadius * 2f, worldHeight);
                    break;
                default:
                    size = new Vector3(worldRadius * 2f, worldHeight, worldRadius * 2f);
                    break;
            }
            return new Bounds(worldCenter, size);
        }
    }

    public override Vector3 ClosestPoint(Vector3 position)
    {
        Vector3 axis = _direction == 0 ? Vector3.right : _direction == 1 ? Vector3.up : Vector3.forward;
        if (transform == null)
        {
            float halfH2 = MathF.Max(_height * 0.5f - _radius, 0f);
            Vector3 p0a = _center - axis * halfH2;
            Vector3 p1a = _center + axis * halfH2;
            Vector3 closest = ClosestPointOnSegment(position, p0a, p1a);
            Vector3 dir = position - closest;
            float dist = dir.magnitude;
            if (dist <= _radius) return position;
            return closest + dir.normalized * _radius;
        }
        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
        float axisScale = _direction == 0 ? MathF.Abs(scale.x) : _direction == 1 ? MathF.Abs(scale.y) : MathF.Abs(scale.z);
        float worldHeight = MathF.Max(_height * axisScale, worldRadius * 2f);
        float halfH = worldHeight * 0.5f - worldRadius;
        Vector3 worldCenter = transform.TransformPoint(_center);
        Vector3 worldAxis = axis;
        Vector3 p0 = worldCenter - worldAxis * halfH;
        Vector3 p1 = worldCenter + worldAxis * halfH;
        Vector3 segClosest = ClosestPointOnSegment(position, p0, p1);
        Vector3 d = position - segClosest;
        float dMag = d.magnitude;
        if (dMag <= worldRadius) return position;
        return segClosest + d.normalized * worldRadius;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = Vector3.Dot(ab, ab);
        if (denom < 1e-8f) return a;
        float t = Vector3.Dot(p - a, ab) / denom;
        t = Math.Clamp(t, 0f, 1f);
        return a + ab * t;
    }

    public override ColliderShape GetShape()
    {
        if (transform == null)
            return new ColliderShape(ColliderShapeType.Capsule, _center, Vector3.one, _radius, _height, _direction);

        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
        float axisScale = _direction == 0 ? MathF.Abs(scale.x) : _direction == 1 ? MathF.Abs(scale.y) : MathF.Abs(scale.z);
        float worldHeight = MathF.Max(_height * axisScale, worldRadius * 2f);
        Vector3 worldCenter = transform.TransformPoint(_center);
        return new ColliderShape(ColliderShapeType.Capsule, worldCenter, Vector3.one, worldRadius, worldHeight, _direction);
    }
}
