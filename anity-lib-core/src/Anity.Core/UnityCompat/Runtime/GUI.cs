using System;
using System.Collections.Generic;

namespace UnityEngine;

public delegate void WindowFunction(int id);

public static class GUI
{
  private static GUISkin? _skin;
  private static Color _color = Color.white;
  private static Color _contentColor = Color.white;
  private static Color _backgroundColor = Color.white;
  private static bool _enabled = true;
  private static bool _changed;
  private static int _depth;
  private static Matrix4x4 _matrix = Matrix4x4.identity;
  private static readonly Stack<(bool enabled, Color color, Color contentColor, Color backgroundColor, Matrix4x4 matrix)> _groupStack = new();
  private static readonly Stack<Rect> _clipStack = new();
  private static readonly Dictionary<int, Rect> _windows = new();
  private static int _nextWindowId = 1000;
  private static int _hotControl;
  private static int _keyboardControl;
  private static string _nextControlName = string.Empty;
  private static int _nextControlId = 1000;

  public static GUISkin skin
  {
    get => _skin ??= new GUISkin();
    set => _skin = value;
  }

  public static Color color
  {
    get => _color;
    set => _color = value;
  }

  public static Color contentColor
  {
    get => _contentColor;
    set => _contentColor = value;
  }

  public static Color backgroundColor
  {
    get => _backgroundColor;
    set => _backgroundColor = value;
  }

  public static bool enabled
  {
    get => _enabled;
    set => _enabled = value;
  }

  public static bool changed
  {
    get => _changed;
    set => _changed = value;
  }

  public static int depth
  {
    get => _depth;
    set => _depth = value;
  }

  public static Matrix4x4 matrix
  {
    get => _matrix;
    set => _matrix = value;
  }

  public static int hotControl
  {
    get => _hotControl;
    set => _hotControl = value;
  }

  public static int keyboardControl
  {
    get => _keyboardControl;
    set => _keyboardControl = value;
  }

  public static void Label(Rect position, string text)
  {
    _ = position; _ = text;
  }

  public static void Label(Rect position, GUIContent content)
  {
    _ = position; _ = content;
  }

  public static void Label(Rect position, Texture image)
  {
    _ = position; _ = image;
  }

  public static void Label(Rect position, string text, GUIStyle style)
  {
    _ = position; _ = text; _ = style;
  }

  public static void Label(Rect position, GUIContent content, GUIStyle style)
  {
    _ = position; _ = content; _ = style;
  }

  public static bool Button(Rect position, string text)
  {
    _ = position; _ = text;
    return false;
  }

  public static bool Button(Rect position, GUIContent content)
  {
    _ = position; _ = content;
    return false;
  }

  public static bool Button(Rect position, string text, GUIStyle style)
  {
    _ = position; _ = text; _ = style;
    return false;
  }

  public static bool Button(Rect position, GUIContent content, GUIStyle style)
  {
    _ = position; _ = content; _ = style;
    return false;
  }

  public static void Box(Rect position, string text)
  {
    _ = position; _ = text;
  }

  public static void Box(Rect position, GUIContent content)
  {
    _ = position; _ = content;
  }

  public static void Box(Rect position, Texture image)
  {
    _ = position; _ = image;
  }

  public static void Box(Rect position, string text, GUIStyle style)
  {
    _ = position; _ = text; _ = style;
  }

  public static void Box(Rect position, GUIContent content, GUIStyle style)
  {
    _ = position; _ = content; _ = style;
  }

  public static bool RepeatButton(Rect position, string text)
  {
    _ = position; _ = text;
    return false;
  }

  public static bool RepeatButton(Rect position, GUIContent content)
  {
    _ = position; _ = content;
    return false;
  }

  public static bool RepeatButton(Rect position, string text, GUIStyle style)
  {
    _ = position; _ = text; _ = style;
    return false;
  }

  public static bool RepeatButton(Rect position, GUIContent content, GUIStyle style)
  {
    _ = position; _ = content; _ = style;
    return false;
  }

  public static string TextField(Rect position, string text)
  {
    _ = position;
    return text;
  }

  public static string TextField(Rect position, string text, int maxLength)
  {
    _ = position; _ = maxLength;
    return text;
  }

  public static string TextField(Rect position, string text, GUIStyle style)
  {
    _ = position; _ = style;
    return text;
  }

  public static string TextField(Rect position, string text, int maxLength, GUIStyle style)
  {
    _ = position; _ = maxLength; _ = style;
    return text;
  }

