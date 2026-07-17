using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine.Bindings;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace UnityEngine.SceneManagement;

[NativeHeader("Runtime/Export/SceneManager/SceneManager.bindings.h")]
[RequiredByNativeCode]
public class SceneManager
{
  private const string DefaultSceneName = "SampleScene";
  private const string ObsoleteUnloadMessage = "Use SceneManager.UnloadSceneAsync. This function is not safe to use during triggers and under other circumstances. See Scripting reference for more details.";

  private sealed class SceneState
  {
    internal int Handle;
    internal string Name = string.Empty;
    internal int BuildIndex;
    internal bool IsLoaded;
    internal bool IsSubScene;
    internal bool IsDirty;
    internal string Path = string.Empty;
    internal LocalPhysicsMode LocalPhysicsMode;
    internal readonly List<GameObject> RootObjects = new();
  }

  private static readonly object s_Gate = new();
  private static readonly Dictionary<int, SceneState> s_Scenes = new();
  private static readonly List<int> s_SceneOrder = new();
  private static int s_NextHandle = 1;
  private static int s_ActiveHandle;

  public static event UnityAction<Scene, LoadSceneMode> sceneLoaded;
  public static event UnityAction<Scene> sceneUnloaded;
  public static event UnityAction<Scene, Scene> activeSceneChanged;

  static SceneManager()
  {
    Scene defaultScene = CreateSceneInternal(DefaultSceneName, 0, true, LocalPhysicsMode.None, string.Empty);
    s_ActiveHandle = defaultScene.handle;
  }

  public static int sceneCount
  {
    get
    {
      lock (s_Gate)
        return s_SceneOrder.Count(handle => s_Scenes.TryGetValue(handle, out SceneState state) && state.IsLoaded);
    }
  }

  public static int sceneCountInBuildSettings
  {
    get
    {
      try
      {
        return UnityEditor.EditorBuildSettings.scenes.Length;
      }
      catch
      {
        lock (s_Gate)
          return s_Scenes.Values.Count(state => state.BuildIndex >= 0);
      }
    }
  }

  public static int loadedSceneCount => sceneCount;

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  public static Scene GetActiveScene() => GetSceneByHandle(s_ActiveHandle);

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  [NativeThrows]
  public static bool SetActiveScene(Scene scene)
  {
    if (!TryGetState(scene.handle, out SceneState state) || !state.IsLoaded)
      return false;

    Scene previous = GetActiveScene();
    s_ActiveHandle = scene.handle;
    if (previous != scene)
      activeSceneChanged?.Invoke(previous, scene);
    return true;
  }

  public static Scene CreateScene(string sceneName)
    => CreateScene(sceneName, new CreateSceneParameters(LocalPhysicsMode.None));

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  [NativeThrows]
  public static Scene CreateScene([NotNull("ArgumentNullException")] string sceneName, CreateSceneParameters parameters)
  {
    if (sceneName is null)
      throw new ArgumentNullException(nameof(sceneName));
    if (sceneName.Length == 0)
      throw new ArgumentException("Scene name cannot be empty.", nameof(sceneName));

    Scene scene = CreateSceneInternal(sceneName, -1, true, parameters.localPhysicsMode, string.Empty);
    SetActiveScene(scene);
    return scene;
  }

  [Obsolete("Use SceneManager.sceneCount and SceneManager.GetSceneAt(int index) to loop the all scenes instead.")]
  public static Scene[] GetAllScenes()
  {
    lock (s_Gate)
      return s_SceneOrder
        .Where(handle => s_Scenes.TryGetValue(handle, out SceneState state) && state.IsLoaded)
        .Select(handle => new Scene(handle))
        .ToArray();
  }

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  public static Scene GetSceneByPath(string scenePath)
  {
    lock (s_Gate)
    {
      int handle = s_SceneOrder.FirstOrDefault(id => s_Scenes.TryGetValue(id, out SceneState state) && string.Equals(state.Path, scenePath, StringComparison.Ordinal));
      return handle == 0 ? default : new Scene(handle);
    }
  }

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  public static Scene GetSceneByName(string name)
  {
    lock (s_Gate)
    {
      int handle = s_SceneOrder.FirstOrDefault(id => s_Scenes.TryGetValue(id, out SceneState state) && string.Equals(state.Name, name, StringComparison.Ordinal));
      return handle == 0 ? default : new Scene(handle);
    }
  }

