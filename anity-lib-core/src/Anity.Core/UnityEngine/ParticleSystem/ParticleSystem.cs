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
    public RotationOverLifetimeModule rotationOverLifetime { get; private set; }
    public ForceOverLifetimeModule forceOverLifetime { get; private set; }
    public CollisionModule collision { get; private set; }
    public TriggerModule trigger { get; private set; }
    public SubEmittersModule subEmitters { get; private set; }
    public TextureSheetAnimationModule textureSheetAnimation { get; private set; }
    public NoiseModule noise { get; private set; }
    public TrailModule trails { get; private set; }
    public LightsModule lights { get; private set; }
    public ColorBySpeedModule colorBySpeed { get; private set; }
    public SizeBySpeedModule sizeBySpeed { get; private set; }
    public RotationBySpeedModule rotationBySpeed { get; private set; }
    public InheritVelocityModule inheritVelocity { get; private set; }
    public LimitVelocityOverLifetimeModule limitVelocityOverLifetime { get; private set; }
    public ExternalForcesModule externalForces { get; private set; }
    public CustomDataModule customData { get; private set; }

    public bool isPlaying => _isPlaying;
    public bool isPaused => _isPaused;
    public bool isStopped => _isStopped;
    public bool isEmitting { get; set; }
    public int particleCount => _particles.Count;
    public float time => _time;
    public bool useAutoRandomSeed { get; set; } = true;
    public uint randomSeed { get; set; }
    public bool playOnAwake { get; set; } = true;

    public ParticleSystem()
    {
        main = new MainModule(this);
        emission = new EmissionModule(this);
        shape = new ShapeModule(this);
        velocityOverLifetime = new VelocityOverLifetimeModule(this);
        colorOverLifetime = new ColorOverLifetimeModule(this);
        sizeOverLifetime = new SizeOverLifetimeModule(this);
        rotationOverLifetime = new RotationOverLifetimeModule(this);
        forceOverLifetime = new ForceOverLifetimeModule(this);
        collision = new CollisionModule(this);
        trigger = new TriggerModule(this);
        subEmitters = new SubEmittersModule(this);
        textureSheetAnimation = new TextureSheetAnimationModule(this);
        noise = new NoiseModule(this);
        trails = new TrailModule(this);
        lights = new LightsModule(this);
        colorBySpeed = new ColorBySpeedModule(this);
        sizeBySpeed = new SizeBySpeedModule(this);
        rotationBySpeed = new RotationBySpeedModule(this);
        inheritVelocity = new InheritVelocityModule(this);
        limitVelocityOverLifetime = new LimitVelocityOverLifetimeModule(this);
        externalForces = new ExternalForcesModule(this);
        customData = new CustomDataModule(this);
    }

    public void Play()
    {
        Play(true);
    }

    public void Play(bool withChildren)
    {
        _ = withChildren;
        _isPlaying = true;
        _isPaused = false;
        _isStopped = false;
        isEmitting = true;
    }

    public void Pause()
    {
        Pause(true);
    }

    public void Pause(bool withChildren)
    {
        _ = withChildren;
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
        Emit(new EmitParams(), count);
    }

    public void Emit(Particle[] particles)
    {
        if (particles == null) return;
        foreach (var p in particles)
        {
            if (_particles.Count < main.maxParticles)
                _particles.Add(p);
        }
    }

    public void Emit(Vector3 position, Vector3 velocity, float size, float lifetime, Color32 color)
    {
        if (_particles.Count >= main.maxParticles) return;
        _particles.Add(new Particle
        {
            position = position,
            velocity = velocity,
            startSize = size,
            startSize3D = Vector3.one * size,
            startColor = color,
            startLifetime = lifetime,
            remainingLifetime = lifetime,
            randomSeed = (ulong)UnityEngine.Random.Range(0, int.MaxValue),
            rotation = 0f,
            rotation3D = Vector3.zero,
        });
    }

    public void Emit(EmitParams emitParams, int count)
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
                    int count = UnityEngine.Random.Range(burst.minCount, burst.maxCount + 1);
                    for (int i = 0; i < count && _particles.Count < main.maxParticles; i++)
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

            if (rotationOverLifetime.enabled)
            {
                p.angularVelocity = rotationOverLifetime.angularVelocity.Evaluate(normalizedLifetime);
                p.rotation += p.angularVelocity * deltaTime * Mathf.Rad2Deg;
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
            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                    pos += UnityEngine.Random.insideUnitSphere * shape.radius;
                    break;
                case ParticleSystemShapeType.Hemisphere:
                    var pt = UnityEngine.Random.insideUnitSphere;
                    pt.y = Mathf.Abs(pt.y);
                    pos += pt * shape.radius;
                    break;
                case ParticleSystemShapeType.Cone:
                    pos += UnityEngine.Random.insideUnitSphere * shape.radius;
                    vel = Vector3.Lerp(vel, Vector3.up * speed, shape.sphericalDirectionAmount);
                    vel += UnityEngine.Random.insideUnitSphere * shape.randomDirectionAmount;
                    break;
                case ParticleSystemShapeType.Box:
                    pos += new Vector3(
                        UnityEngine.Random.Range(-shape.radius, shape.radius),
                        UnityEngine.Random.Range(-shape.length * 0.5f, shape.length * 0.5f),
                        UnityEngine.Random.Range(-shape.radius, shape.radius));
                    break;
                case ParticleSystemShapeType.Circle:
                    var circlePos = UnityEngine.Random.insideUnitCircle * shape.radius;
                    pos += new Vector3(circlePos.x, 0, circlePos.y);
                    break;
            }
        }

        var particle = new Particle
        {
            position = pos,
            velocity = vel,
            startSize = size,
            startSize3D = main.startSize3D ? new Vector3(
                main.startSizeX.Evaluate(random),
                main.startSizeY.Evaluate(random),
                main.startSizeZ.Evaluate(random)) : Vector3.one * size,
            startColor = useDefaults ? new Color32((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), (byte)(color.a * 255)) : @params.startColor,
            startLifetime = lifetime,
            remainingLifetime = lifetime,
            rotation = rotation * Mathf.Rad2Deg,
            rotation3D = main.startRotation3D ? new Vector3(
                main.startRotationX.Evaluate(random) * Mathf.Rad2Deg,
                main.startRotationY.Evaluate(random) * Mathf.Rad2Deg,
                main.startRotationZ.Evaluate(random) * Mathf.Rad2Deg) : new Vector3(0f, 0f, rotation * Mathf.Rad2Deg),
            startRotation = rotation,
            startSpeed = speed,
            lifetime = lifetime,
            scale = Vector3.one * size,
            totalSize3D = Vector3.one * size,
            axisOfRotation = @params.axisOfRotation != Vector3.zero ? @params.axisOfRotation : Vector3.up,
            randomSeed = @params.randomSeed > 0f ? (ulong)@params.randomSeed : (ulong)UnityEngine.Random.Range(0, int.MaxValue),
            seed = (int)(@params.randomSeed > 0f ? @params.randomSeed : UnityEngine.Random.Range(0, int.MaxValue)),
            meshIndex = 0,
            angularVelocity = 0f,
            rotationVelocity = 0f,
            radialVelocity = 0f,
            animatedVelocity = Vector3.zero,
            angularVelocity3D = Vector3.zero,
            totalVelocity = vel,
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
