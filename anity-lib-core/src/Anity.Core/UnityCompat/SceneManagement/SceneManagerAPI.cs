using System;
using System.Collections.Generic;

namespace UnityEngine.SceneManagement;

public enum UnloadSceneOptions
{
  None = 0,
  UnloadAllEmbeddedSceneObjects = 1
}

public static class SceneManagerAPI
{
  private static readonly List<Scene> _scenes = new();

  public static event Action<Scene, LoadSceneMode>? sceneLoaded;
  public static event Action<Scene>? sceneUnloaded;
  public static event Action<Scene, Scene>? activeSceneChanged;

  internal static void Internal_SceneLoaded(Scene scene, LoadSceneMode mode)
  {
    sceneLoaded?.Invoke(scene, mode);
  }

  internal static void Internal_SceneUnloaded(Scene scene)
  {
    sceneUnloaded?.Invoke(scene);
  }

  internal static void Internal_ActiveSceneChanged(Scene previous, Scene current)
  {
    activeSceneChanged?.Invoke(previous, current);
  }
}
