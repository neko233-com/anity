using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor;

public enum InspectorMode
{
  Normal,
  Debug,
  DebugInternal,
  DebugNormal,
  Verbose
}

public sealed class SerializedObject : IDisposable
{
  private readonly List<object> _targetObjects = new();
  private bool _isDifferentCacheDirty;

  public object? targetObject { get; }
  public object[] targetObjects => _targetObjects.ToArray();
  public bool hasModifiedProperties { get; set; }
  public InspectorMode InspectorMode { get; set; } = InspectorMode.Normal;
  public bool forceChildVisibility { get; set; }
  public object? context { get; set; }
  public SerializedObject? NestedObject { get; set; }

  public SerializedObject(object? target)
  {
    targetObject = target;
    if (target is not null)
    {
      _targetObjects.Add(target);
    }
  }

  public SerializedObject(object[] targets)
  {
    if (targets is { Length: > 0 })
    {
      targetObject = targets[0];
      _targetObjects.AddRange(targets.Where(t => t is not null));
    }
  }

  public SerializedProperty? FindProperty(string propertyPath)
  {
    return propertyPath is null ? null : new SerializedProperty(this, targetObject, propertyPath);
  }

  public SerializedProperty FindProperty(string propertyPath, bool includeChildren)
  {
    _ = includeChildren;
    return new SerializedProperty(this, targetObject, propertyPath);
  }

  public SerializedProperty GetIterator()
  {
    return new SerializedProperty(this, targetObject, string.Empty);
  }

  public bool Update()
  {
    _isDifferentCacheDirty = false;
    return true;
  }

  public bool ApplyModifiedProperties()
  {
    var modified = hasModifiedProperties;
    hasModifiedProperties = false;
    return modified;
  }

  public void ApplyModifiedPropertiesWithoutUndo()
  {
    hasModifiedProperties = false;
  }

  public void SetIsDifferentCacheDirty()
  {
    _isDifferentCacheDirty = true;
  }

  public void UpdateIfRequiredOrScript()
  {
    Update();
  }

  public void UpdateIfNeeded()
  {
    Update();
  }

  public void CopyFromSerializedProperty(SerializedProperty property)
  {
    _ = property;
  }

  public void Dispose()
  {
    _targetObjects.Clear();
  }

  public static explicit operator bool(SerializedObject obj) => obj is not null && obj.targetObject is not null;
  public override string ToString() => $"SerializedObject({targetObject?.GetType().Name ?? "null"})";
}
