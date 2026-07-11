using System;
using System.Collections.Generic;

namespace UnityEngine;

internal enum ColliderShapeType2D
{
  Box,
  Circle,
  Capsule,
  Polygon,
  Edge
}

internal readonly struct ColliderShape2D
{
  public readonly ColliderShapeType2D type;
  public readonly Vector2 offset;
  public readonly Vector2 size;
  public readonly float radius;
  public readonly Vector2[] points;
  public readonly CapsuleDirection2D capsuleDirection;
  public readonly List<Vector2[]> paths;

  public ColliderShape2D(ColliderShapeType2D type, Vector2 offset, Vector2 size, float radius, Vector2[]? points = null, CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Vertical, List<Vector2[]>? paths = null)
  {
    this.type = type;
    this.offset = offset;
    this.size = size;
    this.radius = radius;
    this.points = points ?? Array.Empty<Vector2>();
    this.capsuleDirection = capsuleDirection;
    this.paths = paths ?? new List<Vector2[]>();
  }
}

internal static class Physics2DWorld
{
  private static readonly List<Collider2D> _colliders = new();
  private static readonly List<Rigidbody2D> _rigidbodies = new();
  private static readonly Dictionary<(Collider2D, Collider2D), CollisionState2D> _collisionStates = new();
  private static readonly Dictionary<(Collider2D, Collider2D), TriggerState2D> _triggerStates = new();

  private class CollisionState2D
  {
    public bool stay;
  }

  private class TriggerState2D
  {
    public bool stay;
  }

  public static bool queriesHitTriggers { get; set; } = true;
  public static bool queriesStartInColliders { get; set; } = true;
  public static int velocityIterations { get; set; } = 8;
  public static int positionIterations { get; set; } = 3;
  public static float defaultContactOffset { get; set; } = 0.01f;
  public static float bounceThreshold { get; set; } = 2f;
  public static float sleepThreshold { get; set; } = 0.005f;

  public static void Register(Collider2D collider)
  {
    if (collider is null || _colliders.Contains(collider)) return;
    _colliders.Add(collider);
  }

  public static void Unregister(Collider2D collider)
  {
    if (collider is null) return;
    _ = _colliders.Remove(collider);
  }

  public static void Register(Rigidbody2D rigidbody)
  {
    if (rigidbody is null || _rigidbodies.Contains(rigidbody)) return;
    _rigidbodies.Add(rigidbody);
  }

  public static void Unregister(Rigidbody2D rigidbody)
  {
    if (rigidbody is null) return;
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

    var staleCollisions = new List<(Collider2D, Collider2D)>();
    foreach (var kv in _collisionStates)
    {
      if (!kv.Value.stay)
      {
        staleCollisions.Add(kv.Key);
        DispatchMessage(kv.Key.Item1.gameObject, "OnCollisionExit2D", new Collision2D(kv.Key.Item1, kv.Key.Item2, Vector2.up));
        DispatchMessage(kv.Key.Item2.gameObject, "OnCollisionExit2D", new Collision2D(kv.Key.Item2, kv.Key.Item1, Vector2.down));
      }
      else
      {
        kv.Value.stay = false;
      }
    }
    foreach (var key in staleCollisions) _collisionStates.Remove(key);

    var staleTriggers = new List<(Collider2D, Collider2D)>();
    foreach (var kv in _triggerStates)
    {
      if (!kv.Value.stay)
      {
        staleTriggers.Add(kv.Key);
        DispatchMessage(kv.Key.Item1.gameObject, "OnTriggerExit2D", kv.Key.Item2);
        DispatchMessage(kv.Key.Item2.gameObject, "OnTriggerExit2D", kv.Key.Item1);
      }
      else
      {
        kv.Value.stay = false;
      }
    }
    foreach (var key in staleTriggers) _triggerStates.Remove(key);
  }

  private static void IntegrateRigidbodies(float deltaTime)
  {
    foreach (var rb in _rigidbodies)
    {
      if (rb is null || rb.IsDestroyed || !rb.simulated) continue;
      if (rb.bodyType == RigidbodyType2D.Static) continue;

      rb.ApplyForces(deltaTime);
      rb.Integrate(deltaTime);
    }
  }

  private static void DetectAndResolveCollisions(float deltaTime)
  {
    _ = deltaTime;
    var count = _colliders.Count;

    foreach (var c in _colliders)
    {
      if (c != null) c.ClearContacts();
    }

    for (var i = 0; i < count; i++)
    {
      var a = _colliders[i];
      if (a is null || a.IsDestroyed || !a.enabled) continue;

      for (var j = i + 1; j < count; j++)
      {
        var b = _colliders[j];
        if (b is null || b.IsDestroyed || !b.enabled) continue;
        if (!CanLayersCollide(a.gameObject?.layer ?? 0, b.gameObject?.layer ?? 0)) continue;

        var key = MakePair(a, b);

        if (a.isTrigger || b.isTrigger)
        {
          if (Intersect(a, b))
          {
            if (!_triggerStates.TryGetValue(key, out var state))
            {
              state = new TriggerState2D();
              _triggerStates[key] = state;
              DispatchMessage(a.gameObject, "OnTriggerEnter2D", b);
              DispatchMessage(b.gameObject, "OnTriggerEnter2D", a);
            }
            else
            {
              DispatchMessage(a.gameObject, "OnTriggerStay2D", b);
              DispatchMessage(b.gameObject, "OnTriggerStay2D", a);
            }
            state.stay = true;
          }
          continue;
        }

        if (Intersect(a, b, out var normal, out var penetration))
        {
          if (!_collisionStates.TryGetValue(key, out var colState))
          {
            colState = new CollisionState2D();
            _collisionStates[key] = colState;
            DispatchCollisionEnter(a, b, normal);
          }
          else
          {
            DispatchCollisionStay(a, b, normal);
          }
          colState.stay = true;

          Vector2 worldPosA = GetWorldPosition(a, a.GetShape().offset);
          Vector2 worldPosB = GetWorldPosition(b, b.GetShape().offset);
          Vector2 point = Vector2.Lerp(worldPosA, worldPosB, 0.5f);
          var cpA = new ContactPoint2D(a, b, normal, point, -penetration);
          var cpB = new ContactPoint2D(b, a, -normal, point, -penetration);
          a.AddContact(cpA);
          b.AddContact(cpB);

          ResolveCollision(a, b, normal, penetration);
        }
      }
    }
  }

  private static (Collider2D, Collider2D) MakePair(Collider2D a, Collider2D b)
  {
    return a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a);
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

