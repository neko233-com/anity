using UnityEditor;
using UnityEngine;

namespace Anity.Editor.Host.Services.Windows;

internal sealed class SceneViewWindow : EditorWindow
{
  protected override void OnEnable()
  {
    titleContent = new GUIContent("Scene");
  }

  protected override void OnGUI()
  {
    EditorGUILayout.LabelField("Window", "Scene");
    EditorGUILayout.LabelField("Camera", $"count={EditorWindow.GetWindows().Count}");
    if (EditorGUILayout.Button("Focus"))
    {
      FocusWindowIfItsOpen<SceneViewWindow>();
    }
  }
}

internal sealed class HierarchyWindow : EditorWindow
{
  protected override void OnEnable()
  {
    titleContent = new GUIContent("Hierarchy");
  }

  protected override void OnGUI()
  {
    EditorGUILayout.LabelField("Window", "Hierarchy");
    EditorGUILayout.LabelField("Objects", "SampleRoot/Player");
  }
}

internal sealed class ProjectWindow : EditorWindow
{
  protected override void OnEnable()
  {
    titleContent = new GUIContent("Project");
  }

  protected override void OnGUI()
  {
    EditorGUILayout.LabelField("Window", "Project Browser");
    if (EditorGUILayout.Button("Refresh"))
    {
      UnityEditor.EditorApplication.ForceReload();
    }
  }
}

internal sealed class ConsoleWindow : EditorWindow
{
  protected override void OnEnable()
  {
    titleContent = new GUIContent("Console");
  }

  protected override void OnGUI()
  {
    EditorGUILayout.LabelField("Window", "Console");
    EditorGUILayout.LabelField("Entries", "No issues");
  }
}

internal sealed class InspectorWindow : EditorWindow
{
  protected override void OnEnable()
  {
    titleContent = new GUIContent("Inspector");
  }

  protected override void OnGUI()
  {
    EditorGUILayout.LabelField("Window", "Inspector");
    EditorGUILayout.LabelField("Selected", "None");
  }
}

