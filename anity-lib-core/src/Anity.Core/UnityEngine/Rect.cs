namespace UnityEngine;

public struct Rect
{
  public float x;
  public float y;
  public float width;
  public float height;

  public Rect(float x, float y, float width, float height)
  {
    this.x = x;
    this.y = y;
    this.width = width;
    this.height = height;
  }

  public static Rect zero => new(0f, 0f, 0f, 0f);

  public float xMin
  {
    get => x;
    set => x = value;
  }

  public float yMin
  {
    get => y;
    set => y = value;
  }

  public float xMax
  {
    get => x + width;
    set => width = value - x;
  }

  public float yMax
  {
    get => y + height;
    set => height = value - y;
  }
}

