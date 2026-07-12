using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine;

public enum QualityLevel
{
  Low,
  Medium,
  High,
  Ultra
}

public enum ShadowQuality
{
  Disable = 0,
  HardOnly = 1,
  All = 2,
  HardShadows = 1
}

public enum ShadowResolution
{
  Off = -1,
  Low = 0,
  Medium = 1,
  High = 2,
  VeryHigh = 3
}

public enum ShadowProjection
{
  CloseFit = 0,
  StableFit = 1
}

public enum ShadowmaskMode
{
  Shadowmask = 0,
  DistanceShadowmask = 1
}

public enum AnisotropicFiltering
{
  Disable = 0,
  Enable = 1,
  ForceEnable = 2
}

public enum AntiAliasing
{
  Disabled = 0,
  _2x = 2,
  _4x = 4,
  _8x = 8
}

public enum TextureQuality
{
  FullRes = 0,
  HalfRes = 1,
  QuarterRes = 2,
  EighthRes = 3
}

public enum SkinWeights
{
  None = 0,
  OneBone = 1,
  TwoBones = 2,
  FourBones = 4,
  Unlimited = 255
}

public enum VSyncCount
{
  DontSync = 0,
  EveryVBlank = 1,
  EverySecondVBlank = 2
}

public enum MSAA
{
  None = 1,
  _2x = 2,
  _4x = 4,
  _8x = 8
}

public static class QualitySettings
{
  private static RenderPipelineAsset _renderPipeline;
  private static int _currentQualityLevel;
  private static readonly string[] _names = { "Low", "Medium", "High", "Ultra" };
  private static readonly Dictionary<Type, RenderPipelineAsset> _qualityPipelines = new();

  public static RenderPipelineAsset renderPipeline
  {
    get => _renderPipeline;
    set
    {
      if (_renderPipeline == value) return;
      var old = _renderPipeline;
      _renderPipeline = value;
      GraphicsSettings.OnQualityPipelineChanged(old, value);
    }
  }

  public static int currentQualityLevel
  {
    get => _currentQualityLevel;
    set => SetQualityLevel(value);
  }

  public static string[] names => _names;
  public static int count => _names.Length;

  public static int pixelLightCount { get; set; } = 4;
  public static float shadowDistance { get; set; } = 150f;
  public static float shadowNearPlaneOffset { get; set; } = 3f;
  public static int shadowCascades { get; set; } = 1;
  public static float shadowCascade2Split { get; set; } = 1f / 3f;
  public static Vector3 shadowCascade4Split { get; set; } = new(0.067f, 0.2f, 0.467f);
  public static ShadowResolution shadowResolution { get; set; } = ShadowResolution.Medium;
  public static ShadowProjection shadowProjection { get; set; } = ShadowProjection.CloseFit;
  public static ShadowQuality shadows { get; set; } = ShadowQuality.All;
  public static ShadowmaskMode shadowmaskMode { get; set; } = ShadowmaskMode.DistanceShadowmask;
  public static bool softParticles { get; set; }
  public static bool softVegetation { get; set; } = true;
  public static bool realtimeReflectionProbes { get; set; } = true;
  public static bool billboardsFaceCameraPosition { get; set; } = true;
  public static int vSyncCount
  {
    get => _vSyncCount;
    set => _vSyncCount = Mathf.Clamp(value, 0, 4);
  }
  private static int _vSyncCount = 1;

  public static int antiAliasing { get; set; }
  public static MSAA antiAliasingValue => (MSAA)(antiAliasing > 0 ? antiAliasing : 1);
  public static float lodBias { get; set; } = 2f;
  public static int maximumLODLevel { get; set; }
  public static AnisotropicFiltering anisotropicFiltering { get; set; } = AnisotropicFiltering.Enable;
  public static int anisotropicFilteringLevel { get; set; } = 2;
  public static int masterTextureLimit
  {
    get => (int)textureQuality;
    set => textureQuality = (TextureQuality)Mathf.Clamp(value, 0, 3);
  }
  public static TextureQuality textureQuality { get; set; } = TextureQuality.FullRes;
  public static SkinWeights skinWeights { get; set; } = SkinWeights.TwoBones;
  public static int targetFrameRate
  {
    get => Application.targetFrameRate;
    set => Application.targetFrameRate = value;
  }
  public static int realtimeGICPUUsage { get; set; } = 25;
  public static int particleRaycastBudget { get; set; } = 256;
  public static int asyncUploadTimeSlice { get; set; } = 2;
  public static int asyncUploadBufferSize { get; set; } = 16;
  public static bool streamingMipmapsActive { get; set; }
  public static bool streamingMipmapsAddAllCameras { get; set; } = true;
  public static float streamingMipmapsMemoryBudget { get; set; } = 512f;
  public static int streamingMipmapsRenderersPerFrame { get; set; } = 512;
  public static int streamingMipmapsMaxLevelReduction { get; set; } = 2;
  public static int streamingMipmapsMaxFileIORequests { get; set; } = 1024;

  public static ColorSpace activeColorSpace { get; set; } = ColorSpace.Gamma;
  public static ColorSpace desiredColorSpace { get; set; } = ColorSpace.Gamma;
  public static bool hdr { get; set; }

  public static void SetQualityLevel(int index)
  {
    _currentQualityLevel = Mathf.Clamp(index, 0, _names.Length - 1);
  }

  public static void SetQualityLevel(int index, bool applyExpensiveChanges)
  {
    SetQualityLevel(index);
  }

  public static int GetQualityLevel() => _currentQualityLevel;
  public static void IncreaseLevel(bool applyExpensiveChanges = false) => SetQualityLevel(_currentQualityLevel + 1);
  public static void DecreaseLevel(bool applyExpensiveChanges = false) => SetQualityLevel(_currentQualityLevel - 1);
}
