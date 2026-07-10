using System;
using System.Collections.Generic;

namespace UnityEngine;

internal static class PhysicsWorld
{
    private static readonly List<Collider> _colliders = new();
    private static readonly List<Rigidbody> _rigidbodies = new();

    public static void Register(Collider collider)
    {
        if (collider is null || _colliders.Contains(collider)) return;
        _colliders.Add(collider);
    }

    public static void Unregister(Collider collider)
    {
        if (collider is null) return;
        _ = _colliders.Remove(collider);
    }

    public static void Register(Rigidbody rigidbody)
    {
        if (rigidbody is null || _rigidbodies.Contains(rigidbody)) return;
        _rigidbodies.Add(rigidbody);
    }

    public static void Unregister(Rigidbody rigidbody)
    {
        if (rigidbody is null) return;
        _ = _rigidbodies.Remove(rigidbody);
    }

    public static IReadOnlyList<Collider> GetColliders() => _colliders;
    public static IReadOnlyList<Rigidbody> GetRigidbodies() => _rigidbodies;

    public static void Simulate(float deltaTime)
    {
        CleanupDestroyed();
        IntegrateRigidbodies(deltaTime);
        DetectAndResolveCollisions(deltaTime);
    }

    private static void CleanupDestroyed()
    {
        for (var i = _colliders.Count - 1; i >= 0; i--)
        {
            var c = _colliders[i];
            if (c is null || c.IsDestroyed || c.gameObject is null || !c.gameObject.activeInHierarchy)
            {
                _colliders.RemoveAt(i);
            }
        }

        for (var i = _rigidbodies.Count - 1; i >= 0; i--)
        {
            var rb = _rigidbodies[i];
            if (rb is null || rb.IsDestroyed || rb.gameObject is null || !rb.gameObject.activeInHierarchy)
            {
                _rigidbodies.RemoveAt(i);
            }
        }
    }

    private static void IntegrateRigidbodies(float deltaTime)
    {
        foreach (var rb in _rigidbodies)
        {
            if (rb is null || rb.IsDestroyed || rb.isKinematic) continue;

            if (rb.useGravity)
            {
                rb.velocity += Physics.gravity * deltaTime;
            }

            if (rb.drag > 0f)
            {
                rb.velocity *= Math.Max(0f, 1f - rb.drag * deltaTime);
            }

            if (rb.angularDrag > 0f)
            {
                rb.angularVelocity *= Math.Max(0f, 1f - rb.angularDrag * deltaTime);
            }

            var transform = rb.transform;
            if (transform is null) continue;

            transform.localPosition += rb.velocity * deltaTime;

            if (!rb.freezeRotation && rb.angularVelocity.magnitude > 1e-6f)
            {
                var deltaAngle = rb.angularVelocity * deltaTime;
                transform.localRotation *= Quaternion.Euler(deltaAngle.x, deltaAngle.y, deltaAngle.z);
            }
        }
    }

    private static void DetectAndResolveCollisions(float deltaTime)
    {
        _ = deltaTime;

        var count = _colliders.Count;
        for (var i = 0; i < count; i++)
        {
            var a = _colliders[i];
            if (a is null || a.IsDestroyed || !a.enabled) continue;

            for (var j = i + 1; j < count; j++)
            {
                var b = _colliders[j];
                if (b is null || b.IsDestroyed || !b.enabled) continue;

                if (!Physics.IsLayerCollisionEnabled(a.gameObject?.layer ?? 0, b.gameObject?.layer ?? 0))
                {
                    continue;
                }

                if (a.isTrigger || b.isTrigger)
                {
                    if (Intersect(a, b, out _, out _))
                    {
                        DispatchTrigger(a, b);
                    }
                    continue;
                }

                if (Intersect(a, b, out var normal, out var penetration))
                {
                    ResolveCollision(a, b, normal, penetration);
                    DispatchCollision(a, b, normal);
                }
            }
        }
    }

