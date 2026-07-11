using System;
using System.IO;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Il2Cpp;

namespace Anity.Core.Runtime.Platform;

public enum TargetPlatform
{
  WebGL,
  Windows,
  MacOS,
  Linux,
  Android,
  iOS
}

public enum RenderingConfig
{
  URP,
  Forward,
  Deferred
}

public enum RenderPipelineType
{
  URP,
  HDRP,
  BuiltIn
}

public static class PlatformConfig
{
  private static TargetPlatform _currentPlatform = DetectCurrentPlatform();
  private static RenderingConfig _renderingConfig = RenderingConfig.URP;
  private static RenderPipelineType _renderPipeline = RenderPipelineType.URP;

  public static TargetPlatform CurrentPlatform
  {
    get => _currentPlatform;
    private set => _currentPlatform = value;
  }

  public static bool IsWebGL => _currentPlatform == TargetPlatform.WebGL;
  public static bool IsWindows => _currentPlatform == TargetPlatform.Windows;
  public static bool IsMacOS => _currentPlatform == TargetPlatform.MacOS;
  public static bool IsLinux => _currentPlatform == TargetPlatform.Linux;
  public static bool IsAndroid => _currentPlatform == TargetPlatform.Android;
  public static bool IsIOS => _currentPlatform == TargetPlatform.iOS;
  public static bool IsEditor { get; set; } = false;
  public static bool IsMobile => _currentPlatform == TargetPlatform.Android || _currentPlatform == TargetPlatform.iOS;

  public static RenderingConfig renderingConfig
  {
    get => _renderingConfig;
    set => _renderingConfig = value;
  }

  public static RenderPipelineType currentRenderPipeline
  {
    get => _renderPipeline;
    private set => _renderPipeline = value;
  }

  public static string dataPath => UnityEngine.Application.dataPath;
  public static string persistentDataPath => UnityEngine.Application.persistentDataPath;
  public static string temporaryCachePath => UnityEngine.Application.temporaryCachePath;
  public static string streamingAssetsPath => UnityEngine.Application.streamingAssetsPath;

  public static void SetTargetPlatform(TargetPlatform platform)
  {
    _currentPlatform = platform;
  }

  public static void SetRenderPipeline(RenderPipelineType pipeline)
  {
    if (pipeline == RenderPipelineType.URP)
    {
      _renderPipeline = pipeline;
      _renderingConfig = RenderingConfig.URP;
    }
  }

  private static TargetPlatform DetectCurrentPlatform()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return TargetPlatform.Windows;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return TargetPlatform.MacOS;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      return TargetPlatform.Linux;
    return TargetPlatform.Windows;
  }
}
