using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class PhysicsSceneExtensions
{
    public static bool Simulate(this PhysicsScene scene, float step)
    {
        Physics.s_world.Simulate(step);
        return true;
    }
}

public static class Physics
{
    internal static PhysicsWorld s_world = new PhysicsWorld();

    private static bool _transformsSynced;
    public static Vector3 gravity
    {
        get => s_world.gravity;
        set => s_world.gravity = value;
    }

    public static bool autoSimulation
    {
        get => s_world.autoSimulation;
        set => s_world.autoSimulation = value;
    }

    public static float bounceThreshold
    {
        get => s_world.bounceThreshold;
        set => s_world.bounceThreshold = value;
    }

    public static float sleepThreshold
    {
        get => s_world.sleepThreshold;
        set => s_world.sleepThreshold = value;
    }

    public static float defaultContactOffset
    {
        get => s_world.defaultContactOffset;
        set => s_world.defaultContactOffset = value;
    }

    public static int defaultSolverIterations
    {
        get => s_world.defaultSolverIterations;
        set => s_world.defaultSolverIterations = value;
    }

    public static PhysicsScene defaultPhysicsScene => new PhysicsScene();

    public static void IgnoreLayerCollision(int layer1, int layer2, bool ignore = true)
    {
        s_world.SetIgnoreLayerCollision(layer1, layer2, ignore);
    }

    public static bool GetIgnoreLayerCollision(int layer1, int layer2)
    {
        return s_world.GetIgnoreLayerCollision(layer1, layer2);
    }

    public static bool IgnoreCollision(Collider collider1, Collider collider2, bool ignore = true)
    {
        s_world.SetIgnoreCollision(collider1, collider2, ignore);
        return true;
    }

    public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return Raycast(origin, direction, out _, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth)
    {
        return Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.RaycastAll(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (results == null || results.Length == 0) return 0;
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        var hits = s_world.RaycastAll(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
        int count = Math.Min(hits.Length, results.Length);
        for (int i = 0; i < count; i++) results[i] = hits[i];
        return count;
    }

    public static bool Linecast(Vector3 start, Vector3 end, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return Linecast(start, end, out _, layerMask, queryTriggerInteraction);
    }

    public static bool Linecast(Vector3 start, Vector3 end, out RaycastHit hitInfo, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 1e-6f)
        {
            hitInfo = default;
            return false;
        }
        return Raycast(start, dir, out hitInfo, dist, layerMask, queryTriggerInteraction);
    }

    public static RaycastHit[] LinecastAll(Vector3 start, Vector3 end, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 1e-6f) return Array.Empty<RaycastHit>();
        return RaycastAll(start, dir, dist, layerMask, queryTriggerInteraction);
    }

