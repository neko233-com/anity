using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.SceneManagement;

public class Scene : IEquatable<Scene>
{
  internal int _handle;
  internal List<GameObject> _rootObjects = new();
  internal bool _isLoaded;

  public int handle => _handle;
  public string name { get; internal set; }
  public int buildIndex { get; internal set; }
  public bool isLoaded => _isLoaded;
  public bool isSubScene { get; internal set; }
  public bool isDirty { get; internal set; }
  public string path { get; internal set; }
  public bool isValid => IsValid();
  public int rootCount => _rootObjects?.Count ?? 0;

  internal Scene(int handle, string name, int buildIndex, bool isLoaded = true, bool isSubScene = false, string path = "")
  {
    if (handle < 0)
    {
      handle = 0;
    }

    _handle = handle;
    this.name = name ?? string.Empty;
    this.buildIndex = buildIndex;
    _isLoaded = isLoaded;
    this.isSubScene = isSubScene;
    this.isDirty = false;
    this.path = path ?? string.Empty;
    _rootObjects = new List<GameObject>();
  }

  public Scene(string name, int buildIndex = 0, bool isLoaded = true)
    : this(SceneManager.CreateHandleForUntrackedScene(name, buildIndex), name, buildIndex, isLoaded, false)
  {
  }

  public bool IsValid()
  {
    return _handle != 0;
  }

  public GameObject[] GetRootGameObjects()
  {
    return _rootObjects?.Where(go => go != null && !go.IsDestroyed).ToArray() ?? Array.Empty<GameObject>();
  }

  public GameObject? GetGameObject(string name)
  {
    foreach (var go in _rootObjects)
    {
      if (go != null && !go.IsDestroyed && go.name == name)
        return go;
    }
    return null;
  }

  public override int GetHashCode()
  {
    return _handle.GetHashCode();
  }

  public override bool Equals(object? obj)
  {
    return obj is Scene other && Equals(other);
  }

  public bool Equals(Scene? other)
  {
    if (other is null) return false;
    return _handle == other._handle;
  }

  public static bool operator ==(Scene? left, Scene? right)
  {
    if (left is null && right is null) return true;
    if (left is null || right is null) return false;
    return left._handle == right._handle;
  }

  public static bool operator !=(Scene? left, Scene? right)
  {
    return !(left == right);
  }

  public static readonly Scene Invalid = new(0, string.Empty, -1, false);
}
