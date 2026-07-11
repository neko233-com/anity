using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEditor;

public abstract class Editor
{
  public object? target { get; private set; }
  public object[]? targets { get; private set; }
  public SerializedObject? serializedObject { get; private set; }

  public bool useGUILayout { get; set; } = true;
  public bool isInspectorExpanded { get; set; } = true;

  public virtual bool HasPreviewGUI() => false;
  public virtual GUIContent GetPreviewTitle() => GUIContent.Temp("Preview");
  public virtual bool ShouldHideOpenButton() => false;
  public virtual bool RequiresConstantRepaint() => false;

  public virtual void OnInspectorGUI()
  {
    DrawDefaultInspector();
  }

  protected virtual void OnHeaderGUI() {}
  protected virtual void OnEnable() {}
  protected virtual void OnDisable() {}

  public virtual bool UseDefaultMargins() => true;

  public virtual bool DrawDefaultInspector()
  {
    if (serializedObject is null || target is null)
      return false;

    serializedObject.Update();

    var fields = GetSerializedFields(target.GetType());
    foreach (var field in fields)
    {
      var prop = serializedObject.FindProperty(field.Name);
      if (prop is null) continue;
      DrawPropertyField(field, prop);
    }

    return serializedObject.ApplyModifiedProperties();
  }

  private static void DrawPropertyField(FieldInfo field, SerializedProperty prop)
  {
    var label = ObjectNames.NicifyVariableName(field.Name);

    switch (prop.propertyType)
    {
      case SerializedPropertyType.Integer:
        prop.intValue = EditorGUILayout.IntField(label, prop.intValue);
        break;
      case SerializedPropertyType.Float:
        prop.floatValue = EditorGUILayout.FloatField(label, prop.floatValue);
        break;
      case SerializedPropertyType.Boolean:
        prop.boolValue = EditorGUILayout.Toggle(label, prop.boolValue);
        break;
      case SerializedPropertyType.String:
        prop.stringValue = EditorGUILayout.TextField(label, prop.stringValue ?? string.Empty);
        break;
      case SerializedPropertyType.Color:
        prop.colorValue = EditorGUILayout.ColorField(label, prop.colorValue);
        break;
      case SerializedPropertyType.Vector2:
        prop.vector2Value = EditorGUILayout.Vector2Field(label, prop.vector2Value);
        break;
      case SerializedPropertyType.Vector3:
        prop.vector3Value = EditorGUILayout.Vector3Field(label, prop.vector3Value);
        break;
      case SerializedPropertyType.ObjectReference:
        prop.objectReferenceValue = EditorGUILayout.ObjectField(label, prop.objectReferenceValue, field.FieldType, true);
        break;
      case SerializedPropertyType.Enum:
        if (field.FieldType.IsEnum)
        {
          var enumValue = EditorGUILayout.EnumPopup(label, (Enum)Enum.ToObject(field.FieldType, prop.enumValueIndex));
          prop.enumValueIndex = Convert.ToInt32(enumValue);
        }
        break;
      case SerializedPropertyType.Rect:
        var rect = prop.rectValue;
        rect = EditorGUILayout.RectField(label, rect);
        prop.rectValue = rect;
        break;
    }
  }

  private static List<FieldInfo> GetSerializedFields(Type type)
  {
    var result = new List<FieldInfo>();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    var current = type;
    while (current != null && current != typeof(object) && current != typeof(MonoBehaviour) && current != typeof(ScriptableObject))
    {
      foreach (var field in current.GetFields(flags))
      {
        if (field.IsNotSerialized) continue;
        if (field.IsPrivate && field.GetCustomAttribute<SerializeField>() is null) continue;
        if (field.IsInitOnly) continue;
        result.Add(field);
      }
      current = current.BaseType;
    }

    result.Reverse();
    return result;
  }

  public virtual bool CanEditMultipleObjects() => false;

  public static Editor CreateEditor(UnityEngine.Object targetObject)
  {
    return CreateEditor(new[] { targetObject });
  }

  public static Editor CreateEditor(UnityEngine.Object targetObject, System.Type editorType)
  {
    _ = editorType;
    return CreateEditor(new[] { targetObject });
  }

  public static Editor CreateEditor(UnityEngine.Object[] targetObjects)
  {
    var editor = new GenericEditor();
    editor.SetTargetObjects(targetObjects);
    return editor;
  }

  public static Editor CreateEditor(UnityEngine.Object[] targetObjects, System.Type editorType)
  {
    _ = editorType;
    return CreateEditor(targetObjects);
  }

  public static bool HasPreviewGUI(Editor editor)
  {
    return editor?.HasPreviewGUI() ?? false;
  }

  public void DrawHeader()
  {
    OnHeaderGUI();
  }

  public void Repaint()
  {
  }

  internal void SetTargetObject(object? target)
  {
    this.target = target;
    this.targets = target != null ? new[] { target } : null;
    serializedObject = new SerializedObject(target);
    OnEnable();
  }

  internal void SetTargetObjects(object[] targetObjects)
  {
    this.targets = targetObjects;
    this.target = targetObjects != null && targetObjects.Length > 0 ? targetObjects[0] : null;
    serializedObject = new SerializedObject(target);
    OnEnable();
  }
}

internal sealed class GenericEditor : Editor
{
}

public static class ObjectNames
{
  public static string NicifyVariableName(string name)
  {
    if (string.IsNullOrEmpty(name)) return name;
    if (name.StartsWith("m_")) name = name[2..];
    var result = new List<char>();
    for (var i = 0; i < name.Length; i++)
    {
      if (i == 0)
      {
        result.Add(char.ToUpperInvariant(name[i]));
      }
      else if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
      {
        result.Add(' ');
        result.Add(name[i]);
      }
      else
      {
        result.Add(name[i]);
      }
    }
    return new string(result.ToArray());
  }
}
