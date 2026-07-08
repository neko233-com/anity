using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public static class EditorApplication
{
  private static readonly DateTime _start = DateTime.UtcNow;
  private static readonly Queue<Action> _delayQueue = new();
  private static bool _isPlayerChanging;

  public static event Action? update;
  public static event Action? projectChanged;
  public static event Action? hierarchyChanged;
  public static event Action? quitting;
  public static event Action<PlayModeStateChange>? playModeStateChanged;
  public static event Action? delayCall;

  public static bool isUpdating { get; set; }
  public static bool inBatchMode => false;
  public static bool isCompiling { get; set; } = false;
  public static bool isRemoteConnected { get; set; } = false;
  public static int activeEditorWindowID { get; } = -1;
  public static bool x64 => IntPtr.Size == 8;

  public static bool isPlaying { get; set; } = false;
  public static bool isPlayingOrWillChangePlaymode => isPlaying;
  public static bool isPaused { get; set; } = false;
  public static bool isLoaded { get; } = true;
  public static int frameCount { get; private set; }
  public static double timeSinceStartup => (DateTime.UtcNow - _start).TotalSeconds;
  public static string applicationContentsPath => AppContext.BaseDirectory;
  public static string applicationPath => Environment.ProcessPath ?? string.Empty;

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

  public static void RegisterLogCallback(LogCallback callback) => _ = callback;
  public static void UnregisterLogCallback(LogCallback callback) => _ = callback;

  public static void OpenURL(string url)
  {
    _ = url;
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

  public static void LockReloadAssemblies() {}
  public static void UnlockReloadAssemblies() {}

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

  public static int ExecuteMenuItemWithArgs(string menuItem, params string[] args)
  {
    _ = menuItem;
    _ = args;
    return 0;
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
