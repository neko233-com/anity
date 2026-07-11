using System;
using System.Collections.Generic;

namespace UnityEngine;

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

    public float Evaluate(float time)
    {
        switch (mode)
        {
            case ParticleSystemCurveMode.Constant:
                return constant * curveMultiplier;
            case ParticleSystemCurveMode.TwoConstants:
                return Random.Range(constantMin, constantMax) * curveMultiplier;
            case ParticleSystemCurveMode.Curve:
                return curve.Evaluate(time) * curveMultiplier;
            case ParticleSystemCurveMode.TwoCurves:
                return curveMax.Evaluate(time) * curveMultiplier;
            default:
                return constant * curveMultiplier;
        }
    }

    public static implicit operator MinMaxCurve(float value) => new MinMaxCurve(value);
}

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

    public Color Evaluate(float time)
    {
        switch (mode)
        {
            case ParticleSystemGradientMode.Color:
                return color;
            case ParticleSystemGradientMode.TwoColors:
                return Color.Lerp(colorMin, colorMax, Random.value);
            case ParticleSystemGradientMode.Gradient:
                return gradient.Evaluate(time);
            case ParticleSystemGradientMode.TwoGradients:
                return gradientMax.Evaluate(time);
            default:
                return color;
        }
    }

    public static implicit operator MinMaxGradient(Color color) => new MinMaxGradient(color);
}

public struct ParticleSystemBurst
{
    public float time;
    public short count;
    public short countMin;
    public short cycleCount;
    public float repeatInterval;

    public ParticleSystemBurst(float time, short count)
    {
        this.time = time;
        this.count = count;
        countMin = count;
        cycleCount = 0;
        repeatInterval = 0.01f;
    }
}

public class MainModule
{
    private readonly ParticleSystem _ps;
    public MainModule(ParticleSystem ps) { _ps = ps; }
    public float duration { get; set; } = 5f;
    public bool loop { get; set; }
    public bool playOnAwake { get; set; } = true;
    public bool prewarm { get; set; }
    public MinMaxCurve startDelay { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve startLifetime { get; set; } = new MinMaxCurve(5f);
    public MinMaxCurve startSpeed { get; set; } = new MinMaxCurve(5f);
    public MinMaxCurve startSize { get; set; } = new MinMaxCurve(1f);
    public MinMaxGradient startColor { get; set; } = Color.white;
    public MinMaxCurve startRotation { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve gravityModifier { get; set; } = new MinMaxCurve(0f);
    public ParticleSystemSimulationSpace simulationSpace { get; set; }
    public float simulationSpeed { get; set; } = 1f;
    public ParticleSystemScalingMode scalingMode { get; set; }
    public int maxParticles { get; set; } = 1000;
    public ParticleSystemGravitySimulation gravitySimulation { get; set; } = ParticleSystemGravitySimulation.Physics2D;
}

public class EmissionModule
{
    private readonly ParticleSystem _ps;
    private readonly List<ParticleSystemBurst> _bursts = new();
    public EmissionModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve rateOverTime { get; set; } = new MinMaxCurve(10f);
    public MinMaxCurve rateOverDistance { get; set; } = new MinMaxCurve(0f);
    public int burstCount => _bursts.Count;
    public void SetBursts(ParticleSystemBurst[] bursts) { _bursts.Clear(); if (bursts != null) _bursts.AddRange(bursts); }
    public void SetBursts(List<ParticleSystemBurst> bursts) { _bursts.Clear(); if (bursts != null) _bursts.AddRange(bursts); }
    public void AddBurst(ParticleSystemBurst burst) { _bursts.Add(burst); }
    public ParticleSystemBurst[] GetBursts() { return _bursts.ToArray(); }
}

public class ShapeModule
{
    private readonly ParticleSystem _ps;
    public ShapeModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public ParticleSystemShapeType shapeType { get; set; } = ParticleSystemShapeType.Cone;
    public float radius { get; set; } = 1f;
    public float angle { get; set; } = 25f;
    public float length { get; set; } = 5f;
}

public class VelocityOverLifetimeModule
{
    private readonly ParticleSystem _ps;
    public VelocityOverLifetimeModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve x { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve y { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve z { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve orbitalX { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve orbitalY { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve orbitalZ { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve radial { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve speedModifier { get; set; } = new MinMaxCurve(1f);
}

public class ColorOverLifetimeModule
{
    private readonly ParticleSystem _ps;
    public ColorOverLifetimeModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxGradient color { get; set; } = Color.white;
}

public class SizeOverLifetimeModule
{
    private readonly ParticleSystem _ps;
    public SizeOverLifetimeModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve size { get; set; } = new MinMaxCurve(1f);
}

public class ForceOverLifetimeModule
{
    private readonly ParticleSystem _ps;
    public ForceOverLifetimeModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve x { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve y { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve z { get; set; } = new MinMaxCurve(0f);
}
