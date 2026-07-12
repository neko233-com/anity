using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace UnityEngine;

public delegate void LogCallback(string condition, string stackTrace, LogType type);

public enum ApplicationInstallMode
{
  Unknown,
  Store,
  DeveloperBuild,
  Adhoc,
  Enterprise,
  Editor
}

public enum ApplicationSandboxType
{
  Unknown,
  NotSandboxed,
  Sandboxed,
  SandboxBroken
}

public struct DeepLinkArgs
{
  public string url;
}

public static class Application
{
  private static RuntimePlatform? _overridePlatform;
  private static bool _isPaused;
  private static int _loadedLevel;
  private static LogCallback? _logCallback;
  public static string dataPath => AppContext.BaseDirectory;
  public static string streamingAssetsPath => Path.Combine(AppContext.BaseDirectory, "StreamingAssets");
  public static string persistentDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), UnityEditor.PlayerSettings.companyName, UnityEditor.PlayerSettings.productName);
  public static string temporaryCachePath => Path.GetTempPath();
  public static RuntimePlatform platform
  {
    get => _overridePlatform ?? GetRuntimePlatform();
    set => _overridePlatform = value;
  }
  public static bool isPlaying => UnityEditor.EditorApplication.isPlaying;
  public static bool isEditor => false;
  public static bool isFocused { get; set; } = true;
  public static bool isPaused { get => _isPaused; internal set => _isPaused = value; }
  private static bool _isBatchMode;
  public static bool isBatchMode
  {
    get => _isBatchMode;
    set => _isBatchMode = value;
  }
  public static bool isMobilePlatform => platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;
  public static bool isConsolePlatform => platform is RuntimePlatform.PS4 or RuntimePlatform.PS5 or RuntimePlatform.XboxOne or RuntimePlatform.Switch;
  public static bool isWebGL => platform == RuntimePlatform.WebGLPlayer;
  public static bool runInBackground { get; set; } = true;
  public static int targetFrameRate { get; set; } = -1;
  public static int sleepTimeout { get; set; } = -1;
  public static bool genuine => false;
  public static bool genuineCheckAvailable => false;
  public static string absoluteURL { get; set; } = string.Empty;
  public static string identifier => UnityEditor.PlayerSettings.applicationIdentifier;
  public static string bundleIdentifier => UnityEditor.PlayerSettings.applicationIdentifier;
  public static string companyName => UnityEditor.PlayerSettings.companyName;
  public static string productName => UnityEditor.PlayerSettings.productName;
  public static string version => UnityEditor.PlayerSettings.bundleVersion;
  public static string buildGUID => UnityEditor.PlayerSettings.buildGUID;
  public static string unityVersion => "2022.3.61f1";
  public static ApplicationInstallMode installMode => ApplicationInstallMode.Unknown;
  public static ApplicationSandboxType sandboxType => ApplicationSandboxType.NotSandboxed;
  public static string productGUID => string.Empty;
  public static string cloudProjectId => string.Empty;
  public static int loadedLevel => _loadedLevel;
  public static string loadedLevelName => _loadedLevel.ToString();
  public static bool isLoadingLevel => false;
  public static int levelCount => 1;
  public static string installerName => string.Empty;
  public static SystemLanguage systemLanguage { get; set; } = SystemLanguage.ChineseSimplified;
  public static NetworkReachability internetReachability { get; set; } = NetworkReachability.ReachableViaLocalAreaNetwork;
  public static bool backgroundLoadingPriority { get; set; }
  public static bool wantsToQuit { get; set; }
  public static string consoleLogPath => Path.Combine(temporaryCachePath, "Player.log");

  public static event Action<string, string, LogType>? logMessageReceived;
  public static event Action<string, string, LogType>? logMessageReceivedThreaded;
  public static event Action? onBeforeRender;
  public static event Action? quitting;
  public static event Func<bool>? wantsToQuitEvent;
  public static event Action? lowMemory;
  public static event Action<bool>? focusChanged;
  public static event Action<bool>? pauseStatusChanged;
  public static event Action<bool>? pausing;
  public static event Action? unloadingUnusedAssets;
  public static event Action<DeepLinkArgs>? deepLinkActivated;

  public static void Quit()
  {
    wantsToQuit = true;
    bool canQuit = true;
    if (wantsToQuitEvent is not null)
    {
      foreach (Func<bool> handler in wantsToQuitEvent.GetInvocationList())
      {
        if (!handler())
        {
          canQuit = false;
          break;
        }
      }
    }
    if (canQuit)
    {
      try { PlayerPrefs.SaveIfDirty(); } catch { }
      quitting?.Invoke();
    }
  }

  public static void Quit(int exitCode)
  {
    _ = exitCode;
    Quit();
  }

  public static void CancelQuit()
  {
    wantsToQuit = false;
  }

  public static void Unload()
  {
    unloadingUnusedAssets?.Invoke();
  }

  public static void OpenURL(string url)
  {
    if (string.IsNullOrWhiteSpace(url)) return;
    try
    {
      System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch { }
  }

  private static readonly Dictionary<LogType, StackTraceLogType> _stackTraceTypes = new();

  public static void SetStackTraceLogType(LogType logType, StackTraceLogType stackTraceType)
  {
    _stackTraceTypes[logType] = stackTraceType;
  }

  public static StackTraceLogType GetStackTraceLogType(LogType logType)
  {
    return _stackTraceTypes.TryGetValue(logType, out var t) ? t : StackTraceLogType.ScriptOnly;
  }

  public static void SetLogCallback(LogCallback? handler)
  {
    _logCallback = handler;
  }

  public static void SetLogCallback(LogCallback? handler, bool append)
  {
    if (!append)
    {
      _logCallback = handler;
    }
    else if (handler != null)
    {
      _logCallback += handler;
    }
  }

  public static Task<string> RequestAdvertisingIdentifierAsync()
  {
    return Task.FromResult(string.Empty);
  }

  public static bool GenerateCheckReport()
  {
    return false;
  }

  [Obsolete("Use SceneManager.LoadScene")]
  public static AsyncOperation LoadLevelAsync(string levelName)
  {
    return SceneManager.LoadSceneAsync(levelName);
  }

  [Obsolete("Use SceneManager.LoadScene")]
  public static AsyncOperation LoadLevelAsync(int index)
  {
    return SceneManager.LoadSceneAsync(index);
  }

  [Obsolete("Use SceneManager.LoadScene")]
  public static void LoadLevel(string name)
  {
    SceneManager.LoadScene(name);
  }

  [Obsolete("Use SceneManager.LoadScene")]
  public static void LoadLevel(int index)
  {
    SceneManager.LoadScene(index);
  }

  public static string[] GetUnityVersion()
  {
    return unityVersion.Split('.');
  }

  internal static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
  {
    _logCallback?.Invoke(condition, stackTrace, type);
    logMessageReceived?.Invoke(condition, stackTrace, type);
    logMessageReceivedThreaded?.Invoke(condition, stackTrace, type);
  }

  internal static void OnFocusChanged(bool focus)
  {
    isFocused = focus;
    focusChanged?.Invoke(focus);
  }

  internal static void OnPauseChanged(bool pause)
  {
    _isPaused = pause;
    pauseStatusChanged?.Invoke(pause);
    pausing?.Invoke(pause);
  }

  internal static void OnLowMemory()
  {
    lowMemory?.Invoke();
  }

  internal static void OnDeepLinkActivated(string url)
  {
    deepLinkActivated?.Invoke(new DeepLinkArgs { url = url });
  }

  private static RuntimePlatform GetRuntimePlatform()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return RuntimePlatform.WindowsPlayer;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return RuntimePlatform.OSXPlayer;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return RuntimePlatform.LinuxPlayer;
    return RuntimePlatform.WindowsPlayer;
  }
}

