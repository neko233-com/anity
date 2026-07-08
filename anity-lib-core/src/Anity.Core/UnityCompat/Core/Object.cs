namespace UnityEngine;

public class Object
{
  private bool _destroyed;
  private string? _name;

  public int GetInstanceID() => GetHashCode();

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

  private static void MarkDestroyed(Object? obj)
  {
    if (obj is null)
    {
      return;
    }

    obj._destroyed = true;
    if (obj is GameObject gameObject)
    {
      GameObject.UnregisterFromScene(gameObject);
    }
  }

  public static Object Instantiate(Object original)
  {
    return original;
  }

  public bool IsDestroyed => _destroyed;

  public string name
  {
    get => _name ?? string.Empty;
    set => _name = value;
  }
}
