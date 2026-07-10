using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public static class GUILayout
{
  private static readonly Stack<string> _scope = new();

  public static void Space(int pixels) {}

  public static void FlexibleSpace() {}

  public static void Label(string text, params GUILayoutOption[]? options)
  {
    _ = text;
    _ = options;
  }

  public static void Label(string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = text;
    _ = style;
    _ = options;
  }

  public static void Label(GUIContent content, GUIStyle? style = null, params GUILayoutOption[]? options)
  {
    _ = content;
    _ = style;
    _ = options;
  }

  public static void Box(string text, params GUILayoutOption[]? options)
  {
    _ = text;
    _ = options;
  }

  public static void Box(string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    Box(text, options);
  }

  public static void Box(Texture? image, params GUILayoutOption[]? options)
  {
    _ = image;
    _ = options;
  }

  public static void Box(Texture? image, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    Box(image, options);
  }

  public static bool Button(string text, params GUILayoutOption[]? options)
  {
    _ = text;
    _ = options;
    return false;
  }

  public static bool Button(GUIContent content, GUIStyle? style = null, params GUILayoutOption[]? options)
  {
    _ = content;
    _ = style;
    _ = options;
    return false;
  }

  public static bool Button(string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    return Button(text, options);
  }

  public static bool Button(Texture? image, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = image;
    _ = style;
    _ = options;
    return false;
  }

  public static bool Button(Texture? image, params GUILayoutOption[]? options)
  {
    _ = image;
    _ = options;
    return false;
  }

  public static string TextField(string text, params GUILayoutOption[]? options)
  {
    _ = options;
    return text;
  }

  public static string TextField(Rect position, string text)
  {
    _ = position;
    return text;
  }

  public static string TextField(string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    return TextField(text, options);
  }

  public static string TextArea(string text, params GUILayoutOption[]? options)
  {
    _ = options;
    return text;
  }

  public static string TextArea(string text, GUIStyle? style)
  {
    _ = style;
    return text;
  }

  public static string PasswordField(string value, char ch, int maxLength, params GUILayoutOption[]? options)
  {
    _ = ch;
    _ = maxLength;
    _ = options;
    return value;
  }

  public static int IntField(int value, params GUILayoutOption[]? options)
  {
    _ = options;
    return value;
  }

  public static float FloatField(float value, params GUILayoutOption[]? options)
  {
    _ = options;
    return value;
  }

  public static bool Toggle(bool value, string text = "", params GUILayoutOption[]? options)
  {
    _ = text;
    _ = options;
    return value;
  }

  public static bool ToggleLeft(bool value, string text = "", params GUILayoutOption[]? options)
  {
    _ = text;
    _ = options;
    return value;
  }

  public static bool Toggle(bool value, string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    return Toggle(value, text, options);
  }

  public static int Popup(int selectedIndex, string[] displayedOptions, params GUILayoutOption[]? options)
  {
    if (displayedOptions is null || displayedOptions.Length == 0)
    {
      return -1;
    }

    return Math.Clamp(selectedIndex, 0, displayedOptions.Length - 1);
  }

  public static int IntPopup(int selectedIndex, string[] displayedOptions, int[] optionValues, params GUILayoutOption[]? options)
  {
    _ = optionValues;
    _ = options;
    return Popup(selectedIndex, displayedOptions);
  }

  public static int SelectionGrid(int selected, string[] displayedOptions, int xCount, params GUILayoutOption[]? options)
  {
    _ = xCount;
    _ = options;
    return Popup(selected, displayedOptions);
  }

  public static Vector2 MinMaxSlider(Rect position, float minValue, float maxValue, float minLimit, float maxLimit)
  {
    _ = position;
    _ = minValue;
    _ = maxValue;
    _ = minLimit;
    _ = maxLimit;
    return new Vector2(minValue, maxValue);
  }

  public static int Toolbar(int selected, string[] texts, params GUILayoutOption[]? options)
  {
    _ = texts;
    _ = options;
    return Math.Max(0, Math.Min(selected, texts?.Length - 1 ?? 0));
  }

  public static Vector2 Vector2Field(string label, Vector2 value, params GUILayoutOption[]? options)
  {
    _ = label;
    _ = options;
    return value;
  }

  public static Vector3 Vector3Field(string label, Vector3 value, params GUILayoutOption[]? options)
  {
    _ = label;
    _ = options;
    return value;
  }

  public static Color ColorField(string label, Color value, params GUILayoutOption[]? options)
  {
    _ = label;
    _ = options;
    return value;
  }

  public static Rect RectField(string label, Rect value, params GUILayoutOption[]? options)
  {
    _ = label;
    _ = options;
    return value;
  }

  public static T ObjectField<T>(string label, T? obj, bool allowSceneObjects = true, params GUILayoutOption[]? options) where T : class
  {
    _ = label;
    _ = allowSceneObjects;
    _ = options;
    return obj!;
  }

  public static Object ObjectField(string label, Object? obj, Type? type, bool allowSceneObjects = true, params GUILayoutOption[]? options)
  {
    _ = type;
    _ = label;
    _ = allowSceneObjects;
    _ = options;
    return obj!;
  }

  public static void BeginVertical(params GUILayoutOption[]? options)
  {
    BeginScope();
  }
  public static void BeginVertical(GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    BeginScope();
  }
  public static void EndVertical() => EndScope();

  public static void BeginHorizontal(params GUILayoutOption[]? options)
  {
    BeginScope();
  }
  public static void BeginHorizontal(GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    BeginScope();
  }
  public static void EndHorizontal() => EndScope();

  public static void BeginScrollView(Rect scrollViewRect, Vector2 scrollPosition, Rect viewRect)
  {
    _ = scrollViewRect;
    _ = scrollPosition;
    _ = viewRect;
  }

  public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[]? options)
  {
    _ = options;
    return scrollPosition;
  }

  public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle? horizontalScrollbar, GUIStyle? verticalScrollbar, GUIStyle? background, params GUILayoutOption[]? options)
  {
    _ = alwaysShowHorizontal;
    _ = alwaysShowVertical;
    _ = horizontalScrollbar;
    _ = verticalScrollbar;
    _ = background;
    _ = options;
    return scrollPosition;
  }

  public static void EndScrollView() {}

  public static void BeginArea(Rect screenRect, string text = "", GUIStyle? style = null, params GUILayoutOption[]? options)
  {
    _ = screenRect;
    _ = text;
    _ = style;
    _ = options;
  }

  public static void EndArea() {}

  public static void BeginDisabledGroup()
  {
    EditorGUI.BeginDisabledGroup();
  }

  public static void EndDisabledGroup()
  {
    EditorGUI.EndDisabledGroup();
  }

  public static void Space(float pixels)
  {
    Space((int)pixels);
  }

  public static GUILayoutOption Width(float width) => new("width", width);
  public static GUILayoutOption Height(float height) => new("height", height);
  public static GUILayoutOption MinWidth(float minWidth) => new("minWidth", minWidth);
  public static GUILayoutOption MaxWidth(float maxWidth) => new("maxWidth", maxWidth);
  public static GUILayoutOption MinHeight(float minHeight) => new("minHeight", minHeight);
  public static GUILayoutOption MaxHeight(float maxHeight) => new("maxHeight", maxHeight);
  public static GUILayoutOption ExpandHeight(bool value = true) => new("expandHeight", value);
  public static GUILayoutOption ExpandWidth(bool value = true) => new("expandWidth", value);
  public static GUILayoutOption ExpandSize(bool width = true, bool height = true) => new("expandSize", new Vector2(width ? 1f : 0f, height ? 1f : 0f));

  private static bool BeginScope()
  {
    _scope.Push("scope");
    return true;
  }

  private static void EndScope()
  {
    if (_scope.Count > 0)
    {
      _ = _scope.Pop();
    }
  }
}



