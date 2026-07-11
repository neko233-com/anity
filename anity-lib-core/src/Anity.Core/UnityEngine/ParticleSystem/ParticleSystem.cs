using System;
using System.Collections.Generic;

namespace UnityEngine;

[RequireComponent(typeof(Transform))]
public partial class ParticleSystem : Component
{
    private bool _isPlaying;
    private bool _isPaused;
    private bool _isStopped = true;
    private float _time;
    private readonly List<Particle> _particles = new();
    private float _emitAccumulator;

    public MainModule main { get; private set; }
    public EmissionModule emission { get; private set; }
    public ShapeModule shape { get; private set; }
    public VelocityOverLifetimeModule velocityOverLifetime { get; private set; }
    public ColorOverLifetimeModule colorOverLifetime { get; private set; }
    public SizeOverLifetimeModule sizeOverLifetime { get; private set; }
    public ForceOverLifetimeModule forceOverLifetime { get; private set; }
    public CollisionModule collision { get; private set; }
    public TriggerModule trigger { get; private set; }
    public SubEmittersModule subEmitters { get; private set; }
    public TextureSheetAnimationModule textureSheetAnimation { get; private set; }
    public NoiseModule noise { get; private set; }
    public TrailModule trails { get; private set; }
    public ColorBySpeedModule colorBySpeed { get; private set; }
    public SizeBySpeedModule sizeBySpeed { get; private set; }
    public RotationBySpeedModule rotationBySpeed { get; private set; }
    public InheritVelocityModule inheritVelocity { get; private set; }
    public LimitVelocityOverLifetimeModule limitVelocityOverLifetime { get; private set; }

    public bool isPlaying => _isPlaying;
    public bool isPaused => _isPaused;
    public bool isStopped => _isStopped;
    public bool isEmitting { get; set; }
    public int particleCount => _particles.Count;
    public float time => _time;
    public bool useAutoRandomSeed { get; set; } = true;
    public int randomSeed { get; set; }
    public bool playOnAwake { get; set; } = true;

    public ParticleSystem()
    {
        main = new MainModule(this);
        emission = new EmissionModule(this);
        shape = new ShapeModule(this);
        velocityOverLifetime = new VelocityOverLifetimeModule(this);
        colorOverLifetime = new ColorOverLifetimeModule(this);
        sizeOverLifetime = new SizeOverLifetimeModule(this);
        forceOverLifetime = new ForceOverLifetimeModule(this);
        collision = new CollisionModule(this);
        trigger = new TriggerModule(this);
        subEmitters = new SubEmittersModule(this);
        textureSheetAnimation = new TextureSheetAnimationModule(this);
        noise = new NoiseModule(this);
        trails = new TrailModule(this);
        colorBySpeed = new ColorBySpeedModule(this);
        sizeBySpeed = new SizeBySpeedModule(this);
        rotationBySpeed = new RotationBySpeedModule(this);
        inheritVelocity = new InheritVelocityModule(this);
        limitVelocityOverLifetime = new LimitVelocityOverLifetimeModule(this);
    }

    public void Play()
    {
        _isPlaying = true;
        _isPaused = false;
        _isStopped = false;
        isEmitting = true;
    }

    public void Pause()
    {
        _isPaused = true;
        _isPlaying = false;
    }

