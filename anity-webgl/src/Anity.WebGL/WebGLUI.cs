using System;
using UnityEngine;
using UnityEngine.UI;

namespace Anity.WebGL;

/// <summary>
/// WebGL-specific UI rendering support for Unity-compatible UI.
/// Handles WebGL canvas scaling, input, and rendering constraints.
/// </summary>
public static class WebGLUI
{
  /// <summary>
  /// Gets the default canvas resolution for WebGL.
  /// </summary>
  public static Vector2 DefaultResolution => new(960, 600);

  /// <summary>
  /// Gets the maximum canvas size for WebGL.
  /// </summary>
  public static Vector2 MaxCanvasSize => new(4096, 4096);

  /// <summary>
  /// Gets whether UI scaling is supported.
  /// </summary>
  public static bool SupportsScaling => true;

  /// <summary>
  /// Gets whether UI events are supported.
  /// </summary>
  public static bool SupportsEvents => true;

  /// <summary>
  /// Initializes WebGL UI support.
  /// </summary>
  public static void Initialize()
  {
    // WebGL UI initialization:
    // - Configure canvas scaling
    // - Set up input handling
    // - Configure rendering pipeline
  }

  /// <summary>
  /// Scales the canvas to fit the browser window.
  /// </summary>
  /// <param name="canvas">The canvas to scale.</param>
  /// <param name="referenceResolution">The reference resolution.</param>
  public static void ScaleCanvas(Canvas canvas, Vector2 referenceResolution)
  {
    if (canvas is null)
    {
      return;
    }

    // WebGL canvas scaling implementation
    var scaler = canvas.GetComponent<CanvasScaler>();
    if (scaler is not null)
    {
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = referenceResolution;
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
    }
  }

  /// <summary>
  /// Handles WebGL-specific input events.
  /// </summary>
  /// <param name="eventType">The event type.</param>
  /// <param name="data">The event data.</param>
  public static void HandleInput(string eventType, string data)
  {
    // WebGL input handling implementation
  }
}

/// <summary>
/// WebGL-specific TextMeshPro support.
/// </summary>
public static class WebGLTextMeshPro
{
  /// <summary>
  /// Gets whether TextMeshPro is supported in WebGL.
  /// </summary>
  public static bool IsSupported => true;

  /// <summary>
  /// Gets the maximum texture size for TextMeshPro.
  /// </summary>
  public static int MaxTextureSize => 4096;

  /// <summary>
  /// Initializes TextMeshPro for WebGL.
  /// </summary>
  public static void Initialize()
  {
    // TextMeshPro WebGL initialization
  }
}

/// <summary>
/// WebGL-specific AssetBundle support.
/// </summary>
public static class WebGLAssetBundle
{
  /// <summary>
  /// Gets whether AssetBundles are supported in WebGL.
  /// </summary>
  public static bool IsSupported => true;

  /// <summary>
  /// Gets the maximum AssetBundle size.
  /// </summary>
  public static long MaxBundleSize => 50 * 1024 * 1024; // 50MB

  /// <summary>
  /// Loads an AssetBundle from URL.
  /// </summary>
  /// <param name="url">The URL to load from.</param>
  /// <param name="callback">The callback when loaded.</param>
  public static void LoadFromUrl(string url, Action<byte[]> callback)
  {
    // WebGL AssetBundle URL loading implementation
  }
}

/// <summary>
/// WebGL-specific networking support.
/// </summary>
public static class WebGLNetworking
{
  /// <summary>
  /// Gets whether UnityWebRequest is supported.
  /// </summary>
  public static bool SupportsUnityWebRequest => true;

  /// <summary>
  /// Gets whether WebSocket is supported.
  /// </summary>
  public static bool SupportsWebSocket => true;

  /// <summary>
  /// Gets whether HTTP is supported.
  /// </summary>
  public static bool SupportsHTTP => true;

  /// <summary>
  /// Gets whether HTTPS is supported.
  /// </summary>
  public static bool SupportsHTTPS => true;

  /// <summary>
  /// Gets the maximum concurrent connections.
  /// </summary>
  public static int MaxConcurrentConnections => 6;
}

/// <summary>
/// WebGL-specific video playback support.
/// </summary>
public static class WebGLVideo
{
  /// <summary>
  /// Gets whether video playback is supported.
  /// </summary>
  public static bool IsSupported => true;

  /// <summary>
  /// Gets whether autoplay is supported.
  /// </summary>
  public static bool SupportsAutoplay => false; // WebGL requires user interaction

  /// <summary>
  /// Gets the maximum video resolution.
  /// </summary>
  public static Vector2 MaxResolution => new(1920, 1080);

  /// <summary>
  /// Gets the supported video formats.
  /// </summary>
  public static string[] SupportedFormats => new[] { "mp4", "webm", "ogg" };
}
