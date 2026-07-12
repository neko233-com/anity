using System;
using System.Collections.Generic;

namespace UnityEngine;

public abstract class Joint2D : Behaviour
{
    public Rigidbody2D? connectedBody { get; set; }
    public bool enableCollision { get; set; }
    public float breakForce { get; set; } = float.PositiveInfinity;
    public float breakTorque { get; set; } = float.PositiveInfinity;
    public bool autoConfigureConnectedAnchor { get; set; } = true;
    public Vector2 anchor { get; set; }
    public Vector2 connectedAnchor { get; set; }

    public event Action<Joint2D>? breakForceExceeded;
    public JointLimitState2D limitState => JointLimitState2D.Inactive;
    public Vector2 reactionForce => Vector2.zero;
    public float reactionTorque => 0f;
}

public sealed class FixedJoint2D : Joint2D
{
    public float dampingRatio { get; set; } = 0.05f;
    public float frequency { get; set; } = 3f;
    public float referenceAngle { get; set; }
}

public sealed class SpringJoint2D : Joint2D
{
    public bool autoConfigureDistance { get; set; } = true;
    public float distance { get; set; }
    public float dampingRatio { get; set; } = 0.05f;
    public float frequency { get; set; } = 3f;
}

public sealed class DistanceJoint2D : Joint2D
{
    public bool autoConfigureDistance { get; set; } = true;
    public bool maxDistanceOnly { get; set; }
    public float distance { get; set; }
    public float dampingRatio { get; set; } = 0f;
    public float frequency { get; set; } = 0f;
}

public sealed class HingeJoint2D : Joint2D
{
    public bool useMotor { get; set; }
    public bool useLimits { get; set; }
    public JointMotor2D motor { get; set; }
    public JointAngleLimits2D limits { get; set; }
    public float referenceAngle { get; set; }
    public JointLimitState2D limitState => JointLimitState2D.Inactive;
    public float jointAngle => 0f;
    public float jointSpeed => 0f;
}

public sealed class SliderJoint2D : Joint2D
{
    public bool autoConfigureAngle { get; set; } = true;
    public float angle { get; set; }
    public bool useMotor { get; set; }
    public bool useLimits { get; set; }
    public JointMotor2D motor { get; set; }
    public JointTranslationLimits2D limits { get; set; }
    public float referenceAngle { get; set; }
    public float jointTranslation => 0f;
    public float jointSpeed => 0f;
    public JointLimitState2D limitState => JointLimitState2D.Inactive;
}

public sealed class WheelJoint2D : Joint2D
{
    public bool useMotor { get; set; }
    public JointMotor2D motor { get; set; }
    public float suspensionAngle { get; set; }
    public JointSuspension2D suspension { get; set; }
    public float referenceAngle { get; set; }
    public float jointTranslation => 0f;
    public float jointSpeed => 0f;
}

public sealed class RelativeJoint2D : Joint2D
{
    public bool autoConfigureOffset { get; set; } = true;
    public float maxForce { get; set; } = float.PositiveInfinity;
    public float maxTorque { get; set; } = float.PositiveInfinity;
    public float correctionScale { get; set; } = 0.3f;
    public Vector2 linearOffset { get; set; }
    public float angularOffset { get; set; }
    public Vector2 target => Vector2.zero;
}

public sealed class FrictionJoint2D : Joint2D
{
    public float maxForce { get; set; } = float.PositiveInfinity;
    public float maxTorque { get; set; } = float.PositiveInfinity;
}

public sealed class TargetJoint2D : Joint2D
{
    public Vector2 target { get; set; }
    public bool autoConfigureTarget { get; set; } = true;
    public float maxForce { get; set; } = float.PositiveInfinity;
    public float dampingRatio { get; set; } = 1f;
    public float frequency { get; set; } = 5f;
    public float frequencyRatio { get => frequency; set => frequency = value; }
}

public struct JointMotor2D
{
    public float motorSpeed;
    public float maxMotorTorque;
}

public struct JointAngleLimits2D
{
    public float min;
    public float max;
    public float bounciness;
}

public struct JointTranslationLimits2D
{
    public float min;
    public float max;
}

public struct JointSuspension2D
{
    public float dampingRatio;
    public float frequency;
    public float angle;
}

public enum JointLimitState2D
{
    Inactive,
    LowerLimit,
    UpperLimit,
    EqualLimits
}

