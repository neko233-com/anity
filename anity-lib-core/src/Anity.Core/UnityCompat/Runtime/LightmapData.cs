namespace UnityEngine;

public class LightmapData
{
  public Texture2D? lightmapColor { get; set; }
  public Texture2D? lightmapDir { get; set; }
  public Texture2D? lightmapShadowMask { get; set; }
  public Texture2D? shadowMask { get; set; }
}

public static class LightmapSettings
{
  public static LightmapData[] lightmaps { get; set; } = new LightmapData[0];
  public static LightmapsMode lightmapsMode { get; set; } = LightmapsMode.NonDirectional;
  public static LightmapParameters lightmapParameters { get; set; } = new LightmapParameters();
  public static Material? skybox { get; set; }
  public static Light[] additiveLights { get; set; } = new Light[0];
  public static Light[] directionalLights { get; set; } = new Light[0];
  public static Light[] pointLights { get; set; } = new Light[0];
  public static Light[] spotLights { get; set; } = new Light[0];
  public static Light[] areaLights { get; set; } = new Light[0];
  public static Light[] otherLights { get; set; } = new Light[0];
  public static Light[] reflectionProbes { get; set; } = new Light[0];
  public static Light[] reflectionProbeVolumes { get; set; } = new Light[0];
  public static Light[] fog { get; set; } = new Light[0];
  public static Light[] ambientLight { get; set; } = new Light[0];
  public static Light[] halo { get; set; } = new Light[0];
  public static Light[] lensFlares { get; set; } = new Light[0];
  public static Light[] proceduralTextures { get; set; } = new Light[0];
  public static Light[] proceduralSkybox { get; set; } = new Light[0];
  public static Light[] proceduralReflectionProbes { get; set; } = new Light[0];
}

public class LightmapParameters
{
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
