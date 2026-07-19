using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

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
        m_DefaultRendererIndex = 0;
      }
      else
      {
        m_DefaultRendererIndex = Mathf.Clamp(m_DefaultRendererIndex, 0, m_RendererDataList.Length - 1);
      }
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
    private readonly Dictionary<int, ScriptableRenderer> m_Renderers = new();

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
      m_Renderer = GetRenderer(m_Asset?.m_DefaultRendererIndex ?? 0);
    }

    private ScriptableRenderer GetRenderer(int requestedIndex)
    {
      int index = ResolveRendererIndex(requestedIndex);
      if (m_Renderers.TryGetValue(index, out var existing))
        return existing;

      ScriptableRendererData rendererData = null;
      if (m_Asset?.m_RendererDataList != null && index >= 0 && index < m_Asset.m_RendererDataList.Length)
        rendererData = m_Asset.m_RendererDataList[index];
      rendererData ??= new ForwardRendererData();

      var renderer = rendererData.InternalCreateRenderer();
      m_Renderers[index] = renderer;
      return renderer;
    }

    private int ResolveRendererIndex(int requestedIndex)
    {
      if (m_Asset?.m_RendererDataList == null || m_Asset.m_RendererDataList.Length == 0)
        return 0;

      int defaultIndex = Mathf.Clamp(m_Asset.m_DefaultRendererIndex, 0, m_Asset.m_RendererDataList.Length - 1);
      if (requestedIndex < 0 || requestedIndex >= m_Asset.m_RendererDataList.Length || m_Asset.m_RendererDataList[requestedIndex] == null)
        return defaultIndex;
      return requestedIndex;
    }

    // A Base camera decides how its shared target is cleared, and Overlay
    // cameras preserve that target's color.  The generic SRP clear would run
    // before ExecuteRender and break this contract.
    protected override bool ShouldClearCameraTarget(Camera camera) => false;

    protected override void ExecuteRender(ScriptableRenderContext context, Camera camera)
    {
      var additionalData = camera.GetUniversalAdditionalCameraData();
      // Overlay cameras are consumed by their owning Base camera.  Rendering
      // them again from the global camera list would clear/submit twice.
      if (additionalData.renderType == CameraRenderType.Overlay)
        return;
      RenderCameraStack(context, camera);
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

      var additionalData = camera.GetUniversalAdditionalCameraData();
      if (additionalData.renderType == CameraRenderType.Overlay)
        return;
      pipeline.RenderCameraStack(context, camera);
    }

    private void RenderCameraStack(ScriptableRenderContext context, Camera baseCamera)
    {
      var baseData = baseCamera.GetUniversalAdditionalCameraData();
      var overlays = baseData.GetValidatedOverlayStack(baseCamera);
      var target = baseCamera.targetTexture != null
        ? new RenderTargetIdentifier(baseCamera.targetTexture)
        : new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
      int targetWidth = baseCamera.targetTexture?.width ?? baseCamera.pixelWidth;
      int targetHeight = baseCamera.targetTexture?.height ?? baseCamera.pixelHeight;

      // Metal has a native Tex2DArray + instance-id path for Unity-style
      // single-pass instancing.  Keep multipass as the compatibility path for
      // every other backend and target shape; a partial implementation must
      // never silently collapse the right eye into layer zero.
      bool singlePassInstanced = CanUseNativeSinglePassInstanced(baseCamera);
      if (singlePassInstanced)
      {
        RenderCamera(context, baseCamera, baseData, target, baseCamera.targetTexture, targetWidth, targetHeight,
          isLastInStack: overlays.Count == 0, isOverlay: false,
          stereoEye: Camera.StereoscopicEye.Left, stereoRightEye: Camera.StereoscopicEye.Right);
        for (int i = 0; i < overlays.Count; i++)
        {
          var overlay = overlays[i];
          RenderCamera(context, overlay, overlay.GetUniversalAdditionalCameraData(), target,
            baseCamera.targetTexture, targetWidth, targetHeight, i == overlays.Count - 1,
            isOverlay: true, stereoEye: Camera.StereoscopicEye.Left,
            stereoRightEye: Camera.StereoscopicEye.Right);
        }
        context.Submit();
        return;
      }

      int eyeCount = baseCamera.stereoEnabled ? 2 : 1;
      for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++)
      {
        // A multipass stereo target is still one composited XR frame. Defer
        // the final-stack resolve/grade until the right eye has been drawn;
        // otherwise the native array HDR pass would re-grade both layers once
        // per eye.
        bool isLastEye = eyeIndex == eyeCount - 1;
        Camera.StereoscopicEye? stereoEye = eyeCount == 2
          ? (eyeIndex == 0 ? Camera.StereoscopicEye.Left : Camera.StereoscopicEye.Right)
          : null;
        if (stereoEye.HasValue && !IncludesStereoEye(baseCamera.stereoTargetEye, stereoEye.Value))
          continue;
        RenderTargetIdentifier eyeTarget = target;
        eyeTarget.m_DepthSlice = eyeIndex;
        RenderCamera(context, baseCamera, baseData, eyeTarget, baseCamera.targetTexture, targetWidth, targetHeight,
          isLastInStack: overlays.Count == 0 && isLastEye, isOverlay: false, stereoEye: stereoEye);
        for (int i = 0; i < overlays.Count; i++)
        {
          var overlay = overlays[i];
          RenderCamera(context, overlay, overlay.GetUniversalAdditionalCameraData(), eyeTarget,
            baseCamera.targetTexture, targetWidth, targetHeight, i == overlays.Count - 1 && isLastEye,
            isOverlay: true, stereoEye: stereoEye);
        }
      }
      context.Submit();
    }

    private static bool IncludesStereoEye(Camera.StereoTargetEyeMask mask, Camera.StereoscopicEye eye) =>
      eye == Camera.StereoscopicEye.Left
        ? (mask & Camera.StereoTargetEyeMask.Left) != 0
        : (mask & Camera.StereoTargetEyeMask.Right) != 0;

    private static bool CanUseNativeSinglePassInstanced(Camera camera)
    {
      var target = camera.targetTexture;
      return camera.stereoEnabled &&
        IncludesStereoEye(camera.stereoTargetEye, Camera.StereoscopicEye.Left) &&
        IncludesStereoEye(camera.stereoTargetEye, Camera.StereoscopicEye.Right) &&
        target is not null && target.dimension == UnityEngine.TextureDimension.Tex2DArray && target.volumeDepth >= 2 &&
        XRDisplaySubsystem.AllowsSinglePassInstanced(target) &&
        NativeGraphicsDevice.Current?.DeviceType == GraphicsDeviceType.Metal;
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera, UniversalAdditionalCameraData additionalData, RenderTargetIdentifier target, RenderTexture? nativeTarget, int targetWidth, int targetHeight, bool isLastInStack, bool isOverlay, Camera.StereoscopicEye? stereoEye = null, Camera.StereoscopicEye? stereoRightEye = null)
    {
      beginCameraRendering?.Invoke(context, camera);
      try
      {
      var asset = m_Asset;
      var renderingData = new RenderingData();
      Rect viewport = GetTargetViewport(camera, targetWidth, targetHeight);
      var cameraData = new CameraData
      {
        camera = camera,
        nativeTargetTexture = nativeTarget,
        cameraType = camera.cameraType,
        isSceneViewCamera = camera.cameraType == CameraType.SceneView,
        isHdrEnabled = asset != null ? asset.supportsHDR && camera.allowHDR : camera.allowHDR,
        renderScale = asset != null ? asset.renderScale : 1.0f,
        msaaSamples = (int)(asset != null ? asset.msaaSampleCount : MsaaQuality.Disabled),
        targetTexture = target,
        pixelRect = viewport,
        renderType = additionalData.renderType,
        isBaseCamera = !isOverlay,
        isCameraStacked = true,
        isLastCameraInStack = isLastInStack,
        clearDepth = !isOverlay || additionalData.clearDepth,
        requiresDepthTexture = additionalData.requiresDepthTexture,
        requiresOpaqueTexture = additionalData.requiresColorTexture,
        isStereoEnabled = stereoEye.HasValue,
        isSinglePassInstanced = stereoRightEye.HasValue,
        stereoEye = stereoEye ?? Camera.StereoscopicEye.Left,
        xrDepthSlice = target.m_DepthSlice,
        xrEyeCount = stereoRightEye.HasValue ? 2 : 1
      };

      if (isOverlay)
        cameraData.isHdrEnabled = camera.allowHDR && (asset?.supportsHDR ?? true);

      // A provider-owned XR target has meaningful descriptor fields beyond
      // width/height: array layers, VR usage, MSAA and dynamic-scale
      // ownership. Renderer features must receive those exact fields in both
      // single-pass and explicit multipass modes.
      cameraData.cameraTargetDescriptor = nativeTarget?.descriptor ?? new RenderTextureDescriptor(targetWidth, targetHeight)
      {
        msaaSamples = cameraData.msaaSamples,
        colorFormat = cameraData.isHdrEnabled ? UnityEngine.RenderTextureFormat.DefaultHDR : UnityEngine.RenderTextureFormat.Default,
        depthBufferBits = 32,
        sRGB = QualitySettings.activeColorSpace == ColorSpace.Gamma
      };
      if (cameraData.isSinglePassInstanced && nativeTarget is null)
      {
        cameraData.cameraTargetDescriptor.dimension = UnityEngine.TextureDimension.Tex2DArray;
        cameraData.cameraTargetDescriptor.volumeDepth = cameraData.xrEyeCount;
      }

      renderingData.cameraData = cameraData;
      renderingData.supportsDynamicBatching = asset != null && asset.supportsDynamicBatching;
      renderingData.supportsInstancing = asset != null && asset.supportsGPUInstancing;
      renderingData.perObjectData = PerObjectData.None;
      renderingData.postProcessingEnabled = additionalData.renderPostProcessing && isLastInStack;

      var shadowData = new ShadowData
      {
        shadowDistance = asset != null ? asset.shadowDistance : QualitySettings.shadowDistance,
        supportsSoftShadows = asset != null && asset.supportsSoftShadows
      };
      renderingData.shadowData = shadowData;

      // Preserve ScriptableCullingParameters' camera-derived culling matrix,
      // mask and origin. Object-initializing only `camera` silently left an
      // identity matrix and a zero mask, which made the real Cull bridge
      // reject all normal scene renderers.
      var cullingParameters = nativeTarget is not null &&
          XRDisplaySubsystem.TryGetCullingParameters(nativeTarget, camera, out var providerCullingParameters)
        ? providerCullingParameters
        : new ScriptableCullingParameters(camera);
      cullingParameters.shadowDistance = shadowData.shadowDistance;
      if (stereoEye.HasValue)
      {
        Matrix4x4 stereoView = camera.GetStereoViewMatrix(stereoEye.Value);
        cullingParameters.cullingMatrix = camera.GetStereoProjectionMatrix(stereoEye.Value) * stereoView;
        cullingParameters.worldOrigin = stereoView.inverse.MultiplyPoint(Vector3.zero);
        cullingParameters.cullStereoSeparate = true;
        cullingParameters.stereoProjectionMatrix = true;
      }

      var renderer = GetRenderer(additionalData.rendererIndex);
      renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);

      context.Cull(ref cullingParameters, out var cullingResults);
      // A single-pass instanced submission needs the union of both eye
      // frusta, not the left-eye result reused for layer one.  Retain the
      // normal ScriptableCullingParameters public shape and execute the two
      // culls explicitly, then preserve deterministic instance ordering.
      if (stereoRightEye.HasValue)
      {
        var rightCullingParameters = cullingParameters;
        Matrix4x4 rightView = camera.GetStereoViewMatrix(stereoRightEye.Value);
        rightCullingParameters.cullingMatrix = camera.GetStereoProjectionMatrix(stereoRightEye.Value) * rightView;
        rightCullingParameters.worldOrigin = rightView.inverse.MultiplyPoint(Vector3.zero);
        context.Cull(ref rightCullingParameters, out var rightCullingResults);
        cullingResults = MergeStereoCullingResults(cullingResults, rightCullingResults);
      }
      renderingData.cullResults = cullingResults;

      var lightData = new LightData
      {
        mainLightIndex = GetMainLightIndex(cullingResults),
        visibleLights = cullingResults.visibleLights
      };
      renderingData.lightData = lightData;

      context.SetupCameraProperties(camera);

      CommandBuffer cmd = CommandBufferPool.Get("Clear Render Target");
      cmd.SetRenderTarget(target);
      cmd.SetViewport(viewport);
      GetCameraClearFlags(camera, isOverlay, additionalData.clearDepth, out bool clearDepth, out bool clearColor);
      if (nativeTarget != null)
        NativeGraphicsDevice.Current?.EnsureCameraRenderTarget(nativeTarget);
      NativeGraphicsDevice.Current?.TryRecordCameraPass(
        unchecked((ulong)(uint)target.m_NameID), targetWidth, targetHeight,
        viewport, camera.backgroundColor, clearDepth, clearColor,
        cameraData.msaaSamples, isLastInStack, cameraData.isHdrEnabled,
        isCameraTarget: nativeTarget == null, depthSlice: target.m_DepthSlice,
        depthSliceCount: cameraData.xrEyeCount);
      cmd.ClearRenderTarget(clearDepth, clearColor, camera.backgroundColor);
      context.ExecuteCommandBuffer(cmd);
      CommandBufferPool.Release(cmd);

      Matrix4x4 rasterProjection = stereoEye.HasValue
        ? camera.GetStereoProjectionMatrix(stereoEye.Value) : camera.projectionMatrix;
      Matrix4x4 nonJitteredProjection = stereoEye.HasValue
        ? camera.GetStereoNonJitteredProjectionMatrix(stereoEye.Value) : camera.nonJitteredProjectionMatrix;
      Matrix4x4 view = stereoEye.HasValue ? camera.GetStereoViewMatrix(stereoEye.Value) : camera.worldToCameraMatrix;
      Matrix4x4? rightRasterViewProjection = stereoRightEye.HasValue
        ? camera.GetStereoProjectionMatrix(stereoRightEye.Value) * camera.GetStereoViewMatrix(stereoRightEye.Value) : null;
      Matrix4x4? rightNonJitteredViewProjection = stereoRightEye.HasValue
        ? camera.GetStereoNonJitteredProjectionMatrix(stereoRightEye.Value) * camera.GetStereoViewMatrix(stereoRightEye.Value) : null;
      context.SetNativeCameraDrawState(NativeGraphicsDevice.Current, nativeTarget,
        rasterProjection * view, camera, nonJitteredProjection * view, target.m_DepthSlice,
        rightRasterViewProjection, rightNonJitteredViewProjection);

      renderer.Setup(context, ref renderingData);
      renderer.Execute(context, ref renderingData);
      context.SetNativeCameraDrawState(null, null, Matrix4x4.identity);

      context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
      context.DrawUIOverlay(camera);
      context.DrawGizmos(camera, GizmoSubset.PostImageEffects);

      }
      finally
      {
        endCameraRendering?.Invoke(context, camera);
      }
    }

    private static CullingResults MergeStereoCullingResults(CullingResults left, CullingResults right)
    {
      var merged = new List<Renderer>(left.visibleRendererSnapshot.Length + right.visibleRendererSnapshot.Length);
      var seen = new HashSet<int>();
      void AddRange(Renderer[] candidates)
      {
        foreach (var candidate in candidates)
        {
          if (candidate is not null && seen.Add(candidate.GetInstanceID()))
            merged.Add(candidate);
        }
      }
      AddRange(left.visibleRendererSnapshot);
      AddRange(right.visibleRendererSnapshot);
      merged.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
      return new CullingResults
      {
        _visibleLights = left.visibleLights,
        _visibleReflectionProbes = left.visibleReflectionProbes,
        _lightAndReflectionProbeCount = left.lightAndReflectionProbeCount,
        _visibleLightCount = left.visibleLightCount,
        _visibleInstanceCount = merged.Count,
        _visibleRenderers = merged.ToArray(),
        visibleRenderers = new VisibleRendererList { length = merged.Count, nativePointer = IntPtr.Zero },
        velocityNeedsRasterization = left.velocityNeedsRasterization || right.velocityNeedsRasterization
      };
    }

    private static void GetCameraClearFlags(Camera camera, bool isOverlay, bool overlayClearDepth, out bool clearDepth, out bool clearColor)
    {
      if (isOverlay)
      {
        clearDepth = overlayClearDepth;
        clearColor = false;
        return;
      }

      clearDepth = camera.clearFlags != CameraClearFlags.Nothing;
      clearColor = camera.clearFlags == CameraClearFlags.Color || camera.clearFlags == CameraClearFlags.Skybox;
    }

    private static Rect GetTargetViewport(Camera camera, int targetWidth, int targetHeight)
    {
      Rect normalized = camera.rect;
      return new Rect(
        normalized.x * targetWidth,
        normalized.y * targetHeight,
        normalized.width * targetWidth,
        normalized.height * targetHeight);
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
      foreach (var renderer in m_Renderers.Values)
        renderer?.Dispose();
      m_Renderers.Clear();
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
    // The Base target is carried through Overlay cameras so the final stack
    // post pass can execute against the actual native attachment.
    public RenderTexture nativeTargetTexture;
    public RenderTextureDescriptor cameraTargetDescriptor;
    public RenderTargetIdentifier targetTexture;
    public Rect pixelRect;
    public CameraType cameraType;
    public CameraRenderType renderType;
    public bool isSceneViewCamera;
    public bool isHdrEnabled;
    public bool isBaseCamera;
    public bool isCameraStacked;
    public bool isLastCameraInStack;
    public bool clearDepth;
    // Aggregated from UniversalAdditionalCameraData, renderer defaults, and
    // ScriptableRenderPass.ConfigureInput.  These are resource requirements,
    // not an assertion that a backend has already allocated a texture.
    public bool requiresDepthTexture;
    public bool requiresOpaqueTexture;
    public bool requiresNormalsTexture;
    public bool requiresMotionVectors;
    // Valid from AfterRenderingOpaques until per-camera cleanup. The native
    // resource is intentionally transient, matching URP intermediate targets.
    public RenderTexture opaqueTexture;
    public RenderTexture depthTexture;
    public RenderTexture normalsTexture;
    public RenderTexture motionVectorTexture;
    public int msaaSamples;
    public float renderScale;
    public bool isSceneViewFx;
    public bool isStereoEnabled;
    public bool isSinglePassInstanced;
    public Camera.StereoscopicEye stereoEye;
    public int xrDepthSlice;
    public int xrEyeCount;
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
