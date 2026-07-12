using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace Anity.Editor.Host.Services.Windows;

/// <summary>
/// Thin host wrappers — open the full UnityEditor windows from Anity.Core
/// (Scene / Game / Project / Hierarchy / Inspector / Console / Quick Search).
/// Host no longer uses placeholder OnGUI stubs.
/// </summary>
internal static class UnityStyleWindows
{
  public static EditorWindow OpenSceneView() => SceneView.ShowWindow();

  public static EditorWindow OpenGameView() => GameView.ShowWindow();

  public static EditorWindow OpenHierarchy() => HierarchyWindow.ShowWindow();

  public static EditorWindow OpenProject() => ProjectWindow.ShowWindow();

  public static EditorWindow OpenInspector() => InspectorWindow.ShowWindow();

  public static EditorWindow OpenConsole() => ConsoleWindow.ShowWindow();

  public static EditorWindow OpenSearch() => SearchWindow.Show(null);

  public static EditorWindow OpenByName(string name) => name switch
  {
    "Scene View" or "Scene" => OpenSceneView(),
    "Game View" or "Game" => OpenGameView(),
    "Hierarchy" => OpenHierarchy(),
    "Project" or "Project Browser" => OpenProject(),
    "Inspector" => OpenInspector(),
    "Console" => OpenConsole(),
    "Search" or "Quick Search" => OpenSearch(),
    _ => OpenSceneView()
  };

  public static System.Type ResolveType(string name) => name switch
  {
    "Scene View" or "Scene" => typeof(SceneView),
    "Game View" or "Game" => typeof(GameView),
    "Hierarchy" => typeof(HierarchyWindow),
    "Project" or "Project Browser" => typeof(ProjectWindow),
    "Inspector" => typeof(InspectorWindow),
    "Console" => typeof(ConsoleWindow),
    "Search" or "Quick Search" => typeof(SearchWindow),
    _ => typeof(SceneView)
  };
}
