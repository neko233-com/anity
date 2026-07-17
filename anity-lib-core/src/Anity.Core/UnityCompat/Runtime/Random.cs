using System;

namespace UnityEngine;

public static class Random
{
  private const uint SeedMultiplier = 1812433253u;
  private const uint FloatMantissaMask = 0x007fffffu;
  private const float FloatMantissaMaximum = 8388607f;
  private static State _state = CreateState(unchecked((uint)Environment.TickCount));
  private static int _currentSeed;

  [Serializable]
  public struct State
  {
    [SerializeField] private int s0;
    [SerializeField] private int s1;
    [SerializeField] private int s2;
    [SerializeField] private int s3;

    internal State(uint seed)
    {
      uint word0 = seed;
      uint word1 = unchecked(SeedMultiplier * word0 + 1u);
      uint word2 = unchecked(SeedMultiplier * word1 + 1u);
      uint word3 = unchecked(SeedMultiplier * word2 + 1u);
      s0 = unchecked((int)word0);
      s1 = unchecked((int)word1);
      s2 = unchecked((int)word2);
      s3 = unchecked((int)word3);
    }

    internal uint S0 { readonly get => unchecked((uint)s0); set => s0 = unchecked((int)value); }
    internal uint S1 { readonly get => unchecked((uint)s1); set => s1 = unchecked((int)value); }
    internal uint S2 { readonly get => unchecked((uint)s2); set => s2 = unchecked((int)value); }
    internal uint S3 { readonly get => unchecked((uint)s3); set => s3 = unchecked((int)value); }
  }

  public static State state
  {
    get => _state;
    set => _state = value;
  }

  public static int seed
  {
    get => _currentSeed;
    set => InitState(value);
  }

  public static float value
  {
    get
    {
      uint word = NextUInt();
      return (word & FloatMantissaMask) / FloatMantissaMaximum;
    }
  }

  public static void InitState(int seed)
  {
    _currentSeed = seed;
    _state = CreateState(unchecked((uint)seed));
  }

  public static float Range(float minInclusive, float maxInclusive)
  {
    if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
    return minInclusive + (maxInclusive - minInclusive) * value;
  }

  public static int Range(int minInclusive, int maxExclusive)
  {
    if (minInclusive >= maxExclusive) return minInclusive;
    long range = (long)maxExclusive - minInclusive;
    long offset = Math.Min((long)(value * range), range - 1);
    return checked((int)(minInclusive + offset));
  }

  private static State CreateState(uint seed) => new(seed);

  private static uint NextUInt()
  {
    uint value = _state.S0 ^ (_state.S0 << 11);
    _state.S0 = _state.S1;
    _state.S1 = _state.S2;
    _state.S2 = _state.S3;
    _state.S3 = _state.S3 ^ (_state.S3 >> 19) ^ value ^ (value >> 8);
    return _state.S3;
  }

  public static Vector3 insideUnitSphere
  {
    get
    {
      float theta = Range(0f, MathF.PI * 2f);
      float phi = MathF.Acos(Range(-1f, 1f));
      float r = MathF.Pow(value, 1f / 3f);
      float sinPhi = MathF.Sin(phi);
      return new Vector3(
        r * sinPhi * MathF.Cos(theta),
        r * sinPhi * MathF.Sin(theta),
        r * MathF.Cos(phi)
      );
    }
  }

  public static Vector2 insideUnitCircle
  {
    get
    {
      float theta = Range(0f, MathF.PI * 2f);
      float r = MathF.Sqrt(value);
      return new Vector2(r * MathF.Cos(theta), r * MathF.Sin(theta));
    }
  }

  public static Vector3 onUnitSphere
  {
    get
    {
      Vector3 v = insideUnitSphere;
      return v.sqrMagnitude > 1e-10f ? v.normalized : Vector3.forward;
    }
  }

  public static Quaternion rotation
  {
    get
    {
      float u1 = value;
      float u2 = value;
      float u3 = value;
      float sqrt1MinusU1 = MathF.Sqrt(1f - u1);
      float sqrtU1 = MathF.Sqrt(u1);
      return new Quaternion(
        sqrt1MinusU1 * MathF.Sin(2f * MathF.PI * u2),
        sqrt1MinusU1 * MathF.Cos(2f * MathF.PI * u2),
        sqrtU1 * MathF.Sin(2f * MathF.PI * u3),
        sqrtU1 * MathF.Cos(2f * MathF.PI * u3)
      ).normalized;
    }
  }

  public static Quaternion rotationUniform => rotation;

  public static Color ColorHSV() => ColorHSV(0f, 1f, 0f, 1f, 0f, 1f, 1f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax) => ColorHSV(hueMin, hueMax, 0f, 1f, 0f, 1f, 1f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax) => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, 0f, 1f, 1f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax) => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax, 1f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax, float alphaMin, float alphaMax)
  {
    float h = Range(hueMin, hueMax);
    float s = Range(saturationMin, saturationMax);
    float v = Range(valueMin, valueMax);
    float a = Range(alphaMin, alphaMax);
    var color = HSVToRGB(h, s, v);
    color.a = a;
    return color;
  }

  private static Color HSVToRGB(float h, float s, float v)
  {
    h = Mathf.Repeat(h, 1f);
    s = Mathf.Clamp01(s);
    v = Mathf.Clamp01(v);
    if (s == 0f) return new Color(v, v, v, 1f);
    float chroma = v * s;
    float hPrime = h * 6f;
    float x = chroma * (1f - MathF.Abs(hPrime % 2f - 1f));
    float m = v - chroma;
    float r = 0f, g = 0f, b = 0f;
    int sector = (int)hPrime;
    switch (sector)
    {
      case 0: r = chroma; g = x; break;
      case 1: r = x; g = chroma; break;
      case 2: g = chroma; b = x; break;
      case 3: g = x; b = chroma; break;
      case 4: r = x; b = chroma; break;
      default: r = chroma; b = x; break;
    }
    return new Color(r + m, g + m, b + m, 1f);
  }
}