public abstract class Effector2D : Behaviour
{
    public bool useColliderMask { get; set; } = true;
    public int colliderMask { get; set; } = -1;
    public bool useGroundAngle { get; set; }
    public float groundAngle { get; set; } = 90f;
    public float groundAngleVariant { get; set; } = 10f;
    public float surfaceArc { get; set; } = 180f;
}

public sealed class AreaEffector2D : Effector2D
{
    public float forceAngle { get; set; }
    public float forceMagnitude { get; set; }
    public float forceVariation { get; set; }
    public float drag { get; set; }
    public float angularDrag { get; set; }
    public EffectorSelection2D forceTarget { get; set; } = EffectorSelection2D.Rigidbody;
}

public enum EffectorForceMode2D
{
    Constant,
    InverseLinear,
    InverseSquared
}

public sealed class PointEffector2D : Effector2D
{
    public float forceMagnitude { get; set; }
    public float forceVariation { get; set; }
    public float distanceScale { get; set; } = 1f;
    public float drag { get; set; }
    public float angularDrag { get; set; } = 0.2f;
    public EffectorForceMode2D forceMode { get; set; } = EffectorForceMode2D.Constant;
    public EffectorSelection2D forceSource { get; set; } = EffectorSelection2D.Collider;
    public EffectorSelection2D forceTarget { get; set; } = EffectorSelection2D.Rigidbody;
}

public sealed class PlatformEffector2D : Effector2D
{
    public bool useOneWay { get; set; } = true;
    public bool useOneWayGrouping { get; set; }
    public bool useSideFriction { get; set; }
    public bool useSideBounce { get; set; }
    public float sideArc { get; set; } = 1f;
    public float rotationalOffset { get; set; }
    public float sideOffset { get; set; }
}

public sealed class SurfaceEffector2D : Effector2D
{
    public float speed { get; set; } = 1f;
    public float speedVariation { get; set; }
    public float forceScale { get; set; } = 0.1f;
    public EffectorSelection2D forceTarget { get; set; } = EffectorSelection2D.Rigidbody;
    public float damping { get; set; }
    public bool useContactForce { get; set; }
    public bool useBounce { get; set; }
}

public sealed class BuoyancyEffector2D : Effector2D
{
    public float surfaceLevel { get; set; }
    public float density { get; set; } = 2f;
    public float angularDrag { get; set; } = 5f;
    public float linearDrag { get; set; } = 5f;
    public float flowAngle { get; set; }
    public float flowMagnitude { get; set; }
    public float flowVariation { get; set; }
    public LayerMask layerMask { get; set; } = -1;
}

public sealed class CompositeCollider2D : Collider2D
{
    public CompositeCollider2DGeometryType geometryType { get; set; } = CompositeCollider2DGeometryType.Polygons;
    public CompositeCollider2DGenerationType generationType { get; set; } = CompositeCollider2DGenerationType.Synchronous;
    public float vertexDistance { get; set; } = 0.0005f;
    public float offsetDistance { get; set; } = 0.005f;
    public float edgeRadius { get; set; }
    private List<Vector2[]> _paths = new();
    private Vector2[] _generatedPoints = Array.Empty<Vector2>();
    private bool _geometryDirty = true;

    public int pathCount => _paths.Count;

    public int vertexCount
    {
        get
        {
            if (_geometryDirty) GenerateGeometry();
            int count = 0;
            foreach (var p in _paths) count += p.Length;
            return count;
        }
    }

    public int pointCount
    {
        get
        {
            if (_geometryDirty) GenerateGeometry();
            return vertexCount;
        }
    }

    public Vector2[] vertices => _generatedPoints;

    public void GenerateGeometry()
    {
        _paths.Clear();
        _geometryDirty = false;
        if (transform == null || gameObject == null)
        {
            _generatedPoints = new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };
            _paths.Add(_generatedPoints);
            return;
        }

        var allPoints = new List<Vector2>();
        var childTransforms = new List<Transform>();
        CollectChildTransforms(transform, childTransforms);
        foreach (var child in childTransforms)
            CollectColliderPoints(child.gameObject, allPoints);

