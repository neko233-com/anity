using System;

namespace UnityEngine;

public static class Mathf
{
  public const float PI = MathF.PI;
  public const float Deg2Rad = MathF.PI / 180f;
  public const float Rad2Deg = 180f / MathF.PI;

  public static float Abs(float value) => MathF.Abs(value);
  public static float Sqrt(float value) => MathF.Sqrt(value);
  public static float Sin(float value) => MathF.Sin(value);
  public static float Cos(float value) => MathF.Cos(value);
  public static float Pow(float f, float p) => MathF.Pow(f, p);
  public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
  public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
  public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
  public static int FloorToInt(float value) => (int)MathF.Floor(value);
  public static int RoundToInt(float value) => (int)MathF.Round(value);
  public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
  public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
  public static float Clamp01(float value) => Clamp(value, 0f, 1f);
  public static float Max(float a, float b) => MathF.Max(a, b);
  public static float Min(float a, float b) => MathF.Min(a, b);
  public static int Max(int a, int b) => Math.Max(a, b);
  public static int Min(int a, int b) => Math.Min(a, b);
  public const float Epsilon = 1e-06f;
  public static float Round(float value) => MathF.Round(value);
  public static bool Approximately(float a, float b) => Abs(b - a) < Max(1e-06f * Max(Abs(a), Abs(b)), 1e-06f * 8f);
  public static float InverseLerp(float a, float b, float value) => (a == b) ? 0f : Clamp01((value - a) / (b - a));
  public static float Atan2(float y, float x) => MathF.Atan2(y, x);
  public static float Tan(float value) => MathF.Tan(value);
  public static float Acos(float value) => MathF.Acos(value);
  public static float Asin(float value) => MathF.Asin(value);
  public static float Atan(float value) => MathF.Atan(value);
  public static float Sign(float value) => MathF.Sign(value);
  public static float Exp(float value) => MathF.Exp(value);
  public static float Log(float value) => MathF.Log(value);
  public static float Log10(float value) => MathF.Log10(value);
  public static float Log(float a, float b) => MathF.Log(a, b);
  public static float Ceil(float value) => MathF.Ceiling(value);
  public static float Floor(float value) => MathF.Floor(value);
  public static float Min(float a, float b, float c) => MathF.Min(a, MathF.Min(b, c));
  public static float Max(float a, float b, float c) => MathF.Max(a, MathF.Max(b, c));
  public static float DeltaAngle(float current, float target)
  {
    float delta = (target - current + 180f) % 360f - 180f;
    return delta < -180f ? delta + 360f : delta;
  }
  public static float Repeat(float t, float length) => Clamp(t - MathF.Floor(t / length) * length, 0f, length);
  public static float PingPong(float t, float length) => length - MathF.Abs(Repeat(t, length * 2f) - length);
  public static float MoveTowards(float current, float target, float maxDelta)
  {
    if (Abs(target - current) <= maxDelta) return target;
    return current + MathF.Sign(target - current) * maxDelta;
  }
  /// <summary>Unity sRGB transfer (exact curve, not gamma 2.0 approx).</summary>
  public static float GammaToLinearSpace(float value)
  {
    if (value <= 0.04045f) return value / 12.92f;
    return Pow((value + 0.055f) / 1.055f, 2.4f);
  }

  public static float LinearToGammaSpace(float value)
  {
    if (value <= 0.0031308f) return 12.92f * value;
    return 1.055f * Pow(value, 1f / 2.4f) - 0.055f;
  }
  public const float Infinity = float.PositiveInfinity;
  public const float NegativeInfinity = float.NegativeInfinity;

