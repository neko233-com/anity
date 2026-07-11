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
  public static float GammaToLinearSpace(float value) => Sqrt(value);
  public static float LinearToGammaSpace(float value) => value * value;
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
}
