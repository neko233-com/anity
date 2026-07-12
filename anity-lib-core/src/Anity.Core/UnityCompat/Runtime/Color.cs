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

  public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
  public static Color operator *(float f, Color c) => c * f;

  public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
  public static implicit operator Color(Vector4 v) => new Color(v.x, v.y, v.z, v.w);

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

  public static Color Lerp(Color a, Color b, float t)
  {
    t = Mathf.Clamp01(t);
    return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
  }

  public static Color LerpUnclamped(Color a, Color b, float t)
  {
    return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
  }

  public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;

  public Color linear
  {
    get
    {
      return new Color(
        Mathf.GammaToLinearSpace(r),
        Mathf.GammaToLinearSpace(g),
        Mathf.GammaToLinearSpace(b),
        a);
    }
  }

  public Color gamma
  {
    get
    {
      return new Color(
        Mathf.LinearToGammaSpace(r),
        Mathf.LinearToGammaSpace(g),
        Mathf.LinearToGammaSpace(b),
        a);
    }
  }

  public float maxColorComponent => Mathf.Max(Mathf.Max(r, g), b);

  public static Color HSVToRGB(float H, float S, float V)
  {
    return HSVToRGB(H, S, V, true);
  }

  public static Color HSVToRGB(float H, float S, float V, bool hdr)
  {
    _ = hdr;
    Color white = Color.white;
    if (S == 0f)
    {
      white.r = V;
      white.g = V;
      white.b = V;
    }
    else if (V == 0f)
    {
      white.r = 0f;
      white.g = 0f;
      white.b = 0f;
    }
    else
    {
      white.r = 0f;
      white.g = 0f;
      white.b = 0f;
      float num = H * 6f;
      int num2 = (int)MathF.Floor(num);
      float num3 = num - (float)num2;
      float num4 = V * (1f - S);
      float num5 = V * (1f - S * num3);
      float num6 = V * (1f - S * (1f - num3));
      switch (num2 + 1)
      {
        case 0:
          white.r = V; white.g = num4; white.b = num5; break;
        case 1:
          white.r = V; white.g = num6; white.b = num4; break;
        case 2:
          white.r = num5; white.g = V; white.b = num4; break;
        case 3:
          white.r = num4; white.g = V; white.b = num6; break;
        case 4:
          white.r = num4; white.g = num5; white.b = V; break;
        case 5:
          white.r = num6; white.g = num4; white.b = V; break;
        case 6:
          white.r = V; white.g = num4; white.b = num5; break;
        case 7:
          white.r = V; white.g = num6; white.b = num4; break;
      }
    }
    white.r = Mathf.Clamp(white.r, 0f, 1f);
    white.g = Mathf.Clamp(white.g, 0f, 1f);
    white.b = Mathf.Clamp(white.b, 0f, 1f);
    white.a = 1f;
    return white;
  }

  public static void RGBToHSV(Color rgbColor, out float H, out float S, out float V)
  {
    if (rgbColor.b > rgbColor.g && rgbColor.b > rgbColor.r)
    {
      RGBToHSVHelper(4f, rgbColor.b, rgbColor.r, rgbColor.g, out H, out S, out V);
    }
    else if (rgbColor.g > rgbColor.r)
    {
      RGBToHSVHelper(2f, rgbColor.g, rgbColor.b, rgbColor.r, out H, out S, out V);
    }
    else
    {
      RGBToHSVHelper(0f, rgbColor.r, rgbColor.g, rgbColor.b, out H, out S, out V);
    }
  }

  private static void RGBToHSVHelper(float offset, float dominantcolor, float colorone, float colortwo, out float H, out float S, out float V)
  {
    V = dominantcolor;
    if (V != 0f)
    {
      float num;
      if (colorone > colortwo) num = colortwo;
      else num = colorone;
      float num2 = V - num;
      if (num2 != 0f)
      {
        S = num2 / V;
        H = offset + (colorone - colortwo) / num2;
      }
      else
      {
        S = 0f;
        H = offset + (colorone - colortwo);
      }
      H /= 6f;
      if (H < 0f) H += 1f;
    }
    else
    {
      S = 0f;
      H = 0f;
    }
  }
}
