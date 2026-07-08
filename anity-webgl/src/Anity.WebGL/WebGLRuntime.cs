using System;
using System.Runtime.InteropServices;

namespace Anity.WebGL;

/// <summary>
/// WebGL platform runtime support for Unity-compatible WebGL builds.
/// Handles WebGL-specific constraints: no JIT, no threads, no file system.
/// </summary>
public static class WebGLRuntime
{
  private static bool _initialized;
  private static bool _isRunningInWebGL;

  /// <summary>
  /// Gets whether the application is running in a WebGL environment.
  /// </summary>
  public static bool IsRunningInWebGL
  {
    get
    {
      if (!_initialized)
      {
        _isRunningInWebGL = DetectWebGL();
        _initialized = true;
      }

      return _isRunningInWebGL;
    }
  }

  /// <summary>
  /// WebGL does not support JIT compilation.
  /// </summary>
  public static bool SupportsJit => false;

  /// <summary>
  /// WebGL does not support threading.
  /// </summary>
  public static bool SupportsThreading => false;

  /// <summary>
  /// WebGL has no direct file system access.
  /// </summary>
  public static bool SupportsFileSystem => false;

  /// <summary>
  /// WebGL uses IndexedDB for persistent storage.
  /// </summary>
  public static bool SupportsIndexedDB => true;

  /// <summary>
  /// WebGL maximum memory size in bytes (default 256MB).
  /// </summary>
  public static long MaxMemorySize => 256 * 1024 * 1024;

  /// <summary>
  /// Initializes WebGL runtime support.
  /// </summary>
  public static void Initialize()
  {
    if (_initialized)
    {
      return;
    }

    _isRunningInWebGL = DetectWebGL();
    _initialized = true;

    if (_isRunningInWebGL)
    {
      ConfigureForWebGL();
    }
  }

  /// <summary>
  /// Configures the runtime for WebGL builds.
  /// </summary>
  private static void ConfigureForWebGL()
  {
    // WebGL-specific configuration:
    // - Disable threading
    // - Configure memory limits
    // - Set up IndexedDB storage
    // - Configure audio context
  }

  private static bool DetectWebGL()
  {
    // In a real WebGL environment, this would check for WebGL-specific indicators
    // For now, return false for non-WebGL environments
    return false;
  }
}

/// <summary>
/// WebGL-specific audio support.
/// </summary>
public static class WebGLAudio
{
  /// <summary>
  /// Gets whether audio is supported in WebGL.
  /// </summary>
  public static bool IsSupported => true;

  /// <summary>
  /// Gets the maximum number of audio sources.
  /// </summary>
  public static int MaxSources => 32;

  /// <summary>
  /// Initializes the audio system for WebGL.
  /// </summary>
  public static void Initialize()
  {
    // WebGL audio requires user interaction to start
  }
}

/// <summary>
/// WebGL-specific input support.
/// </summary>
public static class WebGLInput
{
  /// <summary>
  /// Gets whether touch input is supported.
  /// </summary>
  public static bool TouchSupported => true;

  /// <summary>
  /// Gets whether mouse input is supported.
  /// </summary>
  public static bool MouseSupported => true;

  /// <summary>
  /// Gets whether keyboard input is supported.
  /// </summary>
  public static bool KeyboardSupported => true;

  /// <summary>
  /// Gets whether gamepad input is supported.
  /// </summary>
  public static bool GamepadSupported => true;

  /// <summary>
  /// Gets the maximum number of touch points.
  /// </summary>
  public static int MaxTouchPoints => 10;
}

/// <summary>
/// WebGL-specific storage support using IndexedDB.
/// </summary>
public static class WebGLStorage
{
  /// <summary>
  /// Gets whether persistent storage is available.
  /// </summary>
  public static bool IsPersistent => true;

  /// <summary>
  /// Gets the maximum storage size in bytes.
  /// </summary>
  public static long MaxStorageSize => 50 * 1024 * 1024; // 50MB

  /// <summary>
  /// Saves data to IndexedDB.
  /// </summary>
  /// <param name="key">The storage key.</param>
  /// <param name="data">The data to save.</param>
  /// <returns>True if the save was successful.</returns>
  public static bool Save(string key, byte[] data)
  {
    // WebGL IndexedDB save implementation
    return false;
  }

  /// <summary>
  /// Loads data from IndexedDB.
  /// </summary>
  /// <param name="key">The storage key.</param>
  /// <returns>The loaded data, or null if not found.</returns>
  public static byte[]? Load(string key)
  {
    // WebGL IndexedDB load implementation
    return null;
  }

  /// <summary>
  /// Deletes data from IndexedDB.
  /// </summary>
  /// <param name="key">The storage key.</param>
  /// <returns>True if the delete was successful.</returns>
  public static bool Delete(string key)
  {
    // WebGL IndexedDB delete implementation
    return false;
  }

  /// <summary>
  /// Checks if a key exists in IndexedDB.
  /// </summary>
  /// <param name="key">The storage key.</param>
  /// <returns>True if the key exists.</returns>
  public static bool Exists(string key)
  {
    // WebGL IndexedDB exists implementation
    return false;
  }

  /// <summary>
  /// Clears all data from IndexedDB.
  /// </summary>
  public static void Clear()
  {
    // WebGL IndexedDB clear implementation
  }
}
