using System;
using System.Collections.Generic;

namespace UnityEngine;

public enum ColliderShapeType
{
    Sphere,
    Box,
    Capsule
}

public struct ColliderShape
{
    public ColliderShapeType Type;
    public Vector3 Center;
    public Vector3 Size;
    public float Radius;
    public float Height;
    public int Direction;

    public ColliderShape(ColliderShapeType type, Vector3 center, Vector3 size, float radius, float height, int direction)
    {
        Type = type;
        Center = center;
        Size = size;
        Radius = radius;
        Height = height;
        Direction = direction;
    }
}

internal readonly struct CollisionPair : IEquatable<CollisionPair>
{
    public readonly Collider A;
    public readonly Collider B;

    public CollisionPair(Collider a, Collider b)
    {
        A = a.GetInstanceID() < b.GetInstanceID() ? a : b;
        B = a.GetInstanceID() < b.GetInstanceID() ? b : a;
    }

    public bool Equals(CollisionPair other) => A == other.A && B == other.B;
    public override bool Equals(object obj) => obj is CollisionPair other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(A?.GetInstanceID() ?? 0, B?.GetInstanceID() ?? 0);
    public static bool operator ==(CollisionPair left, CollisionPair right) => left.Equals(right);
    public static bool operator !=(CollisionPair left, CollisionPair right) => !left.Equals(right);
}

internal static class PhysicsWorld
{
    private static readonly List<Collider> _colliders = new();
    private static readonly List<Rigidbody> _rigidbodies = new();
    private static readonly List<Joint> _joints = new();
    private static readonly List<WheelCollider> _wheels = new();
    private static readonly HashSet<CollisionPair> _collisionStates = new();
    private static readonly HashSet<CollisionPair> _triggerStates = new();
    private static readonly Dictionary<(Collider, Collider), bool> _ignoreCollisionPairs = new();
    private static bool[,] _layerMatrix = new bool[32, 32];
    private static Vector3 _gravity = new(0f, -9.81f, 0f);
    private static bool _autoSimulation = true;
    private static float _bounceThreshold = 2f;
    private static float _sleepThreshold = 0.005f;
    private static float _defaultContactOffset = 0.01f;
    private static int _defaultSolverIterations = 6;

    public static Vector3 gravity { get => _gravity; set => _gravity = value; }
    public static bool autoSimulation { get => _autoSimulation; set => _autoSimulation = value; }
    public static float bounceThreshold { get => _bounceThreshold; set => _bounceThreshold = value; }
    public static float sleepThreshold { get => _sleepThreshold; set => _sleepThreshold = value; }
    public static float defaultContactOffset { get => _defaultContactOffset; set => _defaultContactOffset = value; }
    public static int defaultSolverIterations { get => _defaultSolverIterations; set => _defaultSolverIterations = value; }

    static PhysicsWorld()
    {
        for (int i = 0; i < 32; i++)
            for (int j = 0; j < 32; j++)
                _layerMatrix[i, j] = true;
    }

    public static void Register(Collider c) => RegisterCollider(c);

    public static void RegisterCollider(Collider c)
    {
        if (!_colliders.Contains(c)) _colliders.Add(c);
    }

    public static void UnregisterCollider(Collider c)
    {
        _colliders.Remove(c);
    }

    public static void Register(Rigidbody rb) => RegisterRigidbody(rb);

    public static void RegisterRigidbody(Rigidbody rb)
    {
        if (!_rigidbodies.Contains(rb)) _rigidbodies.Add(rb);
    }

    public static void UnregisterRigidbody(Rigidbody rb)
    {
        _rigidbodies.Remove(rb);
    }

    public static void RegisterJoint(Joint joint)
    {
        if (!_joints.Contains(joint)) _joints.Add(joint);
    }

    public static void UnregisterJoint(Joint joint)
    {
        _joints.Remove(joint);
    }

    public static void RegisterWheel(WheelCollider wheel)
    {
        if (!_wheels.Contains(wheel)) _wheels.Add(wheel);
    }

    public static void UnregisterWheel(WheelCollider wheel)
    {
        _wheels.Remove(wheel);
    }

    public static bool GetIgnoreLayerCollision(int layer1, int layer2)
    {
        layer1 = Math.Clamp(layer1, 0, 31);
        layer2 = Math.Clamp(layer2, 0, 31);
        return !_layerMatrix[layer1, layer2];
    }

    public static void SetIgnoreLayerCollision(int layer1, int layer2, bool ignore)
    {
        layer1 = Math.Clamp(layer1, 0, 31);
        layer2 = Math.Clamp(layer2, 0, 31);
        _layerMatrix[layer1, layer2] = !ignore;
        _layerMatrix[layer2, layer1] = !ignore;
    }

    public static bool GetIgnoreCollision(Collider a, Collider b)
    {
        if (a == null || b == null) return false;
        return _ignoreCollisionPairs.TryGetValue((a, b), out var ignore) && ignore;
    }

    public static void SetIgnoreCollision(Collider a, Collider b, bool ignore)
    {
        if (a == null || b == null) return;
        _ignoreCollisionPairs[(a, b)] = ignore;
        _ignoreCollisionPairs[(b, a)] = ignore;
    }

    private static Vector3 TransformPoint(Vector3 localPos, Transform t)
    {
        if (t == null) return localPos;
        return t.position + t.rotation * localPos;
    }

    private static ColliderShape GetWorldShape(Collider c)
    {
        return c.GetShape();
    }

