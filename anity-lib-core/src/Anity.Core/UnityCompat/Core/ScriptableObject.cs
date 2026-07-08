namespace UnityEngine;

public abstract class ScriptableObject : Object
{
  public static T CreateInstance<T>() where T : ScriptableObject, new()
  {
    return new T();
  }

  public static ScriptableObject CreateInstance(System.Type type)
  {
    if (!typeof(ScriptableObject).IsAssignableFrom(type))
    {
      throw new System.ArgumentException($"Type '{type}' is not a ScriptableObject.");
    }
    return (ScriptableObject)System.Activator.CreateInstance(type)!;
  }
}
