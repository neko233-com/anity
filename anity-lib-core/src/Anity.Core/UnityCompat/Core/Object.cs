using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public class Object : IDisposable
{
  private bool _destroyed;
  private string? _name;
  private int _instanceId;
  private HideFlags _hideFlags;
  private bool _dontDestroyOnLoad;
  private float _destroyDelay = -1f;
  private static readonly HashSet<Object> _allObjects = new();
  private static readonly List<ObjectDestroyInfo> _destroyQueue = new();
  private static int _nextInstanceId;

  public Object()
  {
    _instanceId = ++_nextInstanceId;
    _allObjects.Add(this);
  }

  public HideFlags hideFlags
  {
    get => _hideFlags;
    set => _hideFlags = value;
  }

  public virtual string name
  {
    get => _name ?? string.Empty;
    set => _name = value;
  }

  public int GetInstanceID()
  {
    return _instanceId;
  }

  public static void Destroy(Object? obj)
  {
    Destroy(obj, 0f);
  }

  public static void Destroy(Object? obj, float t)
  {
    if (obj is null || obj._destroyed)
    {
      return;
    }

    if (t <= 0f)
    {
      DestroyImmediate(obj);
      return;
    }

    obj._destroyDelay = t;
    _destroyQueue.Add(new ObjectDestroyInfo(obj, Time.time + t));
  }

  public static void DestroyImmediate(Object? obj)
  {
    DestroyImmediate(obj, false);
  }

  public static void DestroyImmediate(Object? obj, bool allowDestroyingAssets)
  {
    _ = allowDestroyingAssets;
    if (obj is null || obj._destroyed)
    {
      return;
    }

    obj._destroyed = true;
    _allObjects.Remove(obj);
    _destroyQueue.RemoveAll(x => x.Target == obj);

    if (obj is GameObject gameObject)
    {
      for (int i = gameObject.transform.childCount - 1; i >= 0; i--)
      {
        var child = gameObject.transform.GetChild(i);
        if (child.gameObject is not null)
        {
          DestroyImmediate(child.gameObject);
        }
      }

      var components = gameObject.GetComponents<Component>();
      foreach (var comp in components)
      {
        if (comp is MonoBehaviour mb)
        {
          try { mb.InternalOnDestroy(); } catch { }
        }
      }

      GameObject.UnregisterFromScene(gameObject);
    }
    else if (obj is Component component)
    {
      if (component.gameObject is not null)
      {
        component.gameObject.RemoveComponentInternal(component);
      }
      if (component is Behaviour behaviour && behaviour.enabled)
      {
        try { if (component is MonoBehaviour mb) mb.InternalOnDisable(); } catch { }
      }
    }
  }

  public static void DestroyObject(Object? obj)
  {
    Destroy(obj);
  }

  public static void DestroyObject(Object? obj, float t)
  {
    Destroy(obj, t);
  }

  internal static void TickDestroyQueue()
  {
    float currentTime = Time.time;
    for (int i = _destroyQueue.Count - 1; i >= 0; i--)
    {
      var info = _destroyQueue[i];
      if (currentTime >= info.DestroyTime)
      {
        _destroyQueue.RemoveAt(i);
        if (info.Target is not null && !info.Target._destroyed)
        {
          DestroyImmediate(info.Target);
        }
      }
    }
  }

  public static Object Instantiate(Object original)
  {
    return Instantiate(original, Vector3.zero, Quaternion.identity, null);
  }

  public static Object Instantiate(Object original, Vector3 position, Quaternion rotation)
  {
    return Instantiate(original, position, rotation, null);
  }

  public static Object Instantiate(Object original, Transform? parent)
  {
    return Instantiate(original, Vector3.zero, Quaternion.identity, parent);
  }

  public static Object Instantiate(Object original, Transform? parent, bool worldPositionStays)
  {
    return Instantiate(original, Vector3.zero, Quaternion.identity, parent, worldPositionStays);
  }

  public static Object Instantiate(Object original, Transform? parent, InstantiateParameters instantiateParameters)
  {
    _ = instantiateParameters;
    return Instantiate(original, Vector3.zero, Quaternion.identity, parent);
  }

  public static T Instantiate<T>(T original) where T : Object
  {
    return (T)Instantiate((Object)original);
  }

  public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
  {
    return (T)Instantiate((Object)original, position, rotation);
  }

  public static T Instantiate<T>(T original, Transform? parent) where T : Object
  {
    return (T)Instantiate((Object)original, parent);
  }

  public static T Instantiate<T>(T original, Transform? parent, bool worldPositionStays) where T : Object
  {
    return (T)Instantiate((Object)original, parent, worldPositionStays);
  }

  public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform? parent) where T : Object
  {
    return (T)Instantiate((Object)original, position, rotation, parent);
  }

  public static T Instantiate<T>(T original, Transform? parent, InstantiateParameters instantiateParameters) where T : Object
  {
    return (T)Instantiate((Object)original, parent, instantiateParameters);
  }

  private static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform? parent, bool worldPositionStays = true)
  {
    if (original is GameObject originalGo)
    {
      return CloneGameObject(originalGo, position, rotation, parent, worldPositionStays);
    }
    else if (original is Component originalComp)
    {
      var go = CloneGameObject(originalComp.gameObject!, position, rotation, parent, worldPositionStays);
      return go.GetComponent(originalComp.GetType())!;
    }
    else
    {
      var type = original.GetType();
      if (type.IsAbstract || type.IsInterface)
      {
        return original;
      }
      var clone = (Object)Activator.CreateInstance(type)!;
      clone._name = original._name + "(Clone)";
      CopyFields(original, clone);
      return clone;
    }
  }

  private static GameObject CloneGameObject(GameObject original, Vector3 position, Quaternion rotation, Transform? parent, bool worldPositionStays)
  {
    var clone = new GameObject(original.name + "(Clone)");
    clone.tag = original.tag;
    clone.layer = original.layer;
    clone.isStatic = original.isStatic;
    clone.SetActive(original.activeSelf);

    var originalComponents = original.GetComponents<Component>();
    foreach (var comp in originalComponents)
    {
      if (comp is Transform) continue;
      var type = comp.GetType();
      if (type.IsAbstract) continue;
      var newComp = clone.AddComponent(type);
      CopyFields(comp, newComp);
      if (newComp is Behaviour newBehaviour)
      {
        var origBehaviour = comp as Behaviour;
        newBehaviour.enabled = origBehaviour?.enabled ?? true;
      }
    }

    if (parent is not null)
    {
      clone.transform.SetParent(parent, worldPositionStays);
      if (!worldPositionStays)
      {
        clone.transform.localPosition = original.transform.localPosition;
        clone.transform.localRotation = original.transform.localRotation;
        clone.transform.localScale = original.transform.localScale;
      }
      else
      {
        clone.transform.position = position;
        clone.transform.rotation = rotation;
      }
    }
    else
    {
      clone.transform.position = position;
      clone.transform.rotation = rotation;
      clone.transform.localScale = original.transform.localScale;
    }

    for (int i = 0; i < original.transform.childCount; i++)
    {
      var child = original.transform.GetChild(i);
      if (child.gameObject is not null)
      {
        var childClone = CloneGameObject(child.gameObject, child.position, child.rotation, clone.transform, false);
        childClone.transform.localPosition = child.localPosition;
        childClone.transform.localRotation = child.localRotation;
        childClone.transform.localScale = child.localScale;
      }
    }

    foreach (var comp in clone.GetComponents<MonoBehaviour>())
    {
      try { comp.InternalAwake(); } catch { }
    }

    return clone;
  }

  private static void CopyFields(object source, object target)
  {
    var type = source.GetType();
    while (type is not null && type != typeof(Object) && type != typeof(object))
    {
      var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
      foreach (var field in fields)
      {
        if (field.IsStatic || field.IsInitOnly) continue;
        try
        {
          var value = field.GetValue(source);
          field.SetValue(target, value);
        }
        catch { }
      }
      type = type.BaseType;
    }
  }

  public static Object? FindObjectOfType(Type type)
  {
    return _allObjects.FirstOrDefault(o => !o._destroyed && type.IsAssignableFrom(o.GetType()));
  }

  public static T? FindObjectOfType<T>() where T : Object
  {
    return (T?)FindObjectOfType(typeof(T));
  }

  public static Object[] FindObjectsOfType(Type type)
  {
    return _allObjects.Where(o => !o._destroyed && type.IsAssignableFrom(o.GetType())).ToArray();
  }

  public static T[] FindObjectsOfType<T>() where T : Object
  {
    return _allObjects.OfType<T>().Where(o => !o._destroyed).ToArray();
  }

  public static Object[] FindObjectsByType(Type type, FindObjectsSortMode sortMode)
  {
    var objects = FindObjectsOfType(type);
    if (sortMode == FindObjectsSortMode.InstanceID)
    {
      Array.Sort(objects, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }
    return objects;
  }

  public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object
  {
    var objects = FindObjectsOfType<T>();
    if (sortMode == FindObjectsSortMode.InstanceID)
    {
      Array.Sort(objects, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }
    return objects;
  }

  public static T? FindObjectOfType<T>(bool includeInactive) where T : Object
  {
    _ = includeInactive;
    return FindObjectOfType<T>();
  }

  public static T[] FindObjectsOfType<T>(bool includeInactive) where T : Object
  {
    _ = includeInactive;
    return FindObjectsOfType<T>();
  }

  public static void DontDestroyOnLoad(Object target)
  {
    if (target is not null)
    {
      target._dontDestroyOnLoad = true;
    }
  }

  public bool IsDontDestroyOnLoad => _dontDestroyOnLoad;

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
    if (source is null || destination is null) return;
    CopyFields(source, destination);
  }

  public static bool operator ==(Object? a, Object? b)
  {
    bool aNull = a is null || a._destroyed;
    bool bNull = b is null || b._destroyed;
    if (aNull && bNull) return true;
    if (aNull || bNull) return false;
    return ReferenceEquals(a, b) || a._instanceId == b._instanceId;
  }

  public static bool operator !=(Object? a, Object? b)
  {
    return !(a == b);
  }

  public override bool Equals(object? obj)
  {
    if (obj is Object other)
    {
      if (_destroyed && other._destroyed) return true;
      if (_destroyed || other._destroyed) return false;
      return _instanceId == other._instanceId;
    }
    if (_destroyed && obj is null) return true;
    return false;
  }

  public override int GetHashCode()
  {
    return _instanceId;
  }

  public bool IsDestroyed => _destroyed;

  public override string ToString()
  {
    return name ?? GetType().Name;
  }

  public void Dispose()
  {
    Destroy(this);
    GC.SuppressFinalize(this);
  }

  internal static IReadOnlyCollection<Object> AllObjects => _allObjects;

  internal void RemoveComponentInternal(Component component)
  {
    if (this is GameObject go)
    {
      go.RemoveComponentInternal(component);
    }
  }
}

public static class GameObjectExtensions
{
  public static void RemoveComponentInternal(this GameObject go, Component component)
  {
    _ = go;
    _ = component;
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

[Flags]
public enum HideFlags
{
  None = 0,
  HideInHierarchy = 1,
  HideInInspector = 2,
  DontSaveInEditor = 4,
  NotEditable = 8,
  DontSaveInBuild = 16,
  DontUnloadUnusedAsset = 32,
  DontSave = DontSaveInEditor | DontSaveInBuild,
  HideAndDontSave = HideInHierarchy | DontSaveInEditor | NotEditable | DontSaveInBuild | DontUnloadUnusedAsset
}

internal struct ObjectDestroyInfo
{
  public Object Target;
  public float DestroyTime;

  public ObjectDestroyInfo(Object target, float destroyTime)
  {
    Target = target;
    DestroyTime = destroyTime;
  }
}
