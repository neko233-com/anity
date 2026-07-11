using System;

namespace UnityEngine;

public static class Random
{
  private static System.Random _rng = new();
  private static int _currentSeed;

  public struct State
  {
    public int seed;
    public int[] buffer;
    public int index;

    public State(int seed)
    {
      this.seed = seed;
      buffer = new int[56];
      index = 0;
      var r = new System.Random(seed);
      for (int i = 0; i < buffer.Length; i++)
      {
        buffer[i] = r.Next();
      }
    }
  }

  public static State state
  {
    get => new State(_currentSeed);
    set
    {
      _currentSeed = value.seed;
      _rng = new System.Random(_currentSeed);
    }
  }

  public static int seed
  {
    get => _currentSeed;
    set => InitState(value);
  }

  public static float value => (float)_rng.NextDouble();

  public static void InitState(int seed)
  {
    _currentSeed = seed;
    _rng = new System.Random(seed);
  }

  public static float Range(float minInclusive, float maxInclusive)
  {
    if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
    return minInclusive + (maxInclusive - minInclusive) * value;
  }

  public static int Range(int minInclusive, int maxExclusive)
  {
    if (minInclusive >= maxExclusive) return minInclusive;
    return _rng.Next(minInclusive, maxExclusive);
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

  public static Color ColorHSV() => ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax) => ColorHSV(hueMin, hueMax, 0f, 1f, 0f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax) => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, 0f, 1f);
  public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax)
  {
    float h = Range(hueMin, hueMax);
    float s = Range(saturationMin, saturationMax);
    float v = Range(valueMin, valueMax);
    return HSVToRGB(h, s, v);
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
