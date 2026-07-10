using System;
using System.Collections.Generic;

namespace UnityEngine;

internal enum ColliderShapeType2D
{
  Box,
  Circle,
  Capsule,
  Polygon
}

internal readonly struct ColliderShape2D
{
  public readonly ColliderShapeType2D type;
  public readonly Vector2 offset;
  public readonly Vector2 size;
  public readonly float radius;
  public readonly Vector2[] points;

  public ColliderShape2D(ColliderShapeType2D type, Vector2 offset, Vector2 size, float radius, Vector2[]? points = null)
  {
    this.type = type;
    this.offset = offset;
    this.size = size;
    this.radius = radius;
    this.points = points ?? Array.Empty<Vector2>();
  }
}

internal static class Physics2DWorld
{
  private static readonly List<Collider2D> _colliders = new();
  private static readonly List<Rigidbody2D> _rigidbodies = new();

  public static void Register(Collider2D collider)
  {
    if (collider is null || _colliders.Contains(collider))
    {
      return;
    }

    _colliders.Add(collider);
  }

  public static void Unregister(Collider2D collider)
  {
    if (collider is null)
    {
      return;
    }

    _ = _colliders.Remove(collider);
  }

  public static void Register(Rigidbody2D rigidbody)
  {
    if (rigidbody is null || _rigidbodies.Contains(rigidbody))
    {
      return;
    }

    _rigidbodies.Add(rigidbody);
  }

  public static void Unregister(Rigidbody2D rigidbody)
  {
    if (rigidbody is null)
    {
      return;
    }

    _ = _rigidbodies.Remove(rigidbody);
  }

  public static IReadOnlyList<Collider2D> GetColliders() => _colliders;

  public static IReadOnlyList<Rigidbody2D> GetRigidbodies() => _rigidbodies;

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
      if (rb is null || rb.IsDestroyed || rb.bodyType == RigidbodyType2D.Static)
      {
        continue;
      }

