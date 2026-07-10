namespace UnityEngine;

public class BoxCollider2D : Collider2D
{
  public Vector2 size { get; set; } = Vector2.one;

  internal override ColliderShape2D GetShape()
  {
    return new ColliderShape2D(ColliderShapeType2D.Box, offset, size, 0f);
  }
}
