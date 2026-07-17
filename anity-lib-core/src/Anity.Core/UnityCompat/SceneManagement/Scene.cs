using System;
using System.Collections.Generic;
using UnityEngine.Bindings;

namespace UnityEngine.SceneManagement;

[Serializable]
[NativeHeader("Runtime/Export/SceneManager/Scene.bindings.h")]
public struct Scene
{
  [SerializeField]
  [HideInInspector]
  private int m_Handle;

  internal Scene(int handle)
  {
    m_Handle = handle;
  }

  public int handle => m_Handle;
  public string path => SceneManager.GetScenePath(m_Handle);

  public string name
  {
    get => SceneManager.GetSceneName(m_Handle);
    set => SceneManager.SetSceneName(m_Handle, value);
  }

  public bool isLoaded => SceneManager.GetSceneIsLoaded(m_Handle);
  public int buildIndex => SceneManager.GetSceneBuildIndex(m_Handle);
  public bool isDirty => SceneManager.GetSceneIsDirty(m_Handle);
  public int rootCount => SceneManager.GetRootCount(m_Handle);

  public bool isSubScene
  {
    get => SceneManager.GetSceneIsSubScene(m_Handle);
    set => SceneManager.SetSceneIsSubScene(m_Handle, value);
  }

  public bool IsValid() => SceneManager.IsValidSceneHandle(m_Handle);

  public GameObject[] GetRootGameObjects()
  {
    var rootGameObjects = new List<GameObject>(rootCount);
    GetRootGameObjects(rootGameObjects);
    return rootGameObjects.ToArray();
  }

  public void GetRootGameObjects(List<GameObject> rootGameObjects)
  {
    if (rootGameObjects.Capacity < rootCount)
      rootGameObjects.Capacity = rootCount;

    rootGameObjects.Clear();
    if (!IsValid())
      throw new ArgumentException("The scene is invalid.");
    if (!Application.isPlaying && !isLoaded)
      throw new ArgumentException("The scene is not loaded.");
    if (rootCount == 0)
      return;

    SceneManager.CopyRootGameObjects(m_Handle, rootGameObjects);
  }

  public static bool operator ==(Scene lhs, Scene rhs) => lhs.handle == rhs.handle;
  public static bool operator !=(Scene lhs, Scene rhs) => lhs.handle != rhs.handle;

  public override int GetHashCode() => m_Handle.GetHashCode();

  public override bool Equals(object other)
    => other is Scene scene && handle == scene.handle;
}
