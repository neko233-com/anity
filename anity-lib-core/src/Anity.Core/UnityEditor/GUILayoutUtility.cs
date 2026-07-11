using UnityEngine;

namespace UnityEditor;

public static class GUILayoutUtility
{
  private static Rect _lastRect = new(0, 0, 200, 18);
  private static float _currentY;

  public static Rect GetRect(float width, float height)
  {
    var rect = new Rect(0, _currentY, width, height);
    _currentY += height;
    return rect;
  }

  public static Rect GetRect(float height)
  {
    return GetRect(200f, height);
  }

  public static Rect GetRect(GUIContent content, GUIStyle? style = null)
  {
    _ = content; _ = style;
    return GetRect(18f);
  }

  public static Rect GetControlRect(float height, params GUILayoutOption[]? options)
  {
    _ = options;
    return GetRect(height);
  }

  public static Rect GetControlRect(bool hasLabel, float height, GUIStyle? style = null)
  {
    _ = hasLabel; _ = style;
    return GetRect(height);
  }

  public static Rect GetControlRect(bool hasLabel, float height, float labelWidth)
  {
    _ = hasLabel; _ = labelWidth;
    return GetRect(height);
  }

  public static Rect GetLastRect()
  {
    return _lastRect;
  }

  public static void ResetCursor()
  {
    _currentY = 0f;
  }
}
