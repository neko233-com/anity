using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace UnityEngine;

public class GameObject : Object
{
  private static readonly Dictionary<string, List<GameObject>> _sceneObjects = new(StringComparer.Ordinal);
  private static readonly List<GameObject> _allObjects = new();
  private readonly List<Component> _components = new();
  private Scene _scene;
  private bool _isActiveInHierarchyPrev;

  public GameObject(string name = "GameObject")
  {
    this.name = name;
    transform = new Transform { gameObject = this };
    _components.Add(transform);
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

  public Transform transform { get; }
  public bool activeSelf { get; private set; } = true;

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

  public SceneManagement.Scene scene => _scene ?? SceneManagement.Scene.Invalid;

  public static GameObject? Find(string name)
  {
    return _sceneObjects.TryGetValue(name, out var list)
      ? list.FirstOrDefault(go => !go.IsDestroyed)
      : null;
  }

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

  public static GameObject[] FindObjectsOfType(Type componentType)
  {
    if (componentType is null)
    {
      return Array.Empty<GameObject>();
    }

    return _allObjects
      .Where(go => !go.IsDestroyed && go.GetComponent(componentType) is not null)
      .ToArray();
  }

  public static T[] FindObjectsOfType<T>() where T : class
  {
    var result = new List<T>();
    foreach (var go in _allObjects.Where(go => !go.IsDestroyed))
    {
      var component = go.GetComponent<T>();
      if (component is not null)
      {
        result.Add(component);
      }
    }

    return result.ToArray();
  }

  public T AddComponent<T>() where T : Component, new()
  {
    return (T)AddComponent(typeof(T));
  }

  public Component AddComponent(Type componentType)
  {
    if (componentType is null || !typeof(Component).IsAssignableFrom(componentType))
    {
      throw new ArgumentException("componentType must inherit from Component", nameof(componentType));
    }

    if (componentType.IsAbstract)
    {
      throw new InvalidOperationException("Cannot add abstract component.");
    }

    var component = Activator.CreateInstance(componentType) as Component
      ?? throw new InvalidOperationException($"Failed to create {componentType.Name}");

    return RegisterComponent(component);
  }

  internal Component RegisterComponent(Component component)
  {
    component.gameObject = this;
    _components.Add(component);

    if (component is MonoBehaviour mb)
    {
      try { mb.InternalAwake(); } catch { }
      if (activeInHierarchy && mb.enabled)
      {
        try { mb.InternalOnEnable(); } catch { }
      }
    }

    return component;
  }

  internal void RemoveComponentInternal(Component component)
  {
    _components.Remove(component);
  }

  public Component? GetComponent(Type componentType)
  {
    if (componentType is null)
    {
      return null;
    }

    foreach (var component in _components)
    {
      if (component is not null && componentType.IsInstanceOfType(component))
      {
        return component;
      }
    }

    return null;
  }

  public T? GetComponent<T>() where T : class
  {
    return GetComponent(typeof(T)) as T;
  }

  public bool TryGetComponent<T>(out T? component) where T : class
  {
    component = GetComponent<T>();
    return component is not null;
  }

  public Component[] GetComponents(Type componentType)
  {
    if (componentType is null)
    {
      return Array.Empty<Component>();
    }

    return _components
      .Where(component => component is not null && componentType.IsInstanceOfType(component))
      .ToArray();
  }

  public T[] GetComponents<T>() where T : class
  {
    return GetComponents(typeof(T)).OfType<T>().ToArray();
  }

  public void GetComponents<T>(List<T> results) where T : class
  {
    if (results is null) return;
    results.Clear();
    foreach (var component in _components)
    {
      if (component is T t)
      {
        results.Add(t);
      }
    }
  }

  public Component[] GetComponentsInChildren(Type componentType, bool includeInactive = true)
  {
    if (componentType is null)
    {
      return Array.Empty<Component>();
    }

    if (!includeInactive && !activeSelf)
    {
      return Array.Empty<Component>();
    }

    var components = new List<Component>(GetComponents(componentType));
    CollectChildrenRecursive(transform, child =>
    {
      if (!includeInactive && !(child.gameObject?.activeSelf ?? false))
      {
        return;
      }

      if (child.gameObject is not null)
      {
        components.AddRange(child.gameObject.GetComponents(componentType));
      }
    });

    return components.ToArray();
  }

  public T[] GetComponentsInChildren<T>(bool includeInactive = true) where T : class
  {
    return GetComponentsInChildren(typeof(T), includeInactive).OfType<T>().ToArray();
  }

  public Component[] GetComponentsInParent(Type componentType, bool includeInactive = true)
  {
    if (componentType is null)
    {
      return Array.Empty<Component>();
    }

    if (!includeInactive && !activeSelf)
    {
      return Array.Empty<Component>();
    }

    var components = new List<Component>(GetComponents(componentType));
    for (var parent = transform.parent; parent is not null; parent = parent.parent)
    {
      if (!includeInactive && !(parent.gameObject?.activeSelf ?? false))
      {
        continue;
      }

      if (parent.gameObject is not null)
      {
        components.AddRange(parent.gameObject.GetComponents(componentType));
      }
    }

    return components.ToArray();
  }

  public T[] GetComponentsInParent<T>(bool includeInactive = true) where T : class
  {
    return GetComponentsInParent(typeof(T), includeInactive).OfType<T>().ToArray();
  }

  public Component? GetComponentInChildren(Type componentType, bool includeInactive = true)
  {
    return GetComponentsInChildren(componentType, includeInactive).FirstOrDefault();
  }

  public T? GetComponentInChildren<T>(bool includeInactive = true) where T : class
  {
    return GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
  }

  public Component? GetComponentInParent(Type componentType)
  {
    return GetComponentsInParent(componentType, true).FirstOrDefault();
  }

  public T? GetComponentInParent<T>() where T : class
  {
    return GetComponentInParent(typeof(T)) as T;
  }

  public void SetActive(bool value)
  {
    if (activeSelf == value) return;
    activeSelf = value;

    bool wasActive = _isActiveInHierarchyPrev;
    bool nowActive = activeInHierarchy;
    _isActiveInHierarchyPrev = nowActive;

    if (wasActive != nowActive)
    {
      SetComponentsActiveState(this, nowActive);
      for (int i = 0; i < transform.childCount; i++)
      {
        var child = transform.GetChild(i);
        if (child.gameObject is not null && child.gameObject.activeSelf)
        {
          child.gameObject.SetChildrenActiveState(child.gameObject, nowActive);
        }
      }
    }
  }

  private void SetChildrenActiveState(GameObject go, bool rootActive)
  {
    bool nowActive = rootActive && go.activeSelf;
    SetComponentsActiveState(go, nowActive);
    for (int i = 0; i < go.transform.childCount; i++)
    {
      var child = go.transform.GetChild(i);
      if (child.gameObject is not null && child.gameObject.activeSelf)
      {
        SetChildrenActiveState(child.gameObject, nowActive);
      }
    }
  }

  private void SetComponentsActiveState(GameObject go, bool isActive)
  {
    foreach (var comp in go._components)
    {
      if (comp is Behaviour behaviour && behaviour.enabled)
      {
        if (comp is MonoBehaviour mb)
        {
          try
          {
            if (isActive) mb.InternalOnEnable();
            else mb.InternalOnDisable();
          }
          catch { }
        }
      }
    }
  }

  public void SendMessage(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
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

  public void SendMessageUpwards(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    SendMessage(methodName, value, options);
    if (transform.parent is not null && transform.parent.gameObject is not null)
    {
      transform.parent.gameObject.SendMessageUpwards(methodName, value, options);
    }
  }

  public void BroadcastMessage(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    SendMessage(methodName, value, options);
    for (int i = 0; i < transform.childCount; i++)
    {
      var child = transform.GetChild(i);
      child.gameObject?.BroadcastMessage(methodName, value, options);
    }
  }

  private static void AddToScene(GameObject go)
  {
    _allObjects.Add(go);
    if (!_sceneObjects.TryGetValue(go.name, out var byName))
    {
      byName = new List<GameObject>();
      _sceneObjects[go.name] = byName;
    }

    if (!byName.Contains(go))
    {
      byName.Add(go);
    }

    if (go._scene != null && go.transform != null && go.transform.parent is null)
    {
      SceneManager.RegisterRootGameObject(go, go._scene);
    }
  }

  internal static GameObject[] GetSceneRootGameObjects()
  {
    var activeScene = SceneManager.GetActiveScene();
    return activeScene.GetRootGameObjects();
  }

  internal static void UnregisterFromScene(GameObject? go)
  {
    if (go is null)
    {
      return;
    }

    _ = _allObjects.Remove(go);

    if (go._scene != null)
    {
      SceneManager.UnregisterRootGameObject(go, go._scene);
    }

    if (_sceneObjects.TryGetValue(go.name, out var byName))
    {
      byName.Remove(go);
      if (byName.Count == 0)
      {
        _ = _sceneObjects.Remove(go.name);
      }
    }
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

  public static GameObject CreatePrimitive(PrimitiveType type)
  {
    return new GameObject(type.ToString());
  }

  public override string ToString()
  {
    return name;
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
