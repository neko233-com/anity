using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

public static class Selection
{
  private static readonly List<Object> _objects = new();
  public static event Action? selectionChanged;

  public static Object[] objects => _objects.ToArray();
  public static int objectCount => _objects.Count;
  public static Object? activeObject { get; set; }
  public static Object? activeContext { get; set; }
  public static Object? activeGameObject => activeObject;
  public static Transform? activeTransform => activeObject as Transform;

  public static void SetActiveObjectWithContext(Object? obj, Object? context = null)
  {
    activeObject = obj;
    activeContext = context;
    if (obj is not null && !_objects.Contains(obj))
    {
      _objects.Add(obj);
    }
    else if (obj is null)
    {
      _objects.Clear();
    }
    selectionChanged?.Invoke();
  }

  public static void SetActiveObject(Object? obj, bool reveal = false)
  {
    _ = reveal;
    activeObject = obj;
    if (obj is null)
    {
      _objects.Clear();
      selectionChanged?.Invoke();
      return;
    }

    if (!_objects.Contains(obj))
    {
      _objects.Add(obj);
    }

    selectionChanged?.Invoke();
  }

  public static T[] GetFiltered<T>(SelectionMode mode = SelectionMode.Unfiltered) where T : Object
  {
    _ = mode;
    return _objects.OfType<T>().ToArray();
  }

  public static Object[] GetFiltered(Type type, SelectionMode mode = SelectionMode.Unfiltered)
  {
    _ = mode;
    return _objects.Where(obj => obj is not null && (obj.GetType() == type || type.IsAssignableFrom(obj.GetType()))).ToArray();
  }

  public static void SelectAll()
  {
    if (activeObject is not null && !_objects.Contains(activeObject))
    {
      _objects.Add(activeObject);
    }
    selectionChanged?.Invoke();
  }

  public static void Clear()
  {
    _objects.Clear();
    activeObject = null;
    activeContext = null;
    selectionChanged?.Invoke();
  }
}
