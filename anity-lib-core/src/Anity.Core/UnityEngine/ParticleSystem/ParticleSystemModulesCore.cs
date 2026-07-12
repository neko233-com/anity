using System;
using System.Collections.Generic;

namespace UnityEngine;

public partial class ParticleSystem
{
    public struct MainModule
    {
        private ParticleSystem _particleSystem;

        internal MainModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            duration = 5f;
            loop = false;
            playOnAwake = true;
            prewarm = false;
            startDelay = new MinMaxCurve(0f);
            startLifetime = new MinMaxCurve(5f);
            startSpeed = new MinMaxCurve(5f);
            startSize = new MinMaxCurve(1f);
            startSize3D = false;
            startColor = new MinMaxGradient(Color.white);
            startRotation = new MinMaxCurve(0f);
            startRotation3D = false;
            gravityModifier = new MinMaxCurve(0f);
            simulationSpace = ParticleSystemSimulationSpace.Local;
            simulationSpeed = 1f;
            scalingMode = ParticleSystemScalingMode.Local;
            maxParticles = 1000;
            startSizeX = new MinMaxCurve(1f);
            startSizeY = new MinMaxCurve(1f);
            startSizeZ = new MinMaxCurve(1f);
            startRotationX = new MinMaxCurve(0f);
            startRotationY = new MinMaxCurve(0f);
            startRotationZ = new MinMaxCurve(0f);
        }

        public float duration { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public bool prewarm { get; set; }
        public MinMaxCurve startDelay { get; set; }
        public MinMaxCurve startLifetime { get; set; }
        public MinMaxCurve startSpeed { get; set; }
        public MinMaxCurve startSize { get; set; }
        public bool startSize3D { get; set; }
        public MinMaxCurve startSizeX { get; set; }
        public MinMaxCurve startSizeY { get; set; }
        public MinMaxCurve startSizeZ { get; set; }
        public MinMaxGradient startColor { get; set; }
        public MinMaxCurve startRotation { get; set; }
        public bool startRotation3D { get; set; }
        public MinMaxCurve startRotationX { get; set; }
        public MinMaxCurve startRotationY { get; set; }
        public MinMaxCurve startRotationZ { get; set; }
        public MinMaxCurve gravityModifier { get; set; }
        public ParticleSystemSimulationSpace simulationSpace { get; set; }
        public float simulationSpeed { get; set; }
        public ParticleSystemScalingMode scalingMode { get; set; }
        public int maxParticles { get; set; }
    }

    public struct EmissionModule
    {
        private ParticleSystem _particleSystem;
        private List<Burst> _bursts;

        internal EmissionModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            _bursts = new List<Burst>();
            enabled = true;
            rateOverTime = new MinMaxCurve(10f);
            rateOverDistance = new MinMaxCurve(0f);
        }

        public bool enabled { get; set; }
        public MinMaxCurve rateOverTime { get; set; }
        public MinMaxCurve rateOverDistance { get; set; }
        public int burstCount => _bursts.Count;

        public void SetBursts(Burst[] bursts)
        {
            _bursts.Clear();
            if (bursts != null) _bursts.AddRange(bursts);
        }

        public void SetBursts(List<Burst> bursts)
        {
            _bursts.Clear();
            if (bursts != null) _bursts.AddRange(bursts);
        }

        public void AddBurst(Burst burst)
        {
            _bursts.Add(burst);
        }

        public Burst[] GetBursts()
        {
            return _bursts.ToArray();
        }

        public void SetBurst(int index, Burst burst)
        {
            if (index >= 0 && index < _bursts.Count)
                _bursts[index] = burst;
        }

