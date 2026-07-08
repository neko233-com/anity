namespace UnityEngine;

public class Light : Behaviour
{
  public LightType type { get; set; } = LightType.Spot;
  public Color color { get; set; } = Color.white;
  public float intensity { get; set; } = 1f;
  public float range { get; set; } = 10f;
  public float spotAngle { get; set; } = 30f;
  public float innerSpotAngle { get; set; } = 20f;
  public float cookieSize { get; set; } = 10f;
  public Texture? cookie { get; set; }
  public LightShadows shadows { get; set; } = LightShadows.None;
  public float shadowStrength { get; set; } = 1f;
  public float shadowBias { get; set; } = 0.05f;
  public float shadowNormalBias { get; set; } = 0.4f;
  public float shadowNearPlane { get; set; } = 0.2f;
  public float bounceIntensity { get; set; } = 1f;
  public float colorTemperature { get; set; } = 6570f;
  public bool useColorTemperature { get; set; }
  public int renderingLayerMask { get; set; } = 1;
  public LightmapBakeType lightmapBakeType { get; set; } = LightmapBakeType.Realtime;
  public LightmappingMode mapping { get; set; } = LightmappingMode.Auto;
  public int cookieLightID { get; set; }
  public bool useViewFrustumForShadowCasterCull { get; set; } = true;
  public int renderMode { get; set; }
  public int cullingMask { get; set; } = -1;

  public void GetCommandBuffer()
  {
  }
}

public enum LightType
{
  Spot = 0,
  Directional = 1,
  Point = 2,
  Area = 3,
  Rectangle = 3,
  Disc = 4
}

public enum LightShadows
{
  None = 0,
  Hard = 1,
  Soft = 2
}

public enum LightmapBakeType
{
  Realtime = 4,
  Baked = 2,
  Mixed = 1
}

public enum LightmappingMode
{
  Auto = 0,
  Realtime = 4,
  Baked = 2,
  Mixed = 1
}
