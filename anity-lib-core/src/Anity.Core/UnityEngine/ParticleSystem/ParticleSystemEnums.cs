namespace UnityEngine;

public enum ParticleSystemStopBehavior
{
    StopEmittingAndClear,
    StopEmitting
}

public enum ParticleSystemRandomInitOptions
{
    None,
    RestartSeed
}

public enum ParticleSystemShapeType
{
    Sphere,
    SphereShell,
    Hemisphere,
    HemisphereShell,
    Cone,
    ConeShell,
    Box,
    BoxShell,
    BoxEdge,
    Mesh,
    MeshShell,
    MeshRenderer,
    SkinnedMeshRenderer,
    Circle,
    CircleEdge,
    SingleSidedEdge,
    Donut,
    Rectangle,
    RectangleEdge
}

public enum ParticleSystemShapeMultiModeValue
{
    Random,
    Loop,
    PingPong,
    Spread
}

public enum ParticleSystemGradientMode
{
    Color,
    Gradient,
    TwoColors,
    RandomColor
}

public enum ParticleSystemCurveMode
{
    Constant,
    Curve,
    TwoCurves,
    TwoConstants
}

public enum ParticleSystemSimulationSpace
{
    Local,
    World,
    Custom
}

public enum ParticleSystemScalingMode
{
    Hierarchy,
    Local,
    Shape
}

public enum ParticleSystemRenderMode
{
    Billboard,
    Stretch,
    HorizontalBillboard,
    VerticalBillboard,
    Mesh,
    None
}

public enum ParticleSystemRenderSpace
{
    View,
    World,
    Local,
    Facing,
    Velocity
}

public enum ParticleSystemSortMode
{
    None,
    Distance,
    YoungestInFront,
    OldestInFront,
    Depth
}

public enum ParticleSystemStretchRotation
{
    View,
    Runtime,
    Velocity
}

public enum ParticleSystemCollisionMode
{
    Collision3D,
    Collision2D
}

public enum ParticleSystemCollisionQuality
{
    Low,
    Medium,
    High
}

public enum ParticleSystemCollisionType
{
    World,
    Planes
}

public enum ParticleSystemTrailMode
{
    Ratio,
    Lifetime
}

public enum ParticleSystemNoiseQuality
{
    Low,
    Medium,
    High
}

public enum ParticleSystemVertexStreams
{
    Position,
    Normal,
    Tangent,
    Color,
    UV,
    UV2,
    UV3,
    UV4,
    AnimBlend,
    AnimFrame,
    InstanceID,
    Speed,
    Size,
    Rotation,
    Age,
    Custom1,
    Custom2,
    Stretched
}

public enum ParticleSystemCustomData
{
    Custom1,
    Custom2
}

public enum ParticleSystemCustomDataMode
{
    None,
    Vector,
    Color
}

public enum ParticleSystemMeshShapeType
{
    Vertex,
    Edge,
    Triangle
}

public enum ParticleSystemAnimationMode
{
    Grid,
    Sprites
}

public enum ParticleSystemAnimationTimeMode
{
    Lifetime,
    Speed,
    FPS
}

public enum ParticleSystemAnimationType
{
    WholeSheet,
    SingleRow
}

public enum ParticleSystemEmitterVelocityMode
{
    Transform,
    Rigidbody
}

public enum ParticleSystemCullingMode
{
    Automatic,
    PauseAndCatchup,
    Pause,
    AlwaysSimulate
}

public enum ParticleSystemRingBufferMode
{
    PauseUntilReplacement,
    DisableUntilReplacement
}

public enum ParticleSystemForceFieldShape
{
    Box,
    Sphere,
    Hemisphere,
    Cylinder,
    Cone
}

public enum ParticleSystemForceFieldShapeType
{
    Directional,
    Spherical,
    Toroidal,
    VectorField,
    Drag,
    Swirl
}

public enum ParticleSystemInheritVelocityMode
{
    Initial,
    Current
}

public enum ParticleSystemGameObjectFilter
{
    All,
    LayerMask,
    List
}

public enum ParticleSystemOverlapAction
{
    Ignore,
    Kill,
    Callback
}

public enum ParticleSystemSubEmitterType
{
    Birth,
    Collision,
    Death,
    Trigger,
    Manual
}

public enum ParticleSystemSubEmitterProperties
{
    InheritNothing,
    InheritEverything,
    InheritColor,
    InheritSize,
    InheritRotation,
    InheritLifetime,
    InheritDuration
}

public enum ParticleSystemTrailRibbonShape
{
    Default,
    Circle,
    OneSidedPlane,
    TwoSidedPlane
}