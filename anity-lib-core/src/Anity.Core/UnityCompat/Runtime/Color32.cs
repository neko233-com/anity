namespace UnityEngine;

public struct Color32
{
  public byte r;
  public byte g;
  public byte b;
  public byte a;

  public Color32(byte r, byte g, byte b, byte a = 255)
  {
    this.r = r;
    this.g = g;
    this.b = b;
    this.a = a;
  }

  public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

  public static implicit operator Color32(Color c) => new Color32(
    (byte)Math.Clamp(Math.Round(c.r * 255f), 0, 255),
    (byte)Math.Clamp(Math.Round(c.g * 255f), 0, 255),
    (byte)Math.Clamp(Math.Round(c.b * 255f), 0, 255),
    (byte)Math.Clamp(Math.Round(c.a * 255f), 0, 255));

  public static Color32 Lerp(Color32 a, Color32 b, float t)
  {
    t = Math.Clamp(t, 0f, 1f);
    return new Color32(
      (byte)(a.r + (b.r - a.r) * t),
      (byte)(a.g + (b.g - a.g) * t),
      (byte)(a.b + (b.b - a.b) * t),
      (byte)(a.a + (b.a - a.a) * t));
  }

  public static Color32 LerpUnclamped(Color32 a, Color32 b, float t) => new Color32(
    (byte)(a.r + (b.r - a.r) * t),
    (byte)(a.g + (b.g - a.g) * t),
    (byte)(a.b + (b.b - a.b) * t),
    (byte)(a.a + (b.a - a.a) * t));

  public override string ToString() => $"RGBA({r}, {g}, {b}, {a})";

  public override bool Equals(object? obj) => obj is Color32 c && r == c.r && g == c.g && b == c.b && a == c.a;

  public override int GetHashCode() => HashCode.Combine(r, g, b, a);
}

