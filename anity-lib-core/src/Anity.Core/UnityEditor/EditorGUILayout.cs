using UnityEngine;
using System;

namespace UnityEditor;

public static class EditorGUILayout
{
  public static bool HasMixedValue { get; set; }

  public static void LabelField(string label, string text)
  {
    GUILayout.Label($"{label}: {text}");
  }

  public static void LabelField(string label, string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    _ = options;
    GUILayout.Label($"{label}: {text}");
  }

  public static void LabelField(string label, Object? obj)
  {
    GUILayout.Label($"{label}: {obj?.GetType().Name ?? "null"}");
  }

  public static void LabelField(GUIContent label, GUIStyle? style = null, params GUILayoutOption[]? options)
  {
    _ = style;
    _ = options;
    GUILayout.Label(label?.text ?? string.Empty);
  }

  public static string TextField(string label, string text, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return GUILayout.TextField(text, options);
  }

  public static string TextArea(string label, string text, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return TextField(label, text, options);
  }

  public static int IntField(string label, int value, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return GUILayout.IntField(value, options);
  }

  public static float FloatField(string label, float value, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return GUILayout.FloatField(value, options);
  }

  public static double DoubleField(string label, double value, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    _ = options;
    return value;
  }

  public static bool ToggleLeft(string label, bool value, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return GUILayout.Toggle(value, "  ", options);
  }

