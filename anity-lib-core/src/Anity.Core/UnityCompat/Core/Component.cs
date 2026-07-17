using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Export/Scripting/Component.bindings.h")]
[NativeClass("Unity::Component")]
[Scripting.RequiredByNativeCode]
public class Component : Object
{
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

  // Unity components expose these as non-null attached-object properties. Keep
  // the runtime-compatible null value for invalid direct construction while
  // presenting the same non-null contract to normal AddComponent callers.
  public GameObject gameObject { get; internal set; } = null!;
  public Transform transform => (gameObject?.transform)!;

  public string tag
  {
    get => gameObject?.tag ?? "Untagged";
    set
    {
      if (gameObject is not null) gameObject.tag = value;
    }
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponent(Type type) => gameObject?.GetComponent(type);

  [System.Security.SecuritySafeCritical]
  public T GetComponent<T>() => gameObject is null ? default! : gameObject.GetComponent<T>();

  [Bindings.FreeFunction(HasExplicitThis = true)]
  public Component? GetComponent(string type) => gameObject?.GetComponentByName(type);

  public Component[] GetComponents(Type type)
    => gameObject?.GetComponents(type) ?? Array.Empty<Component>();

  public void GetComponents(Type type, List<Component> results)
  {
    if (gameObject is null)
    {
      if (results is null) throw new ArgumentNullException(nameof(results));
      results.Clear();
      return;
    }
    gameObject.GetComponents(type, results);
  }

  public T[] GetComponents<T>() => gameObject?.GetComponents<T>() ?? Array.Empty<T>();

  public void GetComponents<T>(List<T> results)
  {
    if (gameObject is null)
    {
      if (results is null) throw new ArgumentNullException(nameof(results));
      results.Clear();
      return;
    }
    gameObject.GetComponents(results);
  }

  [Internal.ExcludeFromDocs]
  public Component[] GetComponentsInChildren(Type t)
    => GetComponentsInChildren(t, false);

  public Component[] GetComponentsInChildren(Type t, bool includeInactive)
    => gameObject?.GetComponentsInChildren(t, includeInactive) ?? Array.Empty<Component>();

  public T[] GetComponentsInChildren<T>() => GetComponentsInChildren<T>(false);

  public T[] GetComponentsInChildren<T>(bool includeInactive)
    => gameObject?.GetComponentsInChildren<T>(includeInactive) ?? Array.Empty<T>();

  public void GetComponentsInChildren<T>(List<T> results)
    => GetComponentsInChildren(false, results);

  public void GetComponentsInChildren<T>(bool includeInactive, List<T> result)
  {
    if (gameObject is null)
    {
      if (result is null) throw new ArgumentNullException(nameof(result));
      result.Clear();
      return;
    }
    gameObject.GetComponentsInChildren(includeInactive, result);
  }

  [Internal.ExcludeFromDocs]
  public Component[] GetComponentsInParent(Type t) => GetComponentsInParent(t, false);

  public Component[] GetComponentsInParent(
    Type t,
    [Internal.DefaultValue("false")] bool includeInactive)
    => gameObject?.GetComponentsInParent(t, includeInactive) ?? Array.Empty<Component>();

  public T[] GetComponentsInParent<T>() => GetComponentsInParent<T>(false);

  public T[] GetComponentsInParent<T>(bool includeInactive)
    => gameObject?.GetComponentsInParent<T>(includeInactive) ?? Array.Empty<T>();

  public void GetComponentsInParent<T>(bool includeInactive, List<T> results)
  {
    if (gameObject is null)
    {
      if (results is null) throw new ArgumentNullException(nameof(results));
      results.Clear();
      return;
    }
    gameObject.GetComponentsInParent(includeInactive, results);
  }

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInChildren(Type t) => GetComponentInChildren(t, false);

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInChildren(Type t, bool includeInactive)
    => gameObject?.GetComponentInChildren(t, includeInactive);

  [Internal.ExcludeFromDocs]
  public T GetComponentInChildren<T>() => GetComponentInChildren<T>(false);

  public T GetComponentInChildren<T>([Internal.DefaultValue("false")] bool includeInactive)
    => gameObject is null ? default! : gameObject.GetComponentInChildren<T>(includeInactive);

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInParent(Type t)
    => gameObject?.GetComponentInParent(t, false);

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public Component? GetComponentInParent(Type t, bool includeInactive)
    => gameObject?.GetComponentInParent(t, includeInactive);

  public T GetComponentInParent<T>()
    => gameObject is null ? default! : gameObject.GetComponentInParent<T>(false);

  public T GetComponentInParent<T>([Internal.DefaultValue("false")] bool includeInactive)
    => gameObject is null ? default! : gameObject.GetComponentInParent<T>(includeInactive);

  public int GetComponentIndex() => gameObject?.GetComponentIndex(this) ?? -1;

  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
  public bool TryGetComponent(Type type, out Component? component)
  {
    if (gameObject is not null) return gameObject.TryGetComponent(type, out component);
    component = null;
    return false;
  }

  [System.Security.SecuritySafeCritical]
  public bool TryGetComponent<T>(out T component)
  {
    if (gameObject is not null) return gameObject.TryGetComponent(out component);
    component = default!;
    return false;
  }

  public bool CompareTag(string tag)
    => gameObject is not null && gameObject.CompareTag(tag);

  public void SendMessage(string methodName)
    => SendMessage(methodName, null, SendMessageOptions.RequireReceiver);

  public void SendMessage(string methodName, object? value)
    => SendMessage(methodName, value, SendMessageOptions.RequireReceiver);

  public void SendMessage(string methodName, SendMessageOptions options)
    => SendMessage(methodName, null, options);

  [Bindings.FreeFunction("SendMessage", HasExplicitThis = true)]
  public void SendMessage(string methodName, object? value, SendMessageOptions options)
    => gameObject?.SendMessage(methodName, value, options);

  [Internal.ExcludeFromDocs]
  public void SendMessageUpwards(string methodName)
    => SendMessageUpwards(methodName, null, SendMessageOptions.RequireReceiver);

  [Internal.ExcludeFromDocs]
  public void SendMessageUpwards(string methodName, object? value)
    => SendMessageUpwards(methodName, value, SendMessageOptions.RequireReceiver);

  public void SendMessageUpwards(string methodName, SendMessageOptions options)
    => SendMessageUpwards(methodName, null, options);

  [Bindings.FreeFunction(HasExplicitThis = true)]
  public void SendMessageUpwards(
    string methodName,
    [Internal.DefaultValue("null")] object? value,
    [Internal.DefaultValue("SendMessageOptions.RequireReceiver")] SendMessageOptions options)
    => gameObject?.SendMessageUpwards(methodName, value, options);

  [Internal.ExcludeFromDocs]
  public void BroadcastMessage(string methodName)
    => BroadcastMessage(methodName, null, SendMessageOptions.RequireReceiver);

  [Internal.ExcludeFromDocs]
  public void BroadcastMessage(string methodName, object? parameter)
    => BroadcastMessage(methodName, parameter, SendMessageOptions.RequireReceiver);

  public void BroadcastMessage(string methodName, SendMessageOptions options)
    => BroadcastMessage(methodName, null, options);

  [Bindings.FreeFunction("BroadcastMessage", HasExplicitThis = true)]
  public void BroadcastMessage(
    string methodName,
    [Internal.DefaultValue("null")] object? parameter,
    [Internal.DefaultValue("SendMessageOptions.RequireReceiver")] SendMessageOptions options)
    => gameObject?.BroadcastMessage(methodName, parameter, options);

  internal bool IsActive() => gameObject is not null && gameObject.activeInHierarchy;

  private static NotSupportedException DeprecatedProperty(string name)
    => new($"{name} property has been deprecated");
}

public enum SendMessageOptions
{
  RequireReceiver,
  DontRequireReceiver
}
