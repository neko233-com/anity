using System;

namespace UnityEngine;

/// <summary>
/// Unity CharacterController component for character movement.
/// </summary>
[RequireComponent(typeof(Transform))]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Physics/Character Controller")]
public class CharacterController : Collider
{
    private float _slopeLimit = 45.0f;
    private float _stepOffset = 0.3f;
    private float _skinWidth = 0.08f;
    private float _minMoveDistance = 0.001f;
    private float _center_y = 1.0f;
    private float _height = 2.0f;
    private float _radius = 0.5f;
    private bool _isGrounded;
    private Vector3 _velocity;
    private float _verticalVelocity;

    public float slopeLimit
    {
        get => _slopeLimit;
        set => _slopeLimit = value;
    }

    public float stepOffset
    {
        get => _stepOffset;
        set => _stepOffset = value;
    }

    public float skinWidth
    {
        get => _skinWidth;
        set => _skinWidth = value;
    }

    public float minMoveDistance
    {
        get => _minMoveDistance;
        set => _minMoveDistance = value;
    }

    public Vector3 center
    {
        get => new Vector3(0, _center_y, 0);
        set => _center_y = value.y;
    }

    public float height
    {
        get => _height;
        set => _height = value;
    }

    public float radius
    {
        get => _radius;
        set => _radius = value;
    }

    public bool isGrounded => _isGrounded;
    public Vector3 velocity => _velocity;
    public CollisionFlags collisionFlags { get; private set; }

    public CollisionFlags Move(Vector3 motion)
    {
        _velocity = motion;
        _isGrounded = motion.y <= 0;
        collisionFlags = CollisionFlags.None;
        return CollisionFlags.None;
    }

    public bool SimpleMove(Vector3 speed)
    {
        _velocity = speed;
        _isGrounded = true;
        return true;
    }

    public bool isGrounded2 => _isGrounded;
    public CollisionFlags collisionFlags2 => collisionFlags;
}

/// <summary>
/// Collision flags from CharacterController.Move.
/// </summary>
[Flags]
public enum CollisionFlags
{
    None = 0,
    Sides = 1,
    Above = 2,
    Below = 4
}
