using System;

namespace UnityEditor;

public sealed class BuildPlayerWindow : EditorWindow
{
  public static BuildPlayerWindow ShowBuildPlayerWindow()
  {
    var window = new BuildPlayerWindow();
    window.Show();
    return window;
  }

  public void BuildPlayer()
  {
    BuildPipeline.BuildPlayer(new BuildPlayerOptions());
  }

  public static void OpenProject(string path)
  {
    _ = path;
  }

  public static bool CanBuildPlayer => true;
}
