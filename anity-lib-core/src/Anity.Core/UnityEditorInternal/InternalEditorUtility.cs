using System;

namespace UnityEditorInternal;

public static class InternalEditorUtility
{
  private static bool _reloading;
  private static bool _proSkin;

  public static bool inBatchMode { get; set; }
  public static bool isHumanControllable => true;
  public static bool isApplicationActive => true;
  public static bool hasProLicense => true;
  public static bool isProSkin
  {
    get => _proSkin;
    set => _proSkin = value;
  }

  public static string unityPreferencesFolder => "UserSettings";
  public static string projectPath => AppDomain.CurrentDomain.BaseDirectory;

  public static event Action? scriptReloaded;

  public static void ReloadAssemblies()
  {
    _reloading = true;
    scriptReloaded?.Invoke();
    _reloading = false;
  }

  public static void RequestScriptReload()
  {
    ReloadAssemblies();
  }

  public static bool IsRecompiling()
  {
    return _reloading;
  }

  public static void OpenFileAtLineExternal(string filename, int line)
  {
    _ = filename;
    _ = line;
  }

  public static void LoadRequiredAdditionalDataToWindow()
  {
  }

  public static void LoadWindowLayout(string path, bool addToStack)
  {
    _ = path;
    _ = addToStack;
  }
}
