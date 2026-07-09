using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering
{
  public abstract class RenderPipelineAsset : ScriptableObject
  {
    public abstract Type pipelineType { get; }
    public virtual string[] renderingLayerMaskNames => new string[32];
    public virtual string[] prefixedRenderingLayerMaskNames => new string[32];
    public virtual int terrainBrushPassIndex => -1;

    public virtual Material defaultMaterial => null;
    public virtual Shader defaultShader => null;
    public virtual Material defaultParticleMaterial => null;
    public virtual Material defaultLineMaterial => null;
    public virtual Material defaultTerrainMaterial => null;
    public virtual Material defaultUIMaterial => null;
    public virtual Material defaultUIOverdrawMaterial => null;
    public virtual Material defaultUIETC1SupportedMaterial => null;
    public virtual Material default2DMaskMaterial => null;
    public virtual Shader defaultTextMeshProShader => null;
    public virtual Shader defaultTextShader => null;

    public virtual bool autoreleaseResources
    {
      get => true;
      set { }
    }

    public virtual int shadowCascadeCount => 4;

    public virtual string[] ComputeSystemShadersKeywords(string[] userKeywords)
    {
      return Array.Empty<string>();
    }

    public virtual string[] GetTerrainCompatibleLitShaderKeywords()
    {
      return Array.Empty<string>();
    }

    protected abstract RenderPipeline CreatePipeline();
  }

  public abstract class RenderPipeline : IDisposable
  {
    public bool disposed { get; private set; }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
      disposed = true;
    }

    protected virtual void Dispose(bool disposing) { }

    public void Render(ScriptableRenderContext context, Camera[] cameras)
    {
      if (cameras == null) throw new ArgumentNullException(nameof(cameras));
      Render(context, new List<Camera>(cameras));
    }

    public void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
      if (disposed) throw new ObjectDisposedException(nameof(RenderPipeline));
      RenderInternal(context, cameras);
    }

    protected abstract void Render(ScriptableRenderContext context, Camera[] cameras);
    protected abstract void Render(ScriptableRenderContext context, List<Camera> cameras);

    internal virtual void RenderInternal(ScriptableRenderContext context, List<Camera> cameras)
    {
      Render(context, cameras);
    }
  }

  public struct RenderingData
  {
    public CameraData cameraData;
    public LightData lightData;
    public ShadowData shadowData;
    public PostProcessingData postProcessingData;
    public PerObjectData perObjectData;
    public bool supportsDynamicBatching;
    public bool supportsInstancing;
    public bool postProcessingEnabled;
    public CommandBufferPool commandBufferPool;
  }

  public struct CameraData
  {
    public Camera camera;
    public CameraType cameraType;
    public RenderTextureDescriptor cameraTargetDescriptor;
    public float renderScale;
    public int maxShadowBatches;
    public Vector2Int maxTileSize;
    public Vector2Int maxRenderSize;
    public Vector2Int minRenderSize;
    public bool isHdrEnabled;
    public bool isStereoEnabled;
    public bool isXRActive;
    public bool clearDepth;
    public bool clearColor;
    public CameraClearFlags clearFlags;
    public Color backgroundColor;
    public Vector3 worldSpaceCameraPos;
    public Matrix4x4 viewMatrix;
    public Matrix4x4 projectionMatrix;
    public Matrix4x4 viewAndProjectionMatrix;
    public bool viewTransformIsIdentity;
    public bool projectionMatrixIsIdentity;
    public float volumeLayerMask;
    public VolumeStack volumeStack;
    public bool isDefaultViewport;
    public Rect pixelRect;
    public Rect normalizedViewPort;
    public int pixelWidth;
    public int pixelHeight;
    public int rendererIndex;
    public ScriptableRenderer renderer;
    public bool postProcessEnabled;
    public int antiAliasing;
    public bool isStopNaNEnabled;
    public bool isDitheringEnabled;
    public bool isFogEnabled;
    public bool isToneMapingEnabled;
    public float sceneViewFilterMode;
    public LayerMask volumeMask;
  }

  public struct LightData
  {
    public int mainLightIndex;
    public int additionalLightsCount;
    public int pixelAdditionalLightsCount;
    public int vertexAdditionalLightsCount;
    public int totalAdditionalLightsCount;
    public int mainLightIndexShadow;
    public int maxPerObjectAdditionalLightsCount;
    public bool additionalLightsPerVertex;
    public bool supportsMixedLighting;
    public bool supportsSubtractiveMixedLighting;
    public bool supportsDynamicLightmapTextures;
    public LightCategory mainLightCategory;
    public bool mainLightIsImportant;
  }

  public struct ShadowData
  {
    public ShadowQuality mainLightShadowsQuality;
    public ShadowResolution mainLightShadowmapResolution;
    public ShadowQuality additionalLightsShadowsQuality;
    public ShadowResolution additionalLightsShadowmapResolution;
    public int shadowCascadesCount;
    public float shadowDistance;
    public float shadowCascade2Split;
    public Vector3 shadowCascade4Split;
    public float shadowNearPlaneOffset;
    public float shadowCascadeBorder;
    public bool supportsMainLightShadows;
    public bool supportsAdditionalLightShadows;
    public bool supportsSoftShadows;
  }

  public struct PostProcessingData
  {
    public bool isStopNaNEnabled;
    public bool isGrainEnabled;
    public PostProcessingToneMapingMode toneMAP;
  }

  public enum PostProcessingToneMapingMode
  {
    None = 0,
    GradingOnly = 1,
    Neutral = 2,
    ACES = 3,
    External = 4
  }

  public enum ShadowQuality
  {
    Disable = 0,
    HardShadows = 1,
    All = 2
  }

  [Flags]
  public enum PerObjectData
  {
    None = 0,
    LightProbe = 1 << 0,
    ReflectionProbes = 1 << 1,
    LightProbeProxyVolume = 1 << 2,
    Lightmaps = 1 << 3,
    MotionVectors = 1 << 4,
    LightData = 1 << 5,
    OcclusionProbe = 1 << 6
  }

  public enum LightCategory
  {
    Pixel = 0,
    Vertex = 1
  }

  public struct ScriptableCullingParameters
  {
    public Camera camera;
    public Matrix4x4 worldToCameraMatrix;
    public Matrix4x4 projectionMatrix;
    public Plane[] cullingPlanes;
    public int cullingMask;
    public int layerMask;
    public float isOrthographic;
    public bool isShadowCaster;
    public float shadowDistance;
    public float shadowNearPlaneOffset;
    public int shadowCascades;
  }

  public sealed class CommandBufferPool
  {
    public static CommandBuffer Get(string name = "")
    {
      return new CommandBuffer(name);
    }

    public static void Release(CommandBuffer buffer)
    {
    }
  }
}
