using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityEditor;

public static class EditorApplication
{
  private static readonly DateTime _start = DateTime.UtcNow;
  private static readonly Queue<Action> _delayQueue = new();
  private static bool _isPlayerChanging;
  private static int _lockCount;
  private static bool _isTemporaryProjectItem;

  public static event Action? update;
  public static event Action? projectChanged;
  public static event Action? hierarchyChanged;
  public static event Action? hierarchyWindowChanged;
  public static event Action? projectWindowChanged;
  public static event Action? quitting;
  public static event Action<PauseState>? pauseStateChanged;
  public static event Action<PlayModeStateChange>? playModeStateChanged;
  public static event Action<GenericMenu>? contextualPropertyMenu;
  public static event Action? globalEventHandler;
  public static event Action? delayCall;

  public static bool isUpdating { get; set; }
  public static bool inBatchMode => false;
  public static bool isCompiling { get; set; } = false;
  public static bool isRemoteConnected { get; set; } = false;
  public static bool isTemporaryProjectItem => _isTemporaryProjectItem;
  public static int activeEditorWindowID { get; } = -1;
  public static bool x64 => IntPtr.Size == 8;
  public static bool isHierarchyDirty { get; set; }
  public static bool isProjectDirty { get; set; }
  public static bool isInspectorLocked { get; set; }

  public static bool isPlaying { get; set; } = false;
  public static bool isPlayingOrWillChangePlaymode => isPlaying || _isPlayerChanging;
  public static bool isPaused { get; set; } = false;
  public static bool isLoaded { get; } = true;
  public static int frameCount { get; private set; }
  public static double timeSinceStartup => (DateTime.UtcNow - _start).TotalSeconds;
  public static double realtimeSinceStartup => (DateTime.UtcNow - _start).TotalSeconds;
  public static string applicationContentsPath => AppContext.BaseDirectory;
  public static string applicationPath => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
  public static string applicationDataPath => Path.Combine(AppContext.BaseDirectory, "Data");
  public static string projectPath => Directory.GetCurrentDirectory();
  public static EditorWindow? sceneHierarchyWindow { get; set; }

  public static EnterPlayModeOptions enterPlayModeOptions { get; set; }
  public static bool enterPlayModeOptionsEnabled { get; set; }

  public static UpdateMode updateMode { get; set; } = UpdateMode.Default;

  public static void Update()
  {
    if (isPaused)
    {
      return;
    }

    isUpdating = true;
    frameCount++;
    update?.Invoke();

    while (_delayQueue.Count > 0)
    {
      var action = _delayQueue.Dequeue();
      action?.Invoke();
    }

    delayCall?.Invoke();
    isUpdating = false;
  }

  public static void DelayAction(Action action)
  {
    if (action is null) return;
    _delayQueue.Enqueue(action);
  }

  public static void DelayCall(Action action) => DelayAction(action);

  public static void QueuePlayerLoopUpdate()
  {
  }

  public static void UpdateMainWindowTitle()
  {
  }

  public static void RepaintHierarchyWindow()
  {
    hierarchyWindowChanged?.Invoke();
  }

  public static void RepaintProjectWindow()
  {
    projectWindowChanged?.Invoke();
  }

  public static void RegisterLogCallback(LogCallback callback) => _ = callback;
  public static void UnregisterLogCallback(LogCallback callback) => _ = callback;

  public static void OpenURL(string url)
  {
    _ = url;
  }

  public static void Beep()
  {
    Console.Beep();
  }

  public static void ForceReload()
  {
    projectChanged?.Invoke();
  }

  public static void OpenProject(string projectPath)
  {
    _ = projectPath;
    projectChanged?.Invoke();
  }

  public static void Exit(int exitCode = 0)
  {
    _ = exitCode;
    quitting?.Invoke();
  }

  public static void InvokeMenuItem(string menuPath)
  {
    _ = menuPath;
  }

  public static void LockReloadAssemblies() => _lockCount++;
  public static void UnlockReloadAssemblies() { if (_lockCount > 0) _lockCount--; }
  public static bool isAssemblyReloadLocked => _lockCount > 0;

  public static void LoadLevelInPlayMode(string levelName, bool mustReload = true)
  {
    _ = levelName;
    _ = mustReload;
    if (!isPlaying) return;
  }

  public static void SetCurrentScene(string path, bool? reload = null)
  {
    _ = path;
    _ = reload;
  }

  public static void EnterPlaymode()
  {
    if (_isPlayerChanging) return;
    _isPlayerChanging = true;
    playModeStateChanged?.Invoke(PlayModeStateChange.ExitingEditMode);
    isPlaying = true;
    playModeStateChanged?.Invoke(PlayModeStateChange.EnteredPlayMode);
    _isPlayerChanging = false;
  }

  public static void ExitPlaymode()
  {
    if (_isPlayerChanging) return;
    _isPlayerChanging = true;
    playModeStateChanged?.Invoke(PlayModeStateChange.ExitingPlayMode);
    isPlaying = false;
    playModeStateChanged?.Invoke(PlayModeStateChange.EnteredEditMode);
    _isPlayerChanging = false;
  }

  public static void Step()
  {
    if (isPaused)
    {
      Update();
    }
  }

  public static void SaveAssets()
  {
  }

  public static void NewEmptyScene()
  {
  }

  public static void NewScene()
  {
    NewEmptyScene();
  }

  public static int ExecuteMenuItemWithArgs(string menuItem, params string[] args)
  {
    _ = menuItem;
    _ = args;
    return 0;
  }

  public static string GetFolderPath(SpecialFolder folder)
  {
    return folder switch
    {
      SpecialFolder.ApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      SpecialFolder.LocalApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      SpecialFolder.MyDocuments => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
      SpecialFolder.Desktop => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
      SpecialFolder.Temp => Path.GetTempPath(),
      _ => AppContext.BaseDirectory
    };
  }

  public static class Utility
  {
    public static void ShowStartupSplash(bool show) => _ = show;
  }
}

public delegate void LogCallback(string condition, string stackTrace, UnityEngine.LogType type);

public enum PlayModeStateChange
{
  EnteredEditMode,
  ExitingEditMode,
  EnteredPlayMode,
  ExitingPlayMode
}

public enum PauseState
{
  Paused,
  Unpaused
}

public enum UpdateMode
{
  Default,
  Polled,
  Manual
}

[Flags]
public enum EnterPlayModeOptions
{
  None = 0,
  DisableDomainReload = 1,
  DisableSceneReload = 2
}

public enum EnterPlaymodeBehavior
{
  Normal,
  ReloadAll
}

public enum SpecialFolder
{
  ApplicationData,
  LocalApplicationData,
  MyDocuments,
  Desktop,
  Temp,
  Project,
  Packages
}

public static class ProjectWindowUtil
{
  public static void ShowCreatedAsset(UnityEngine.Object asset)
  {
    _ = asset;
  }

  public static UnityEngine.Object CreateAssetWithContent(string path, string content)
  {
    _ = path;
    _ = content;
    return null;
  }

  public static void StartNameEditingIfProjectWindowExists(int instanceID)
  {
    _ = instanceID;
  }
}
