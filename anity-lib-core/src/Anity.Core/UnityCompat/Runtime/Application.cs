using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UnityEngine;

public static class Application
{
  private static RuntimePlatform? _overridePlatform;
  private static bool _isPaused;
  private static int _loadedLevel;
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
  public static bool isPaused => _isPaused;
  public static bool isBatchMode => false;
  public static bool isMobilePlatform => platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;
  public static bool isConsolePlatform => false;
  public static bool runInBackground { get; set; } = true;
  public static int targetFrameRate { get; set; } = -1;
  public static int sleepTimeout { get; set; } = -1;
  public static bool genuine => true;
  public static bool genuineCheckAvailable => true;
  public static string absoluteURL { get; set; } = string.Empty;
  public static string identifier => UnityEditor.PlayerSettings.applicationIdentifier;
  public static string bundleIdentifier => UnityEditor.PlayerSettings.applicationIdentifier;
  public static string companyName => UnityEditor.PlayerSettings.companyName;
  public static string productName => UnityEditor.PlayerSettings.productName;
  public static string version => UnityEditor.PlayerSettings.bundleVersion;
  public static string buildGUID => UnityEditor.PlayerSettings.buildGUID;
  public static string unityVersion => "2022.3.61f1";
  public static int loadedLevel => _loadedLevel;
  public static string loadedLevelName => _loadedLevel.ToString();
  public static bool isLoadingLevel => false;
  public static int levelCount => 1;
  public static bool isWebGL => platform == RuntimePlatform.WebGLPlayer;
  public static string installerName => string.Empty;
  public static SystemLanguage systemLanguage { get; set; } = SystemLanguage.ChineseSimplified;
  public static NetworkReachability internetReachability { get; set; } = NetworkReachability.ReachableViaLocalAreaNetwork;
  public static bool backgroundLoadingPriority { get; set; }
  public static bool wantsToQuit { get; set; }
  public static string consoleLogPath => Path.Combine(temporaryCachePath, "Player.log");

  public static event Action<string, string, LogType>? logMessageReceived;
  public static event Action? onBeforeRender;
  public static event Action? quitting;
  public static event Func<bool>? wantsToQuitEvent;
  public static event Action? lowMemory;
  public static event Action<bool>? focusChanged;
  public static event Action<bool>? pauseStatusChanged;
  public static event Action? unloadingUnusedAssets;

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

  public static AsyncOperation LoadLevelAsync(string levelName)
  {
    var op = new AsyncOperation();
    op.isDone = true;
    return op;
  }

  public static AsyncOperation LoadLevelAsync(int index)
  {
    return LoadLevelAsync(index.ToString());
  }

  public static void LoadLevel(string name)
  {
    _ = name;
  }

  public static void LoadLevel(int index)
  {
    _ = index;
  }

  public static string[] GetUnityVersion()
  {
    return unityVersion.Split('.');
  }

  internal static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
  {
    logMessageReceived?.Invoke(condition, stackTrace, type);
  }

  internal static void OnFocusChanged(bool focus)
  {
    isFocused = focus;
    focusChanged?.Invoke(focus);
  }

  internal static void OnPauseChanged(bool pause)
  {
    pauseStatusChanged?.Invoke(pause);
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
