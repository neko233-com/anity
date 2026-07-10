using UnityEngine;

namespace UnityEngine;

public class Collider2D : Component
{
  public bool enabled { get; set; } = true;
  public bool isTrigger { get; set; }
  public Rigidbody2D? attachedRigidbody { get; set; }
  public Bounds? bounds { get; set; }
  public PhysicsMaterial2D? sharedMaterial { get; set; }
  public Vector2 offset { get; set; }

  public Collider2D()
  {
    Physics2DWorld.Register(this);
  }

  public bool IsTouching(Collider2D otherCollider)
  {
    if (otherCollider is null || otherCollider == this)
    {
      return false;
    }

    return Physics2DWorld.Intersect(this, otherCollider);
  }

  public bool IsTouchingLayers(int layerMask = -1)
  {
    foreach (var collider in Physics2DWorld.GetColliders())
    {
      if (collider is null || collider == this || collider.IsDestroyed)
      {
        continue;
      }

      if (!Physics2DWorld.LayerMatches(collider.gameObject?.layer ?? 0, layerMask))
      {
        continue;
      }

      if (Physics2DWorld.Intersect(this, collider))
      {
        return true;
      }
    }

    return false;
  }

  internal virtual ColliderShape2D GetShape()
  {
    return new ColliderShape2D(ColliderShapeType2D.Box, offset, Vector2.one, 0.5f);
  }
}