    public static int LinecastNonAlloc(Vector3 start, Vector3 end, RaycastHit[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 1e-6f || results == null || results.Length == 0) return 0;
        return RaycastNonAlloc(start, dir, results, dist, layerMask, queryTriggerInteraction);
    }

    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return SphereCast(origin, radius, direction, out _, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool SphereCast(Ray ray, float radius, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.SphereCast(ray.origin, radius, ray.direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, out RaycastHit hitInfo)
    {
        return SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static RaycastHit[] SphereCastAll(Vector3 origin, float radius, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.SphereCastAll(origin, radius, direction, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, RaycastHit[] results, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (results == null || results.Length == 0) return 0;
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        var hits = s_world.SphereCastAll(origin, radius, direction, maxDistance, layerMask, queryTriggerInteraction);
        int count = Math.Min(hits.Length, results.Length);
        for (int i = 0; i < count; i++) results[i] = hits[i];
        return count;
    }

    public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, RaycastHit[] results)
    {
        return SphereCastNonAlloc(origin, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCast(center, halfExtents, direction, out _, Quaternion.identity, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCast(center, halfExtents, direction, out hitInfo, Quaternion.identity, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, Quaternion orientation, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.BoxCast(center, halfExtents, direction, out hitInfo, maxDistance, orientation, layerMask, queryTriggerInteraction);
    }

    public static bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCast(center, halfExtents, direction, out _, orientation, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCast(center, halfExtents, direction, out hitInfo, orientation, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static RaycastHit[] BoxCastAll(Vector3 center, Vector3 halfExtents, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCastAll(center, halfExtents, direction, Quaternion.identity, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static RaycastHit[] BoxCastAll(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        if (s_world.BoxCast(center, halfExtents, direction, out var hit, maxDistance, orientation, layerMask, queryTriggerInteraction))
            return new[] { hit };
        return Array.Empty<RaycastHit>();
    }

    public static int BoxCastNonAlloc(Vector3 center, Vector3 halfExtents, Vector3 direction, RaycastHit[] results, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCastNonAlloc(center, halfExtents, direction, results, Quaternion.identity, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static int BoxCastNonAlloc(Vector3 center, Vector3 halfExtents, Vector3 direction, RaycastHit[] results, Quaternion orientation, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (results == null || results.Length == 0) return 0;
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        if (s_world.BoxCast(center, halfExtents, direction, out var hit, maxDistance, orientation, layerMask, queryTriggerInteraction))
        {
            results[0] = hit;
            return 1;
        }
        return 0;
    }

    public static int BoxCastNonAlloc(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction, RaycastHit[] results, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return BoxCastNonAlloc(center, halfExtents, direction, results, orientation, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return CapsuleCast(point1, point2, radius, direction, out _, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        return s_world.CapsuleCast(point1, point2, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, out RaycastHit hitInfo)
    {
        return CapsuleCast(point1, point2, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static RaycastHit[] CapsuleCastAll(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        if (s_world.CapsuleCast(point1, point2, radius, direction, out var hit, maxDistance, layerMask, queryTriggerInteraction))
            return new[] { hit };
        return Array.Empty<RaycastHit>();
    }

    public static int CapsuleCastNonAlloc(Vector3 point1, Vector3 point2, float radius, Vector3 direction, RaycastHit[] results, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (results == null || results.Length == 0) return 0;
        if (float.IsPositiveInfinity(maxDistance)) maxDistance = 1e8f;
        if (s_world.CapsuleCast(point1, point2, radius, direction, out var hit, maxDistance, layerMask, queryTriggerInteraction))
        {
            results[0] = hit;
            return 1;
        }
        return 0;
    }

    public static int CapsuleCastNonAlloc(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, RaycastHit[] results)
    {
        return CapsuleCastNonAlloc(point1, point2, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool CheckSphere(Vector3 position, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.CheckSphere(position, radius, layerMask, queryTriggerInteraction);
    }

    public static bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation = default, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.CheckBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction);
    }

    public static bool CheckCapsule(Vector3 start, Vector3 end, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.CheckCapsule(start, end, radius, layerMask, queryTriggerInteraction);
    }

    public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.OverlapSphere(position, radius, layerMask, queryTriggerInteraction);
    }

    public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.OverlapSphereNonAlloc(position, radius, results, layerMask, queryTriggerInteraction);
    }

    public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation = default, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.OverlapBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction);
    }

    public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation = default, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.OverlapBoxNonAlloc(center, halfExtents, results, orientation, layerMask, queryTriggerInteraction);
    }

    public static Collider[] OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.OverlapCapsule(point0, point1, radius, layerMask, queryTriggerInteraction);
    }

    public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        return s_world.OverlapCapsuleNonAlloc(point0, point1, radius, results, layerMask, queryTriggerInteraction);
    }

    public static int GetContacts(Collider collider, Collider[] results)
    {
        return s_world.GetContacts(collider, results);
    }

    public static void SyncTransforms()
    {
        _transformsSynced = true;
    }

    public static void Simulate(float deltaTime)
    {
        s_world.Simulate(deltaTime);
    }

    public static void StepSimulation(float deltaTime)
    {
        Simulate(deltaTime);
    }

    public static bool ComputePenetration(Collider colliderA, Vector3 positionA, Quaternion rotationA, Collider colliderB, Vector3 positionB, Quaternion rotationB, out Vector3 direction, out float distance)
    {
        direction = Vector3.zero;
        distance = 0f;
        return false;
    }

    public static Vector3 ClosestPoint(Vector3 point, Collider collider, Vector3 position, Quaternion rotation)
    {
        if (collider == null) return point;
        var bounds = collider.bounds;
        return new Vector3(
            Math.Clamp(point.x, bounds.min.x, bounds.max.x),
            Math.Clamp(point.y, bounds.min.y, bounds.max.y),
            Math.Clamp(point.z, bounds.min.z, bounds.max.z));
    }
}