  public static Scene GetSceneByBuildIndex(int buildIndex)
  {
    lock (s_Gate)
    {
      int handle = s_SceneOrder.FirstOrDefault(id => s_Scenes.TryGetValue(id, out SceneState state) && state.BuildIndex == buildIndex);
      return handle == 0 ? default : new Scene(handle);
    }
  }

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  [NativeThrows]
  public static Scene GetSceneAt(int index)
  {
    lock (s_Gate)
    {
      int[] loaded = s_SceneOrder.Where(handle => s_Scenes.TryGetValue(handle, out SceneState state) && state.IsLoaded).ToArray();
      if ((uint)index >= (uint)loaded.Length)
        throw new ArgumentException("Invalid scene index.", nameof(index));
      return new Scene(loaded[index]);
    }
  }

  public static void LoadScene(string sceneName, [UnityEngine.Internal.DefaultValue("LoadSceneMode.Single")] LoadSceneMode mode)
    => _ = LoadScene(sceneName, new LoadSceneParameters(mode));

  [UnityEngine.Internal.ExcludeFromDocs]
  public static void LoadScene(string sceneName)
    => _ = LoadScene(sceneName, new LoadSceneParameters(LoadSceneMode.Single));

  public static Scene LoadScene(string sceneName, LoadSceneParameters parameters)
    => LoadSceneInternal(sceneName, -1, parameters);

  public static void LoadScene(int sceneBuildIndex, [UnityEngine.Internal.DefaultValue("LoadSceneMode.Single")] LoadSceneMode mode)
    => _ = LoadScene(sceneBuildIndex, new LoadSceneParameters(mode));

  [UnityEngine.Internal.ExcludeFromDocs]
  public static void LoadScene(int sceneBuildIndex)
    => _ = LoadScene(sceneBuildIndex, new LoadSceneParameters(LoadSceneMode.Single));

  public static Scene LoadScene(int sceneBuildIndex, LoadSceneParameters parameters)
    => LoadSceneInternal(null, sceneBuildIndex, parameters);

  public static AsyncOperation LoadSceneAsync(int sceneBuildIndex, [UnityEngine.Internal.DefaultValue("LoadSceneMode.Single")] LoadSceneMode mode)
    => LoadSceneAsync(sceneBuildIndex, new LoadSceneParameters(mode));

  [UnityEngine.Internal.ExcludeFromDocs]
  public static AsyncOperation LoadSceneAsync(int sceneBuildIndex)
    => LoadSceneAsync(sceneBuildIndex, new LoadSceneParameters(LoadSceneMode.Single));

  public static AsyncOperation LoadSceneAsync(int sceneBuildIndex, LoadSceneParameters parameters)
  {
    _ = LoadScene(sceneBuildIndex, parameters);
    return CompletedOperation();
  }

  public static AsyncOperation LoadSceneAsync(string sceneName, [UnityEngine.Internal.DefaultValue("LoadSceneMode.Single")] LoadSceneMode mode)
    => LoadSceneAsync(sceneName, new LoadSceneParameters(mode));

  [UnityEngine.Internal.ExcludeFromDocs]
  public static AsyncOperation LoadSceneAsync(string sceneName)
    => LoadSceneAsync(sceneName, new LoadSceneParameters(LoadSceneMode.Single));

