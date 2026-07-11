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
        _ = emitParams;
        Emit(count);
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
            InternalSimulate(deltaTime);
        }
        _time = t;
    }

    private void InternalSimulate(float deltaTime)
    {
        if (!_isPlaying && !_isPaused) return;

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

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.remainingLifetime -= deltaTime;
            if (p.remainingLifetime <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            Vector3 velocity = p.velocity;
            velocity.y -= main.gravityModifier.Evaluate(p.startLifetime - p.remainingLifetime) * Physics.gravity.magnitude * deltaTime;

            if (forceOverLifetime.enabled)
            {
                velocity += new Vector3(
                    forceOverLifetime.x.Evaluate(0f),
                    forceOverLifetime.y.Evaluate(0f),
                    forceOverLifetime.z.Evaluate(0f)) * deltaTime;
            }

            p.velocity = velocity;
            p.position += velocity * deltaTime;
            _particles[i] = p;
        }
    }

    private void EmitParticle(EmitParams @params)
    {
        var particle = new Particle
        {
            position = transform.position,
            velocity = UnityEngine.Random.insideUnitSphere * main.startSpeed.Evaluate(UnityEngine.Random.value),
            startSize = main.startSize.Evaluate(UnityEngine.Random.value),
            startColor = main.startColor.Evaluate(UnityEngine.Random.value),
            startLifetime = main.duration * UnityEngine.Random.value,
            remainingLifetime = main.duration * UnityEngine.Random.value,
            rotation = UnityEngine.Random.value * 360f,
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
