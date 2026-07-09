using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
  public class UniversalRenderPipelineAsset : RenderPipelineAsset
  {
    [SerializeField] private string m_PipelineTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipeline";
    [SerializeField] internal UniversalRendererData[] m_RendererDataList = new UniversalRendererData[1];
    [SerializeField] internal int m_DefaultRendererIndex;

    public override Type pipelineType => typeof(UniversalRenderPipeline);

    public string pipelineTypeName
    {
      get => m_PipelineTypeName;
      set => m_PipelineTypeName = value;
    }

    public UniversalRendererData[] rendererDataList
    {
      get => m_RendererDataList;
      set
      {
        m_RendererDataList = value;
        OnValidate();
      }
    }

    public int defaultRendererIndex
    {
      get => m_DefaultRendererIndex;
      set
      {
        m_DefaultRendererIndex = value;
        OnValidate();
      }
    }

    public bool supportsHDR = true;
    public bool supportsDynamicBatching = true;
    public bool supportsSRPBatcher = true;
    public bool useAdaptivePerformance = false;

    public ShadowQuality shadows = ShadowQuality.All;
    public ShadowResolution shadowResolution = ShadowResolution.Medium;
    public float shadowDistance = 40f;
    public int shadowCascades = 2;
    public float shadowCascade2Split = 1f / 3f;
    public Vector3 shadowCascade4Split = new Vector3(1f / 5f, 2f / 5f, 3f / 5f);
    public float shadowNearPlaneOffset = 2f;
    public float shadowCascadeBorder = 0.25f;

    public bool supportsMainLightShadows = true;
    public int mainLightShadowmapResolution = 1024;
    public LightShadowCasterMode mainLightShadowCasterMode = LightShadowCasterMode.Default;

    public bool supportsAdditionalLightShadows = true;
    public int additionalLightsShadowmapResolution = 512;
    public LightShadowCasterMode additionalLightsShadowCasterMode = LightShadowCasterMode.Default;
    public int additionalLightsCount = 8;

    public bool supportsSoftShadows = true;
    public int maxAdditionalLightsCount = 8;

    public bool useFastSRGBLinearConversion = false;

    public CameraRenderType defaultRendererType = CameraRenderType.Overlay;

    protected override RenderPipeline CreatePipeline()
    {
      return new UniversalRenderPipeline();
    }

    internal void OnValidate()
    {
      if (m_RendererDataList == null || m_RendererDataList.Length == 0)
      {
        m_RendererDataList = new UniversalRendererData[1];
      }

      m_DefaultRendererIndex = Mathf.Clamp(m_DefaultRendererIndex, 0, m_RendererDataList.Length - 1);
    }
  }

  public class UniversalRenderPipeline : RenderPipeline
  {
    private static readonly Lazy<UniversalRenderPipeline> s_Instance = new(() => new UniversalRenderPipeline());

    public static UniversalRenderPipeline instance => s_Instance.Value;

    public static bool isActive => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

    public static UniversalRenderPipelineAsset asset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

    public UniversalRenderPipeline() { }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
      BeginFrameRendering(context, cameras);
      foreach (var camera in cameras)
      {
        BeginCameraRendering(context, camera);
        RenderSingleCamera(context, camera);
        EndCameraRendering(context, camera);
      }
      EndFrameRendering(context, cameras);
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
      BeginFrameRendering(context, cameras);
      foreach (var camera in cameras)
      {
        BeginCameraRendering(context, camera);
        RenderSingleCamera(context, camera);
        EndCameraRendering(context, camera);
      }
      EndFrameRendering(context, cameras);
    }

    public static void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
    }

    public static event Action<ScriptableRenderContext, Camera> beginCameraRendering;
    public static event Action<ScriptableRenderContext, Camera> endCameraRendering;
    public static event Action<ScriptableRenderContext, Camera[]> beginFrameRendering;
    public static event Action<ScriptableRenderContext, Camera[]> endFrameRendering;

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
      beginCameraRendering?.Invoke(context, camera);
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
      endCameraRendering?.Invoke(context, camera);
    }

    private void BeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
    {
      beginFrameRendering?.Invoke(context, cameras);
    }

    private void EndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
    {
      endFrameRendering?.Invoke(context, cameras);
    }
  }

  public enum ShadowQuality
  {
    Disable,
    HardShadows,
    All
  }

  public enum ShadowResolution
  {
    Low = 256,
    Medium = 512,
    High = 1024,
    VeryHigh = 2048
  }

  public enum LightShadowCasterMode
  {
    Default,
    NonLightmappedOnly,
    Everything
  }

  public enum CameraRenderType
  {
    Base,
    Overlay
  }

  public enum MsaaQuality
  {
    Disabled = 1,
    _2x = 2,
    _4x = 4,
    _8x = 8
  }

  public enum RenderScaleMode
  {
    ScaleFactor,
    FixedDpi
  }

  public enum Downsampling
  {
    None,
    _2xBilinear,
    _4xBox,
    _4xBilinear
  }
}