      if (!rb.isKinematic)
      {
        rb.velocity += Physics2D.gravity * rb.gravityScale * deltaTime;
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
      if (transform is null)
      {
        continue;
      }

      var position = transform.localPosition;
      position.x += rb.velocity.x * deltaTime;
      position.y += rb.velocity.y * deltaTime;
      transform.localPosition = position;

      if (MathF.Abs(rb.angularVelocity) > 1e-6f)
      {
        var angles = transform.localEulerAngles;
        angles.z += rb.angularVelocity * deltaTime;
        transform.localEulerAngles = angles;
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
      if (a is null || a.IsDestroyed || !a.enabled)
      {
        continue;
      }

      for (var j = i + 1; j < count; j++)
      {
        var b = _colliders[j];
        if (b is null || b.IsDestroyed || !b.enabled)
        {
          continue;
        }

        if (!CanLayersCollide(a.gameObject?.layer ?? 0, b.gameObject?.layer ?? 0))
        {
          continue;
        }

        if (a.isTrigger || b.isTrigger)
        {
          if (Intersect(a, b))
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

  public static bool Intersect(Collider2D a, Collider2D b)
  {
    return Intersect(a, b, out _, out _);
  }

  public static bool Intersect(Collider2D a, Collider2D b, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var shapeA = a.GetShape();
    var shapeB = b.GetShape();
    var posA = GetWorldPosition(a, shapeA.offset);
    var posB = GetWorldPosition(b, shapeB.offset);

    if (shapeA.type == ColliderShapeType2D.Polygon || shapeB.type == ColliderShapeType2D.Polygon)
    {
      return IntersectPolygonPolygon(a, shapeA, posA, b, shapeB, posB, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Box && shapeB.type == ColliderShapeType2D.Box)
    {
      return IntersectBoxBox(posA, shapeA.size, posB, shapeB.size, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Circle && shapeB.type == ColliderShapeType2D.Circle)
    {
      return IntersectCircleCircle(posA, shapeA.radius, posB, shapeB.radius, out normal, out penetration);
    }

    if ((shapeA.type == ColliderShapeType2D.Box || shapeA.type == ColliderShapeType2D.Capsule) &&
        (shapeB.type == ColliderShapeType2D.Box || shapeB.type == ColliderShapeType2D.Capsule))
    {
      return IntersectBoxBox(posA, shapeA.size, posB, shapeB.size, out normal, out penetration);
    }

    if ((shapeA.type == ColliderShapeType2D.Box || shapeA.type == ColliderShapeType2D.Capsule) && shapeB.type == ColliderShapeType2D.Circle)
    {
      return IntersectBoxCircle(posA, shapeA.size, posB, shapeB.radius, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Circle && (shapeB.type == ColliderShapeType2D.Box || shapeB.type == ColliderShapeType2D.Capsule))
    {
      var result = IntersectBoxCircle(posB, shapeB.size, posA, shapeA.radius, out var n, out penetration);
      normal = -n;
      return result;
    }

    return false;
  }

  private static Vector2 GetWorldPosition(Collider2D collider, Vector2 offset)
  {
    var transform = collider.transform;
    if (transform is null)
    {
      return offset;
    }

    var pos = transform.position;
    return new Vector2(pos.x + offset.x, pos.y + offset.y);
  }

  private static bool IntersectBoxBox(Vector2 a, Vector2 sizeA, Vector2 b, Vector2 sizeB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var halfA = sizeA * 0.5f;
    var halfB = sizeB * 0.5f;

    var dx = b.x - a.x;
    var px = halfA.x + halfB.x - MathF.Abs(dx);
    if (px <= 0f)
    {
      return false;
    }

    var dy = b.y - a.y;
    var py = halfA.y + halfB.y - MathF.Abs(dy);
    if (py <= 0f)
    {
      return false;
    }

    if (px < py)
    {
      penetration = px;
      normal = dx > 0f ? Vector2.right : Vector2.left;
    }
    else
    {
      penetration = py;
      normal = dy > 0f ? Vector2.up : Vector2.down;
    }

    return true;
  }

  private static bool IntersectCircleCircle(Vector2 a, float radiusA, Vector2 b, float radiusB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var delta = b - a;
    var distance = delta.magnitude;
    var sumRadius = radiusA + radiusB;
    if (distance >= sumRadius || distance < 1e-6f)
    {
      return false;
    }

    penetration = sumRadius - distance;
    normal = delta.normalized;
    return true;
  }

  private static bool IntersectBoxCircle(Vector2 boxCenter, Vector2 boxSize, Vector2 circleCenter, float radius, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var half = boxSize * 0.5f;
    var closest = new Vector2(
      Math.Clamp(circleCenter.x, boxCenter.x - half.x, boxCenter.x + half.x),
      Math.Clamp(circleCenter.y, boxCenter.y - half.y, boxCenter.y + half.y));

    var delta = circleCenter - closest;
    var distance = delta.magnitude;
    if (distance >= radius)
    {
      return false;
    }

    if (distance < 1e-6f)
    {
      normal = (circleCenter - boxCenter).normalized;
      if (normal.magnitude < 1e-6f)
      {
        normal = Vector2.up;
      }

      penetration = radius;
      return true;
    }

    penetration = radius - distance;
    normal = delta.normalized;
    return true;
  }

  private static bool IntersectPolygonPolygon(Collider2D a, ColliderShape2D shapeA, Vector2 posA, Collider2D b, ColliderShape2D shapeB, Vector2 posB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var isEdgeA = a is EdgeCollider2D;
    var isEdgeB = b is EdgeCollider2D;
    var pointsA = TransformPoints(shapeA.points, posA);
    var pointsB = TransformPoints(shapeB.points, posB);

    if (pointsA.Length == 0 || pointsB.Length == 0)
    {
      return false;
    }

    // Edge vs anything: test segment intersections.
    if (isEdgeA || isEdgeB)
    {
      var edgePoints = isEdgeA ? pointsA : pointsB;
      var otherPoints = isEdgeA ? pointsB : pointsA;
      var otherIsBox = isEdgeA ? shapeB.type == ColliderShapeType2D.Box || shapeB.type == ColliderShapeType2D.Capsule : shapeA.type == ColliderShapeType2D.Box || shapeA.type == ColliderShapeType2D.Capsule;
      var otherBoxCenter = isEdgeA ? posB : posA;
      var otherBoxSize = isEdgeA ? shapeB.size : shapeA.size;
      var otherCircleCenter = isEdgeA ? posB : posA;
      var otherCircleRadius = isEdgeA ? shapeB.radius : shapeA.radius;
      var otherIsCircle = isEdgeA ? shapeB.type == ColliderShapeType2D.Circle : shapeA.type == ColliderShapeType2D.Circle;

      for (var i = 0; i < edgePoints.Length - 1; i++)
      {
        var segA = edgePoints[i];
        var segB = edgePoints[i + 1];

        if (otherIsBox)
        {
          if (SegmentIntersectBox(segA, segB, otherBoxCenter, otherBoxSize, out var n, out var pen))
          {
            normal = isEdgeA ? n : -n;
            penetration = MathF.Max(penetration, pen);
          }
        }
        else if (otherIsCircle)
        {
          if (SegmentIntersectCircle(segA, segB, otherCircleCenter, otherCircleRadius, out var n, out var pen))
          {
            normal = isEdgeA ? n : -n;
            penetration = MathF.Max(penetration, pen);
          }
        }
        else
        {
          for (var j = 0; j < otherPoints.Length - 1; j++)
          {
            if (SegmentsIntersect(segA, segB, otherPoints[j], otherPoints[j + 1], out var n, out var pen))
            {
              normal = isEdgeA ? n : -n;
              penetration = MathF.Max(penetration, pen);
            }
          }
        }
      }

      return penetration > 0f;
    }

    // Polygon vs polygon: SAT on combined edges.
    if (PolygonIntersectPolygon(pointsA, pointsB, out normal, out penetration))
    {
      return true;
    }

    // Polygon vs box/circle fallback.
    if (shapeB.type == ColliderShapeType2D.Box || shapeB.type == ColliderShapeType2D.Capsule)
    {
      return PolygonIntersectBox(pointsA, posB, shapeB.size, out normal, out penetration);
    }

    if (shapeB.type == ColliderShapeType2D.Circle)
    {
      return PolygonIntersectCircle(pointsA, posB, shapeB.radius, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Box || shapeA.type == ColliderShapeType2D.Capsule)
    {
      var result = PolygonIntersectBox(pointsB, posA, shapeA.size, out var n, out penetration);
      normal = -n;
      return result;
    }

    if (shapeA.type == ColliderShapeType2D.Circle)
    {
      var result = PolygonIntersectCircle(pointsB, posA, shapeA.radius, out var n, out penetration);
      normal = -n;
      return result;
    }

    return false;
  }

  private static Vector2[] TransformPoints(Vector2[] points, Vector2 offset)
  {
    if (points.Length == 0) return points;
    var result = new Vector2[points.Length];
    for (var i = 0; i < points.Length; i++)
    {
      result[i] = points[i] + offset;
    }

    return result;
  }

  private static bool SegmentIntersectCircle(Vector2 a, Vector2 b, Vector2 center, float radius, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;
    var closest = ClosestPointOnSegment(center, a, b);
    var delta = center - closest;
    var distance = delta.magnitude;
    if (distance >= radius || distance < 1e-6f) return false;
    penetration = radius - distance;
    normal = delta.normalized;
    return true;
  }

  private static bool SegmentIntersectBox(Vector2 a, Vector2 b, Vector2 center, Vector2 size, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;
    var half = size * 0.5f;
    var min = center - half;
    var max = center + half;

    // Quick AABB reject for segment.
    if ((a.x < min.x && b.x < min.x) || (a.x > max.x && b.x > max.x) ||
        (a.y < min.y && b.y < min.y) || (a.y > max.y && b.y > max.y))
    {
      return false;
    }

    // Check if either endpoint is inside.
    if (a.x >= min.x && a.x <= max.x && a.y >= min.y && a.y <= max.y)
    {
      penetration = half.magnitude;
      normal = (a - center).normalized;
      if (normal.magnitude < 1e-6f) normal = Vector2.up;
      return true;
    }

    if (b.x >= min.x && b.x <= max.x && b.y >= min.y && b.y <= max.y)
    {
      penetration = half.magnitude;
      normal = (b - center).normalized;
      if (normal.magnitude < 1e-6f) normal = Vector2.up;
      return true;
    }

    // Test segment intersection with box edges.
    var edges = new (Vector2, Vector2)[]
    {
      (new Vector2(min.x, min.y), new Vector2(max.x, min.y)),
      (new Vector2(max.x, min.y), new Vector2(max.x, max.y)),
      (new Vector2(max.x, max.y), new Vector2(min.x, max.y)),
      (new Vector2(min.x, max.y), new Vector2(min.x, min.y))
    };

    var hit = false;
    foreach (var (e0, e1) in edges)
    {
      if (SegmentsIntersect(a, b, e0, e1, out var n, out _))
      {
        normal = n;
        penetration = 0.01f;
        hit = true;
      }
    }

    return hit;
  }

  private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0.01f;

    var d1 = a2 - a1;
    var d2 = b2 - b1;
    var denom = d1.x * d2.y - d1.y * d2.x;
    if (MathF.Abs(denom) < 1e-6f) return false;

    var c = b1 - a1;
    var t = (c.x * d2.y - c.y * d2.x) / denom;
    var u = (c.x * d1.y - c.y * d1.x) / denom;

    if (t < 0f || t > 1f || u < 0f || u > 1f) return false;

    var perp = new Vector2(-d2.y, d2.x).normalized;
    if (perp.magnitude < 1e-6f) return false;
    normal = perp;
    return true;
  }

  private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
  {
    var ab = b - a;
    var t = Vector2.Dot(point - a, ab) / Vector2.Dot(ab, ab);
    t = Math.Clamp(t, 0f, 1f);
    return a + ab * t;
  }

  private static bool PolygonIntersectPolygon(Vector2[] polyA, Vector2[] polyB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = float.PositiveInfinity;

    var axes = new List<Vector2>();
    AddEdges(polyA, axes);
    AddEdges(polyB, axes);

    foreach (var axis in axes)
    {
      if (!TestAxis(axis, polyA, polyB, out var pen))
      {
        return false;
      }

      if (pen < penetration)
      {
        penetration = pen;
        normal = axis;
      }
    }

    return true;
  }

  private static bool PolygonIntersectBox(Vector2[] poly, Vector2 center, Vector2 size, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = float.PositiveInfinity;

    var axes = new List<Vector2>();
    AddEdges(poly, axes);
    axes.Add(Vector2.right);
    axes.Add(Vector2.up);

    var half = size * 0.5f;
    var boxCorners = new[] { center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y), center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y) };

    foreach (var axis in axes)
    {
      if (!TestAxis(axis, poly, boxCorners, out var pen))
      {
        return false;
      }

      if (pen < penetration)
      {
        penetration = pen;
        normal = axis;
      }
    }

    return true;
  }

  private static bool PolygonIntersectCircle(Vector2[] poly, Vector2 center, float radius, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;
    var hit = false;

    if (PointInPolygon(center, poly))
    {
      penetration = radius;
      normal = (center - Centroid(poly)).normalized;
      if (normal.magnitude < 1e-6f) normal = Vector2.up;
      return true;
    }

    for (var i = 0; i < poly.Length; i++)
    {
      var a = poly[i];
      var b = poly[(i + 1) % poly.Length];
      if (SegmentIntersectCircle(a, b, center, radius, out var n, out var pen))
      {
        normal = n;
        penetration = MathF.Max(penetration, pen);
        hit = true;
      }
    }

    return hit;
  }

  private static void AddEdges(Vector2[] poly, List<Vector2> axes)
  {
    for (var i = 0; i < poly.Length; i++)
    {
      var a = poly[i];
      var b = poly[(i + 1) % poly.Length];
      var edge = b - a;
      var normal = new Vector2(-edge.y, edge.x).normalized;
      if (normal.magnitude > 1e-6f)
      {
        axes.Add(normal);
      }
    }
  }

  private static bool TestAxis(Vector2 axis, Vector2[] polyA, Vector2[] polyB, out float penetration)
  {
    penetration = 0f;
    Project(polyA, axis, out var minA, out var maxA);
    Project(polyB, axis, out var minB, out var maxB);
    if (maxA < minB || maxB < minA) return false;
    penetration = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);
    return true;
  }

  private static void Project(Vector2[] poly, Vector2 axis, out float min, out float max)
  {
    min = float.PositiveInfinity;
    max = float.NegativeInfinity;
    foreach (var p in poly)
    {
      var d = Vector2.Dot(p, axis);
      min = MathF.Min(min, d);
      max = MathF.Max(max, d);
    }
  }

  private static bool PointInPolygon(Vector2 point, Vector2[] poly)
  {
    var inside = false;
    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
    {
      var pi = poly[i];
      var pj = poly[j];
      if (((pi.y > point.y) != (pj.y > point.y)) &&
          (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
      {
        inside = !inside;
      }
    }

    return inside;
  }

  private static Vector2 Centroid(Vector2[] poly)
  {
    var c = Vector2.zero;
    foreach (var p in poly) c += p;
    return c / Math.Max(1, poly.Length);
  }

  private static void ResolveCollision(Collider2D a, Collider2D b, Vector2 normal, float penetration)
  {
    var rbA = a.attachedRigidbody;
    var rbB = b.attachedRigidbody;

    if ((rbA is null || rbA.isKinematic || rbA.bodyType == RigidbodyType2D.Static) &&
        (rbB is null || rbB.isKinematic || rbB.bodyType == RigidbodyType2D.Static))
    {
      return;
    }

    var transformA = a.transform;
    var transformB = b.transform;
    if (transformA is null && transformB is null)
    {
      return;
    }

    var inverseMassA = rbA is not null && !rbA.isKinematic && rbA.bodyType != RigidbodyType2D.Static ? 1f / rbA.mass : 0f;
    var inverseMassB = rbB is not null && !rbB.isKinematic && rbB.bodyType != RigidbodyType2D.Static ? 1f / rbB.mass : 0f;
    var totalInverseMass = inverseMassA + inverseMassB;
    if (totalInverseMass <= 1e-6f)
    {
      return;
    }

    var percent = 0.8f;
    var slop = 0.01f;
    var correction = normal * (MathF.Max(penetration - slop, 0f) / totalInverseMass * percent);

    if (transformA is not null && inverseMassA > 0f)
    {
      var pos = transformA.localPosition;
      pos.x -= correction.x * inverseMassA;
      pos.y -= correction.y * inverseMassA;
      transformA.localPosition = pos;
    }

    if (transformB is not null && inverseMassB > 0f)
    {
      var pos = transformB.localPosition;
      pos.x += correction.x * inverseMassB;
      pos.y += correction.y * inverseMassB;
      transformB.localPosition = pos;
    }

    var velocityA = rbA?.velocity ?? Vector2.zero;
    var velocityB = rbB?.velocity ?? Vector2.zero;
    var relativeVelocity = velocityA - velocityB;
    var velocityAlongNormal = relativeVelocity.x * normal.x + relativeVelocity.y * normal.y;

    if (velocityAlongNormal > 0f)
    {
      return;
    }

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

  private static void DispatchCollision(Collider2D a, Collider2D b, Vector2 normal)
  {
    var contact = new Collision2D(a, b, normal);
    DispatchMessage(a.gameObject, "OnCollisionEnter2D", contact);
    DispatchMessage(b.gameObject, "OnCollisionEnter2D", contact);
  }

  private static void DispatchTrigger(Collider2D a, Collider2D b)
  {
    DispatchMessage(a.gameObject, "OnTriggerEnter2D", b);
    DispatchMessage(b.gameObject, "OnTriggerEnter2D", a);
  }

  private static void DispatchMessage(GameObject? target, string methodName, object? arg)
  {
    if (target is null)
    {
      return;
    }

    var behaviours = target.GetComponents<MonoBehaviour>();
    foreach (var behaviour in behaviours)
    {
      if (behaviour is null || !behaviour.enabled)
      {
        continue;
      }

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

  public static bool CanLayersCollide(int layerA, int layerB)
  {
    return Physics2D.IsLayerCollisionEnabled(layerA, layerB);
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask)
  {
    hitInfo = new RaycastHit2D();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f)
    {
      return false;
    }

    var closest = distance;
    Collider2D? closestCollider = null;

    foreach (var collider in _colliders)
    {
      if (collider is null || collider.IsDestroyed || !collider.enabled)
      {
        continue;
      }

      if (!LayerMatches(collider.gameObject?.layer ?? 0, layerMask))
      {
        continue;
      }

      if (RaycastCollider(origin, direction, collider, out var t) && t < closest && t >= 0f)
      {
        closest = t;
        closestCollider = collider;
      }
    }

    if (closestCollider is null)
    {
      return false;
    }

    var point = origin + direction * closest;
    hitInfo = new RaycastHit2D
    {
      collider = closestCollider,
      rigidbody = closestCollider.attachedRigidbody,
      transform = closestCollider.transform,
      point = point,
      normal = -direction,
      distance = closest,
      fraction = closest / distance
    };

    return true;
  }

  private static bool RaycastCollider(Vector2 origin, Vector2 direction, Collider2D collider, out float distance)
  {
    distance = float.PositiveInfinity;
    var shape = collider.GetShape();
    var center = GetWorldPosition(collider, shape.offset);

    if (shape.type == ColliderShapeType2D.Box || shape.type == ColliderShapeType2D.Capsule)
    {
      return RaycastBox(origin, direction, center, shape.size, out distance);
    }

    if (shape.type == ColliderShapeType2D.Circle)
    {
      return RaycastCircle(origin, direction, center, shape.radius, out distance);
    }

    if (shape.type == ColliderShapeType2D.Polygon)
    {
      return RaycastPolygon(origin, direction, TransformPoints(shape.points, center), out distance);
    }

    return false;
  }

  private static bool RaycastPolygon(Vector2 origin, Vector2 direction, Vector2[] points, out float distance)
  {
    distance = float.PositiveInfinity;
    if (points.Length < 2) return false;

    var hit = false;
    for (var i = 0; i < points.Length - 1; i++)
    {
      if (RaycastSegment(origin, direction, points[i], points[i + 1], out var t) && t < distance)
      {
        distance = t;
        hit = true;
      }
    }

    return hit;
  }

  private static bool RaycastSegment(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b, out float distance)
  {
    distance = float.PositiveInfinity;
    var seg = b - a;
    var denom = direction.x * seg.y - direction.y * seg.x;
    if (MathF.Abs(denom) < 1e-6f) return false;

    var diff = a - origin;
    var t = (diff.x * seg.y - diff.y * seg.x) / denom;
    var u = (diff.x * direction.y - diff.y * direction.x) / denom;

    if (t < 0f || u < 0f || u > 1f) return false;
    distance = t;
    return true;
  }

  private static bool RaycastBox(Vector2 origin, Vector2 direction, Vector2 center, Vector2 size, out float distance)
  {
    distance = float.PositiveInfinity;
    var half = size * 0.5f;
    var min = center - half;
    var max = center + half;

    var tmin = (min.x - origin.x) / direction.x;
    var tmax = (max.x - origin.x) / direction.x;
    if (direction.x < 0f)
    {
      (tmin, tmax) = (tmax, tmin);
    }

    var tymin = (min.y - origin.y) / direction.y;
    var tymax = (max.y - origin.y) / direction.y;
    if (direction.y < 0f)
    {
      (tymin, tymax) = (tymax, tymin);
    }

    if (tmin > tymax || tymin > tmax)
    {
      return false;
    }

    tmin = MathF.Max(tmin, tymin);
    tmax = MathF.Min(tmax, tymax);

    if (tmax < 0f)
    {
      return false;
    }

    distance = tmin >= 0f ? tmin : tmax;
    return true;
  }

  private static bool RaycastCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out float distance)
  {
    distance = float.PositiveInfinity;
    var oc = origin - center;
    var a = direction.x * direction.x + direction.y * direction.y;
    var b = 2f * (oc.x * direction.x + oc.y * direction.y);
    var c = oc.x * oc.x + oc.y * oc.y - radius * radius;
    var discriminant = b * b - 4f * a * c;

    if (discriminant < 0f)
    {
      return false;
    }

    var sqrt = MathF.Sqrt(discriminant);
    var t = (-b - sqrt) / (2f * a);
    if (t < 0f)
    {
      t = (-b + sqrt) / (2f * a);
    }

    if (t < 0f)
    {
      return false;
    }

    distance = t;
    return true;
  }

  public static bool OverlapCircle(Vector2 point, float radius, int layerMask, out Collider2D[] results)
  {
    var list = new List<Collider2D>();
    foreach (var collider in _colliders)
    {
      if (collider is null || collider.IsDestroyed || !collider.enabled)
      {
        continue;
      }

      if (!LayerMatches(collider.gameObject?.layer ?? 0, layerMask))
      {
        continue;
      }

      var shape = collider.GetShape();
      var center = GetWorldPosition(collider, shape.offset);
      if (TryOverlapShapeCircle(shape, center, point, radius))
      {
        list.Add(collider);
      }
    }

    results = list.ToArray();
    return results.Length > 0;
  }

  private static bool TryOverlapShapeCircle(ColliderShape2D shape, Vector2 center, Vector2 point, float radius)
  {
    if (shape.type == ColliderShapeType2D.Circle)
    {
      return (center - point).magnitude <= radius + shape.radius;
    }

    if (shape.type == ColliderShapeType2D.Box || shape.type == ColliderShapeType2D.Capsule)
    {
      return IntersectBoxCircle(center, shape.size, point, radius, out _, out _);
    }

    if (shape.type == ColliderShapeType2D.Polygon)
    {
      return PolygonIntersectCircle(TransformPoints(shape.points, center), point, radius, out _, out _);
    }

    return false;
  }

  public static bool OverlapBox(Vector2 point, Vector2 size, float angle, int layerMask, out Collider2D[] results)
  {
    _ = angle;
    var list = new List<Collider2D>();
    foreach (var collider in _colliders)
    {
      if (collider is null || collider.IsDestroyed || !collider.enabled)
      {
        continue;
      }

      if (!LayerMatches(collider.gameObject?.layer ?? 0, layerMask))
      {
        continue;
      }

      var shape = collider.GetShape();
      var center = GetWorldPosition(collider, shape.offset);
      if (TryOverlapShapeBox(shape, center, point, size))
      {
        list.Add(collider);
      }
    }

    results = list.ToArray();
    return results.Length > 0;
  }

  private static bool TryOverlapShapeBox(ColliderShape2D shape, Vector2 center, Vector2 point, Vector2 size)
  {
    if (shape.type == ColliderShapeType2D.Box || shape.type == ColliderShapeType2D.Capsule)
    {
      return IntersectBoxBox(point, size, center, shape.size, out _, out _);
    }

    if (shape.type == ColliderShapeType2D.Circle)
    {
      return IntersectBoxCircle(point, size, center, shape.radius, out _, out _);
    }

    if (shape.type == ColliderShapeType2D.Polygon)
    {
      return PolygonIntersectBox(TransformPoints(shape.points, center), point, size, out _, out _);
    }

    return false;
  }

  public static bool OverlapPoint(Vector2 point, int layerMask, out Collider2D[] results)
  {
    var list = new List<Collider2D>();
    foreach (var collider in _colliders)
    {
      if (collider is null || collider.IsDestroyed || !collider.enabled)
      {
        continue;
      }

      if (!LayerMatches(collider.gameObject?.layer ?? 0, layerMask))
      {
        continue;
      }

      var shape = collider.GetShape();
      var center = GetWorldPosition(collider, shape.offset);
      if (shape.type == ColliderShapeType2D.Box || shape.type == ColliderShapeType2D.Capsule)
      {
        var half = shape.size * 0.5f;
        if (point.x >= center.x - half.x && point.x <= center.x + half.x &&
            point.y >= center.y - half.y && point.y <= center.y + half.y)
        {
          list.Add(collider);
        }
      }
      else if (shape.type == ColliderShapeType2D.Circle)
      {
        if ((center - point).magnitude <= shape.radius)
        {
          list.Add(collider);
        }
      }
      else if (shape.type == ColliderShapeType2D.Polygon)
      {
        if (PointInPolygon(point, TransformPoints(shape.points, center)))
        {
          list.Add(collider);
        }
      }
    }

    results = list.ToArray();
    return results.Length > 0;
  }

  public static bool LayerMatches(int layer, int layerMask)
  {
    if (layerMask == -1)
    {
      return true;
    }

    return (layerMask & (1 << layer)) != 0;
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f || distance <= 0f) return false;

    var steps = Math.Max(1, (int)(distance / Math.Max(MathF.Min(size.x, size.y), 0.01f)));
    var step = distance / steps;

    for (var i = 0; i <= steps; i++)
    {
      var center = origin + direction * (i * step);
      foreach (var collider in _colliders)
      {
        if (collider is null || collider.IsDestroyed || !collider.enabled) continue;
        if (!LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;

        var shape = collider.GetShape();
        var pos = GetWorldPosition(collider, shape.offset);
        if (shape.type == ColliderShapeType2D.Box || shape.type == ColliderShapeType2D.Capsule)
        {
          if (IntersectBoxBox(center, size, pos, shape.size, out _, out _))
          {
            hitInfo = CreateHit(collider, origin, direction, i * step);
            return true;
          }
        }
        else if (shape.type == ColliderShapeType2D.Circle)
        {
          if (IntersectBoxCircle(center, size, pos, shape.radius, out _, out _))
          {
            hitInfo = CreateHit(collider, origin, direction, i * step);
            return true;
          }
        }
        else if (shape.type == ColliderShapeType2D.Polygon)
        {
          if (PolygonIntersectBox(TransformPoints(shape.points, pos), center, size, out _, out _))
          {
            hitInfo = CreateHit(collider, origin, direction, i * step);
            return true;
          }
        }
      }
    }

    return false;
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f || distance <= 0f) return false;

    var steps = Math.Max(1, (int)(distance / Math.Max(radius, 0.01f)));
    var step = distance / steps;

    for (var i = 0; i <= steps; i++)
    {
      var center = origin + direction * (i * step);
      foreach (var collider in _colliders)
      {
        if (collider is null || collider.IsDestroyed || !collider.enabled) continue;
        if (!LayerMatches(collider.gameObject?.layer ?? 0, layerMask)) continue;

        var shape = collider.GetShape();
        var pos = GetWorldPosition(collider, shape.offset);
        if (shape.type == ColliderShapeType2D.Circle)
        {
          if ((center - pos).magnitude <= radius + shape.radius)
          {
            hitInfo = CreateHit(collider, origin, direction, i * step);
            return true;
          }
        }
        else if (shape.type == ColliderShapeType2D.Box || shape.type == ColliderShapeType2D.Capsule)
        {
          if (IntersectBoxCircle(pos, shape.size, center, radius, out _, out _))
          {
            hitInfo = CreateHit(collider, origin, direction, i * step);
            return true;
          }
        }
        else if (shape.type == ColliderShapeType2D.Polygon)
        {
          if (PolygonIntersectCircle(TransformPoints(shape.points, pos), center, radius, out _, out _))
          {
            hitInfo = CreateHit(collider, origin, direction, i * step);
            return true;
          }
        }
      }
    }

    return false;
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float distance, Vector2 castDirection, int layerMask, out RaycastHit2D hitInfo)
  {
    // Approximate capsule cast with box cast using the same size.
    return BoxCast(origin, size, 0f, castDirection, distance, layerMask, out hitInfo);
  }

  private static RaycastHit2D CreateHit(Collider2D collider, Vector2 origin, Vector2 direction, float hitDistance)
  {
    var point = origin + direction * hitDistance;
    return new RaycastHit2D
    {
      collider = collider,
      rigidbody = collider.attachedRigidbody,
      transform = collider.transform,
      point = point,
      normal = -direction,
      distance = hitDistance,
      fraction = 0f
    };
  }
}

public enum RigidbodyType2D
{
  Dynamic,
  Kinematic,
  Static
}

public class Collision2D
{
  public Collider2D collider { get; }
  public Collider2D otherCollider { get; }
  public Rigidbody2D? rigidbody => collider.attachedRigidbody;
  public Rigidbody2D? otherRigidbody => otherCollider.attachedRigidbody;
  public Vector2 relativeVelocity { get; }
  public ContactPoint2D[] contacts { get; }

  public Collision2D(Collider2D collider, Collider2D otherCollider, Vector2 normal)
  {
    this.collider = collider;
    this.otherCollider = otherCollider;
    var vA = collider.attachedRigidbody?.velocity ?? Vector2.zero;
    var vB = otherCollider.attachedRigidbody?.velocity ?? Vector2.zero;
    relativeVelocity = vA - vB;
    contacts = new[] { new ContactPoint2D(normal) };
  }
}

public struct ContactPoint2D
{
  public Vector2 normal;
  public Vector2 point;
  public float separation;

  public ContactPoint2D(Vector2 normal)
  {
    this.normal = normal;
    point = Vector2.zero;
    separation = 0f;
  }
}
