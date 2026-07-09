namespace UnityEngine;

public partial class ParticleSystem
{
    public struct TextureSheetAnimationModule
    {
        private ParticleSystem _particleSystem;

        internal TextureSheetAnimationModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            mode = ParticleSystemAnimationMode.Grid;
            tilesX = 1;
            tilesY = 1;
            animation = ParticleSystemAnimationType.WholeSheet;
            rowIndex = 0;
            frameOverTime = new ParticleSystem.MinMaxCurve(1f);
            startFrame = new ParticleSystem.MinMaxCurve(0f);
            cycles = 1;
            channel = 0;
            uvChannel = 0;
            enabled = false;
            flipU = 0f;
            flipV = 0f;
            frameOverTimeMultiplier = 1f;
            startFrameMultiplier = 1f;
            _sprites = new Sprite[0];
            _spritesCount = 0;
        }

        public bool enabled { get; set; }
        public ParticleSystemAnimationMode mode { get; set; }
        public int tilesX { get; set; }
        public int tilesY { get; set; }
        public ParticleSystemAnimationType animation { get; set; }
        public int rowIndex { get; set; }
        public ParticleSystem.MinMaxCurve frameOverTime { get; set; }
        public ParticleSystem.MinMaxCurve startFrame { get; set; }
        public float frameOverTimeMultiplier { get; set; }
        public float startFrameMultiplier { get; set; }
        public int cycles { get; set; }
        public int channel { get; set; }
        public int uvChannel { get; set; }
        public float flipU { get; set; }
        public float flipV { get; set; }
        public ParticleSystemAnimationTimeMode timeMode { get; set; }
        public float fps { get; set; }
        public bool speedRange { get; set; }
        public Vector2 speedRangeValue { get; set; }

        private Sprite[] _sprites;
        private int _spritesCount;

        public int spriteCount => _spritesCount;

        public Sprite GetSprite(int index)
        {
            return _sprites[index];
        }

        public void SetSprite(int index, Sprite sprite)
        {
            _sprites[index] = sprite;
        }

        public void SetSprites(Sprite[] sprites)
        {
            SetSprites(sprites, sprites.Length);
        }

        public void SetSprites(Sprite[] sprites, int count)
        {
            _sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
                _sprites[i] = sprites[i];
            _spritesCount = count;
        }

