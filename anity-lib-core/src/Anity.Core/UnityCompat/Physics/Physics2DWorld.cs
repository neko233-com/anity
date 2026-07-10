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

  public ColliderShape2D(ColliderShapeType2D type, Vector2 offset, Vector2 size, float radius)
  {
    this.type = type;
    this.offset = offset;
    this.size = size;
    this.radius = radius;
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

    if (shapeA.type == ColliderShapeType2D.Box && shapeB.type == ColliderShapeType2D.Box)
    {
      return IntersectBoxBox(posA, shapeA.size, posB, shapeB.size, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Circle && shapeB.type == ColliderShapeType2D.Circle)
    {
      return IntersectCircleCircle(posA, shapeA.radius, posB, shapeB.radius, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Box && shapeB.type == ColliderShapeType2D.Circle)
    {
      return IntersectBoxCircle(posA, shapeA.size, posB, shapeB.radius, out normal, out penetration);
    }

    if (shapeA.type == ColliderShapeType2D.Circle && shapeB.type == ColliderShapeType2D.Box)
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

    if (shape.type == ColliderShapeType2D.Box)
    {
      return RaycastBox(origin, direction, center, shape.size, out distance);
    }

    if (shape.type == ColliderShapeType2D.Circle)
    {
      return RaycastCircle(origin, direction, center, shape.radius, out distance);
    }

    return false;
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
      if (shape.type == ColliderShapeType2D.Circle)
      {
        var center = GetWorldPosition(collider, shape.offset);
        if ((center - point).magnitude <= radius + shape.radius)
        {
          list.Add(collider);
        }
      }
      else if (shape.type == ColliderShapeType2D.Box)
      {
        var center = GetWorldPosition(collider, shape.offset);
        if (IntersectBoxCircle(center, shape.size, point, radius, out _, out _))
        {
          list.Add(collider);
        }
      }
    }

    results = list.ToArray();
    return results.Length > 0;
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
      if (shape.type == ColliderShapeType2D.Box)
      {
        if (IntersectBoxBox(point, size, center, shape.size, out _, out _))
        {
          list.Add(collider);
        }
      }
      else if (shape.type == ColliderShapeType2D.Circle)
      {
        if (IntersectBoxCircle(point, size, center, shape.radius, out _, out _))
        {
          list.Add(collider);
        }
      }
    }

    results = list.ToArray();
    return results.Length > 0;
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
      if (shape.type == ColliderShapeType2D.Box)
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
