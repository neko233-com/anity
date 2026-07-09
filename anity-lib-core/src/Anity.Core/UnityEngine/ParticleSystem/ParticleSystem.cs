using System;

namespace UnityEngine;

[RequireComponent(typeof(Transform))]
public partial class ParticleSystem : Component
{
    private bool _isPlaying;
    private bool _isStopped = true;
    private bool _isPaused;
    private float _time;
    private int _randomSeed;
    private bool _useAutoRandomSeed = true;
    private ParticleSystemRandomInitOptions _randomInitOptions;

    private MainModule _main;
    private EmissionModule _emission;
    private ShapeModule _shape;
    private VelocityOverLifetimeModule _velocityOverLifetime;
    private LimitVelocityOverLifetimeModule _limitVelocityOverLifetime;
    private InheritVelocityModule _inheritVelocity;
    private ForceOverLifetimeModule _forceOverLifetime;
    private ColorOverLifetimeModule _colorOverLifetime;
    private ColorBySpeedModule _colorBySpeed;
    private SizeOverLifetimeModule _sizeOverLifetime;
    private SizeBySpeedModule _sizeBySpeed;
    private RotationOverLifetimeModule _rotationOverLifetime;
    private RotationBySpeedModule _rotationBySpeed;
    private ExternalForcesModule _externalForces;
    private NoiseModule _noise;
    private CollisionModule _collision;
    private TriggerModule _trigger;
    private SubEmittersModule _subEmitters;
    private TextureSheetAnimationModule _textureSheetAnimation;
    private LightsModule _lights;
    private TrailModule _trail;
    private CustomDataModule _customData;

    public ParticleSystem()
    {
        _main = new MainModule(this);
        _emission = new EmissionModule(this);
        _shape = new ShapeModule(this);
        _velocityOverLifetime = new VelocityOverLifetimeModule(this);
        _limitVelocityOverLifetime = new LimitVelocityOverLifetimeModule(this);
        _inheritVelocity = new InheritVelocityModule(this);
        _forceOverLifetime = new ForceOverLifetimeModule(this);
        _colorOverLifetime = new ColorOverLifetimeModule(this);
        _colorBySpeed = new ColorBySpeedModule(this);
        _sizeOverLifetime = new SizeOverLifetimeModule(this);
        _sizeBySpeed = new SizeBySpeedModule(this);
        _rotationOverLifetime = new RotationOverLifetimeModule(this);
        _rotationBySpeed = new RotationBySpeedModule(this);
        _externalForces = new ExternalForcesModule(this);
        _noise = new NoiseModule(this);
        _collision = new CollisionModule(this);
        _trigger = new TriggerModule(this);
        _subEmitters = new SubEmittersModule(this);
        _textureSheetAnimation = new TextureSheetAnimationModule(this);
        _lights = new LightsModule(this);
        _trail = new TrailModule(this);
        _customData = new CustomDataModule(this);
    }

    public bool isPlaying => _isPlaying;
    public bool isStopped => _isStopped;
    public bool isPaused => _isPaused;
    public bool isEmitting { get; internal set; }
    public int particleCount { get; internal set; }
    public float time { get => _time; set => _time = value; }
    public float realtime { get; internal set; }
    public bool isSendingMessages { get; set; }

    public ParticleSystemRandomInitOptions randomSeed
    {
        get => _randomInitOptions;
        set => _randomInitOptions = value;
    }

    public bool useAutoRandomSeed
    {
        get => _useAutoRandomSeed;
        set => _useAutoRandomSeed = value;
    }

    public MainModule main => _main;
    public EmissionModule emission => _emission;
    public ShapeModule shape => _shape;
    public VelocityOverLifetimeModule velocityOverLifetime => _velocityOverLifetime;
    public LimitVelocityOverLifetimeModule limitVelocityOverLifetime => _limitVelocityOverLifetime;
    public InheritVelocityModule inheritVelocity => _inheritVelocity;
    public ForceOverLifetimeModule forceOverLifetime => _forceOverLifetime;
    public ColorOverLifetimeModule colorOverLifetime => _colorOverLifetime;
    public ColorBySpeedModule colorBySpeed => _colorBySpeed;
    public SizeOverLifetimeModule sizeOverLifetime => _sizeOverLifetime;
    public SizeBySpeedModule sizeBySpeed => _sizeBySpeed;
    public RotationOverLifetimeModule rotationOverLifetime => _rotationOverLifetime;
    public RotationBySpeedModule rotationBySpeed => _rotationBySpeed;
    public ExternalForcesModule externalForces => _externalForces;
    public NoiseModule noise => _noise;
    public CollisionModule collision => _collision;
    public TriggerModule trigger => _trigger;
    public SubEmittersModule subEmitters => _subEmitters;
    public TextureSheetAnimationModule textureSheetAnimation => _textureSheetAnimation;
    public LightsModule lights => _lights;
    public TrailModule trails => _trail;
    public CustomDataModule customData => _customData;

