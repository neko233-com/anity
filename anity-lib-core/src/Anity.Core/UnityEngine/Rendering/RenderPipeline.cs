using System;
using UnityEngine;

namespace UnityEngine.Rendering
{
  public struct RenderingData
  {
    public CameraData cameraData;
    public LightData lightData;
    public ShadowData shadowData;
    public PostProcessingData postProcessingData;
    public bool supportsDynamicBatching;
    public bool supportsInstancing;
    public bool postProcessingEnabled;
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
    public object volumeStack;
    public bool isDefaultViewport;
    public Rect pixelRect;
    public Rect normalizedViewPort;
    public int pixelWidth;
    public int pixelHeight;
    public int rendererIndex;
    public object renderer;
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
    public bool useScreenSpaceShadows;
    public bool supportsSoftShadows;
    public float shadowDistance;
    public int shadowCascadeCount;
    public float shadowCascade2Split;
    public Vector2 shadowCascade3Splits;
    public Vector3 shadowCascade4Splits;
    public float shadowDepthBias;
    public float shadowNormalBias;
    public float shadowNearPlaneOffset;
  }

  public struct ShadowData
  {
    public bool supportsSoftShadows;
    public float shadowDistance;
    public int shadowmapDepthBufferBits;
    public int mainShadowmapWidth;
    public int mainShadowmapHeight;
    public int additionalShadowmapWidth;
    public int additionalShadowmapHeight;
    public bool supportsMainLightShadows;
    public bool supportsAdditionalLightShadows;
    public int mainShadowCascadeCount;
  }

  public struct PostProcessingData
  {
    public ColorGradingMode colorGradingMode;
    public int lutSize;
    public bool useFastSRGBLinearConversion;
  }

  public enum ColorGradingMode
  {
    LowDynamicRange,
    HighDynamicRange
  }

  public enum SortingCriteria
  {
    None = 0,
    RendererPriority = 1,
    Distance = 2,
    CommonOpaque = 3,
    CommonTransparent = 4
  }
}