        if (allPoints.Count < 3)
        {
            _generatedPoints = new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };
        }
        else
        {
            _generatedPoints = ComputeConvexHull(allPoints.ToArray());
        }
        _paths.Add(_generatedPoints);
    }

    private void CollectChildTransforms(Transform parent, List<Transform> result)
    {
        result.Add(parent);
        if (parent.childCount > 0)
        {
            foreach (Transform child in parent)
            {
                CollectChildTransforms(child, result);
            }
        }
    }

    private void CollectColliderPoints(GameObject go, List<Vector2> points)
    {
        if (go == null) return;
        foreach (var col in go.GetComponents<Collider2D>())
        {
            if (col == this || col == null || !col.enabled) continue;
            if (col is TilemapCollider2D tilemap && !tilemap.usedByComposite) continue;

            var shape = col.GetShape();
            var t = col.transform;
            switch (shape.type)
            {
                case ColliderShapeType2D.Box:
                    var half = shape.size * 0.5f;
                    var corners = new[]
                    {
                        shape.offset + new Vector2(-half.x, -half.y),
                        shape.offset + new Vector2(half.x, -half.y),
                        shape.offset + new Vector2(half.x, half.y),
                        shape.offset + new Vector2(-half.x, half.y)
                    };
                    foreach (var c in corners) points.Add(t != transform ? TransformPoint2D(t, c) : c);
                    break;
                case ColliderShapeType2D.Polygon:
                case ColliderShapeType2D.Edge:
                    if (shape.paths != null && shape.paths.Count > 0)
                    {
                        foreach (var path in shape.paths)
                        {
                            if (path != null)
                                foreach (var p in path) points.Add(t != transform ? TransformPoint2D(t, p + shape.offset) : p + shape.offset);
                        }
                    }
                    else if (shape.points != null)
                        foreach (var p in shape.points) points.Add(t != transform ? TransformPoint2D(t, p + shape.offset) : p + shape.offset);
                    break;
                case ColliderShapeType2D.Circle:
                    int segments = 16;
                    float r = shape.radius;
                    for (int i = 0; i < segments; i++)
                    {
                        float a = (float)i / segments * MathF.PI * 2f;
                        var pt = shape.offset + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
                        points.Add(t != transform ? TransformPoint2D(t, pt) : pt);
                    }
                    break;
                case ColliderShapeType2D.Capsule:
                    int capSegments = 12;
                    float capRadius = shape.capsuleDirection == CapsuleDirection2D.Vertical ? shape.size.x * 0.5f : shape.size.y * 0.5f;
                    float capHalfHeight = shape.capsuleDirection == CapsuleDirection2D.Vertical ? shape.size.y * 0.5f - capRadius : shape.size.x * 0.5f - capRadius;
                    Vector2 capAxis = shape.capsuleDirection == CapsuleDirection2D.Vertical ? Vector2.up : Vector2.right;
                    Vector2 capPerp = shape.capsuleDirection == CapsuleDirection2D.Vertical ? Vector2.right : Vector2.up;
                    for (int i = 0; i <= capSegments / 2; i++)
                    {
                        float angle = (float)i / (capSegments / 2) * MathF.PI;
                        Vector2 dir = capPerp * Mathf.Cos(angle) + capAxis * Mathf.Sin(angle);
                        points.Add(t != transform ? TransformPoint2D(t, shape.offset + capAxis * capHalfHeight + dir * capRadius) : shape.offset + capAxis * capHalfHeight + dir * capRadius);
                    }
                    for (int i = capSegments / 2; i <= capSegments; i++)
                    {
                        float angle = MathF.PI + (float)(i - capSegments / 2) / (capSegments / 2) * MathF.PI;
                        Vector2 dir = capPerp * Mathf.Cos(angle) + capAxis * Mathf.Sin(angle);
                        points.Add(t != transform ? TransformPoint2D(t, shape.offset - capAxis * capHalfHeight + dir * capRadius) : shape.offset - capAxis * capHalfHeight + dir * capRadius);
                    }
                    break;
            }
        }
    }

    private static Vector2 TransformPoint2D(Transform t, Vector2 p)
    {
        var pos = t.position;
        var rot = t.rotation;
        var scl = t.lossyScale;
        var scaled = new Vector2(p.x * scl.x, p.y * scl.y);
        var rotated = rot * new Vector3(scaled.x, scaled.y, 0f);
        return new Vector2(pos.x + rotated.x, pos.y + rotated.y);
    }

    private static Vector2[] ComputeConvexHull(Vector2[] points)
    {
        if (points.Length < 3) return points;
        var sorted = new List<Vector2>(points);
        sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var hull = new List<Vector2>();
        foreach (var p in sorted)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        int lowerCount = hull.Count + 1;
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (hull.Count >= lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull.Count >= 3 ? hull.ToArray() : points;
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    public int GetPathPointCount(int index)
    {
        if (_geometryDirty) GenerateGeometry();
        if (index >= 0 && index < _paths.Count)
            return _paths[index]?.Length ?? 0;
        return 0;
    }

    public int GetPath(int index, Vector2[] points)
    {
        if (_geometryDirty) GenerateGeometry();
        if (points == null || index < 0 || index >= _paths.Count) return 0;
        var path = _paths[index];
        if (path == null) return 0;
        int count = Mathf.Min(points.Length, path.Length);
        for (int i = 0; i < count; i++)
        {
            points[i] = path[i];
        }
        return count;
    }

    public int GetPath(int index, List<Vector2> points)
    {
        if (_geometryDirty) GenerateGeometry();
        if (points == null || index < 0 || index >= _paths.Count) return 0;
        points.Clear();
        points.AddRange(_paths[index]);
        return _paths[index].Length;
    }

    public Vector2[] GetPath(int index)
    {
        if (_geometryDirty) GenerateGeometry();
        if (index < 0 || index >= _paths.Count) return Array.Empty<Vector2>();
        return (Vector2[])_paths[index].Clone();
    }

    public bool OverlapPoint(Vector2 point)
    {
        if (_geometryDirty) GenerateGeometry();
        Bounds b = bounds;
        if (!b.Contains(new Vector3(point.x, point.y, b.center.z)))
            return false;

        Vector2 localPoint = point - offset;
        if (transform != null)
        {
            localPoint = (Vector2)transform.InverseTransformPoint(new Vector3(point.x, point.y, 0f));
        }

        foreach (var path in _paths)
        {
            if (path == null || path.Length < 3) continue;
            if (Physics2D.s_world2D.PointInPolygon(localPoint, path))
                return true;
        }
        return false;
    }

    public int OverlapCollider(ContactFilter2D contactFilter, Collider2D[] results)
    {
        _ = contactFilter;
        if (results == null) return 0;
        int count = 0;
        foreach (var collider in Physics2D.s_world2D.GetColliders())
        {
            if (collider == null || collider == this || collider.IsDestroyed) continue;
            if (Physics2D.s_world2D.Intersect(this, collider))
            {
                if (count < results.Length)
                {
                    results[count] = collider;
                    count++;
                }
            }
        }
        return count;
    }

    public int OverlapCollider(ContactFilter2D contactFilter, List<Collider2D> results)
    {
        _ = contactFilter;
        if (results == null) return 0;
        results.Clear();
        int count = 0;
        foreach (var collider in Physics2D.s_world2D.GetColliders())
        {
            if (collider == null || collider == this || collider.IsDestroyed) continue;
            if (Physics2D.s_world2D.Intersect(this, collider))
            {
                results.Add(collider);
                count++;
            }
        }
        return count;
    }

    public new bool IsTouching(Collider2D otherCollider)
    {
        if (otherCollider == null || otherCollider == this) return false;
        return Physics2D.s_world2D.Intersect(this, otherCollider);
    }

    public new bool IsTouchingLayers(int layerMask = -1)
    {
        foreach (var collider in Physics2D.s_world2D.GetColliders())
        {
            if (collider == null || collider == this || collider.IsDestroyed) continue;
            if (!Physics2D.s_world2D.LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;
            if (Physics2D.s_world2D.Intersect(this, collider))
                return true;
        }
        return false;
    }

    internal override ColliderShape2D GetShape()
    {
        if (_geometryDirty || _generatedPoints.Length == 0) GenerateGeometry();
        return new ColliderShape2D(
            geometryType == CompositeCollider2DGeometryType.Outlines ? ColliderShapeType2D.Edge : ColliderShapeType2D.Polygon,
            offset, Vector2.one, edgeRadius, _generatedPoints,
            CapsuleDirection2D.Vertical, _paths);
    }
}

public enum CompositeCollider2DGeometryType
{
    Outlines = 0,
    Polygons = 1
}

public enum CompositeCollider2DGenerationType
{
    Synchronous = 0,
    WhenTriggered = 1,
    Manual = 2
}

public enum EffectorSelection2D
{
    Collider,
    Rigidbody
}
