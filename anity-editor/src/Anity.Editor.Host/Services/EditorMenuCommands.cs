using UnityEditor;
using UnityEditor.Search;
using UnityEditor.SceneManagement;

namespace Anity.Editor.Host.Services;

/// <summary>
/// Host menu commands — route to full Unity 2022-style editor windows.
/// </summary>
internal static class EditorMenuCommands
{
  [MenuItem("Window/General/Scene %#1")]
  public static void OpenSceneView() => SceneView.ShowWindow();

  [MenuItem("Window/General/Game %#2")]
  public static void OpenGameView() => GameView.ShowWindow();

  [MenuItem("Window/General/Hierarchy %#4")]
  public static void OpenHierarchy() => HierarchyWindow.ShowWindow();

  [MenuItem("Window/General/Project %#5")]
  public static void OpenProjectBrowser() => ProjectWindow.ShowWindow();

  [MenuItem("Window/General/Inspector %#3")]
  public static void OpenInspector() => InspectorWindow.ShowWindow();

  [MenuItem("Window/General/Console %#c")]
  public static void OpenConsole() => ConsoleWindow.ShowWindow();

  [MenuItem("Edit/Search All... _%k")] // Ctrl+K
  public static void OpenQuickSearch() => SearchService.OpenQuickSearch();

  [MenuItem("Assets/Open Prefab Mode %#p")]
  public static void OpenPrefabMode()
  {
    var go = Selection.activeGameObject;
    if (go != null && PrefabStageUtility.EnterPrefabMode(go))
      return;

    var obj = Selection.activeObject;
    if (obj == null) return;
    string path = AssetDatabase.GetAssetPath(obj);
    if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
      PrefabStage.OpenPrefab(path);
  }
}