  public static string TextArea(Rect position, string text)
  {
    _ = position;
    return text;
  }

  public static string TextArea(Rect position, string text, int maxLength)
  {
    _ = position; _ = maxLength;
    return text;
  }

  public static string TextArea(Rect position, string text, GUIStyle style)
  {
    _ = position; _ = style;
    return text;
  }

  public static string TextArea(Rect position, string text, int maxLength, GUIStyle style)
  {
    _ = position; _ = maxLength; _ = style;
    return text;
  }

  public static string PasswordField(Rect position, string password, char maskChar)
  {
    _ = position; _ = maskChar;
    return password;
  }

  public static string PasswordField(Rect position, string password, char maskChar, int maxLength)
  {
    _ = position; _ = maskChar; _ = maxLength;
    return password;
  }

  public static string PasswordField(Rect position, string password, char maskChar, GUIStyle style)
  {
    _ = position; _ = maskChar; _ = style;
    return password;
  }

  public static string PasswordField(Rect position, string password, char maskChar, int maxLength, GUIStyle style)
  {
    _ = position; _ = maskChar; _ = maxLength; _ = style;
    return password;
  }

  public static bool Toggle(Rect position, bool value, string text)
  {
    _ = position; _ = text;
    return value;
  }

  public static bool Toggle(Rect position, bool value, GUIContent content)
  {
    _ = position; _ = content;
    return value;
  }

  public static bool Toggle(Rect position, bool value, string text, GUIStyle style)
  {
    _ = position; _ = text; _ = style;
    return value;
  }

  public static bool Toggle(Rect position, bool value, GUIContent content, GUIStyle style)
  {
    _ = position; _ = content; _ = style;
    return value;
  }

  public static int Toolbar(Rect position, int selected, string[] texts)
  {
    _ = position; _ = texts;
    return Math.Clamp(selected, 0, texts?.Length - 1 ?? 0);
  }

  public static int Toolbar(Rect position, int selected, GUIContent[] contents)
  {
    _ = position; _ = contents;
    return Math.Clamp(selected, 0, contents?.Length - 1 ?? 0);
  }

  public static int Toolbar(Rect position, int selected, string[] texts, GUIStyle style)
  {
    _ = position; _ = texts; _ = style;
    return Math.Clamp(selected, 0, texts?.Length - 1 ?? 0);
  }

  public static int Toolbar(Rect position, int selected, GUIContent[] contents, GUIStyle style)
  {
    _ = position; _ = contents; _ = style;
    return Math.Clamp(selected, 0, contents?.Length - 1 ?? 0);
  }

  public static int SelectionGrid(Rect position, int selected, string[] texts, int xCount)
  {
    _ = position; _ = texts; _ = xCount;
    return Math.Clamp(selected, 0, texts?.Length - 1 ?? 0);
  }

  public static int SelectionGrid(Rect position, int selected, GUIContent[] contents, int xCount)
  {
    _ = position; _ = contents; _ = xCount;
    return Math.Clamp(selected, 0, contents?.Length - 1 ?? 0);
  }

  public static int SelectionGrid(Rect position, int selected, string[] texts, int xCount, GUIStyle style)
  {
    _ = position; _ = texts; _ = xCount; _ = style;
    return Math.Clamp(selected, 0, texts?.Length - 1 ?? 0);
  }

  public static int SelectionGrid(Rect position, int selected, GUIContent[] contents, int xCount, GUIStyle style)
  {
    _ = position; _ = contents; _ = xCount; _ = style;
    return Math.Clamp(selected, 0, contents?.Length - 1 ?? 0);
  }