public enum RuntimePlatform
{
  WindowsPlayer,
  WindowsEditor,
  OSXPlayer,
  OSXEditor,
  LinuxPlayer,
  LinuxEditor,
  Android,
  IPhonePlayer,
  WebGLPlayer,
  Switch,
  PS4,
  PS5,
  XboxOne,
  tvOS
}

public enum SystemLanguage
{
  Afrikaans,
  Arabic,
  Basque,
  Belarusian,
  Bulgarian,
  Catalan,
  Chinese,
  ChineseSimplified,
  ChineseTraditional,
  Czech,
  Danish,
  Dutch,
  English,
  Estonian,
  Faroese,
  Finnish,
  French,
  German,
  Greek,
  Hebrew,
  Hungarian,
  Icelandic,
  Indonesian,
  Italian,
  Japanese,
  Korean,
  Latvian,
  Lithuanian,
  Norwegian,
  Polish,
  Portuguese,
  Romanian,
  Russian,
  SerboCroatian,
  Slovak,
  Slovenian,
  Spanish,
  Swedish,
  Thai,
  Turkish,
  Ukrainian,
  Vietnamese
}

public enum NetworkReachability
{
  NotReachable,
  ReachableViaCarrierDataNetwork,
  ReachableViaLocalAreaNetwork
}
