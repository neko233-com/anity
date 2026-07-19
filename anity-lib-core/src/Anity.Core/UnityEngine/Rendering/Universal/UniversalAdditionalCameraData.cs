using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
  /// <summary>
  /// URP camera-specific settings.  Base cameras own a stack; overlay cameras
  /// are rendered into the base camera's target without clearing its color.
  /// </summary>
  public sealed class UniversalAdditionalCameraData : Behaviour
  {
    private static readonly ConditionalWeakTable<Camera, UniversalAdditionalCameraData> s_DetachedCameraData = new();
    private readonly List<Camera> m_CameraStack = new();
    private CameraRenderType m_RenderType = CameraRenderType.Base;
    private int m_RendererIndex = -1;
    private bool m_RenderPostProcessing = true;
    private bool m_ClearDepth = true;
    private bool m_RequiresDepthTexture;
    private bool m_RequiresColorTexture;

    public CameraRenderType renderType
    {
      get => m_RenderType;
      set => m_RenderType = value;
    }

    /// <summary>-1 selects the pipeline asset's default renderer.</summary>
    public int rendererIndex
    {
      get => m_RendererIndex;
      set => m_RendererIndex = value;
    }

    public List<Camera> cameraStack => m_CameraStack;
    public bool renderPostProcessing { get => m_RenderPostProcessing; set => m_RenderPostProcessing = value; }
    public bool clearDepth { get => m_ClearDepth; set => m_ClearDepth = value; }
    public bool requiresDepthTexture { get => m_RequiresDepthTexture; set => m_RequiresDepthTexture = value; }
    public bool requiresColorTexture { get => m_RequiresColorTexture; set => m_RequiresColorTexture = value; }

    internal static UniversalAdditionalCameraData GetOrCreate(Camera camera)
    {
      if (camera == null) throw new ArgumentNullException(nameof(camera));
      var componentData = camera.GetComponent<UniversalAdditionalCameraData>();
      return componentData ?? s_DetachedCameraData.GetValue(camera, _ => new UniversalAdditionalCameraData());
    }

    internal List<Camera> GetValidatedOverlayStack(Camera baseCamera)
    {
      var result = new List<Camera>(m_CameraStack.Count);
      var seen = new HashSet<Camera>();
      foreach (var overlay in m_CameraStack)
      {
        if (overlay == null || overlay == baseCamera || !overlay.enabled || !seen.Add(overlay))
          continue;

        if (GetOrCreate(overlay).renderType == CameraRenderType.Overlay)
          result.Add(overlay);
      }
      return result;
    }
  }

  /// <summary>URP 14.x camera extension entry point.</summary>
  public static class CameraExtensions
  {
    public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera)
    {
      return UniversalAdditionalCameraData.GetOrCreate(camera);
    }
  }
}
