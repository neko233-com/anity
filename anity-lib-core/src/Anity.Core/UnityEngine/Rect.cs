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

  public Vector2 position
  {
    get => new Vector2(x, y);
    set { x = value.x; y = value.y; }
  }

  public Vector2 size
  {
    get => new Vector2(width, height);
    set { width = value.x; height = value.y; }
  }

  public Vector2 center => new Vector2(x + width * 0.5f, y + height * 0.5f);

  public bool Contains(Vector2 point) => point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;
  public bool Contains(Vector3 point) => point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;

  public static bool operator ==(Rect lhs, Rect rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.width == rhs.width && lhs.height == rhs.height;
  public static bool operator !=(Rect lhs, Rect rhs) => !(lhs == rhs);

  public override bool Equals(object? obj) => obj is Rect other && this == other;
  public override int GetHashCode() => HashCode.Combine(x, y, width, height);
  public override string ToString() => $"(x:{x}, y:{y}, width:{width}, height:{height})";
}