  public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters)
  {
    _ = LoadScene(sceneName, parameters);
    return CompletedOperation();
  }

  [Obsolete(ObsoleteUnloadMessage)]
  public static bool UnloadScene(Scene scene) => TryUnload(scene.handle, UnloadSceneOptions.None, out _);

  [Obsolete(ObsoleteUnloadMessage)]
  public static bool UnloadScene(int sceneBuildIndex)
  {
    Scene scene = GetSceneByBuildIndex(sceneBuildIndex);
    return scene.IsValid() && TryUnload(scene.handle, UnloadSceneOptions.None, out _);
  }

  [Obsolete(ObsoleteUnloadMessage)]
  public static bool UnloadScene(string sceneName)
  {
    Scene scene = ResolveScene(sceneName);
    return scene.IsValid() && TryUnload(scene.handle, UnloadSceneOptions.None, out _);
  }

  public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex)
    => UnloadSceneAsync(sceneBuildIndex, UnloadSceneOptions.None);

  public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex, UnloadSceneOptions options)
  {
    Scene scene = GetSceneByBuildIndex(sceneBuildIndex);
    return CreateUnloadOperation(scene, options);
  }

  public static AsyncOperation UnloadSceneAsync(string sceneName)
    => UnloadSceneAsync(sceneName, UnloadSceneOptions.None);

  public static AsyncOperation UnloadSceneAsync(string sceneName, UnloadSceneOptions options)
    => CreateUnloadOperation(ResolveScene(sceneName), options);

  public static AsyncOperation UnloadSceneAsync(Scene scene)
    => UnloadSceneAsync(scene, UnloadSceneOptions.None);

  public static AsyncOperation UnloadSceneAsync(Scene scene, UnloadSceneOptions options)
    => CreateUnloadOperation(scene, options);

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  [NativeThrows]
  public static void MergeScenes(Scene sourceScene, Scene destinationScene)
  {
    if (!sourceScene.IsValid())
      throw new ArgumentException("Source scene is not valid.", nameof(sourceScene));
    if (!destinationScene.IsValid())
      throw new ArgumentException("Destination scene is not valid.", nameof(destinationScene));
    if (!sourceScene.isLoaded || !destinationScene.isLoaded)
      throw new ArgumentException("Both scenes must be loaded.");
    if (sourceScene == destinationScene)
      throw new ArgumentException("Source and destination scene must be different.");

    foreach (GameObject rootObject in sourceScene.GetRootGameObjects())
      rootObject.SetSceneInternal(destinationScene);

    RemoveSceneState(sourceScene.handle, false, false);
  }

  [StaticAccessor("SceneManagerBindings", StaticAccessorType.DoubleColon)]
  [NativeThrows]
  public static void MoveGameObjectToScene([NotNull("ArgumentNullException")] GameObject go, Scene scene)
  {
    if (go == null)
      throw new ArgumentNullException(nameof(go));
    if (!scene.IsValid())
      throw new ArgumentException("Destination scene is not valid");
    if (!scene.isLoaded)
      throw new ArgumentException("Destination scene is not loaded");
    if (go.transform.parent is not null)
      throw new ArgumentException("Gameobject is not a root in a scene");
    go.SetSceneInternal(scene);
  }

  public static void MoveGameObjectsToScene(NativeArray<int> instanceIDs, Scene scene)
  {
    if (!instanceIDs.IsCreated)
      throw new ArgumentException("NativeArray is uninitialized", nameof(instanceIDs));
    if (instanceIDs.Length == 0)
      return;

    for (int i = 0; i < instanceIDs.Length; i++)
    {
      if (UnityEngine.Object.FindObjectFromInstanceID(instanceIDs[i]) is not GameObject gameObject)
        throw new ArgumentException($"Instance ID {instanceIDs[i]} is not a GameObject.", nameof(instanceIDs));
      MoveGameObjectToScene(gameObject, scene);
    }
  }

  internal static bool IsValidSceneHandle(int handle)
  {
    lock (s_Gate)
      return handle != 0 && s_Scenes.ContainsKey(handle);
  }

  internal static string GetScenePath(int handle)
    => TryGetState(handle, out SceneState state) ? state.Path : string.Empty;

  internal static string GetSceneName(int handle)
    => TryGetState(handle, out SceneState state) ? state.Name : string.Empty;

  internal static void SetSceneName(int handle, string value)
  {
    if (value is null)
      throw new ArgumentNullException(nameof(value));
    if (!TryGetState(handle, out SceneState state))
      throw new ArgumentException("The scene is invalid.");
    state.Name = value;
  }

  internal static bool GetSceneIsLoaded(int handle)
    => TryGetState(handle, out SceneState state) && state.IsLoaded;

  internal static int GetSceneBuildIndex(int handle)
    => TryGetState(handle, out SceneState state) ? state.BuildIndex : -1;

  internal static bool GetSceneIsDirty(int handle)
    => TryGetState(handle, out SceneState state) && state.IsDirty;

  internal static void SetSceneIsDirty(int handle, bool value)
  {
    if (!TryGetState(handle, out SceneState state))
      throw new ArgumentException("The scene is invalid.");
    state.IsDirty = value;
  }

  internal static void SetScenePath(int handle, string value)
  {
    if (!TryGetState(handle, out SceneState state))
      throw new ArgumentException("The scene is invalid.");
    state.Path = value ?? string.Empty;
  }

  internal static bool GetSceneIsSubScene(int handle)
    => TryGetState(handle, out SceneState state) && state.IsSubScene;

  internal static void SetSceneIsSubScene(int handle, bool value)
  {
    if (!TryGetState(handle, out SceneState state))
      throw new ArgumentException("The scene is invalid.");
    state.IsSubScene = value;
  }

  internal static int GetRootCount(int handle)
  {
    if (!TryGetState(handle, out SceneState state))
      return 0;
    lock (s_Gate)
    {
      state.RootObjects.RemoveAll(gameObject => gameObject == null || gameObject.IsDestroyed || gameObject.transform.parent is not null);
      return state.RootObjects.Count;
    }
  }

  internal static void CopyRootGameObjects(int handle, List<GameObject> destination)
  {
    if (!TryGetState(handle, out SceneState state))
      return;
    lock (s_Gate)
    {
      state.RootObjects.RemoveAll(gameObject => gameObject == null || gameObject.IsDestroyed || gameObject.transform.parent is not null);
      destination.AddRange(state.RootObjects);
    }
  }

  internal static void RegisterRootGameObject(GameObject go, Scene scene)
  {
    if (go == null || !TryGetState(scene.handle, out SceneState state))
      return;
    lock (s_Gate)
    {
      if (!state.RootObjects.Contains(go))
        state.RootObjects.Add(go);
    }
  }

  internal static void UnregisterRootGameObject(GameObject go, Scene scene)
  {
    if (go == null || !TryGetState(scene.handle, out SceneState state))
      return;
    lock (s_Gate)
      state.RootObjects.Remove(go);
  }

  private static Scene LoadSceneInternal(string sceneName, int sceneBuildIndex, LoadSceneParameters parameters)
  {
    Scene existing = sceneBuildIndex >= 0 ? GetSceneByBuildIndex(sceneBuildIndex) : ResolveScene(sceneName);
    string resolvedName = sceneBuildIndex >= 0 ? $"Scene_{sceneBuildIndex}" : (string.IsNullOrWhiteSpace(sceneName) ? DefaultSceneName : sceneName);
    string resolvedPath = sceneBuildIndex >= 0 ? string.Empty : sceneName ?? string.Empty;

    if (parameters.loadSceneMode == LoadSceneMode.Single)
    {
      int keepHandle = existing.handle;
      int[] toRemove;
      lock (s_Gate)
        toRemove = s_SceneOrder.Where(handle => handle != keepHandle).ToArray();
      foreach (int handle in toRemove)
        RemoveSceneState(handle, true, true);
    }

    Scene scene;
    if (existing.IsValid())
    {
      scene = existing;
      if (TryGetState(scene.handle, out SceneState state))
      {
        state.IsLoaded = true;
        state.LocalPhysicsMode = parameters.localPhysicsMode;
      }
    }
    else
    {
      int buildIndex = sceneBuildIndex;
      if (buildIndex < 0)
        buildIndex = FindBuildIndex(sceneName);
      scene = CreateSceneInternal(resolvedName, buildIndex, true, parameters.localPhysicsMode, resolvedPath);
    }

    Scene previous = GetActiveScene();
    s_ActiveHandle = scene.handle;
    if (previous != scene)
      activeSceneChanged?.Invoke(previous, scene);
    sceneLoaded?.Invoke(scene, parameters.loadSceneMode);
    return scene;
  }

  private static Scene CreateSceneInternal(string name, int buildIndex, bool isLoaded, LocalPhysicsMode physicsMode, string path)
  {
    lock (s_Gate)
    {
      int handle = s_NextHandle++;
      var state = new SceneState
      {
        Handle = handle,
        Name = name ?? string.Empty,
        BuildIndex = buildIndex,
        IsLoaded = isLoaded,
        Path = path ?? string.Empty,
        LocalPhysicsMode = physicsMode
      };
      s_Scenes.Add(handle, state);
      s_SceneOrder.Add(handle);
      return new Scene(handle);
    }
  }

  private static Scene ResolveScene(string nameOrPath)
  {
    Scene byName = GetSceneByName(nameOrPath);
    return byName.IsValid() ? byName : GetSceneByPath(nameOrPath);
  }

  private static int FindBuildIndex(string scenePath)
  {
    try
    {
      UnityEditor.EditorBuildSettings.EditorBuildSettingsScene[] editorScenes = UnityEditor.EditorBuildSettings.scenes;
      for (int i = 0; i < editorScenes.Length; i++)
      {
        if (editorScenes[i].enabled && string.Equals(editorScenes[i].path, scenePath, StringComparison.OrdinalIgnoreCase))
          return i;
      }
    }
    catch
    {
    }
    return -1;
  }

  private static AsyncOperation CreateUnloadOperation(Scene scene, UnloadSceneOptions options)
  {
    bool success = scene.IsValid() && TryUnload(scene.handle, options, out _);
    var operation = new AsyncOperation();
    if (success)
    {
      operation.progress = 1f;
      operation.isDone = true;
    }
    return operation;
  }

  private static bool TryUnload(int handle, UnloadSceneOptions options, out Scene unloadedScene)
  {
    _ = options;
    unloadedScene = GetSceneByHandle(handle);
    if (!unloadedScene.IsValid() || !unloadedScene.isLoaded || loadedSceneCount <= 1)
      return false;

    RemoveSceneState(handle, true, true);
    return true;
  }

  private static void RemoveSceneState(int handle, bool destroyRoots, bool raiseEvent)
  {
    Scene scene = GetSceneByHandle(handle);
    if (!scene.IsValid() || !TryGetState(handle, out SceneState state))
      return;

    GameObject[] roots;
    lock (s_Gate)
      roots = state.RootObjects.ToArray();

    if (destroyRoots)
    {
      foreach (GameObject gameObject in roots)
      {
        if (gameObject != null && !gameObject.IsDestroyed && !gameObject.IsDontDestroyOnLoad)
          UnityEngine.Object.DestroyImmediate(gameObject);
      }
    }

    lock (s_Gate)
    {
      state.RootObjects.Clear();
      state.IsLoaded = false;
      s_Scenes.Remove(handle);
      s_SceneOrder.Remove(handle);
    }

    if (s_ActiveHandle == handle)
    {
      lock (s_Gate)
        s_ActiveHandle = s_SceneOrder.FirstOrDefault(id => s_Scenes.TryGetValue(id, out SceneState candidate) && candidate.IsLoaded);
    }

    if (raiseEvent)
      sceneUnloaded?.Invoke(scene);
  }

  private static bool TryGetState(int handle, out SceneState state)
  {
    lock (s_Gate)
      return s_Scenes.TryGetValue(handle, out state);
  }

  private static Scene GetSceneByHandle(int handle)
    => IsValidSceneHandle(handle) ? new Scene(handle) : default;

  private static AsyncOperation CompletedOperation()
  {
    return new AsyncOperation
    {
      progress = 1f,
      isDone = true
    };
  }
}
