using System;

namespace UnityEngine;

public partial class ParticleSystem
{
    [Serializable]
    public struct MinMaxCurve
    {
        public ParticleSystemCurveMode mode;
        public float constant;
        public float constantMin;
        public float constantMax;
        public AnimationCurve curve;
        public AnimationCurve curveMin;
        public AnimationCurve curveMax;
        public float curveMultiplier;

        public MinMaxCurve(float constant)
        {
            mode = ParticleSystemCurveMode.Constant;
            this.constant = constant;
            constantMin = constant;
            constantMax = constant;
            curve = AnimationCurve.Linear(0f, constant, 1f, constant);
            curveMin = curve;
            curveMax = curve;
            curveMultiplier = 1f;
        }

        public MinMaxCurve(float constant, AnimationCurve curve)
        {
            mode = ParticleSystemCurveMode.Curve;
            this.constant = constant;
            constantMin = constant;
            constantMax = constant;
            this.curve = curve;
            curveMin = curve;
            curveMax = curve;
            curveMultiplier = 1f;
        }

        public MinMaxCurve(float min, float max)
        {
            mode = ParticleSystemCurveMode.TwoConstants;
            constant = (min + max) * 0.5f;
            constantMin = min;
            constantMax = max;
            curve = AnimationCurve.Linear(0f, constant, 1f, constant);
            curveMin = curve;
            curveMax = curve;
            curveMultiplier = 1f;
        }

        public MinMaxCurve(float min, float max, AnimationCurve curveMin, AnimationCurve curveMax)
        {
            mode = ParticleSystemCurveMode.TwoCurves;
            constant = (min + max) * 0.5f;
            constantMin = min;
            constantMax = max;
            curve = curveMin;
            this.curveMin = curveMin;
            this.curveMax = curveMax;
            curveMultiplier = 1f;
        }

        public float Evaluate(float time)
        {
            return Evaluate(time, UnityEngine.Random.value);
        }

        public float Evaluate(float time, float randomFactor)
        {
            switch (mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return constant * curveMultiplier;
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Lerp(constantMin, constantMax, randomFactor) * curveMultiplier;
                case ParticleSystemCurveMode.Curve:
                    return curve.Evaluate(time) * curveMultiplier;
                case ParticleSystemCurveMode.TwoCurves:
                    float minVal = curveMin != null ? curveMin.Evaluate(time) : constantMin;
                    float maxVal = curveMax != null ? curveMax.Evaluate(time) : constantMax;
                    return Mathf.Lerp(minVal, maxVal, randomFactor) * curveMultiplier;
                default:
                    return constant * curveMultiplier;
            }
        }

        public static implicit operator MinMaxCurve(float value) => new MinMaxCurve(value);
    }

    [Serializable]
    public struct MinMaxGradient
    {
        public ParticleSystemGradientMode mode;
        public Color color;
        public Color colorMin;
        public Color colorMax;
        public Gradient gradient;
        public Gradient gradientMin;
        public Gradient gradientMax;

        public MinMaxGradient(Color color)
        {
            mode = ParticleSystemGradientMode.Color;
            this.color = color;
            colorMin = color;
            colorMax = color;
            gradient = new Gradient();
            gradientMin = gradient;
            gradientMax = gradient;
        }

        public MinMaxGradient(Gradient gradient)
        {
            mode = ParticleSystemGradientMode.Gradient;
            color = Color.white;
            colorMin = Color.white;
            colorMax = Color.white;
            this.gradient = gradient;
            gradientMin = gradient;
            gradientMax = gradient;
        }

        public MinMaxGradient(Color min, Color max)
        {
            mode = ParticleSystemGradientMode.TwoColors;
            color = Color.Lerp(min, max, 0.5f);
            colorMin = min;
            colorMax = max;
            gradient = new Gradient();
            gradientMin = gradient;
            gradientMax = gradient;
        }

        public MinMaxGradient(Gradient min, Gradient max)
        {
            mode = ParticleSystemGradientMode.TwoGradients;
            color = Color.white;
            colorMin = Color.white;
            colorMax = Color.white;
            gradient = min;
            gradientMin = min;
            gradientMax = max;
        }

        public Color Evaluate(float time)
        {
            return Evaluate(time, UnityEngine.Random.value);
        }

