namespace UnityEngine.SceneManagement;

[System.Serializable]
public struct LoadSceneParameters
{
  public LoadSceneMode loadSceneMode { get; set; }
  public LocalPhysicsMode localPhysicsMode { get; set; }

  public LoadSceneParameters(LoadSceneMode mode)
  {
    loadSceneMode = mode;
    localPhysicsMode = LocalPhysicsMode.None;
  }

  public LoadSceneParameters(LoadSceneMode mode, LocalPhysicsMode physicsMode)
  {
    loadSceneMode = mode;
    localPhysicsMode = physicsMode;
  }
}
