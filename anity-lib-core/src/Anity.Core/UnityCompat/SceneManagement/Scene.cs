using UnityEngine;
using System;

namespace UnityEngine.SceneManagement;

public readonly struct Scene
{
  private readonly int _handle;

  public int handle => _handle;
  public string name { get; }
  public int buildIndex { get; }
  public bool isLoaded { get; }
  public bool isSubScene { get; }
  public string path { get; }
  public bool isValid => IsValid();

  internal Scene(int handle, string name, int buildIndex, bool isLoaded = true, bool isSubScene = false, string path = "")
  {
    if (handle < 0)
    {
      handle = 0;
    }

    _handle = handle;
    this.name = name ?? string.Empty;
    this.buildIndex = buildIndex;
    this.isLoaded = isLoaded;
    this.isSubScene = isSubScene;
    this.path = path ?? string.Empty;
  }

  public Scene(string name, int buildIndex = 0, bool isLoaded = true)
    : this(SceneManager.CreateHandleForUntrackedScene(name, buildIndex), name, buildIndex, isLoaded, false)
  {
  }

  public bool IsValid()
  {
    return _handle != 0;
  }

  public int rootCount => IsValid() ? SceneManager.GetRootCount(_handle) : 0;

  public GameObject[] GetRootGameObjects()
  {
    return IsValid() ? SceneManager.GetRootGameObjects(_handle) : Array.Empty<GameObject>();
  }

  public override int GetHashCode()
  {
    return _handle.GetHashCode();
  }

  public override bool Equals(object? obj)
  {
    return obj is Scene other && _handle == other._handle;
  }

  public static bool operator ==(Scene left, Scene right)
  {
    return left.Equals(right);
  }

  public static bool operator !=(Scene left, Scene right)
  {
    return !left.Equals(right);
  }

  public static readonly Scene Invalid = new(0, string.Empty, -1, false);
}
