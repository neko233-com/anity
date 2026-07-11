using System;

namespace UnityEngine;

public struct Rect : IEquatable<Rect>
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

  public Rect(Vector2 position, Vector2 size)
  {
    x = position.x;
    y = position.y;
    width = size.x;
    height = size.y;
  }

  public static Rect zero => new(0f, 0f, 0f, 0f);

  public float xMin
  {
    get => x;
    set
    {
      float xMax = this.xMax;
      x = value;
      width = xMax - x;
    }
  }

  public float yMin
  {
    get => y;
    set
    {
      float yMax = this.yMax;
      y = value;
      height = yMax - y;
    }
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

  public Vector2 min
  {
    get => new Vector2(xMin, yMin);
    set
    {
      xMin = value.x;
      yMin = value.y;
    }
  }

  public Vector2 max
  {
    get => new Vector2(xMax, yMax);
    set
    {
      xMax = value.x;
      yMax = value.y;
    }
  }

  public Vector2 position
  {
    get => new Vector2(x, y);
    set { x = value.x; y = value.y; }
  }

  public Vector2 center => new Vector2(x + width * 0.5f, y + height * 0.5f);

  public Vector2 size
  {
    get => new Vector2(width, height);
    set { width = value.x; height = value.y; }
  }

  public bool Contains(Vector2 point)
  {
    return point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
  }

  public bool Contains(Vector3 point)
  {
    return point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
  }

  public bool Contains(Vector3 point, bool allowInverse)
  {
    _ = allowInverse;
    return Contains(point);
  }

  public bool Overlaps(Rect other)
  {
    return other.xMin < xMax && other.xMax > xMin && other.yMin < yMax && other.yMax > yMin;
  }

  public bool Overlaps(Rect other, bool allowInverse)
  {
    _ = allowInverse;
    return Overlaps(other);
  }

  public void Expand(float expand)
  {
    x -= expand;
    y -= expand;
    width += expand * 2f;
    height += expand * 2f;
  }

  public void Expand(Vector2 expand)
  {
    x -= expand.x;
    y -= expand.y;
    width += expand.x * 2f;
    height += expand.y * 2f;
  }

  public void Encapsulate(Vector2 point)
  {
    float newXMin = MathF.Min(xMin, point.x);
    float newYMin = MathF.Min(yMin, point.y);
    float newXMax = MathF.Max(xMax, point.x);
    float newYMax = MathF.Max(yMax, point.y);
    x = newXMin;
    y = newYMin;
    width = newXMax - newXMin;
    height = newYMax - newYMin;
  }

  public void Encapsulate(Vector3 point)
  {
    Encapsulate(new Vector2(point.x, point.y));
  }

  public void Encapsulate(Rect other)
  {
    Encapsulate(other.min);
    Encapsulate(other.max);
  }

  public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
  {
    return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
  }

  public Vector2 GetPoint(Vector2 normalizedCoords)
  {
    return new Vector2(
      x + width * normalizedCoords.x,
      y + height * normalizedCoords.y);
  }

  public Vector2 NormalizedToPoint(Rect rectangle, Vector2 normalizedRectCoordinates)
  {
    return new Vector2(
      rectangle.x + rectangle.width * normalizedRectCoordinates.x,
      rectangle.y + rectangle.height * normalizedRectCoordinates.y);
  }

  public static Vector2 PointToNormalized(Rect rectangle, Vector2 point)
  {
    return new Vector2(
      MathF.Abs(rectangle.width) > 1e-6f ? (point.x - rectangle.x) / rectangle.width : 0f,
      MathF.Abs(rectangle.height) > 1e-6f ? (point.y - rectangle.y) / rectangle.height : 0f);
  }

  public static bool operator ==(Rect lhs, Rect rhs)
  {
    return lhs.x == rhs.x && lhs.y == rhs.y && lhs.width == rhs.width && lhs.height == rhs.height;
  }

  public static bool operator !=(Rect lhs, Rect rhs)
  {
    return !(lhs == rhs);
  }

  public override bool Equals(object? obj)
  {
    return obj is Rect other && this == other;
  }

  public bool Equals(Rect other)
  {
    return this == other;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(x, y, width, height);
  }

  public override string ToString()
  {
    return $"(x:{x:F2}, y:{y:F2}, width:{width:F2}, height:{height:F2})";
  }
}
