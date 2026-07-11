namespace UnityEngine;

public class CollisionModule
{
    private readonly ParticleSystem _ps;
    public CollisionModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public ParticleSystemCollisionType type { get; set; }
    public int collidesWith { get; set; } = -1;
    public MinMaxCurve dampen { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve bounce { get; set; } = new MinMaxCurve(0f);
    public MinMaxCurve lifetimeLoss { get; set; } = new MinMaxCurve(0f);
    public float minKillSpeed { get; set; }
}

public class TriggerModule
{
    private readonly ParticleSystem _ps;
    public TriggerModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
}

public class SubEmittersModule
{
    private readonly ParticleSystem _ps;
    public SubEmittersModule(ParticleSystem ps) { _ps = ps; }
    public ParticleSystem[] subEmitters { get; set; }
    public int subEmittersCount => subEmitters?.Length ?? 0;
}

public class TextureSheetAnimationModule
{
    private readonly ParticleSystem _ps;
    public TextureSheetAnimationModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public int numTilesX { get; set; } = 1;
    public int numTilesY { get; set; } = 1;
    public int cycleCount { get; set; } = 1;
    public float fps { get; set; } = 30f;
}

public class NoiseModule
{
    private readonly ParticleSystem _ps;
    public NoiseModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve strength { get; set; } = new MinMaxCurve(1f);
    public float frequency { get; set; } = 0.5f;
    public bool damping { get; set; } = true;
    public float scrollSpeed { get; set; }
}

public class TrailModule
{
    private readonly ParticleSystem _ps;
    public TrailModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public float ratio { get; set; } = 1f;
    public MinMaxCurve lifetime { get; set; } = new MinMaxCurve(1f);
    public float widthPerTrail { get; set; } = 1f;
}

public class ColorBySpeedModule
{
    private readonly ParticleSystem _ps;
    public ColorBySpeedModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxGradient color { get; set; } = Color.white;
    public Vector2 range { get; set; } = new Vector2(0f, 1f);
}

public class SizeBySpeedModule
{
    private readonly ParticleSystem _ps;
    public SizeBySpeedModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve size { get; set; } = new MinMaxCurve(1f);
    public Vector2 range { get; set; } = new Vector2(0f, 1f);
}

public class RotationBySpeedModule
{
    private readonly ParticleSystem _ps;
    public RotationBySpeedModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
}

public class InheritVelocityModule
{
    private readonly ParticleSystem _ps;
    public InheritVelocityModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
    public MinMaxCurve curve { get; set; } = new MinMaxCurve(1f);
    public ParticleSystemInheritVelocityMode mode { get; set; }
}

public class LimitVelocityOverLifetimeModule
{
    private readonly ParticleSystem _ps;
    public LimitVelocityOverLifetimeModule(ParticleSystem ps) { _ps = ps; }
    public bool enabled { get; set; }
}

public enum ParticleSystemGravitySimulation { Physics3D, Physics2D }
