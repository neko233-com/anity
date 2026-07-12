using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor;

public static class EditorBuildSettings
{
  public sealed class EditorBuildSettingsScene
  {
    public string path { get; set; }
    public bool enabled { get; set; }
    public string guid { get; set; }

    public EditorBuildSettingsScene() { }

    public EditorBuildSettingsScene(string path, bool enabled, string guid = "")
    {
      this.path = path;
      this.enabled = enabled;
      this.guid = guid ?? string.Empty;
    }
  }

  private static List<EditorBuildSettingsScene> _scenes = [];

  public static EditorBuildSettingsScene[] scenes
  {
    get => _scenes.ToArray();
    set => _scenes = value?.ToList() ?? [];
  }

  public static EditorBuildSettingsScene[] enabledScenes
  {
    get => _scenes.Where(s => s.enabled).ToArray();
  }

  public static int selectedSceneIndex { get; set; }

  public static EditorBuildSettingsScene GetSceneByPath(string path)
  {
    return _scenes.FirstOrDefault(s => s.path == path);
  }

  public static EditorBuildSettingsScene GetSceneByGUID(string guid)
  {
    return _scenes.FirstOrDefault(s => s.guid == guid);
  }

  public static EditorBuildSettingsScene[] GetScenes()
  {
    return _scenes.ToArray();
  }

  public static void AddScene(EditorBuildSettingsScene scene)
  {
    if (scene is null) throw new ArgumentNullException(nameof(scene));
    _scenes.Add(scene);
  }

  public static bool RemoveScene(EditorBuildSettingsScene scene)
  {
    if (scene is null) throw new ArgumentNullException(nameof(scene));
    return _scenes.Remove(scene);
  }

  public static void MoveScene(int index, EditorBuildSettingsScene scene)
  {
    if (scene is null) throw new ArgumentNullException(nameof(scene));
    if (index < 0 || index > _scenes.Count)
      throw new ArgumentOutOfRangeException(nameof(index));
    _scenes.Remove(scene);
    _scenes.Insert(index, scene);
  }

  public static EditorBuildSettingsScene activeScene => scenes.Length > 0
    ? scenes[selectedSceneIndex]
    : null;

  public static EditorBuildSettingsScene GetActiveScene()
  {
    return activeScene;
  }

  public static string GetEditorBuildSettingsScenePath(int index)
  {
    if (index < 0 || index >= _scenes.Count)
      throw new ArgumentOutOfRangeException(nameof(index));
    return _scenes[index].path;
  }

  public static string[] GetScenePaths()
  {
    return _scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
  }
}
