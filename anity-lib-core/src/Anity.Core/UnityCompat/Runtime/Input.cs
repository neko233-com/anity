using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class Input
{
  private static Vector3 _mousePosition = Vector3.zero;
  private static Vector2 _mouseScrollDelta;
  private static readonly HashSet<KeyCode> _pressedKeys = new();
  private static readonly HashSet<KeyCode> _justPressedKeys = new();
  private static readonly HashSet<KeyCode> _justReleasedKeys = new();
  private static readonly HashSet<int> _pressedMouseButtons = new();
  private static readonly HashSet<int> _justPressedMouseButtons = new();
  private static readonly HashSet<int> _justReleasedMouseButtons = new();
  private static readonly Dictionary<string, float> _axes = new(StringComparer.Ordinal);
  private static readonly List<Touch> _touches = new();
  private static readonly List<Touch> _touchesBuffer = new();

  public static bool mousePresent => true;
  public static bool anyKey => _pressedKeys.Count > 0 || _pressedMouseButtons.Count > 0;
  public static bool anyKeyDown => _justPressedKeys.Count > 0 || _justPressedMouseButtons.Count > 0;

  public static Vector3 mousePosition
  {
    get => _mousePosition;
    set => _mousePosition = value;
  }

  public static Vector2 mouseScrollDelta => _mouseScrollDelta;
  public static float mouseScrollDeltaX => _mouseScrollDelta.x;
  public static float mouseScrollDeltaY => _mouseScrollDelta.y;
  public static bool simulateMouseWithTouches { get; set; } = true;
  public static bool touchPressureSupported => false;
  public static bool multiTouchEnabled { get; set; } = true;
  public static bool stylusTouchSupported => false;
  public static int touchCount => _touches.Count;
  public static bool anyKeyWasPressedThisFrame => _justPressedKeys.Count > 0;
  public static bool anyKeyWasReleasedThisFrame => _justReleasedKeys.Count > 0;
  public static IMECompositionMode imeCompositionMode { get; set; } = IMECompositionMode.Auto;

  public static void ResetFrameState()
  {
    _ = _justPressedKeys.RemoveWhere(_ => true);
    _ = _justReleasedKeys.RemoveWhere(_ => true);
    _ = _justPressedMouseButtons.RemoveWhere(_ => true);
    _ = _justReleasedMouseButtons.RemoveWhere(_ => true);
    _mouseScrollDelta = default;

    for (var i = _touches.Count - 1; i >= 0; i--)
    {
      var touch = _touches[i];
      if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
      {
        _touches.RemoveAt(i);
      }
      else if (touch.phase == TouchPhase.Began)
      {
        touch.phase = TouchPhase.Stationary;
        _touches[i] = touch;
      }
    }
  }

  public static Touch GetTouch(int index)
  {
    if (index < 0 || index >= _touches.Count)
    {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    return _touches[index];
  }

  public static Touch[] touches
  {
    get
    {
      _touchesBuffer.Clear();
      _touchesBuffer.AddRange(_touches);
      return _touchesBuffer.ToArray();
    }
  }

  public static bool GetTouchSupported()
  {
    return true;
  }

  internal static void SetTouch(int fingerId, Vector2 position, TouchPhase phase, float pressure = 1f)
  {
    var existingIndex = _touches.FindIndex(t => t.fingerId == fingerId);
    var touch = new Touch
    {
      fingerId = fingerId,
      position = position,
      rawPosition = position,
      deltaPosition = Vector2.zero,
      deltaTime = Time.deltaTime,
      tapCount = 1,
      phase = phase,
      pressure = pressure,
      maximumPossiblePressure = 1f,
      type = TouchType.Direct
    };

    if (existingIndex >= 0)
    {
      var previous = _touches[existingIndex];
      touch.deltaPosition = position - previous.position;
      touch.tapCount = previous.tapCount;
      _touches[existingIndex] = touch;
    }
    else if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
    {
      touch.phase = TouchPhase.Began;
      _touches.Add(touch);
    }
  }

  internal static void ClearTouches()
  {
    _touches.Clear();
  }

  public static bool GetMouseButtonDown(int button)
  {
    return _justPressedMouseButtons.Contains(button);
  }

  public static bool GetMouseButton(int button)
  {
    return _pressedMouseButtons.Contains(button);
  }

  public static bool GetMouseButtonUp(int button)
  {
    return _justReleasedMouseButtons.Contains(button);
  }

  public static bool GetKeyDown(KeyCode key)
  {
    return _justPressedKeys.Contains(key);
  }

  public static bool GetKey(KeyCode key)
  {
    return _pressedKeys.Contains(key);
  }

  public static bool GetKeyUp(KeyCode key)
  {
    return _justReleasedKeys.Contains(key);
  }

  public static bool GetButton(string buttonName)
  {
    return _pressedKeys.Contains(ToKey(buttonName));
  }

  public static bool GetButtonDown(string buttonName)
  {
    return _justPressedKeys.Contains(ToKey(buttonName));
  }

  public static bool GetButtonUp(string buttonName)
  {
    return _justReleasedKeys.Contains(ToKey(buttonName));
  }

  public static bool GetButtonUp(string buttonName, bool defaultValue)
  {
    _ = defaultValue;
    return GetButtonUp(buttonName);
  }

  public static float GetAxis(string axisName)
  {
    return _axes.TryGetValue(axisName, out var value) ? value : 0f;
  }

  public static float GetAxisRaw(string axisName)
  {
    return GetAxis(axisName);
  }

  public static float GetAxisRaw(string axisName, bool raw)
  {
    _ = raw;
    return GetAxis(axisName);
  }

  public static Vector3 acceleration => Vector3.zero;
  public static Vector3 gyro => Vector3.zero;
  public static bool mouseAbsolute => true;
  public static bool compensateSensors => true;

  public static string[] GetJoystickNames()
  {
    return Array.Empty<string>();
  }

  public static bool IsJoystickPreconfigured()
  {
    return false;
  }

  public static string compositionString => string.Empty;
  public static IMECompositionMode imeSelected
  {
    get => IMECompositionMode.Auto;
    set => imeCompositionMode = value;
  }

  public static void ClearLastPenContactEvent() {}

  internal static void SetMouseButtonState(int button, bool down)
  {
    if (down)
    {
      if (_pressedMouseButtons.Add(button))
      {
        _ = _justPressedMouseButtons.Add(button);
      }
    }
    else
    {
      if (_pressedMouseButtons.Remove(button))
      {
        _ = _justReleasedMouseButtons.Add(button);
      }
    }
  }

  internal static void SetKeyState(KeyCode key, bool down)
  {
    if (down)
    {
      if (_pressedKeys.Add(key))
      {
        _ = _justPressedKeys.Add(key);
      }
    }
    else
    {
      if (_pressedKeys.Remove(key))
      {
        _ = _justReleasedKeys.Add(key);
      }
    }
  }

  internal static void SetAxis(string axis, float value)
  {
    _axes[axis] = value;
  }

  internal static void SetScrollWheel(float deltaX, float deltaY)
  {
    _mouseScrollDelta = new Vector2(deltaX, deltaY);
  }

  private static KeyCode ToKey(string buttonName)
  {
    if (string.IsNullOrWhiteSpace(buttonName))
    {
      return KeyCode.None;
    }

    if (Enum.TryParse(buttonName, true, out KeyCode parsed))
    {
      return parsed;
    }

    return buttonName.ToLowerInvariant() switch
    {
      "fire1" => KeyCode.Mouse0,
      "fire2" => KeyCode.Mouse1,
      _ => KeyCode.None
    };
  }
}

public enum IMECompositionMode
{
  Auto,
  On,
  Off
}

public struct Touch
{
  public int fingerId;
  public Vector2 position;
  public Vector2 rawPosition;
  public Vector2 deltaPosition;
  public float deltaTime;
  public int tapCount;
  public TouchPhase phase;
  public float pressure;
  public float maximumPossiblePressure;
  public TouchType type;
}

public enum TouchPhase
{
  Began,
  Moved,
  Stationary,
  Ended,
  Canceled
}

public enum TouchType
{
  Direct,
  Indirect,
  Stylus
}