    public void Stop()
    {
        Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void Stop(bool stopChildren, ParticleSystemStopBehavior behavior)
    {
        _ = stopChildren;
        _ = behavior;
        _isPlaying = false;
        _isPaused = false;
        _isStopped = true;
        isEmitting = false;
    }

    public void Clear()
    {
        Clear(true);
    }

    public void Clear(bool clearChildren)
    {
        _ = clearChildren;
        _particles.Clear();
        _time = 0f;
        _emitAccumulator = 0f;
    }

    public bool IsAlive()
    {
        return IsAlive(true);
    }

    public bool IsAlive(bool aliveChildren)
    {
        _ = aliveChildren;
        return _isPlaying || _particles.Count > 0;
    }

    public void Emit(int count)
    {
        for (int i = 0; i < count; i++)
        {
            EmitParticle(default);
        }
    }

    public void Emit(ParticleSystem.EmitParams emitParams, int count)
    {
        for (int i = 0; i < count; i++)
        {
            EmitParticle(emitParams);
        }
    }

    public void Simulate(float t)
    {
        Simulate(t, true, false);
    }

    public void Simulate(float t, bool withChildren)
    {
        Simulate(t, withChildren, false);
    }

    public void Simulate(float t, bool withChildren, bool restart)
    {
        _ = withChildren;
        if (restart)
        {
            _time = 0f;
        }
        float deltaTime = t - _time;
        if (deltaTime > 0)
        {
            bool wasPlaying = _isPlaying;
            _isPlaying = true;
            InternalSimulate(deltaTime);
            if (!wasPlaying && !_isPaused)
            {
                _isPlaying = false;
                _isStopped = true;
            }
        }
        _time = t;
    }

    private void InternalSimulate(float deltaTime)
    {
        if (main.loop)
        {
            while (_time >= main.duration)
            {
                _time -= main.duration;
            }
        }

        if (isEmitting && emission.enabled)
        {
            float rate = emission.rateOverTime.Evaluate(_time);
            _emitAccumulator += rate * deltaTime;
            while (_emitAccumulator >= 1f && _particles.Count < main.maxParticles)
            {
                EmitParticle(default);
                _emitAccumulator -= 1f;
            }

            foreach (var burst in emission.GetBursts())
            {
                if (_time >= burst.time && _time - deltaTime < burst.time)
                {
                    for (int i = 0; i < burst.count && _particles.Count < main.maxParticles; i++)
                    {
                        EmitParticle(default);
                    }
                }
            }
        }

        float normalizedLifetime = 0f;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.remainingLifetime -= deltaTime;
            if (p.remainingLifetime <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            normalizedLifetime = p.startLifetime > 0f ? 1f - (p.remainingLifetime / p.startLifetime) : 0f;

            Vector3 velocity = p.velocity;
            float gravity = main.gravityModifier.Evaluate(normalizedLifetime);
            velocity.y -= gravity * 9.81f * deltaTime;

            if (forceOverLifetime.enabled)
            {
                velocity += new Vector3(
                    forceOverLifetime.x.Evaluate(normalizedLifetime),
                    forceOverLifetime.y.Evaluate(normalizedLifetime),
                    forceOverLifetime.z.Evaluate(normalizedLifetime)) * deltaTime;
            }

            if (velocityOverLifetime.enabled)
            {
                velocity += new Vector3(
                    velocityOverLifetime.x.Evaluate(normalizedLifetime),
                    velocityOverLifetime.y.Evaluate(normalizedLifetime),
                    velocityOverLifetime.z.Evaluate(normalizedLifetime)) * deltaTime;
            }

            p.velocity = velocity;
            p.position += velocity * deltaTime;
            _particles[i] = p;
        }
    }

    private void EmitParticle(EmitParams @params)
    {
        if (_particles.Count >= main.maxParticles) return;

        bool useDefaults = @params.startLifetime <= 0f;
        float random = UnityEngine.Random.value;
        float lifetime = useDefaults ? main.startLifetime.Evaluate(random) : @params.startLifetime;
        float size = useDefaults ? main.startSize.Evaluate(random) : @params.startSize;
        float speed = useDefaults ? main.startSpeed.Evaluate(random) : @params.startSpeed;
        Color color = main.startColor.Evaluate(0f);
        float rotation = useDefaults ? main.startRotation.Evaluate(random) : @params.startRotation;
        Vector3 pos = useDefaults ? transform.position : @params.position;
        Vector3 vel = useDefaults ? UnityEngine.Random.insideUnitSphere * speed : @params.velocity;

        if (@params.applyShapeToPosition && shape.enabled)
        {
            pos += UnityEngine.Random.insideUnitSphere * shape.radius;
        }

        var particle = new Particle
        {
            position = pos,
            velocity = vel,
            startSize = size,
            startColor = useDefaults ? new Color32((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), (byte)(color.a * 255)) : @params.startColor,
            startLifetime = lifetime,
            remainingLifetime = lifetime,
            rotation = rotation * Mathf.Rad2Deg,
            startRotation = rotation,
            startSpeed = speed,
            lifetime = lifetime,
            scale = Vector3.one * size,
            totalSize3D = Vector3.one * size,
            startSize3D = Vector3.one * size,
            axisOfRotation = @params.axisOfRotation != Vector3.zero ? @params.axisOfRotation : Vector3.up,
            seed = @params.randomSeed > 0f ? (uint)@params.randomSeed : (uint)UnityEngine.Random.Range(0, int.MaxValue),
            meshIndex = 0,
            angularVelocity = 0f,
            rotationVelocity = 0f,
            radialVelocity = 0f,
            animatedVelocity = Vector3.zero,
            rotation3D = new Vector3(0f, 0f, rotation),
            angularVelocity3D = Vector3.zero,
        };
        _particles.Add(particle);
    }

    public int GetParticles(Particle[] particles)
    {
        int count = Math.Min(particles.Length, _particles.Count);
        for (int i = 0; i < count; i++)
        {
            particles[i] = _particles[i];
        }
        return count;
    }

    public int GetParticles(List<Particle> particles)
    {
        particles.Clear();
        particles.AddRange(_particles);
        return particles.Count;
    }

    public void SetParticles(Particle[] particles, int size)
    {
        _particles.Clear();
        for (int i = 0; i < size && i < particles.Length; i++)
        {
            _particles.Add(particles[i]);
        }
    }

    public void SetParticles(List<Particle> particles, int size)
    {
        _particles.Clear();
        for (int i = 0; i < size && i < particles.Count; i++)
        {
            _particles.Add(particles[i]);
        }
    }
}
