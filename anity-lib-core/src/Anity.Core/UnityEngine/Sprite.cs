namespace UnityEngine;

public class Sprite : Object
{
  public Rect rect { get; set; }
  public Vector2 pivot { get; set; }
  public Texture2D? texture { get; set; }

  public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot)
  {
    return new Sprite
    {
      texture = texture,
      rect = rect,
      pivot = pivot
    };
  }
}
