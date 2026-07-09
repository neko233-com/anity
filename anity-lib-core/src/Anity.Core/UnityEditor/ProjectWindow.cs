using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
  public sealed class ProjectWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private Vector2 _rightPaneScroll;
    private readonly List<ProjectFolder> _folders = new List<ProjectFolder>();
    private readonly List<ProjectAsset> _assets = new List<ProjectAsset>();
    private string _searchFilter = string.Empty;
    private int _selectedFolder;
    private int _selectedAsset;
    private float _leftPaneWidth = 200f;
    private bool _isTwoColumn = true;

    public static ProjectWindow instance { get; private set; }

    public ProjectWindow()
    {
      titleContent = new GUIContent("Project");
      minSize = new Vector2(400f, 300f);
      instance = this;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
      BuildDefaultTree();
    }

    protected override void OnGUI()
    {
      DrawToolbar();

      if (_isTwoColumn)
        DrawTwoColumn();
      else
        DrawOneColumn();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      if (GUILayout.Button(EditorGUIUtility.FindTexture("SaveProject"), EditorStyles.toolbarButton))
        AssetDatabase.SaveAssets();

      GUILayout.Button("Create", EditorStyles.toolbarDropDown);
      GUILayout.Button("Packages", EditorStyles.toolbarDropDown);

      GUILayout.FlexibleSpace();

      _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
      if (GUILayout.Button("", EditorStyles.toolbarSearchFieldCancelButton))
        _searchFilter = string.Empty;

      if (GUILayout.Button(_isTwoColumn ? "=" : "≡", EditorStyles.toolbarButton))
        _isTwoColumn = !_isTwoColumn;

      GUILayout.EndHorizontal();
    }

    private void DrawTwoColumn()
    {
      GUILayout.BeginHorizontal();

      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(_leftPaneWidth));
      DrawFolderTree();
      GUILayout.EndScrollView();

      _rightPaneScroll = GUILayout.BeginScrollView(_rightPaneScroll);
      DrawAssetList();
      GUILayout.EndScrollView();

      GUILayout.EndHorizontal();
    }

    private void DrawOneColumn()
    {
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
      DrawFolderTree();
      GUILayout.EndScrollView();
    }

    private void DrawFolderTree()
    {
      foreach (var folder in _folders)
        DrawFolder(folder, 0);
    }

    private void DrawFolder(ProjectFolder folder, int depth)
    {
      var indent = depth * 16f;
      GUILayout.BeginHorizontal();
      GUILayout.Space(indent);

      if (folder.Children.Count > 0)
      {
        if (GUILayout.Button(folder.Expanded ? "▼" : "▶", GUILayout.Width(16f)))
          folder.Expanded = !folder.Expanded;
      }
      else
      {
        GUILayout.Space(16f);
      }

      var selected = _selectedFolder == folder.InstanceID;
      var style = selected ? EditorStyles.highlightLabel : EditorStyles.label;
      if (GUILayout.Button("📁 " + folder.Name, style, GUILayout.ExpandWidth(true)))
      {
        _selectedFolder = folder.InstanceID;
        Selection.activeInstanceID = folder.InstanceID;
      }

      GUILayout.EndHorizontal();

      if (folder.Expanded)
      {
        foreach (var child in folder.Children)
          DrawFolder(child, depth + 1);
      }
    }

    private void DrawAssetList()
    {
      foreach (var asset in _assets)
      {
        var selected = _selectedAsset == asset.InstanceID;
        var style = selected ? EditorStyles.highlightLabel : EditorStyles.label;
        if (GUILayout.Button($"{GetIconForType(asset.Type)} {asset.Name}", style, GUILayout.ExpandWidth(true)))
        {
          _selectedAsset = asset.InstanceID;
          Selection.activeInstanceID = asset.InstanceID;
        }
      }
    }

    private string GetIconForType(Type type)
    {
      if (type == typeof(SceneAsset)) return "📜";
      if (type == typeof(GameObject)) return "📦";
      if (type == typeof(Material)) return "🎨";
      if (type == typeof(Texture2D)) return "🖼️";
      if (type == typeof(Shader)) return "📝";
      if (type == typeof(AnimationClip)) return "🎬";
      if (type == typeof(AudioClip)) return "🔊";
      if (type == typeof(Mesh)) return "🔺";
      if (type == typeof(ScriptableObject)) return "📋";
      if (type.FullName?.Contains("MonoScript") == true) return "📄";
      return "📄";
    }

    private void BuildDefaultTree()
    {
      _folders.Clear();
      _assets.Clear();

      var assets = new ProjectFolder { InstanceID = 1, Name = "Assets", Expanded = true };
      var scenes = new ProjectFolder { InstanceID = 2, Name = "Scenes" };
      var scripts = new ProjectFolder { InstanceID = 3, Name = "Scripts" };
      var materials = new ProjectFolder { InstanceID = 4, Name = "Materials" };
      var textures = new ProjectFolder { InstanceID = 5, Name = "Textures" };
      var prefabs = new ProjectFolder { InstanceID = 6, Name = "Prefabs" };

      assets.Children.Add(scenes);
      assets.Children.Add(scripts);
      assets.Children.Add(materials);
      assets.Children.Add(textures);
      assets.Children.Add(prefabs);
      _folders.Add(assets);

      _assets.Add(new ProjectAsset { InstanceID = 100, Name = "SampleScene.unity", Type = typeof(SceneAsset) });
    }

    [MenuItem("Window/General/Project")]
    public static ProjectWindow ShowWindow()
    {
      return GetWindow<ProjectWindow>("Project");
    }
  }

  internal sealed class ProjectFolder
  {
    public int InstanceID;
    public string Name = string.Empty;
    public bool Expanded;
    public List<ProjectFolder> Children = new List<ProjectFolder>();
  }

  internal sealed class ProjectAsset
  {
    public int InstanceID;
    public string Name = string.Empty;
    public string Guid = string.Empty;
    public string Path = string.Empty;
    public Type Type = typeof(UnityEngine.Object);
    public long Size;
    public DateTime ModifiedTime;
  }

  public sealed class SceneAsset : UnityEngine.Object
  {
  }
}
