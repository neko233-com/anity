using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Component : Object
{
  public GameObject? gameObject { get; internal set; }
  public Transform? transform => gameObject?.transform;

  public string tag
  {
    get => gameObject?.tag ?? "Untagged";
    set
    {
      if (gameObject is not null) gameObject.tag = value;
    }
  }

  public T? GetComponent<T>() where T : class
  {
    return gameObject?.GetComponent<T>();
  }

  public Component? GetComponent(Type componentType)
  {
    return gameObject?.GetComponent(componentType);
  }

  public T[] GetComponents<T>() where T : class
  {
    return gameObject?.GetComponents<T>() ?? Array.Empty<T>();
  }

  public Component[] GetComponents(Type componentType)
  {
    return gameObject?.GetComponents(componentType) ?? Array.Empty<Component>();
  }

  public void GetComponents<T>(List<T> results) where T : class
  {
    if (gameObject is not null)
    {
      gameObject.GetComponents(results);
    }
    else
    {
      results.Clear();
    }
  }

  public Component[] GetComponentsInChildren(Type componentType, bool includeInactive = true)
  {
    return gameObject?.GetComponentsInChildren(componentType, includeInactive) ?? Array.Empty<Component>();
  }

  public T[] GetComponentsInChildren<T>(bool includeInactive = true) where T : class
  {
    return gameObject?.GetComponentsInChildren<T>(includeInactive) ?? Array.Empty<T>();
  }

  public T[] GetComponentsInParent<T>(bool includeInactive = true) where T : class
  {
    return gameObject?.GetComponentsInParent<T>(includeInactive) ?? Array.Empty<T>();
  }

  public Component? GetComponentInParent(Type componentType)
  {
    return gameObject?.GetComponentInParent(componentType);
  }

  public T? GetComponentInParent<T>() where T : class
  {
    return gameObject?.GetComponentInParent<T>();
  }

  public Component? GetComponentInChildren(Type componentType, bool includeInactive = true)
  {
    return gameObject?.GetComponentInChildren(componentType, includeInactive);
  }

  public T? GetComponentInChildren<T>(bool includeInactive = true) where T : class
  {
    return gameObject?.GetComponentInChildren<T>(includeInactive);
  }

  public bool TryGetComponent<T>(out T? component) where T : class
  {
    if (gameObject is not null)
    {
      return gameObject.TryGetComponent(out component);
    }
    component = default;
    return false;
  }

  public bool CompareTag(string tag)
  {
    return gameObject is not null && string.Equals(gameObject.tag, tag, StringComparison.Ordinal);
  }

  public void SendMessage(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    gameObject?.SendMessage(methodName, value, options);
  }

  public void SendMessageUpwards(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    gameObject?.SendMessageUpwards(methodName, value, options);
  }

  public void BroadcastMessage(string methodName, object? value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
  {
    gameObject?.BroadcastMessage(methodName, value, options);
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
