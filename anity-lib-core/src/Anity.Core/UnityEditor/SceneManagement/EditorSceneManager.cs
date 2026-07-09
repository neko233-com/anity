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
    return true;
  }

  public static bool SaveScene(Scene scene, string path)
  {
    return true;
  }

  public static bool SaveScene(Scene scene, string path, bool saveAsCopy)
  {
    return true;
  }

  public static bool CloseScene(Scene scene, bool removeScene)
  {
    return true;
  }

  public static bool CloseAllScenes()
  {
    return true;
  }

  public static void SaveCurrentModifiedScenesIfUserWantsTo()
  {
    // shell no-op
  }

  public static void MarkSceneDirty(Scene scene)
  {
    _ = scene;
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
    SceneManager.LoadScene(scenePath, ToRuntimeMode(parameters.loadSceneMode));
    return SceneManager.GetActiveScene();
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

public readonly struct LoadSceneParameters
{
  public readonly OpenSceneMode loadSceneMode;
  public readonly bool loadMode;

  public LoadSceneParameters(OpenSceneMode mode)
  {
    loadSceneMode = mode;
    loadMode = true;
  }
}
