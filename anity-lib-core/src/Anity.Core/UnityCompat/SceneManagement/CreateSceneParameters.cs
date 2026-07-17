namespace UnityEngine.SceneManagement;

[System.Serializable]
public struct CreateSceneParameters
{
  public LocalPhysicsMode localPhysicsMode { get; set; }

  public CreateSceneParameters(LocalPhysicsMode physicsMode)
  {
    localPhysicsMode = physicsMode;
  }
}
