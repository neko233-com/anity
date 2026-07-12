using System;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

/// <summary>
/// Parameterized continuous collision detection (CCD) / TOI solvers.
/// Replaces pure discrete step approximation for high-speed bodies.
/// </summary>
public static class ContinuousCollision
{
    /// <summary>
    /// Sphere vs sphere TOI along linear motion of A (B static for this step).
    /// Returns true if collision in [0, maxDistance], with fraction t in [0,1] of the displacement.
    /// </summary>
    public static bool SphereSphereTOI(
        Vector3 posA, float radiusA, Vector3 velocityA,
        Vector3 posB, float radiusB,
        float deltaTime, out float timeOfImpact, out Vector3 contactNormal, out Vector3 contactPoint)
    {
        timeOfImpact = 0f;
        contactNormal = Vector3.up;
        contactPoint = posA;

        // Prefer anity-native C++ TOI (Unity native physics path parity)
        if (AnityNative.TrySphereSphereTOI(
                posA.x, posA.y, posA.z, radiusA,
                velocityA.x, velocityA.y, velocityA.z,
                posB.x, posB.y, posB.z, radiusB,
                deltaTime, out timeOfImpact, out float nx, out float ny, out float nz))
        {
            contactNormal = new Vector3(nx, ny, nz);
            contactPoint = posB + contactNormal * radiusB;
            return true;
        }

        Vector3 relPos = posA - posB;
        Vector3 relVel = velocityA;
        float r = radiusA + radiusB;
        float r2 = r * r;

        float c = Vector3.Dot(relPos, relPos) - r2;
        if (c < 0f)
        {
            timeOfImpact = 0f;
            float dist = relPos.magnitude;
            contactNormal = dist > 1e-6f ? relPos / dist : Vector3.up;
            contactPoint = posB + contactNormal * radiusB;
            return true;
        }

        float a = Vector3.Dot(relVel, relVel);
        if (a < 1e-12f) return false;

        float b = Vector3.Dot(relPos, relVel);
        if (b >= 0f) return false;

        float discr = b * b - a * c;
        if (discr < 0f) return false;

        float t = (-b - MathF.Sqrt(discr)) / a;
        if (t < 0f || t > deltaTime) return false;

        timeOfImpact = t;
        Vector3 posAtHit = posA + velocityA * t;
        Vector3 n = posAtHit - posB;
        float nLen = n.magnitude;
        contactNormal = nLen > 1e-6f ? n / nLen : Vector3.up;
        contactPoint = posB + contactNormal * radiusB;
        return true;
    }

    /// <summary>
    /// Sphere vs AABB (axis-aligned box) continuous cast via expanded AABB raycast.
    /// </summary>
    public static bool SphereAABBTOI(
        Vector3 pos, float radius, Vector3 velocity,
        Vector3 boxCenter, Vector3 boxHalfExtents,
        float deltaTime, out float timeOfImpact, out Vector3 contactNormal, out Vector3 contactPoint)
    {
        timeOfImpact = 0f;
        contactNormal = Vector3.up;
        contactPoint = pos;

        Vector3 expanded = boxHalfExtents + new Vector3(radius, radius, radius);
        Vector3 min = boxCenter - expanded;
        Vector3 max = boxCenter + expanded;

        // Ray vs AABB slab method for ray origin=pos, dir=velocity over [0, deltaTime]
        float tmin = 0f;
        float tmax = deltaTime;
        Vector3 hitNormal = Vector3.zero;
        Vector3 origin = pos;
        Vector3 dir = velocity;

        for (int i = 0; i < 3; i++)
        {
            float o = origin[i];
            float d = dir[i];
            float mn = min[i];
            float mx = max[i];

            if (MathF.Abs(d) < 1e-12f)
            {
                if (o < mn || o > mx) return false;
                continue;
            }

            float inv = 1f / d;
            float t1 = (mn - o) * inv;
            float t2 = (mx - o) * inv;
            float nSign = -1f;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                nSign = 1f;
            }

            if (t1 > tmin)
            {
                tmin = t1;
                hitNormal = Vector3.zero;
                hitNormal[i] = nSign * MathF.Sign(d) * -1f;
                if (MathF.Abs(hitNormal[i]) < 1e-6f)
                    hitNormal[i] = nSign;
            }
            tmax = MathF.Min(tmax, t2);
            if (tmin > tmax) return false;
        }

