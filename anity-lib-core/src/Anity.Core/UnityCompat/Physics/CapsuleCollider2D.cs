namespace UnityEngine;

/// <summary>
/// 2D capsule collider.
/// </summary>
public class CapsuleCollider2D : Collider2D
{
    public Vector2 size { get; set; } = new Vector2(1f, 2f);
    public CapsuleDirection2D direction { get; set; } = CapsuleDirection2D.Vertical;

    internal override ColliderShape2D GetShape()
    {
        return new ColliderShape2D(ColliderShapeType2D.Capsule, offset, size, 0f);
    }
}

public enum CapsuleDirection2D
{
    Vertical,
    Horizontal
}
