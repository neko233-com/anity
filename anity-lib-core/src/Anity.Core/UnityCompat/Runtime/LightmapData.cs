namespace UnityEngine;

public class LightmapData
{
    public Texture2D? lightmapColor { get; set; }
    public Texture2D? lightmapDir { get; set; }
    public Texture2D? shadowMask { get; set; }
    public Texture2D? lightmapFar { get; set; }
    public Texture2D? lightmapNear { get; set; }
    public Texture2D? lightmap { get; set; }
    public Texture2D? lightmapShadow { get; set; }
    public Texture2D? lightmapShadowMask { get; set; }
}

public static class LightmapSettings
{
    public static LightmapData[] lightmaps { get; set; } = new LightmapData[0];
    public static LightmapsMode lightmapsMode { get; set; } = LightmapsMode.NonDirectional;
    public static LightProbes? lightProbes { get; set; }
    public static LightmapParameters lightmapParameters { get; set; } = new LightmapParameters();
    public static ColorSpace bakedColorSpace { get; set; } = ColorSpace.Gamma;
    public static ColorSpace lightmapColorSpace
    {
        get => bakedColorSpace;
        set => bakedColorSpace = value;
    }
    public static int quality { get; set; } = 0;
    public static Material? skybox { get; set; }
}

public class LightmapParameters
{
    public float resolution { get; set; } = 1f;
    public float irradianceQuality { get; set; } = 1f;
    public float backFaceTolerance { get; set; } = 0f;
    public float padding { get; set; } = 2f;
    public int quality { get; set; } = 0;
    public float blurRadius { get; set; } = 2f;
    public int directLightQuality { get; set; } = 64;
    public int antiAliasingSamples { get; set; } = 1;
    public float AOQuality { get; set; } = 0f;
    public float AOAntiAliasingSamples { get; set; } = 1f;
    public float bounceBoost { get; set; } = 1f;
    public float bounceIntensity { get; set; } = 1f;
    public Color ambientLight { get; set; } = Color.gray;
    public Color ambientSkyColor { get; set; } = new Color(0.212f, 0.227f, 0.259f);
    public Color ambientEquatorColor { get; set; } = new Color(0.114f, 0.125f, 0.133f);
    public Color ambientGroundColor { get; set; } = new Color(0.047f, 0.043f, 0.035f);
    public float ambientIntensity { get; set; } = 1f;
    public AmbientMode ambientMode { get; set; } = AmbientMode.Trilight;
    public float skyboxIntensity { get; set; } = 1f;
    public Material? skyboxMaterial { get; set; }
    public Cubemap? skyboxCubemap { get; set; }
    public float reflectionIntensity { get; set; } = 1f;
    public int reflectionBounces { get; set; } = 1;
    public float haloStrength { get; set; } = 0.5f;
    public float flareStrength { get; set; } = 1f;
    public float flareFadeSpeed { get; set; } = 3f;
    public float hallowScale { get; set; } = 0.5f;
}

public enum AmbientMode
{
    Skybox,
    Trilight,
    Flat,
    Custom
}
