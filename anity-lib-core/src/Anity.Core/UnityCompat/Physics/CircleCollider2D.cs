namespace UnityEngine;

public class CircleCollider2D : Collider2D
{
  public float radius { get; set; } = 0.5f;

  internal override ColliderShape2D GetShape()
  {
    return new ColliderShape2D(ColliderShapeType2D.Circle, offset, Vector2.zero, radius);
  }
}
