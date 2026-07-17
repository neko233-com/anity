using UnityEngine.SceneManagement;

namespace UnityEditor.SceneManagement;

public static class EditorSceneManager
{
  public static Scene GetActiveScene()
  {
    return SceneManager.GetActiveScene();
  }

  public static Scene OpenScene(string scenePath, OpenSceneMode mode = OpenSceneMode.Single)
  {
    SceneManager.LoadScene(scenePath, ToRuntimeMode(mode));
    return SceneManager.GetActiveScene();
  }

  public static Scene OpenScene(string path, OpenSceneMode mode, bool outcastsInScene)
  {
    _ = outcastsInScene;
    return OpenScene(path, mode);
  }

  public static bool SaveScene(Scene scene)
  {
    SceneManager.SetSceneIsDirty(scene.handle, false);
    return true;
  }

  public static bool SaveScene(Scene scene, string path)
  {
    SceneManager.SetScenePath(scene.handle, path);
    SceneManager.SetSceneIsDirty(scene.handle, false);
    return true;
  }

  public static bool SaveScene(Scene scene, string path, bool saveAsCopy)
  {
    _ = saveAsCopy;
    return SaveScene(scene, path);
  }

  public static bool CloseScene(Scene scene, bool removeScene)
  {
    if (removeScene)
    {
      SceneManager.UnloadSceneAsync(scene);
    }
    return true;
  }

  public static bool CloseAllScenes()
  {
    return true;
  }

  public static bool SaveCurrentModifiedScenesIfUserWantsTo()
  {
    return true;
  }

  public static void MarkSceneDirty(Scene scene)
  {
    SceneManager.SetSceneIsDirty(scene.handle, true);
  }

  public static bool SaveOpenScenes()
  {
    return true;
  }

  public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode)
  {
    _ = setup;
    _ = mode;
    return SceneManager.CreateScene("NewScene");
  }

  public static Scene NewScene()
  {
    return NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
  }

  public static Scene NewScene(NewSceneSetup setup)
  {
    return NewScene(setup, NewSceneMode.Single);
  }

  public static Scene LoadSceneInPlayMode(string scenePath, LoadSceneParameters parameters)
  {
    SceneManager.LoadScene(scenePath, parameters.loadSceneMode);
    return SceneManager.GetActiveScene();
  }

  /// <summary>Create an isolated preview scene for Prefab Mode / preview rendering.</summary>
  public static Scene NewPreviewScene()
  {
    return SceneManager.CreateScene("PrefabMode_Preview");
  }

  public static bool ClosePreviewScene(Scene scene)
  {
    if (!scene.IsValid()) return false;
    return CloseScene(scene, true);
  }

  private static LoadSceneMode ToRuntimeMode(OpenSceneMode mode)
  {
    return mode == OpenSceneMode.Additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
  }
}

public enum NewSceneSetup
{
  EmptyScene,
  DefaultGameObjects
}

public enum NewSceneMode
{
  Single,
  Additive
}

public enum OpenSceneMode
{
  Single,
  Additive,
  AdditiveWithoutLoading
}
