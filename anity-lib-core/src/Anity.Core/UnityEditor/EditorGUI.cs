using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public static class EditorGUI
{
  private static bool _isEditing;
  private static bool _hasChanged;
  private static readonly Stack<HorizontalScope> _horizontalStack = new();
  private static readonly Stack<VerticalScope> _verticalStack = new();
  private static readonly Stack<bool> _disabledStack = new();
  private static readonly Stack<ToggleGroupScope> _toggleGroupStack = new();
  private static readonly Stack<AreaScope> _areaStack = new();
  private static readonly Stack<PropertyScope> _propertyStack = new();
  private static readonly Stack<FadeGroupScope> _fadeGroupStack = new();
  private static readonly Stack<FoldoutHeaderScope> _foldoutHeaderStack = new();
  private static readonly Stack<int> _buildTargetStack = new();
  private static readonly Stack<ScrollViewScope> _scrollViewStack = new();
  private static int _nextControlId = 1000;

  public static bool Enabled { get; set; } = true;
  public static bool showMixedValue { get; set; }
  public static int indentLevel { get; set; }
  public static Utility.PrefixLabelMode PrefixLabelMode { get; set; }
  public static float fieldWidth { get; set; } = -1f;
  public static float labelWidth { get; set; } = -1f;
  public static string delayedText { get; set; } = string.Empty;

  public static void BeginChangeCheck()
  {
    _isEditing = true;
    _hasChanged = false;
  }

  public static bool EndChangeCheck()
  {
    var changed = _isEditing && _hasChanged;
    _isEditing = false;
    _hasChanged = false;
    return changed;
  }

  private static void MarkChanged()
  {
    if (_isEditing)
    {
      _hasChanged = true;
    }
  }

  public static bool Toggle(Rect position, bool value, GUIContent? label = null)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static bool Toggle(Rect position, string label, bool value)
  {
    return Toggle(position, value, new GUIContent(label));
  }

  public static bool Toggle(bool value, string label = "")
  {
    _ = label;
    return value;
  }

  public static bool Toggle(string label, bool value, bool leftValue)
  {
    _ = leftValue;
    return Toggle(value, label);
  }

  public static bool Toggle(string label, bool value)
  {
    return Toggle(value, label);
  }

  public static bool ToggleLeft(Rect position, GUIContent label, bool value, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return value;
  }

  public static bool ToggleLeft(string label, bool value, bool leftValue = false)
  {
    _ = leftValue;
    return Toggle(value, label);
  }

  public static bool ToggleLeft(Rect position, bool value, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return value;
  }

  public static void Space(int pixels)
  {
    _ = pixels;
  }

  public static void Space(float pixels)
  {
    _ = pixels;
  }

  public static int IntField(Rect position, int value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static int IntField(Rect position, string label, int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static int IntField(Rect position, GUIContent label, int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static int IntField(int value, string label = "")
  {
    _ = label;
    return value;
  }

  public static int IntField(string label, int value)
  {
    return IntField(value, label);
  }

  public static float FloatField(Rect position, float value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static float FloatField(Rect position, string label, float value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static float FloatField(Rect position, GUIContent label, float value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static float FloatField(float value, string label = "")
  {
    _ = label;
    return value;
  }

  public static float FloatField(string label, float value)
  {
    return FloatField(value, label);
  }

  public static double DoubleField(Rect position, double value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static double DoubleField(Rect position, string label, double value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static double DoubleField(Rect position, GUIContent label, double value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static double DoubleField(double value, string label = "")
  {
    _ = label;
    return value;
  }

  public static double DoubleField(string label, double value)
  {
    return DoubleField(value, label);
  }

  public static long LongField(Rect position, long value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static long LongField(Rect position, string label, long value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static long LongField(Rect position, GUIContent label, long value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static long LongField(long value, string label = "")
  {
    _ = label;
    return value;
  }

  public static string TextArea(Rect position, string value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static string TextArea(string value, int maxLines = 3)
  {
    _ = maxLines;
    return value;
  }

  public static string TextField(Rect position, string value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static string TextField(Rect position, string label, string value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static string TextField(Rect position, GUIContent label, string value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static string TextField(string value, string label = "")
  {
    _ = label;
    return value;
  }

  public static string TextField(string value, bool readOnly)
  {
    _ = readOnly;
    return value;
  }

  public static string DelayedTextField(Rect position, string value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static string DelayedTextField(Rect position, string label, string value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static string DelayedTextField(Rect position, GUIContent label, string value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static string DelayedTextField(string value, string label = "")
  {
    _ = label;
    return value;
  }

  public static int DelayedIntField(Rect position, int value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static int DelayedIntField(Rect position, string label, int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static int DelayedIntField(Rect position, GUIContent label, int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static int DelayedIntField(int value, string label = "")
  {
    _ = label;
    return value;
  }

  public static float DelayedFloatField(Rect position, float value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static float DelayedFloatField(Rect position, string label, float value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static float DelayedFloatField(Rect position, GUIContent label, float value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static float DelayedFloatField(float value, string label = "")
  {
    _ = label;
    return value;
  }

  public static double DelayedDoubleField(Rect position, double value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static double DelayedDoubleField(Rect position, string label, double value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static double DelayedDoubleField(Rect position, GUIContent label, double value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static double DelayedDoubleField(double value, string label = "")
  {
    _ = label;
    return value;
  }

  public static string PasswordField(Rect position, string value, char passwordChar, GUIStyle? style = null)
  {
    _ = position;
    _ = passwordChar;
    _ = style;
    return value;
  }

  public static string PasswordField(string value, char passwordChar, int maxLength = 0, string label = "")
  {
    _ = passwordChar;
    _ = maxLength;
    _ = label;
    return value;
  }

  public static int Popup(Rect position, int selectedValue, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    if (displayedOptions is null || displayedOptions.Length == 0) return -1;
    return Math.Clamp(selectedValue, 0, displayedOptions.Length - 1);
  }

  public static int Popup(Rect position, int selectedValue, GUIContent[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    if (displayedOptions is null || displayedOptions.Length == 0) return -1;
    return Math.Clamp(selectedValue, 0, displayedOptions.Length - 1);
  }

  public static int Popup(int selectedValue, string[] displayedOptions, int[]? optionValues = null, GUIStyle? style = null, string label = "")
  {
    _ = optionValues;
    _ = style;
    _ = label;
    if (displayedOptions is null || displayedOptions.Length == 0) return -1;
    return Math.Clamp(selectedValue, 0, displayedOptions.Length - 1);
  }

  public static int Popup(int selectedValue, GUIContent[] displayedOptions, GUIStyle? style = null, string label = "")
  {
    _ = style;
    _ = label;
    if (displayedOptions is null || displayedOptions.Length == 0) return -1;
    return Math.Clamp(selectedValue, 0, displayedOptions.Length - 1);
  }

  public static int IntPopup(Rect position, int selectedValue, GUIContent[] displayedOptions, int[] optionValues, GUIStyle? style = null)
  {
    _ = position;
    _ = optionValues;
    _ = style;
    return Popup(selectedValue, displayedOptions);
  }

  public static int IntPopup(int selectedValue, string[] displayedOptions, int[] optionValues, string label = "")
  {
    _ = optionValues;
    _ = label;
    return Popup(selectedValue, displayedOptions);
  }

  public static int IntPopup(int selectedValue, GUIContent[] displayedOptions, int[] optionValues, GUIStyle? style = null)
  {
    _ = optionValues;
    _ = style;
    return Popup(selectedValue, displayedOptions);
  }

  public static int EnumMaskField(Rect position, int value, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = displayedOptions;
    _ = style;
    return value;
  }

  public static int EnumMaskField(Rect position, GUIContent label, int value, string[] displayedOptions)
  {
    _ = position;
    _ = label;
    _ = displayedOptions;
    return value;
  }

  public static int EnumMaskField(int value, string label, string[]? displayedOptions = null)
  {
    _ = label;
    _ = displayedOptions;
    return value;
  }

  public static int EnumMaskField(int value, GUIContent[] displayedOptions)
  {
    _ = displayedOptions;
    return value;
  }

  public static void LabelField(Rect position, GUIContent label, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
  }

  public static void LabelField(Rect position, string label, GUIStyle? style = null)
  {
    LabelField(position, new GUIContent(label), style);
  }

  public static void LabelField(string label, string text)
  {
    GUILayout.Label($"{label}: {text}");
  }

  public static void LabelField(Rect position, string label, string text, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    GUILayout.Label($"{label}: {text}");
  }

  public static Vector2 Vector2Field(Rect position, string label, Vector2 value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector2 Vector2Field(Rect position, GUIContent label, Vector2 value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector2 Vector2Field(Vector2 value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Vector2 Vector2Field(Rect position, Vector2 value)
  {
    _ = position;
    return value;
  }

  public static Vector2 Vector2Field(string label, Vector2 value)
  {
    _ = label;
    return value;
  }

  public static Vector3 Vector3Field(Rect position, Vector3 value)
  {
    _ = position;
    return value;
  }

  public static Vector3 Vector3Field(Rect position, string label, Vector3 value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector3 Vector3Field(Rect position, GUIContent label, Vector3 value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector3 Vector3Field(Vector3 value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Vector3 Vector3Field(string label, Vector3 value)
  {
    _ = label;
    return value;
  }

  public static Vector4 Vector4Field(Rect position, Vector4 value)
  {
    _ = position;
    return value;
  }

  public static Vector4 Vector4Field(Rect position, string label, Vector4 value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector4 Vector4Field(Rect position, GUIContent label, Vector4 value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector4 Vector4Field(Vector4 value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Vector4 Vector4Field(string label, Vector4 value)
  {
    _ = label;
    return value;
  }

  public static Rect RectField(Rect position, string label, Rect value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Rect RectField(Rect position, GUIContent label, Rect value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Rect RectField(Rect value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Rect RectField(Rect position, Rect value)
  {
    _ = position;
    return value;
  }

  public static Rect RectField(string label, Rect value)
  {
    _ = label;
    return value;
  }

  public static Bounds BoundsField(Rect position, Bounds value)
  {
    _ = position;
    return value;
  }

  public static Bounds BoundsField(Rect position, string label, Bounds value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Bounds BoundsField(Rect position, GUIContent label, Bounds value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Bounds BoundsField(Bounds value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Bounds BoundsField(string label, Bounds value)
  {
    _ = label;
    return value;
  }

  public static Color ColorField(Rect position, string label, Color value, bool showEyedropper = true, bool hdr = false, bool showAlpha = true)
  {
    _ = position;
    _ = label;
    _ = showEyedropper;
    _ = hdr;
    _ = showAlpha;
    return value;
  }

  public static Color ColorField(Rect position, GUIContent label, Color value, bool showEyedropper = true, bool hdr = false, bool showAlpha = true)
  {
    _ = position;
    _ = label;
    _ = showEyedropper;
    _ = hdr;
    _ = showAlpha;
    return value;
  }

  public static Color ColorField(Rect position, Color value, bool showEyedropper = true, bool hdr = false, bool showAlpha = true)
  {
    _ = position;
    _ = showEyedropper;
    _ = hdr;
    _ = showAlpha;
    return value;
  }

  public static Color ColorField(Color value, string label = "", bool showEyedropper = true, bool hdr = false, bool showAlpha = true)
  {
    _ = label;
    _ = showEyedropper;
    _ = hdr;
    _ = showAlpha;
    return value;
  }

  public static Color ColorField(Rect position, GUIContent label, Color value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static AnimationCurve CurveField(Rect position, string label, AnimationCurve value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static AnimationCurve CurveField(Rect position, GUIContent label, AnimationCurve value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static AnimationCurve CurveField(Rect position, AnimationCurve value)
  {
    _ = position;
    return value;
  }

  public static AnimationCurve CurveField(string label, AnimationCurve value)
  {
    _ = label;
    return value;
  }

  public static Gradient GradientField(Rect position, Gradient value)
  {
    _ = position;
    return value;
  }

  public static Gradient GradientField(Rect position, string label, Gradient value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Gradient GradientField(Rect position, GUIContent label, Gradient value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Gradient GradientField(string label, Gradient value)
  {
    _ = label;
    return value;
  }

  public static Object ObjectField(Rect position, Object obj, Type objType, bool allowSceneObjects)
  {
    _ = position;
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static Object ObjectField(Rect position, string label, Object obj, Type objType, bool allowSceneObjects = true)
  {
    _ = position;
    _ = label;
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static Object ObjectField(Rect position, GUIContent label, Object obj, Type objType, bool allowSceneObjects = true)
  {
    _ = position;
    _ = label;
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static T ObjectField<T>(string label, T? obj, bool allowSceneObjects) where T : class
  {
    _ = label;
    _ = allowSceneObjects;
    return obj!;
  }

  public static T ObjectField<T>(Rect position, T? obj, bool allowSceneObjects) where T : class
  {
    _ = position;
    _ = allowSceneObjects;
    return obj!;
  }

  public static Object ObjectField(Object obj, Type objType, bool allowSceneObjects)
  {
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static Object ObjectField(string label, Object obj, Type objType, bool allowSceneObjects = true)
  {
    _ = label;
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static Object ObjectField(GUIContent label, Object obj, Type objType, bool allowSceneObjects = true)
  {
    _ = label;
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static T ObjectField<T>(GUIContent label, T? obj, bool allowSceneObjects) where T : class
  {
    _ = label;
    _ = allowSceneObjects;
    return obj!;
  }

  public static T EnumPopup<T>(Rect position, T selected) where T : struct, Enum
  {
    _ = position;
    return selected;
  }

  public static T EnumPopup<T>(string label, T selected) where T : struct, Enum
  {
    _ = label;
    return selected;
  }

  public static Enum EnumPopup(Rect position, Enum selected, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return selected;
  }

  public static Enum EnumPopup(Rect position, string label, Enum selected, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return selected;
  }

  public static Enum EnumPopup(Rect position, GUIContent label, Enum selected, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return selected;
  }

  public static Enum EnumPopup(string label, Enum selected, GUIStyle? style = null)
  {
    _ = label;
    _ = style;
    return selected;
  }

  public static Enum EnumPopup(GUIContent label, Enum selected, GUIStyle? style = null)
  {
    _ = label;
    _ = style;
    return selected;
  }

  public static string TagField(Rect position, string tag, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return tag;
  }

  public static string TagField(Rect position, string label, string tag, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return tag;
  }

  public static string TagField(Rect position, GUIContent label, string tag, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return tag;
  }

  public static string TagField(string tag, GUIStyle? style = null)
  {
    _ = style;
    return tag;
  }

  public static string TagField(string label, string tag, GUIStyle? style = null)
  {
    _ = label;
    _ = style;
    return tag;
  }

  public static string TagField(GUIContent label, string tag, GUIStyle? style = null)
  {
    _ = label;
    _ = style;
    return tag;
  }

  public static int TagField(Rect position, int selectedTagIndex, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = displayedOptions;
    _ = style;
    return selectedTagIndex;
  }

  public static int TagField(Rect position, string label, int selectedTagIndex, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = displayedOptions;
    _ = style;
    return selectedTagIndex;
  }

  public static int TagField(Rect position, GUIContent label, int selectedTagIndex, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = displayedOptions;
    _ = style;
    return selectedTagIndex;
  }

  public static int TagField(string label, int selectedTagIndex, string[] displayedOptions)
  {
    _ = label;
    _ = displayedOptions;
    return selectedTagIndex;
  }

  public static int TagField(GUIContent label, int selectedTagIndex, string[] displayedOptions)
  {
    _ = label;
    _ = displayedOptions;
    return selectedTagIndex;
  }

  public static int LayerField(Rect position, int layer, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return layer;
  }

  public static int LayerField(Rect position, string label, int layer, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return layer;
  }

  public static int LayerField(Rect position, GUIContent label, int layer, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return layer;
  }

  public static int LayerField(int layer)
  {
    return layer;
  }

  public static int LayerField(string label, int layer)
  {
    _ = label;
    return layer;
  }

  public static int LayerField(GUIContent label, int layer)
  {
    _ = label;
    return layer;
  }

  public static int LayerMaskField(Rect position, int layerMask, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return layerMask;
  }

  public static int LayerMaskField(Rect position, string label, int layerMask, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return layerMask;
  }

  public static int LayerMaskField(Rect position, GUIContent label, int layerMask, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return layerMask;
  }

  public static int LayerMaskField(int layerMask)
  {
    return layerMask;
  }

  public static int LayerMaskField(string label, int layerMask)
  {
    _ = label;
    return layerMask;
  }

  public static int LayerMaskField(GUIContent label, int layerMask)
  {
    _ = label;
    return layerMask;
  }

  public static int MaskField(Rect position, int mask, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = displayedOptions;
    _ = style;
    return mask;
  }

  public static int MaskField(Rect position, string label, int mask, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = displayedOptions;
    _ = style;
    return mask;
  }

  public static int MaskField(Rect position, GUIContent label, int mask, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = displayedOptions;
    _ = style;
    return mask;
  }

  public static int MaskField(int mask, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = displayedOptions;
    _ = style;
    return mask;
  }

  public static int MaskField(string label, int mask, string[] displayedOptions)
  {
    _ = label;
    _ = displayedOptions;
    return mask;
  }

  public static int MaskField(GUIContent label, int mask, string[] displayedOptions)
  {
    _ = label;
    _ = displayedOptions;
    return mask;
  }

  public static void PropertyField(Rect position, SerializedProperty property, GUIContent? label = null, bool includeChildren = true)
  {
    _ = position;
    _ = property;
    _ = label;
    _ = includeChildren;
  }

  public static void PropertyField(SerializedProperty property, string? label = null, bool includeChildren = true)
  {
    _ = property;
    _ = label;
    _ = includeChildren;
  }

  public static bool DelayButton(int controlID, string label = "", GUIStyle? style = null)
  {
    _ = controlID;
    _ = label;
    _ = style;
    return false;
  }

  public static bool DelayButton(Rect position, int controlID, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = controlID;
    _ = content;
    _ = style;
    return false;
  }

  public static bool Button(Rect position, string text, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return false;
  }

  public static bool Button(Rect position, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return false;
  }

  public static bool Button(string text, GUIStyle? style = null)
  {
    _ = text;
    _ = style;
    return false;
  }

  public static void DrawRect(Rect rect, Color color)
  {
    _ = rect;
    _ = color;
  }

  public static void DrawRect(Rect rect, string text)
  {
    _ = rect;
    _ = text;
  }

  public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode = ScaleMode.StretchToFill, bool alphaBlend = true, float imageAspect = 0)
  {
    _ = position;
    _ = image;
    _ = scaleMode;
    _ = alphaBlend;
    _ = imageAspect;
  }

  public static void DrawPreviewTexture(Rect position, Texture image)
  {
    _ = position;
    _ = image;
  }

  public static void DrawPreviewTexture(Rect position, Texture image, Material mat, ScaleMode scaleMode = ScaleMode.StretchToFill)
  {
    _ = position;
    _ = image;
    _ = mat;
    _ = scaleMode;
  }

  public static void DrawLabel(Rect position, string label, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
  }

  public static void DrawLabel(Rect position, GUIContent label, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
  }

  public static void MinMaxSlider(Rect position, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
  {
    _ = position;
    _ = minLimit;
    _ = maxLimit;
    if (minValue > maxValue) { var t = minValue; minValue = maxValue; maxValue = t; }
    minValue = Math.Clamp(minValue, minLimit, maxLimit);
    maxValue = Math.Clamp(maxValue, minLimit, maxLimit);
  }

  public static void MinMaxSlider(Rect position, string label, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
  {
    _ = position;
    _ = label;
    _ = minLimit;
    _ = maxLimit;
    if (minValue > maxValue) { var t = minValue; minValue = maxValue; maxValue = t; }
    minValue = Math.Clamp(minValue, minLimit, maxLimit);
    maxValue = Math.Clamp(maxValue, minLimit, maxLimit);
  }

  public static void MinMaxSlider(Rect position, GUIContent label, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
  {
    _ = position;
    _ = label;
    _ = minLimit;
    _ = maxLimit;
    if (minValue > maxValue) { var t = minValue; minValue = maxValue; maxValue = t; }
    minValue = Math.Clamp(minValue, minLimit, maxLimit);
    maxValue = Math.Clamp(maxValue, minLimit, maxLimit);
  }

  public static void MinMaxSlider(string label, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
  {
    _ = label;
    _ = minLimit;
    _ = maxLimit;
    if (minValue > maxValue) { var t = minValue; minValue = maxValue; maxValue = t; }
    minValue = Math.Clamp(minValue, minLimit, maxLimit);
    maxValue = Math.Clamp(maxValue, minLimit, maxLimit);
  }

  public static Rect PrefixLabel(Rect totalPosition, GUIContent label)
  {
    _ = label;
    return totalPosition;
  }

  public static Rect PrefixLabel(Rect position, int controlID, GUIContent label, GUIStyle? labelStyle = null)
  {
    _ = controlID;
    _ = labelStyle;
    _ = label;
    return position;
  }

  public static bool Foldout(Rect position, bool foldout, GUIContent content, bool toggleOnLabelClick = true, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = toggleOnLabelClick;
    _ = style;
    return foldout;
  }

  public static bool Foldout(Rect position, bool foldout, string content, bool toggleOnLabelClick = true, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = toggleOnLabelClick;
    _ = style;
    return foldout;
  }

  public static bool Foldout(bool foldout, string label, bool toggleOnLabelClick = false)
  {
    _ = toggleOnLabelClick;
    _ = label;
    return foldout;
  }

  public static bool Foldout(bool foldout, GUIContent label, bool toggleOnLabelClick = false, GUIStyle? style = null)
  {
    _ = label;
    _ = toggleOnLabelClick;
    _ = style;
    return foldout;
  }

  public static bool FoldoutHeaderGroup(Rect position, bool expanded, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return expanded;
  }

  public static bool FoldoutHeaderGroup(bool expanded, string label)
  {
    _ = label;
    return expanded;
  }

  public static int IntSlider(Rect position, int value, int leftValue, int rightValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static int IntSlider(Rect position, string label, int value, int leftValue, int rightValue)
  {
    _ = position;
    _ = label;
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static int IntSlider(Rect position, GUIContent label, int value, int leftValue, int rightValue)
  {
    _ = position;
    _ = label;
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static int IntSlider(int value, int leftValue, int rightValue)
  {
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static int IntSlider(string label, int value, int leftValue, int rightValue)
  {
    _ = label;
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static float FloatSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle? style = null)
  {
    return Slider(position, value, leftValue, rightValue, style);
  }

  public static float FloatSlider(Rect position, string label, float value, float leftValue, float rightValue)
  {
    return Slider(position, label, value, leftValue, rightValue);
  }

  public static float FloatSlider(Rect position, GUIContent label, float value, float leftValue, float rightValue)
  {
    return Slider(position, label, value, leftValue, rightValue);
  }

  public static float FloatSlider(float value, float leftValue, float rightValue)
  {
    return Slider(value, leftValue, rightValue);
  }

  public static float FloatSlider(string label, float value, float leftValue, float rightValue)
  {
    return Slider(label, value, leftValue, rightValue);
  }

  public static Vector2Int Vector2IntField(Rect position, string label, Vector2Int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector2Int Vector2IntField(Rect position, GUIContent label, Vector2Int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector2Int Vector2IntField(Vector2Int value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Vector2Int Vector2IntField(Rect position, Vector2Int value)
  {
    _ = position;
    return value;
  }

  public static Vector2Int Vector2IntField(string label, Vector2Int value)
  {
    _ = label;
    return value;
  }

  public static Vector3Int Vector3IntField(Rect position, Vector3Int value)
  {
    _ = position;
    return value;
  }

  public static Vector3Int Vector3IntField(Rect position, string label, Vector3Int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector3Int Vector3IntField(Rect position, GUIContent label, Vector3Int value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static Vector3Int Vector3IntField(Vector3Int value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Vector3Int Vector3IntField(string label, Vector3Int value)
  {
    _ = label;
    return value;
  }

  public static RectInt RectIntField(Rect position, RectInt value)
  {
    _ = position;
    return value;
  }

  public static RectInt RectIntField(Rect position, string label, RectInt value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static RectInt RectIntField(Rect position, GUIContent label, RectInt value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static RectInt RectIntField(RectInt value, string label = "")
  {
    _ = label;
    return value;
  }

  public static RectInt RectIntField(string label, RectInt value)
  {
    _ = label;
    return value;
  }

  public static BoundsInt BoundsIntField(Rect position, BoundsInt value)
  {
    _ = position;
    return value;
  }

  public static BoundsInt BoundsIntField(Rect position, string label, BoundsInt value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static BoundsInt BoundsIntField(Rect position, GUIContent label, BoundsInt value)
  {
    _ = position;
    _ = label;
    return value;
  }

  public static BoundsInt BoundsIntField(BoundsInt value, string label = "")
  {
    _ = label;
    return value;
  }

  public static BoundsInt BoundsIntField(string label, BoundsInt value)
  {
    _ = label;
    return value;
  }

  public static Enum EnumFlagsField(Rect position, Enum enumValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return enumValue;
  }

  public static Enum EnumFlagsField(Rect position, string label, Enum enumValue, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return enumValue;
  }

  public static Enum EnumFlagsField(Rect position, GUIContent label, Enum enumValue, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
    return enumValue;
  }

  public static Enum EnumFlagsField(string label, Enum enumValue, GUIStyle? style = null)
  {
    _ = label;
    _ = style;
    return enumValue;
  }

  public static Enum EnumFlagsField(GUIContent label, Enum enumValue, GUIStyle? style = null)
  {
    _ = label;
    _ = style;
    return enumValue;
  }

  public static T EnumFlagsField<T>(Rect position, T enumValue) where T : struct, Enum
  {
    _ = position;
    return enumValue;
  }

  public static T EnumFlagsField<T>(string label, T enumValue) where T : struct, Enum
  {
    _ = label;
    return enumValue;
  }

  public static float Slider(Rect position, float value, float leftValue, float rightValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return Slider(value, leftValue, rightValue);
  }

  public static float Slider(Rect position, string label, float value, float leftValue, float rightValue)
  {
    _ = position;
    _ = label;
    return Slider(value, leftValue, rightValue);
  }

  public static float Slider(Rect position, GUIContent label, float value, float leftValue, float rightValue)
  {
    _ = position;
    _ = label;
    return Slider(value, leftValue, rightValue);
  }

  public static float Slider(float value, float leftValue, float rightValue, bool horizontal = true)
  {
    _ = horizontal;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static float Slider(string label, float value, float leftValue, float rightValue)
  {
    _ = label;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static float HorizontalSlider(float value, float leftValue, float rightValue, int id = 0)
  {
    _ = id;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static float VerticalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static bool PrefixButton(Rect position, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return false;
  }

  public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle? horizontalScrollbar = null, GUIStyle? verticalScrollbar = null)
  {
    _ = position;
    _ = viewRect;
    _ = alwaysShowHorizontal;
    _ = alwaysShowVertical;
    _ = horizontalScrollbar;
    _ = verticalScrollbar;
    _scrollViewStack.Push(new ScrollViewScope(scrollPosition));
    return scrollPosition;
  }

  public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition)
  {
    _ = position;
    _scrollViewStack.Push(new ScrollViewScope(scrollPosition));
    return scrollPosition;
  }

  public static bool BeginScrollView(Rect position, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle? horizontalScrollbar = null, GUIStyle? verticalScrollbar = null)
  {
    _ = position;
    _ = alwaysShowHorizontal;
    _ = alwaysShowVertical;
    _ = horizontalScrollbar;
    _ = verticalScrollbar;
    _scrollViewStack.Push(new ScrollViewScope(Vector2.zero));
    return true;
  }

  public static Vector2 EndScrollView()
  {
    if (_scrollViewStack.Count > 0)
    {
      var scope = _scrollViewStack.Pop();
      return scope.scrollPosition;
    }
    return default;
  }

  public static bool BeginToggleGroup(Rect position, GUIContent label, bool value)
  {
    _ = position;
    _ = label;
    _toggleGroupStack.Push(new ToggleGroupScope(value));
    return value;
  }

  public static bool BeginToggleGroup(bool value, string label, bool isBold = false)
  {
    _ = label;
    _ = isBold;
    _toggleGroupStack.Push(new ToggleGroupScope(value));
    return value;
  }

  public static bool BeginToggleGroup(GUIContent label, bool value)
  {
    _ = label;
    _toggleGroupStack.Push(new ToggleGroupScope(value));
    return value;
  }

  public static void EndToggleGroup()
  {
    if (_toggleGroupStack.Count > 0) _ = _toggleGroupStack.Pop();
  }

  public static void BeginDisabledGroup(bool disabled = false)
  {
    _disabledStack.Push(Enabled);
    Enabled = !disabled;
  }

  public static void EndDisabledGroup()
  {
    Enabled = _disabledStack.Count > 0 ? _disabledStack.Pop() : true;
  }

  public static void BeginProperty(Rect totalPosition, GUIContent label, SerializedProperty property)
  {
    _ = totalPosition;
    _ = label;
    _propertyStack.Push(new PropertyScope(property, showMixedValue));
    showMixedValue = property?.hasMultipleDifferentValues ?? false;
  }

  public static void BeginProperty(SerializedProperty property, GUIContent label)
  {
    _ = label;
    _propertyStack.Push(new PropertyScope(property, showMixedValue));
    showMixedValue = property?.hasMultipleDifferentValues ?? false;
  }

  public static void EndProperty()
  {
    showMixedValue = _propertyStack.Count > 0 ? _propertyStack.Pop().previousMixedValue : false;
  }

  public static void BeginHorizontal(GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style; _ = options; _horizontalStack.Push(new HorizontalScope());
  }

  public static void BeginHorizontal(params GUILayoutOption[]? options)
  {
    _ = options; _horizontalStack.Push(new HorizontalScope());
  }

  public static void BeginHorizontal()
  {
    _horizontalStack.Push(new HorizontalScope());
  }

  public static Rect BeginHorizontal(Rect position, GUIStyle? style = null)
  {
    _ = style; _horizontalStack.Push(new HorizontalScope()); return position;
  }

  public static void EndHorizontal()
  {
    if (_horizontalStack.Count > 0) _ = _horizontalStack.Pop();
  }

  public static void BeginVertical(GUIStyle? style, params GUILayoutOption[]? options)
  {
    _ = style; _ = options; _verticalStack.Push(new VerticalScope());
  }

  public static void BeginVertical(params GUILayoutOption[]? options)
  {
    _ = options; _verticalStack.Push(new VerticalScope());
  }

  public static void BeginVertical()
  {
    _verticalStack.Push(new VerticalScope());
  }

  public static Rect BeginVertical(Rect position, GUIStyle? style = null)
  {
    _ = style; _verticalStack.Push(new VerticalScope()); return position;
  }

  public static void EndVertical()
  {
    if (_verticalStack.Count > 0) _ = _verticalStack.Pop();
  }

  public static void BeginArea(Rect screenRect, string text = "", GUIStyle? style = null)
  {
    _ = text; _ = style; _areaStack.Push(new AreaScope(screenRect));
  }

  public static void EndArea()
  {
    if (_areaStack.Count > 0) _ = _areaStack.Pop();
  }

  public static bool BeginFadeGroup(float value)
  {
    _fadeGroupStack.Push(new FadeGroupScope(value));
    return value > 0f;
  }

  public static void EndFadeGroup()
  {
    if (_fadeGroupStack.Count > 0) _ = _fadeGroupStack.Pop();
  }

  public static bool BeginFoldoutHeaderGroup(bool foldout, GUIContent content, GUIStyle? style = null)
  {
    _ = content; _ = style; _foldoutHeaderStack.Push(new FoldoutHeaderScope(foldout)); return foldout;
  }

  public static bool BeginFoldoutHeaderGroup(bool foldout, string content, GUIStyle? style = null)
  {
    _ = content; _ = style; _foldoutHeaderStack.Push(new FoldoutHeaderScope(foldout)); return foldout;
  }

  public static void EndFoldoutHeaderGroup()
  {
    if (_foldoutHeaderStack.Count > 0) _ = _foldoutHeaderStack.Pop();
  }

  public static int BeginBuildTargetSelectionGrouping()
  {
    _buildTargetStack.Push(0);
    return 0;
  }

  public static void EndBuildTargetSelectionGrouping()
  {
    if (_buildTargetStack.Count > 0) _ = _buildTargetStack.Pop();
  }

  public static void DrawAAPolyLine(params Vector3[] points) { _ = points; }
  public static void DrawAAPolyLine(float width, params Vector3[] points) { _ = width; _ = points; }
  public static void DrawLine(Vector3 from, Vector3 to) { _ = from; _ = to; }
  public static void DrawLine(Vector3 from, Vector3 to, Color color, float thickness = 1f) { _ = from; _ = to; _ = color; _ = thickness; }
  public static void DrawRay(Vector3 from, Vector3 direction) { _ = from; _ = direction; }
  public static void DrawCube(Vector3 center, Vector3 size) { _ = center; _ = size; }
  public static void DrawWireCube(Vector3 center, Vector3 size) { _ = center; _ = size; }
  public static void DrawSphere(Vector3 center, float radius) { _ = center; _ = radius; }
  public static void DrawWireSphere(Vector3 center, float radius) { _ = center; _ = radius; }

  public static void Indent()
  {
    indentLevel++;
  }

  public static void Indent(int indent)
  {
    indentLevel += indent;
  }

  public static void Unindent()
  {
    indentLevel = Math.Max(0, indentLevel - 1);
  }

  public static void ProgressBar(Rect position, float value, string text) { _ = position; _ = value; _ = text; }

  public static bool InspectorTitlebar(Rect position, bool expanded, Object targetObj)
  {
    _ = position; _ = targetObj; return expanded;
  }

  public static bool InspectorTitlebar(Rect position, bool expanded, Object[] targetObjs)
  {
    _ = position; _ = targetObjs; return expanded;
  }

  public static bool DropdownButton(Rect position, GUIContent content, FocusType focusType)
  {
    _ = position; _ = content; _ = focusType; return false;
  }

  public static void HandleUtilityAddDefaultControl(int controlId) { _ = controlId; }
  public static Rect DragWindow(Rect position) { return position; }
  public static Rect DragWindow() { return new Rect(0, 0, 0, 0); }
  public static void SelectableLabel(Rect position, string text) { _ = position; _ = text; }
  public static void HelpBox(Rect position, string message, MessageType type) { _ = position; _ = message; _ = type; }
  public static void HelpBox(string message, MessageType type) { _ = message; _ = type; }

  public static int GetControlID(int hint, FocusType focusType) { _ = focusType; return hint; }
  public static int GetControlID(FocusType focusType, Rect rect) { _ = focusType; _ = rect; return _nextControlId++; }
  public static int GetControlID(GUIContent content, FocusType focusType) { _ = content; _ = focusType; return _nextControlId++; }
  public static int GetObjectPickerControlID() { return 0; }

  public static void ShowObjectPicker<T>(Action<T> selector, bool allowSceneObjects, string searchFilter = "", int controlID = 0) where T : class
  {
    _ = selector; _ = allowSceneObjects; _ = searchFilter; _ = controlID;
  }

  public static void UtilityFieldLayout(Rect position, string label) { _ = position; _ = label; }

  public static ColorUtility.ColorParts GetColorParts(Color color)
  {
    return new ColorUtility.ColorParts(color.r, color.g, color.b, color.a, false);
  }

  public static class MixedValueContent
  {
    public static readonly GUIContent content = new("—");
  }

  public static class Utility
  {
    public enum PrefixLabelMode { Normal, NoPrefix, AlignmentCorrection }
  }

  private readonly struct HorizontalScope { }
  private readonly struct VerticalScope { }
  private readonly struct ToggleGroupScope { public readonly bool value; public ToggleGroupScope(bool value) => this.value = value; }
  private readonly struct AreaScope { public readonly Rect rect; public AreaScope(Rect rect) => this.rect = rect; }
  private readonly struct PropertyScope { public readonly SerializedProperty? property; public readonly bool previousMixedValue; public PropertyScope(SerializedProperty? property, bool previousMixedValue) { this.property = property; this.previousMixedValue = previousMixedValue; } }
  private readonly struct FadeGroupScope { public readonly float value; public FadeGroupScope(float value) => this.value = value; }
  private readonly struct FoldoutHeaderScope { public readonly bool foldout; public FoldoutHeaderScope(bool foldout) => this.foldout = foldout; }
  private readonly struct ScrollViewScope { public readonly Vector2 scrollPosition; public ScrollViewScope(Vector2 scrollPosition) => this.scrollPosition = scrollPosition; }
}

public enum FocusType { Keyboard, Mouse, Native }
public enum ScaleMode { StretchToFill, ScaleAndCrop, ScaleToFit }

public static class ColorUtility
{
  public struct ColorParts
  {
    public float r; public float g; public float b; public float a; public bool hdr;
    public ColorParts(float r, float g, float b, float a, bool hdr) { this.r = r; this.g = g; this.b = b; this.a = a; this.hdr = hdr; }
  }
}
