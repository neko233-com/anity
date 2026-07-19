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
    public enum RenderingMode
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
    internal MsaaQuality m_MSAA = MsaaQuality.Disabled;

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

    [SerializeField]
    internal List<ScriptableRendererFeature> m_RendererFeatures = new List<ScriptableRendererFeature>();

    public int opaqueLayerMask => m_OpaqueLayerMask;
    public int transparentLayerMask => m_TransparentLayerMask;
    public bool supportsCameraDepthTexture => m_SupportsCameraDepthTexture;
    public bool supportsCameraOpaqueTexture => m_SupportsCameraOpaqueTexture;
    public Downsampling opaqueDownsampling => m_OpaqueDownsampling;
    public bool supportsHDR => m_SupportsHDR;
    public MsaaQuality msaaSampleCount => m_MSAA;
    public float renderScale => m_RenderScale;
    public bool supportsDynamicBatching => m_SupportsDynamicBatching;
    public bool srpBatcher => m_SRPBatcher;
    public RenderingMode renderingMode => m_RenderingMode;

    public UniversalRendererData()
    {
    }

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
    internal readonly UniversalRendererData m_Data;
    private DrawObjectsPass m_OpaquePass;
    private DrawObjectsPass m_TransparentPass;

    public UniversalRenderer(UniversalRendererData data)
    {
      m_Data = data;
      InitializeRenderPasses();
    }

    private void InitializeRenderPasses()
    {
      m_OpaquePass = new DrawObjectsPass(
        "Draw Opaque Objects",
        true,
        RenderQueueRange.opaque,
        (uint)(m_Data != null ? m_Data.opaqueLayerMask : -1));
      m_TransparentPass = new DrawObjectsPass(
        "Draw Transparent Objects",
        false,
        RenderQueueRange.transparent,
        (uint)(m_Data != null ? m_Data.transparentLayerMask : -1));
    }

    public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData)
    {
      base.SetupCullingParameters(ref cullingParameters, ref cameraData);
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      // Renderer-data defaults are visible to renderer features during their
      // setup callbacks, just like per-camera requests.
      renderingData.cameraData.requiresDepthTexture |= m_Data?.supportsCameraDepthTexture ?? false;
      renderingData.cameraData.requiresOpaqueTexture |= m_Data?.supportsCameraOpaqueTexture ?? false;
      base.Setup(context, ref renderingData);
      // ScriptableRenderer clears its queue at the start of every camera.
      // Default URP geometry passes must therefore be enqueued per camera,
      // rather than only once in the renderer constructor.
      EnqueuePass(m_OpaquePass);
      EnqueuePass(m_TransparentPass);
      UpdateCameraInputRequirements(ref renderingData);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      ExecuteRenderPassQueue(context, ref renderingData, drawSkybox: true);
    }
  }

  public sealed class ForwardRendererData : UniversalRendererData
  {
    protected override ScriptableRenderer Create()
    {
      return new ForwardRenderer(this);
    }
  }

  public sealed class ForwardRenderer : UniversalRenderer
  {
    public ForwardRenderer(ForwardRendererData data) : base(data)
    {
    }
  }

  public sealed class Renderer2DData : ScriptableRendererData
  {
    [SerializeField] internal bool m_SupportsHDR = false;
    [SerializeField] internal float m_RenderScale = 1f;
    [SerializeField] internal MsaaQuality m_MSAA = MsaaQuality.Disabled;
    [SerializeField] internal int m_Light2DLayerMask = -1;

    public bool supportsHDR => m_SupportsHDR;
    public float renderScale => m_RenderScale;
    public MsaaQuality msaaSampleCount => m_MSAA;
    public int light2DLayerMask => m_Light2DLayerMask;

    protected override ScriptableRenderer Create()
    {
      return new Renderer2D(this);
    }
  }

  public sealed class Renderer2D : ScriptableRenderer
  {
    private readonly Renderer2DData m_Data;
    private DrawObjectsPass m_SpritePass;

    public Renderer2D(Renderer2DData data)
    {
      m_Data = data;
      Initialize2DPasses();
    }

    private void Initialize2DPasses()
    {
      m_SpritePass = new DrawObjectsPass(
        "Draw 2D Sprites",
        new[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("Universal2D") },
        true,
        RenderQueueRange.all,
        (uint)(m_Data != null ? m_Data.light2DLayerMask : -1));
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      base.Setup(context, ref renderingData);
      EnqueuePass(m_SpritePass);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      ExecuteRenderPassQueue(context, ref renderingData, drawSkybox: false);
    }
  }

  public sealed class Light2D : Behaviour
  {
    public Light2D()
    {
    }

    public Color color { get; set; } = Color.white;
    public float intensity { get; set; } = 1f;
    public float pointLightOuterRadius { get; set; } = 1f;
    public float pointLightInnerRadius { get; set; } = 0f;
    public float pointLightOuterAngle { get; set; } = 360f;
    public float pointLightInnerAngle { get; set; } = 360f;
    public Light2DType lightType { get; set; } = Light2DType.Point;
    public float shadowIntensity { get; set; } = 0.5f;
    public int shadowVolumeIntensity { get; set; } = 0;
    public bool alphaBlendOnOverlap { get; set; } = true;
    public float falloffIntensity { get; set; } = 1f;
    public int targetSortingLayer { get; set; }
    public int blendStyleIndex { get; set; }
  }

  public enum Light2DType
  {
    Parametric = 0,
    Freeform = 1,
    Sprite = 2,
    Point = 3,
    Global = 4
  }

  public static class SortingLayer
  {
    public static int layersCount => 6;

    public static SortingLayerInfo[] layers => new SortingLayerInfo[]
    {
      new SortingLayerInfo { id = 0, name = "Default", value = 0 },
      new SortingLayerInfo { id = 1, name = "Background", value = 1000 },
      new SortingLayerInfo { id = 2, name = "Geometry", value = 2000 },
      new SortingLayerInfo { id = 3, name = "TransparentFX", value = 3000 },
      new SortingLayerInfo { id = 4, name = "Ignore Raycast", value = 4000 },
      new SortingLayerInfo { id = 5, name = "UI", value = 5000 }
    };

    public static int GetLayerValueFromID(int id) => id * 1000;
    public static int NameToID(string name) => layers[0].id;
    public static string IDToName(int id) => "Default";
    public static bool IsValid(int id) => true;
  }

  public struct SortingLayerInfo
  {
    public int id;
    public string name;
    public int value;
  }

  public class SortingGroup : Behaviour
  {
    public int sortingLayerID { get; set; }
    public string sortingLayerName { get; set; } = "Default";
    public int sortingOrder { get; set; }
    public SortingGroup invalidSortingGroup => null;
  }
}
