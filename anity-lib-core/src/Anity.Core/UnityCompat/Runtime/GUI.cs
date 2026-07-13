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
    _ = text;
    return DoButton(position);
  }

  public static bool Button(Rect position, GUIContent content)
  {
    _ = content;
    return DoButton(position);
  }

  public static bool Button(Rect position, string text, GUIStyle style)
  {
    _ = text; _ = style;
    return DoButton(position);
  }

  public static bool Button(Rect position, GUIContent content, GUIStyle style)
  {
    _ = content; _ = style;
    return DoButton(position);
  }

  private static int StableControlId(Rect position, int seed)
  {
    unchecked
    {
      int h = seed;
      h = (h * 397) ^ position.x.GetHashCode();
      h = (h * 397) ^ position.y.GetHashCode();
      h = (h * 397) ^ position.width.GetHashCode();
      h = (h * 397) ^ position.height.GetHashCode();
      return h == 0 ? 1 : h;
    }
  }

  private static bool DoButton(Rect position)
  {
    if (!_enabled) return false;
    var e = Event.current;
    if (e == null) return false;
    int id = StableControlId(position, 0xB007);
    bool hover = position.Contains(e.mousePosition);
    if (e.type == EventType.MouseDown && e.button == 0 && hover)
    {
      _hotControl = id;
      e.Use();
      return false;
    }
    if (e.type == EventType.MouseUp && e.button == 0 && _hotControl == id)
    {
      _hotControl = 0;
      e.Use();
      if (hover)
      {
        _changed = true;
        return true;
      }
    }
    return false;
  }

  /// <summary>Test/runtime: inject a full click on rect (MouseDown+Up at center).</summary>
  public static void SimulateClick(Rect position)
  {
    var center = new Vector2(position.x + position.width * 0.5f, position.y + position.height * 0.5f);
    Event.current = new Event
    {
      type = EventType.MouseDown,
      button = 0,
      mousePosition = center,
      clickCount = 1
    };
    DoButton(position);
    Event.current = new Event
    {
      type = EventType.MouseUp,
      button = 0,
      mousePosition = center,
      clickCount = 1
    };
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
    _ = text;
    return DoRepeatButton(position);
  }

  public static bool RepeatButton(Rect position, GUIContent content)
  {
    _ = content;
    return DoRepeatButton(position);
  }

  public static bool RepeatButton(Rect position, string text, GUIStyle style)
  {
    _ = text; _ = style;
    return DoRepeatButton(position);
  }

  public static bool RepeatButton(Rect position, GUIContent content, GUIStyle style)
  {
    _ = content; _ = style;
    return DoRepeatButton(position);
  }

  private static bool DoRepeatButton(Rect position)
  {
    if (!_enabled) return false;
    var e = Event.current;
    if (e == null) return false;
    int id = StableControlId(position, 0xBEE7);
    bool hover = position.Contains(e.mousePosition);
    if (e.type == EventType.MouseDown && e.button == 0 && hover)
    {
      _hotControl = id;
      e.Use();
      return true;
    }
    if (e.type == EventType.MouseDrag && _hotControl == id && hover)
      return true;
    if (e.type == EventType.MouseUp && _hotControl == id)
    {
      _hotControl = 0;
      e.Use();
    }
    return _hotControl == id && hover;
  }

  public static string TextField(Rect position, string text)
  {
    return DoTextField(position, text, -1);
  }

  public static string TextField(Rect position, string text, int maxLength)
  {
    return DoTextField(position, text, maxLength);
  }

  public static string TextField(Rect position, string text, GUIStyle style)
  {
    _ = style;
    return DoTextField(position, text, -1);
  }

  public static string TextField(Rect position, string text, int maxLength, GUIStyle style)
  {
    _ = style;
    return DoTextField(position, text, maxLength);
  }

  private static string DoTextField(Rect position, string text, int maxLength)
  {
    text ??= string.Empty;
    if (!_enabled) return text;
    var e = Event.current;
    if (e == null) return text;
    int id = StableControlId(position, 0x7E87);
    if (e.type == EventType.MouseDown && position.Contains(e.mousePosition))
    {
      _keyboardControl = id;
      e.Use();
    }
    if (_keyboardControl == id && e.type == EventType.KeyDown)
    {
      if (e.keyCode == KeyCode.Backspace && text.Length > 0)
      {
        text = text.Substring(0, text.Length - 1);
        _changed = true;
        e.Use();
      }
      else if (e.character != '\0' && !char.IsControl(e.character))
      {
        text += e.character;
        if (maxLength > 0 && text.Length > maxLength)
          text = text.Substring(0, maxLength);
        _changed = true;
        e.Use();
      }
    }
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
    _ = text;
    return DoToggle(position, value);
  }

  public static bool Toggle(Rect position, bool value, GUIContent content)
  {
    _ = content;
    return DoToggle(position, value);
  }

  public static bool Toggle(Rect position, bool value, string text, GUIStyle style)
  {
    _ = text; _ = style;
    return DoToggle(position, value);
  }

  public static bool Toggle(Rect position, bool value, GUIContent content, GUIStyle style)
  {
    _ = content; _ = style;
    return DoToggle(position, value);
  }

  private static bool DoToggle(Rect position, bool value)
  {
    if (!_enabled) return value;
    var e = Event.current;
    if (e == null) return value;
    if (e.type == EventType.MouseDown && e.button == 0 && position.Contains(e.mousePosition))
    {
      value = !value;
      _changed = true;
      e.Use();
    }
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
    return DoHorizontalSlider(position, value, leftValue, rightValue);
  }

  public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb)
  {
    _ = slider; _ = thumb;
    return DoHorizontalSlider(position, value, leftValue, rightValue);
  }

  private static float DoHorizontalSlider(Rect position, float value, float leftValue, float rightValue)
  {
    float min = Mathf.Min(leftValue, rightValue);
    float max = Mathf.Max(leftValue, rightValue);
    value = Mathf.Clamp(value, min, max);
    if (!_enabled) return value;
    var e = Event.current;
    if (e == null) return value;
    int id = StableControlId(position, 0x51D0);
    bool hover = position.Contains(e.mousePosition);
    if (e.type == EventType.MouseDown && e.button == 0 && hover)
    {
      _hotControl = id;
      float t0 = position.width > 0 ? (e.mousePosition.x - position.x) / position.width : 0f;
      value = Mathf.Lerp(leftValue, rightValue, Mathf.Clamp01(t0));
      _changed = true;
      e.Use();
    }
    if (e.type == EventType.MouseDrag && _hotControl == id)
    {
      float t = position.width > 0 ? (e.mousePosition.x - position.x) / position.width : 0f;
      t = Mathf.Clamp01(t);
      float next = Mathf.Lerp(leftValue, rightValue, t);
      if (!Mathf.Approximately(next, value))
      {
        value = next;
        _changed = true;
      }
      e.Use();
    }
    if (e.type == EventType.MouseUp && _hotControl == id)
    {
      _hotControl = 0;
      e.Use();
    }
    return value;
  }

  public static float VerticalSlider(Rect position, float value, float topValue, float bottomValue)
  {
    return DoVerticalSlider(position, value, topValue, bottomValue);
  }

  public static float VerticalSlider(Rect position, float value, float topValue, float bottomValue, GUIStyle slider, GUIStyle thumb)
  {
    _ = slider; _ = thumb;
    return DoVerticalSlider(position, value, topValue, bottomValue);
  }

  private static float DoVerticalSlider(Rect position, float value, float topValue, float bottomValue)
  {
    float min = Mathf.Min(topValue, bottomValue);
    float max = Mathf.Max(topValue, bottomValue);
    value = Mathf.Clamp(value, min, max);
    if (!_enabled) return value;
    var e = Event.current;
    if (e == null) return value;
    int id = StableControlId(position, 0x51D1);
    bool hover = position.Contains(e.mousePosition);
    if (e.type == EventType.MouseDown && e.button == 0 && hover)
    {
      _hotControl = id;
      float t0 = position.height > 0 ? (e.mousePosition.y - position.y) / position.height : 0f;
      value = Mathf.Lerp(topValue, bottomValue, Mathf.Clamp01(t0));
      _changed = true;
      e.Use();
    }
    if (e.type == EventType.MouseDrag && _hotControl == id)
    {
      float t = position.height > 0 ? (e.mousePosition.y - position.y) / position.height : 0f;
      t = Mathf.Clamp01(t);
      float next = Mathf.Lerp(topValue, bottomValue, t);
      if (!Mathf.Approximately(next, value))
      {
        value = next;
        _changed = true;
      }
      e.Use();
    }
    if (e.type == EventType.MouseUp && _hotControl == id)
    {
      _hotControl = 0;
      e.Use();
    }
    return value;
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
