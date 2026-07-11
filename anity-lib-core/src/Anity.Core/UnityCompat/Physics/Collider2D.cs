using UnityEngine;

namespace UnityEngine;

public class Collider2D : Component
{
  public bool enabled { get; set; } = true;
  public bool isTrigger { get; set; }
  public Rigidbody2D? attachedRigidbody { get; set; }
  public PhysicsMaterial2D? sharedMaterial { get; set; }
  public Vector2 offset { get; set; }
  private List<ContactPoint2D> _contactPoints = new();

  public Bounds bounds
  {
    get
    {
      var shape = GetShape();
      Vector2 center = Vector2.zero;
      Vector2 size = Vector2.one;
      if (transform != null)
      {
        center = (Vector2)transform.TransformPoint(new Vector3(shape.offset.x, shape.offset.y, 0f));
      }
      else
      {
        center = shape.offset;
      }

      switch (shape.type)
      {
        case ColliderShapeType2D.Box:
          size = shape.size;
          break;
        case ColliderShapeType2D.Circle:
          size = new Vector2(shape.radius * 2f, shape.radius * 2f);
          break;
        case ColliderShapeType2D.Capsule:
          float capRadius = shape.capsuleDirection == CapsuleDirection2D.Vertical ? shape.size.x * 0.5f : shape.size.y * 0.5f;
          float capHeight = shape.capsuleDirection == CapsuleDirection2D.Vertical ? shape.size.y : shape.size.x;
          if (shape.capsuleDirection == CapsuleDirection2D.Vertical)
            size = new Vector2(capRadius * 2f, capHeight);
          else
            size = new Vector2(capHeight, capRadius * 2f);
          break;
        case ColliderShapeType2D.Polygon:
        case ColliderShapeType2D.Edge:
          if (shape.points != null && shape.points.Length > 0)
          {
            Vector2 min = shape.points[0];
            Vector2 max = shape.points[0];
            for (int i = 1; i < shape.points.Length; i++)
            {
              min = new Vector2(Mathf.Min(min.x, shape.points[i].x), Mathf.Min(min.y, shape.points[i].y));
              max = new Vector2(Mathf.Max(max.x, shape.points[i].x), Mathf.Max(max.y, shape.points[i].y));
            }
            size = max - min;
            center = (min + max) * 0.5f + shape.offset;
            if (transform != null) center = (Vector2)transform.TransformPoint(new Vector3(center.x, center.y, 0f));
          }
          break;
      }

      return new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.01f));
    }
  }

  public Collider2D()
  {
    Physics2D.s_world2D.Register(this);
  }

  ~Collider2D()
  {
    Physics2D.s_world2D.Unregister(this);
  }

  public bool IsTouching(Collider2D otherCollider)
  {
    if (otherCollider is null || otherCollider == this)
    {
      return false;
    }

    return Physics2D.s_world2D.Intersect(this, otherCollider);
  }

  public bool IsTouchingLayers(int layerMask = -1)
  {
    foreach (var collider in Physics2D.s_world2D.GetColliders())
    {
      if (collider is null || collider == this || collider.IsDestroyed)
      {
        continue;
      }

      if (!Physics2D.s_world2D.LayerMatches(collider.gameObject?.layer ?? 0, layerMask))
      {
        continue;
      }

      if (Physics2D.s_world2D.Intersect(this, collider))
      {
        return true;
      }
    }

    return false;
  }

  public Vector2 ClosestPoint(Vector2 position)
  {
    var shape = GetShape();
    Vector2 worldPos = transform != null ? (Vector2)transform.TransformPoint(new Vector3(shape.offset.x, shape.offset.y, 0f)) : shape.offset;

    switch (shape.type)
    {
      case ColliderShapeType2D.Box:
        Vector2 half = shape.size * 0.5f;
        return new Vector2(
          Math.Clamp(position.x, worldPos.x - half.x, worldPos.x + half.x),
          Math.Clamp(position.y, worldPos.y - half.y, worldPos.y + half.y));
      case ColliderShapeType2D.Circle:
        Vector2 delta = position - worldPos;
        float dist = delta.magnitude;
        if (dist <= shape.radius) return position;
        return worldPos + delta.normalized * shape.radius;
      case ColliderShapeType2D.Capsule:
        Physics2D.s_world2D.GetCapsuleTransform(shape, worldPos, out var capCenter, out var capAxis, out var capRadius, out var capHalfHeight);
        Vector2 p0 = capCenter - capAxis * capHalfHeight;
        Vector2 p1 = capCenter + capAxis * capHalfHeight;
        Vector2 closestSeg = Physics2D.s_world2D.ClosestPointOnSegment(position, p0, p1);
        Vector2 d = position - closestSeg;
        float dMag = d.magnitude;
        if (dMag <= capRadius) return position;
        return closestSeg + d.normalized * capRadius;
      case ColliderShapeType2D.Polygon:
        Vector2[] polyPoints = Physics2D.s_world2D.TransformPoints(shape.points, worldPos);
        if (polyPoints == null || polyPoints.Length == 0) return position;
        if (Physics2D.s_world2D.PointInPolygon(position, polyPoints)) return position;
        float bestDist = float.MaxValue;
        Vector2 bestPt = position;
        for (int i = 0; i < polyPoints.Length; i++)
        {
          Vector2 a = polyPoints[i];
          Vector2 b = polyPoints[(i + 1) % polyPoints.Length];
          Vector2 cp = Physics2D.s_world2D.ClosestPointOnSegment(position, a, b);
          float dPoly = Vector2.Distance(position, cp);
          if (dPoly < bestDist)
          {
            bestDist = dPoly;
            bestPt = cp;
          }
        }
        return bestPt;
      case ColliderShapeType2D.Edge:
        Vector2[] edgePoints = Physics2D.s_world2D.TransformPoints(shape.points, worldPos);
        if (edgePoints == null || edgePoints.Length < 2) return position;
        Vector2 bestEdgePt = position;
        float bestEdgeDist = float.MaxValue;
        for (int i = 0; i < edgePoints.Length - 1; i++)
        {
          Vector2 a = edgePoints[i];
          Vector2 b = edgePoints[i + 1];
          Vector2 cp = Physics2D.s_world2D.ClosestPointOnSegment(position, a, b);
          float dEdge = Vector2.Distance(position, cp);
          if (dEdge < bestEdgeDist)
          {
            bestEdgeDist = dEdge;
            bestEdgePt = cp;
          }
        }
        return bestEdgePt;
      default:
        return position;
    }
  }

  public bool IsTouching(Collider2D collider, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return IsTouching(collider);
  }

  public int GetContacts(ContactPoint2D[] contacts)
  {
    if (contacts == null) return 0;
    int count = Math.Min(_contactPoints.Count, contacts.Length);
    for (int i = 0; i < count; i++) contacts[i] = _contactPoints[i];
    return count;
  }

  public int GetContacts(List<ContactPoint2D> contacts)
  {
    if (contacts == null) return 0;
    contacts.AddRange(_contactPoints);
    return _contactPoints.Count;
  }

  internal void ClearContacts()
  {
    _contactPoints.Clear();
  }

  internal void AddContact(ContactPoint2D cp)
  {
    _contactPoints.Add(cp);
  }

  internal virtual ColliderShape2D GetShape()
  {
    return new ColliderShape2D(ColliderShapeType2D.Box, offset, Vector2.one, 0.5f);
  }
}

