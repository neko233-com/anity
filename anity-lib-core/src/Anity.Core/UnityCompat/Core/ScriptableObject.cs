using System;

namespace UnityEngine;

public abstract class ScriptableObject : Object
{
  public static T CreateInstance<T>() where T : ScriptableObject, new()
  {
    var instance = new T();
    instance.name = typeof(T).Name;
    return instance;
  }

  public static ScriptableObject CreateInstance(Type type)
  {
    if (!typeof(ScriptableObject).IsAssignableFrom(type))
    {
      throw new ArgumentException($"Type '{type}' is not a ScriptableObject.");
    }
    var instance = (ScriptableObject)Activator.CreateInstance(type)!;
    instance.name = type.Name;
    return instance;
  }

  public static ScriptableObject CreateInstance(string className)
  {
    var type = Type.GetType(className);
    if (type is null)
    {
      throw new ArgumentException($"Type '{className}' not found.");
    }
    return CreateInstance(type);
  }

  public static T[] FindObjectsOfType<T>() where T : ScriptableObject
  {
    return Array.Empty<T>();
  }

  public static T? FindObjectOfType<T>() where T : ScriptableObject
  {
    return default;
  }

  protected virtual void OnEnable()
  {
  }

  protected virtual void OnDisable()
  {
  }

  protected virtual void OnDestroy()
  {
  }

  protected virtual void OnValidate()
  {
  }

  protected virtual void OnLoaded()
  {
  }

  protected virtual void OnBeforeSerialize()
  {
  }

  protected virtual void OnAfterDeserialize()
  {
  }

  public ScriptableObject GetCopy()
  {
    var copy = (ScriptableObject)MemberwiseClone();
    copy.name = name + " (Copy)";
    return copy;
  }

  public void SetDirty()
  {
  }

  public void Save()
  {
  }

  public static ScriptableObject[] LoadAll(string type)
  {
    _ = type;
    return Array.Empty<ScriptableObject>();
  }

  public static ScriptableObject? Load(string type, string name)
  {
    _ = type;
    _ = name;
    return null;
  }

  public static T? Load<T>(string name) where T : ScriptableObject
  {
    _ = name;
    return default;
  }
}
