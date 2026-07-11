using System;

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

public sealed class PointEffector2D : Effector2D
{
    public float forceMagnitude { get; set; }
    public float forceVariation { get; set; }
    public float distanceScale { get; set; } = 1f;
    public float drag { get; set; }
    public float angularDrag { get; set; }
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
}

public sealed class SurfaceEffector2D : Effector2D
{
    public float speed { get; set; } = 1f;
    public float speedVariation { get; set; }
    public float forceScale { get; set; } = 0.1f;
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
}

public sealed class CompositeCollider2D : Collider2D
{
    public CompositeCollider2D.GenerationType geometryType { get; set; } = CompositeCollider2D.GenerationType.Polygons;
    public float vertexDistance { get; set; } = 0.0005f;
    public float offsetDistance { get; set; } = 0.005f;
    private List<Vector2[]> _paths = new();
    private Vector2[] _generatedPoints = Array.Empty<Vector2>();

    public int pathCount => _paths.Count;

    public void GenerateGeometry()
    {
        _paths.Clear();
        if (transform == null)
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

        var colliders = GetComponents<Collider2D>();
        var allPoints = new List<Vector2>();
        foreach (var col in colliders)
        {
            if (col == this || col == null || !col.enabled) continue;
            var shape = col.GetShape();
            switch (shape.type)
            {
                case ColliderShapeType2D.Box:
                    var half = shape.size * 0.5f;
                    allPoints.Add(shape.offset + new Vector2(-half.x, -half.y));
                    allPoints.Add(shape.offset + new Vector2(half.x, -half.y));
                    allPoints.Add(shape.offset + new Vector2(half.x, half.y));
                    allPoints.Add(shape.offset + new Vector2(-half.x, half.y));
                    break;
                case ColliderShapeType2D.Polygon:
                case ColliderShapeType2D.Edge:
                    if (shape.points != null && shape.points.Length > 0)
                        allPoints.AddRange(shape.points);
                    break;
            }
        }

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
            _generatedPoints = allPoints.ToArray();
        }
        _paths.Add(_generatedPoints);
    }

    public int GetPath(int index, List<Vector2> points)
    {
        if (points == null || index < 0 || index >= _paths.Count) return 0;
        points.Clear();
        points.AddRange(_paths[index]);
        return _paths[index].Length;
    }

    public Vector2[] GetPath(int index)
    {
        if (index < 0 || index >= _paths.Count) return Array.Empty<Vector2>();
        return (Vector2[])_paths[index].Clone();
    }

    public enum GenerationType
    {
        Polygons,
        Outlines
    }

    internal override ColliderShape2D GetShape()
    {
        if (_generatedPoints.Length == 0) GenerateGeometry();
        return new ColliderShape2D(ColliderShapeType2D.Polygon, offset, Vector2.one, 0f, _generatedPoints, CapsuleDirection2D.Vertical, _paths);
    }
}

public enum EffectorSelection2D
{
    Collider,
    Rigidbody
}
