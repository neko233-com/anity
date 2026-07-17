using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.SceneManagement;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Export/Scripting/GameObject.bindings.h")]
[ExcludeFromPreset]
[Scripting.UsedByNativeCode]
public sealed class GameObject : Object
{
  private static readonly object _sceneLock = new();
  private static readonly Dictionary<string, List<GameObject>> _sceneObjects = new(StringComparer.Ordinal);
  private static readonly List<GameObject> _allObjects = new();
  private readonly List<Component> _components = new();
  private Transform _transform;
  private Scene _scene;

  public GameObject()
    : this("New Game Object")
  {
  }

  public GameObject(string name)
  {
    this.name = name;
    _transform = Transform.CreateForGameObject(this);
    _components.Add(_transform);
    _scene = SceneManager.GetActiveScene();
    AddToScene(this);
  }

  public GameObject(string name, params Type[]? components)
    : this(name)
  {
    if (components == null)
    {
      return;
    }

    foreach (var comp in components)
    {
      AddComponent(comp);
    }
  }

  public Transform transform => _transform;
  public GameObject gameObject => this;
  public ulong sceneCullingMask => ulong.MaxValue;
  public bool activeSelf { get; private set; } = true;

  [Obsolete("GameObject.active is obsolete. Use GameObject.SetActive(), GameObject.activeSelf or GameObject.activeInHierarchy.")]
  public bool active
  {
    get => activeSelf;
    set => SetActive(value);
  }

  public bool activeInHierarchy
  {
    get
    {
      if (!activeSelf) return false;
      var current = transform.parent;
      while (current is not null)
      {
        if (current.gameObject is null || !current.gameObject.activeSelf)
        {
          return false;
        }
        current = current.parent;
      }
      return true;
    }
  }

  public string tag { get; set; } = "Untagged";

  private int _layer;
  public int layer
  {
    get => _layer;
    set => _layer = value & 31;
  }

  public bool isStatic { get; set; }

  public SceneManagement.Scene scene => _scene;

  [Bindings.FreeFunction(Name = "GameObjectBindings::GetSceneByInstanceID")]
  public static Scene GetScene(int instanceID)
  {
    return Object.FindObjectFromInstanceID(instanceID) is GameObject gameObject
      ? gameObject.scene
      : default;
  }

  [Bindings.FreeFunction(Name = "GameObjectBindings::Find")]
  public static GameObject? Find(string name)
  {
    return _sceneObjects.TryGetValue(name, out var list)
      ? list.FirstOrDefault(go => !go.IsDestroyed)
      : null;
  }

  [Bindings.FreeFunction(Name = "GameObjectBindings::FindGameObjectsWithTag", ThrowsException = true)]
  public static GameObject[] FindGameObjectsWithTag(string tag)
  {
    return _allObjects
      .Where(go => !go.IsDestroyed && string.Equals(go.tag, tag, StringComparison.Ordinal))
      .ToArray();
  }

  public static GameObject? FindWithTag(string tag)
  {
    return FindGameObjectsWithTag(tag).FirstOrDefault();
  }

  [Bindings.FreeFunction(Name = "GameObjectBindings::FindGameObjectWithTag", ThrowsException = true)]
  public static GameObject? FindGameObjectWithTag(string tag) => FindWithTag(tag);

