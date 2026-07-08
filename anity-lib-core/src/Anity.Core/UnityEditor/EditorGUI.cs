using System;
using UnityEngine;

namespace UnityEditor;

public static class EditorGUI
{
  private static bool _isEditing;
  private static bool _hasChanged;
  public static bool Enabled { get; set; } = true;

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

  public static bool ToggleLeft(string label, bool value, bool leftValue = false)
  {
    _ = leftValue;
    return Toggle(value, label);
  }

  public static void Space(int pixels)
  {
    _ = pixels;
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

  public static int IntField(Rect position, int value)
  {
    _ = position;
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

  public static float FloatField(Rect position, float value)
  {
    _ = position;
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

  public static string TextArea(string value, int maxLines = 3)
  {
    _ = maxLines;
    return value;
  }

  public static string TextArea(Rect position, string value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static string TextField(string value, string label = "")
  {
    _ = label;
    return value;
  }

  public static string TextField(Rect position, string value, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return value;
  }

  public static string TextField(string value, bool readOnly)
  {
    _ = readOnly;
    return value;
  }

  public static string DelayedTextField(string value, string label = "")
  {
    _ = label;
    return value;
  }

  public static string PasswordField(string value, char passwordChar, int maxLength = 0, string label = "")
  {
    _ = passwordChar;
    _ = maxLength;
    _ = label;
    return value;
  }

  public static int Popup(int selectedValue, string[] displayedOptions, int[]? optionValues = null, GUIStyle? style = null, string label = "")
  {
    _ = optionValues;
    _ = style;
    _ = label;
    if (displayedOptions is null || displayedOptions.Length == 0)
    {
      return -1;
    }

    return Math.Clamp(selectedValue, 0, displayedOptions.Length - 1);
  }

  public static int Popup(Rect position, int selectedValue, string[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    return Popup(selectedValue, displayedOptions, null, style);
  }

  public static int Popup(int selectedValue, GUIContent[] displayedOptions, GUIStyle? style = null, string label = "")
  {
    _ = style;
    _ = label;
    if (displayedOptions is null || displayedOptions.Length == 0)
    {
      return -1;
    }

    return Math.Clamp(selectedValue, 0, displayedOptions.Length - 1);
  }

  public static int Popup(Rect position, int selectedValue, GUIContent[] displayedOptions, GUIStyle? style = null)
  {
    _ = position;
    return Popup(selectedValue, displayedOptions, style);
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

  public static Vector2 Vector2Field(Vector2 value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Vector2 Vector2Field(string label, Vector2 value)
  {
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

  public static Rect RectField(Rect value, string label = "")
  {
    _ = label;
    return value;
  }

  public static Rect RectField(string label, Rect value)
  {
    _ = label;
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

  public static T ObjectField<T>(string label, T? obj, bool allowSceneObjects) where T : class
  {
    _ = label;
    _ = allowSceneObjects;
    return obj!;
  }

  public static Object ObjectField(Object obj, Type objType, bool allowSceneObjects)
  {
    _ = objType;
    _ = allowSceneObjects;
    return obj;
  }

  public static T EnumPopup<T>(string label, T selected) where T : struct, Enum
  {
    _ = label;
    return selected;
  }

  public static int TagField(string label, int selectedTagIndex, string[] displayedOptions)
  {
    _ = label;
    _ = displayedOptions;
    return selectedTagIndex;
  }

  public static int LayerField(string label, int layer)
  {
    _ = label;
    return layer;
  }

  public static void PropertyField(SerializedProperty property, string? label = null, bool includeChildren = true)
  {
    _ = property;
    _ = label;
    _ = includeChildren;
  }

  public static void PropertyField(Rect position, SerializedProperty property, GUIContent label, bool includeChildren = true)
  {
    _ = position;
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

  public static bool Button(string text, GUIStyle? style = null)
  {
    _ = text;
    _ = style;
    return false;
  }

  public static bool Button(Rect position, string text, GUIStyle? style = null)
  {
    _ = position;
    return Button(text, style);
  }

  public static bool Button(Rect position, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return false;
  }

  public static void DrawRect(Rect rect, string text)
  {
    _ = rect;
    _ = text;
  }

  public static void DrawLabel(Rect position, string label, GUIStyle? style = null)
  {
    _ = position;
    _ = label;
    _ = style;
  }

  public static void MinMaxSlider(string label, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
  {
    _ = label;
    _ = minLimit;
    _ = maxLimit;
    if (minValue > maxValue)
    {
      var t = minValue;
      minValue = maxValue;
      maxValue = t;
    }

    minValue = Math.Clamp(minValue, minLimit, maxLimit);
    maxValue = Math.Clamp(maxValue, minLimit, maxLimit);
  }

  public static Rect PrefixLabel(Rect position, int controlID, GUIContent label, GUIStyle? labelStyle = null)
  {
    _ = controlID;
    _ = labelStyle;
    _ = label;
    return position;
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

  public static bool FoldoutHeaderGroup(bool expanded, string label)
  {
    _ = label;
    return expanded;
  }

  public static int IntSlider(int value, int leftValue, int rightValue)
  {
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static float Slider(float value, float leftValue, float rightValue, bool horizontal = true)
  {
    _ = horizontal;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static float Slider(Rect position, float value, float leftValue, float rightValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return Slider(value, leftValue, rightValue);
  }

  public static int IntSlider(Rect position, int value, int leftValue, int rightValue, GUIStyle? style = null)
  {
    _ = position;
    _ = style;
    return Math.Clamp(value, leftValue, rightValue);
  }

  public static float HorizontalSlider(float value, float leftValue, float rightValue, int id = 0)
  {
    _ = id;
    return Math.Clamp(value, Math.Min(leftValue, rightValue), Math.Max(leftValue, rightValue));
  }

  public static bool PrefixButton(Rect position, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return false;
  }

  public static bool BeginScrollView(Rect position, Vector2 scrollPosition)
  {
    _ = position;
    _ = scrollPosition;
    return true;
  }

  public static Vector2 EndScrollView()
  {
    return default;
  }

  public static bool BeginToggleGroup(bool value, string label, bool isBold = false)
  {
    _ = label;
    _ = isBold;
    return value;
  }

  public static void EndToggleGroup() {}

  public static bool ToggleLeft(Rect position, bool value, GUIContent content, GUIStyle? style = null)
  {
    _ = position;
    _ = content;
    _ = style;
    return value;
  }

  public static bool BeginScrollView(Rect position, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle? horizontalScrollbar = null, GUIStyle? verticalScrollbar = null)
  {
    _ = position;
    _ = alwaysShowHorizontal;
    _ = alwaysShowVertical;
    _ = horizontalScrollbar;
    _ = verticalScrollbar;
    return true;
  }

  public static int GetControlID(int hint, FocusType focusType)
  {
    _ = hint;
    _ = focusType;
    return hint;
  }

  public static int GetControlID(FocusType focusType, Rect rect)
  {
    _ = focusType;
    _ = rect;
    return 0;
  }

  public static int GetObjectPickerControlID()
  {
    return 0;
  }

  public static void BeginDisabledGroup(bool disabled = false)
  {
    _ = disabled;
  }

  public static void EndDisabledGroup() {}

  public static void BeginProperty(SerializedProperty property, GUIContent label)
  {
    _ = property;
    _ = label;
  }

  public static void EndProperty() {}

  public static void BeginHorizontal() {}
  public static void EndHorizontal() {}
  public static void BeginVertical() {}
  public static void EndVertical() {}

  public static void BeginArea(Rect position, string title = "")
  {
    _ = position;
    _ = title;
  }

  public static void EndArea() {}

  public static void DrawAAPolyLine(params Vector3[] points)
  {
    _ = points;
  }

  public static void DrawLine(Vector3 from, Vector3 to)
  {
    _ = from;
    _ = to;
  }

  public static void DrawRay(Vector3 from, Vector3 direction)
  {
    _ = from;
    _ = direction;
  }

  public static void DrawCube(Vector3 center, Vector3 size)
  {
    _ = center;
    _ = size;
  }
}

public enum FocusType
{
  Keyboard,
  Mouse,
  Native
}
