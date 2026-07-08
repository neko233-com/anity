using System;

namespace UnityEditor;

public sealed class SerializedObject : IDisposable
{
  public object? targetObject { get; }
  public bool hasModifiedProperties { get; set; }

  public SerializedObject(object? target)
  {
    targetObject = target;
  }

  public SerializedProperty? FindProperty(string propertyPath)
  {
    return propertyPath is null ? null : new SerializedProperty(targetObject, propertyPath);
  }

  public SerializedProperty FindProperty(string propertyPath, bool includeChildren)
  {
    _ = includeChildren;
    return new SerializedProperty(targetObject, propertyPath);
  }

  public bool Update() => true;
  public bool ApplyModifiedProperties() => hasModifiedProperties;
  public void ApplyModifiedPropertiesWithoutUndo() {}
  public void UpdateIfNeeded() {}
  public void UpdateIfRequiredOrScript() {}
  public void Dispose() {}

  public static explicit operator bool(SerializedObject obj) => obj is not null && obj.targetObject is not null;
  public override string ToString() => $"SerializedObject({targetObject?.GetType().Name ?? "null"})";
}

