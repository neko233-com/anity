using System;

namespace Unity.Burst;

/// <summary>
/// Burst compiler attribute for high-performance code generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class BurstCompileAttribute : Attribute
{
    public bool CompileSynchronously { get; set; } = true;
    public bool Debug { get; set; }
    public bool DisableSafetyChecks { get; set; }
    public bool EnableBurstCompilation { get; set; } = true;
    public bool EnableDirectPrinter { get; set; }
    public bool EnableExternalDebugger { get; set; }
    public bool EnableFailSafetyChecks { get; set; }
    public bool EnableNativeInDebugMode { get; set; }
    public bool ForceSynchronousCompilation { get; set; }
    public bool ThrowOnDebugException { get; set; }

    public BurstCompileAttribute() { }

    public BurstCompileAttribute(params Type[] options)
    {
    }
}

/// <summary>
/// Burst-compatible math library.
/// </summary>
public static class math
{
    public const float PI = 3.141592653589793115997963468544185161590576171875f;
    public const float EPSILON = 1.1754943508222875079687365372222459899711132049560546875e-38f;
    public const float INFINITY = float.PositiveInfinity;
    public const float NaN = float.NaN;

    public static float abs(float x) => MathF.Abs(x);
    public static int abs(int x) => Math.Abs(x);
    public static float ceil(float x) => MathF.Ceiling(x);
    public static float floor(float x) => MathF.Floor(x);
    public static float round(float x) => MathF.Round(x);
    public static float sqrt(float x) => MathF.Sqrt(x);
    public static float pow(float x, float y) => MathF.Pow(x, y);
    public static float sin(float x) => MathF.Sin(x);
    public static float cos(float x) => MathF.Cos(x);
    public static float tan(float x) => MathF.Tan(x);
    public static float asin(float x) => MathF.Asin(x);
    public static float acos(float x) => MathF.Acos(x);
    public static float atan(float x) => MathF.Atan(x);
    public static float atan2(float y, float x) => MathF.Atan2(y, x);
    public static float log(float x) => MathF.Log(x);
    public static float exp(float x) => MathF.Exp(x);
    public static float sign(float x) => MathF.Sign(x);
    public static float clamp(float x, float min, float max) => Math.Clamp(x, min, max);
    public static int clamp(int x, int min, int max) => Math.Clamp(x, min, max);
    public static float lerp(float a, float b, float t) => a + (b - a) * t;
    public static float inverseLerp(float a, float b, float value) => (value - a) / (b - a);
    public static float remap(float value, float fromMin, float fromMax, float toMin, float toMax) => toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
    public static float smoothstep(float from, float to, float t) => clamp((t - from) / (to - from), 0.0f, 1.0f);
    public static float max(float a, float b) => MathF.Max(a, b);
    public static float min(float a, float b) => MathF.Min(a, b);
    public static float step(float y, float x) => x >= y ? 1.0f : 0.0f;
    public static float frac(float x) => x - floor(x);
    public static float fmod(float x, float y) => x % y;
    public static float saturate(float x) => clamp(x, 0.0f, 1.0f);
    public static float radians(float degrees) => degrees * PI / 180.0f;
    public static float degrees(float radians) => radians * 180.0f / PI;
}

/// <summary>
/// Burst-compatible fixed point math.
/// </summary>
public struct FixedPoint
{
    private int _value;
    private const int ONE = 1000;

    public FixedPoint(int value) => _value = value * ONE;
    public FixedPoint(float value) => _value = (int)(value * ONE);

    public static implicit operator FixedPoint(int value) => new(value);
    public static implicit operator FixedPoint(float value) => new(value);
    public static implicit operator int(FixedPoint value) => value._value / ONE;
    public static implicit operator float(FixedPoint value) => value._value / (float)ONE;

    public static FixedPoint operator +(FixedPoint a, FixedPoint b) => new(a._value + b._value);
    public static FixedPoint operator -(FixedPoint a, FixedPoint b) => new(a._value - b._value);
    public static FixedPoint operator *(FixedPoint a, FixedPoint b) => new(a._value * b._value / ONE);
    public static FixedPoint operator /(FixedPoint a, FixedPoint b) => new(a._value * ONE / b._value);
    public static FixedPoint operator -(FixedPoint a) => new(-a._value);

    public static bool operator ==(FixedPoint a, FixedPoint b) => a._value == b._value;
    public static bool operator !=(FixedPoint a, FixedPoint b) => a._value != b._value;
    public static bool operator >(FixedPoint a, FixedPoint b) => a._value > b._value;
    public static bool operator <(FixedPoint a, FixedPoint b) => a._value < b._value;
    public static bool operator >=(FixedPoint a, FixedPoint b) => a._value >= b._value;
    public static bool operator <=(FixedPoint a, FixedPoint b) => a._value <= b._value;

    public override bool Equals(object obj) => obj is FixedPoint other && Equals(other);
    public bool Equals(FixedPoint other) => _value == other._value;
    public override int GetHashCode() => _value;
    public override string ToString() => (_value / (float)ONE).ToString();
}
