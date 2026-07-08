using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Component : Object
{
  public GameObject? gameObject { get; internal set; }
  public Transform? transform => gameObject?.transform;

  public T? GetComponent<T>() where T : Component
  {
    return gameObject?.GetComponent<T>();
  }

  public Component? GetComponent(Type componentType)
  {
    return gameObject?.GetComponent(componentType);
  }

  public T[] GetComponents<T>() where T : Component
  {
    return gameObject?.GetComponents<T>() ?? Array.Empty<T>();
  }

  public Component[] GetComponents(Type componentType)
  {
    return gameObject?.GetComponents(componentType) ?? Array.Empty<Component>();
  }

  public Component[] GetComponentsInChildren(Type componentType, bool includeInactive = true)
  {
    return gameObject?.GetComponentsInChildren(componentType, includeInactive) ?? Array.Empty<Component>();
  }

  public T[] GetComponentsInChildren<T>(bool includeInactive = true) where T : Component
  {
    return gameObject?.GetComponentsInChildren<T>(includeInactive) ?? Array.Empty<T>();
  }

  public T[] GetComponentsInParent<T>(bool includeInactive = true) where T : Component
  {
    return gameObject?.GetComponentsInParent<T>(includeInactive) ?? Array.Empty<T>();
  }

  public Component? GetComponentInParent(Type componentType)
  {
    return gameObject?.GetComponentInParent(componentType);
  }

  public T? GetComponentInParent<T>() where T : Component
  {
    return gameObject?.GetComponentInParent<T>();
  }

  public bool TryGetComponent<T>(out T? component) where T : Component
  {
    component = GetComponent<T>();
    return component is not null;
  }

  public bool CompareTag(string tag)
  {
    return gameObject is not null && string.Equals(gameObject.tag, tag, StringComparison.Ordinal);
  }

  public void SendMessage(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    _ = methodName;
    _ = value;
    _ = options;
  }

  public void SendMessageUpwards(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    SendMessage(methodName, value, options);
  }

  public void BroadcastMessage(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    SendMessage(methodName, value, options);
  }

  public bool IsActive()
  {
    return gameObject is not null && gameObject.activeInHierarchy;
  }
}

public enum SendMessageOptions
{
  RequireReceiver,
  DontRequireReceiver
}
