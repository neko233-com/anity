using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
  public class UniversalRenderPipelineAsset : RenderPipelineAsset
  {
    [SerializeField] private string m_PipelineTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipeline";
    [SerializeField] internal ScriptableRendererData[] m_RendererDataList = new ScriptableRendererData[1];
    [SerializeField] internal int m_DefaultRendererIndex;

    public override Type pipelineType => typeof(UniversalRenderPipeline);

    public string pipelineTypeName
    {
      get => m_PipelineTypeName;
      set => m_PipelineTypeName = value;
    }

    public ScriptableRendererData[] rendererDataList
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
    public bool supportsDynamicBatching = false;
    public bool supportsGPUInstancing = true;
    public bool supportsSRPBatcher = true;
    public bool useAdaptivePerformance = false;
    public bool srpBatcher => supportsSRPBatcher;

    public ShadowQuality shadows = ShadowQuality.All;
    public ShadowResolution shadowResolution = ShadowResolution.Medium;
    public float shadowDistance = 40f;
    public int shadowCascades = 1;
    public float cascade2Split = 1f / 3f;
    public Vector3 cascade4Split = new Vector3(1f / 5f, 2f / 5f, 3f / 5f);
    public float shadowNearPlaneOffset = 2f;
    public float shadowCascadeBorder = 0.25f;

    public bool supportsMainLightShadows = true;
    public int mainLightShadowmapResolution = 1024;
    public LightShadowCasterMode mainLightShadowCasterMode = LightShadowCasterMode.Default;

    public bool supportsAdditionalLightShadows = false;
    public int additionalLightsShadowmapResolution = 512;
    public LightShadowCasterMode additionalLightsShadowCasterMode = LightShadowCasterMode.Default;
    public int additionalLightsCount = 8;

    public bool supportsSoftShadows = true;
    public int maxAdditionalLightsCount = 8;

    public bool useFastSRGBLinearConversion = false;
    public MsaaQuality msaaSampleCount = MsaaQuality.Disabled;
    public float renderScale = 1.0f;

    public CameraRenderType defaultRendererType = CameraRenderType.Base;

    private Material m_DefaultMaterial;
    private Material m_DefaultParticleMaterial;
    private Material m_DefaultTerrainMaterial;
    private Material m_DefaultUIMaterial;
    private Material m_Default2DMaterial;
    private Shader m_DefaultShader;

    public override Material defaultMaterial
    {
      get
      {
        if (m_DefaultMaterial == null)
        {
          m_DefaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
        return m_DefaultMaterial;
      }
    }

    public override Shader defaultShader
    {
      get
      {
        if (m_DefaultShader == null)
        {
          m_DefaultShader = Shader.Find("Universal Render Pipeline/Lit");
        }
        return m_DefaultShader;
      }
    }

    public override Material defaultParticleMaterial
    {
      get
      {
        if (m_DefaultParticleMaterial == null)
        {
          m_DefaultParticleMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        }
        return m_DefaultParticleMaterial;
      }
    }

    public override Material defaultTerrainMaterial
    {
      get
      {
        if (m_DefaultTerrainMaterial == null)
        {
          m_DefaultTerrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        }
        return m_DefaultTerrainMaterial;
      }
    }

    public override Material defaultUIMaterial
    {
      get
      {
        if (m_DefaultUIMaterial == null)
        {
          m_DefaultUIMaterial = new Material(Shader.Find("Universal Render Pipeline/UI/Default"));
        }
        return m_DefaultUIMaterial;
      }
    }

    public override Material default2DMaterial
    {
      get
      {
        if (m_Default2DMaterial == null)
        {
          m_Default2DMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        return m_Default2DMaterial;
      }
    }

    public UniversalRenderPipelineAsset()
    {
      EnsureDefaultRenderer();
    }

    private void EnsureDefaultRenderer()
    {
      if (m_RendererDataList == null || m_RendererDataList.Length == 0 || m_RendererDataList[0] == null)
      {
        m_RendererDataList = new ScriptableRendererData[1];
        var forwardRendererData = new ForwardRendererData();
        m_RendererDataList[0] = forwardRendererData;
      }
      m_DefaultRendererIndex = 0;
    }

    protected override RenderPipeline CreatePipeline()
    {
      EnsureDefaultRenderer();
      return new UniversalRenderPipeline(this);
    }

    internal void OnValidate()
    {
      if (m_RendererDataList == null || m_RendererDataList.Length == 0)
      {
        EnsureDefaultRenderer();
      }
      else if (m_RendererDataList[0] == null)
      {
        EnsureDefaultRenderer();
      }

      m_DefaultRendererIndex = Mathf.Clamp(m_DefaultRendererIndex, 0, m_RendererDataList.Length - 1);
    }
  }

  public class UniversalRenderPipeline : RenderPipeline
  {
    private static readonly Lazy<UniversalRenderPipeline> s_Instance = new(() => new UniversalRenderPipeline(null));
    private UniversalRenderPipelineAsset m_Asset;
    private ScriptableRenderer m_Renderer;

    public static UniversalRenderPipeline instance => s_Instance.Value;

    public static bool isActive => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

    public static UniversalRenderPipelineAsset asset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

    public ScriptableRenderer renderer => m_Renderer;

    public UniversalRenderPipeline() : this(null) { }

    public UniversalRenderPipeline(UniversalRenderPipelineAsset asset)
    {
      m_Asset = asset;
      InitializeRenderer();
    }

    private void InitializeRenderer()
    {
      if (m_Asset != null && m_Asset.m_RendererDataList != null && m_Asset.m_RendererDataList.Length > 0)
      {
        var rendererData = m_Asset.m_RendererDataList[m_Asset.m_DefaultRendererIndex];
        if (rendererData != null)
        {
          m_Renderer = rendererData.InternalCreateRenderer();
          return;
        }
      }
      var defaultData = new ForwardRendererData();
      m_Renderer = defaultData.InternalCreateRenderer();
    }

    protected override void ExecuteRender(ScriptableRenderContext context, Camera camera)
    {
      beginCameraRendering?.Invoke(context, camera);
      RenderSingleCamera(context, camera);
      endCameraRendering?.Invoke(context, camera);
    }

    public static void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
      if (camera == null) return;

      var asset = UniversalRenderPipeline.asset;
      var pipeline = asset != null ? GraphicsSettings.currentRenderPipelineInstance as UniversalRenderPipeline : instance;
      if (pipeline == null || pipeline.m_Renderer == null)
      {
        context.Submit();
        return;
      }

      var renderingData = new RenderingData();
      var cameraData = new CameraData
      {
        camera = camera,
        cameraType = camera.cameraType,
        isSceneViewCamera = camera.cameraType == CameraType.SceneView,
        isHdrEnabled = asset != null ? asset.supportsHDR && camera.allowHDR : camera.allowHDR,
        renderScale = asset != null ? asset.renderScale : 1.0f,
        msaaSamples = (int)(asset != null ? asset.msaaSampleCount : MsaaQuality.Disabled)
      };

      cameraData.cameraTargetDescriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight)
      {
        msaaSamples = cameraData.msaaSamples,
        colorFormat = cameraData.isHdrEnabled ? UnityEngine.RenderTextureFormat.DefaultHDR : UnityEngine.RenderTextureFormat.Default,
        depthBufferBits = 32,
        sRGB = QualitySettings.activeColorSpace == ColorSpace.Gamma
      };

      renderingData.cameraData = cameraData;
      renderingData.supportsDynamicBatching = asset != null && asset.supportsDynamicBatching;
      renderingData.supportsInstancing = asset != null && asset.supportsGPUInstancing;
      renderingData.perObjectData = PerObjectData.None;

      var shadowData = new ShadowData
      {
        shadowDistance = asset != null ? asset.shadowDistance : QualitySettings.shadowDistance,
        supportsSoftShadows = asset != null && asset.supportsSoftShadows
      };
      renderingData.shadowData = shadowData;

      var cullingParameters = new ScriptableCullingParameters
      {
        camera = camera,
        shadowDistance = shadowData.shadowDistance
      };

      pipeline.m_Renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);

      context.Cull(ref cullingParameters, out var cullingResults);
      renderingData.cullResults = cullingResults;

      var lightData = new LightData
      {
        mainLightIndex = GetMainLightIndex(cullingResults),
        visibleLights = cullingResults.visibleLights
      };
      renderingData.lightData = lightData;

      context.SetupCameraProperties(camera);

      CommandBuffer cmd = CommandBufferPool.Get("Clear Render Target");
      cmd.ClearRenderTarget(true, true, camera.backgroundColor);
      context.ExecuteCommandBuffer(cmd);
      CommandBufferPool.Release(cmd);

      pipeline.m_Renderer.Setup(context, ref renderingData);
      pipeline.m_Renderer.Execute(context, ref renderingData);

      context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
      context.DrawUIOverlay(camera);
      context.DrawGizmos(camera, GizmoSubset.PostImageEffects);

      context.Submit();
    }

    private static int GetMainLightIndex(CullingResults cullingResults)
    {
      if (cullingResults.visibleLights == null) return -1;
      for (int i = 0; i < cullingResults.visibleLights.Length; i++)
      {
        if (cullingResults.visibleLights[i].lightType == LightType.Directional)
          return i;
      }
      return cullingResults.visibleLights.Length > 0 ? 0 : -1;
    }

    public static event Action<ScriptableRenderContext, Camera> beginCameraRendering;
    public static event Action<ScriptableRenderContext, Camera> endCameraRendering;
    public static event Action<ScriptableRenderContext, IList<Camera>> beginFrameRendering;
    public static event Action<ScriptableRenderContext, IList<Camera>> endFrameRendering;

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      m_Renderer?.Dispose();
    }
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

  public struct CameraData
  {
    public Camera camera;
    public RenderTextureDescriptor cameraTargetDescriptor;
    public CameraType cameraType;
    public bool isSceneViewCamera;
    public bool isHdrEnabled;
    public int msaaSamples;
    public float renderScale;
    public bool isSceneViewFx;
  }

  public struct LightData
  {
    public int mainLightIndex;
    public VisibleLight[] visibleLights;
    public int additionalLightsCount;
    public int maxPerObjectAdditionalLightsCount;
    public bool supportsAdditionalLights;
    public bool supportsMixedLighting;
  }

  public struct ShadowData
  {
    public float shadowDistance;
    public bool supportsSoftShadows;
    public int mainLightShadowmapResolution;
    public int additionalLightsShadowmapResolution;
    public bool supportsMainLightShadows;
    public bool supportsAdditionalLightShadows;
    public int shadowCascadesCount;
    public float cascade2Split;
    public Vector3 cascade4Split;
  }

  public struct RenderingData
  {
    public CameraData cameraData;
    public CullingResults cullResults;
    public LightData lightData;
    public ShadowData shadowData;
    public bool supportsDynamicBatching;
    public bool supportsInstancing;
    public PerObjectData perObjectData;
    public bool postProcessingEnabled;
  }
}
