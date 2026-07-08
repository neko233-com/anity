using UnityEditor;

namespace Anity.Editor.Host.Services;

internal static class EditorMenuCommands
{
  [MenuItem("Window/Scene View")]
  public static void OpenSceneView()
  {
    EditorWindow.GetWindow<Windows.SceneViewWindow>(true, "Scene View");
  }

  [MenuItem("Window/Hierarchy")]
  public static void OpenHierarchy()
  {
    EditorWindow.GetWindow<Windows.HierarchyWindow>(true, "Hierarchy");
  }

  [MenuItem("Window/Project")]
  public static void OpenProjectBrowser()
  {
    EditorWindow.GetWindow<Windows.ProjectWindow>(true, "Project");
  }

  [MenuItem("Window/Console")]
  public static void OpenConsole()
  {
    EditorWindow.GetWindow<Windows.ConsoleWindow>(true, "Console");
  }

  [MenuItem("Window/Inspector")]
  public static void OpenInspector()
  {
    EditorWindow.GetWindow<Windows.InspectorWindow>(true, "Inspector");
  }
}
