using System;
using UnityEngine;

namespace Anity.WebGL;

public static class WebGLRuntime
{
  private static bool _initialized;
  private static bool _isRunningInWebGL;

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

  public static bool SupportsJit => false;
  public static bool SupportsThreading => false;
  public static bool SupportsFileSystem => false;
  public static bool SupportsIndexedDB => true;
  public static long MaxMemorySize => 256 * 1024 * 1024;

  public static void Initialize()
  {
    if (_initialized) return;
    _isRunningInWebGL = DetectWebGL();
    _initialized = true;
    if (_isRunningInWebGL) ConfigureForWebGL();
  }

  private static void ConfigureForWebGL() { }
  private static bool DetectWebGL() => false;
}

public static class WebGLAudio
{
  public static bool IsSupported => true;
  public static int MaxSources => 32;
  public static void Initialize() { }
}

public static class WebGLInput
{
  public static bool TouchSupported => true;
  public static bool MouseSupported => true;
  public static bool KeyboardSupported => true;
  public static bool GamepadSupported => true;
  public static int MaxTouchPoints => 10;

  public static event Action<int, float, float>? OnTouchStart;
  public static event Action<int, float, float>? OnTouchMove;
  public static event Action<int>? OnTouchEnd;
  public static event Action<int>? OnTouchCancel;

  public static event Action<string>? OnKeyDown;
  public static event Action<string>? OnKeyUp;
  public static event Action<char>? OnKeyPress;

  public static void SimulateTouchStart(int fingerId, float x, float y) => OnTouchStart?.Invoke(fingerId, x, y);
  public static void SimulateTouchMove(int fingerId, float x, float y) => OnTouchMove?.Invoke(fingerId, x, y);
  public static void SimulateTouchEnd(int fingerId) => OnTouchEnd?.Invoke(fingerId);
  public static void SimulateTouchCancel(int fingerId) => OnTouchCancel?.Invoke(fingerId);
  public static void SimulateKeyDown(string key) => OnKeyDown?.Invoke(key);
  public static void SimulateKeyUp(string key) => OnKeyUp?.Invoke(key);
  public static void SimulateKeyPress(char key) => OnKeyPress?.Invoke(key);
}

public static class WebGLGraphics
{
  private static IntPtr _canvas;
  private static Color _clearColor = Color.black;

  public static void SetCanvas(IntPtr canvas) => _canvas = canvas;
  public static IntPtr GetCanvas() => _canvas;
  public static void RenderFrame() { }
  public static void Present() { }
  public static void SetClearColor(Color color) => _clearColor = color;
  public static void SetClearColor(float r, float g, float b, float a = 1f) => _clearColor = new Color(r, g, b, a);
  public static Color GetClearColor() => _clearColor;
  public static void Clear() { }
}

public static class WebGLDisplay
{
  public static int width { get; set; } = 1920;
  public static int height { get; set; } = 1080;
  public static float dpiScale { get; set; } = 1f;
  public static bool fullscreen { get; set; }

  public static event Action<int, int>? OnResize;

  public static void Resize(int newWidth, int newHeight)
  {
    width = newWidth;
    height = newHeight;
    OnResize?.Invoke(newWidth, newHeight);
  }
}

public static class WebGLStorage
{
  public static bool IsPersistent => true;
  public static long MaxStorageSize => 50 * 1024 * 1024;

  public static bool Save(string key, byte[] data)
  {
    _ = key; _ = data; return false;
  }

  public static byte[]? Load(string key)
  {
    _ = key; return null;
  }

  public static bool Delete(string key)
  {
    _ = key; return false;
  }

  public static bool Exists(string key)
  {
    _ = key; return false;
  }

  public static void Clear() { }
}
