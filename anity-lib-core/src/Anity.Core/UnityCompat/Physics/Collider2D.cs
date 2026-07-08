using UnityEngine;

namespace UnityEngine;

public class Collider2D : Component
{
  public bool enabled { get; set; } = true;
  public bool isTrigger { get; set; }
  public Rigidbody2D? attachedRigidbody { get; set; }
  public Bounds? bounds { get; set; }
  public object? sharedMaterial { get; set; }
  public Vector2 offset { get; set; }

  public bool IsTouching(Collider2D otherCollider)
  {
    return ReferenceEquals(otherCollider, this);
  }
}