  public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed)
  {
    return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
  }

  public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
  {
    smoothTime = Max(0.0001f, smoothTime);
    float omega = 2f / smoothTime;
    float x = omega * deltaTime;
    float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
    float change = current - target;
    float originalTo = target;
    float maxChange = maxSpeed * smoothTime;
    change = Clamp(change, -maxChange, maxChange);
    target = current - change;
    float temp = (currentVelocity + omega * change) * deltaTime;
    currentVelocity = (currentVelocity - omega * temp) * exp;
    float output = target + (change + temp) * exp;
    if (originalTo - current > 0f == output > originalTo)
    {
      output = originalTo;
      currentVelocity = (output - originalTo) / deltaTime;
    }
    return output;
  }

  public static float SmoothStep(float from, float to, float t)
  {
    t = Clamp01(t);
    t = t * t * (3f - 2f * t);
    return to * t + from * (1f - t);
  }

  public static bool IsPowerOfTwo(int value)
  {
    return value > 0 && (value & (value - 1)) == 0;
  }

  public static int NextPowerOfTwo(int value)
  {
    if (value <= 0) return 1;
    value--;
    value |= value >> 1;
    value |= value >> 2;
    value |= value >> 4;
    value |= value >> 8;
    value |= value >> 16;
    return value + 1;
  }

  public static int ClosestPowerOfTwo(int value)
  {
    int next = NextPowerOfTwo(value);
    int prev = next >> 1;
    if (prev <= 0) return next;
    return (value - prev) < (next - value) ? prev : next;
  }

  public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed)
  {
    return SmoothDampAngle(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
  }

  public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime)
  {
    return SmoothDampAngle(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.deltaTime);
  }

  public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
  {
    target = current + DeltaAngle(current, target);
    return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
  }

  public static float LerpAngle(float a, float b, float t)
  {
    float delta = DeltaAngle(a, b);
    return a + delta * Clamp01(t);
  }

  public static float MoveTowardsAngle(float current, float target, float maxDelta)
  {
    float deltaAngle = DeltaAngle(current, target);
    if (-maxDelta < deltaAngle && deltaAngle < maxDelta)
      return target;
    target = current + deltaAngle;
    return MoveTowards(current, target, maxDelta);
  }

  public static float Sinh(float value) => MathF.Sinh(value);
  public static float Cosh(float value) => MathF.Cosh(value);
  public static float Tanh(float value) => MathF.Tanh(value);

  /// <summary>
  /// 2D Perlin noise in [0,1] (Unity Mathf.PerlinNoise range). Improved Perlin (2002).
  /// </summary>
  public static float PerlinNoise(float x, float y)
  {
    // Map classic [-1,1] gradient noise to Unity's [0,1] output range
    return Clamp01(PerlinNoiseRaw(x, y) * 0.5f + 0.5f);
  }

  /// <summary>Classic improved Perlin in approx [-1,1] (internal diagnostic / tests via public API).</summary>
  public static float PerlinNoiseRaw(float x, float y)
  {
    int xi = FloorToInt(x) & 255;
    int yi = FloorToInt(y) & 255;
    float xf = x - Floor(x);
    float yf = y - Floor(y);
    float u = Fade(xf);
    float v = Fade(yf);

    int aa = Perm[Perm[xi] + yi];
    int ab = Perm[Perm[xi] + yi + 1];
    int ba = Perm[Perm[xi + 1] + yi];
    int bb = Perm[Perm[xi + 1] + yi + 1];

    float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
    float x2 = Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
    return Lerp(x1, x2, v);
  }

  private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

  private static float Grad(int hash, float x, float y)
  {
    // 2D gradient from low 3 bits of hash
    int h = hash & 7;
    float u = h < 4 ? x : y;
    float v = h < 4 ? y : x;
    return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
  }

  // Ken Perlin's permutation table (doubled to avoid overflow)
  private static readonly int[] Perm = BuildPerm();

  private static int[] BuildPerm()
  {
    int[] p =
    {
      151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,
      8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,
      35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,
      134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
      55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,
      18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,
      250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,
      189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
      172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,
      228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,
      107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,
      138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
    };
    var perm = new int[512];
    for (int i = 0; i < 512; i++)
      perm[i] = p[i & 255];
    return perm;
  }

  public static float Gamma(float value, float absmax, float gamma)
  {
    bool negative = value < 0f;
    float absval = Abs(value);
    if (absval > absmax)
      return negative ? -absval : absval;
    float result = Pow(absval / absmax, gamma) * absmax;
    return negative ? -result : result;
  }

  public static void ColorToHSV(Color color, out float H, out float S, out float V)
  {
    Color.RGBToHSV(color, out H, out S, out V);
  }

  public static Color HSVToRGB(float H, float S, float V)
  {
    return Color.HSVToRGB(H, S, V);
  }

  public static Color HSVToRGB(float H, float S, float V, bool hdr)
  {
    return Color.HSVToRGB(H, S, V, hdr);
  }

  public static float Max(params float[] values)
  {
    float max = values[0];
    for (int i = 1; i < values.Length; i++)
      if (values[i] > max) max = values[i];
    return max;
  }

  public static float Min(params float[] values)
  {
    float min = values[0];
    for (int i = 1; i < values.Length; i++)
      if (values[i] < min) min = values[i];
    return min;
  }
}
