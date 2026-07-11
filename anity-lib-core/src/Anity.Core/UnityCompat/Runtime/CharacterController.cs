using System;

namespace UnityEngine;

[Flags]
public enum CollisionFlags
{
    None = 0,
    Sides = 1,
    Above = 2,
    Below = 4
}

[RequireComponent(typeof(Transform))]
[AddComponentMenu("Physics/Character Controller")]
public class CharacterController : Collider
{
    private float _slopeLimit = 45f;
    private float _stepOffset = 0.3f;
    private float _skinWidth = 0.08f;
    private float _minMoveDistance = 0.001f;
    private Vector3 _center = new Vector3(0f, 1f, 0f);
    private float _height = 2f;
    private float _radius = 0.5f;
    private bool _isGrounded;
    private Vector3 _velocity;
    private Vector3 _moveDelta;
    private bool _detectCollisions = true;
    private CollisionFlags _collisionFlags;

    public float slopeLimit
    {
        get => _slopeLimit;
        set => _slopeLimit = Math.Clamp(value, 0f, 180f);
    }

    public float stepOffset
    {
        get => _stepOffset;
        set => _stepOffset = MathF.Max(0f, value);
    }

    public float skinWidth
    {
        get => _skinWidth;
        set => _skinWidth = MathF.Max(0f, value);
    }

    public float minMoveDistance
    {
        get => _minMoveDistance;
        set => _minMoveDistance = MathF.Max(0f, value);
    }

    public Vector3 center
    {
        get => _center;
        set => _center = value;
    }

    public float height
    {
        get => _height;
        set => _height = MathF.Max(0f, value);
    }

    public float radius
    {
        get => _radius;
        set => _radius = MathF.Max(0f, value);
    }

    public bool isGrounded => _isGrounded;
    public Vector3 velocity => _velocity;
    public CollisionFlags collisionFlags => _collisionFlags;
    public bool detectCollisions
    {
        get => _detectCollisions;
        set => _detectCollisions = value;
    }

    public override Bounds bounds
    {
        get
        {
            if (transform == null) return new Bounds(_center, new Vector3(_radius * 2f, _height, _radius * 2f));
            Vector3 scale = transform.lossyScale;
            float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
            float worldHeight = _height * MathF.Abs(scale.y);
            Vector3 worldCenter = transform.TransformPoint(_center);
            return new Bounds(worldCenter, new Vector3(worldRadius * 2f, worldHeight, worldRadius * 2f));
        }
    }

    public override ColliderShape GetShape()
    {
        if (transform == null)
            return new ColliderShape(ColliderShapeType.Capsule, _center, Vector3.one, _radius, _height, 1);
        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
        float worldHeight = MathF.Max(_height * MathF.Abs(scale.y), worldRadius * 2f);
        Vector3 worldCenter = transform.TransformPoint(_center);
        return new ColliderShape(ColliderShapeType.Capsule, worldCenter, Vector3.one, worldRadius, worldHeight, 1);
    }

    public override Vector3 ClosestPoint(Vector3 position)
    {
        if (transform == null) return position;
        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
        float worldHeight = MathF.Max(_height * MathF.Abs(scale.y), worldRadius * 2f);
        float halfH = worldHeight * 0.5f - worldRadius;
        Vector3 worldCenter = transform.TransformPoint(_center);
        Vector3 p0 = worldCenter - Vector3.up * halfH;
        Vector3 p1 = worldCenter + Vector3.up * halfH;
        Vector3 closest = ClosestPointOnSegment(position, p0, p1);
        Vector3 d = position - closest;
        float dMag = d.magnitude;
        if (dMag <= worldRadius) return position;
        return closest + d.normalized * worldRadius;
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

    public CollisionFlags Move(Vector3 motion)
    {
        _collisionFlags = CollisionFlags.None;
        _velocity = motion / MathF.Max(Time.deltaTime, 0.0001f);
        _isGrounded = false;

        if (transform == null || motion.magnitude < _minMoveDistance)
            return _collisionFlags;

        Vector3 currentPos = transform.position;
        Vector3 desiredPos = currentPos + motion;

        if (_detectCollisions)
        {
            Vector3 skinOffset = Vector3.up * _skinWidth;
            Bounds checkBounds = new Bounds((currentPos + desiredPos) * 0.5f + skinOffset,
                new Vector3(_radius * 2f + MathF.Abs(motion.x) + _skinWidth * 2f,
                    _height + MathF.Abs(motion.y) + _skinWidth * 2f,
                    _radius * 2f + MathF.Abs(motion.z) + _skinWidth * 2f));

            Vector3 remainingMotion = motion;
            Vector3 finalPos = currentPos;

            for (int step = 0; step < 3; step++)
            {
                if (remainingMotion.magnitude < 1e-6f) break;

                Vector3 stepPos = finalPos + remainingMotion;
                bool hitSomething = false;
                Vector3 hitNormal = Vector3.up;
                float hitDistance = float.MaxValue;

                if (Physics.SphereCast(finalPos + Vector3.up * (_height * 0.5f), _radius + _skinWidth,
                    remainingMotion.normalized, out RaycastHit hit, remainingMotion.magnitude + _skinWidth))
                {
                    hitSomething = true;
                    hitNormal = hit.normal;
                    hitDistance = hit.distance - _skinWidth;
                }

                if (!hitSomething)
                {
                    finalPos = stepPos;
                    break;
                }

                if (hitDistance < 0f) hitDistance = 0f;
                finalPos += remainingMotion.normalized * hitDistance;
                remainingMotion -= remainingMotion.normalized * hitDistance;

                float slopeAngle = MathF.Acos(MathF.Abs(Vector3.Dot(hitNormal, Vector3.up))) * (180f / MathF.PI);
                if (slopeAngle <= _slopeLimit || step == 0)
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(remainingMotion, hitNormal);
                    if (Vector3.Dot(hitNormal, Vector3.up) > 0.5f)
                    {
                        _isGrounded = true;
                        _collisionFlags |= CollisionFlags.Below;
                        slideDir.y = MathF.Max(0f, slideDir.y);
                    }
                    else if (Vector3.Dot(hitNormal, Vector3.up) < -0.5f)
                    {
                        _collisionFlags |= CollisionFlags.Above;
                        if (remainingMotion.y > 0f) remainingMotion.y = 0f;
                    }
                    else
                    {
                        _collisionFlags |= CollisionFlags.Sides;
                    }
                    remainingMotion = slideDir;
                }
                else
                {
                    remainingMotion = Vector3.zero;
                    if (Vector3.Dot(hitNormal, Vector3.up) > 0.5f)
                    {
                        _isGrounded = true;
                        _collisionFlags |= CollisionFlags.Below;
                    }
                    else if (Vector3.Dot(hitNormal, Vector3.up) < -0.5f)
                    {
                        _collisionFlags |= CollisionFlags.Above;
                    }
                    else
                    {
                        _collisionFlags |= CollisionFlags.Sides;
                    }
                }
            }

            transform.position = finalPos;
        }
        else
        {
            transform.position = desiredPos;
        }

        _moveDelta = motion;
        return _collisionFlags;
    }

    public bool SimpleMove(Vector3 speed)
    {
        Vector3 motion = speed * Time.deltaTime;
        motion += Physics.gravity * Time.deltaTime;
        Move(motion);
        _velocity = speed;
        _velocity.y = 0f;
        return _isGrounded;
    }
}
