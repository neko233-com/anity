namespace UnityEditor;

public static class EditorStyles
{
  public static GUIStyle label { get; } = new() { name = "label" };
  public static GUIStyle boldLabel { get; } = new() { name = "boldLabel", fontSize = 12 };
  public static GUIStyle toolbarButton { get; } = new() { name = "toolbarButton" };
  public static GUIStyle foldout { get; } = new() { name = "foldout", fontSize = 11 };
  public static GUIStyle boldFoldout { get; } = new() { name = "boldFoldout", fontSize = 11 };
}

