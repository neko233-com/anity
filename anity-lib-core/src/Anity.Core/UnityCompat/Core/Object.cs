using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Export/Scripting/UnityEngineObject.bindings.h")]
[Bindings.NativeHeader("Runtime/GameCode/CloneObject.h")]
[Bindings.NativeHeader("Runtime/SceneManager/SceneManager.h")]
[Scripting.RequiredByNativeCode(GenerateProxy = true)]
public class Object
{
  private bool _destroyed;
  private string? _name;
  private int _instanceId;
  private HideFlags _hideFlags;
  private bool _dontDestroyOnLoad;
  private float _destroyDelay = -1f;
  private static readonly HashSet<Object> _allObjects = new();
  private static readonly object _allObjectsLock = new();
  private static readonly List<ObjectDestroyInfo> _destroyQueue = new();
  private static int _nextInstanceId;

  public Object()
  {
    _instanceId = ++_nextInstanceId;
    lock (_allObjectsLock)
      _allObjects.Add(this);
  }

  /// <summary>Thread/test-safe snapshot of live objects (avoids Collection was modified).</summary>
  private static Object[] SnapshotAllObjects()
  {
    lock (_allObjectsLock)
    {
      var arr = new Object[_allObjects.Count];
      _allObjects.CopyTo(arr);
      return arr;
    }
  }