    public void Play()
    {
        Play(true);
    }

    public void Play(bool withChildren)
    {
        _isPlaying = true;
        _isStopped = false;
        _isPaused = false;
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play(true);
            }
        }
    }

    public void Stop()
    {
        Stop(true);
    }

    public void Stop(bool withChildren)
    {
        Stop(withChildren, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior)
    {
        _isPlaying = false;
        _isStopped = true;
        _isPaused = false;
        if (stopBehavior == ParticleSystemStopBehavior.StopEmittingAndClear)
        {
            particleCount = 0;
        }
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop(true, stopBehavior);
            }
        }
    }

    public void Pause()
    {
        Pause(true);
    }

    public void Pause(bool withChildren)
    {
        _isPaused = true;
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Pause(true);
            }
        }
    }

    public void Resume()
    {
        Resume(true);
    }

    public void Resume(bool withChildren)
    {
        _isPaused = false;
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Resume(true);
            }
        }
    }

    public void Clear()
    {
        Clear(true);
    }

    public void Clear(bool withChildren)
    {
        particleCount = 0;
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Clear(true);
            }
        }
    }

    public void Simulate(float t)
    {
        Simulate(t, true, true);
    }

    public void Simulate(float t, bool withChildren)
    {
        Simulate(t, withChildren, true);
    }

    public void Simulate(float t, bool withChildren, bool restart)
    {
        Simulate(t, withChildren, restart, true);
    }

    public void Simulate(float t, bool withChildren, bool restart, bool fixedTimeStep)
    {
        _ = fixedTimeStep;
        if (restart)
        {
            _time = 0f;
            particleCount = 0;
            _isStopped = false;
        }
        _time += t;
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Simulate(t, true, restart, fixedTimeStep);
            }
        }
    }

    public bool IsAlive()
    {
        return IsAlive(true);
    }

    public bool IsAlive(bool withChildren)
    {
        if (!_isStopped || particleCount > 0)
            return true;
        if (withChildren)
        {
            foreach (Transform child in transform)
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null && ps.IsAlive(true))
                    return true;
            }
        }
        return false;
    }

    public void Emit(int count)
    {
        particleCount += count;
        if (particleCount > _main.maxParticles)
            particleCount = _main.maxParticles;
    }

    public void Emit(Particle particle)
    {
        particleCount++;
        if (particleCount > _main.maxParticles)
            particleCount = _main.maxParticles;
    }

    public int GetParticles(Particle[] particles)
    {
        return GetParticles(particles, particles.Length);
    }

    public int GetParticles(Particle[] particles, int size)
    {
        int count = Math.Min(size, particleCount);
        for (int i = 0; i < count; i++)
        {
            particles[i] = new Particle
            {
                remainingLifetime = _main.startLifetime.constant * (1f - (float)i / count),
                startLifetime = _main.startLifetime.constant,
                startSize = _main.startSize.constant,
                startSpeed = _main.startSpeed.constant,
                startColor = _main.startColor.colorMin,
                position = Vector3.zero,
                velocity = Vector3.zero
            };
        }
        return count;
    }

    public void SetParticles(Particle[] particles)
    {
        SetParticles(particles, particles.Length);
    }

    public void SetParticles(Particle[] particles, int size)
    {
        particleCount = size;
    }

    public ParticleSystem[] GetChildParticleSystems()
    {
        return GetChildParticleSystems(true);
    }

    public ParticleSystem[] GetChildParticleSystems(bool includeInactive)
    {
        var children = transform.GetComponentsInChildren<ParticleSystem>(includeInactive);
        var result = new ParticleSystem[children.Length - 1];
        int index = 0;
        foreach (var ps in children)
        {
            if (ps != this)
                result[index++] = ps;
        }
        return result;
    }

    public void TriggerSubEmitter(int subEmitterIndex)
    {
        if (subEmitters.enabled && subEmitterIndex >= 0)
        {
        }
    }

    public void ResetSimulation()
    {
        _time = 0f;
        particleCount = 0;
    }

    public static void ClearAllParticles()
    {
    }
}