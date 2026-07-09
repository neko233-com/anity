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
            loop = true;
            playOnAwake = true;
            startLifetime = new ParticleSystem.MinMaxCurve(5f);
            startSpeed = new ParticleSystem.MinMaxCurve(5f);
            startSize = new ParticleSystem.MinMaxCurve(1f);
            startRotation = new ParticleSystem.MinMaxCurve(0f);
            startColor = new ParticleSystem.MinMaxGradient(Color.white);
            gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            simulationSpeed = 1f;
            maxParticles = 1000;
            simulationSpace = ParticleSystemSimulationSpace.Local;
            scalingMode = ParticleSystemScalingMode.Hierarchy;
            playOnAwake = true;
            emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            cullingMode = ParticleSystemCullingMode.Automatic;
            ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplacement;
            ringBufferLoopRange = new Vector2(0f, 1f);
            startDelay = new ParticleSystem.MinMaxCurve(0f);
            startRotation3D = false;
            startSize3D = false;
            flipRotation = 0f;
            randomizeRotationDirection = 0f;
            customSimulationSpace = null;
        }

        public float duration { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public ParticleSystem.MinMaxCurve startLifetime { get; set; }
        public ParticleSystem.MinMaxCurve startSpeed { get; set; }
        public ParticleSystem.MinMaxCurve startSize { get; set; }
        public ParticleSystem.MinMaxCurve startRotation { get; set; }
        public ParticleSystem.MinMaxGradient startColor { get; set; }
        public ParticleSystem.MinMaxCurve gravityModifier { get; set; }
        public float simulationSpeed { get; set; }
        public int maxParticles { get; set; }
        public ParticleSystemSimulationSpace simulationSpace { get; set; }
        public ParticleSystemScalingMode scalingMode { get; set; }
        public ParticleSystemEmitterVelocityMode emitterVelocityMode { get; set; }
        public ParticleSystemCullingMode cullingMode { get; set; }
        public ParticleSystemRingBufferMode ringBufferMode { get; set; }
        public Vector2 ringBufferLoopRange { get; set; }
        public ParticleSystem.MinMaxCurve startDelay { get; set; }
        public bool startRotation3D { get; set; }
        public bool startSize3D { get; set; }
        public float flipRotation { get; set; }
        public float randomizeRotationDirection { get; set; }
        public Transform customSimulationSpace { get; set; }
    }

    public struct EmissionModule
    {
        private ParticleSystem _particleSystem;

        internal EmissionModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            rateOverTime = new ParticleSystem.MinMaxCurve(10f);
            rateOverDistance = new ParticleSystem.MinMaxCurve(0f);
            _bursts = new ParticleSystem.Burst[0];
        }

        public bool enabled { get; set; }
        public ParticleSystem.MinMaxCurve rateOverTime { get; set; }
        public ParticleSystem.MinMaxCurve rateOverDistance { get; set; }

        private ParticleSystem.Burst[] _bursts;

        public int burstCount => _bursts.Length;

        public ParticleSystem.Burst GetBurst(int index)
        {
            return _bursts[index];
        }

        public void SetBurst(int index, ParticleSystem.Burst burst)
        {
            _bursts[index] = burst;
        }

        public void SetBursts(ParticleSystem.Burst[] bursts)
        {
            _bursts = bursts;
        }

        public ParticleSystem.Burst[] GetBursts()
        {
            return _bursts;
        }

        public int GetBursts(ParticleSystem.Burst[] bursts)
        {
            int count = Mathf.Min(bursts.Length, _bursts.Length);
            for (int i = 0; i < count; i++)
                bursts[i] = _bursts[i];
            return count;
        }
    }

    public struct ShapeModule
    {
        private ParticleSystem _particleSystem;

        internal ShapeModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = true;
            shapeType = ParticleSystemShapeType.Cone;
            radius = 1f;
            radiusThickness = 1f;
            angle = 25f;
            position = Vector3.zero;
            rotation = Vector3.zero;
            scale = Vector3.one;
            randomDirectionAmount = 0f;
            sphericalDirectionAmount = 0f;
            randomPositionAmount = 0f;
            alignToDirection = false;
            randomDirection = false;
            meshShapeType = ParticleSystemMeshShapeType.Triangle;
            mesh = null;
            meshRenderer = null;
            skinnedMeshRenderer = null;
            useMeshColor = false;
            useMeshMaterialIndex = false;
            meshMaterialIndex = 0;
            normalOffset = 0f;
            texture = null;
            textureClipChannel = 0;
            textureClipThreshold = 0f;
            textureColorAffectsParticles = false;
            textureAlphaAffectsParticles = false;
            textureBilinearFiltering = true;
            textureUVChannel = 0;
            length = 5f;
            boxThickness = new Vector3(1f, 1f, 1f);
            arc = 360f;
            arcThickness = 1f;
            arcSpread = 0f;
            donutRadius = 0.2f;
            radialSpread = 0f;
            directionX = 0f;
            directionY = 1f;
            directionZ = 0f;
        }

        public bool enabled { get; set; }
        public ParticleSystemShapeType shapeType { get; set; }
        public float radius { get; set; }
        public float radiusThickness { get; set; }
        public float angle { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 scale { get; set; }
        public float randomDirectionAmount { get; set; }
        public float sphericalDirectionAmount { get; set; }
        public float randomPositionAmount { get; set; }
        public bool alignToDirection { get; set; }
        public bool randomDirection { get; set; }
        public ParticleSystemMeshShapeType meshShapeType { get; set; }
        public Mesh mesh { get; set; }
        public MeshRenderer meshRenderer { get; set; }
        public SkinnedMeshRenderer skinnedMeshRenderer { get; set; }
        public bool useMeshColor { get; set; }
        public bool useMeshMaterialIndex { get; set; }
        public int meshMaterialIndex { get; set; }
        public float normalOffset { get; set; }
        public Texture2D texture { get; set; }
        public int textureClipChannel { get; set; }
        public float textureClipThreshold { get; set; }
        public bool textureColorAffectsParticles { get; set; }
        public bool textureAlphaAffectsParticles { get; set; }
        public bool textureBilinearFiltering { get; set; }
        public int textureUVChannel { get; set; }
        public float length { get; set; }
        public Vector3 boxThickness { get; set; }
        public float arc { get; set; }
        public float arcThickness { get; set; }
        public float arcSpread { get; set; }
        public float donutRadius { get; set; }
        public float radialSpread { get; set; }
        public float directionX { get; set; }
        public float directionY { get; set; }
        public float directionZ { get; set; }
    }
}