        public int GetSprites(Sprite[] sprites)
        {
            int count = Mathf.Min(sprites.Length, _spritesCount);
            for (int i = 0; i < count; i++)
                sprites[i] = _sprites[i];
            return count;
        }
    }

    public struct LightsModule
    {
        private ParticleSystem _particleSystem;

        internal LightsModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            light = null;
            ratio = 1f;
            useParticleColor = false;
            useRandomDistribution = true;
            sizeAffectsRange = true;
            alphaAffectsIntensity = true;
            range = new ParticleSystem.MinMaxCurve(1f);
            rangeMultiplier = 1f;
            intensity = new ParticleSystem.MinMaxCurve(1f);
            intensityMultiplier = 1f;
            maxLights = 20;
            mixLights = false;
            shadowStrength = 1f;
            shadowAngle = 0f;
        }

        public bool enabled { get; set; }
        public Light light { get; set; }
        public float ratio { get; set; }
        public bool useParticleColor { get; set; }
        public bool useRandomDistribution { get; set; }
        public bool sizeAffectsRange { get; set; }
        public bool alphaAffectsIntensity { get; set; }
        public ParticleSystem.MinMaxCurve range { get; set; }
        public float rangeMultiplier { get; set; }
        public ParticleSystem.MinMaxCurve intensity { get; set; }
        public float intensityMultiplier { get; set; }
        public int maxLights { get; set; }
        public bool mixLights { get; set; }
        public float shadowStrength { get; set; }
        public float shadowAngle { get; set; }
    }

    public struct TrailModule
    {
        private ParticleSystem _particleSystem;

        internal TrailModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
            mode = ParticleSystemTrailMode.Ratio;
            ratio = 1f;
            lifetime = new ParticleSystem.MinMaxCurve(1f);
            lifetimeMultiplier = 1f;
            minVertexDistance = 0.2f;
            textureScale = 1f;
            widthOverTrail = new ParticleSystem.MinMaxCurve(1f);
            widthOverTrailMultiplier = 1f;
            colorOverTrail = new ParticleSystem.MinMaxGradient(Color.white);
            colorOverLifetime = new ParticleSystem.MinMaxGradient(Color.white);
            dieWithParticles = true;
            sizeAffectsWidth = true;
            sizeAffectsLifetime = false;
            inheritParticleColor = false;
            generateLightingData = false;
            shadowBias = 0.5f;
            ribbonCount = 1;
            ribbongapSize = 0.25f;
            ribbonShape = ParticleSystemTrailRibbonShape.Default;
            ribbonCount = 1;
        }

        public bool enabled { get; set; }
        public ParticleSystemTrailMode mode { get; set; }
        public float ratio { get; set; }
        public ParticleSystem.MinMaxCurve lifetime { get; set; }
        public float lifetimeMultiplier { get; set; }
        public float minVertexDistance { get; set; }
        public float textureScale { get; set; }
        public ParticleSystem.MinMaxCurve widthOverTrail { get; set; }
        public float widthOverTrailMultiplier { get; set; }
        public ParticleSystem.MinMaxGradient colorOverTrail { get; set; }
        public ParticleSystem.MinMaxGradient colorOverLifetime { get; set; }
        public bool dieWithParticles { get; set; }
        public bool sizeAffectsWidth { get; set; }
        public bool sizeAffectsLifetime { get; set; }
        public bool inheritParticleColor { get; set; }
        public bool generateLightingData { get; set; }
        public float shadowBias { get; set; }
        public int ribbonCount { get; set; }
        public float ribbongapSize { get; set; }
        public ParticleSystemTrailRibbonShape ribbonShape { get; set; }
    }

    public struct CustomDataModule
    {
        private ParticleSystem _particleSystem;

        internal CustomDataModule(ParticleSystem ps)
        {
            _particleSystem = ps;
            enabled = false;
        }

        public bool enabled { get; set; }

        public ParticleSystemCustomDataMode GetMode(ParticleSystemCustomData stream)
        {
            return ParticleSystemCustomDataMode.None;
        }

        public void SetMode(ParticleSystemCustomData stream, ParticleSystemCustomDataMode mode)
        {
        }

        public ParticleSystem.MinMaxCurve GetVectorComponent(ParticleSystemCustomData stream, int componentIndex)
        {
            return new ParticleSystem.MinMaxCurve(0f);
        }

        public void SetVectorComponent(ParticleSystemCustomData stream, int componentIndex, ParticleSystem.MinMaxCurve curve)
        {
        }

        public ParticleSystem.MinMaxGradient GetColor(ParticleSystemCustomData stream)
        {
            return new ParticleSystem.MinMaxGradient(Color.white);
        }

        public void SetColor(ParticleSystemCustomData stream, ParticleSystem.MinMaxGradient gradient)
        {
        }

        public void SetColor(ParticleSystemCustomData stream, ParticleSystem.MinMaxGradient gradient, ParticleSystemGradientMode mode)
        {
        }

        public ParticleSystemGradientMode GetColorMode(ParticleSystemCustomData stream)
        {
            return ParticleSystemGradientMode.Gradient;
        }

        public float GetVectorComponentMultiplier(ParticleSystemCustomData stream, int componentIndex)
        {
            return 1f;
        }

        public void SetVectorComponentMultiplier(ParticleSystemCustomData stream, int componentIndex, float multiplier)
        {
        }
    }
}

public class ParticleSystemForceField : Component
{
    public ParticleSystemForceFieldShape shape;
    public Vector3 startRange;
    public Vector3 endRange;
    public float strength;
    public float rotationSpeed;
    public float rotationAttraction;
    public float drag;
    public Vector3 gravity;
    public ParticleSystemAnimationTimeMode timeMode;
    public bool multiplyDragByParticleSize;
    public bool multiplyDragByParticleSpeed;

    public ParticleSystemForceField()
    {
        shape = ParticleSystemForceFieldShape.Sphere;
        startRange = Vector3.zero;
        endRange = Vector3.one;
        strength = 5f;
        rotationSpeed = 0f;
        rotationAttraction = 0.5f;
        drag = 0f;
        gravity = Vector3.zero;
        timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        multiplyDragByParticleSize = false;
        multiplyDragByParticleSpeed = false;
    }
}