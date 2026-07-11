using System;
using System.Collections.Generic;
using System.IO;

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
  public static string[] tags => new[] { "Untagged", "Respawn", "Finish", "Editor", "MainCamera", "Player", "GameManager" };
  public static string[] layers => new[] { "Default", "TransparentFX", "Ignore Raycast", "Water", "UI" };
  public static string[] sortingLayerNames => new[] { "Default" };
  public static int[] sortingLayerUniqueIDs => new[] { 0 };
  public static string[] asmrefGUIDs => Array.Empty<string>();
  public static string[] assemblyNames => new[] { "Assembly-CSharp", "Assembly-CSharp-Editor" };

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

  private static int _repaintCount;
  private static bool _additionalDataLoaded;

  public static void LoadRequiredAdditionalDataToWindow()
  {
    _additionalDataLoaded = true;
  }

  public static void LoadWindowLayout(string path, bool addToStack)
  {
    _ = path;
    _ = addToStack;
  }

  public static string[] GetAllGlobalTags()
  {
    return tags;
  }

  public static string[] GetAllLayers()
  {
    return layers;
  }

  public static string TagToLayer(string tag)
  {
    _ = tag;
    return "Default";
  }

  public static string LayerToTag(string layer)
  {
    _ = layer;
    return "Untagged";
  }

  public static bool IsNativeModule(string assemblyName)
  {
    _ = assemblyName;
    return false;
  }

  public static string[] GetScriptAssemblies()
  {
    return assemblyNames;
  }

  public static string[] GetEditorScriptAssemblies()
  {
    return new[] { "Assembly-CSharp-Editor" };
  }

  public static string[] GetRuntimeScriptAssemblies()
  {
    return new[] { "Assembly-CSharp" };
  }

  public static string GetAssemblyPath(string assemblyName)
  {
    _ = assemblyName;
    return string.Empty;
  }

  public static string[] GetAssemblies()
  {
    return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();
  }

  public static bool IsInEditor()
  {
    return true;
  }

  public static bool IsInPlayer()
  {
    return false;
  }

  public static string[] GetPlatformDefines(string platformName)
  {
    _ = platformName;
    return Array.Empty<string>();
  }

  public static string[] GetDefinesForAssembly(string assemblyName)
  {
    _ = assemblyName;
    return Array.Empty<string>();
  }

  public static string[] GetPredefinedDefines()
  {
    return new[] { "UNITY_EDITOR", "UNITY_STANDALONE", "UNITY_2022_3_OR_NEWER" };
  }

  public static void RepaintAll()
  {
    _repaintCount++;
  }

  public static void SetDirty(UnityEngine.Object obj)
  {
    _ = obj;
  }

  public static bool IsObjectAManagedReference(UnityEngine.Object obj)
  {
    _ = obj;
    return false;
  }

  public static string[] GetSerializedObjectProperties(UnityEngine.Object obj)
  {
    _ = obj;
    return Array.Empty<string>();
  }

  public static string GetActiveSceneName()
  {
    return "Untitled";
  }

  public static string[] GetOpenScenes()
  {
    return Array.Empty<string>();
  }

  public static bool IsSceneSaved(string scenePath)
  {
    _ = scenePath;
    return true;
  }

  public static string GetSceneAssetPath(string scenePath)
  {
    _ = scenePath;
    return scenePath;
  }

  public static string[] FindAssets(string filter)
  {
    _ = filter;
    return Array.Empty<string>();
  }

  public static string[] FindAssets(string filter, string[] searchInFolders)
  {
    _ = filter;
    _ = searchInFolders;
    return Array.Empty<string>();
  }

  public static string GetAssetPath(string guid)
  {
    _ = guid;
    return string.Empty;
  }

  public static string GUIDToAssetPath(string guid)
  {
    _ = guid;
    return string.Empty;
  }

  public static string AssetPathToGUID(string path)
  {
    _ = path;
    return string.Empty;
  }
}
