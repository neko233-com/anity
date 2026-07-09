using System;

namespace UnityEngine;

/// <summary>
/// Unity ParticleSystem component.
/// </summary>
public class ParticleSystem : Component
{
    private bool _isPlaying;
    private bool _isStopped;
    private bool _isPaused;
    private bool _loop = true;
    private bool _playOnAwake = true;
    private float _duration = 5.0f;
    private float _startLifetime = 5.0f;
    private float _startSpeed = 5.0f;
    private float _startSize = 1.0f;
    private float _startRotation;
    private Color _startColor = Color.white;
    private float _gravityModifier;
    private float _simulationSpeed = 1.0f;
    private bool _playbackBehaviourEnabled = true;
    private int _maxParticles = 1000;
    private ParticleSystemMainModule _main;
    private ParticleSystemEmissionModule _emission;
    private ParticleSystemShapeModule _shape;

    public ParticleSystem()
    {
        _main = default;
        _emission = default;
        _shape = default;
    }

    public bool isPlaying => _isPlaying;
    public bool isStopped => _isStopped;
    public bool isPaused => _isPaused;
    public bool isEmitting { get; }
    public int particleCount { get; }
    public float time { get; set; }

    public ParticleSystemMainModule main => _main;
    public ParticleSystemEmissionModule emission => _emission;
    public ParticleSystemShapeModule shape => _shape;

    public void Play() { _isPlaying = true; _isStopped = false; }
    public void Play(bool withChildren) { Play(); }
    public void Stop() { _isPlaying = false; _isStopped = true; }
    public void Stop(bool withChildren) { Stop(); }
    public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior) { Stop(); }
    public void Pause() { _isPaused = true; }
    public void Pause(bool withChildren) { Pause(); }
    public void Resume() { _isPaused = false; }
    public void Resume(bool withChildren) { Resume(); }
    public void Clear() { }
    public void Clear(bool withChildren) { }
    public void Simulate(float t, bool withChildren = true, bool restart = true) { }
    public bool IsAlive() => _isPlaying || !_isStopped;
    public bool IsAlive(bool withChildren) => IsAlive();
    public void Emit(int count) { }
    public void Emit(Particle particle) { }
    public int GetParticles(Particle[] particles) => 0;
    public int GetParticles(Particle[] particles, int size) => 0;
    public void SetParticles(Particle[] particles) { }
    public void SetParticles(Particle[] particles, int size) { }
    public ParticleSystemRandomInitOptions randomSeed { get; set; }
    public bool useAutoRandomSeed { get; set; }

    public void Simulate(float t) { }

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
    }
}

/// <summary>
/// Particle system stop behavior.
/// </summary>
public enum ParticleSystemStopBehavior
{
    StopEmittingAndClear,
    StopEmitting
}

/// <summary>
/// Particle system random init options.
/// </summary>
public enum ParticleSystemRandomInitOptions
{
    None,
    RestartSeed
}

/// <summary>
/// Main module of ParticleSystem.
/// </summary>
public struct ParticleSystemMainModule
{
    public float duration { get; set; }
    public bool loop { get; set; }
    public bool playOnAwake { get; set; }
    public float startLifetime { get; set; }
    public float startSpeed { get; set; }
    public float startSize { get; set; }
    public float startRotation { get; set; }
    public Color startColor { get; set; }
    public float gravityModifier { get; set; }
    public float simulationSpeed { get; set; }
    public int maxParticles { get; set; }
}

/// <summary>
/// Emission module of ParticleSystem.
/// </summary>
public struct ParticleSystemEmissionModule
{
    public bool enabled { get; set; }
    public float rateOverTime { get; set; }
    public float rateOverDistance { get; set; }
}

/// <summary>
/// Shape type for ParticleSystem.
/// </summary>
public enum ParticleSystemShapeType
{
    Sphere,
    Hemisphere,
    Cone,
    Box,
    Mesh,
    MeshRenderer,
    SkinnedMeshRenderer,
    Circle,
    SingleSidedEdge,
    BoxEdge,
    Donut,
    RectangleEdge,
    UniformBox
}

/// <summary>
/// Shape module of ParticleSystem.
/// </summary>
public struct ParticleSystemShapeModule
{
    public bool enabled { get; set; }
    public ParticleSystemShapeType shapeType { get; set; }
    public float radius { get; set; }
    public float angle { get; set; }
    public Vector3 position { get; set; }
    public Vector3 rotation { get; set; }
    public Vector3 scale { get; set; }
}

/// <summary>
/// Burst definition for ParticleSystem emission.
/// </summary>
[Serializable]
public struct ParticleSystemBurst
{
    public float time;
    public int count;
    public short minCount;
    public short maxCount;
    public float cycleCount;
    public float repeatInterval;
    public float probability;

    public ParticleSystemBurst(float time, short minCount, short maxCount)
    {
        this.time = time;
        this.minCount = minCount;
        this.maxCount = maxCount;
        count = 0;
        cycleCount = 0;
        repeatInterval = 0;
        probability = 1;
    }
}