  internal static Object? FindObjectFromInstanceID(int instanceID)
  {
    if (instanceID == 0)
      return null;

    lock (_allObjectsLock)
      return _allObjects.FirstOrDefault(candidate => !candidate._destroyed && candidate._instanceId == instanceID);
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

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original) where T : Object
    => InstantiateAsync(original, 1, null, ReadOnlySpan<Vector3>.Empty, ReadOnlySpan<Quaternion>.Empty);

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, Transform? parent) where T : Object
    => InstantiateAsync(original, 1, parent, ReadOnlySpan<Vector3>.Empty, ReadOnlySpan<Quaternion>.Empty);

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, Vector3 position, Quaternion rotation) where T : Object
    => InstantiateAsync(original, 1, null, new[] { position }, new[] { rotation });

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, Transform? parent, Vector3 position, Quaternion rotation) where T : Object
    => InstantiateAsync(original, 1, parent, new[] { position }, new[] { rotation });

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count) where T : Object
    => InstantiateAsync(original, count, null, ReadOnlySpan<Vector3>.Empty, ReadOnlySpan<Quaternion>.Empty);

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, Transform? parent) where T : Object
    => InstantiateAsync(original, count, parent, ReadOnlySpan<Vector3>.Empty, ReadOnlySpan<Quaternion>.Empty);

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, Vector3 position, Quaternion rotation) where T : Object
    => InstantiateAsync(original, count, null, new[] { position }, new[] { rotation });

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, ReadOnlySpan<Vector3> positions, ReadOnlySpan<Quaternion> rotations) where T : Object
    => InstantiateAsync(original, count, null, positions, rotations);

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(T original, int count, Transform? parent, Vector3 position, Quaternion rotation) where T : Object
    => InstantiateAsync(original, count, parent, new[] { position }, new[] { rotation });

  public static AsyncInstantiateOperation<T> InstantiateAsync<T>(
    T original,
    int count,
    Transform? parent,
    ReadOnlySpan<Vector3> positions,
    ReadOnlySpan<Quaternion> rotations) where T : Object
  {
    if (original == null)
      throw new ArgumentException("The Object you want to instantiate is null.");
    if (count <= 0)
      throw new ArgumentException("Cannot call instantiate multiple with count less or equal to zero");
    if (original is ScriptableObject)
      throw new ArgumentException("Cannot call instantiate multiple for a ScriptableObject");

    Vector3[] positionValues = positions.ToArray();
    Quaternion[] rotationValues = rotations.ToArray();
    Transform? originalTransform = original switch
    {
      GameObject gameObject => gameObject.transform,
      Component component => component.transform,
      _ => null
    };
    Vector3 originalPosition = originalTransform?.position ?? Vector3.zero;
    Quaternion originalRotation = originalTransform?.rotation ?? Quaternion.identity;

    var operation = new AsyncInstantiateOperation(
      count,
      typeof(T),
      index =>
      {
        Vector3 position = positionValues.Length == 0
          ? originalPosition
          : positionValues[index % positionValues.Length];
        Quaternion rotation = rotationValues.Length == 0
          ? originalRotation
          : rotationValues[index % rotationValues.Length];
        return InstantiateInternal(original, position, rotation, parent, true);
      });
    return new AsyncInstantiateOperation<T>(operation);
  }

  [System.Security.SecuritySafeCritical]
  public int GetInstanceID()
  {
    return _instanceId;
  }

  [Internal.ExcludeFromDocs]
  public static void Destroy(Object? obj)
  {
    Destroy(obj, 0f);
  }

  [Bindings.NativeMethod(Name = "Scripting::DestroyObjectFromScripting", IsFreeFunction = true, ThrowsException = true)]
  public static void Destroy(Object? obj, [Internal.DefaultValue("0.0F")] float t)
  {
    if (obj is null || obj._destroyed)
    {
      return;
    }

    obj._destroyDelay = MathF.Max(0f, t);
    _destroyQueue.Add(new ObjectDestroyInfo(obj, Time.time + obj._destroyDelay));
  }

  [Internal.ExcludeFromDocs]
  public static void DestroyImmediate(Object? obj)
  {
    DestroyImmediate(obj, false);
  }

  [Bindings.NativeMethod(Name = "Scripting::DestroyObjectFromScriptingImmediate", IsFreeFunction = true, ThrowsException = true)]
  public static void DestroyImmediate(Object? obj, [Internal.DefaultValue("false")] bool allowDestroyingAssets)
  {
    _ = allowDestroyingAssets;
    if (obj is null || obj._destroyed)
    {
      return;
    }

    obj._destroyed = true;
    lock (_allObjectsLock)
      _allObjects.Remove(obj);
    _destroyQueue.RemoveAll(x => x.Target == obj);

    if (obj is Texture texture)
      Anity.Core.Runtime.Native.NativeGraphicsDevice.ReleaseTextureFromAll(texture);
    if (obj is VFX.VisualEffect visualEffect)
      visualEffect.ReleaseNativeState();
    if (obj is AvatarMask avatarMask)
      avatarMask.ReleaseNativeState();

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
        DestroyImmediate(comp);
      }

      GameObject.UnregisterFromScene(gameObject);
    }
    else if (obj is Component component)
    {
      if (component.gameObject is not null)
      {
        component.gameObject.RemoveComponentInternal(component);
      }
      if (component is Camera cam)
      {
        Camera.RemoveCamera(cam);
      }
      if (component is Canvas canvas)
      {
        Canvas._canvases.Remove(canvas);
      }
      if (component is Behaviour behaviour && behaviour.enabled)
      {
        try { if (component is MonoBehaviour mb) mb.InternalOnDisable(); } catch { }
      }
      if (component is MonoBehaviour destroyedBehaviour)
      {
        try { destroyedBehaviour.InternalOnDestroy(); } catch { }
      }
    }
  }

  [Obsolete("use Object.Destroy instead.")]
  [Internal.ExcludeFromDocs]
  public static void DestroyObject(Object? obj)
  {
    Destroy(obj);
  }

  [Obsolete("use Object.Destroy instead.")]
  public static void DestroyObject(Object? obj, [Internal.DefaultValue("0.0F")] float t)
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

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
  public static Object Instantiate(Object original)
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    return InstantiateInternal(original, position, rotation, null, true);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
  public static Object Instantiate(Object original, Vector3 position, Quaternion rotation)
  {
    return InstantiateInternal(original, position, rotation, null, true);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
  public static Object Instantiate(Object original, Transform? parent)
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    return InstantiateInternal(original, position, rotation, parent, false);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
  public static Object Instantiate(Object original, Transform? parent, bool instantiateInWorldSpace)
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    return InstantiateInternal(original, position, rotation, parent, instantiateInWorldSpace);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
  public static Object Instantiate(Object original, SceneManagement.Scene scene)
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    Object clone = InstantiateInternal(original, position, rotation, null, true);
    GameObject? cloneGameObject = clone as GameObject ?? (clone as Component)?.gameObject;
    if (cloneGameObject is not null)
      SceneManagement.SceneManager.MoveGameObjectToScene(cloneGameObject, scene);
    return clone;
  }

  internal static Object Instantiate(Object original, Transform? parent, InstantiateParameters instantiateParameters)
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    bool worldPositionStays = instantiateParameters.instantiateInWorldSpace || instantiateParameters.worldPositionStays;
    return InstantiateInternal(original, position, rotation, parent, worldPositionStays);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
  public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform? parent)
  {
    return InstantiateInternal(original, position, rotation, parent, true);
  }

  public static T Instantiate<T>(T original) where T : Object
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    return (T)InstantiateInternal(original, position, rotation, null, true);
  }

  public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
  {
    return (T)InstantiateInternal(original, position, rotation, null, true);
  }

  public static T Instantiate<T>(T original, Transform? parent) where T : Object
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    return (T)InstantiateInternal(original, position, rotation, parent, false);
  }

  public static T Instantiate<T>(T original, Transform? parent, bool worldPositionStays) where T : Object
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    return (T)InstantiateInternal(original, position, rotation, parent, worldPositionStays);
  }

  public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform? parent) where T : Object
  {
    return (T)InstantiateInternal(original, position, rotation, parent, true);
  }

  internal static T Instantiate<T>(T original, Transform? parent, InstantiateParameters instantiateParameters) where T : Object
  {
    GetOriginalWorldPose(original, out Vector3 position, out Quaternion rotation);
    bool worldPositionStays = instantiateParameters.instantiateInWorldSpace || instantiateParameters.worldPositionStays;
    return (T)InstantiateInternal(original, position, rotation, parent, worldPositionStays);
  }

  private static void GetOriginalWorldPose(Object original, out Vector3 position, out Quaternion rotation)
  {
    Transform? transform = original is GameObject gameObject
      ? gameObject.transform
      : (original as Component)?.transform;
    position = transform?.position ?? Vector3.zero;
    rotation = transform?.rotation ?? Quaternion.identity;
  }

  /// <summary>
  /// Core clone path. Named distinctly so generic overloads cannot recurse into each other
  /// (C# prefers Instantiate&lt;T&gt;(T, Vector3, Quaternion, Transform) over a private 5-arg overload).
  /// </summary>
  private static Object InstantiateInternal(Object original, Vector3 position, Quaternion rotation, Transform? parent, bool worldPositionStays)
  {
    if (original == null) throw new ArgumentNullException(nameof(original));
    if (original is GameObject originalGo)
    {
      return CloneGameObject(originalGo, position, rotation, parent, worldPositionStays);
    }
    if (original is Component originalComp)
    {
      var go = CloneGameObject(originalComp.gameObject!, position, rotation, parent, worldPositionStays);
      return go.GetComponent(originalComp.GetType())!;
    }

    var type = original.GetType();
    if (type.IsAbstract || type.IsInterface)
      return original;
    var clone = (Object)Activator.CreateInstance(type)!;
    clone._name = original._name + "(Clone)";
    CopyFields(original, clone);
    return clone;
  }

  private static GameObject CloneGameObject(
    GameObject original,
    Vector3 position,
    Quaternion rotation,
    Transform? parent,
    bool worldPositionStays,
    bool invokeLifecycle = true)
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
      var newComp = clone.RegisterClonedComponent(type);
      CopyFields(comp, newComp);
      if (newComp is Behaviour newBehaviour)
      {
        var origBehaviour = comp as Behaviour;
        newBehaviour.enabled = origBehaviour?.enabled ?? true;
      }
      if (newComp is MonoBehaviour newMonoBehaviour && comp is MonoBehaviour originalMonoBehaviour)
      {
        newMonoBehaviour.useGUILayout = originalMonoBehaviour.useGUILayout;
        newMonoBehaviour.runInEditMode = originalMonoBehaviour.runInEditMode;
      }
    }

    if (parent is not null && worldPositionStays)
    {
      clone.transform.position = position;
      clone.transform.rotation = rotation;
      clone.transform.localScale = original.transform.localScale;
      clone.transform.SetParent(parent, true);
    }
    else if (parent is not null)
    {
      clone.transform.SetParent(parent, false);
      clone.transform.localPosition = original.transform.localPosition;
      clone.transform.localRotation = original.transform.localRotation;
      clone.transform.localScale = original.transform.localScale;
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
        var childClone = CloneGameObject(child.gameObject, child.position, child.rotation, clone.transform, false, false);
        childClone.transform.localPosition = child.localPosition;
        childClone.transform.localRotation = child.localRotation;
        childClone.transform.localScale = child.localScale;
      }
    }

    if (invokeLifecycle)
      InvokeCloneLifecycle(clone);

    return clone;
  }

  private static void InvokeCloneLifecycle(GameObject root)
  {
    var behaviours = new List<MonoBehaviour>();
    CollectCloneBehaviours(root, behaviours);
    foreach (MonoBehaviour behaviour in behaviours)
    {
      if (behaviour.gameObject is not null && behaviour.gameObject.activeInHierarchy)
      {
        try { behaviour.InternalAwake(); } catch { }
        if (behaviour.enabled)
        {
          try { behaviour.InternalOnEnable(); } catch { }
        }
      }
    }
  }

  private static void CollectCloneBehaviours(GameObject gameObject, List<MonoBehaviour> behaviours)
  {
    behaviours.AddRange(gameObject.GetComponents<MonoBehaviour>());
    for (int i = 0; i < gameObject.transform.childCount; i++)
    {
      GameObject? child = gameObject.transform.GetChild(i).gameObject;
      if (child is not null)
        CollectCloneBehaviours(child, behaviours);
    }
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
        if (field.DeclaringType == typeof(Component)) continue;
        if (field.DeclaringType == typeof(MonoBehaviour)) continue;
        if (field.IsNotSerialized) continue;
        if (field.DeclaringType?.Assembly != typeof(Object).Assembly
            && !field.IsPublic
            && !field.IsDefined(typeof(SerializeField), false))
          continue;
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

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public static Object? FindObjectOfType(Type type)
  {
    return FindObjectOfType(type, false);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public static Object? FindObjectOfType(Type type, bool includeInactive)
  {
    return SnapshotAllObjects().FirstOrDefault(o => o != null
      && !o._destroyed
      && type.IsAssignableFrom(o.GetType())
      && (includeInactive || IsActiveInHierarchy(o)));
  }

  public static T? FindObjectOfType<T>() where T : Object
  {
    return (T?)FindObjectOfType(typeof(T));
  }

  public static Object[] FindObjectsOfType(Type type)
  {
    return FindObjectsOfType(type, false);
  }

  [Bindings.FreeFunction("UnityEngineObjectBindings::FindObjectsOfType")]
  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.ArrayOfTypeReferencedByFirstArgument)]
  public static Object[] FindObjectsOfType(Type type, bool includeInactive)
  {
    return SnapshotAllObjects().Where(o => o != null
      && !o._destroyed
      && type.IsAssignableFrom(o.GetType())
      && (includeInactive || IsActiveInHierarchy(o))).ToArray();
  }

  public static T[] FindObjectsOfType<T>() where T : Object
  {
    return FindObjectsOfType<T>(false);
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

  [Bindings.FreeFunction("UnityEngineObjectBindings::FindObjectsByType")]
  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.ArrayOfTypeReferencedByFirstArgument)]
  public static Object[] FindObjectsByType(Type type, FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode)
  {
    var objects = FindObjectsOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    if (sortMode == FindObjectsSortMode.InstanceID)
    {
      Array.Sort(objects, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }
    return objects;
  }

  public static T[] FindObjectsByType<T>(FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) where T : Object
  {
    var objects = FindObjectsOfType<T>(findObjectsInactive == FindObjectsInactive.Include);
    if (sortMode == FindObjectsSortMode.InstanceID)
    {
      Array.Sort(objects, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }
    return objects;
  }

  public static T? FindObjectOfType<T>(bool includeInactive) where T : Object
  {
    return (T?)FindObjectOfType(typeof(T), includeInactive);
  }

  public static T[] FindObjectsOfType<T>(bool includeInactive) where T : Object
  {
    return FindObjectsOfType(typeof(T), includeInactive).OfType<T>().ToArray();
  }

  private static bool IsActiveInHierarchy(Object obj)
    => obj switch
    {
      GameObject gameObject => gameObject.activeInHierarchy,
      Component component => component.gameObject is not null && component.gameObject.activeInHierarchy,
      _ => true
    };

  public static Object? FindFirstObjectByType(Type type)
    => FindFirstObjectByType(type, FindObjectsInactive.Exclude);

  public static Object? FindFirstObjectByType(Type type, FindObjectsInactive findObjectsInactive)
    => FindObjectsByType(type, findObjectsInactive, FindObjectsSortMode.InstanceID).FirstOrDefault();

  public static T? FindFirstObjectByType<T>() where T : Object
    => FindFirstObjectByType<T>(FindObjectsInactive.Exclude);

  public static T? FindFirstObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object
    => FindObjectsByType<T>(findObjectsInactive, FindObjectsSortMode.InstanceID).FirstOrDefault();

  public static Object? FindAnyObjectByType(Type type)
    => FindAnyObjectByType(type, FindObjectsInactive.Exclude);

  public static Object? FindAnyObjectByType(Type type, FindObjectsInactive findObjectsInactive)
    => FindObjectsByType(type, findObjectsInactive, FindObjectsSortMode.None).FirstOrDefault();

  public static T? FindAnyObjectByType<T>() where T : Object
    => FindAnyObjectByType<T>(FindObjectsInactive.Exclude);

  public static T? FindAnyObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object
    => FindObjectsByType<T>(findObjectsInactive, FindObjectsSortMode.None).FirstOrDefault();

  [Obsolete("Please use Resources.FindObjectsOfTypeAll instead")]
  public static Object[] FindObjectsOfTypeAll(Type type) => FindObjectsOfType(type, true);

  [Obsolete("use Resources.FindObjectsOfTypeAll instead.")]
  [Bindings.FreeFunction("UnityEngineObjectBindings::FindObjectsOfTypeIncludingAssets")]
  public static Object[] FindObjectsOfTypeIncludingAssets(Type type) => FindObjectsOfType(type, true);

  [Obsolete("warning use Object.FindObjectsByType instead.")]
  public static Object[] FindSceneObjectsOfType(Type type) => FindObjectsOfType(type, false);

  [Bindings.FreeFunction("GetSceneManager().DontDestroyOnLoad", ThrowsException = true)]
  public static void DontDestroyOnLoad([Bindings.NotNull("NullExceptionObject")] Object target)
  {
    if (target is not null)
    {
      target._dontDestroyOnLoad = true;
    }
  }

  internal bool IsDontDestroyOnLoad => _dontDestroyOnLoad;

  internal static void Copy<T>(T source, T destination) where T : Object
  {
    if (source is null || destination is null) return;
    CopyFields(source, destination);
  }

  public static bool operator ==(Object? x, Object? y)
  {
    bool xNull = x is null || x._destroyed;
    bool yNull = y is null || y._destroyed;
    if (xNull && yNull) return true;
    if (xNull || yNull) return false;
    return ReferenceEquals(x, y) || x._instanceId == y._instanceId;
  }

  public static bool operator !=(Object? x, Object? y)
  {
    return !(x == y);
  }

  public static implicit operator bool(Object? exists) => exists != null;

  public override bool Equals(object? other)
  {
    if (other is Object otherObject)
    {
      if (_destroyed && otherObject._destroyed) return true;
      if (_destroyed || otherObject._destroyed) return false;
      return _instanceId == otherObject._instanceId;
    }
    if (_destroyed && other is null) return true;
    return false;
  }

  public override int GetHashCode()
  {
    return _instanceId;
  }

  internal bool IsDestroyed => _destroyed;

  public override string ToString()
  {
    return name ?? GetType().Name;
  }

  internal static Object[] GetAllObjects()
  {
    return SnapshotAllObjects().Where(o => o != null && !o._destroyed).ToArray();
  }

  internal static IReadOnlyCollection<Object> AllObjects => SnapshotAllObjects();

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

internal struct InstantiateParameters
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