public class Collision2D
{
  public Collider2D collider { get; }
  public Collider2D otherCollider { get; }
  public Vector2 normal { get; }
  public Rigidbody2D? rigidbody => collider.attachedRigidbody;
  public Rigidbody2D? otherRigidbody => otherCollider.attachedRigidbody;
  public Transform? transform => collider.transform;
  public ContactPoint2D[] contacts { get; }
  public Vector2 relativeVelocity { get; }

  public Collision2D(Collider2D col, Collider2D other, Vector2 n, Vector2 relVel = default)
  {
    collider = col;
    otherCollider = other;
    normal = n;
    relativeVelocity = relVel;
    contacts = new[] { new ContactPoint2D(col, other, n, Vector2.zero, 0f) };
  }
}

public struct ContactPoint2D
{
  public Collider2D collider;
  public Collider2D otherCollider;
  public Vector2 normal;
  public Vector2 point;
  public float separation;
  public Rigidbody2D? rigidbody => collider?.attachedRigidbody;
  public Rigidbody2D? otherRigidbody => otherCollider?.attachedRigidbody;

  public ContactPoint2D(Collider2D col, Collider2D other, Vector2 n, Vector2 pt, float sep)
  {
    collider = col;
    otherCollider = other;
    normal = n;
    point = pt;
    separation = sep;
  }
}
