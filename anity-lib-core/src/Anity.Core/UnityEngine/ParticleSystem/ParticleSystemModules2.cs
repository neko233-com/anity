namespace UnityEngine;

public partial class ParticleSystem
{
    public struct VelocityOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal VelocityOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            x = new ParticleSystem.MinMaxCurve(0f);
            y = new ParticleSystem.MinMaxCurve(0f);
            z = new ParticleSystem.MinMaxCurve(0f);
            space = ParticleSystemSimulationSpace.Local;
            orbitalX = new ParticleSystem.MinMaxCurve(0f);
            orbitalY = new ParticleSystem.MinMaxCurve(0f);
            orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f);
            orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f);
            orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f);
            radial = new ParticleSystem.MinMaxCurve(0f);
            speedModifier = new ParticleSystem.MinMaxCurve(1f);
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve x { get; set; }
        public ParticleSystem.MinMaxCurve y { get; set; }
        public ParticleSystem.MinMaxCurve z { get; set; }
        public ParticleSystemSimulationSpace space { get; set; }
        public ParticleSystem.MinMaxCurve orbitalX { get; set; }
        public ParticleSystem.MinMaxCurve orbitalY { get; set; }
        public ParticleSystem.MinMaxCurve orbitalZ { get; set; }
        public ParticleSystem.MinMaxCurve orbitalOffsetX { get; set; }
        public ParticleSystem.MinMaxCurve orbitalOffsetY { get; set; }
        public ParticleSystem.MinMaxCurve orbitalOffsetZ { get; set; }
        public ParticleSystem.MinMaxCurve radial { get; set; }
        public ParticleSystem.MinMaxCurve speedModifier { get; set; }
    }

    public struct LimitVelocityOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal LimitVelocityOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            limitX = new ParticleSystem.MinMaxCurve(1f);
            limitY = new ParticleSystem.MinMaxCurve(1f);
            limitZ = new ParticleSystem.MinMaxCurve(1f);
            limit = new ParticleSystem.MinMaxCurve(1f);
            dampen = 1f;
            drag = 0f;
            multiplyByDrag = false;
            multiplyByParticleSize = false;
            multiplyByParticleMass = false;
            space = ParticleSystemSimulationSpace.Local;
            separateAxes = false;
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve limitX { get; set; }
        public ParticleSystem.MinMaxCurve limitY { get; set; }
        public ParticleSystem.MinMaxCurve limitZ { get; set; }
        public ParticleSystem.MinMaxCurve limit { get; set; }
        public float dampen { get; set; }
        public float drag { get; set; }
        public bool multiplyByDrag { get; set; }
        public bool multiplyByParticleSize { get; set; }
        public bool multiplyByParticleMass { get; set; }
        public ParticleSystemSimulationSpace space { get; set; }
        public bool separateAxes { get; set; }
    }

    public struct InheritVelocityModule
    {
        private ParticleSystem _particleSystem;

        internal InheritVelocityModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            mode = ParticleSystemInheritVelocityMode.Initial;
            curve = new ParticleSystem.MinMaxCurve(1f);
            subEmitterSamplingDistance = 0.25f;
        }

        public bool enabled { get; set; }
        public ParticleSystemInheritVelocityMode mode { get; set; }
        public MinMaxCurve curve { get; set; }
        public float subEmitterSamplingDistance { get; set; }
    }

    public struct ForceOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal ForceOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            x = new ParticleSystem.MinMaxCurve(0f);
            y = new ParticleSystem.MinMaxCurve(0f);
            z = new ParticleSystem.MinMaxCurve(0f);
            space = ParticleSystemSimulationSpace.Local;
            randomize = false;
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve x { get; set; }
        public ParticleSystem.MinMaxCurve y { get; set; }
        public ParticleSystem.MinMaxCurve z { get; set; }
        public ParticleSystemSimulationSpace space { get; set; }
        public bool randomize { get; set; }
    }

    public struct ColorOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal ColorOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            color = new ParticleSystem.MinMaxGradient(Color.white);
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxGradient color { get; set; }
    }

    public struct ColorBySpeedModule
    {
        private ParticleSystem _particleSystem;

        internal ColorBySpeedModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            color = new ParticleSystem.MinMaxGradient(Color.white);
            range = new Vector2(0f, 1f);
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxGradient color { get; set; }
        public Vector2 range { get; set; }
    }

    public struct SizeOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal SizeOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            size = new ParticleSystem.MinMaxCurve(1f);
            sizeX = new ParticleSystem.MinMaxCurve(1f);
            sizeY = new ParticleSystem.MinMaxCurve(1f);
            sizeZ = new ParticleSystem.MinMaxCurve(1f);
            separateAxes = false;
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve size { get; set; }
        public ParticleSystem.MinMaxCurve sizeX { get; set; }
        public ParticleSystem.MinMaxCurve sizeY { get; set; }
        public ParticleSystem.MinMaxCurve sizeZ { get; set; }
        public bool separateAxes { get; set; }
    }

    public struct SizeBySpeedModule
    {
        private ParticleSystem _particleSystem;

        internal SizeBySpeedModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            size = new ParticleSystem.MinMaxCurve(1f);
            sizeX = new ParticleSystem.MinMaxCurve(1f);
            sizeY = new ParticleSystem.MinMaxCurve(1f);
            sizeZ = new ParticleSystem.MinMaxCurve(1f);
            separateAxes = false;
            range = new Vector2(0f, 1f);
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve size { get; set; }
        public ParticleSystem.MinMaxCurve sizeX { get; set; }
        public ParticleSystem.MinMaxCurve sizeY { get; set; }
        public ParticleSystem.MinMaxCurve sizeZ { get; set; }
        public bool separateAxes { get; set; }
        public Vector2 range { get; set; }
    }

    public struct RotationOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal RotationOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            x = new ParticleSystem.MinMaxCurve(0f);
            y = new ParticleSystem.MinMaxCurve(0f);
            z = new ParticleSystem.MinMaxCurve(0f);
            separateAxes = false;
            zCurve = new ParticleSystem.MinMaxCurve(0f);
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve x { get; set; }
        public ParticleSystem.MinMaxCurve y { get; set; }
        public ParticleSystem.MinMaxCurve z { get; set; }
        public ParticleSystem.MinMaxCurve zCurve { get; set; }
        public bool separateAxes { get; set; }
    }

    public struct RotationBySpeedModule
    {
        private ParticleSystem _particleSystem;

        internal RotationBySpeedModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            x = new ParticleSystem.MinMaxCurve(0f);
            y = new ParticleSystem.MinMaxCurve(0f);
            z = new ParticleSystem.MinMaxCurve(0f);
            separateAxes = false;
            range = new Vector2(0f, 1f);
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve x { get; set; }
        public ParticleSystem.MinMaxCurve y { get; set; }
        public ParticleSystem.MinMaxCurve z { get; set; }
        public bool separateAxes { get; set; }
        public Vector2 range { get; set; }
    }
}