        public Color Evaluate(float time, float randomFactor)
        {
            switch (mode)
            {
                case ParticleSystemGradientMode.Color:
                    return color;
                case ParticleSystemGradientMode.TwoColors:
                    return Color.Lerp(colorMin, colorMax, randomFactor);
                case ParticleSystemGradientMode.Gradient:
                    return gradient != null ? gradient.Evaluate(time) : color;
                case ParticleSystemGradientMode.TwoGradients:
                    Gradient g = randomFactor < 0.5f ? gradientMin : gradientMax;
                    return g != null ? g.Evaluate(time) : color;
                default:
                    return color;
            }
        }

        public static implicit operator MinMaxGradient(Color color) => new MinMaxGradient(color);
    }

    [Serializable]
    public struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 animatedVelocity;
        public Vector3 totalVelocity;
        public float startSize;
        public Vector3 startSize3D;
        public float rotation;
        public Vector3 rotation3D;
        public float angularVelocity;
        public float rotationVelocity;
        public Vector3 angularVelocity3D;
        public Color32 startColor;
        public float remainingLifetime;
        public float startLifetime;
        public ulong randomSeed;
        public int meshIndex;
        public int seed;
        public float lifetime;
        public float startSpeed;
        public float startRotation;
        public Vector3 axisOfRotation;
        public float radialVelocity;
        public Vector3 scale;
        public Vector3 totalSize3D;

        public Color GetCurrentColor(ParticleSystem system)
        {
            _ = system;
            return startColor;
        }

        public float GetCurrentSize(ParticleSystem system)
        {
            _ = system;
            return startSize;
        }

        public Vector3 GetCurrentSize3D(ParticleSystem system)
        {
            _ = system;
            return startSize3D != Vector3.zero ? startSize3D : Vector3.one * startSize;
        }
    }

    [Serializable]
    public struct Burst
    {
        public float time;
        public short minCount;
        public short maxCount;
        public int cycleCount;
        public float repeatInterval;
        public float probability;

        public Burst(float time, short count)
        {
            this.time = time;
            minCount = count;
            maxCount = count;
            cycleCount = 0;
            repeatInterval = 0.01f;
            probability = 1f;
        }

        public Burst(float time, int count)
        {
            this.time = time;
            minCount = (short)count;
            maxCount = (short)count;
            cycleCount = 0;
            repeatInterval = 0.01f;
            probability = 1f;
        }

        public Burst(float time, short minCount, short maxCount)
        {
            this.time = time;
            this.minCount = minCount;
            this.maxCount = maxCount;
            cycleCount = 0;
            repeatInterval = 0.01f;
            probability = 1f;
        }

        public Burst(float time, int minCount, int maxCount)
        {
            this.time = time;
            this.minCount = (short)minCount;
            this.maxCount = (short)maxCount;
            cycleCount = 0;
            repeatInterval = 0.01f;
            probability = 1f;
        }

        public Burst(float time, int count, int cycles, float interval)
        {
            this.time = time;
            minCount = (short)count;
            maxCount = (short)count;
            cycleCount = cycles;
            repeatInterval = interval;
            probability = 1f;
        }

        public Burst(float time, short minCount, short maxCount, int cycles, float interval)
        {
            this.time = time;
            this.minCount = minCount;
            this.maxCount = maxCount;
            cycleCount = cycles;
            repeatInterval = interval;
            probability = 1f;
        }
    }

    [Serializable]
    public struct EmitParams
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 axisOfRotation;
        public float startLifetime;
        public float startSpeed;
        public float startSize;
        public float startSizeY;
        public float startSizeZ;
        public float startRotation;
        public float startRotationY;
        public float startRotationZ;
        public float randomSeed;
        public Color32 startColor;
        public bool applyShapeToPosition;

        public void Reset()
        {
            position = Vector3.zero;
            velocity = Vector3.zero;
            axisOfRotation = Vector3.up;
            startLifetime = 0f;
            startSpeed = 0f;
            startSize = 0f;
            startSizeY = 0f;
            startSizeZ = 0f;
            startRotation = 0f;
            startRotationY = 0f;
            startRotationZ = 0f;
            randomSeed = 0f;
            startColor = new Color32(255, 255, 255, 255);
            applyShapeToPosition = false;
        }
    }
}