  public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick = false, GUIStyle? style = null)
  {
    _ = toggleOnLabelClick;
    _ = style;
    GUILayout.Label(content);
    return foldout;
  }

  public static bool Foldout(bool foldout, GUIContent content, bool toggleOnLabelClick = false, GUIStyle? style = null)
  {
    return Foldout(foldout, content?.text ?? string.Empty, toggleOnLabelClick, style);
  }

  public static bool Toggle(string label, bool value, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return GUILayout.Toggle(value, string.Empty, options);
  }

  public static bool Button(string label, params GUILayoutOption[]? options)
  {
    return GUILayout.Button(label, options);
  }

  public static bool Button(string label, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    return Button(label, options);
  }

  public static int Popup(string label, int selectedIndex, string[] displayedOptions, params GUILayoutOption[]? options)
  {
    GUILayout.Label(label);
    return GUILayout.Popup(selectedIndex, displayedOptions, options);
  }

  public static void Popup(string label, int selectedIndex, GUIContent[] displayedOptions, GUIStyle? style = null)
  {
    _ = style;
    GUILayout.Label(label);
    _ = selectedIndex;
    _ = displayedOptions;
  }

  public static int Popup(string label, int selectedIndex, GUIContent[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return displayedOptions is null || displayedOptions.Length == 0 ? -1 : selectedIndex;
  }

  public static int IntPopup(string label, int selectedValue, string[] displayedOptions, int[] optionValues, params GUILayoutOption[]? options)
  {
    _ = optionValues;
    _ = options;
    return Popup(label, selectedValue, displayedOptions);
  }

  public static int IntSlider(string label, int value, int leftValue, int rightValue, params GUILayoutOption[]? options)
  {
    GUILayout.Label($"{label} [{leftValue}-{rightValue}]");
    _ = options;
    return value;
  }

  public static float Slider(string label, float value, float leftValue, float rightValue, params GUILayoutOption[]? options)
  {
    GUILayout.Label($"{label} [{leftValue}-{rightValue}]");
    _ = options;
    return value;
  }

  public static void MinMaxSlider(string label, ref float minValue, ref float maxValue, float minLimit, float maxLimit, params GUILayoutOption[]? options)
  {
    _ = options;
    _ = label;
    if (minValue > maxValue)
    {
      (minValue, maxValue) = (maxValue, minValue);
    }
    minValue = Math.Clamp(minValue, minLimit, maxLimit);
    maxValue = Math.Clamp(maxValue, minLimit, maxLimit);
  }

  public static int TagField(string label, int selectedTagIndex, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return selectedTagIndex;
  }

  public static int LayerField(string label, int layer, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return layer;
  }

  public static Vector3 Vector3Field(string label, Vector3 value, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return value;
  }

  public static Vector2 Vector2Field(string label, Vector2 value, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return value;
  }

  public static Quaternion QuaternionField(string label, Quaternion value, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return value;
  }

  public static Rect RectField(string label, Rect value, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return value;
  }

  public static Color ColorField(string label, Color value, bool showEyedropper = true, bool hdr = false, bool showAlpha = true, params GUILayoutOption[]? options)
  {
    _ = showEyedropper;
    _ = hdr;
    _ = showAlpha;
    _ = options;
    GUILayout.Label(label);
    return value;
  }

  public static T ObjectField<T>(string label, T? obj, bool allowSceneObjects) where T : class
  {
    _ = allowSceneObjects;
    GUILayout.Label(label);
    return obj!;
  }

  public static Object ObjectField(string label, Object? obj, System.Type objType, bool allowSceneObjects, params GUILayoutOption[]? options)
  {
    _ = objType;
    _ = allowSceneObjects;
    _ = options;
    GUILayout.Label(label);
    return obj!;
  }

  public static Object ObjectField(string label, Object? obj, bool allowSceneObjects, params GUILayoutOption[]? options)
  {
    _ = allowSceneObjects;
    _ = options;
    GUILayout.Label(label);
    return obj!;
  }

  public static T EnumPopup<T>(string label, T selected) where T : struct, System.Enum
  {
    GUILayout.Label(label);
    return selected;
  }

  public static int EnumPopup(string label, int selected)
  {
    GUILayout.Label(label);
    return selected;
  }

  public static string DelayedTextField(string label, string text)
  {
    return TextField(label, text);
  }

  public static int DelayedIntField(string label, int value)
  {
    GUILayout.Label(label);
    return value;
  }

  public static float DelayedFloatField(string label, float value)
  {
    GUILayout.Label(label);
    return value;
  }

  public static void PropertyField(SerializedProperty property, string label = "", bool includeChildren = true)
  {
    _ = includeChildren;
    GUILayout.Label($"{label}: {property?.propertyPath}");
  }

  public static void PropertyField(SerializedProperty property, bool includeChildren)
  {
    PropertyField(property, string.Empty, includeChildren);
  }

  public static int GetPropertyHeight(SerializedProperty property, GUIContent? label = null, bool includeChildren = true)
  {
    _ = property;
    _ = label;
    _ = includeChildren;
    return 18;
  }

  public static int GetPropertyHeight(SerializedProperty property, bool includeChildren)
  {
    return GetPropertyHeight(property, null, includeChildren);
  }

  public static Rect GetControlRect(bool hasLabel, float labelWidth = -1)
  {
    _ = hasLabel;
    _ = labelWidth;
    return new Rect(0f, 0f, 0f, 0f);
  }

  public static Rect GetControlRect(bool hasLabel, GUIStyle style)
  {
    _ = hasLabel;
    _ = style;
    return new Rect(0f, 0f, 0f, 0f);
  }

  public static Rect GetControlRect()
  {
    return new Rect(0f, 0f, 0f, 0f);
  }

  public static bool BeginFadeGroup(float fadeGroupKey)
  {
    _ = fadeGroupKey;
    return fadeGroupKey > 0f;
  }

  public static void EndFadeGroup() {}

  public static void PrefixLabel(string label, string tooltip, GUIStyle? style = null)
  {
    _ = label;
    _ = tooltip;
    _ = style;
  }

  public static void Separator() {}
  public static void Space(int pixels) => GUILayout.Space(pixels);

  public static void HelpBox(string message, MessageType type)
  {
    _ = type;
    GUILayout.Label(message);
  }

  public static void HelpBox(string message, MessageType type, bool wide)
  {
    _ = wide;
    HelpBox(message, type);
  }

  public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[]? options)
  {
    return GUILayout.BeginScrollView(scrollPosition, options);
  }

  public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle? horizontalScrollbar = null, GUIStyle? verticalScrollbar = null, GUIStyle? background = null, params GUILayoutOption[]? options)
  {
    _ = alwaysShowHorizontal;
    _ = alwaysShowVertical;
    _ = horizontalScrollbar;
    _ = verticalScrollbar;
    _ = background;
    return BeginScrollView(scrollPosition, options);
  }

  public static void EndScrollView() => GUILayout.EndScrollView();

  public static bool BeginToggleGroup(bool value, string label, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label);
    return value;
  }

  public static bool BeginToggleGroup(bool value, GUIContent label, params GUILayoutOption[]? options)
  {
    _ = options;
    GUILayout.Label(label?.text ?? string.Empty);
    return value;
  }

  public static void EndToggleGroup() {}

  public static bool BeginFoldoutHeaderGroup(bool foldout, string text)
  {
    GUILayout.Label(text);
    return foldout;
  }

  public static bool BeginFoldoutHeaderGroup(bool foldout, string text, GUIStyle? style)
  {
    _ = style;
    return BeginFoldoutHeaderGroup(foldout, text);
  }

  public static void EndFoldoutHeaderGroup() {}

  public static void BeginHorizontal() => GUILayout.BeginHorizontal();
  public static void BeginHorizontal(params GUILayoutOption[]? options) => GUILayout.BeginHorizontal(options);
  public static void BeginHorizontal(GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    BeginHorizontal(options);
  }

  public static void EndHorizontal() => GUILayout.EndHorizontal();

  public static void BeginVertical() => GUILayout.BeginVertical();
  public static void BeginVertical(params GUILayoutOption[]? options) => GUILayout.BeginVertical(options);
  public static void BeginVertical(GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style;
    BeginVertical(options);
  }

  public static void EndVertical() => GUILayout.EndVertical();

  public static void BeginDisabledGroup()
  {
    EditorGUI.BeginDisabledGroup();
  }

  public static void EndDisabledGroup()
  {
    EditorGUI.EndDisabledGroup();
  }

  public static void BeginChangeCheck() => EditorGUI.BeginChangeCheck();
  public static bool EndChangeCheck() => EditorGUI.EndChangeCheck();
}