        if (tmin < 0f)
        {
            // inside expanded box
            timeOfImpact = 0f;
            Vector3 toCenter = pos - boxCenter;
            float ax = MathF.Abs(toCenter.x) - boxHalfExtents.x;
            float ay = MathF.Abs(toCenter.y) - boxHalfExtents.y;
            float az = MathF.Abs(toCenter.z) - boxHalfExtents.z;
            if (ax >= ay && ax >= az) contactNormal = new Vector3(MathF.Sign(toCenter.x), 0, 0);
            else if (ay >= az) contactNormal = new Vector3(0, MathF.Sign(toCenter.y), 0);
            else contactNormal = new Vector3(0, 0, MathF.Sign(toCenter.z));
            contactPoint = pos - contactNormal * radius;
            return true;
        }

        if (tmin > deltaTime) return false;

        timeOfImpact = tmin;
        contactNormal = hitNormal.sqrMagnitude > 1e-8f ? hitNormal.normalized : Vector3.up;
        contactPoint = pos + velocity * tmin - contactNormal * radius;
        return true;
    }

    /// <summary>
    /// Integrate a rigidbody with Continuous / ContinuousDynamic CCD using parametric TOI.
    /// Falls back to discrete integration when mode is Discrete or velocity is small.
    /// </summary>
    internal static void IntegrateWithCCD(Rigidbody rb, float deltaTime, PhysicsWorld world)
    {
        if (rb == null || rb.isKinematic || rb.IsSleeping()) return;
        if (rb.transform == null)
        {
            rb.Integrate(deltaTime);
            return;
        }

        var mode = rb.collisionDetectionMode;
        if (mode == CollisionDetectionMode.Discrete || rb.velocity.sqrMagnitude < 1e-6f)
        {
            rb.Integrate(deltaTime);
            return;
        }

        float radius = EstimateBodyRadius(rb);
        Vector3 start = rb.transform.position;
        Vector3 vel = rb.velocity;
        float bestT = deltaTime;
        Vector3 bestN = Vector3.up;
        Collider? hitCollider = null;

        var colliders = world._colliders;
        for (int i = 0; i < colliders.Count; i++)
        {
            var c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (c.attachedRigidbody == rb) continue;

            var shape = c.GetShape();
            float t;
            Vector3 n, pt;
            bool hit = false;

            if (shape.Type == ColliderShapeType.Sphere)
            {
                hit = SphereSphereTOI(start, radius, vel, shape.Center, shape.Radius, deltaTime, out t, out n, out pt);
            }
            else if (shape.Type == ColliderShapeType.Box)
            {
                hit = SphereAABBTOI(start, radius, vel, shape.Center, shape.Size * 0.5f, deltaTime, out t, out n, out pt);
            }
            else
            {
                // capsule / others: expanded sphere cast approximation via world SphereCast
                hit = world.SphereCast(start, radius, vel.normalized, out var rayHit, vel.magnitude * deltaTime + radius, -1, QueryTriggerInteraction.Ignore);
                if (hit)
                {
                    t = rayHit.distance / MathF.Max(vel.magnitude, 1e-6f);
                    n = rayHit.normal;
                    pt = rayHit.point;
                }
                else
                {
                    t = deltaTime; n = Vector3.up; pt = start;
                }
            }

            if (hit && t < bestT)
            {
                bestT = MathF.Max(0f, t - 1e-4f);
                bestN = n;
                hitCollider = c;
            }
        }

        if (hitCollider != null && bestT < deltaTime)
        {
            // advance to TOI and cancel velocity into surface
            rb.transform.position = start + vel * bestT;
            float vn = Vector3.Dot(rb.velocity, bestN);
            if (vn < 0f)
                rb.velocity = rb.velocity - bestN * vn; // slide / stop penetration component
            // residual time optional sub-step (single TOI for now)
        }
        else
        {
            rb.Integrate(deltaTime);
        }
    }

    public static float EstimateBodyRadius(Rigidbody rb)
    {
        if (rb == null) return 0.5f;
        var cols = rb.GetComponents<Collider>();
        float maxR = 0.25f;
        if (cols != null)
        {
            foreach (var c in cols)
            {
                if (c == null) continue;
                var s = c.GetShape();
                float r = s.Type switch
                {
                    ColliderShapeType.Sphere => s.Radius,
                    ColliderShapeType.Capsule => s.Radius + s.Height * 0.5f,
                    ColliderShapeType.Box => s.Size.magnitude * 0.5f,
                    _ => 0.5f
                };
                if (r > maxR) maxR = r;
            }
        }
        return maxR;
    }
}