    public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask)
    {
        return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
    }

    public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        hitInfo = default;
        direction = direction.normalized;
        float closestDist = maxDistance;
        Collider closestCollider = null;
        Vector3 closestPoint = default;
        Vector3 closestNormal = Vector3.up;

        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (queryTriggerInteraction == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;

            var shape = GetWorldShape(c);
            if (RaycastShape(origin, direction, shape, closestDist, out float dist, out Vector3 pt, out Vector3 n))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestCollider = c;
                    closestPoint = pt;
                    closestNormal = n;
                }
            }
        }

        if (closestCollider == null) return false;
        hitInfo = new RaycastHit
        {
            collider = closestCollider,
            distance = closestDist,
            point = closestPoint,
            normal = closestNormal,
            rigidbody = closestCollider.attachedRigidbody,
            transform = closestCollider.transform
        };
        return true;
    }

    public static RaycastHit[] RaycastAll(Ray ray, float maxDistance, int layerMask)
    {
        return RaycastAll(ray.origin, ray.direction, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
    }

    public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
    {
        var hits = new List<RaycastHit>();
        direction = direction.normalized;
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (queryTriggerInteraction == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;

            var shape = GetWorldShape(c);
            if (RaycastShape(origin, direction, shape, maxDistance, out float dist, out Vector3 pt, out Vector3 n))
            {
                hits.Add(new RaycastHit
                {
                    collider = c,
                    distance = dist,
                    point = pt,
                    normal = n,
                    rigidbody = c.attachedRigidbody,
                    transform = c.transform
                });
            }
        }
        return hits.ToArray();
    }

    private static bool RaycastShape(Vector3 origin, Vector3 dir, ColliderShape shape, float maxDist, out float dist, out Vector3 point, out Vector3 normal)
    {
        dist = maxDist;
        point = default;
        normal = Vector3.up;

        if (shape.Type == ColliderShapeType.Sphere)
            return RaycastSphere(origin, dir, shape.Center, shape.Radius, maxDist, out dist, out point, out normal);
        if (shape.Type == ColliderShapeType.Box)
            return RaycastBox(origin, dir, shape.Center, shape.Size * 0.5f, maxDist, out dist, out point, out normal);
        if (shape.Type == ColliderShapeType.Capsule)
            return RaycastCapsule(origin, dir, shape, maxDist, out dist, out point, out normal);
        return false;
    }

    private static bool RaycastSphere(Vector3 origin, Vector3 dir, Vector3 center, float radius, float maxDist, out float dist, out Vector3 point, out Vector3 normal)
    {
        dist = maxDist;
        point = default;
        normal = Vector3.up;
        Vector3 m = origin - center;
        float b = Vector3.Dot(m, dir);
        float c = Vector3.Dot(m, m) - radius * radius;
        if (c > 0f && b > 0f) return false;
        float discr = b * b - c;
        if (discr < 0f) return false;
        float t = -b - MathF.Sqrt(discr);
        if (t < 0f) t = 0f;
        if (t > maxDist) return false;
        dist = t;
        point = origin + dir * t;
        normal = (point - center).normalized;
        return true;
    }

    private static bool RaycastBox(Vector3 origin, Vector3 dir, Vector3 center, Vector3 halfExtents, float maxDist, out float dist, out Vector3 point, out Vector3 normal)
    {
        dist = maxDist;
        point = default;
        normal = Vector3.up;
        Vector3 min = center - halfExtents;
        Vector3 max = center + halfExtents;
        float tmin = 0f, tmax = maxDist;
        Vector3 hitNormal = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            float o = origin[i];
            float d = dir[i];
            float mn = min[i];
            float mx = max[i];
            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < mn || o > mx) return false;
            }
            else
            {
                float inv = 1f / d;
                float t1 = (mn - o) * inv;
                float t2 = (mx - o) * inv;
                Vector3 n = Vector3.zero;
                if (t1 > t2) { (t1, t2) = (t2, t1); }
                if (d > 0) n[i] = t1 > tmin ? -1f : n[i];
                else n[i] = t1 > tmin ? 1f : n[i];
                if (t1 > tmin) { tmin = t1; hitNormal = n; }
                tmax = MathF.Min(tmax, t2);
                if (tmin > tmax) return false;
            }
        }

        if (tmin > maxDist) return false;
        dist = tmin;
        point = origin + dir * tmin;
        normal = hitNormal.magnitude > 1e-6f ? hitNormal.normalized : -dir;
        return true;
    }

    private static bool RaycastCapsule(Vector3 origin, Vector3 dir, ColliderShape shape, float maxDist, out float dist, out Vector3 point, out Vector3 normal)
    {
        dist = maxDist;
        point = default;
        normal = Vector3.up;
        Vector3 axis = shape.Direction == 0 ? Vector3.right : shape.Direction == 1 ? Vector3.up : Vector3.forward;
        float halfH = shape.Height * 0.5f - shape.Radius;
        Vector3 p0 = shape.Center - axis * halfH;
        Vector3 p1 = shape.Center + axis * halfH;
        float bestT = maxDist;
        Vector3 bestPt = default, bestN = Vector3.up;

        if (RaycastSphere(origin, dir, p0, shape.Radius, maxDist, out float t0, out Vector3 pt0, out Vector3 n0) && t0 < bestT)
        { bestT = t0; bestPt = pt0; bestN = n0; }
        if (RaycastSphere(origin, dir, p1, shape.Radius, maxDist, out float t1, out Vector3 pt1, out Vector3 n1) && t1 < bestT)
        { bestT = t1; bestPt = pt1; bestN = n1; }

        Vector3 cyl = p1 - p0;
        float cylLen = cyl.magnitude;
        if (cylLen > 1e-6f)
        {
            Vector3 cylDir = cyl / cylLen;
            Vector3 rel = origin - p0;
            float a = Vector3.Dot(dir, dir) - Vector3.Dot(dir, cylDir) * Vector3.Dot(dir, cylDir);
            float b = 2f * (Vector3.Dot(dir, rel) - Vector3.Dot(dir, cylDir) * Vector3.Dot(rel, cylDir));
            float c = Vector3.Dot(rel, rel) - Vector3.Dot(rel, cylDir) * Vector3.Dot(rel, cylDir) - shape.Radius * shape.Radius;
            if (MathF.Abs(a) > 1e-6f)
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sqrtD = MathF.Sqrt(disc);
                    float t = (-b - sqrtD) / (2f * a);
                    if (t >= 0f && t < bestT)
                    {
                        Vector3 hp = origin + dir * t;
                        float proj = Vector3.Dot(hp - p0, cylDir);
                        if (proj >= 0f && proj <= cylLen)
                        {
                            Vector3 cp = p0 + cylDir * proj;
                            bestT = t;
                            bestPt = hp;
                            bestN = (hp - cp).normalized;
                        }
                    }
                }
            }
        }

        if (bestT >= maxDist) return false;
        dist = bestT;
        point = bestPt;
        normal = bestN;
        return true;
    }

    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction qti)
    {
        hitInfo = default;
        direction = direction.normalized;
        float closest = maxDistance;
        Collider bestC = null;
        Vector3 bestPt = default, bestN = Vector3.up;

        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            var shape = GetWorldShape(c);
            if (SphereCastShape(origin, radius, direction, shape, maxDistance, out float t, out Vector3 pt, out Vector3 n))
            {
                if (t < closest) { closest = t; bestC = c; bestPt = pt; bestN = n; }
            }
        }
        if (bestC == null) return false;
        hitInfo = new RaycastHit { collider = bestC, distance = closest, point = bestPt, normal = bestN, rigidbody = bestC.attachedRigidbody, transform = bestC.transform };
        return true;
    }

    public static RaycastHit[] SphereCastAll(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction qti)
    {
        direction = direction.normalized;
        var hits = new List<RaycastHit>();
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            var shape = GetWorldShape(c);
            if (SphereCastShape(origin, radius, direction, shape, maxDistance, out float t, out Vector3 pt, out Vector3 n))
            {
                hits.Add(new RaycastHit { collider = c, distance = t, point = pt, normal = n, rigidbody = c.attachedRigidbody, transform = c.transform });
            }
        }
        return hits.ToArray();
    }

    private static bool SphereCastShape(Vector3 origin, float radius, Vector3 dir, ColliderShape shape, float maxDist, out float t, out Vector3 pt, out Vector3 n)
    {
        t = maxDist; pt = default; n = Vector3.up;
        if (shape.Type == ColliderShapeType.Sphere)
        {
            float sumR = radius + shape.Radius;
            if (RaycastSphere(origin, dir, shape.Center, sumR, maxDist, out float td, out Vector3 p, out Vector3 nm))
            {
                t = td; pt = p - nm * radius; n = nm;
                return true;
            }
        }
        if (shape.Type == ColliderShapeType.Box)
        {
            Vector3 he = shape.Size * 0.5f + new Vector3(radius, radius, radius);
            if (RaycastBox(origin, dir, shape.Center, he, maxDist, out float td, out Vector3 p, out Vector3 nm))
            {
                t = td; pt = p - nm * radius; n = nm;
                return true;
            }
        }
        if (shape.Type == ColliderShapeType.Capsule)
        {
            float sumR = radius + shape.Radius;
            var expanded = new ColliderShape(ColliderShapeType.Capsule, shape.Center, shape.Size, sumR, shape.Height, shape.Direction);
            if (RaycastCapsule(origin, dir, expanded, maxDist, out float td, out Vector3 p, out Vector3 nm))
            {
                t = td; pt = p - nm * radius; n = nm;
                return true;
            }
        }
        return false;
    }

    public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, float maxDistance, Quaternion orientation, int layerMask, QueryTriggerInteraction qti)
    {
        hitInfo = default;
        direction = direction.normalized;
        float closest = maxDistance;
        Collider bestC = null;
        Vector3 bestPt = default, bestN = Vector3.up;

        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            var shape = GetWorldShape(c);
            if (BoxCastShape(center, halfExtents, direction, shape, maxDistance, out float t, out Vector3 pt, out Vector3 n))
            {
                if (t < closest) { closest = t; bestC = c; bestPt = pt; bestN = n; }
            }
        }
        if (bestC == null) return false;
        hitInfo = new RaycastHit { collider = bestC, distance = closest, point = bestPt, normal = bestN, rigidbody = bestC.attachedRigidbody, transform = bestC.transform };
        return true;
    }

    private static bool BoxCastShape(Vector3 center, Vector3 he, Vector3 dir, ColliderShape shape, float maxDist, out float t, out Vector3 pt, out Vector3 n)
    {
        t = maxDist; pt = default; n = Vector3.up;
        if (shape.Type == ColliderShapeType.Sphere)
        {
            Vector3 expandedHe = he + new Vector3(shape.Radius, shape.Radius, shape.Radius);
            if (RaycastBox(center, dir, shape.Center, expandedHe, maxDist, out float td, out Vector3 p, out Vector3 nm))
            {
                t = td; pt = p; n = nm;
                return true;
            }
        }
        if (shape.Type == ColliderShapeType.Box)
        {
            Vector3 she = shape.Size * 0.5f;
            Vector3 expandedHe = new Vector3(he.x + she.x, he.y + she.y, he.z + she.z);
            if (RaycastBox(center, dir, shape.Center, expandedHe, maxDist, out float td, out Vector3 p, out Vector3 nm))
            {
                t = td; pt = p; n = nm;
                return true;
            }
        }
        if (shape.Type == ColliderShapeType.Capsule)
        {
            Vector3 expandedHe = he + new Vector3(shape.Radius, shape.Radius, shape.Radius);
            if (RaycastBox(center, dir, shape.Center, expandedHe, maxDist, out float td, out Vector3 p, out Vector3 nm))
            {
                t = td; pt = p; n = nm;
                return true;
            }
        }
        return false;
    }

    public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction qti)
    {
        hitInfo = default;
        direction = direction.normalized;
        float closest = maxDistance;
        Collider bestC = null;
        Vector3 bestPt = default, bestN = Vector3.up;

        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            var shape = GetWorldShape(c);
            if (CapsuleCastShape(point1, point2, radius, direction, shape, maxDistance, out float t, out Vector3 pt, out Vector3 n))
            {
                if (t < closest) { closest = t; bestC = c; bestPt = pt; bestN = n; }
            }
        }
        if (bestC == null) return false;
        hitInfo = new RaycastHit { collider = bestC, distance = closest, point = bestPt, normal = bestN, rigidbody = bestC.attachedRigidbody, transform = bestC.transform };
        return true;
    }

    private static bool CapsuleCastShape(Vector3 point0, Vector3 point1, float radius, Vector3 direction, ColliderShape shape, float maxDist, out float t, out Vector3 pt, out Vector3 n)
    {
        t = maxDist; pt = default; n = Vector3.up;
        Vector3 center = (point0 + point1) * 0.5f;
        float height = Vector3.Distance(point0, point1) + radius * 2f;
        int dir = 1;
        Vector3 axis = (point1 - point0).normalized;
        if (MathF.Abs(Vector3.Dot(axis, Vector3.right)) > 0.9f) dir = 0;
        else if (MathF.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.9f) dir = 2;
        var capShape = new ColliderShape(ColliderShapeType.Capsule, center, Vector3.one, radius, height, dir);
        if (Intersect(capShape, shape, out Vector3 nm, out float pen))
        {
            t = 0f;
            pt = center;
            n = nm;
            return true;
        }
        return false;
    }

    public static Collider[] OverlapSphere(Vector3 center, float radius, int layerMask, QueryTriggerInteraction qti)
    {
        var list = new List<Collider>();
        OverlapSphereInternal(center, radius, layerMask, qti, list);
        return list.ToArray();
    }

    public static int OverlapSphereNonAlloc(Vector3 center, float radius, Collider[] results, int layerMask, QueryTriggerInteraction qti)
    {
        var list = new List<Collider>();
        OverlapSphereInternal(center, radius, layerMask, qti, list);
        int count = Math.Min(list.Count, results?.Length ?? 0);
        for (int i = 0; i < count; i++) results[i] = list[i];
        return count;
    }

    public static bool CheckSphere(Vector3 center, float radius, int layerMask, QueryTriggerInteraction qti)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            var shape = GetWorldShape(c);
            if (OverlapShapePoint(center, radius, shape)) return true;
        }
        return false;
    }

    public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, QueryTriggerInteraction qti)
    {
        var list = new List<Collider>();
        OverlapBoxInternal(center, halfExtents, layerMask, qti, list);
        return list.ToArray();
    }

    public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation, int layerMask, QueryTriggerInteraction qti)
    {
        var list = new List<Collider>();
        OverlapBoxInternal(center, halfExtents, layerMask, qti, list);
        int count = Math.Min(list.Count, results?.Length ?? 0);
        for (int i = 0; i < count; i++) results[i] = list[i];
        return count;
    }

    public static bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, QueryTriggerInteraction qti)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            var shape = GetWorldShape(c);
            if (OverlapBoxShape(center, halfExtents, shape)) return true;
        }
        return false;
    }

    public static Collider[] OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, QueryTriggerInteraction qti)
    {
        var list = new List<Collider>();
        OverlapCapsuleInternal(point0, point1, radius, layerMask, qti, list);
        return list.ToArray();
    }

    public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask, QueryTriggerInteraction qti)
    {
        var list = new List<Collider>();
        OverlapCapsuleInternal(point0, point1, radius, layerMask, qti, list);
        int count = Math.Min(list.Count, results?.Length ?? 0);
        for (int i = 0; i < count; i++) results[i] = list[i];
        return count;
    }

    public static bool CheckCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, QueryTriggerInteraction qti)
    {
        Vector3 center = (point0 + point1) * 0.5f;
        float height = Vector3.Distance(point0, point1);
        int dir = 1;
        Vector3 axis = (point1 - point0).normalized;
        if (MathF.Abs(Vector3.Dot(axis, Vector3.right)) > 0.9f) dir = 0;
        else if (MathF.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.9f) dir = 2;
        var capShape = new ColliderShape(ColliderShapeType.Capsule, center, Vector3.one, radius, height + radius * 2f, dir);
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            if (Intersect(capShape, GetWorldShape(c), out _, out _)) return true;
        }
        return false;
    }

    private static void OverlapSphereInternal(Vector3 center, float radius, int layerMask, QueryTriggerInteraction qti, List<Collider> results)
    {
        var sphereShape = new ColliderShape(ColliderShapeType.Sphere, center, Vector3.one, radius, 0f, 0);
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            if (Intersect(sphereShape, GetWorldShape(c), out _, out _)) results.Add(c);
        }
    }

    private static bool OverlapShapePoint(Vector3 center, float radius, ColliderShape shape)
    {
        if (shape.Type == ColliderShapeType.Sphere)
            return Vector3.Distance(center, shape.Center) <= radius + shape.Radius;
        if (shape.Type == ColliderShapeType.Box)
        {
            Vector3 d = Vector3.Abs(center - shape.Center);
            Vector3 h = shape.Size * 0.5f + new Vector3(radius, radius, radius);
            return d.x <= h.x && d.y <= h.y && d.z <= h.z;
        }
        if (shape.Type == ColliderShapeType.Capsule)
        {
            Vector3 axis = shape.Direction == 0 ? Vector3.right : shape.Direction == 1 ? Vector3.up : Vector3.forward;
            float halfH = shape.Height * 0.5f - shape.Radius;
            Vector3 p0 = shape.Center - axis * halfH;
            Vector3 p1 = shape.Center + axis * halfH;
            Vector3 closest = ClosestPointOnSegment(center, p0, p1);
            return Vector3.Distance(center, closest) <= radius + shape.Radius;
        }
        return false;
    }

    private static void OverlapBoxInternal(Vector3 center, Vector3 halfExtents, int layerMask, QueryTriggerInteraction qti, List<Collider> results)
    {
        var boxShape = new ColliderShape(ColliderShapeType.Box, center, halfExtents * 2f, 0f, 0f, 0);
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            if (Intersect(boxShape, GetWorldShape(c), out _, out _)) results.Add(c);
        }
    }

    private static bool OverlapBoxShape(Vector3 center, Vector3 halfExtents, ColliderShape shape)
    {
        var boxShape = new ColliderShape(ColliderShapeType.Box, center, halfExtents * 2f, 0f, 0f, 0);
        return Intersect(boxShape, shape, out _, out _);
    }

    private static void OverlapCapsuleInternal(Vector3 point0, Vector3 point1, float radius, int layerMask, QueryTriggerInteraction qti, List<Collider> results)
    {
        Vector3 center = (point0 + point1) * 0.5f;
        float height = Vector3.Distance(point0, point1);
        int dir = 1;
        Vector3 axis = (point1 - point0).normalized;
        if (MathF.Abs(Vector3.Dot(axis, Vector3.right)) > 0.9f) dir = 0;
        else if (MathF.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.9f) dir = 2;
        var capShape = new ColliderShape(ColliderShapeType.Capsule, center, Vector3.one, radius, height + radius * 2f, dir);
        for (int i = 0; i < _colliders.Count; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (qti == QueryTriggerInteraction.Ignore && c.isTrigger) continue;
            int layer = c.gameObject?.layer ?? 0;
            if (layerMask != -1 && (layerMask & (1 << layer)) == 0) continue;
            if (Intersect(capShape, GetWorldShape(c), out _, out _)) results.Add(c);
        }
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

    private static bool Intersect(ColliderShape a, ColliderShape b, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        if (a.Type == ColliderShapeType.Sphere && b.Type == ColliderShapeType.Sphere)
            return IntersectSphereSphere(a.Center, a.Radius, b.Center, b.Radius, out normal, out penetration);
        if (a.Type == ColliderShapeType.Sphere && b.Type == ColliderShapeType.Box)
            return IntersectSphereBox(a.Center, a.Radius, b.Center, b.Size * 0.5f, out normal, out penetration);
        if (a.Type == ColliderShapeType.Box && b.Type == ColliderShapeType.Sphere)
        {
            if (IntersectSphereBox(b.Center, b.Radius, a.Center, a.Size * 0.5f, out normal, out penetration)) { normal = -normal; return true; }
            return false;
        }
        if (a.Type == ColliderShapeType.Box && b.Type == ColliderShapeType.Box)
            return IntersectBoxBox(a.Center, a.Size * 0.5f, b.Center, b.Size * 0.5f, out normal, out penetration);
        if (a.Type == ColliderShapeType.Capsule && b.Type == ColliderShapeType.Sphere)
            return IntersectCapsuleSphere(a, b.Center, b.Radius, out normal, out penetration);
        if (a.Type == ColliderShapeType.Sphere && b.Type == ColliderShapeType.Capsule)
        {
            if (IntersectCapsuleSphere(b, a.Center, a.Radius, out normal, out penetration)) { normal = -normal; return true; }
            return false;
        }
        if (a.Type == ColliderShapeType.Capsule && b.Type == ColliderShapeType.Capsule)
            return IntersectCapsuleCapsule(a, b, out normal, out penetration);
        if (a.Type == ColliderShapeType.Capsule && b.Type == ColliderShapeType.Box)
            return IntersectCapsuleBox(a, b.Center, b.Size * 0.5f, out normal, out penetration);
        if (a.Type == ColliderShapeType.Box && b.Type == ColliderShapeType.Capsule)
        {
            if (IntersectCapsuleBox(b, a.Center, a.Size * 0.5f, out normal, out penetration)) { normal = -normal; return true; }
            return false;
        }
        return false;
    }

    private static bool IntersectSphereSphere(Vector3 c1, float r1, Vector3 c2, float r2, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        Vector3 d = c2 - c1;
        float dist = d.magnitude;
        float sumR = r1 + r2;
        if (dist >= sumR || dist < 1e-6f) return false;
        penetration = sumR - dist;
        normal = dist > 1e-6f ? d / dist : Vector3.up;
        return true;
    }

    private static bool IntersectSphereBox(Vector3 sc, float r, Vector3 bc, Vector3 he, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        Vector3 closest = new Vector3(
            Math.Clamp(sc.x, bc.x - he.x, bc.x + he.x),
            Math.Clamp(sc.y, bc.y - he.y, bc.y + he.y),
            Math.Clamp(sc.z, bc.z - he.z, bc.z + he.z));
        Vector3 delta = sc - closest;
        float dist = delta.magnitude;
        if (dist > r) return false;
        if (dist < 1e-6f)
        {
            Vector3 d = sc - bc;
            Vector3 overlap = he - Vector3.Abs(d);
            if (overlap.x < overlap.y && overlap.x < overlap.z)
            { normal = new Vector3(d.x < 0 ? -1 : 1, 0, 0); penetration = overlap.x + r; }
            else if (overlap.y < overlap.z)
            { normal = new Vector3(0, d.y < 0 ? -1 : 1, 0); penetration = overlap.y + r; }
            else
            { normal = new Vector3(0, 0, d.z < 0 ? -1 : 1); penetration = overlap.z + r; }
            return true;
        }
        penetration = r - dist;
        normal = delta / dist;
        return true;
    }

    private static bool IntersectBoxBox(Vector3 c1, Vector3 he1, Vector3 c2, Vector3 he2, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        Vector3 d = c2 - c1;
        float ox = he1.x + he2.x - MathF.Abs(d.x);
        float oy = he1.y + he2.y - MathF.Abs(d.y);
        float oz = he1.z + he2.z - MathF.Abs(d.z);
        if (ox <= 0f || oy <= 0f || oz <= 0f) return false;
        if (ox < oy && ox < oz)
        { normal = new Vector3(d.x < 0 ? -1 : 1, 0, 0); penetration = ox; }
        else if (oy < oz)
        { normal = new Vector3(0, d.y < 0 ? -1 : 1, 0); penetration = oy; }
        else
        { normal = new Vector3(0, 0, d.z < 0 ? -1 : 1); penetration = oz; }
        return true;
    }

    private static Vector3 GetCapsuleAxis(ColliderShape cap) => cap.Direction == 0 ? Vector3.right : cap.Direction == 1 ? Vector3.up : Vector3.forward;

    private static bool IntersectCapsuleSphere(ColliderShape cap, Vector3 sc, float r, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        Vector3 axis = GetCapsuleAxis(cap);
        float halfH = cap.Height * 0.5f - cap.Radius;
        Vector3 p0 = cap.Center - axis * halfH;
        Vector3 p1 = cap.Center + axis * halfH;
        Vector3 closest = ClosestPointOnSegment(sc, p0, p1);
        Vector3 delta = sc - closest;
        float dist = delta.magnitude;
        float sumR = cap.Radius + r;
        if (dist >= sumR || dist < 1e-6f) return false;
        penetration = sumR - dist;
        normal = delta / dist;
        return true;
    }

    private static bool IntersectCapsuleCapsule(ColliderShape a, ColliderShape b, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        Vector3 axisA = GetCapsuleAxis(a);
        Vector3 axisB = GetCapsuleAxis(b);
        float hA = a.Height * 0.5f - a.Radius;
        float hB = b.Height * 0.5f - b.Radius;
        Vector3 a0 = a.Center - axisA * hA;
        Vector3 a1 = a.Center + axisA * hA;
        Vector3 b0 = b.Center - axisB * hB;
        Vector3 b1 = b.Center + axisB * hB;
        Vector3 pa = a0, pb = b0;
        float bestDistSq = float.MaxValue;
        for (int i = 0; i <= 8; i++)
        {
            for (int j = 0; j <= 8; j++)
            {
                float t1 = i / 8f, t2 = j / 8f;
                Vector3 p = a0 + (a1 - a0) * t1;
                Vector3 q = b0 + (b1 - b0) * t2;
                float d = Vector3.DistanceSquared(p, q);
                if (d < bestDistSq) { bestDistSq = d; pa = p; pb = q; }
            }
        }
        pa = ClosestPointOnSegment(pb, a0, a1);
        pb = ClosestPointOnSegment(pa, b0, b1);
        Vector3 delta = pb - pa;
        float dist = delta.magnitude;
        float sumR = a.Radius + b.Radius;
        if (dist >= sumR || dist < 1e-6f) return false;
        penetration = sumR - dist;
        normal = delta / dist;
        return true;
    }

    private static bool IntersectCapsuleBox(ColliderShape cap, Vector3 bc, Vector3 he, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        Vector3 axis = GetCapsuleAxis(cap);
        float halfH = cap.Height * 0.5f - cap.Radius;
        Vector3 p0 = cap.Center - axis * halfH;
        Vector3 p1 = cap.Center + axis * halfH;
        int steps = 8;
        float bestPen = 0f;
        Vector3 bestN = Vector3.zero;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 c = p0 + (p1 - p0) * t;
            if (IntersectSphereBox(c, cap.Radius, bc, he, out Vector3 n, out float pen))
            {
                if (pen > bestPen) { bestPen = pen; bestN = n; }
            }
        }
        if (bestPen <= 0f) return false;
        normal = bestN;
        penetration = bestPen;
        return true;
    }

    public static void Simulate(float step)
    {
        foreach (var rb in _rigidbodies)
        {
            if (rb != null) rb.ApplyForces(step);
        }

        ApplyJoints(step);

        foreach (var rb in _rigidbodies)
        {
            if (rb != null) rb.Integrate(step);
        }

        foreach (var wheel in _wheels)
        {
            if (wheel != null) wheel.UpdateWheel(step);
        }

        var currentCollisions = new HashSet<CollisionPair>();
        var currentTriggers = new HashSet<CollisionPair>();

        foreach (var c in _colliders)
        {
            if (c != null) c.ClearContacts();
        }

        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = i + 1; j < _colliders.Count; j++)
            {
                var a = _colliders[i];
                var b = _colliders[j];
                if (a == null || b == null || !a.enabled || !b.enabled) continue;
                if (GetIgnoreCollision(a, b)) continue;
                int la = a.gameObject?.layer ?? 0;
                int lb = b.gameObject?.layer ?? 0;
                if (!_layerMatrix[la, lb]) continue;

                var sa = GetWorldShape(a);
                var sb = GetWorldShape(b);
                if (Intersect(sa, sb, out Vector3 normal, out float pen))
                {
                    bool aTrigger = a.isTrigger;
                    bool bTrigger = b.isTrigger;
                    var pair = new CollisionPair(a, b);

                    if (aTrigger || bTrigger)
                    {
                        currentTriggers.Add(pair);
                        if (!_triggerStates.Contains(pair))
                        {
                            _triggerStates.Add(pair);
                            if (aTrigger) a.SendMessage("OnTriggerEnter", b);
                            if (bTrigger) b.SendMessage("OnTriggerEnter", a);
                        }
                        else
                        {
                            if (aTrigger) a.SendMessage("OnTriggerStay", b);
                            if (bTrigger) b.SendMessage("OnTriggerStay", a);
                        }
                    }
                    else
                    {
                        currentCollisions.Add(pair);
                        ResolveCollision(a, b, normal, pen);
                        Vector3 relVel = Vector3.zero;
                        var rbA = a.attachedRigidbody;
                        var rbB = b.attachedRigidbody;
                        if (rbA != null) relVel -= rbA.velocity;
                        if (rbB != null) relVel += rbB.velocity;
                        var cp = new ContactPoint(
                            b.transform != null ? b.transform.position : Vector3.zero,
                            normal, a, b, -pen);
                        a.AddContact(cp);
                        b.AddContact(new ContactPoint(
                            a.transform != null ? a.transform.position : Vector3.zero,
                            -normal, b, a, -pen));
                        if (!_collisionStates.Contains(pair))
                        {
                            _collisionStates.Add(pair);
                            a.SendMessage("OnCollisionEnter", CreateCollision(a, b, normal, pen, relVel));
                            b.SendMessage("OnCollisionEnter", CreateCollision(b, a, -normal, pen, -relVel));
                        }
                        else
                        {
                            a.SendMessage("OnCollisionStay", CreateCollision(a, b, normal, pen, relVel));
                            b.SendMessage("OnCollisionStay", CreateCollision(b, a, -normal, pen, -relVel));
                        }
                    }
                }
            }
        }

        foreach (var pair in _collisionStates)
        {
            if (!currentCollisions.Contains(pair))
            {
                pair.A?.SendMessage("OnCollisionExit", pair.B);
                pair.B?.SendMessage("OnCollisionExit", pair.A);
            }
        }
        _collisionStates.IntersectWith(currentCollisions);

        foreach (var pair in _triggerStates)
        {
            if (!currentTriggers.Contains(pair))
            {
                bool aT = pair.A != null && pair.A.isTrigger;
                bool bT = pair.B != null && pair.B.isTrigger;
                if (aT) pair.A.SendMessage("OnTriggerExit", pair.B);
                if (bT) pair.B.SendMessage("OnTriggerExit", pair.A);
            }
        }
        _triggerStates.IntersectWith(currentTriggers);
    }

    private static Collision CreateCollision(Collider a, Collider b, Vector3 normal, float pen, Vector3 relVel)
    {
        var col = new Collision();
        col.SetFrom(a, b, normal, pen, relVel);
        return col;
    }

    private static void ApplyJoints(float step)
    {
        foreach (var joint in _joints)
        {
            if (joint == null) continue;
            if (joint is SpringJoint springJoint)
            {
                ApplySpringJoint(springJoint, step);
            }
            else if (joint is FixedJoint fixedJoint)
            {
                ApplyFixedJoint(fixedJoint, step);
            }
            else if (joint is HingeJoint hingeJoint)
            {
                ApplyHingeJoint(hingeJoint, step);
            }
            else if (joint is ConfigurableJoint configurableJoint)
            {
                ApplyConfigurableJoint(configurableJoint, step);
            }
            else if (joint is CharacterJoint characterJoint)
            {
                ApplyCharacterJoint(characterJoint, step);
            }
        }
    }

    private static void ApplyFixedJoint(FixedJoint joint, float step)
    {
        var connectedBody = joint.connectedBody;
        if (connectedBody == null) return;
        var ownBody = joint.GetComponent<Rigidbody>();
        if (ownBody == null || ownBody.isKinematic) return;

        Vector3 anchorPos = ownBody.transform != null ? ownBody.transform.TransformPoint(joint.anchor) : joint.anchor;
        Vector3 connectedAnchorPos = connectedBody.transform != null ? connectedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;

        Vector3 delta = anchorPos - connectedAnchorPos;
        float dist = delta.magnitude;
        if (dist < 1e-6f) return;

        float invMassA = !ownBody.isKinematic ? 1f / ownBody.mass : 0f;
        float invMassB = !connectedBody.isKinematic ? 1f / connectedBody.mass : 0f;
        float totalInvMass = invMassA + invMassB;
        if (totalInvMass <= 1e-6f) return;

        Vector3 dir = delta / dist;
        Vector3 relVel = ownBody.velocity - connectedBody.velocity;
        float velAlongDir = Vector3.Dot(relVel, dir);

        float stiffness = 1000f;
        float damping = 50f;
        float forceMag = (-dist * stiffness - velAlongDir * damping) / totalInvMass;
        Vector3 force = dir * forceMag;

        ownBody.AddForce(-force * invMassA);
        if (!connectedBody.isKinematic) connectedBody.AddForce(force * invMassB);

        Vector3 rA = anchorPos - ownBody.worldCenterOfMass;
        Vector3 rB = connectedAnchorPos - connectedBody.worldCenterOfMass;
        Vector3 torqueA = Vector3.Cross(rA, -force);
        Vector3 torqueB = Vector3.Cross(rB, force);
        ownBody.AddTorque(torqueA * invMassA);
        if (!connectedBody.isKinematic) connectedBody.AddTorque(torqueB * invMassB);

        joint.SetCurrentForce(force);
    }

    private static void ApplyHingeJoint(HingeJoint joint, float step)
    {
        var connectedBody = joint.connectedBody;
        if (connectedBody == null) return;
        var ownBody = joint.GetComponent<Rigidbody>();
        if (ownBody == null || ownBody.isKinematic) return;

        Vector3 anchorPos = ownBody.transform != null ? ownBody.transform.TransformPoint(joint.anchor) : joint.anchor;
        Vector3 connectedAnchorPos = connectedBody.transform != null ? connectedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;

        Vector3 delta = anchorPos - connectedAnchorPos;
        float dist = delta.magnitude;
        if (dist > 1e-6f)
        {
            float invMassA = !ownBody.isKinematic ? 1f / ownBody.mass : 0f;
            float invMassB = !connectedBody.isKinematic ? 1f / connectedBody.mass : 0f;
            float totalInvMass = invMassA + invMassB;
            if (totalInvMass > 1e-6f)
            {
                Vector3 dir = delta / dist;
                Vector3 relVel = ownBody.velocity - connectedBody.velocity;
                float velAlongDir = Vector3.Dot(relVel, dir);
                float stiffness = 800f;
                float damping = 30f;
                float forceMag = (-dist * stiffness - velAlongDir * damping) / totalInvMass;
                Vector3 force = dir * forceMag;
                ownBody.AddForce(-force * invMassA);
                if (!connectedBody.isKinematic) connectedBody.AddForce(force * invMassB);
                joint.SetCurrentForce(force);
            }
        }

        if (joint.useSpring)
        {
            Vector3 axis = joint.axis;
            if (ownBody.transform != null) axis = ownBody.transform.TransformDirection(joint.axis);
            Vector3 connectedAxis = connectedBody.transform != null ? connectedBody.transform.TransformDirection(joint.axis) : joint.axis;
            float angle = Vector3.Angle(axis, connectedAxis);
            float angVel = Vector3.Dot(ownBody.angularVelocity, axis) - Vector3.Dot(connectedBody.angularVelocity, axis);
            float springForce = (joint.spring.targetPosition - angle) * joint.spring.spring - angVel * joint.spring.damper;
            Vector3 torque = axis * springForce;
            ownBody.AddTorque(-torque);
            if (!connectedBody.isKinematic) connectedBody.AddTorque(torque);
            joint.SetCurrentTorque(torque);
        }

        if (joint.useMotor)
        {
            Vector3 axis = joint.axis;
            if (ownBody.transform != null) axis = ownBody.transform.TransformDirection(joint.axis);
            float currentAngVel = Vector3.Dot(ownBody.angularVelocity, axis) - (connectedBody != null ? Vector3.Dot(connectedBody.angularVelocity, axis) : 0f);
            float velDiff = joint.motor.targetVelocity - currentAngVel;
            float torqueMag = Math.Clamp(velDiff * joint.motor.force, -joint.motor.force, joint.motor.force);
            Vector3 torque = axis * torqueMag;
            ownBody.AddTorque(-torque);
            if (connectedBody != null && !connectedBody.isKinematic) connectedBody.AddTorque(torque);
        }
    }

    private static void ApplyConfigurableJoint(ConfigurableJoint joint, float step)
    {
        var connectedBody = joint.connectedBody;
        if (connectedBody == null) return;
        var ownBody = joint.GetComponent<Rigidbody>();
        if (ownBody == null || ownBody.isKinematic) return;

        Vector3 anchorPos = ownBody.transform != null ? ownBody.transform.TransformPoint(joint.anchor) : joint.anchor;
        Vector3 connectedAnchorPos = connectedBody.transform != null ? connectedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;

        Vector3 delta = anchorPos - connectedAnchorPos;
        float dist = delta.magnitude;
        if (dist > 1e-6f)
        {
            float invMassA = !ownBody.isKinematic ? 1f / ownBody.mass : 0f;
            float invMassB = !connectedBody.isKinematic ? 1f / connectedBody.mass : 0f;
            float totalInvMass = invMassA + invMassB;
            if (totalInvMass > 1e-6f)
            {
                Vector3 dir = delta / dist;
                Vector3 relVel = ownBody.velocity - connectedBody.velocity;
                float velAlongDir = Vector3.Dot(relVel, dir);
                float stiffness = 500f;
                float damping = 20f;
                float forceMag = (-dist * stiffness - velAlongDir * damping) / totalInvMass;
                Vector3 force = dir * forceMag;
                ownBody.AddForce(-force * invMassA);
                if (!connectedBody.isKinematic) connectedBody.AddForce(force * invMassB);
                joint.SetCurrentForce(force);
            }
        }
    }

    private static void ApplyCharacterJoint(CharacterJoint joint, float step)
    {
        var connectedBody = joint.connectedBody;
        if (connectedBody == null) return;
        var ownBody = joint.GetComponent<Rigidbody>();
        if (ownBody == null || ownBody.isKinematic) return;

        Vector3 anchorPos = ownBody.transform != null ? ownBody.transform.TransformPoint(joint.anchor) : joint.anchor;
        Vector3 connectedAnchorPos = connectedBody.transform != null ? connectedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;

        Vector3 delta = anchorPos - connectedAnchorPos;
        float dist = delta.magnitude;
        if (dist > 1e-6f)
        {
            float invMassA = !ownBody.isKinematic ? 1f / ownBody.mass : 0f;
            float invMassB = !connectedBody.isKinematic ? 1f / connectedBody.mass : 0f;
            float totalInvMass = invMassA + invMassB;
            if (totalInvMass > 1e-6f)
            {
                Vector3 dir = delta / dist;
                Vector3 relVel = ownBody.velocity - connectedBody.velocity;
                float velAlongDir = Vector3.Dot(relVel, dir);
                float stiffness = 600f;
                float damping = 25f;
                float forceMag = (-dist * stiffness - velAlongDir * damping) / totalInvMass;
                Vector3 force = dir * forceMag;
                ownBody.AddForce(-force * invMassA);
                if (!connectedBody.isKinematic) connectedBody.AddForce(force * invMassB);
                joint.SetCurrentForce(force);
            }
        }
    }

    private static void ApplySpringJoint(SpringJoint joint, float step)
    {
        var connectedBody = joint.connectedBody;
        if (connectedBody == null) return;
        var ownBody = joint.GetComponent<Rigidbody>();
        if (ownBody == null || ownBody.isKinematic) return;

        Vector3 anchorPos = ownBody.transform != null ? ownBody.transform.TransformPoint(joint.anchor) : joint.anchor;
        Vector3 connectedAnchorPos = connectedBody.transform != null ? connectedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;

        Vector3 delta = anchorPos - connectedAnchorPos;
        float dist = delta.magnitude;
        if (dist < 1e-6f) return;

        Vector3 dir = delta / dist;
        float minDist = joint.minDistance;
        float maxDist = joint.maxDistance;

        float springForce = 0f;
        float damperForce = 0f;

        if (dist > maxDist)
        {
            float error = dist - maxDist;
            springForce = error * joint.spring;
            Vector3 relVel = ownBody.velocity - connectedBody.velocity;
            float velAlongDir = Vector3.Dot(relVel, dir);
            damperForce = velAlongDir * joint.damper;
        }
        else if (dist < minDist && minDist > 0f)
        {
            float error = minDist - dist;
            springForce = -error * joint.spring;
            Vector3 relVel = ownBody.velocity - connectedBody.velocity;
            float velAlongDir = Vector3.Dot(relVel, dir);
            damperForce = velAlongDir * joint.damper;
        }

        Vector3 force = dir * (springForce - damperForce);
        ownBody.AddForce(-force);
        if (!connectedBody.isKinematic)
            connectedBody.AddForce(force);
    }

    private static void ResolveCollision(Collider a, Collider b, Vector3 normal, float penetration)
    {
        var rbA = a.attachedRigidbody;
        var rbB = b.attachedRigidbody;
        if ((rbA == null || rbA.isKinematic) && (rbB == null || rbB.isKinematic)) return;

        float invMassA = rbA != null && !rbA.isKinematic ? 1f / rbA.mass : 0f;
        float invMassB = rbB != null && !rbB.isKinematic ? 1f / rbB.mass : 0f;
        float totalInvMass = invMassA + invMassB;
        if (totalInvMass <= 1e-6f) return;

        PhysicMaterial matA = a.sharedMaterial;
        PhysicMaterial matB = b.sharedMaterial;
        float bounciness = PhysicMaterial.CombineBounciness(matA?.bounciness ?? 0f, matB?.bounciness ?? 0f,
            matA?.bounceCombine ?? PhysicMaterialCombine.Average, matB?.bounceCombine ?? PhysicMaterialCombine.Average);
        float friction = PhysicMaterial.CombineFriction(matA?.dynamicFriction ?? 0.6f, matB?.dynamicFriction ?? 0.6f,
            matA?.frictionCombine ?? PhysicMaterialCombine.Average, matB?.frictionCombine ?? PhysicMaterialCombine.Average);

        float correctionAmount = MathF.Max(penetration - _defaultContactOffset, 0f) / totalInvMass * 0.8f;
        Vector3 correction = normal * correctionAmount;
        if (a.transform != null && invMassA > 0f) a.transform.position -= correction * invMassA;
        if (b.transform != null && invMassB > 0f) b.transform.position += correction * invMassB;

        Vector3 velA = rbA?.velocity ?? Vector3.zero;
        Vector3 velB = rbB?.velocity ?? Vector3.zero;
        Vector3 relVel = velA - velB;
        float velAlongNormal = Vector3.Dot(relVel, normal);
        if (velAlongNormal > 0f) return;

        float bounceFactor = MathF.Abs(velAlongNormal) > _bounceThreshold ? bounciness : 0f;
        float impulseMag = -(1f + bounceFactor) * velAlongNormal / totalInvMass;
        Vector3 impulse = normal * impulseMag;
        if (rbA != null && invMassA > 0f)
        {
            rbA.velocity += impulse * invMassA;
            rbA.WakeUp();
        }
        if (rbB != null && invMassB > 0f)
        {
            rbB.velocity -= impulse * invMassB;
            rbB.WakeUp();
        }

        Vector3 tangent = relVel - normal * velAlongNormal;
        float tangentMag = tangent.magnitude;
        if (tangentMag > 1e-6f)
        {
            tangent /= tangentMag;
            float velAlongTangent = Vector3.Dot(relVel, tangent);
            float frictionImpulseMag = -velAlongTangent / totalInvMass;
            float maxFriction = MathF.Abs(impulseMag) * friction;
            frictionImpulseMag = Math.Clamp(frictionImpulseMag, -maxFriction, maxFriction);
            Vector3 frictionImpulse = tangent * frictionImpulseMag;
            if (rbA != null && invMassA > 0f) rbA.velocity += frictionImpulse * invMassA;
            if (rbB != null && invMassB > 0f) rbB.velocity -= frictionImpulse * invMassB;
        }
    }

    public static int GetContacts(Collider collider, Collider[] results)
    {
        if (collider == null || results == null) return 0;
        var shape = GetWorldShape(collider);
        int count = 0;
        for (int i = 0; i < _colliders.Count && count < results.Length; i++)
        {
            var other = _colliders[i];
            if (other == null || other == collider || !other.enabled) continue;
            if (Intersect(shape, GetWorldShape(other), out _, out _))
                results[count++] = other;
        }
        return count;
    }
}
