using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
  [Serializable]
  public class UniversalRendererData : ScriptableRendererData
  {
    [Serializable]
    internal enum RenderingMode
    {
      Forward = 0,
      Deferred = 1
    }

    [SerializeField]
    internal RenderingMode m_RenderingMode = RenderingMode.Forward;

    [SerializeField]
    internal int m_OpaqueLayerMask = -1;

    [SerializeField]
    internal int m_TransparentLayerMask = -1;

    [SerializeField]
    internal uint m_ShadowsTransparentReceive = 0;

    [SerializeField]
    internal int m_MaxShadowCastersPerTile = 32;

    [SerializeField]
    internal bool m_SRPBatcher = true;

    [SerializeField]
    internal bool m_SupportsDynamicBatching;

    [SerializeField]
    internal bool m_SupportsHDR = true;

    [SerializeField]
    internal bool m_UseAdaptivePerformance;

    [SerializeField]
    internal bool m_SupportsCameraDepthTexture;

    [SerializeField]
    internal bool m_SupportsCameraOpaqueTexture;

    [SerializeField]
    internal Downsampling m_OpaqueDownsampling = Downsampling._2xBilinear;

    [SerializeField]
    internal bool m_AccurateGbufferNormals;

    [SerializeField]
    internal bool m_ClusteredRendering = true;

    [SerializeField]
    internal bool m_ShadowTransparentReceive = true;

    [SerializeField]
    internal bool m_UseFastSRGBLinearConversion;

    [SerializeField]
    internal float m_RenderScale = 1f;

    [SerializeField]
    internal bool m_MainLightRenderingEnabled = true;

    [SerializeField]
    internal bool m_MainLightShadowsSupported = true;

    [SerializeField]
    internal int m_MainLightShadowmapResolution = 1024;

    [SerializeField]
    internal bool m_AdditionalLightsRenderingEnabled = true;

    [SerializeField]
    internal LightRenderingMode m_AdditionalLightsRenderingMode = LightRenderingMode.PerPixel;

    [SerializeField]
    internal bool m_AdditionalLightShadowsSupported;

    [SerializeField]
    internal int m_AdditionalLightsShadowmapResolution = 512;

    [SerializeField]
    internal int m_AdditionalLightsPerObjectLimit = 8;

    [SerializeField]
    internal bool m_SoftShadowsSupported;

    [SerializeField]
    internal bool m_DepthPrimingMode;

    [SerializeField]
    internal bool m_ShadowCascades;

    [SerializeField]
    internal bool m_ConservativeEnclosingSphere;

    [SerializeField]
    internal int m_NumIterationsEnclosingSphere = 64;

    [SerializeField]
    internal bool m_UseNormalizedShadowResolution;

    [SerializeField]
    internal bool m_NormalizedRoughness;

    [SerializeField]
    internal bool m_FeatureKeepFrameInfo;

    protected override ScriptableRenderer Create()
    {
      return new UniversalRenderer(this);
    }
  }

  public enum LightRenderingMode
  {
    PerVertex = 0,
    PerPixel = 1
  }

  public class UniversalRenderer : ScriptableRenderer
  {
    private readonly UniversalRendererData m_Data;

    public UniversalRenderer(UniversalRendererData data)
    {
      m_Data = data;
    }
  }

  public sealed class ForwardRendererData : UniversalRendererData
  {
  }

  public sealed class ForwardRenderer : ScriptableRenderer
  {
    public ForwardRenderer(ForwardRendererData data) : base()
    {
    }
  }

  public sealed class Renderer2DData : ScriptableRendererData
  {
    protected override ScriptableRenderer Create()
    {
      return new Renderer2D(this);
    }
  }

  public sealed class Renderer2D : ScriptableRenderer
  {
    public Renderer2D(Renderer2DData data) : base()
    {
    }
  }
}
