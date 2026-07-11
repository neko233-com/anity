using System;

namespace UnityEngine;

public partial class ParticleSystem
{
    [Serializable]
    public struct MinMaxCurve
    {
        public ParticleSystemCurveMode mode;
        public float constantMin;
        public float constantMax;
        public AnimationCurve curveMin;
        public AnimationCurve curveMax;
        public float constant;

        public static MinMaxCurve Curve(float constant)
        {
            return new MinMaxCurve(constant);
        }

        public static MinMaxCurve Curve(float constant, AnimationCurve curve)
        {
            return new MinMaxCurve(constant, curve);
        }

        public static MinMaxCurve Curve(float min, float max)
        {
            return new MinMaxCurve(min, max);
        }

        public static MinMaxCurve Curve(float min, float max, AnimationCurve curveMin, AnimationCurve curveMax)
        {
            return new MinMaxCurve(min, max, curveMin, curveMax);
        }

        public MinMaxCurve(float constant)
        {
            mode = ParticleSystemCurveMode.Constant;
            this.constantMin = constant;
            this.constantMax = constant;
            this.constant = constant;
            curveMin = null;
            curveMax = null;
        }

        public MinMaxCurve(float constant, AnimationCurve curve)
        {
            mode = ParticleSystemCurveMode.Curve;
            this.constantMin = constant;
            this.constantMax = constant;
            this.constant = constant;
            curveMin = curve;
            curveMax = curve;
        }

        public MinMaxCurve(float min, float max)
        {
            mode = ParticleSystemCurveMode.TwoConstants;
            constantMin = min;
            constantMax = max;
            constant = (min + max) * 0.5f;
            curveMin = null;
            curveMax = null;
        }

        public MinMaxCurve(float min, float max, AnimationCurve curveMin, AnimationCurve curveMax)
        {
            mode = ParticleSystemCurveMode.TwoCurves;
            constantMin = min;
            constantMax = max;
            constant = (min + max) * 0.5f;
            this.curveMin = curveMin;
            this.curveMax = curveMax;
        }

        public float Evaluate(float time, float randomFactor)
        {
            switch (mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return constantMin;
                case ParticleSystemCurveMode.Curve:
                    return curveMin != null ? curveMin.Evaluate(time) : constantMin;
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Lerp(constantMin, constantMax, randomFactor);
                case ParticleSystemCurveMode.TwoCurves:
                    float minVal = curveMin != null ? curveMin.Evaluate(time) : constantMin;
                    float maxVal = curveMax != null ? curveMax.Evaluate(time) : constantMax;
                    return Mathf.Lerp(minVal, maxVal, randomFactor);
                default:
                    return constantMin;
            }
        }

        public float Evaluate(float time)
        {
            return Evaluate(time, 0f);
        }
    }

    [Serializable]
    public struct MinMaxGradient
    {
        public ParticleSystemGradientMode mode;
        public Color colorMin;
        public Color colorMax;
        public Gradient gradientMin;
        public Gradient gradientMax;

        public static MinMaxGradient Gradient(Gradient gradient)
        {
            return new MinMaxGradient(gradient);
        }

        public static MinMaxGradient Gradient(Color color)
        {
            return new MinMaxGradient(color);
        }

        public static MinMaxGradient Gradient(Color min, Color max)
        {
            return new MinMaxGradient(min, max);
        }

        public static MinMaxGradient Gradient(Gradient min, Gradient max)
        {
            return new MinMaxGradient(min, max);
        }

        public MinMaxGradient(Color color)
        {
            mode = ParticleSystemGradientMode.Color;
            colorMin = color;
            colorMax = color;
            gradientMin = null;
            gradientMax = null;
        }

        public MinMaxGradient(Gradient gradient)
        {
            mode = ParticleSystemGradientMode.Gradient;
            colorMin = Color.white;
            colorMax = Color.white;
            gradientMin = gradient;
            gradientMax = gradient;
        }

        public MinMaxGradient(Color min, Color max)
        {
            mode = ParticleSystemGradientMode.TwoColors;
            colorMin = min;
            colorMax = max;
            gradientMin = null;
            gradientMax = null;
        }

        public MinMaxGradient(Gradient min, Gradient max)
        {
            mode = ParticleSystemGradientMode.RandomColor;
            colorMin = Color.white;
            colorMax = Color.white;
            gradientMin = min;
            gradientMax = max;
        }

        public Color Evaluate(float time, float randomFactor)
        {
            switch (mode)
            {
                case ParticleSystemGradientMode.Color:
                    return colorMin;
                case ParticleSystemGradientMode.Gradient:
                    return gradientMin != null ? gradientMin.Evaluate(time) : colorMin;
                case ParticleSystemGradientMode.TwoColors:
                    return Color.Lerp(colorMin, colorMax, randomFactor);
                case ParticleSystemGradientMode.RandomColor:
                    Gradient g = randomFactor < 0.5f ? gradientMin : gradientMax;
                    return g != null ? g.Evaluate(time) : colorMin;
                default:
                    return colorMin;
            }
        }

        public Color Evaluate(float time)
        {
            return Evaluate(time, 0f);
        }
    }

    [Serializable]
    public struct Particle
    {
        public float lifetime;
        public float startLifetime;
        public float startSize;
        public float startSpeed;
        public float startRotation;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 animatedVelocity;
        public Vector3 axisOfRotation;
        public float rotation;
        public Vector3 scale;
        public Color32 startColor;
        public uint seed;
        public float remainingLifetime;
        public int meshIndex;
        public float angularVelocity;
        public float rotationVelocity;
        public float radialVelocity;
        public Vector3 totalSize3D;
        public Vector3 startSize3D;
        public Vector3 rotation3D;
        public Vector3 angularVelocity3D;
    }

    [Serializable]
    public struct Burst
    {
        public float time;
        public int count;
        public short minCount;
        public short maxCount;
        public int cycles;
        public float interval;
        public float probability;

        public Burst(float time, int count)
        {
            this.time = time;
            this.count = count;
            this.minCount = (short)count;
            this.maxCount = (short)count;
            this.cycles = 1;
            this.interval = 0f;
            this.probability = 1f;
        }

        public Burst(float time, short minCount, short maxCount)
        {
            this.time = time;
            this.count = 0;
            this.minCount = minCount;
            this.maxCount = maxCount;
            this.cycles = 1;
            this.interval = 0f;
            this.probability = 1f;
        }

        public Burst(float time, int count, int cycles, float interval)
        {
            this.time = time;
            this.count = count;
            this.minCount = (short)count;
            this.maxCount = (short)count;
            this.cycles = cycles;
            this.interval = interval;
            this.probability = 1f;
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