  public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue)
  {
    _ = position;
    return Mathf.Clamp(value, Mathf.Min(leftValue, rightValue), Mathf.Max(leftValue, rightValue));
  }

  public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb)
  {
    _ = position; _ = slider; _ = thumb;
    return Mathf.Clamp(value, Mathf.Min(leftValue, rightValue), Mathf.Max(leftValue, rightValue));
  }

  public static float VerticalSlider(Rect position, float value, float topValue, float bottomValue)
  {
    _ = position;
    return Mathf.Clamp(value, Mathf.Min(topValue, bottomValue), Mathf.Max(topValue, bottomValue));
  }

  public static float VerticalSlider(Rect position, float value, float topValue, float bottomValue, GUIStyle slider, GUIStyle thumb)
  {
    _ = position; _ = slider; _ = thumb;
    return Mathf.Clamp(value, Mathf.Min(topValue, bottomValue), Mathf.Max(topValue, bottomValue));
  }

  public static Vector2 HorizontalScrollbar(Rect position, Vector2 value, float size, float leftValue, float rightValue)
  {
    _ = position; _ = size; _ = leftValue; _ = rightValue;
    return value;
  }

  public static Vector2 VerticalScrollbar(Rect position, Vector2 value, float size, float topValue, float bottomValue)
  {
    _ = position; _ = size; _ = topValue; _ = bottomValue;
    return value;
  }

  public static Rect Window(int id, Rect clientRect, WindowFunction func, string text)
  {
    _ = text;
    return Window(id, clientRect, func, new GUIContent(text), GUI.skin.window);
  }

  public static Rect Window(int id, Rect clientRect, WindowFunction func, Texture image)
  {
    return Window(id, clientRect, func, new GUIContent(image), GUI.skin.window);
  }

  public static Rect Window(int id, Rect clientRect, WindowFunction func, GUIContent content)
  {
    return Window(id, clientRect, func, content, GUI.skin.window);
  }

  public static Rect Window(int id, Rect clientRect, WindowFunction func, string text, GUIStyle style)
  {
    return Window(id, clientRect, func, new GUIContent(text), style);
  }

  public static Rect Window(int id, Rect clientRect, WindowFunction func, GUIContent title, GUIStyle style)
  {
    _ = style;
    _windows[id] = clientRect;
    func?.Invoke(id);
    return clientRect;
  }

  public static void DragWindow()
  {
    DragWindow(new Rect(0, 0, 10000, 10000));
  }

  public static void DragWindow(Rect position)
  {
    _ = position;
  }

  public static void BringWindowToFront(int windowID)
  {
    _ = windowID;
  }

  public static void BringWindowToBack(int windowID)
  {
    _ = windowID;
  }

  public static void FocusWindow(int windowID)
  {
    _ = windowID;
  }

  public static void UnfocusWindow()
  {
  }

  public static string GetNameOfFocusedControl()
  {
    return _nextControlName;
  }

  public static void SetNextControlName(string name)
  {
    _nextControlName = name ?? string.Empty;
  }

  public static int GetControlID(int hint, FocusType focus)
  {
    _ = focus;
    return _nextControlId++;
  }

  public static int GetControlID(GUIContent contents, FocusType focus)
  {
    _ = contents; _ = focus;
    return _nextControlId++;
  }

  public static int GetControlID(FocusType focus)
  {
    _ = focus;
    return _nextControlId++;
  }

  public static void BeginGroup(Rect position)
  {
    BeginGroup(position, string.Empty);
  }

  public static void BeginGroup(Rect position, string text)
  {
    _groupStack.Push((_enabled, _color, _contentColor, _backgroundColor, _matrix));
    _ = position; _ = text;
  }

  public static void BeginGroup(Rect position, GUIContent content)
  {
    _groupStack.Push((_enabled, _color, _contentColor, _backgroundColor, _matrix));
    _ = position; _ = content;
  }

  public static void BeginGroup(Rect position, GUIStyle style)
  {
    _groupStack.Push((_enabled, _color, _contentColor, _backgroundColor, _matrix));
    _ = position; _ = style;
  }

  public static void BeginGroup(Rect position, string text, GUIStyle style)
  {
    _groupStack.Push((_enabled, _color, _contentColor, _backgroundColor, _matrix));
    _ = position; _ = text; _ = style;
  }

  public static void BeginGroup(Rect position, GUIContent content, GUIStyle style)
  {
    _groupStack.Push((_enabled, _color, _contentColor, _backgroundColor, _matrix));
    _ = position; _ = content; _ = style;
  }

  public static void EndGroup()
  {
    if (_groupStack.Count > 0)
    {
      var state = _groupStack.Pop();
      _enabled = state.enabled;
      _color = state.color;
      _contentColor = state.contentColor;
      _backgroundColor = state.backgroundColor;
      _matrix = state.matrix;
    }
  }

  public static void BeginClip(Rect position)
  {
    _clipStack.Push(position);
  }

  public static void EndClip()
  {
    if (_clipStack.Count > 0)
      _clipStack.Pop();
  }

  public static void ScrollTo(Rect position)
  {
    _ = position;
  }

  public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode = ScaleMode.StretchToFill, bool alphaBlend = true, float imageAspect = 0, float? matColor = null, Color? color = null)
  {
    _ = position; _ = image; _ = scaleMode; _ = alphaBlend; _ = imageAspect; _ = matColor; _ = color;
  }

  public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect, Color color)
  {
    _ = position; _ = image; _ = scaleMode; _ = alphaBlend; _ = imageAspect; _ = color;
  }

  public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect, Material mat)
  {
    _ = position; _ = image; _ = scaleMode; _ = alphaBlend; _ = imageAspect; _ = mat;
  }

  public static bool DragWindow(Rect position, out Rect newPosition)
  {
    newPosition = position;
    return false;
  }

  public static void FocusControl(string name)
  {
    _ = name;
  }
}

