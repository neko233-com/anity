using UnityEngine;
using System;
using System.Linq;

namespace UnityEditor;

public static class EditorGUILayout
{
  public static bool HasMixedValue { get; set; }

  private static Rect GetRect(float height = 18f)
  {
    return GUILayoutUtility.GetControlRect(height);
  }

  private static Rect GetRect(GUIContent? label, float height = 18f)
  {
    _ = label;
    return GUILayoutUtility.GetControlRect(height);
  }

  public static void LabelField(string label, string text)
  {
    var rect = GetRect();
    EditorGUI.LabelField(rect, label, text);
  }

  public static void LabelField(string label, string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    EditorGUI.LabelField(rect, label, text, style);
  }

  public static void LabelField(string label, Object? obj)
  {
    var rect = GetRect();
    EditorGUI.LabelField(rect, label, obj?.GetType().Name ?? "null");
  }

  public static void LabelField(GUIContent label, GUIStyle? style = null, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    EditorGUI.LabelField(rect, label, style);
  }

  public static string TextField(string label, string text, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.TextField(rect, label, text);
  }

  public static string TextField(string text, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style; _ = options;
    var rect = GetRect();
    return EditorGUI.TextField(rect, text, style);
  }

  public static string TextArea(string label, string text, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(3 * 18f);
    return EditorGUI.TextField(rect, label, text);
  }

  public static int IntField(string label, int value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.IntField(rect, label, value);
  }

  public static float FloatField(string label, float value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.FloatField(rect, label, value);
  }

  public static double DoubleField(string label, double value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.DoubleField(rect, label, value);
  }

  public static long LongField(string label, long value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.LongField(rect, label, value);
  }

  public static bool ToggleLeft(string label, bool value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.ToggleLeft(rect, new GUIContent(label), value);
  }

