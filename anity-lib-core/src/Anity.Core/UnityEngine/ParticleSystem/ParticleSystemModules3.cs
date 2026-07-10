using System;

namespace UnityEngine;

public partial class ParticleSystem
{
    public struct ExternalForcesModule
    {
        private ParticleSystem _particleSystem;

        internal ExternalForcesModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            multiplierCurve = new ParticleSystem.MinMaxCurve(1f);
            multiplier = 1f;
            influenceFilter = ParticleSystemGameObjectFilter.All;
            _forceFields = new ParticleSystemForceField[0];
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve multiplierCurve { get; set; }
        public float multiplier { get; set; }
        public ParticleSystemGameObjectFilter influenceFilter { get; set; }

        private ParticleSystemForceField[] _forceFields;

        public int forceFieldCount => _forceFields.Length;

        public void AddForceField(ParticleSystemForceField forceField)
        {
            var newArray = new ParticleSystemForceField[_forceFields.Length + 1];
            _forceFields.CopyTo(newArray, 0);
            newArray[_forceFields.Length] = forceField;
            _forceFields = newArray;
        }

        public void RemoveForceField(ParticleSystemForceField forceField)
        {
            int index = Array.IndexOf(_forceFields, forceField);
            if (index >= 0)
            {
                var newArray = new ParticleSystemForceField[_forceFields.Length - 1];
                if (index > 0)
                    Array.Copy(_forceFields, 0, newArray, 0, index);
                if (index < _forceFields.Length - 1)
                    Array.Copy(_forceFields, index + 1, newArray, index, _forceFields.Length - index - 1);
                _forceFields = newArray;
            }
        }

        public void ClearForceFields()
        {
            _forceFields = new ParticleSystemForceField[0];
        }
    }

    public struct NoiseModule
    {
        private ParticleSystem _particleSystem;

        internal NoiseModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            strength = new ParticleSystem.MinMaxCurve(1f);
            strengthX = new ParticleSystem.MinMaxCurve(1f);
            strengthY = new ParticleSystem.MinMaxCurve(1f);
            strengthZ = new ParticleSystem.MinMaxCurve(1f);
            frequency = 0.5f;
            scrollSpeed = new ParticleSystem.MinMaxCurve(0f);
            damping = true;
            octaveCount = 1;
            octaveMultiplier = 0.5f;
            octaveScale = 2f;
            quality = ParticleSystemNoiseQuality.Medium;
            remap = new ParticleSystem.MinMaxCurve(0f, 1f);
            remapX = new ParticleSystem.MinMaxCurve(0f, 1f);
            remapY = new ParticleSystem.MinMaxCurve(0f, 1f);
            remapZ = new ParticleSystem.MinMaxCurve(0f, 1f);
            positionAmount = new ParticleSystem.MinMaxCurve(1f);
            positionAmountX = new ParticleSystem.MinMaxCurve(1f);
            positionAmountY = new ParticleSystem.MinMaxCurve(1f);
            positionAmountZ = new ParticleSystem.MinMaxCurve(1f);
            rotationAmount = new ParticleSystem.MinMaxCurve(0f);
            rotationAmountX = new ParticleSystem.MinMaxCurve(0f);
            rotationAmountY = new ParticleSystem.MinMaxCurve(0f);
            rotationAmountZ = new ParticleSystem.MinMaxCurve(0f);
            sizeAmount = new ParticleSystem.MinMaxCurve(0f);
            separateAxes = false;
            useQualityScale = false;
            remapEnabled = false;
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve strength { get; set; }
        public ParticleSystem.MinMaxCurve strengthX { get; set; }
        public ParticleSystem.MinMaxCurve strengthY { get; set; }
        public ParticleSystem.MinMaxCurve strengthZ { get; set; }
        public float frequency { get; set; }
        public ParticleSystem.MinMaxCurve scrollSpeed { get; set; }
        public bool damping { get; set; }
        public int octaveCount { get; set; }
        public float octaveMultiplier { get; set; }
        public float octaveScale { get; set; }
        public ParticleSystemNoiseQuality quality { get; set; }
        public ParticleSystem.MinMaxCurve remap { get; set; }
        public ParticleSystem.MinMaxCurve remapX { get; set; }
        public ParticleSystem.MinMaxCurve remapY { get; set; }
        public ParticleSystem.MinMaxCurve remapZ { get; set; }
        public ParticleSystem.MinMaxCurve positionAmount { get; set; }
        public ParticleSystem.MinMaxCurve positionAmountX { get; set; }
        public ParticleSystem.MinMaxCurve positionAmountY { get; set; }
        public ParticleSystem.MinMaxCurve positionAmountZ { get; set; }
        public ParticleSystem.MinMaxCurve rotationAmount { get; set; }
        public ParticleSystem.MinMaxCurve rotationAmountX { get; set; }
        public ParticleSystem.MinMaxCurve rotationAmountY { get; set; }
        public ParticleSystem.MinMaxCurve rotationAmountZ { get; set; }
        public ParticleSystem.MinMaxCurve sizeAmount { get; set; }
        public bool separateAxes { get; set; }
        public bool useQualityScale { get; set; }
        public bool remapEnabled { get; set; }
    }

    public struct CollisionModule
    {
        private ParticleSystem _particleSystem;

