namespace UnityEngine;

public class Sprite : Object
{
  public Rect rect { get; set; }
  public Vector2 pivot { get; set; }
  public Vector4 border { get; set; }
  public Texture2D? texture { get; set; }
  public float pixelsPerUnit { get; set; } = 100f;

  public bool hasBorder => border.sqrMagnitude > 0f;

  public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot)
  {
    return new Sprite
    {
      texture = texture,
      rect = rect,
      pivot = pivot
    };
  }

  public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border)
  {
    return new Sprite
    {
      texture = texture,
      rect = rect,
      pivot = pivot,
      pixelsPerUnit = pixelsPerUnit,
      border = border
    };
  }
}

public enum SpriteMeshType
{
  FullRect,
  Tight
}