  public T AddComponent<T>() where T : Component
  {
    return (T)AddComponent(typeof(T));
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component AddComponent(Type componentType)
  {
    return AddComponent(componentType, new HashSet<Type>());
  }

  [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
  [Obsolete("GameObject.AddComponent with string argument has been deprecated. Use GameObject.AddComponent<T>() instead. (UnityUpgradable).", true)]
  public Component AddComponent(string className)
    => throw new NotSupportedException($"String component lookup is not supported: {className}");

  private Component AddComponent(Type componentType, HashSet<Type> adding)
  {
    if (componentType is null || !typeof(Component).IsAssignableFrom(componentType))
    {
      throw new ArgumentException("componentType must inherit from Component", nameof(componentType));
    }

    if (componentType.IsAbstract)
    {
      throw new InvalidOperationException("Cannot add abstract component.");
    }

    Type? disallowRoot = GetDisallowMultipleRoot(componentType);
    if (disallowRoot is not null && _components.Any(component => disallowRoot.IsAssignableFrom(component.GetType())))
    {
      throw new InvalidOperationException($"Cannot add more than one component derived from {disallowRoot.FullName}.");
    }

    if (!adding.Add(componentType))
    {
      return GetComponent(componentType)
        ?? throw new InvalidOperationException($"Circular RequireComponent dependency for {componentType.FullName}.");
    }

    try
    {
      foreach (Type requiredType in GetRequiredComponentTypes(componentType))
      {
        if (GetComponent(requiredType) is not null || adding.Contains(requiredType)) continue;
        AddComponent(requiredType, adding);
      }

      var component = Activator.CreateInstance(componentType) as Component
        ?? throw new InvalidOperationException($"Failed to create {componentType.Name}");

      return RegisterComponent(component);
    }
    finally
    {
      adding.Remove(componentType);
    }
  }

  private static IEnumerable<Type> GetRequiredComponentTypes(Type componentType)
  {
    foreach (RequireComponent requirement in componentType.GetCustomAttributes(typeof(RequireComponent), true))
    {
      foreach (Type? requiredType in new[] { requirement.m_Type0, requirement.m_Type1, requirement.m_Type2 })
      {
        if (requiredType is null) continue;
        if (!typeof(Component).IsAssignableFrom(requiredType) || requiredType.IsAbstract)
          throw new InvalidOperationException($"Required component {requiredType.FullName} must be a concrete Component.");
        yield return requiredType;
      }
    }
  }

  private static Type? GetDisallowMultipleRoot(Type componentType)
  {
    for (Type? current = componentType; current is not null && typeof(Component).IsAssignableFrom(current); current = current.BaseType)
    {
      if (current.IsDefined(typeof(DisallowMultipleComponent), false)) return current;
    }
    return null;
  }

  internal Component RegisterComponent(Component component)
    => RegisterComponent(component, true);

  private Component RegisterComponent(Component component, bool invokeLifecycle)
  {
    if (component is Transform replacement && !ReferenceEquals(replacement, _transform))
    {
      if (replacement is not RectTransform || _transform is RectTransform)
        throw new InvalidOperationException("A GameObject can only contain one Transform component.");

      replacement.gameObject = this;
      replacement.AdoptStateFrom(_transform);
      int transformIndex = _components.IndexOf(_transform);
      if (transformIndex >= 0) _components[transformIndex] = replacement;
      else _components.Insert(0, replacement);
      _transform = replacement;
      return replacement;
    }

    component.gameObject = this;
    _components.Add(component);

    if (invokeLifecycle && component is MonoBehaviour mb && activeInHierarchy)
    {
      try { mb.InternalAwake(); } catch { }
      if (mb.enabled)
      {
        try { mb.InternalOnEnable(); } catch { }
      }
    }

    return component;
  }

  internal Component RegisterClonedComponent(Type componentType)
  {
    if (componentType is null || !typeof(Component).IsAssignableFrom(componentType) || componentType.IsAbstract)
      throw new ArgumentException("componentType must be a concrete Component", nameof(componentType));
    var component = Activator.CreateInstance(componentType) as Component
      ?? throw new InvalidOperationException($"Failed to create {componentType.Name}");
    return RegisterComponent(component, false);
  }

  internal void RemoveComponentInternal(Component component)
  {
    _components.Remove(component);
  }

  [Bindings.FreeFunction(Name = "GameObjectBindings::GetComponentFromType", HasExplicitThis = true, ThrowsException = true)]
  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponent(Type type)
  {
    if (type is null) throw new ArgumentNullException(nameof(type));
    return _components.FirstOrDefault(type.IsInstanceOfType);
  }

  [System.Security.SecuritySafeCritical]
  public T GetComponent<T>()
  {
    Component? component = GetComponent(typeof(T));
    return component is T typed ? typed : default!;
  }

  public Component? GetComponent(string type)
    => GetComponentByName(type);

  internal Component? GetComponentByName(string type)
  {
    if (type is null) throw new ArgumentNullException(nameof(type));
    return _components.FirstOrDefault(component =>
      string.Equals(component.GetType().Name, type, StringComparison.Ordinal)
      || string.Equals(component.GetType().FullName, type, StringComparison.Ordinal));
  }

  public bool TryGetComponent(Type type, out Component? component)
  {
    component = GetComponent(type);
    return component is not null;
  }

  [System.Security.SecuritySafeCritical]
  public bool TryGetComponent<T>(out T component)
  {
    component = GetComponent<T>();
    return component is not null;
  }

  public Component[] GetComponents(Type type)
  {
    if (type is null) throw new ArgumentNullException(nameof(type));
    return _components.Where(type.IsInstanceOfType).ToArray();
  }

  public void GetComponents(Type type, List<Component> results)
  {
    if (results is null) throw new ArgumentNullException(nameof(results));
    results.Clear();
    results.AddRange(GetComponents(type));
  }

  public T[] GetComponents<T>() => _components.OfType<T>().ToArray();

  public void GetComponents<T>(List<T> results)
  {
    if (results is null) throw new ArgumentNullException(nameof(results));
    results.Clear();
    results.AddRange(_components.OfType<T>());
  }

  [Internal.ExcludeFromDocs]
  public Component[] GetComponentsInChildren(Type type) => GetComponentsInChildren(type, false);

  public Component[] GetComponentsInChildren(
    Type type,
    [Internal.DefaultValue("false")] bool includeInactive)
  {
    if (type is null) throw new ArgumentNullException(nameof(type));
    var components = new List<Component>();
    CollectComponentsInChildren(this, type, includeInactive, true, components);
    return components.ToArray();
  }

  public T[] GetComponentsInChildren<T>() => GetComponentsInChildren<T>(false);

  public T[] GetComponentsInChildren<T>(bool includeInactive)
    => GetComponentsInChildren(typeof(T), includeInactive).OfType<T>().ToArray();

  public void GetComponentsInChildren<T>(List<T> results)
    => GetComponentsInChildren(false, results);

  public void GetComponentsInChildren<T>(bool includeInactive, List<T> results)
  {
    if (results is null) throw new ArgumentNullException(nameof(results));
    results.Clear();
    results.AddRange(GetComponentsInChildren<T>(includeInactive));
  }

  [Internal.ExcludeFromDocs]
  public Component[] GetComponentsInParent(Type type) => GetComponentsInParent(type, false);

  public Component[] GetComponentsInParent(
    Type type,
    [Internal.DefaultValue("false")] bool includeInactive)
  {
    if (type is null) throw new ArgumentNullException(nameof(type));
    var components = new List<Component>();
    GameObject? current = this;
    bool isRoot = true;
    while (current is not null)
    {
      if (isRoot || includeInactive || current.activeInHierarchy)
        components.AddRange(current.GetComponents(type));
      current = current.transform.parent?.gameObject;
      isRoot = false;
    }
    return components.ToArray();
  }

  public T[] GetComponentsInParent<T>() => GetComponentsInParent<T>(false);

  public T[] GetComponentsInParent<T>(bool includeInactive)
    => GetComponentsInParent(typeof(T), includeInactive).OfType<T>().ToArray();

  public void GetComponentsInParent<T>(bool includeInactive, List<T> results)
  {
    if (results is null) throw new ArgumentNullException(nameof(results));
    results.Clear();
    results.AddRange(GetComponentsInParent<T>(includeInactive));
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInChildren(Type type) => GetComponentInChildren(type, false);

  [Bindings.FreeFunction(Name = "GameObjectBindings::GetComponentInChildren", HasExplicitThis = true, ThrowsException = true)]
  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInChildren(Type type, bool includeInactive)
    => GetComponentsInChildren(type, includeInactive).FirstOrDefault();

  [Internal.ExcludeFromDocs]
  public T GetComponentInChildren<T>() => GetComponentInChildren<T>(false);

  public T GetComponentInChildren<T>([Internal.DefaultValue("false")] bool includeInactive)
    => GetComponentsInChildren<T>(includeInactive).FirstOrDefault()!;

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInParent(Type type) => GetComponentInParent(type, false);

  [Bindings.FreeFunction(Name = "GameObjectBindings::GetComponentInParent", HasExplicitThis = true, ThrowsException = true)]
  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInParent(Type type, bool includeInactive)
    => GetComponentsInParent(type, includeInactive).FirstOrDefault();

  [Internal.ExcludeFromDocs]
  public T GetComponentInParent<T>() => GetComponentInParent<T>(false);

  public T GetComponentInParent<T>([Internal.DefaultValue("false")] bool includeInactive)
    => GetComponentsInParent<T>(includeInactive).FirstOrDefault()!;

  public int GetComponentCount() => _components.Count;

  public Component GetComponentAtIndex(int index)
  {
    if ((uint)index >= (uint)_components.Count) throw new ArgumentOutOfRangeException(nameof(index));
    return _components[index];
  }

  public T GetComponentAtIndex<T>(int index) where T : Component => (T)GetComponentAtIndex(index);

  public int GetComponentIndex(Component component) => _components.IndexOf(component);

  [Bindings.FreeFunction(Name = "GameObjectBindings::CompareTag", HasExplicitThis = true)]
  public bool CompareTag(string tag) => string.Equals(this.tag, tag, StringComparison.Ordinal);

  private static void CollectComponentsInChildren(
    GameObject current,
    Type type,
    bool includeInactive,
    bool isRoot,
    List<Component> results)
  {
    if (!isRoot && !includeInactive && !current.activeInHierarchy) return;
    results.AddRange(current.GetComponents(type));
    for (int i = 0; i < current.transform.childCount; i++)
    {
      GameObject? child = current.transform.GetChild(i).gameObject;
      if (child is not null)
        CollectComponentsInChildren(child, type, includeInactive, false, results);
    }
  }

  [Bindings.NativeMethod(Name = "SetSelfActive")]
  public void SetActive(bool value)
  {
    if (activeSelf == value) return;

    List<(GameObject GameObject, bool WasActive)> hierarchy = CaptureHierarchyState(this);
    activeSelf = value;

    DispatchHierarchyStateChanges(hierarchy);
  }

  [Obsolete("gameObject.SetActiveRecursively() is obsolete. Use GameObject.SetActive(), which is now inherited by children.")]
  [Bindings.NativeMethod(Name = "SetActiveRecursivelyDeprecated")]
  public void SetActiveRecursively(bool state)
  {
    List<(GameObject GameObject, bool WasActive)> hierarchy = CaptureHierarchyState(this);
    foreach ((GameObject gameObject, _) in hierarchy)
      gameObject.activeSelf = state;
    DispatchHierarchyStateChanges(hierarchy);
  }

  public static void SetGameObjectsActive(NativeArray<int> instanceIDs, bool active)
  {
    if (!instanceIDs.IsCreated)
      throw new ArgumentException("NativeArray is uninitialized", nameof(instanceIDs));
    if (instanceIDs.Length == 0)
      return;

    var ids = new int[instanceIDs.Length];
    for (int i = 0; i < ids.Length; i++)
      ids[i] = instanceIDs[i];
    SetGameObjectsActive(ids.AsSpan(), active);
  }

  public static void SetGameObjectsActive(ReadOnlySpan<int> instanceIDs, bool active)
  {
    if (instanceIDs.Length == 0)
      return;

    var targets = new List<GameObject>(instanceIDs.Length);
    var seen = new HashSet<GameObject>();
    foreach (int instanceID in instanceIDs)
    {
      if (Object.FindObjectFromInstanceID(instanceID) is GameObject gameObject && seen.Add(gameObject))
        targets.Add(gameObject);
    }

    var affected = new List<(GameObject GameObject, bool WasActive)>();
    var affectedSet = new HashSet<GameObject>();
    foreach (GameObject target in targets)
    {
      foreach ((GameObject gameObject, bool wasActive) in CaptureHierarchyState(target))
      {
        if (affectedSet.Add(gameObject))
          affected.Add((gameObject, wasActive));
      }
    }

    foreach (GameObject target in targets)
      target.activeSelf = active;
    DispatchHierarchyStateChanges(affected);
  }

  public static void InstantiateGameObjects(
    int sourceInstanceID,
    int count,
    NativeArray<int> newInstanceIDs,
    NativeArray<int> newTransformInstanceIDs,
    [Optional] Scene destinationScene)
  {
    if (!newInstanceIDs.IsCreated)
      throw new ArgumentException("NativeArray is uninitialized", nameof(newInstanceIDs));
    if (!newTransformInstanceIDs.IsCreated)
      throw new ArgumentException("NativeArray is uninitialized", nameof(newTransformInstanceIDs));
    if (count == 0)
      return;
    if (count != newInstanceIDs.Length || count != newTransformInstanceIDs.Length)
      throw new ArgumentException("Size mismatch! Both arrays must already be the size of count.");

    GameObject? source = Object.FindObjectFromInstanceID(sourceInstanceID) as GameObject;
    for (int i = 0; i < count; i++)
    {
      if (source is null)
      {
        newInstanceIDs[i] = 0;
        newTransformInstanceIDs[i] = 0;
        continue;
      }

      var clone = (GameObject)Object.Instantiate(source, source.transform.position, source.transform.rotation);
      Scene targetScene = destinationScene.IsValid()
        ? destinationScene
        : source.scene;
      if (targetScene.IsValid())
        SceneManager.MoveGameObjectToScene(clone, targetScene);
      newInstanceIDs[i] = clone.GetInstanceID();
      newTransformInstanceIDs[i] = clone.transform.GetInstanceID();
    }
  }

  private static List<(GameObject GameObject, bool WasActive)> CaptureHierarchyState(GameObject root)
  {
    var result = new List<(GameObject, bool)>();
    CaptureHierarchyStateRecursive(root, result);
    return result;
  }

  private static void CaptureHierarchyStateRecursive(
    GameObject gameObject,
    List<(GameObject GameObject, bool WasActive)> result)
  {
    result.Add((gameObject, gameObject.activeInHierarchy));
    for (int i = 0; i < gameObject.transform.childCount; i++)
    {
      GameObject? child = gameObject.transform.GetChild(i).gameObject;
      if (child is not null)
        CaptureHierarchyStateRecursive(child, result);
    }
  }

  private static void DispatchHierarchyStateChanges(
    List<(GameObject GameObject, bool WasActive)> hierarchy)
  {
    for (int i = hierarchy.Count - 1; i >= 0; i--)
    {
      (GameObject gameObject, bool wasActive) = hierarchy[i];
      bool isActive = gameObject.activeInHierarchy;
      if (wasActive && !isActive)
        SetComponentsActiveState(gameObject, false);
    }

    foreach ((GameObject gameObject, bool wasActive) in hierarchy)
    {
      bool isActive = gameObject.activeInHierarchy;
      if (!wasActive && isActive)
        SetComponentsActiveState(gameObject, true);
    }
  }

  private static void SetComponentsActiveState(GameObject go, bool isActive)
  {
    foreach (var comp in go._components)
    {
      if (comp is Behaviour behaviour && behaviour.enabled)
      {
        if (comp is MonoBehaviour mb)
        {
          try
          {
            if (isActive)
            {
              mb.InternalAwake();
              mb.InternalOnEnable();
            }
            else if (mb.IsAwakened)
            {
              mb.InternalOnDisable();
            }
          }
          catch { }
        }
      }
    }
  }

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property rigidbody has been deprecated. Use GetComponent<Rigidbody>() instead. (UnityUpgradable)", true)]
  public Component rigidbody => throw DeprecatedProperty(nameof(rigidbody));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property rigidbody2D has been deprecated. Use GetComponent<Rigidbody2D>() instead. (UnityUpgradable)", true)]
  public Component rigidbody2D => throw DeprecatedProperty(nameof(rigidbody2D));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property camera has been deprecated. Use GetComponent<Camera>() instead. (UnityUpgradable)", true)]
  public Component camera => throw DeprecatedProperty(nameof(camera));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property light has been deprecated. Use GetComponent<Light>() instead. (UnityUpgradable)", true)]
  public Component light => throw DeprecatedProperty(nameof(light));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property animation has been deprecated. Use GetComponent<Animation>() instead. (UnityUpgradable)", true)]
  public Component animation => throw DeprecatedProperty(nameof(animation));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property constantForce has been deprecated. Use GetComponent<ConstantForce>() instead. (UnityUpgradable)", true)]
  public Component constantForce => throw DeprecatedProperty(nameof(constantForce));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property renderer has been deprecated. Use GetComponent<Renderer>() instead. (UnityUpgradable)", true)]
  public Component renderer => throw DeprecatedProperty(nameof(renderer));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property audio has been deprecated. Use GetComponent<AudioSource>() instead. (UnityUpgradable)", true)]
  public Component audio => throw DeprecatedProperty(nameof(audio));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property networkView has been deprecated. Use GetComponent<NetworkView>() instead. (UnityUpgradable)", true)]
  public Component networkView => throw DeprecatedProperty(nameof(networkView));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property collider has been deprecated. Use GetComponent<Collider>() instead. (UnityUpgradable)", true)]
  public Component collider => throw DeprecatedProperty(nameof(collider));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property collider2D has been deprecated. Use GetComponent<Collider2D>() instead. (UnityUpgradable)", true)]
  public Component collider2D => throw DeprecatedProperty(nameof(collider2D));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property hingeJoint has been deprecated. Use GetComponent<HingeJoint>() instead. (UnityUpgradable)", true)]
  public Component hingeJoint => throw DeprecatedProperty(nameof(hingeJoint));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("Property particleSystem has been deprecated. Use GetComponent<ParticleSystem>() instead. (UnityUpgradable)", true)]
  public Component particleSystem => throw DeprecatedProperty(nameof(particleSystem));

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("GameObject.SampleAnimation(AnimationClip, float) has been deprecated. Use AnimationClip.SampleAnimation(GameObject, float) instead (UnityUpgradable).", true)]
  public void SampleAnimation(Object clip, float time)
    => throw new NotSupportedException("GameObject.SampleAnimation is deprecated");

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("gameObject.PlayAnimation is not supported anymore. Use animation.Play()", true)]
  public void PlayAnimation(Object animation)
    => throw new NotSupportedException("gameObject.PlayAnimation is not supported anymore. Use animation.Play();");

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete("gameObject.StopAnimation is not supported anymore. Use animation.Stop()", true)]
  public void StopAnimation()
    => throw new NotSupportedException("gameObject.StopAnimation(); is not supported anymore. Use animation.Stop();");

  private static NotSupportedException DeprecatedProperty(string propertyName)
    => new($"{propertyName} property has been deprecated");

  [Internal.ExcludeFromDocs]
  public void SendMessage(string methodName) => SendMessage(methodName, null, SendMessageOptions.RequireReceiver);

  [Internal.ExcludeFromDocs]
  public void SendMessage(string methodName, object? value)
    => SendMessage(methodName, value, SendMessageOptions.RequireReceiver);

  public void SendMessage(string methodName, SendMessageOptions options) => SendMessage(methodName, null, options);

  [Bindings.FreeFunction(Name = "Scripting::SendScriptingMessage", HasExplicitThis = true)]
  public void SendMessage(
    string methodName,
    [Internal.DefaultValue("null")] object? value,
    [Internal.DefaultValue("SendMessageOptions.RequireReceiver")] SendMessageOptions options)
  {
    bool found = false;
    var args = value is not null ? new[] { value } : Array.Empty<object>();
    var argTypes = value is not null ? new[] { value.GetType() } : Type.EmptyTypes;

    foreach (var comp in _components)
    {
      if (comp is null) continue;
      var type = comp.GetType();
      var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, argTypes, null);
      if (method is null && value is null)
      {
        method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
      }
      if (method is not null)
      {
        found = true;
        try
        {
          var parameters = method.GetParameters();
          if (parameters.Length == 0)
          {
            method.Invoke(comp, null);
          }
          else if (parameters.Length == 1 && value is not null)
          {
            method.Invoke(comp, args);
          }
        }
        catch { }
      }
    }

    if (!found && options == SendMessageOptions.RequireReceiver)
    {
      Debug.LogWarning($"SendMessage: method '{methodName}' not found on {name}");
    }
  }

  [Internal.ExcludeFromDocs]
  public void SendMessageUpwards(string methodName)
    => SendMessageUpwards(methodName, null, SendMessageOptions.RequireReceiver);

  [Internal.ExcludeFromDocs]
  public void SendMessageUpwards(string methodName, object? value)
    => SendMessageUpwards(methodName, value, SendMessageOptions.RequireReceiver);

  public void SendMessageUpwards(string methodName, SendMessageOptions options)
    => SendMessageUpwards(methodName, null, options);

  [Bindings.FreeFunction(Name = "Scripting::SendScriptingMessageUpwards", HasExplicitThis = true)]
  public void SendMessageUpwards(
    string methodName,
    [Internal.DefaultValue("null")] object? value,
    [Internal.DefaultValue("SendMessageOptions.RequireReceiver")] SendMessageOptions options)
  {
    SendMessage(methodName, value, options);
    if (transform.parent is not null && transform.parent.gameObject is not null)
    {
      transform.parent.gameObject.SendMessageUpwards(methodName, value, options);
    }
  }

  [Internal.ExcludeFromDocs]
  public void BroadcastMessage(string methodName)
    => BroadcastMessage(methodName, null, SendMessageOptions.RequireReceiver);

  [Internal.ExcludeFromDocs]
  public void BroadcastMessage(string methodName, object? parameter)
    => BroadcastMessage(methodName, parameter, SendMessageOptions.RequireReceiver);

  public void BroadcastMessage(string methodName, SendMessageOptions options)
    => BroadcastMessage(methodName, null, options);

  [Bindings.FreeFunction(Name = "Scripting::BroadcastScriptingMessage", HasExplicitThis = true)]
  public void BroadcastMessage(
    string methodName,
    [Internal.DefaultValue("null")] object? parameter,
    [Internal.DefaultValue("SendMessageOptions.RequireReceiver")] SendMessageOptions options)
  {
    SendMessage(methodName, parameter, options);
    for (int i = 0; i < transform.childCount; i++)
    {
      var child = transform.GetChild(i);
      child.gameObject?.BroadcastMessage(methodName, parameter, options);
    }
  }

  private static void AddToScene(GameObject go)
  {
    lock (_sceneLock)
    {
      _allObjects.Add(go);
      if (!_sceneObjects.TryGetValue(go.name, out var byName))
      {
        byName = new List<GameObject>();
        _sceneObjects[go.name] = byName;
      }

      if (!byName.Contains(go))
        byName.Add(go);
    }

    if (go._scene != null && go.transform != null && go.transform.parent is null)
      SceneManager.RegisterRootGameObject(go, go._scene);
  }

  internal static GameObject[] GetSceneRootGameObjects()
  {
    var activeScene = SceneManager.GetActiveScene();
    return activeScene.GetRootGameObjects();
  }

  internal static void UnregisterFromScene(GameObject? go)
  {
    if (go is null)
      return;

    lock (_sceneLock)
    {
      _ = _allObjects.Remove(go);
      if (_sceneObjects.TryGetValue(go.name, out var byName))
      {
        byName.Remove(go);
        if (byName.Count == 0)
          _ = _sceneObjects.Remove(go.name);
      }
    }

    if (go._scene.IsValid())
      SceneManager.UnregisterRootGameObject(go, go._scene);
  }

  internal void SetSceneInternal(Scene scene)
  {
    if (_scene == scene) return;
    if (_scene.IsValid()) SceneManager.UnregisterRootGameObject(this, _scene);
    _scene = scene;
    if (transform.parent is null) SceneManager.RegisterRootGameObject(this, scene);
    for (int i = 0; i < transform.childCount; i++)
      transform.GetChild(i).gameObject?.SetSceneInternal(scene);
  }

  private static void CollectChildrenRecursive(Transform root, Action<Transform> onChild)
  {
    for (var i = 0; i < root.childCount; i++)
    {
      var child = root.GetChild(i);
      if (child is null)
      {
        continue;
      }

      onChild(child);
      CollectChildrenRecursive(child, onChild);
    }
  }

  [Bindings.FreeFunction("GameObjectBindings::CreatePrimitive")]
  public static GameObject CreatePrimitive(PrimitiveType type)
  {
    return new GameObject(type.ToString());
  }

}

public enum PrimitiveType
{
  Sphere,
  Capsule,
  Cylinder,
  Cube,
  Plane,
  Quad
}