        internal CollisionModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            type = ParticleSystemCollisionType.World;
            mode = ParticleSystemCollisionMode.Collision3D;
            quality = ParticleSystemCollisionQuality.Medium;
            bounce = 1f;
            bounceMultiplier = 1f;
            dampen = 0f;
            dampenMultiplier = 1f;
            lifetimeLoss = 0f;
            lifetimeLossMultiplier = 1f;
            minKillSpeed = 0f;
            maxKillSpeed = float.PositiveInfinity;
            radiusScale = 1f;
            collidesWith = ~0;
            collidesWithInside = false;
            enableDynamicColliders = true;
            usePlaneMeshColliders = false;
            sendCollisionMessages = false;
            _planes = new Transform[0];
            voxelSize = 0.5f;
            maxCollisionShapes = 256;
            minKillSpeed = 0f;
            maxKillSpeed = 3.40282347e+38f;
        }

        public bool enabled { get; set; }
        public ParticleSystemCollisionType type { get; set; }
        public ParticleSystemCollisionMode mode { get; set; }
        public ParticleSystemCollisionQuality quality { get; set; }
        public float bounce { get; set; }
        public float bounceMultiplier { get; set; }
        public float dampen { get; set; }
        public float dampenMultiplier { get; set; }
        public float lifetimeLoss { get; set; }
        public float lifetimeLossMultiplier { get; set; }
        public float minKillSpeed { get; set; }
        public float maxKillSpeed { get; set; }
        public float radiusScale { get; set; }
        public int collidesWith { get; set; }
        public bool collidesWithInside { get; set; }
        public bool enableDynamicColliders { get; set; }
        public bool usePlaneMeshColliders { get; set; }
        public bool sendCollisionMessages { get; set; }
        public float voxelSize { get; set; }
        public int maxCollisionShapes { get; set; }

        private Transform[] _planes;

        public int planeCount => _planes.Length;

        public Transform GetPlane(int index)
        {
            return _planes[index];
        }

        public void SetPlane(int index, Transform plane)
        {
            _planes[index] = plane;
        }

        public void SetPlanes(Transform[] planes)
        {
            _planes = planes;
        }

        public Transform[] GetPlanes()
        {
            return _planes;
        }
    }

    public struct TriggerModule
    {
        private ParticleSystem _particleSystem;

        internal TriggerModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            inside = ParticleSystemOverlapAction.Ignore;
            outside = ParticleSystemOverlapAction.Ignore;
            enter = ParticleSystemOverlapAction.Ignore;
            exit = ParticleSystemOverlapAction.Ignore;
            _colliders = new Collider[0];
            radiusScale = 1f;
        }

        public bool enabled { get; set; }
        public ParticleSystemOverlapAction inside { get; set; }
        public ParticleSystemOverlapAction outside { get; set; }
        public ParticleSystemOverlapAction enter { get; set; }
        public ParticleSystemOverlapAction exit { get; set; }
        public float radiusScale { get; set; }

        private Collider[] _colliders;

        public int colliderCount => _colliders.Length;

        public Collider GetCollider(int index)
        {
            return _colliders[index];
        }

        public void SetCollider(int index, Collider collider)
        {
            _colliders[index] = collider;
        }

        public void SetColliders(Collider[] colliders)
        {
            _colliders = colliders;
        }

        public Collider[] GetColliders()
        {
            return _colliders;
        }
    }

    public struct SubEmittersModule
    {
        private ParticleSystem _particleSystem;

        internal SubEmittersModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            _subEmitters = new SubEmitter[0];
        }

        public bool enabled { get; set; }

        private SubEmitter[] _subEmitters;

        public int subEmittersCount => _subEmitters.Length;

        public SubEmitter GetSubEmitter(int index)
        {
            return _subEmitters[index];
        }

        public void SetSubEmitter(int index, SubEmitter subEmitter)
        {
            _subEmitters[index] = subEmitter;
        }

        public void AddSubEmitter(ParticleSystem subEmitter, ParticleSystemSubEmitterType type, ParticleSystemSubEmitterProperties properties)
        {
            AddSubEmitter(subEmitter, type, properties, 0f);
        }

        public void AddSubEmitter(ParticleSystem subEmitter, ParticleSystemSubEmitterType type, ParticleSystemSubEmitterProperties properties, float probability)
        {
            var newArray = new SubEmitter[_subEmitters.Length + 1];
            _subEmitters.CopyTo(newArray, 0);
            newArray[_subEmitters.Length] = new SubEmitter
            {
                emitter = subEmitter,
                type = type,
                properties = properties,
                probability = probability
            };
            _subEmitters = newArray;
        }

        public void RemoveSubEmitter(int index)
        {
            var newArray = new SubEmitter[_subEmitters.Length - 1];
            if (index > 0)
                Array.Copy(_subEmitters, 0, newArray, 0, index);
            if (index < _subEmitters.Length - 1)
                Array.Copy(_subEmitters, index + 1, newArray, index, _subEmitters.Length - index - 1);
            _subEmitters = newArray;
        }
    }

    public struct SubEmitter
    {
        public ParticleSystem emitter;
        public ParticleSystemSubEmitterType type;
        public ParticleSystemSubEmitterProperties properties;
        public float probability;
        public int births;
        public int deaths;
        public int collisions;
        public int triggers;
        public int subEmitters;
    }
}