    if (shapeA.type == ColliderShapeType2D.Edge || shapeB.type == ColliderShapeType2D.Edge)
    {
      return IntersectEdge(a, shapeA, posA, b, shapeB, posB, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Capsule || shapeB.type == ColliderShapeType2D.Capsule)
    {
      return IntersectCapsuleAny(a, shapeA, posA, b, shapeB, posB, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Polygon || shapeB.type == ColliderShapeType2D.Polygon)
    {
      return IntersectPolygonPolygon(a, shapeA, posA, b, shapeB, posB, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Box && shapeB.type == ColliderShapeType2D.Box)
      return IntersectBoxBox(posA, shapeA.size, posB, shapeB.size, out normal, out penetration);

    if (shapeA.type == ColliderShapeType2D.Circle && shapeB.type == ColliderShapeType2D.Circle)
      return IntersectCircleCircle(posA, shapeA.radius, posB, shapeB.radius, out normal, out penetration);

    if (shapeA.type == ColliderShapeType2D.Box && shapeB.type == ColliderShapeType2D.Circle)
      return IntersectBoxCircle(posA, shapeA.size, posB, shapeB.radius, out normal, out penetration);

    if (shapeA.type == ColliderShapeType2D.Circle && shapeB.type == ColliderShapeType2D.Box)
    {
      var result = IntersectBoxCircle(posB, shapeB.size, posA, shapeA.radius, out var n, out penetration);
      normal = -n;
      return result;
    }

    return false;
  }

  private static bool IntersectCapsuleAny(Collider2D a, ColliderShape2D shapeA, Vector2 posA, Collider2D b, ColliderShape2D shapeB, Vector2 posB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    GetCapsuleTransform(shapeA, posA, out var capCenter, out var capAxis, out var capRadius, out var capHalfHeight);

    if (shapeB.type == ColliderShapeType2D.Circle)
    {
      return IntersectCapsuleCircle(capCenter, capAxis, capRadius, capHalfHeight, posB, shapeB.radius, out normal, out penetration);
    }

    if (shapeB.type == ColliderShapeType2D.Box)
    {
      return IntersectCapsuleBox(capCenter, capAxis, capRadius, capHalfHeight, posB, shapeB.size, out normal, out penetration);
    }

    if (shapeB.type == ColliderShapeType2D.Capsule)
    {
      GetCapsuleTransform(shapeB, posB, out var capCenter2, out var capAxis2, out var capRadius2, out var capHalfHeight2);
      return IntersectCapsuleCapsule(capCenter, capAxis, capRadius, capHalfHeight, capCenter2, capAxis2, capRadius2, capHalfHeight2, out normal, out penetration);
    }

    if (shapeB.type == ColliderShapeType2D.Polygon)
    {
      var pointsB = TransformPoints(shapeB.points, posB);
      return IntersectCapsulePolygon(capCenter, capAxis, capRadius, capHalfHeight, pointsB, out normal, out penetration);
    }

    return false;
  }

  internal static void GetCapsuleTransform(ColliderShape2D shape, Vector2 pos, out Vector2 center, out Vector2 axis, out float radius, out float halfHeight)
  {
    center = pos;
    radius = shape.capsuleDirection == CapsuleDirection2D.Vertical ? shape.size.x * 0.5f : shape.size.y * 0.5f;
    var height = shape.capsuleDirection == CapsuleDirection2D.Vertical ? shape.size.y : shape.size.x;
    halfHeight = Math.Max(0f, height * 0.5f - radius);
    axis = shape.capsuleDirection == CapsuleDirection2D.Vertical ? Vector2.up : Vector2.right;
  }

  private static bool IntersectCapsuleCircle(Vector2 capCenter, Vector2 capAxis, float capRadius, float capHalfHeight, Vector2 circleCenter, float circleRadius, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var p0 = capCenter - capAxis * capHalfHeight;
    var p1 = capCenter + capAxis * capHalfHeight;
    var closest = ClosestPointOnSegment(circleCenter, p0, p1);
    var delta = circleCenter - closest;
    var dist = delta.magnitude;
    var sumR = capRadius + circleRadius;

    if (dist >= sumR || dist < 1e-6f) return false;

    penetration = sumR - dist;
    normal = delta.normalized;
    return true;
  }

  private static bool IntersectCapsuleBox(Vector2 capCenter, Vector2 capAxis, float capRadius, float capHalfHeight, Vector2 boxCenter, Vector2 boxSize, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var p0 = capCenter - capAxis * capHalfHeight;
    var p1 = capCenter + capAxis * capHalfHeight;
    var bestPen = 0f;
    var bestNormal = Vector2.up;

    if (IntersectCircleBox(p0, capRadius, boxCenter, boxSize, out var n0, out var pen0))
    {
      bestPen = pen0;
      bestNormal = n0;
    }
    if (IntersectCircleBox(p1, capRadius, boxCenter, boxSize, out var n1, out var pen1) && pen1 > bestPen)
    {
      bestPen = pen1;
      bestNormal = n1;
    }

    var steps = Math.Max(2, (int)(capHalfHeight * 2f / Math.Max(capRadius * 0.5f, 0.01f)));
    for (var i = 1; i < steps; i++)
    {
      var t = i / (float)steps;
      var p = Vector2.Lerp(p0, p1, t);
      if (IntersectCircleBox(p, capRadius, boxCenter, boxSize, out var n, out var pen) && pen > bestPen)
      {
        bestPen = pen;
        bestNormal = n;
      }
    }

    if (bestPen <= 0f) return false;
    normal = bestNormal;
    penetration = bestPen;
    return true;
  }

  private static bool IntersectCircleBox(Vector2 circleCenter, float radius, Vector2 boxCenter, Vector2 boxSize, out Vector2 normal, out float penetration)
  {
    if (IntersectBoxCircle(boxCenter, boxSize, circleCenter, radius, out var n, out penetration))
    {
      normal = -n;
      return true;
    }
    normal = Vector2.up;
    penetration = 0f;
    return false;
  }

  private static bool IntersectCapsuleCapsule(Vector2 cA, Vector2 axisA, float rA, float hA, Vector2 cB, Vector2 axisB, float rB, float hB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var a0 = cA - axisA * hA;
    var a1 = cA + axisA * hA;
    var b0 = cB - axisB * hB;
    var b1 = cB + axisB * hB;
    var (pa, pb) = ClosestPointOnSegments(a0, a1, b0, b1);
    var delta = pb - pa;
    var dist = delta.magnitude;
    var sumR = rA + rB;

    if (dist >= sumR || dist < 1e-6f) return false;

    penetration = sumR - dist;
    normal = delta.normalized;
    return true;
  }

  private static bool IntersectCapsulePolygon(Vector2 capCenter, Vector2 capAxis, float capRadius, float capHalfHeight, Vector2[] polyPoints, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var p0 = capCenter - capAxis * capHalfHeight;
    var p1 = capCenter + capAxis * capHalfHeight;
    var bestPen = 0f;
    var bestNormal = Vector2.up;

    if (PolygonIntersectCircle(polyPoints, p0, capRadius, out var n0, out var pen0))
    { bestPen = pen0; bestNormal = n0; }
    if (PolygonIntersectCircle(polyPoints, p1, capRadius, out var n1, out var pen1) && pen1 > bestPen)
    { bestPen = pen1; bestNormal = n1; }

    var steps = Math.Max(2, (int)(capHalfHeight * 2f / Math.Max(capRadius * 0.5f, 0.01f)));
    for (var i = 1; i < steps; i++)
    {
      var t = i / (float)steps;
      var p = Vector2.Lerp(p0, p1, t);
      if (PolygonIntersectCircle(polyPoints, p, capRadius, out var n, out var pen) && pen > bestPen)
      { bestPen = pen; bestNormal = n; }
    }

    if (bestPen <= 0f) return false;
    normal = bestNormal;
    penetration = bestPen;
    return true;
  }

  private static bool IntersectEdge(Collider2D a, ColliderShape2D shapeA, Vector2 posA, Collider2D b, ColliderShape2D shapeB, Vector2 posB, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var edgeShape = shapeA.type == ColliderShapeType2D.Edge ? shapeA : shapeB;
    var edgePos = shapeA.type == ColliderShapeType2D.Edge ? posA : posB;
    var otherShape = shapeA.type == ColliderShapeType2D.Edge ? shapeB : shapeA;
    var otherPos = shapeA.type == ColliderShapeType2D.Edge ? posB : posA;
    var edgeIsA = shapeA.type == ColliderShapeType2D.Edge;

    var edgePoints = TransformPoints(edgeShape.points, edgePos);
    if (edgePoints.Length < 2) return false;

    var bestPen = 0f;
    var bestNormal = Vector2.up;

    for (var i = 0; i < edgePoints.Length - 1; i++)
    {
      var segA = edgePoints[i];
      var segB = edgePoints[i + 1];

      if (otherShape.type == ColliderShapeType2D.Circle)
      {
        if (SegmentIntersectCircle(segA, segB, otherPos, otherShape.radius, out var n, out var pen) && pen > bestPen)
        { bestPen = pen; bestNormal = edgeIsA ? n : -n; }
      }
      else if (otherShape.type == ColliderShapeType2D.Box)
      {
        if (SegmentIntersectBox(segA, segB, otherPos, otherShape.size, out var n, out var pen) && pen > bestPen)
        { bestPen = pen; bestNormal = edgeIsA ? n : -n; }
      }
      else if (otherShape.type == ColliderShapeType2D.Capsule)
      {
        GetCapsuleTransform(otherShape, otherPos, out var cC, out var cA, out var cR, out var cH);
        if (SegmentIntersectCapsule(segA, segB, cC, cA, cR, cH, out var n, out var pen) && pen > bestPen)
        { bestPen = pen; bestNormal = edgeIsA ? n : -n; }
      }
      else if (otherShape.type == ColliderShapeType2D.Polygon)
      {
        var otherPoints = TransformPoints(otherShape.points, otherPos);
        for (var j = 0; j < otherPoints.Length; j++)
        {
          var oA = otherPoints[j];
          var oB = otherPoints[(j + 1) % otherPoints.Length];
          if (SegmentsIntersect(segA, segB, oA, oB, out var n, out var pen) && pen > bestPen)
          { bestPen = pen; bestNormal = edgeIsA ? n : -n; }
        }
      }
    }

    if (bestPen <= 0f) return false;
    normal = bestNormal;
    penetration = bestPen;
    return true;
  }

  private static bool SegmentIntersectCapsule(Vector2 a, Vector2 b, Vector2 capCenter, Vector2 capAxis, float capRadius, float capHalfHeight, out Vector2 normal, out float penetration)
  {
    normal = Vector2.up;
    penetration = 0f;

    var p0 = capCenter - capAxis * capHalfHeight;
    var p1 = capCenter + capAxis * capHalfHeight;

    if (DistanceFromPointToSegment(p0, a, b) <= capRadius)
    {
      var closest = ClosestPointOnSegment(p0, a, b);
      var delta = p0 - closest;
      var dist = delta.magnitude;
      if (dist < capRadius && dist > 1e-6f)
      {
        penetration = capRadius - dist;
        normal = delta.normalized;
        return true;
      }
    }
    if (DistanceFromPointToSegment(p1, a, b) <= capRadius)
    {
      var closest = ClosestPointOnSegment(p1, a, b);
      var delta = p1 - closest;
      var dist = delta.magnitude;
      if (dist < capRadius && dist > 1e-6f)
      {
        penetration = capRadius - dist;
        normal = delta.normalized;
        return true;
      }
    }

    var steps = Math.Max(2, (int)(capHalfHeight * 2f / Math.Max(capRadius * 0.5f, 0.01f)));
    for (var i = 1; i < steps; i++)
    {
      var t = i / (float)steps;
      var p = Vector2.Lerp(p0, p1, t);
      if (DistanceFromPointToSegment(p, a, b) <= capRadius)
      {
        var closest = ClosestPointOnSegment(p, a, b);
        var delta = p - closest;
        var dist = delta.magnitude;
        if (dist < capRadius && dist > 1e-6f)
        {
          penetration = capRadius - dist;
          normal = delta.normalized;
          return true;
        }
      }
    }

    return false;
  }

  private static float DistanceFromPointToSegment(Vector2 point, Vector2 a, Vector2 b)
  {
    var closest = ClosestPointOnSegment(point, a, b);
    return (point - closest).magnitude;
  }

  private static Vector2 GetWorldPosition(Collider2D collider, Vector2 offset)
  {
    var transform = collider.transform;
    if (transform is null) return offset;
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
    if (px <= 0f) return false;

    var dy = b.y - a.y;
    var py = halfA.y + halfB.y - MathF.Abs(dy);
    if (py <= 0f) return false;

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
    if (distance >= sumRadius || distance < 1e-6f) return false;
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
    if (distance >= radius) return false;
    if (distance < 1e-6f)
    {
      normal = (circleCenter - boxCenter).normalized;
      if (normal.magnitude < 1e-6f) normal = Vector2.up;
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
    var pointsA = GetPolygonPoints(shapeA, posA);
    var pointsB = GetPolygonPoints(shapeB, posB);
    if (pointsA.Length == 0 || pointsB.Length == 0) return false;

    if (PolygonIntersectPolygon(pointsA, pointsB, out normal, out penetration))
      return true;
    if (shapeB.type == ColliderShapeType2D.Box)
      return PolygonIntersectBox(pointsA, posB, shapeB.size, out normal, out penetration);
    if (shapeB.type == ColliderShapeType2D.Circle)
      return PolygonIntersectCircle(pointsA, posB, shapeB.radius, out normal, out penetration);
    return false;
  }

  private static Vector2[] GetPolygonPoints(ColliderShape2D shape, Vector2 pos)
  {
    if (shape.type == ColliderShapeType2D.Box)
    {
      var half = shape.size * 0.5f;
      return new[]
      {
        pos + new Vector2(-half.x, -half.y),
        pos + new Vector2(half.x, -half.y),
        pos + new Vector2(half.x, half.y),
        pos + new Vector2(-half.x, half.y)
      };
    }
    return TransformPoints(shape.points, pos);
  }

  internal static Vector2[] TransformPoints(Vector2[] points, Vector2 offset)
  {
    if (points.Length == 0) return points;
    var result = new Vector2[points.Length];
    for (var i = 0; i < points.Length; i++)
      result[i] = points[i] + offset;
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

    if ((a.x < min.x && b.x < min.x) || (a.x > max.x && b.x > max.x) ||
        (a.y < min.y && b.y < min.y) || (a.y > max.y && b.y > max.y))
      return false;

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

  internal static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
  {
    var ab = b - a;
    var denom = Vector2.Dot(ab, ab);
    if (denom < 1e-8f) return a;
    var t = Vector2.Dot(point - a, ab) / denom;
    t = Math.Clamp(t, 0f, 1f);
    return a + ab * t;
  }

  private static (Vector2, Vector2) ClosestPointOnSegments(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
  {
    var u = a1 - a0;
    var v = b1 - b0;
    var w = a0 - b0;
    var a = Vector2.Dot(u, u);
    var b = Vector2.Dot(u, v);
    var c = Vector2.Dot(v, v);
    var d = Vector2.Dot(u, w);
    var e = Vector2.Dot(v, w);
    var denom = a * c - b * b;
    float s, t;

    if (denom < 1e-8f)
    {
      s = 0f;
      t = b > c ? d / b : e / c;
      t = Math.Clamp(t, 0f, 1f);
    }
    else
    {
      s = Math.Clamp((b * e - c * d) / denom, 0f, 1f);
      t = (b * s + e) / c;
      if (t < 0f) { t = 0f; s = Math.Clamp(-d / a, 0f, 1f); }
      else if (t > 1f) { t = 1f; s = Math.Clamp((b - d) / a, 0f, 1f); }
    }

    return (a0 + u * s, b0 + v * t);
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
      if (!TestAxis(axis, polyA, polyB, out var pen)) return false;
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
    var boxCorners = new[]
    {
      center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y),
      center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y)
    };
    foreach (var axis in axes)
    {
      if (!TestAxis(axis, poly, boxCorners, out var pen)) return false;
      if (pen < penetration) { penetration = pen; normal = axis; }
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
      var n = new Vector2(-edge.y, edge.x).normalized;
      if (n.magnitude > 1e-6f) axes.Add(n);
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

  public static bool PointInPolygon(Vector2 point, Vector2[] poly)
  {
    var inside = false;
    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
    {
      var pi = poly[i];
      var pj = poly[j];
      if (((pi.y > point.y) != (pj.y > point.y)) &&
          (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + 1e-8f) + pi.x))
        inside = !inside;
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
      return;

    var transformA = a.transform;
    var transformB = b.transform;
    if (transformA is null && transformB is null) return;

    var invMassA = rbA is not null && rbA.bodyType == RigidbodyType2D.Dynamic ? 1f / rbA.mass : 0f;
    var invMassB = rbB is not null && rbB.bodyType == RigidbodyType2D.Dynamic ? 1f / rbB.mass : 0f;
    var totalInvMass = invMassA + invMassB;
    if (totalInvMass <= 1e-6f) return;

    var matA = a.sharedMaterial;
    var matB = b.sharedMaterial;
    var bounciness = CombineBounciness(matA?.bounciness ?? 0f, matB?.bounciness ?? 0f, matA?.bouncinessCombine ?? PhysicsMaterialCombine2D.Average, matB?.bouncinessCombine ?? PhysicsMaterialCombine2D.Average);
    var friction = CombineFriction(matA?.friction ?? 0.4f, matB?.friction ?? 0.4f, matA?.frictionCombine ?? PhysicsMaterialCombine2D.Average, matB?.frictionCombine ?? PhysicsMaterialCombine2D.Average);

    var percent = 0.8f;
    var slop = 0.01f;
    var correction = normal * (MathF.Max(penetration - slop, 0f) / totalInvMass * percent);

    if (transformA is not null && invMassA > 0f)
    {
      var pos = transformA.localPosition;
      pos.x -= correction.x * invMassA;
      pos.y -= correction.y * invMassA;
      transformA.localPosition = pos;
    }
    if (transformB is not null && invMassB > 0f)
    {
      var pos = transformB.localPosition;
      pos.x += correction.x * invMassB;
      pos.y += correction.y * invMassB;
      transformB.localPosition = pos;
    }

    var velA = rbA?.velocity ?? Vector2.zero;
    var velB = rbB?.velocity ?? Vector2.zero;
    var relVel = velA - velB;
    var velAlongNormal = Vector2.Dot(relVel, normal);

    if (velAlongNormal > 0f) return;

    var bounceThreshold = 2f;
    var bounceFactor = MathF.Abs(velAlongNormal) > bounceThreshold ? bounciness : 0f;
    var impulseScalar = -(1f + bounceFactor) * velAlongNormal / totalInvMass;
    var impulse = normal * impulseScalar;

    if (rbA is not null && invMassA > 0f)
    {
      rbA.velocity += impulse * invMassA;
      rbA.WakeUp();
    }
    if (rbB is not null && invMassB > 0f)
    {
      rbB.velocity -= impulse * invMassB;
      rbB.WakeUp();
    }

    var tangent = new Vector2(-normal.y, normal.x).normalized;
    var velAlongTangent = Vector2.Dot(relVel, tangent);
    var frictionImpulseScalar = -velAlongTangent / totalInvMass;
    frictionImpulseScalar = Math.Clamp(frictionImpulseScalar, -MathF.Abs(impulseScalar) * friction, MathF.Abs(impulseScalar) * friction);
    var frictionImpulse = tangent * frictionImpulseScalar;

    if (rbA is not null && invMassA > 0f)
      rbA.velocity += frictionImpulse * invMassA;
    if (rbB is not null && invMassB > 0f)
      rbB.velocity -= frictionImpulse * invMassB;
  }

  private static float CombineBounciness(float a, float b, PhysicsMaterialCombine2D ca, PhysicsMaterialCombine2D cb)
  {
    var mode = ca > cb ? ca : cb;
    return mode switch
    {
      PhysicsMaterialCombine2D.Average => (a + b) * 0.5f,
      PhysicsMaterialCombine2D.Minimum => MathF.Min(a, b),
      PhysicsMaterialCombine2D.Maximum => MathF.Max(a, b),
      PhysicsMaterialCombine2D.Multiply => a * b,
      _ => (a + b) * 0.5f
    };
  }

  private static float CombineFriction(float a, float b, PhysicsMaterialCombine2D ca, PhysicsMaterialCombine2D cb)
  {
    var mode = ca > cb ? ca : cb;
    return mode switch
    {
      PhysicsMaterialCombine2D.Average => (a + b) * 0.5f,
      PhysicsMaterialCombine2D.Minimum => MathF.Min(a, b),
      PhysicsMaterialCombine2D.Maximum => MathF.Max(a, b),
      PhysicsMaterialCombine2D.Multiply => a * b,
      _ => (a + b) * 0.5f
    };
  }

  private static void DispatchCollisionEnter(Collider2D a, Collider2D b, Vector2 normal)
  {
    var contact = new Collision2D(a, b, normal);
    DispatchMessage(a.gameObject, "OnCollisionEnter2D", contact);
    DispatchMessage(b.gameObject, "OnCollisionEnter2D", contact);
  }

  private static void DispatchCollisionStay(Collider2D a, Collider2D b, Vector2 normal)
  {
    var contact = new Collision2D(a, b, normal);
    DispatchMessage(a.gameObject, "OnCollisionStay2D", contact);
    DispatchMessage(b.gameObject, "OnCollisionStay2D", contact);
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
        try { method.Invoke(behaviour, new[] { arg }); }
        catch { }
      }
    }
  }

  public static bool CanLayersCollide(int layerA, int layerB)
  {
    return Physics2D.IsLayerCollisionEnabled(layerA, layerB);
  }

  public static bool LayerMatches(int layer, int layerMask)
  {
    if (layerMask == -1) return true;
    return (layerMask & (1 << layer)) != 0;
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask)
  {
    hitInfo = new RaycastHit2D();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f) return false;

    var closest = distance;
    Collider2D? closestCol = null;
    Vector2 closestNormal = -direction;

    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;

      if (RaycastCollider(origin, direction, col, out var t, out var n) && t < closest && t >= -1e-4f)
      {
        closest = t;
        closestCol = col;
        closestNormal = n;
      }
    }

    if (closestCol is null) return false;

    var point = origin + direction * MathF.Max(0f, closest);
    hitInfo = new RaycastHit2D
    {
      collider = closestCol,
      rigidbody = closestCol.attachedRigidbody,
      transform = closestCol.transform,
      point = point,
      normal = closestNormal,
      distance = MathF.Max(0f, closest),
      fraction = MathF.Max(0f, closest) / distance
    };
    return true;
  }

  public static List<RaycastHit2D> RaycastAll(Vector2 origin, Vector2 direction, float distance, int layerMask)
  {
    var hits = new List<RaycastHit2D>();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f) return hits;

    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;

      if (RaycastCollider(origin, direction, col, out var t, out var n) && t <= distance && t >= -1e-4f)
      {
        var pt = origin + direction * MathF.Max(0f, t);
        hits.Add(new RaycastHit2D
        {
          collider = col,
          rigidbody = col.attachedRigidbody,
          transform = col.transform,
          point = pt,
          normal = n,
          distance = MathF.Max(0f, t),
          fraction = MathF.Max(0f, t) / distance
        });
      }
    }
    hits.Sort((a, b) => a.distance.CompareTo(b.distance));
    return hits;
  }

  private static bool RaycastCollider(Vector2 origin, Vector2 direction, Collider2D col, out float distance, out Vector2 normal)
  {
    distance = float.PositiveInfinity;
    normal = -direction;
    var shape = col.GetShape();
    var center = GetWorldPosition(col, shape.offset);

    if (!queriesStartInColliders)
    {
      if (shape.type == ColliderShapeType2D.Circle)
      {
        if ((origin - center).magnitude < shape.radius) return false;
      }
      else if (shape.type == ColliderShapeType2D.Box)
      {
        var h = shape.size * 0.5f;
        if (origin.x >= center.x - h.x && origin.x <= center.x + h.x &&
            origin.y >= center.y - h.y && origin.y <= center.y + h.y) return false;
      }
    }

    if (shape.type == ColliderShapeType2D.Box)
      return RaycastBox(origin, direction, center, shape.size, out distance, out normal);
    if (shape.type == ColliderShapeType2D.Circle)
      return RaycastCircle(origin, direction, center, shape.radius, out distance, out normal);
    if (shape.type == ColliderShapeType2D.Capsule)
    {
      GetCapsuleTransform(shape, center, out var cC, out var cA, out var cR, out var cH);
      return RaycastCapsule(origin, direction, cC, cA, cR, cH, out distance, out normal);
    }
    if (shape.type == ColliderShapeType2D.Polygon || shape.type == ColliderShapeType2D.Edge)
      return RaycastPolygon(origin, direction, TransformPoints(shape.points, center), out distance, out normal);
    return false;
  }

  private static bool RaycastCapsule(Vector2 origin, Vector2 direction, Vector2 capCenter, Vector2 capAxis, float capRadius, float capHalfHeight, out float distance, out Vector2 normal)
  {
    distance = float.PositiveInfinity;
    normal = -direction;

    var p0 = capCenter - capAxis * capHalfHeight;
    var p1 = capCenter + capAxis * capHalfHeight;
    var hit = false;

    if (RaycastCircle(origin, direction, p0, capRadius, out var t0, out var n0) && t0 < distance)
    { distance = t0; normal = n0; hit = true; }
    if (RaycastCircle(origin, direction, p1, capRadius, out var t1, out var n1) && t1 < distance)
    { distance = t1; normal = n1; hit = true; }

    var steps = Math.Max(4, (int)(capHalfHeight * 2f / Math.Max(capRadius * 0.25f, 0.01f)));
    for (var i = 1; i < steps; i++)
    {
      var t = i / (float)steps;
      var p = Vector2.Lerp(p0, p1, t);
      if (RaycastCircle(origin, direction, p, capRadius, out var tt, out var nn) && tt < distance)
      { distance = tt; normal = nn; hit = true; }
    }
    return hit;
  }

  private static bool RaycastPolygon(Vector2 origin, Vector2 direction, Vector2[] points, out float distance, out Vector2 normal)
  {
    distance = float.PositiveInfinity;
    normal = -direction;
    if (points.Length < 2) return false;
    var hit = false;
    var len = points[0] == points[^1] ? points.Length - 1 : points.Length;
    for (var i = 0; i < len; i++)
    {
      var a = points[i];
      var b = points[(i + 1) % points.Length];
      if (RaycastSegment(origin, direction, a, b, out var t, out var n) && t < distance)
      { distance = t; normal = n; hit = true; }
    }
    return hit;
  }

  private static bool RaycastSegment(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b, out float distance, out Vector2 normal)
  {
    distance = float.PositiveInfinity;
    normal = -direction;
    var seg = b - a;
    var denom = direction.x * seg.y - direction.y * seg.x;
    if (MathF.Abs(denom) < 1e-6f) return false;
    var diff = a - origin;
    var t = (diff.x * seg.y - diff.y * seg.x) / denom;
    var u = (diff.x * direction.y - diff.y * direction.x) / denom;
    if (t < 0f || u < 0f || u > 1f) return false;
    distance = t;
    var segDir = seg.normalized;
    normal = new Vector2(-segDir.y, segDir.x).normalized;
    if (Vector2.Dot(normal, direction) > 0f) normal = -normal;
    return true;
  }

  private static bool RaycastBox(Vector2 origin, Vector2 direction, Vector2 center, Vector2 size, out float distance, out Vector2 normal)
  {
    distance = float.PositiveInfinity;
    normal = -direction;
    var half = size * 0.5f;
    var min = center - half;
    var max = center + half;

    float tMin, tMax, nMin = 0, nMax = 0;

    if (MathF.Abs(direction.x) < 1e-6f)
    {
      if (origin.x < min.x || origin.x > max.x) return false;
      tMin = float.NegativeInfinity;
      tMax = float.PositiveInfinity;
    }
    else
    {
      var invD = 1f / direction.x;
      var t1 = (min.x - origin.x) * invD;
      var t2 = (max.x - origin.x) * invD;
      var s = 1f;
      if (t1 > t2) { (t1, t2) = (t2, t1); s = -1f; }
      tMin = t1; nMin = s > 0 ? -1 : 1;
      tMax = t2; nMax = s > 0 ? 1 : -1;
    }

    if (MathF.Abs(direction.y) < 1e-6f)
    {
      if (origin.y < min.y || origin.y > max.y) return false;
    }
    else
    {
      var invD = 1f / direction.y;
      var t1 = (min.y - origin.y) * invD;
      var t2 = (max.y - origin.y) * invD;
      var s = 1f;
      if (t1 > t2) { (t1, t2) = (t2, t1); s = -1f; }
      if (t1 > tMin) { tMin = t1; nMin = 0; nMax = 0; if (s > 0) nMin = -1; else nMin = 1; }
      tMax = MathF.Min(tMax, t2);
    }

    if (tMin > tMax) return false;
    distance = tMin >= 0f ? tMin : tMax;

    if (tMin >= 0f)
      normal = nMin != 0 ? new Vector2(nMin, 0) : new Vector2(0, nMax);
    else
      normal = nMin != 0 ? new Vector2(-nMin, 0) : new Vector2(0, -nMax);

    return true;
  }

  private static bool RaycastCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out float distance, out Vector2 normal)
  {
    distance = float.PositiveInfinity;
    normal = -direction;
    var oc = origin - center;
    var a = Vector2.Dot(direction, direction);
    var b = 2f * Vector2.Dot(oc, direction);
    var c = Vector2.Dot(oc, oc) - radius * radius;
    var disc = b * b - 4f * a * c;
    if (disc < 0f) return false;
    var sqrt = MathF.Sqrt(disc);
    var t = (-b - sqrt) / (2f * a);
    if (t < 0f) t = (-b + sqrt) / (2f * a);
    if (t < 0f) return false;
    distance = t;
    normal = (origin + direction * t - center).normalized;
    return true;
  }

  public static bool OverlapCircle(Vector2 point, float radius, int layerMask, out Collider2D[] results)
  {
    var list = new List<Collider2D>();
    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;
      var shape = col.GetShape();
      var center = GetWorldPosition(col, shape.offset);
      if (TryOverlapShapeCircle(shape, center, point, radius)) list.Add(col);
    }
    results = list.ToArray();
    return results.Length > 0;
  }

  private static bool TryOverlapShapeCircle(ColliderShape2D shape, Vector2 center, Vector2 point, float radius)
  {
    if (shape.type == ColliderShapeType2D.Circle)
      return (center - point).magnitude <= radius + shape.radius;
    if (shape.type == ColliderShapeType2D.Box)
      return IntersectBoxCircle(center, shape.size, point, radius, out _, out _);
    if (shape.type == ColliderShapeType2D.Capsule)
    {
      GetCapsuleTransform(shape, center, out var cC, out var cA, out var cR, out var cH);
      return IntersectCapsuleCircle(cC, cA, cR, cH, point, radius, out _, out _);
    }
    if (shape.type == ColliderShapeType2D.Polygon || shape.type == ColliderShapeType2D.Edge)
      return PolygonIntersectCircle(TransformPoints(shape.points, center), point, radius, out _, out _);
    return false;
  }

  public static bool OverlapBox(Vector2 point, Vector2 size, float angle, int layerMask, out Collider2D[] results)
  {
    _ = angle;
    var list = new List<Collider2D>();
    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;
      var shape = col.GetShape();
      var center = GetWorldPosition(col, shape.offset);
      if (TryOverlapShapeBox(shape, center, point, size)) list.Add(col);
    }
    results = list.ToArray();
    return results.Length > 0;
  }

  private static bool TryOverlapShapeBox(ColliderShape2D shape, Vector2 center, Vector2 point, Vector2 size)
  {
    if (shape.type == ColliderShapeType2D.Box)
      return IntersectBoxBox(point, size, center, shape.size, out _, out _);
    if (shape.type == ColliderShapeType2D.Circle)
      return IntersectBoxCircle(point, size, center, shape.radius, out _, out _);
    if (shape.type == ColliderShapeType2D.Capsule)
    {
      GetCapsuleTransform(shape, center, out var cC, out var cA, out var cR, out var cH);
      return IntersectCapsuleBox(cC, cA, cR, cH, point, size, out _, out _);
    }
    if (shape.type == ColliderShapeType2D.Polygon)
      return PolygonIntersectBox(TransformPoints(shape.points, center), point, size, out _, out _);
    return false;
  }

  public static bool OverlapCapsule(Vector2 point, Vector2 size, CapsuleDirection2D dir, int layerMask, out Collider2D[] results)
  {
    var list = new List<Collider2D>();
    var shape = new ColliderShape2D(ColliderShapeType2D.Capsule, Vector2.zero, size, 0f, null, dir);
    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;
      var colShape = col.GetShape();
      var colCenter = GetWorldPosition(col, colShape.offset);
      if (IntersectCapsuleAny(null, shape, point, col, colShape, colCenter, out _, out _)) list.Add(col);
    }
    results = list.ToArray();
    return results.Length > 0;
  }

  public static bool OverlapPoint(Vector2 point, int layerMask, out Collider2D[] results)
  {
    var list = new List<Collider2D>();
    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;
      var shape = col.GetShape();
      var center = GetWorldPosition(col, shape.offset);
      if (shape.type == ColliderShapeType2D.Box)
      {
        var h = shape.size * 0.5f;
        if (point.x >= center.x - h.x && point.x <= center.x + h.x &&
            point.y >= center.y - h.y && point.y <= center.y + h.y) list.Add(col);
      }
      else if (shape.type == ColliderShapeType2D.Circle)
      {
        if ((center - point).magnitude <= shape.radius) list.Add(col);
      }
      else if (shape.type == ColliderShapeType2D.Capsule)
      {
        GetCapsuleTransform(shape, center, out var cC, out var cA, out var cR, out var cH);
        if (IntersectCapsuleCircle(cC, cA, cR, cH, point, 0f, out _, out _)) list.Add(col);
      }
      else if (shape.type == ColliderShapeType2D.Polygon)
      {
        if (PointInPolygon(point, TransformPoints(shape.points, center))) list.Add(col);
      }
    }
    results = list.ToArray();
    return results.Length > 0;
  }

  public static bool OverlapArea(Vector2 pointA, Vector2 pointB, int layerMask, out Collider2D[] results)
  {
    var min = new Vector2(MathF.Min(pointA.x, pointB.x), MathF.Min(pointA.y, pointB.y));
    var max = new Vector2(MathF.Max(pointA.x, pointB.x), MathF.Max(pointA.y, pointB.y));
    var size = max - min;
    var center = (min + max) * 0.5f;
    return OverlapBox(center, size, 0f, layerMask, out results);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f || distance <= 0f) return false;
    _ = angle;

    var closest = distance;
    Collider2D? closestCol = null;
    Vector2 closestNormal = -direction;
    Vector2 closestPoint = origin;

    var half = size * 0.5f;
    var corners = new[]
    {
      origin + new Vector2(-half.x, -half.y),
      origin + new Vector2(half.x, -half.y),
      origin + new Vector2(half.x, half.y),
      origin + new Vector2(-half.x, half.y)
    };

    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;

      if (TryOverlapShapeBox(col.GetShape(), GetWorldPosition(col, col.GetShape().offset), origin, size))
      {
        if (!queriesStartInColliders) continue;
      }

      foreach (var corner in corners)
      {
        if (RaycastCollider(corner, direction, col, out var t, out var n) && t < closest && t >= -1e-4f)
        {
          closest = t;
          closestCol = col;
          closestNormal = n;
          closestPoint = corner + direction * MathF.Max(0f, t);
        }
      }
    }

    if (closestCol is null) return false;

    hitInfo = new RaycastHit2D
    {
      collider = closestCol,
      rigidbody = closestCol.attachedRigidbody,
      transform = closestCol.transform,
      point = closestPoint,
      normal = closestNormal,
      distance = MathF.Max(0f, closest),
      fraction = MathF.Max(0f, closest) / distance
    };
    return true;
  }

  public static List<RaycastHit2D> BoxCastAll(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask)
  {
    var hits = new List<RaycastHit2D>();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f || distance <= 0f) return hits;
    _ = angle;

    var half = size * 0.5f;
    var corners = new[]
    {
      origin + new Vector2(-half.x, -half.y), origin + new Vector2(half.x, -half.y),
      origin + new Vector2(half.x, half.y), origin + new Vector2(-half.x, half.y)
    };

    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;

      var bestT = float.PositiveInfinity;
      var bestN = -direction;
      var bestP = origin;

      foreach (var corner in corners)
      {
        if (RaycastCollider(corner, direction, col, out var t, out var n) && t <= distance && t < bestT && t >= -1e-4f)
        {
          bestT = t; bestN = n; bestP = corner + direction * MathF.Max(0f, t);
        }
      }

      if (bestT <= distance)
      {
        hits.Add(new RaycastHit2D
        {
          collider = col, rigidbody = col.attachedRigidbody, transform = col.transform,
          point = bestP, normal = bestN, distance = MathF.Max(0f, bestT), fraction = MathF.Max(0f, bestT) / distance
        });
      }
    }
    hits.Sort((a, b) => a.distance.CompareTo(b.distance));
    return hits;
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f || distance <= 0f) return false;

    var closest = distance;
    Collider2D? closestCol = null;
    Vector2 closestNormal = -direction;

    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;

      var shape = col.GetShape();
      var center = GetWorldPosition(col, shape.offset);

      float t;
      Vector2 n;
      bool hit;

      if (shape.type == ColliderShapeType2D.Circle)
      {
        hit = RaycastCircle(origin, direction, center, shape.radius + radius, out t, out n);
      }
      else if (shape.type == ColliderShapeType2D.Box)
      {
        hit = RaycastBoxExpanded(origin, direction, center, shape.size, radius, out t, out n);
      }
      else if (shape.type == ColliderShapeType2D.Capsule)
      {
        GetCapsuleTransform(shape, center, out var cC, out var cA, out var cR, out var cH);
        hit = RaycastCapsule(origin, direction, cC, cA, cR + radius, cH, out t, out n);
      }
      else
      {
        hit = false; t = float.PositiveInfinity; n = -direction;
      }

      if (hit && t < closest && t >= -1e-4f)
      {
        closest = t; closestCol = col; closestNormal = n;
      }
    }

    if (closestCol is null) return false;

    var pt = origin + direction * MathF.Max(0f, closest);
    hitInfo = new RaycastHit2D
    {
      collider = closestCol, rigidbody = closestCol.attachedRigidbody, transform = closestCol.transform,
      point = pt, normal = closestNormal, distance = MathF.Max(0f, closest), fraction = MathF.Max(0f, closest) / distance
    };
    return true;
  }

  private static bool RaycastBoxExpanded(Vector2 origin, Vector2 direction, Vector2 boxCenter, Vector2 boxSize, float expand, out float distance, out Vector2 normal)
  {
    var expandedSize = boxSize + new Vector2(expand * 2f, expand * 2f);
    return RaycastBox(origin, direction, boxCenter, expandedSize, out distance, out normal);
  }

  public static List<RaycastHit2D> CircleCastAll(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask)
  {
    var hits = new List<RaycastHit2D>();
    direction = direction.normalized;
    if (direction.magnitude < 1e-6f || distance <= 0f) return hits;

    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
      if (col.isTrigger && !queriesHitTriggers) continue;

      var shape = col.GetShape();
      var center = GetWorldPosition(col, shape.offset);
      bool hit; float t; Vector2 n;

      if (shape.type == ColliderShapeType2D.Circle)
        hit = RaycastCircle(origin, direction, center, shape.radius + radius, out t, out n);
      else if (shape.type == ColliderShapeType2D.Box)
        hit = RaycastBoxExpanded(origin, direction, center, shape.size, radius, out t, out n);
      else if (shape.type == ColliderShapeType2D.Capsule)
      {
        GetCapsuleTransform(shape, center, out var cC, out var cA, out var cR, out var cH);
        hit = RaycastCapsule(origin, direction, cC, cA, cR + radius, cH, out t, out n);
      }
      else
      { hit = false; t = float.PositiveInfinity; n = -direction; }

      if (hit && t <= distance && t >= -1e-4f)
      {
        var pt = origin + direction * MathF.Max(0f, t);
        hits.Add(new RaycastHit2D
        {
          collider = col, rigidbody = col.attachedRigidbody, transform = col.transform,
          point = pt, normal = n, distance = MathF.Max(0f, t), fraction = MathF.Max(0f, t) / distance
        });
      }
    }
    hits.Sort((a, b) => a.distance.CompareTo(b.distance));
    return hits;
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 castDirection, float distance, int layerMask, out RaycastHit2D hitInfo)
  {
    _ = angle;
    hitInfo = new RaycastHit2D();
    castDirection = castDirection.normalized;
    if (castDirection.magnitude < 1e-6f || distance <= 0f) return false;

    var radius = directionType == CapsuleDirection2D.Vertical ? size.x * 0.5f : size.y * 0.5f;
    var height = directionType == CapsuleDirection2D.Vertical ? size.y : size.x;
    var halfH = Math.Max(0f, height * 0.5f - radius);
    var axis = directionType == CapsuleDirection2D.Vertical ? Vector2.up : Vector2.right;
    var p0 = origin - axis * halfH;
    var p1 = origin + axis * halfH;

    var closest = distance;
    Collider2D? closestCol = null;
    Vector2 closestNormal = -castDirection;

    bool TestPoint(Vector2 p)
    {
      foreach (var col in _colliders)
      {
        if (col is null || col.IsDestroyed || !col.enabled) continue;
        if (!LayerMatches(col.gameObject?.layer ?? 0, layerMask)) continue;
        if (col.isTrigger && !queriesHitTriggers) continue;
        var shape = col.GetShape();
        var c = GetWorldPosition(col, shape.offset);
        bool hit; float t; Vector2 n;
        if (shape.type == ColliderShapeType2D.Circle)
          hit = RaycastCircle(p, castDirection, c, shape.radius + radius, out t, out n);
        else if (shape.type == ColliderShapeType2D.Box)
          hit = RaycastBoxExpanded(p, castDirection, c, shape.size, radius, out t, out n);
        else if (shape.type == ColliderShapeType2D.Capsule)
        {
          GetCapsuleTransform(shape, c, out var cc, out var ca, out var cr, out var ch);
          hit = RaycastCapsule(p, castDirection, cc, ca, cr + radius, ch, out t, out n);
        }
        else { hit = false; t = float.PositiveInfinity; n = -castDirection; }
        if (hit && t < closest && t >= -1e-4f)
        { closest = t; closestCol = col; closestNormal = n; return true; }
      }
      return false;
    }

    TestPoint(p0);
    TestPoint(p1);
    var steps = Math.Max(4, (int)(halfH * 2f / Math.Max(radius * 0.25f, 0.01f)));
    for (var i = 1; i < steps; i++)
    {
      TestPoint(Vector2.Lerp(p0, p1, i / (float)steps));
    }

    if (closestCol is null) return false;

    var pt = origin + castDirection * MathF.Max(0f, closest);
    hitInfo = new RaycastHit2D
    {
      collider = closestCol, rigidbody = closestCol.attachedRigidbody, transform = closestCol.transform,
      point = pt, normal = closestNormal, distance = MathF.Max(0f, closest), fraction = MathF.Max(0f, closest) / distance
    };
    return true;
  }

  public static int GetContacts(Collider2D collider, Collider2D[] results)
  {
    if (results is null || results.Length == 0) return 0;
    var count = 0;
    foreach (var col in _colliders)
    {
      if (col is null || col == collider || col.IsDestroyed || !col.enabled) continue;
      if (!CanLayersCollide(collider.gameObject?.layer ?? 0, col.gameObject?.layer ?? 0)) continue;
      if (Intersect(collider, col))
      {
        if (count < results.Length) results[count++] = col;
      }
    }
    return count;
  }

  public static int GetContacts(Vector2 point, Collider2D[] results)
  {
    if (results is null || results.Length == 0) return 0;
    var count = 0;
    foreach (var col in _colliders)
    {
      if (col is null || col.IsDestroyed || !col.enabled) continue;
      var shape = col.GetShape();
      var c = GetWorldPosition(col, shape.offset);
      bool overlap = false;
      if (shape.type == ColliderShapeType2D.Circle)
        overlap = (c - point).magnitude <= shape.radius;
      else if (shape.type == ColliderShapeType2D.Box)
      {
        var h = shape.size * 0.5f;
        overlap = point.x >= c.x - h.x && point.x <= c.x + h.x && point.y >= c.y - h.y && point.y <= c.y + h.y;
      }
      if (overlap && count < results.Length) results[count++] = col;
    }
    return count;
  }
}
