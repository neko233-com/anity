using System.Runtime.InteropServices;
using System;
using System.IO;

namespace UnityEngine;

public static class Application
{
  public static string dataPath => AppContext.BaseDirectory;
  public static string streamingAssetsPath => Path.Combine(AppContext.BaseDirectory, "StreamingAssets");
  public static string persistentDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Anity");
  public static string temporaryCachePath => Path.GetTempPath();
  public static RuntimePlatform platform => RuntimePlatformFromOS();
  public static bool isPlaying => true;
  public static bool isEditor => false;
  public static bool isMobilePlatform => platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;
  public static bool isConsolePlatform => false;
  public static bool runInBackground { get; set; }
  public static int targetFrameRate { get; set; } = -1;
  public static int sleepTimeout { get; set; }
  public static bool genuine => true;
  public static bool genuineCheckAvailable => true;
  public static string absoluteURL { get; set; } = string.Empty;
  public static string identifier => string.Empty;
  public static string companyName => "Anity";
  public static string productName => "Anity";
  public static string version => "1.0.0";
  public static string unityVersion => "2022.3";
  public static SystemLanguage systemLanguage { get; set; } = SystemLanguage.ChineseSimplified;
  public static NetworkReachability internetReachability { get; set; } = NetworkReachability.ReachableViaLocalAreaNetwork;
  public static bool backgroundLoadingPriority { get; set; }

  public static event Action<string, string, LogType>? logMessageReceived;
  public static event Action? onBeforeRender;
  public static event Action? quitting;
  public static event Action? lowMemory;
  public static event Action<bool>? focusChanged;
  public static event Action<bool>? pauseStatusChanged;
  public static event Action? unloadingUnusedAssets;

  public static void Quit()
  {
    quitting?.Invoke();
  }

  public static void Quit(int exitCode)
  {
    _ = exitCode;
    Quit();
  }

  public static void CancelQuit()
  {
  }

  public static void Unload()
  {
    unloadingUnusedAssets?.Invoke();
  }

  public static void OpenURL(string url)
  {
    _ = url;
  }

  public static string[] GetUnityVersion()
  {
    return unityVersion.Split('.');
  }

  private static RuntimePlatform RuntimePlatformFromOS()
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
