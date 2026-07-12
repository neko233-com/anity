using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.SceneManagement;

public static class SceneManager
{
  private const string DefaultSceneName = "SampleScene";

  private static readonly List<Scene> _scenes = new();
  private static readonly Dictionary<string, int> _nameToHandle = new(StringComparer.Ordinal);
  private static readonly Dictionary<string, int> _pathToHandle = new(StringComparer.Ordinal);
  private static readonly Dictionary<int, int> _buildIndexToHandle = new();
  private static int _nextHandle = 1;
  private static int _activeHandle;

  public static event UnityAction<Scene, LoadSceneMode> sceneLoaded;
  public static event UnityAction<Scene> sceneUnloaded;
  public static event UnityAction<Scene, Scene> activeSceneChanged;

  static SceneManager()
  {
    var defaultScene = CreateSceneInternal(DefaultSceneName, 0, true);
    _activeHandle = defaultScene._handle;
  }

  public static int sceneCount => _scenes.Count;
  public static int sceneCountInBuildSettings => _scenes.Count;
  public static int loadedSceneCount => _scenes.Count(s => s._isLoaded);

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
    var scene = CreateSceneInternal(name, _scenes.Count, true);
    _activeHandle = scene._handle;
    return scene;
  }

  private static Scene CreateSceneInternal(string sceneName, int buildIndex, bool isLoaded)
  {
    var name = string.IsNullOrWhiteSpace(sceneName) ? DefaultSceneName : sceneName;
    var handle = _nextHandle++;
    var scene = new Scene(handle, name, buildIndex, isLoaded, false, "");
    _scenes.Add(scene);
    _nameToHandle[name] = handle;
    _buildIndexToHandle[buildIndex] = handle;
    return scene;
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
    if (index < 0 || index >= _scenes.Count)
    {
      return Scene.Invalid;
    }

    return _scenes[index];
  }

  public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
  {
    var scene = LoadSceneInternal(sceneName, mode);
    _activeHandle = scene._handle;
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
    op.isDone = true;
    op.progress = 1f;
    return op;
  }

  public static AsyncOperation LoadSceneAsync(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
  {
    var fallback = $"Scene_{buildIndex}";
    return LoadSceneAsync(fallback, mode);
  }

  public static bool SetActiveScene(Scene scene)
  {
    if (scene is null || !scene._isLoaded)
    {
      return false;
    }

    var previous = GetActiveScene();
    _activeHandle = scene._handle;
    if (previous != scene)
    {
      activeSceneChanged?.Invoke(previous, scene);
    }
    return true;
  }

  public static AsyncOperation UnloadSceneAsync(string sceneName)
  {
    return UnloadByHandle(_nameToHandle.TryGetValue(sceneName, out var handle) ? handle : 0);
  }

  public static AsyncOperation UnloadSceneAsync(Scene scene)
  {
    return scene != null ? UnloadByHandle(scene._handle) : new AsyncOperation();
  }

  public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex)
  {
    var scene = GetSceneByBuildIndex(sceneBuildIndex);
    return scene.IsValid() ? UnloadByHandle(scene._handle) : new AsyncOperation();
  }

  public static AsyncOperation UnloadSceneAsync(Scene scene, UnloadSceneOptions options)
  {
    _ = options;
    return UnloadSceneAsync(scene);
  }

  public static AsyncOperation UnloadSceneAsync(string sceneName, UnloadSceneOptions options)
  {
    _ = options;
    return UnloadSceneAsync(sceneName);
  }

  public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex, UnloadSceneOptions options)
  {
    _ = options;
    return UnloadSceneAsync(sceneBuildIndex);
  }

  public static void LoadScene(string sceneName, LoadSceneParameters parameters)
  {
    LoadScene(sceneName, parameters.loadSceneMode);
  }

  public static void LoadScene(int buildIndex, LoadSceneParameters parameters)
  {
    LoadScene(buildIndex, parameters.loadSceneMode);
  }

  public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters)
  {
    return LoadSceneAsync(sceneName, parameters.loadSceneMode);
  }

  public static AsyncOperation LoadSceneAsync(int buildIndex, LoadSceneParameters parameters)
  {
    return LoadSceneAsync(buildIndex, parameters.loadSceneMode);
  }

  internal static int CreateHandleForUntrackedScene(string sceneName, int buildIndex)
  {
    sceneName = sceneName ?? DefaultSceneName;
    var existing = ResolveHandle(sceneName, buildIndex);
    return existing != 0 ? existing : CreateSceneInternal(sceneName, buildIndex, true)._handle;
  }

  internal static int GetRootCount(int handle)
  {
    var scene = GetSceneByHandleInternal(handle);
    return scene?._rootObjects?.Count ?? 0;
  }

  internal static GameObject[] GetRootGameObjects(int handle)
  {
    var scene = GetSceneByHandleInternal(handle);
    return scene?.GetRootGameObjects() ?? Array.Empty<GameObject>();
  }

  internal static void RegisterRootGameObject(GameObject go, Scene scene)
  {
    if (go == null || scene == null) return;
    if (!scene._rootObjects.Contains(go))
    {
      scene._rootObjects.Add(go);
    }
  }

  internal static void UnregisterRootGameObject(GameObject go, Scene scene)
  {
    if (go == null || scene == null) return;
    scene._rootObjects.Remove(go);
  }

  public static void MergeScenes(Scene sourceScene, Scene targetScene)
  {
    if (sourceScene == null || targetScene == null) return;
    var roots = sourceScene.GetRootGameObjects();
    foreach (var go in roots)
    {
      if (go != null && go.transform != null)
      {
        go.transform.SetParent(null, true);
        targetScene._rootObjects.Add(go);
      }
    }
    sourceScene._rootObjects.Clear();
  }

  public static void MoveGameObjectToScene(GameObject go, Scene scene)
  {
    if (go == null || scene == null) return;
    var oldScene = GetSceneByName(go.scene.name);
    if (oldScene != null && oldScene._rootObjects.Contains(go))
    {
      oldScene._rootObjects.Remove(go);
    }
    if (!scene._rootObjects.Contains(go))
    {
      scene._rootObjects.Add(go);
    }
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

    if (mode == LoadSceneMode.Single)
    {
      foreach (var s in _scenes.Where(s => s._isLoaded).ToList())
      {
        if (s._handle != _activeHandle)
        {
          s._isLoaded = false;
          s._rootObjects.Clear();
        }
      }
    }

    var existing = GetSceneByName(name);
    if (!existing.IsValid())
    {
      existing = GetSceneByPath(name);
    }
    if (existing.IsValid())
    {
      existing._isLoaded = true;
      sceneLoaded?.Invoke(existing, mode);
      return existing;
    }

    var buildIndex = -1;
    try
    {
      var editorScenes = UnityEditor.EditorBuildSettings.scenes;
      for (int i = 0; i < editorScenes.Length; i++)
      {
        if (editorScenes[i].enabled && string.Equals(editorScenes[i].path, name, System.StringComparison.OrdinalIgnoreCase))
        {
          buildIndex = i;
          break;
        }
      }
    }
    catch { }
    var scene = CreateSceneInternal(name, buildIndex >= 0 ? buildIndex : _buildIndexToHandle.Count, true);
    scene.path = name;
    _pathToHandle[name] = scene._handle;
    sceneLoaded?.Invoke(scene, mode);

    return scene;
  }

  private static AsyncOperation UnloadByHandle(int handle)
  {
    var op = new AsyncOperation();
    var scene = GetSceneByHandleInternal(handle);
    if (scene == null)
    {
      return op;
    }

    scene._isLoaded = false;
    var roots = scene._rootObjects.ToList();
    foreach (var go in roots)
    {
      if (go != null && !go.IsDontDestroyOnLoad)
      {
        Object.DestroyImmediate(go);
      }
    }
    scene._rootObjects.Clear();

    if (_activeHandle == handle)
    {
      var remaining = _scenes.FirstOrDefault(s => s._isLoaded);
      _activeHandle = remaining != null ? remaining._handle : 0;
      if (_activeHandle == 0 && _scenes.Count > 0)
      {
        _scenes[0]._isLoaded = true;
        _activeHandle = _scenes[0]._handle;
      }
    }

    _scenes.Remove(scene);
    _nameToHandle.Remove(scene.name);
    if (!string.IsNullOrEmpty(scene.path))
    {
      _pathToHandle.Remove(scene.path);
    }
    _buildIndexToHandle.Remove(scene.buildIndex);

    sceneUnloaded?.Invoke(scene);

    op.isDone = true;
    op.progress = 1f;
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

  private static Scene? GetSceneByHandleInternal(int handle)
  {
    return _scenes.FirstOrDefault(s => s._handle == handle);
  }

  private static Scene GetSceneByHandle(int handle)
  {
    return GetSceneByHandleInternal(handle) ?? Scene.Invalid;
  }
}
