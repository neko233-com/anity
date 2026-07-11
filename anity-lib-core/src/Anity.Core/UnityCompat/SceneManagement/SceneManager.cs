using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.SceneManagement;

public static class SceneManager
{
  private const string DefaultSceneName = "SampleScene";

  private sealed class SceneState
  {
    public int Handle;
    public string Name = string.Empty;
    public int BuildIndex;
    public bool IsLoaded = true;
    public bool IsSubScene;
    public string Path = string.Empty;
  }

  private static readonly Dictionary<int, SceneState> _scenes = new();
  private static readonly Dictionary<string, int> _nameToHandle = new(StringComparer.Ordinal);
  private static readonly Dictionary<string, int> _pathToHandle = new(StringComparer.Ordinal);
  private static readonly Dictionary<int, int> _buildIndexToHandle = new();
  private static readonly List<int> _loadedOrder = new();
  private static int _nextHandle = 1;
  private static int _activeHandle;

  public static event UnityAction<Scene, LoadSceneMode> sceneLoaded;
  public static event UnityAction<Scene> sceneUnloaded;
  public static event UnityAction<Scene, Scene> activeSceneChanged;

  static SceneManager()
  {
    var defaultHandle = RegisterScene(DefaultSceneName, 0, true);
    _activeHandle = defaultHandle;
    PushLoaded(defaultHandle);
  }

  public static int sceneCount => _loadedOrder.Count;
  public static int sceneCountInBuildSettings => _scenes.Count;
  public static int loadedSceneCount => _loadedOrder.Count;
  public static Scene activeScene
  {
    get => GetSceneByHandle(_activeHandle);
    set => SetActiveScene(value);
  }

  public static Scene GetActiveScene()
  {
    return activeScene;
  }

  public static Scene CreateScene(string sceneName)
  {
    var name = string.IsNullOrWhiteSpace(sceneName) ? $"Scene_{_nextHandle}" : sceneName;
    var handle = RegisterScene(name, _scenes.Count, true);
    PushLoaded(handle);
    _activeHandle = handle;
    return GetSceneByHandle(handle);
  }

  public static Scene GetSceneByName(string sceneName)
  {
    sceneName = sceneName ?? DefaultSceneName;
    if (_nameToHandle.TryGetValue(sceneName, out var handle))
    {
      return GetSceneByHandle(handle);
    }

    return Scene.Invalid;
  }

  public static Scene GetSceneByBuildIndex(int buildIndex)
  {
    if (_buildIndexToHandle.TryGetValue(buildIndex, out var handle))
    {
      return GetSceneByHandle(handle);
    }

    return Scene.Invalid;
  }

  public static Scene GetSceneAt(int index)
  {
    if (index < 0 || index >= _loadedOrder.Count)
    {
      return Scene.Invalid;
    }

    return GetSceneByHandle(_loadedOrder[index]);
  }

  public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
  {
    var scene = LoadSceneInternal(sceneName, mode);
    _activeHandle = scene.handle;
  }

  public static void LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
  {
    var fallback = $"Scene_{buildIndex}";
    LoadScene(fallback, mode);
  }

  public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
  {
    var op = new AsyncOperation();
    LoadScene(sceneName, mode);
    return op;
  }