        public Burst GetBurst(int index)
        {
            return _bursts[index];
        }
    }

    public struct ShapeModule
    {
        private ParticleSystem _particleSystem;

        internal ShapeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            shapeType = ParticleSystemShapeType.Cone;
            radius = 1f;
            radiusThickness = 1f;
            angle = 25f;
            length = 5f;
            randomDirectionAmount = 0f;
            sphericalDirectionAmount = 0f;
            randomPositionAmount = 0f;
            alignToDirection = false;
        }

        public bool enabled { get; set; }
        public ParticleSystemShapeType shapeType { get; set; }
        public float radius { get; set; }
        public float radiusThickness { get; set; }
        public float angle { get; set; }
        public float length { get; set; }
        public float randomDirectionAmount { get; set; }
        public float sphericalDirectionAmount { get; set; }
        public float randomPositionAmount { get; set; }
        public bool alignToDirection { get; set; }
    }

    public struct VelocityOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal VelocityOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            x = new MinMaxCurve(0f);
            y = new MinMaxCurve(0f);
            z = new MinMaxCurve(0f);
            orbitalX = new MinMaxCurve(0f);
            orbitalY = new MinMaxCurve(0f);
            orbitalZ = new MinMaxCurve(0f);
            orbitalOffsetX = new MinMaxCurve(0f);
            orbitalOffsetY = new MinMaxCurve(0f);
            orbitalOffsetZ = new MinMaxCurve(0f);
            radial = new MinMaxCurve(0f);
            speedModifier = new MinMaxCurve(1f);
        }

        public bool enabled { get; set; }
        public MinMaxCurve x { get; set; }
        public MinMaxCurve y { get; set; }
        public MinMaxCurve z { get; set; }
        public MinMaxCurve orbitalX { get; set; }
        public MinMaxCurve orbitalY { get; set; }
        public MinMaxCurve orbitalZ { get; set; }
        public MinMaxCurve orbitalOffsetX { get; set; }
        public MinMaxCurve orbitalOffsetY { get; set; }
        public MinMaxCurve orbitalOffsetZ { get; set; }
        public MinMaxCurve radial { get; set; }
        public MinMaxCurve speedModifier { get; set; }
    }

    public struct ColorOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal ColorOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            color = new MinMaxGradient(Color.white);
        }

        public bool enabled { get; set; }
        public MinMaxGradient color { get; set; }
    }

    public struct SizeOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal SizeOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            size = new MinMaxCurve(1f);
            x = new MinMaxCurve(1f);
            y = new MinMaxCurve(1f);
            z = new MinMaxCurve(1f);
            separateAxes = false;
        }

        public bool enabled { get; set; }
        public MinMaxCurve size { get; set; }
        public MinMaxCurve x { get; set; }
        public MinMaxCurve y { get; set; }
        public MinMaxCurve z { get; set; }
        public bool separateAxes { get; set; }
    }

    public struct RotationOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal RotationOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            angularVelocity = new MinMaxCurve(0f);
            x = new MinMaxCurve(0f);
            y = new MinMaxCurve(0f);
            z = new MinMaxCurve(0f);
            separateAxes = false;
        }

        public bool enabled { get; set; }
        public MinMaxCurve angularVelocity { get; set; }
        public MinMaxCurve x { get; set; }
        public MinMaxCurve y { get; set; }
        public MinMaxCurve z { get; set; }
        public bool separateAxes { get; set; }
    }

    public struct ForceOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal ForceOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            x = new MinMaxCurve(0f);
            y = new MinMaxCurve(0f);
            z = new MinMaxCurve(0f);
            space = ParticleSystemSimulationSpace.Local;
        }

        public bool enabled { get; set; }
        public MinMaxCurve x { get; set; }
        public MinMaxCurve y { get; set; }
        public MinMaxCurve z { get; set; }
        public ParticleSystemSimulationSpace space { get; set; }
    }

    public struct ColorBySpeedModule
    {
        private ParticleSystem _particleSystem;

        internal ColorBySpeedModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            color = new MinMaxGradient(Color.white);
            range = new Vector2(0f, 1f);
        }

        public bool enabled { get; set; }
        public MinMaxGradient color { get; set; }
        public Vector2 range { get; set; }
    }

    public struct SizeBySpeedModule
    {
        private ParticleSystem _particleSystem;

        internal SizeBySpeedModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            size = new MinMaxCurve(1f);
            range = new Vector2(0f, 1f);
        }

        public bool enabled { get; set; }
        public MinMaxCurve size { get; set; }
        public Vector2 range { get; set; }
    }

    public struct RotationBySpeedModule
    {
        private ParticleSystem _particleSystem;

        internal RotationBySpeedModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            angularVelocity = new MinMaxCurve(0f);
            x = new MinMaxCurve(0f);
            y = new MinMaxCurve(0f);
            z = new MinMaxCurve(0f);
            range = new Vector2(0f, 1f);
            separateAxes = false;
        }

        public bool enabled { get; set; }
        public MinMaxCurve angularVelocity { get; set; }
        public MinMaxCurve x { get; set; }
        public MinMaxCurve y { get; set; }
        public MinMaxCurve z { get; set; }
        public Vector2 range { get; set; }
        public bool separateAxes { get; set; }
    }

    public struct InheritVelocityModule
    {
        private ParticleSystem _particleSystem;

        internal InheritVelocityModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            curve = new MinMaxCurve(1f);
            mode = ParticleSystemInheritVelocityMode.Initial;
        }

        public bool enabled { get; set; }
        public MinMaxCurve curve { get; set; }
        public ParticleSystemInheritVelocityMode mode { get; set; }
    }

    public struct LimitVelocityOverLifetimeModule
    {
        private ParticleSystem _particleSystem;

        internal LimitVelocityOverLifetimeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            limit = new MinMaxCurve(1f);
            limitX = new MinMaxCurve(1f);
            limitY = new MinMaxCurve(1f);
            limitZ = new MinMaxCurve(1f);
            dampen = 0f;
            separateAxes = false;
        }

        public bool enabled { get; set; }
        public MinMaxCurve limit { get; set; }
        public MinMaxCurve limitX { get; set; }
        public MinMaxCurve limitY { get; set; }
        public MinMaxCurve limitZ { get; set; }
        public float dampen { get; set; }
        public bool separateAxes { get; set; }
    }

}
