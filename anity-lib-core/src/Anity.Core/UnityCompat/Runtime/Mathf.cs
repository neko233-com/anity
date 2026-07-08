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
  public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
  public static int FloorToInt(float value) => (int)MathF.Floor(value);
  public static int RoundToInt(float value) => (int)MathF.Round(value);
  public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
  public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
  public static float Clamp01(float value) => Clamp(value, 0f, 1f);
  public static float Max(float a, float b) => MathF.Max(a, b);
  public static float Min(float a, float b) => MathF.Min(a, b);
}