  public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick = false, GUIStyle? style = null)
  {
    var rect = GetRect();
    return EditorGUI.Foldout(rect, foldout, content, toggleOnLabelClick, style);
  }

  public static bool Foldout(bool foldout, GUIContent content, bool toggleOnLabelClick = false, GUIStyle? style = null)
  {
    var rect = GetRect();
    return EditorGUI.Foldout(rect, foldout, content, toggleOnLabelClick, style);
  }

  public static bool Toggle(string label, bool value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Toggle(rect, label, value);
  }

  public static bool Button(string label, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Button(rect, label);
  }

  public static bool Button(string label, GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style; _ = options;
    var rect = GetRect();
    return EditorGUI.Button(rect, label, style);
  }

  public static int Popup(string label, int selectedIndex, string[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Popup(rect, selectedIndex, displayedOptions);
  }

  public static int Popup(string label, int selectedIndex, GUIContent[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Popup(rect, selectedIndex, displayedOptions);
  }

  public static int Popup(GUIContent label, int selectedIndex, GUIContent[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Popup(rect, selectedIndex, displayedOptions);
  }

  public static int IntPopup(string label, int selectedValue, string[] displayedOptions, int[] optionValues, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    var contents = displayedOptions.Select(s => new GUIContent(s)).ToArray();
    return EditorGUI.IntPopup(rect, selectedValue, contents, optionValues);
  }

  public static int IntSlider(string label, int value, int leftValue, int rightValue, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.IntSlider(rect, value, leftValue, rightValue);
  }

  public static float Slider(string label, float value, float leftValue, float rightValue, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Slider(rect, value, leftValue, rightValue);
  }

  public static void MinMaxSlider(string label, ref float minValue, ref float maxValue, float minLimit, float maxLimit, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    EditorGUI.MinMaxSlider(rect, ref minValue, ref maxValue, minLimit, maxLimit);
  }

  public static int TagField(string label, int selectedTagIndex, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.TagField(rect, selectedTagIndex, new[] { "Untagged" });
  }

  public static string TagField(string tag, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    EditorGUI.LabelField(rect, tag);
    return tag;
  }

  public static int LayerField(string label, int layer, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.LayerField(rect, layer);
  }

  public static int LayerField(int layer, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.LayerField(rect, layer);
  }

  public static Vector3 Vector3Field(string label, Vector3 value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Vector3Field(rect, label, value);
  }

  public static Vector2 Vector2Field(string label, Vector2 value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Vector2Field(rect, label, value);
  }

  public static Quaternion QuaternionField(string label, Quaternion value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    _ = label;
    return value;
  }

  public static Rect RectField(string label, Rect value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(18f * 2f);
    return EditorGUI.RectField(rect, label, value);
  }

  public static Color ColorField(string label, Color value, bool showEyedropper = true, bool hdr = false, bool showAlpha = true, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.ColorField(rect, label, value, showEyedropper, hdr, showAlpha);
  }

  public static T ObjectField<T>(string label, T? obj, bool allowSceneObjects) where T : class
  {
    var rect = GetRect();
    return EditorGUI.ObjectField<T>(rect, obj, allowSceneObjects);
  }

  public static Object ObjectField(string label, Object? obj, Type objType, bool allowSceneObjects, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.ObjectField(rect, label, obj!, objType, allowSceneObjects);
  }

  public static Object ObjectField(string label, Object? obj, bool allowSceneObjects, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.ObjectField(rect, label, obj!, typeof(Object), allowSceneObjects);
  }

  public static T EnumPopup<T>(string label, T selected) where T : struct, Enum
  {
    var rect = GetRect();
    return EditorGUI.EnumPopup(rect, selected);
  }

  public static Enum EnumPopup(string label, Enum selected)
  {
    var rect = GetRect();
    return EditorGUI.EnumPopup(rect, selected);
  }

  public static int EnumPopup(string label, int selected)
  {
    var rect = GetRect();
    _ = label;
    return selected;
  }

  public static string DelayedTextField(string label, string text)
  {
    var rect = GetRect();
    return EditorGUI.DelayedTextField(rect, text);
  }

  public static int DelayedIntField(string label, int value)
  {
    var rect = GetRect();
    return EditorGUI.DelayedIntField(rect, value);
  }

  public static float DelayedFloatField(string label, float value)
  {
    var rect = GetRect();
    return EditorGUI.DelayedFloatField(rect, value);
  }

  public static double DelayedDoubleField(string label, double value)
  {
    var rect = GetRect();
    return EditorGUI.DelayedDoubleField(rect, value);
  }

  public static void PropertyField(SerializedProperty property, string label = "", bool includeChildren = true)
  {
    var rect = GetRect();
    EditorGUI.PropertyField(rect, property, new GUIContent(label), includeChildren);
  }

  public static void PropertyField(SerializedProperty property, bool includeChildren)
  {
    PropertyField(property, string.Empty, includeChildren);
  }

  public static int GetPropertyHeight(SerializedProperty property, GUIContent? label = null, bool includeChildren = true)
  {
    _ = property; _ = label; _ = includeChildren;
    return 18;
  }

  public static int GetPropertyHeight(SerializedProperty property, bool includeChildren)
  {
    return GetPropertyHeight(property, null, includeChildren);
  }

  public static Rect GetControlRect(bool hasLabel, float labelWidth = -1)
  {
    _ = hasLabel; _ = labelWidth;
    return GetRect();
  }

  public static Rect GetControlRect(bool hasLabel, GUIStyle style)
  {
    _ = hasLabel; _ = style;
    return GetRect();
  }

  public static Rect GetControlRect()
  {
    return GetRect();
  }

  public static Rect GetControlRect(float height, params GUILayoutOption[]? options)
  {
    _ = options;
    return GetRect(height);
  }

  public static bool BeginFadeGroup(float fadeGroupKey)
  {
    return EditorGUI.BeginFadeGroup(fadeGroupKey);
  }

  public static void EndFadeGroup()
  {
    EditorGUI.EndFadeGroup();
  }

  public static void PrefixLabel(string label, string tooltip, GUIStyle? style = null)
  {
    var rect = GetRect();
    _ = tooltip; _ = style;
    EditorGUI.PrefixLabel(rect, 0, new GUIContent(label));
  }

  public static void Separator()
  {
    GUILayout.Box(string.Empty, GUILayout.Height(2f));
  }

  public static void Separator(float height)
  {
    GUILayout.Box(string.Empty, GUILayout.Height(height));
  }

  public static void Space(int pixels) => GUILayout.Space(pixels);
  public static void Space() => GUILayout.Space(0f);
  public static void Space(float pixels) => GUILayout.Space(pixels);

  public static void HelpBox(string message, MessageType type)
  {
    var rect = GetRect(36f);
    EditorGUI.HelpBox(rect, message, type);
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
    _ = alwaysShowHorizontal; _ = alwaysShowVertical; _ = horizontalScrollbar; _ = verticalScrollbar; _ = background;
    return BeginScrollView(scrollPosition, options);
  }

  public static void EndScrollView() => GUILayout.EndScrollView();

  public static bool BeginToggleGroup(bool value, string label, params GUILayoutOption[]? options)
  {
    _ = options;
    return EditorGUI.BeginToggleGroup(value, label);
  }

  public static bool BeginToggleGroup(bool value, GUIContent label, params GUILayoutOption[]? options)
  {
    _ = options;
    return EditorGUI.BeginToggleGroup(label, value);
  }

  public static void EndToggleGroup()
  {
    EditorGUI.EndToggleGroup();
  }

  public static bool BeginFoldoutHeaderGroup(bool foldout, string text)
  {
    return EditorGUI.BeginFoldoutHeaderGroup(foldout, text);
  }

  public static bool BeginFoldoutHeaderGroup(bool foldout, string text, GUIStyle? style)
  {
    _ = style;
    return BeginFoldoutHeaderGroup(foldout, text);
  }

  public static bool BeginFoldoutHeaderGroup(bool foldout, GUIContent content, GUIStyle? style = null)
  {
    _ = style;
    return EditorGUI.BeginFoldoutHeaderGroup(foldout, content, style);
  }

  public static void EndFoldoutHeaderGroup()
  {
    EditorGUI.EndFoldoutHeaderGroup();
  }

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

  public static void BeginDisabledGroup(bool disabled = false)
  {
    EditorGUI.BeginDisabledGroup(disabled);
  }

  public static void EndDisabledGroup()
  {
    EditorGUI.EndDisabledGroup();
  }

  public static void BeginChangeCheck() => EditorGUI.BeginChangeCheck();
  public static bool EndChangeCheck() => EditorGUI.EndChangeCheck();

  public static Vector4 Vector4Field(string label, Vector4 value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.Vector4Field(rect, label, value);
  }

  public static Bounds BoundsField(string label, Bounds value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(18f * 2f);
    return EditorGUI.BoundsField(rect, label, value);
  }

  public static AnimationCurve CurveField(string label, AnimationCurve value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.CurveField(rect, label, value);
  }

  public static Gradient GradientField(string label, Gradient value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.GradientField(rect, label, value);
  }

  public static int MaskField(string label, int mask, string[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.MaskField(rect, label, mask, displayedOptions);
  }

  public static void ProgressBar(float value, string text)
  {
    var rect = GetRect();
    EditorGUI.ProgressBar(rect, value, text);
  }

  public static bool InspectorTitlebar(bool expanded, Object targetObj)
  {
    var rect = GetRect();
    return EditorGUI.InspectorTitlebar(rect, expanded, targetObj);
  }

  public static bool InspectorTitlebar(bool expanded, Object[] targetObjs)
  {
    var rect = GetRect();
    return EditorGUI.InspectorTitlebar(rect, expanded, targetObjs);
  }

  public static int IntPopup(int selectedValue, GUIContent[] displayedOptions, int[] optionValues, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect();
    return EditorGUI.IntPopup(rect, selectedValue, displayedOptions, optionValues);
  }

  public static void EnumFlagsField(string label, Enum value)
  {
    var rect = GetRect();
    _ = value;
    EditorGUI.LabelField(rect, label, value.ToString());
  }

  public static Vector2 Vector2Field(GUIContent label, Vector2 value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Vector2Field(rect, label.text, value);
  }

  public static Vector3 Vector3Field(GUIContent label, Vector3 value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Vector3Field(rect, label.text, value);
  }

  public static Vector4 Vector4Field(GUIContent label, Vector4 value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Vector4Field(rect, label.text, value);
  }

  public static Color ColorField(GUIContent label, Color value, bool showEyedropper = true, bool hdr = false, bool showAlpha = true, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.ColorField(rect, label.text, value, showEyedropper, hdr, showAlpha);
  }

  public static AnimationCurve CurveField(GUIContent label, AnimationCurve value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.CurveField(rect, label.text, value);
  }

  public static Bounds BoundsField(GUIContent label, Bounds value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label, 18f * 2f);
    return EditorGUI.BoundsField(rect, label.text, value);
  }

  public static Gradient GradientField(GUIContent label, Gradient value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.GradientField(rect, label.text, value);
  }

  public static Rect RectField(GUIContent label, Rect value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label, 18f * 2f);
    return EditorGUI.RectField(rect, label.text, value);
  }

  public static int MaskField(GUIContent label, int mask, string[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.MaskField(rect, label.text, mask, displayedOptions);
  }

  public static int Popup(GUIContent label, int selectedIndex, string[] displayedOptions, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Popup(rect, selectedIndex, displayedOptions);
  }

  public static int IntPopup(GUIContent label, int selectedValue, string[] displayedOptions, int[] optionValues, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    var contents = displayedOptions.Select(s => new GUIContent(s)).ToArray();
    return EditorGUI.IntPopup(rect, selectedValue, contents, optionValues);
  }

  public static int IntSlider(GUIContent label, int value, int leftValue, int rightValue, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.IntSlider(rect, value, leftValue, rightValue);
  }

  public static float Slider(GUIContent label, float value, float leftValue, float rightValue, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Slider(rect, value, leftValue, rightValue);
  }

  public static void MinMaxSlider(GUIContent label, ref float minValue, ref float maxValue, float minLimit, float maxLimit, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    EditorGUI.MinMaxSlider(rect, ref minValue, ref maxValue, minLimit, maxLimit);
  }

  public static int TagField(GUIContent label, int selectedTagIndex, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.TagField(rect, selectedTagIndex, new[] { "Untagged" });
  }

  public static int LayerField(GUIContent label, int layer, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.LayerField(rect, layer);
  }

  public static string TextField(GUIContent label, string text, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.TextField(rect, label.text, text);
  }

  public static int IntField(GUIContent label, int value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.IntField(rect, label.text, value);
  }

  public static float FloatField(GUIContent label, float value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.FloatField(rect, label.text, value);
  }

  public static double DoubleField(GUIContent label, double value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.DoubleField(rect, label.text, value);
  }

  public static long LongField(GUIContent label, long value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.LongField(rect, label.text, value);
  }

  public static bool Toggle(GUIContent label, bool value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.Toggle(rect, label.text, value);
  }

  public static bool ToggleLeft(GUIContent label, bool value, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.ToggleLeft(rect, label, value);
  }

  public static bool Foldout(bool foldout, GUIContent content, bool toggleOnLabelClick, GUIStyle style, Action action)
  {
    _ = action;
    return Foldout(foldout, content, toggleOnLabelClick, style);
  }

  public static Object ObjectField(GUIContent label, Object? obj, Type objType, bool allowSceneObjects, params GUILayoutOption[]? options)
  {
    _ = options;
    var rect = GetRect(label);
    return EditorGUI.ObjectField(rect, label, obj!, objType, allowSceneObjects);
  }

  public static T EnumPopup<T>(GUIContent label, T selected) where T : struct, Enum
  {
    var rect = GetRect(label);
    return EditorGUI.EnumPopup(rect, selected);
  }

  public static Enum EnumPopup(GUIContent label, Enum selected)
  {
    var rect = GetRect(label);
    return EditorGUI.EnumPopup(rect, selected);
  }

  public static string DelayedTextField(GUIContent label, string text)
  {
    var rect = GetRect(label);
    return EditorGUI.DelayedTextField(rect, text);
  }

  public static int DelayedIntField(GUIContent label, int value)
  {
    var rect = GetRect(label);
    return EditorGUI.DelayedIntField(rect, value);
  }

  public static float DelayedFloatField(GUIContent label, float value)
  {
    var rect = GetRect(label);
    return EditorGUI.DelayedFloatField(rect, value);
  }

  public static double DelayedDoubleField(GUIContent label, double value)
  {
    var rect = GetRect(label);
    return EditorGUI.DelayedDoubleField(rect, value);
  }
}
