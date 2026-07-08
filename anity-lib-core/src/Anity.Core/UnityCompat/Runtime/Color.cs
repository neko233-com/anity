namespace UnityEngine;

public struct Color
{
  public float r;
  public float g;
  public float b;
  public float a;

  public Color(float r, float g, float b, float a = 1f)
  {
    this.r = r; this.g = g; this.b = b; this.a = a;
  }

  public static Color clear => new(0f, 0f, 0f, 0f);
  public static Color white => new(1f, 1f, 1f, 1f);
  public static Color black => new(0f, 0f, 0f, 1f);
  public static Color red => new(1f, 0f, 0f, 1f);
  public static Color green => new(0f, 1f, 0f, 1f);
  public static Color blue => new(0f, 0f, 1f, 1f);
  public static Color gray => new(0.5f, 0.5f, 0.5f, 1f);
  public static Color yellow => new(1f, 0.92f, 0.016f, 1f);
  public static Color cyan => new(0f, 1f, 1f, 1f);
  public static Color magenta => new(1f, 0f, 1f, 1f);

  public static bool operator !=(Color a, Color b)
  {
    return !a.Equals(b);
  }

  public static bool operator ==(Color a, Color b)
  {
    return a.Equals(b);
  }

  public override bool Equals(object obj)
  {
    if (obj is Color other)
    {
      return r == other.r && g == other.g && b == other.b && a == other.a;
    }
    return false;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(r, g, b, a);
  }
}