  public static AsyncOperation LoadSceneAsync(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
  {
    var fallback = $"Scene_{buildIndex}";
    return LoadSceneAsync(fallback, mode);
  }

  public static void SetActiveScene(Scene scene)
  {
    var state = ResolveSceneState(scene);
    if (state is null || !state.IsLoaded)
    {
      return;
    }

    var previous = GetActiveScene();
    _activeHandle = state.Handle;
    PushLoaded(state.Handle);
    activeSceneChanged?.Invoke(previous, scene);
  }

  public static AsyncOperation UnloadSceneAsync(string sceneName)
  {
    return UnloadByHandle(_nameToHandle.TryGetValue(sceneName, out var handle) ? handle : 0);
  }

  public static AsyncOperation UnloadSceneAsync(Scene scene)
  {
    return UnloadByHandle(scene.handle);
  }

  internal static int CreateHandleForUntrackedScene(string sceneName, int buildIndex)
  {
    sceneName = sceneName ?? DefaultSceneName;
    var existing = ResolveHandle(sceneName, buildIndex);
    return existing != 0 ? existing : RegisterScene(sceneName, buildIndex, true);
  }

  internal static int GetRootCount(int handle)
  {
    if (handle <= 0)
    {
      return 0;
    }

    return GameObject.GetSceneRootGameObjects().Length;
  }

  internal static GameObject[] GetRootGameObjects(int handle)
  {
    _ = handle;
    return GameObject.GetSceneRootGameObjects();
  }

  public static void MergeScenes(Scene sourceScene, Scene targetScene)
  {
    _ = sourceScene;
    _ = targetScene;
  }

  public static void MoveGameObjectToScene(GameObject go, Scene scene)
  {
    _ = go;
    _ = scene;
  }

  public static Scene GetSceneByPath(string scenePath)
  {
    if (_pathToHandle.TryGetValue(scenePath, out var handle))
    {
      return GetSceneByHandle(handle);
    }
    return Scene.Invalid;
  }

  private static Scene LoadSceneInternal(string sceneName, LoadSceneMode mode)
  {
    var name = string.IsNullOrWhiteSpace(sceneName) ? DefaultSceneName : sceneName;
    var handle = ResolveHandle(name, _buildIndexToHandle.Count);

    if (mode == LoadSceneMode.Single)
    {
      _loadedOrder.Clear();
    }

    if (handle == 0)
    {
      handle = RegisterScene(name, _buildIndexToHandle.Count, true);
    }

    var state = _scenes[handle];
    state.IsLoaded = true;
    _scenes[handle] = state;
    PushLoaded(handle);

    var scene = GetSceneByHandle(handle);
    sceneLoaded?.Invoke(scene, mode);

    return scene;
  }

  private static AsyncOperation UnloadByHandle(int handle)
  {
    var op = new AsyncOperation();
    if (!_scenes.TryGetValue(handle, out var state))
    {
      return op;
    }

    var scene = GetSceneByHandle(handle);
    state.IsLoaded = false;
    _scenes[handle] = state;
    _ = _loadedOrder.RemoveAll(x => x == handle);

    if (_activeHandle == handle)
    {
      _activeHandle = _loadedOrder.Count == 0 ? GetDefaultSceneHandle() : _loadedOrder[^1];
    }

    sceneUnloaded?.Invoke(scene);

    return op;
  }

  private static int ResolveHandle(string sceneName, int fallbackBuildIndex)
  {
    sceneName = sceneName ?? DefaultSceneName;
    if (_nameToHandle.TryGetValue(sceneName, out var existing))
    {
      return existing;
    }

    var fallbackScene = GetSceneByBuildIndex(fallbackBuildIndex);
    return fallbackScene.IsValid() && string.Equals(fallbackScene.name, sceneName, StringComparison.Ordinal)
      ? fallbackScene.handle
      : 0;
  }

  private static int RegisterScene(string sceneName, int buildIndex, bool loaded, bool isSubScene = false)
  {
    var normalizedName = sceneName ?? DefaultSceneName;
    var state = new SceneState
    {
      Handle = _nextHandle++,
      Name = normalizedName,
      BuildIndex = buildIndex,
      IsLoaded = loaded,
      IsSubScene = isSubScene
    };

    var handle = state.Handle;
    _scenes[handle] = state;
    _nameToHandle[normalizedName] = handle;
    _buildIndexToHandle[buildIndex] = handle;
    return handle;
  }

  private static void PushLoaded(int handle)
  {
    _ = _loadedOrder.RemoveAll(x => x == handle);
    _loadedOrder.Add(handle);
  }

  private static int GetDefaultSceneHandle()
  {
    return _nameToHandle.TryGetValue(DefaultSceneName, out var defaultHandle)
      ? defaultHandle
      : _scenes.Keys.FirstOrDefault();
  }

  private static Scene GetSceneByHandle(int handle)
  {
    return _scenes.TryGetValue(handle, out var state)
      ? new Scene(state.Handle, state.Name, state.BuildIndex, state.IsLoaded, state.IsSubScene, state.Path)
      : Scene.Invalid;
  }

  private static SceneState? ResolveSceneState(Scene scene)
  {
    return _scenes.TryGetValue(scene.handle, out var state) ? state : null;
  }
}