public static class GUIUtility
{
  private static int _hotControl;
  private static int _keyboardControl;
  private static int _nextControlId = 1000;
  private static Matrix4x4 _matrix = Matrix4x4.identity;
  private static readonly Stack<Matrix4x4> _matrixStack = new();

  public static int hotControl
  {
    get => _hotControl;
    set => _hotControl = value;
  }

  public static int keyboardControl
  {
    get => _keyboardControl;
    set => _keyboardControl = value;
  }

  public static int GetControlID(int hint, FocusType focus)
  {
    _ = focus;
    return _nextControlId++;
  }

  public static int GetControlID(FocusType focus)
  {
    _ = focus;
    return _nextControlId++;
  }

  public static int GetControlID(GUIContent contents, FocusType focus)
  {
    _ = contents; _ = focus;
    return _nextControlId++;
  }

  public static void RotateAroundPivot(float angle, Vector2 pivotPoint)
  {
    _ = angle; _ = pivotPoint;
  }

  public static void ScaleAroundPivot(Vector2 scale, Vector2 pivotPoint)
  {
    _ = scale; _ = pivotPoint;
  }

  public static Vector2 GUIToScreenPoint(Vector2 guiPoint)
  {
    return guiPoint;
  }

  public static Vector2 ScreenToGUIPoint(Vector2 screenPoint)
  {
    return screenPoint;
  }

  public static Rect GUIToScreenRect(Rect guiRect)
  {
    return guiRect;
  }

  public static Rect ScreenToGUIRect(Rect screenRect)
  {
    return screenRect;
  }

  public static void ExitGUI() { }

  public static int GetControlID(int hint, FocusType focus, Rect position)
  {
    _ = position;
    return GetControlID(hint, focus);
  }

  public static void PushMatrix()
  {
    _matrixStack.Push(_matrix);
  }

  public static void PopMatrix()
  {
    if (_matrixStack.Count > 0)
      _matrix = _matrixStack.Pop();
  }

  public static float pixelsPerPoint { get; set; } = 1f;

  public static Event ProcessEvent(Event e)
  {
    return e;
  }

  public static Vector2 QueryPointer(Vector2 pointerPosition)
  {
    return pointerPosition;
  }
}

public class GUISkin : Object
{
  public GUIStyle box { get; set; } = new();
  public GUIStyle button { get; set; } = new();
  public GUIStyle toggle { get; set; } = new();
  public GUIStyle label { get; set; } = new();
  public GUIStyle textField { get; set; } = new();
  public GUIStyle textArea { get; set; } = new();
  public GUIStyle window { get; set; } = new();
  public GUIStyle horizontalSlider { get; set; } = new();
  public GUIStyle horizontalSliderThumb { get; set; } = new();
  public GUIStyle verticalSlider { get; set; } = new();
  public GUIStyle verticalSliderThumb { get; set; } = new();
  public GUIStyle horizontalScrollbar { get; set; } = new();
  public GUIStyle horizontalScrollbarThumb { get; set; } = new();
  public GUIStyle horizontalScrollbarLeftButton { get; set; } = new();
  public GUIStyle horizontalScrollbarRightButton { get; set; } = new();
  public GUIStyle verticalScrollbar { get; set; } = new();
  public GUIStyle verticalScrollbarThumb { get; set; } = new();
  public GUIStyle verticalScrollbarUpButton { get; set; } = new();
  public GUIStyle verticalScrollbarDownButton { get; set; } = new();
  public GUIStyle scrollView { get; set; } = new();
  public GUIStyle[] customStyles { get; set; } = Array.Empty<GUIStyle>();
  public Font? font { get; set; }
  public GUIStyle FindStyle(string styleName)
  {
    return label;
  }
}
