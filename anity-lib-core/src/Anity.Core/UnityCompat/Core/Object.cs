using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Object : IDisposable
{
  private bool _destroyed;
  private string? _name;
  private int _instanceId;
  private static readonly Dictionary<int, Object> _instances = new();
  private static int _nextInstanceId;

  public int GetInstanceID()
  {
    if (_instanceId == 0)
    {
      _instanceId = ++_nextInstanceId;
      _instances[_instanceId] = this;
    }
    return _instanceId;
  }

  public static void Destroy(Object? obj)
  {
    MarkDestroyed(obj);
  }

  public static void Destroy(Object? obj, float t)
  {
    _ = t;
    MarkDestroyed(obj);
  }

  public static void DestroyImmediate(Object? obj)
  {
    MarkDestroyed(obj);
  }

  public static void DestroyImmediate(Object? obj, bool allowDestroyingAssets)
  {
    _ = allowDestroyingAssets;
    MarkDestroyed(obj);
  }

  public static void DestroyObject(Object? obj)
  {
    MarkDestroyed(obj);
  }

  public static void DestroyObject(Object? obj, float t)
  {
    _ = t;
    MarkDestroyed(obj);
  }

  private static void MarkDestroyed(Object? obj)
  {
    if (obj is null)
    {
      return;
    }

    obj._destroyed = true;
    _instances.Remove(obj._instanceId);
    if (obj is GameObject gameObject)
    {
      GameObject.UnregisterFromScene(gameObject);
    }
  }

  public static Object Instantiate(Object original)
  {
    return original;
  }

  public static Object Instantiate(Object original, Vector3 position, Quaternion rotation)
  {
    return original;
  }

  public static Object Instantiate(Object original, Transform? parent)
  {
    return original;
  }

  public static Object Instantiate(Object original, Transform? parent, bool worldPositionStays)
  {
    return original;
  }

  public static Object Instantiate(Object original, Transform? parent, InstantiateParameters instantiateParameters)
  {
    return original;
  }

  public static T Instantiate<T>(T original) where T : Object
  {
    return original;
  }

  public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
  {
    return original;
  }

  public static T Instantiate<T>(T original, Transform? parent) where T : Object
  {
    return original;
  }

  public static T Instantiate<T>(T original, Transform? parent, bool worldPositionStays) where T : Object
  {
    return original;
  }

  public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform? parent) where T : Object
  {
    return original;
  }

  public static T Instantiate<T>(T original, Transform? parent, InstantiateParameters instantiateParameters) where T : Object
  {
    return original;
  }

  public static Object? FindObjectOfType(Type type)
  {
    _ = type;
    return null;
  }

  public static T? FindObjectOfType<T>() where T : Object
  {
    return default;
  }

  public static Object[] FindObjectsOfType(Type type)
  {
    _ = type;
    return Array.Empty<Object>();
  }

  public static T[] FindObjectsOfType<T>() where T : Object
  {
    return Array.Empty<T>();
  }

  public static Object[] FindObjectsByType(Type type, FindObjectsSortMode sortMode)
  {
    _ = type;
    _ = sortMode;
    return Array.Empty<Object>();
  }

  public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object
  {
    _ = sortMode;
    return Array.Empty<T>();
  }

  public static T? FindObjectOfType<T>(bool includeInactive) where T : Object
  {
    _ = includeInactive;
    return default;
  }

  public static T[] FindObjectsOfType<T>(bool includeInactive) where T : Object
  {
    _ = includeInactive;
    return Array.Empty<T>();
  }

  public static void DontDestroyOnLoad(Object target)
  {
    _ = target;
  }

  public static void Print(object? message)
  {
    Debug.Log(message);
  }

  public static void print(object? message)
  {
    Debug.Log(message);
  }

  public static void Copy<T>(T source, T destination) where T : Object
  {
    _ = source;
    _ = destination;
  }

  public static bool operator ==(Object? a, Object? b)
  {
    if (a is null && b is null) return true;
    if (a is null || b is null) return false;
    return ReferenceEquals(a, b) || a.GetInstanceID() == b.GetInstanceID();
  }

  public static bool operator !=(Object? a, Object? b)
  {
    return !(a == b);
  }

  public override bool Equals(object? obj)
  {
    if (obj is Object other)
    {
      return GetInstanceID() == other.GetInstanceID();
    }
    return false;
  }

  public override int GetHashCode()
  {
    return GetInstanceID();
  }

  public bool IsDestroyed => _destroyed;

  public string name
  {
    get => _name ?? string.Empty;
    set => _name = value;
  }

  public override string ToString()
  {
    return name ?? GetType().Name;
  }

  public void Dispose()
  {
    Destroy(this);
    GC.SuppressFinalize(this);
  }
}

public struct InstantiateParameters
{
  public int layer;
  public Transform? parent;
  public bool instantiateInWorldSpace;
  public bool worldPositionStays;
}

public enum FindObjectsSortMode
{
  None,
  InstanceID,
  NoneLegacy
}

public enum FindObjectsInactive
{
  Exclude,
  Include
}
