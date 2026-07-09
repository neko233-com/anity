using System;

namespace UnityEngine;

/// <summary>
/// PhysicsScene represents a physics scene that can be queried independently.
/// </summary>
public struct PhysicsScene : IEquatable<PhysicsScene>
{
    private int _handle;

    public bool IsValid() => _handle != 0;
    public bool IsEmpty() => _handle == 0;

    public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        hitInfo = default;
        return false;
    }

    public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask)
    {
        hitInfo = default;
        return false;
    }

    public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance)
    {
        hitInfo = default;
        return false;
    }

    public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo)
    {
        hitInfo = default;
        return false;
    }

    public bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, Quaternion orientation = default, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        hitInfo = default;
        return false;
    }

    public bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        hitInfo = default;
        return false;
    }

    public bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        hitInfo = default;
        return false;
    }

    public bool OverlapSphere(Vector3 position, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return false;
    }

    public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return 0;
    }

    public bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        hitInfo = default;
        return false;
    }

    public int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        return 0;
    }

    public int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        return 0;
    }

    public bool Equals(PhysicsScene other) => _handle == other._handle;
    public override bool Equals(object? obj) => obj is PhysicsScene other && Equals(other);
    public override int GetHashCode() => _handle;
    public static bool operator ==(PhysicsScene left, PhysicsScene right) => left.Equals(right);
    public static bool operator !=(PhysicsScene left, PhysicsScene right) => !left.Equals(right);
}

/// <summary>
/// PhysicsScene2D represents a 2D physics scene that can be queried independently.
/// </summary>
public struct PhysicsScene2D : IEquatable<PhysicsScene2D>
{
    private int _handle;

    public bool IsValid() => _handle != 0;
    public bool IsEmpty() => _handle == 0;

    public bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance = float.PositiveInfinity, int layerMask = -1)
    {
        hitInfo = default;
        return false;
    }

    public bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance)
    {
        hitInfo = default;
        return false;
    }

    public bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo)
    {
        hitInfo = default;
        return false;
    }

    public bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance = float.PositiveInfinity, int layerMask = -1)
    {
        hitInfo = default;
        return false;
    }

    public bool CircleCast(Vector2 origin, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance = float.PositiveInfinity, int layerMask = -1)
    {
        hitInfo = default;
        return false;
    }

    public bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D capsuleDirection, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance = float.PositiveInfinity, int layerMask = -1)
    {
        hitInfo = default;
        return false;
    }

    public bool OverlapCircle(Vector2 point, float radius, int layerMask = -1)
    {
        return false;
    }

    public int OverlapCircleNonAlloc(Vector2 point, float radius, Collider2D[] results, int layerMask = -1, float minDepth = float.PositiveInfinity, float maxDepth = float.PositiveInfinity)
    {
        return 0;
    }

    public bool OverlapPoint(Vector2 point, int layerMask = -1)
    {
        return false;
    }

    public int OverlapPointNonAlloc(Vector2 point, Collider2D[] results, int layerMask = -1, float minDepth = float.PositiveInfinity, float maxDepth = float.PositiveInfinity)
    {
        return 0;
    }

    public int OverlapAreaNonAlloc(Vector2 pointA, Vector2 pointB, Collider2D[] results, int layerMask = -1, float minDepth = float.PositiveInfinity, float maxDepth = float.PositiveInfinity)
    {
        return 0;
    }

    public bool Equals(PhysicsScene2D other) => _handle == other._handle;
    public override bool Equals(object? obj) => obj is PhysicsScene2D other && Equals(other);
    public override int GetHashCode() => _handle;
    public static bool operator ==(PhysicsScene2D left, PhysicsScene2D right) => left.Equals(right);
    public static bool operator !=(PhysicsScene2D left, PhysicsScene2D right) => !left.Equals(right);
}

/// <summary>
/// Capsule direction for 2D physics.
/// </summary>
public enum CapsuleDirection2D
{
    Vertical,
    Horizontal
}