    public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask)
    {
        var hits = RaycastAll(ray, maxDistance, layerMask);
        if (hits.Count == 0)
        {
            hitInfo = RaycastHit.empty;
            return false;
        }

        hitInfo = hits[0];
        return true;
    }

    public static List<RaycastHit> RaycastAll(Ray ray, float maxDistance, int layerMask)
    {
        var hits = new List<RaycastHit>();
        var direction = ray.direction.normalized;
        if (direction.magnitude < 1e-6f) return hits;

        foreach (var collider in _colliders)
        {
            if (collider is null || collider.IsDestroyed || !collider.enabled) continue;
            if (!Physics.LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;

            if (RaycastCollider(ray.origin, direction, collider, out var t, out var normal) && t < maxDistance && t >= 0f)
            {
                var point = ray.origin + direction * t;
                hits.Add(new RaycastHit
                {
                    collider = collider,
                    rigidbody = collider.attachedRigidbody,
                    transform = collider.transform,
                    point = point,
                    normal = normal,
                    distance = t,
                    barycentricCoordinate = new Vector3(1f - t, t, 0f)
                });
            }
        }

        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    private static bool RaycastCollider(Vector3 origin, Vector3 direction, Collider collider, out float distance, out Vector3 normal)
    {
        distance = float.PositiveInfinity;
        normal = -direction;

        switch (collider)
        {
            case SphereCollider sphere:
                return RaycastSphere(origin, direction, GetWorldPosition(collider, sphere.center), sphere.radius, out distance, out normal);
            case BoxCollider box:
                return RaycastBox(origin, direction, GetWorldPosition(collider, box.center), box.size, collider.transform?.rotation ?? Quaternion.identity, out distance, out normal);
            case CapsuleCollider capsule:
                GetCapsulePoints(collider, capsule, out var p0, out var p1, out var radius);
                return RaycastCapsule(origin, direction, p0, p1, radius, out distance, out normal);
        }

        return false;
    }

    private static void GetCapsulePoints(Collider collider, CapsuleCollider capsule, out Vector3 point0, out Vector3 point1, out float radius)
    {
        var center = GetWorldPosition(collider, capsule.center);
        var rotation = collider.transform?.rotation ?? Quaternion.identity;
        var axis = capsule.direction switch
        {
            0 => rotation * Vector3.right,
            1 => rotation * Vector3.up,
            _ => rotation * Vector3.forward
        };
        var halfHeight = MathF.Max(0f, capsule.height * 0.5f - capsule.radius);
        point0 = center - axis * halfHeight;
        point1 = center + axis * halfHeight;
        radius = capsule.radius;
    }

    private static bool RaycastCapsule(Vector3 origin, Vector3 direction, Vector3 point0, Vector3 point1, float radius, out float distance, out Vector3 normal)
    {
        distance = float.PositiveInfinity;
        normal = -direction;

        var axis = point1 - point0;
        var length = axis.magnitude;
        if (length < 1e-6f)
        {
            return RaycastSphere(origin, direction, point0, radius, out distance, out normal);
        }

        axis = axis.normalized;
        var steps = Math.Max(1, (int)(length / radius));
        var step = length / steps;
        var hit = false;
        for (var i = 0; i <= steps; i++)
        {
            var p = point0 + axis * (i * step);
            if (RaycastSphere(origin, direction, p, radius, out var t, out var n) && t < distance)
            {
                distance = t;
                normal = n;
                hit = true;
            }
        }

        return hit;
    }

    private static bool RaycastSphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance, out Vector3 normal)
    {
        distance = float.PositiveInfinity;
        normal = -direction;

        var oc = origin - center;
        var a = Vector3.Dot(direction, direction);
        var b = 2f * Vector3.Dot(oc, direction);
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var discriminant = b * b - 4f * a * c;

        if (discriminant < 0f) return false;

        var sqrt = MathF.Sqrt(discriminant);
        var t = (-b - sqrt) / (2f * a);
        if (t < 0f) t = (-b + sqrt) / (2f * a);
        if (t < 0f) return false;

        distance = t;
        normal = (origin + direction * t - center).normalized;
        return true;
    }

    private static bool RaycastBox(Vector3 origin, Vector3 direction, Vector3 center, Vector3 size, Quaternion rotation, out float distance, out Vector3 normal)
    {
        distance = float.PositiveInfinity;
        normal = -direction;

        var invRotation = Quaternion.Inverse(rotation);
        var localOrigin = invRotation * (origin - center);
        var localDir = invRotation * direction;

        var half = size * 0.5f;
        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;
        var axis = 0;
        var sign = 0;

        for (var i = 0; i < 3; i++)
        {
            var o = i == 0 ? localOrigin.x : i == 1 ? localOrigin.y : localOrigin.z;
            var d = i == 0 ? localDir.x : i == 1 ? localDir.y : localDir.z;
            var min = i == 0 ? -half.x : i == 1 ? -half.y : -half.z;
            var max = i == 0 ? half.x : i == 1 ? half.y : half.z;

            if (MathF.Abs(d) < 1e-6f)
            {
                if (o < min || o > max) return false;
            }
            else
            {
                var invD = 1f / d;
                var t1 = (min - o) * invD;
                var t2 = (max - o) * invD;
                var s = 1;
                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                    s = -1;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                    axis = i;
                    sign = s;
                }

                tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
        }

        if (tMin < 0f) return false;
        distance = tMin;

        var localNormal = Vector3.zero;
        localNormal[axis] = sign;
        normal = rotation * localNormal;
        return true;
    }

    public static bool Intersect(Collider a, Collider b, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;

        return (a, b) switch
        {
            (SphereCollider sa, SphereCollider sb) => IntersectSphereSphere(GetWorldPosition(a, sa.center), sa.radius, GetWorldPosition(b, sb.center), sb.radius, out normal, out penetration),
            (BoxCollider ba, BoxCollider bb) => IntersectBoxBox(GetWorldPosition(a, ba.center), ba.size, a.transform?.rotation ?? Quaternion.identity, GetWorldPosition(b, bb.center), bb.size, b.transform?.rotation ?? Quaternion.identity, out normal, out penetration),
            (SphereCollider sa, BoxCollider bb) => IntersectSphereBox(GetWorldPosition(a, sa.center), sa.radius, GetWorldPosition(b, bb.center), bb.size, b.transform?.rotation ?? Quaternion.identity, out normal, out penetration) && InvertNormal(ref normal),
            (BoxCollider ba, SphereCollider sb) => IntersectSphereBox(GetWorldPosition(b, sb.center), sb.radius, GetWorldPosition(a, ba.center), ba.size, a.transform?.rotation ?? Quaternion.identity, out normal, out penetration),
            (CapsuleCollider ca, CapsuleCollider cb) => IntersectCapsuleCapsule(a, ca, b, cb, out normal, out penetration),
            (CapsuleCollider ca, SphereCollider sb) => IntersectCapsuleSphere(a, ca, GetWorldPosition(b, sb.center), sb.radius, out normal, out penetration),
            (SphereCollider sa, CapsuleCollider cb) => IntersectCapsuleSphere(b, cb, GetWorldPosition(a, sa.center), sa.radius, out normal, out penetration) && InvertNormal(ref normal),
            (CapsuleCollider ca, BoxCollider bb) => IntersectCapsuleBox(a, ca, GetWorldPosition(b, bb.center), bb.size, b.transform?.rotation ?? Quaternion.identity, out normal, out penetration),
            (BoxCollider ba, CapsuleCollider cb) => IntersectCapsuleBox(b, cb, GetWorldPosition(a, ba.center), ba.size, a.transform?.rotation ?? Quaternion.identity, out normal, out penetration) && InvertNormal(ref normal),
            _ => false
        };
    }

    private static bool InvertNormal(ref Vector3 normal)
    {
        normal = -normal;
        return true;
    }

    private static bool IntersectSphereSphere(Vector3 a, float radiusA, Vector3 b, float radiusB, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;

        var delta = b - a;
        var distance = delta.magnitude;
        var sumRadius = radiusA + radiusB;
        if (distance >= sumRadius || distance < 1e-6f) return false;

        penetration = sumRadius - distance;
        normal = delta.normalized;
        return true;
    }

    private static bool IntersectBoxBox(Vector3 centerA, Vector3 sizeA, Quaternion rotA, Vector3 centerB, Vector3 sizeB, Quaternion rotB, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;

        var delta = centerB - centerA;
        var axes = new List<Vector3>
        {
            rotA * Vector3.right,
            rotA * Vector3.up,
            rotA * Vector3.forward,
            rotB * Vector3.right,
            rotB * Vector3.up,
            rotB * Vector3.forward
        };

        for (var i = 0; i < axes.Count; i++)
        {
            for (var j = i + 1; j < axes.Count; j++)
            {
                var cross = Vector3.Cross(axes[i], axes[j]);
                if (cross.magnitude > 1e-6f)
                {
                    axes.Add(cross.normalized);
                }
            }
        }

        var minPenetration = float.PositiveInfinity;
        var minAxis = Vector3.up;

        foreach (var axis in axes)
        {
            if (!TestAxis(axis, centerA, sizeA, rotA, centerB, sizeB, rotB, out var pen)) continue;
            if (pen < minPenetration)
            {
                minPenetration = pen;
                minAxis = axis;
            }
        }

        if (minPenetration == float.PositiveInfinity) return false;

        penetration = minPenetration;
        normal = Vector3.Dot(delta, minAxis) > 0f ? minAxis : -minAxis;
        return true;
    }

    private static bool TestAxis(Vector3 axis, Vector3 centerA, Vector3 sizeA, Quaternion rotA, Vector3 centerB, Vector3 sizeB, Quaternion rotB, out float penetration)
    {
        penetration = 0f;
        axis = axis.normalized;
        if (axis.magnitude < 1e-6f) return false;

        var halfA = sizeA * 0.5f;
        var halfB = sizeB * 0.5f;

        var ra = ProjectBox(centerA, halfA, rotA, axis);
        var rb = ProjectBox(centerB, halfB, rotB, axis);
        var centerDist = MathF.Abs(Vector3.Dot(centerB - centerA, axis));
        var sum = ra + rb;

        if (centerDist >= sum) return false;

        penetration = sum - centerDist;
        return true;
    }

    private static float ProjectBox(Vector3 center, Vector3 halfSize, Quaternion rotation, Vector3 axis)
    {
        var right = rotation * Vector3.right;
        var up = rotation * Vector3.up;
        var forward = rotation * Vector3.forward;
        return MathF.Abs(Vector3.Dot(right * halfSize.x, axis)) +
               MathF.Abs(Vector3.Dot(up * halfSize.y, axis)) +
               MathF.Abs(Vector3.Dot(forward * halfSize.z, axis));
    }

    private static bool IntersectSphereBox(Vector3 sphereCenter, float radius, Vector3 boxCenter, Vector3 boxSize, Quaternion boxRotation, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;

        var invRotation = Quaternion.Inverse(boxRotation);
        var localSphere = invRotation * (sphereCenter - boxCenter);
        var half = boxSize * 0.5f;

        var closest = new Vector3(
            Math.Clamp(localSphere.x, -half.x, half.x),
            Math.Clamp(localSphere.y, -half.y, half.y),
            Math.Clamp(localSphere.z, -half.z, half.z));

        var delta = localSphere - closest;
        var distance = delta.magnitude;
        if (distance >= radius) return false;

        if (distance < 1e-6f)
        {
            normal = (sphereCenter - boxCenter).normalized;
            if (normal.magnitude < 1e-6f) normal = Vector3.up;
            penetration = radius;
            return true;
        }

        penetration = radius - distance;
        normal = boxRotation * delta.normalized;
        return true;
    }

    private static bool IntersectCapsuleCapsule(Collider a, CapsuleCollider ca, Collider b, CapsuleCollider cb, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        GetCapsulePoints(a, ca, out var a0, out var a1, out var ra);
        GetCapsulePoints(b, cb, out var b0, out var b1, out var rb);

        var (pa, pb) = ClosestPointOnSegments(a0, a1, b0, b1);
        var delta = pb - pa;
        var distance = delta.magnitude;
        var sumRadius = ra + rb;
        if (distance >= sumRadius || distance < 1e-6f) return false;

        penetration = sumRadius - distance;
        normal = delta.normalized;
        return true;
    }

    private static bool IntersectCapsuleSphere(Collider a, CapsuleCollider capsule, Vector3 sphereCenter, float sphereRadius, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        GetCapsulePoints(a, capsule, out var p0, out var p1, out var radius);
        var closest = ClosestPointOnSegment(sphereCenter, p0, p1);
        var delta = sphereCenter - closest;
        var distance = delta.magnitude;
        var sumRadius = radius + sphereRadius;
        if (distance >= sumRadius || distance < 1e-6f) return false;

        penetration = sumRadius - distance;
        normal = delta.normalized;
        return true;
    }

    private static bool IntersectCapsuleBox(Collider a, CapsuleCollider capsule, Vector3 boxCenter, Vector3 boxSize, Quaternion boxRotation, out Vector3 normal, out float penetration)
    {
        GetCapsulePoints(a, capsule, out var p0, out var p1, out var radius);
        return IntersectCapsuleBox(p0, p1, radius, boxCenter, boxSize, boxRotation, out normal, out penetration);
    }

    private static bool IntersectCapsuleBox(Vector3 point0, Vector3 point1, float radius, Vector3 boxCenter, Vector3 boxSize, Quaternion boxRotation, out Vector3 normal, out float penetration)
    {
        normal = Vector3.up;
        penetration = 0f;
        var axis = point1 - point0;
        var length = axis.magnitude;
        var steps = Math.Max(1, (int)(length / Math.Max(radius, 0.001f)));
        var step = length / steps;
        var dir = length > 1e-6f ? axis.normalized : Vector3.up;

        for (var i = 0; i <= steps; i++)
        {
            var p = point0 + dir * (i * step);
            if (IntersectSphereBox(p, radius, boxCenter, boxSize, boxRotation, out var n, out var pen))
            {
                if (pen > penetration)
                {
                    penetration = pen;
                    normal = n;
                }
            }
        }

        return penetration > 0f;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
        t = Math.Clamp(t, 0f, 1f);
        return a + ab * t;
    }

    private static (Vector3, Vector3) ClosestPointOnSegments(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        // Use midpoint approximations for segment-segment closest points.
        var bestA = a0;
        var bestB = b0;
        var bestDist = (bestB - bestA).magnitude;
        var steps = 8;
        for (var i = 0; i <= steps; i++)
        {
            var ta = i / (float)steps;
            var pa = Vector3.Lerp(a0, a1, ta);
            for (var j = 0; j <= steps; j++)
            {
                var tb = j / (float)steps;
                var pb = Vector3.Lerp(b0, b1, tb);
                var d = (pb - pa).magnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestA = pa;
                    bestB = pb;
                }
            }
        }

        return (bestA, bestB);
    }

    private static void ResolveCollision(Collider a, Collider b, Vector3 normal, float penetration)
    {
        var rbA = a.attachedRigidbody;
        var rbB = b.attachedRigidbody;

        if ((rbA is null || rbA.isKinematic) && (rbB is null || rbB.isKinematic))
        {
            return;
        }

        var transformA = a.transform;
        var transformB = b.transform;

        var inverseMassA = rbA is not null && !rbA.isKinematic ? 1f / rbA.mass : 0f;
        var inverseMassB = rbB is not null && !rbB.isKinematic ? 1f / rbB.mass : 0f;
        var totalInverseMass = inverseMassA + inverseMassB;
        if (totalInverseMass <= 1e-6f) return;

        var percent = 0.8f;
        var slop = 0.01f;
        var correction = normal * (MathF.Max(penetration - slop, 0f) / totalInverseMass * percent);

        if (transformA is not null && inverseMassA > 0f)
        {
            transformA.localPosition -= correction * inverseMassA;
        }

        if (transformB is not null && inverseMassB > 0f)
        {
            transformB.localPosition += correction * inverseMassB;
        }

        var velocityA = rbA?.velocity ?? Vector3.zero;
        var velocityB = rbB?.velocity ?? Vector3.zero;
        var relativeVelocity = velocityA - velocityB;
        var velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal > 0f) return;

        var restitution = 0f;
        var impulseScalar = -(1f + restitution) * velocityAlongNormal / totalInverseMass;
        var impulse = normal * impulseScalar;

        if (rbA is not null && inverseMassA > 0f)
        {
            rbA.velocity += impulse * inverseMassA;
        }

        if (rbB is not null && inverseMassB > 0f)
        {
            rbB.velocity -= impulse * inverseMassB;
        }
    }

    private static void DispatchCollision(Collider a, Collider b, Vector3 normal)
    {
        var contact = new Collision(a, b, normal);
        DispatchMessage(a.gameObject, "OnCollisionEnter", contact);
        DispatchMessage(b.gameObject, "OnCollisionEnter", contact);
    }

    private static void DispatchTrigger(Collider a, Collider b)
    {
        DispatchMessage(a.gameObject, "OnTriggerEnter", b);
        DispatchMessage(b.gameObject, "OnTriggerEnter", a);
    }

    private static void DispatchMessage(GameObject? target, string methodName, object? arg)
    {
        if (target is null) return;

        var behaviours = target.GetComponents<MonoBehaviour>();
        foreach (var behaviour in behaviours)
        {
            if (behaviour is null || !behaviour.enabled) continue;

            var method = behaviour.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { arg?.GetType() ?? typeof(object) },
                null);

            if (method is not null)
            {
                try
                {
                    method.Invoke(behaviour, new[] { arg });
                }
                catch
                {
                    // Ignore reflection errors in compatibility layer.
                }
            }
        }
    }

    private static Vector3 GetWorldPosition(Collider collider, Vector3 localOffset)
    {
        var transform = collider.transform;
        if (transform is null) return localOffset;
        return transform.TransformPoint(localOffset);
    }

    public static bool OverlapSphere(Vector3 position, float radius, int layerMask, Collider[]? results)
    {
        var found = 0;
        foreach (var collider in _colliders)
        {
            if (collider is null || collider.IsDestroyed || !collider.enabled) continue;
            if (!Physics.LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;

            if (TryOverlapSphere(position, radius, collider))
            {
                if (results is not null && found < results.Length) results[found] = collider;
                found++;
            }
        }

        return found > 0;
    }

    private static bool TryOverlapSphere(Vector3 position, float radius, Collider collider)
    {
        switch (collider)
        {
            case SphereCollider sphere:
                var center = GetWorldPosition(collider, sphere.center);
                return (center - position).magnitude <= radius + sphere.radius;
            case BoxCollider box:
                var boxCenter = GetWorldPosition(collider, box.center);
                return IntersectSphereBox(position, radius, boxCenter, box.size, collider.transform?.rotation ?? Quaternion.identity, out _, out _);
            case CapsuleCollider capsule:
                GetCapsulePoints(collider, capsule, out var p0, out var p1, out var capRadius);
                var closest = ClosestPointOnSegment(position, p0, p1);
                return (position - closest).magnitude <= radius + capRadius;
        }

        return false;
    }

    public static bool OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, Collider[]? results)
    {
        var found = 0;
        foreach (var collider in _colliders)
        {
            if (collider is null || collider.IsDestroyed || !collider.enabled) continue;
            if (!Physics.LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;

            if (TryOverlapBox(center, halfExtents, orientation, collider))
            {
                if (results is not null && found < results.Length) results[found] = collider;
                found++;
            }
        }

        return found > 0;
    }

    private static bool TryOverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, Collider collider)
    {
        switch (collider)
        {
            case BoxCollider box:
                var otherCenter = GetWorldPosition(collider, box.center);
                return IntersectBoxBox(center, halfExtents * 2f, orientation, otherCenter, box.size, collider.transform?.rotation ?? Quaternion.identity, out _, out _);
            case SphereCollider sphere:
                var sphereCenter = GetWorldPosition(collider, sphere.center);
                return IntersectSphereBox(sphereCenter, sphere.radius, center, halfExtents * 2f, orientation, out _, out _);
            case CapsuleCollider capsule:
                return IntersectCapsuleBox(collider, capsule, center, halfExtents * 2f, orientation, out _, out _);
        }

        return false;
    }

    public static bool OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, Collider[]? results)
    {
        var found = 0;
        foreach (var collider in _colliders)
        {
            if (collider is null || collider.IsDestroyed || !collider.enabled) continue;
            if (!Physics.LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;

            if (TryOverlapCapsule(point0, point1, radius, collider))
            {
                if (results is not null && found < results.Length) results[found] = collider;
                found++;
            }
        }

        return found > 0;
    }

    private static bool TryOverlapCapsule(Vector3 point0, Vector3 point1, float radius, Collider collider)
    {
        switch (collider)
        {
            case SphereCollider sphere:
                var center = GetWorldPosition(collider, sphere.center);
                var closest = ClosestPointOnSegment(center, point0, point1);
                return (center - closest).magnitude <= radius + sphere.radius;
            case BoxCollider box:
                var boxCenter = GetWorldPosition(collider, box.center);
                return IntersectCapsuleBox(point0, point1, radius, boxCenter, box.size, collider.transform?.rotation ?? Quaternion.identity, out _, out _);
            case CapsuleCollider capsule:
                GetCapsulePoints(collider, capsule, out var c0, out var c1, out var capRadius);
                var (pa, pb) = ClosestPointOnSegments(point0, point1, c0, c1);
                return (pb - pa).magnitude <= radius + capRadius;
        }

        return false;
    }
}

public class Collision
{
    public Collider collider { get; }
    public Collider otherCollider { get; }
    public Vector3 relativeVelocity { get; }
    public Rigidbody? rigidbody => collider?.attachedRigidbody;
    public Rigidbody? otherRigidbody => otherCollider?.attachedRigidbody;
    public Transform? transform => collider?.transform;
    public GameObject? gameObject => collider?.gameObject;
    public ContactPoint[] contacts { get; }

    public Collision(Collider a, Collider b, Vector3 normal)
    {
        collider = a;
        otherCollider = b;
        relativeVelocity = (a.attachedRigidbody?.velocity ?? Vector3.zero) - (b.attachedRigidbody?.velocity ?? Vector3.zero);
        contacts = new[] { new ContactPoint { point = (a.bounds.center + b.bounds.center) * 0.5f, normal = normal, thisCollider = a, otherCollider = b } };
    }
}

public struct ContactPoint
{
    public Vector3 point;
    public Vector3 normal;
    public Collider? thisCollider;
    public Collider? otherCollider;
}
