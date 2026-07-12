using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine;

public class Light : Behaviour
{
  private readonly List<CommandBuffer> _commandBuffers = new();

  public LightType type { get; set; } = LightType.Point;
  public Color color { get; set; } = Color.white;
  public float intensity { get; set; } = 1f;
  public float range { get; set; } = 10f;
  public float spotAngle { get; set; } = 30f;
  public float innerSpotAngle { get; set; } = 20f;
  public float cookieSize { get; set; } = 10f;
  public Texture? cookie { get; set; }
  public LightShadows shadows { get; set; } = LightShadows.None;
  public float shadowStrength { get; set; } = 1f;
  public ShadowResolution shadowResolution { get; set; } = ShadowResolution.Medium;
  public float shadowBias { get; set; } = 0.05f;
  public float shadowNormalBias { get; set; } = 0.4f;
  public float shadowNearPlane { get; set; } = 0.2f;
  public int shadowCustomResolution { get; set; }
  public float bounceIntensity { get; set; } = 1f;
  public float colorTemperature { get; set; } = 6570f;
  public bool useColorTemperature { get; set; }
  public int renderingLayerMask { get; set; } = 1;
  public LightmapBakeType lightmapBakeType { get; set; } = LightmapBakeType.Realtime;
  public LightmappingMode mapping { get; set; } = LightmappingMode.Auto;
  public int cookieLightID { get; set; }
  public bool useViewFrustumForShadowCasterCull { get; set; } = true;
  public LightRenderMode renderMode { get; set; } = LightRenderMode.Auto;
  public int cullingMask { get; set; } = -1;
  public bool alreadyLightmapped { get; set; }
  public Vector2 areaSize { get; set; } = new(1f, 1f);
  public Flare flare { get; set; }
  public Bounds bounds => new(transform.position, new Vector3(range * 2f, range * 2f, range * 2f));
  public int commandBufferCount => _commandBuffers.Count;

  public void AddCommandBuffer(LightEvent evt, CommandBuffer buffer)
  {
    if (buffer != null && !_commandBuffers.Contains(buffer))
      _commandBuffers.Add(buffer);
  }

  public void AddCommandBufferAsync(LightEvent evt, CommandBuffer buffer, ComputeQueueType queueType)
  {
    AddCommandBuffer(evt, buffer);
  }

  public void RemoveCommandBuffer(LightEvent evt, CommandBuffer buffer)
  {
    _commandBuffers.Remove(buffer);
  }

  public void RemoveCommandBuffers(LightEvent evt)
  {
    _commandBuffers.Clear();
  }

  public CommandBuffer[] GetCommandBuffers(LightEvent evt)
  {
    return _commandBuffers.ToArray();
  }

  public void RemoveAllCommandBuffers()
  {
    _commandBuffers.Clear();
  }

  public static Light[] GetLights(LightType type, int layer)
  {
    var all = FindObjectsOfType<Light>();
    var result = new List<Light>();
    for (var i = 0; i < all.Length; i++)
      if (all[i].type == type && (all[i].cullingMask & (1 << layer)) != 0)
        result.Add(all[i]);
    return result.ToArray();
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

public enum LightRenderMode
{
  Auto = 0,
  ForcePixel = 1,
  ForceVertex = 2
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
