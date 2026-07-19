using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using EventType = UnityEngine.EventType;

namespace UnityEditor
{
  public sealed class ProjectWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private Vector2 _rightPaneScroll;
    private Vector2 _favoritesScroll;
    private readonly List<ProjectFolder> _folders = new();
    private readonly List<ProjectAsset> _assets = new();
    private readonly List<string> _favorites = new();
    private string _searchFilter = string.Empty;
    private int _selectedFolder;
    private int _selectedAsset;
    private string _selectedFolderPath = "Assets";
    private float _leftPaneWidth = 200f;
    private bool _isTwoColumn = true;
    private bool _isGridView = true;
    private int _iconSize = 64;
    private bool _draggingSplitter;
    private double _lastClickTime;
    private readonly HashSet<int> _expandedFolders = new();

    private static readonly Dictionary<Type, string> IconNames = new()
    {
      { typeof(SceneAsset), "SceneAsset Icon" },
      { typeof(GameObject), "Prefab Icon" },
      { typeof(Material), "Material Icon" },
      { typeof(Texture2D), "Texture2D Icon" },
      { typeof(Texture), "Texture Icon" },
      { typeof(Shader), "Shader Icon" },
      { typeof(AnimationClip), "AnimationClip Icon" },
      { typeof(AudioClip), "AudioClip Icon" },
      { typeof(Mesh), "Mesh Icon" },
      { typeof(ScriptableObject), "ScriptableObject Icon" },
      { typeof(MonoScript), "cs Script Icon" },
      { typeof(UnityEngine.TextAsset), "TextAsset Icon" },
      { typeof(UnityEngine.Font), "Font Icon" },
    };

    public static ProjectWindow? instance { get; private set; }

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
      BuildFavorites();
    }

    protected override void OnGUI()
    {
      DrawToolbar();
      DrawBreadcrumb();

      if (_isTwoColumn)
        DrawTwoColumn();
      else
        DrawOneColumn();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40f)))
        AssetDatabase.SaveAssets();

      if (GUILayout.Button("Create", EditorStyles.toolbarDropDown, GUILayout.Width(60f)))
      {
        ShowCreateMenu();
      }

      GUILayout.FlexibleSpace();

      _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
      if (GUILayout.Button("X", EditorStyles.toolbarSearchFieldCancelButton, GUILayout.Width(16f)))
        _searchFilter = string.Empty;

      if (GUILayout.Button(_isGridView ? "Grid" : "List", EditorStyles.toolbarButton, GUILayout.Width(40f)))
        _isGridView = !_isGridView;

      if (GUILayout.Button(_isTwoColumn ? "Two" : "One", EditorStyles.toolbarButton, GUILayout.Width(36f)))
        _isTwoColumn = !_isTwoColumn;

      GUILayout.EndHorizontal();
    }

    private void DrawBreadcrumb()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      var parts = _selectedFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      string currentPath = string.Empty;

      for (int i = 0; i < parts.Length; i++)
      {
        if (i > 0)
        {
          GUILayout.Label(">", EditorStyles.miniLabel, GUILayout.Width(12f));
        }

        string part = parts[i];
        currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
        var style = (i == parts.Length - 1) ? EditorStyles.boldLabel : EditorStyles.miniLabel;

        if (GUILayout.Button(part, style, GUILayout.ExpandWidth(false)))
        {
          _selectedFolderPath = currentPath;
          RefreshAssetList();
        }
      }

      GUILayout.FlexibleSpace();
      GUILayout.Label($"{_assets.Count} items", EditorStyles.miniLabel);

      GUILayout.EndHorizontal();
    }

    private void DrawTwoColumn()
    {
      GUILayout.BeginHorizontal();

      GUILayout.BeginVertical(GUILayout.Width(_leftPaneWidth), GUILayout.ExpandHeight(true));
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

      GUILayout.Label("Favorites", EditorStyles.miniBoldLabel);
      DrawFavorites();

      GUILayout.Space(8f);
      DrawFolderTree();

      GUILayout.EndScrollView();
      GUILayout.EndVertical();

      var splitterRect = GUILayoutUtility.GetRect(4f, 100f);
      splitterRect.width = 4f;
      EditorGUI.DrawRect(splitterRect, new Color(0.3f, 0.3f, 0.3f));
      HandleSplitterDrag(splitterRect);

      _rightPaneScroll = GUILayout.BeginScrollView(_rightPaneScroll, GUILayout.ExpandHeight(true));
      DrawAssetList();
      GUILayout.EndScrollView();

      GUILayout.EndHorizontal();
    }

    private void DrawOneColumn()
    {
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

      GUILayout.Label("Favorites", EditorStyles.miniBoldLabel);
      DrawFavorites();
      GUILayout.Space(8f);

      DrawFolderTree();
      GUILayout.Space(8f);
      DrawAssetList();

      GUILayout.EndScrollView();
    }

    private void HandleSplitterDrag(Rect splitterRect)
    {
      var evt = Event.current;
      if (evt == null) return;
      if (evt.type == UnityEngine.EventType.MouseDown && splitterRect.Contains(evt.mousePosition))
      {
        _draggingSplitter = true;
        evt.Use();
      }
      else if (evt.type == UnityEngine.EventType.MouseDrag && _draggingSplitter)
      {
        _leftPaneWidth = Mathf.Clamp(evt.mousePosition.x, 150f, 400f);
        evt.Use();
      }
      else if (evt.type == UnityEngine.EventType.MouseUp)
      {
        _draggingSplitter = false;
      }
    }

    private void DrawFavorites()
    {
      _favoritesScroll = GUILayout.BeginScrollView(_favoritesScroll, GUILayout.Height(80f));

      foreach (var fav in _favorites)
      {
        bool isSelected = _selectedFolderPath == fav;
        var style = isSelected ? EditorStyles.whiteLabel : EditorStyles.label;

        GUILayout.BeginHorizontal();
        GUILayout.Space(4f);
        var icon = EditorGUIUtility.FindTexture("Favorite Icon");
        GUILayout.Box(icon, GUILayout.Width(16f), GUILayout.Height(16f));
        if (GUILayout.Button(Path.GetFileName(fav), style, GUILayout.ExpandWidth(true)))
        {
          _selectedFolderPath = fav;
          RefreshAssetList();
        }
        GUILayout.EndHorizontal();
      }

      GUILayout.EndScrollView();
    }

    private void BuildFavorites()
    {
      _favorites.Clear();
      _favorites.Add("Assets");
    }

    private void DrawFolderTree()
    {
      foreach (var folder in _folders)
        DrawFolder(folder, 0);
    }

    private void DrawFolder(ProjectFolder folder, int depth)
    {
      float indent = depth * 14f;
      bool isSelected = _selectedFolder == folder.InstanceID;
      bool isExpanded = _expandedFolders.Contains(folder.InstanceID);

      GUILayout.BeginHorizontal();
      GUILayout.Space(indent);

      if (folder.Children.Count > 0)
      {
        if (GUILayout.Button(isExpanded ? "v" : ">", EditorStyles.label, GUILayout.Width(14f), GUILayout.Height(16f)))
        {
          if (isExpanded)
            _expandedFolders.Remove(folder.InstanceID);
          else
            _expandedFolders.Add(folder.InstanceID);
        }
      }
      else
      {
        GUILayout.Space(14f);
      }

      var folderIcon = EditorGUIUtility.FindTexture("Folder Icon");
      GUILayout.Box(folderIcon, GUILayout.Width(16f), GUILayout.Height(16f));

      var style = isSelected ? EditorStyles.whiteLabel : EditorStyles.label;
      if (GUILayout.Button(folder.Name, style, GUILayout.ExpandWidth(true)))
      {
        _selectedFolder = folder.InstanceID;
        _selectedFolderPath = folder.Path;
        Selection.activeInstanceID = folder.InstanceID;
        RefreshAssetList();
      }

      GUILayout.EndHorizontal();

      if (isExpanded && folder.Children.Count > 0)
      {
        foreach (var child in folder.Children)
          DrawFolder(child, depth + 1);
      }
    }

    private void DrawAssetList()
    {
      var filteredAssets = _assets.Where(a =>
        string.IsNullOrEmpty(_searchFilter) ||
        a.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
      ).ToList();

      if (_isGridView)
      {
        DrawAssetGrid(filteredAssets);
      }
      else
      {
        DrawAssetListView(filteredAssets);
      }
    }

    private void DrawAssetGrid(List<ProjectAsset> assets)
    {
      float padding = 8f;
      float itemWidth = _iconSize + padding * 2;
      float itemHeight = _iconSize + 32f;

      int columns = Mathf.Max(1, (int)Mathf.Max(200f, position.width - _leftPaneWidth - 20f) / (int)itemWidth);

      int col = 0;
      GUILayout.BeginHorizontal();

      foreach (var asset in assets)
      {
        if (col >= columns)
        {
          GUILayout.EndHorizontal();
          GUILayout.BeginHorizontal();
          col = 0;
        }

        DrawGridAssetItem(asset, itemWidth, itemHeight);
        col++;
      }

      GUILayout.EndHorizontal();
    }

    private void DrawGridAssetItem(ProjectAsset asset, float width, float height)
    {
      bool isSelected = _selectedAsset == asset.InstanceID;
      var bgStyle = isSelected ? EditorStyles.treeViewItemSelected : EditorStyles.treeView;

      GUILayout.BeginVertical(bgStyle, GUILayout.Width(width), GUILayout.Height(height));

      var icon = GetPreviewIcon(asset);
      var iconRect = GUILayoutUtility.GetRect(_iconSize, _iconSize);
      EditorGUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

      var labelStyle = isSelected ? EditorStyles.whiteLabel : EditorStyles.label;
      GUILayout.Label(asset.Name, labelStyle, GUILayout.Width(width - 8f));

      var evt = Event.current;
      if (evt != null)
      {
        var lastRect = GUILayoutUtility.GetLastRect();
        if (lastRect.Contains(evt.mousePosition))
        {
          if (evt.type == UnityEngine.EventType.MouseDown)
          {
            SelectAsset(asset);
            if (evt.clickCount == 2)
              OpenAsset(asset);
            evt.Use();
          }
        }
      }

      GUILayout.EndVertical();
    }

    private void DrawAssetListView(List<ProjectAsset> assets)
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);
      GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(200f));
      GUILayout.Label("Size", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
      GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
      GUILayout.Label("Modified", EditorStyles.miniBoldLabel);
      GUILayout.EndHorizontal();

      foreach (var asset in assets)
      {
        bool isSelected = _selectedAsset == asset.InstanceID;
        var bgStyle = isSelected ? EditorStyles.treeViewItemSelected : EditorStyles.treeView;

        GUILayout.BeginHorizontal(bgStyle);

        var icon = GetPreviewIcon(asset);
        GUILayout.Box(icon, GUILayout.Width(16f), GUILayout.Height(16f));

        var labelStyle = isSelected ? EditorStyles.whiteLabel : EditorStyles.label;
        if (GUILayout.Button(asset.Name, labelStyle, GUILayout.Width(180f)))
        {
          SelectAsset(asset);
          var evt = Event.current;
          if (evt != null && evt.clickCount == 2)
            OpenAsset(asset);
        }

        GUILayout.Label(FormatSize(asset.Size), EditorStyles.miniLabel, GUILayout.Width(80f));
        GUILayout.Label(asset.Type?.Name ?? "Unknown", EditorStyles.miniLabel, GUILayout.Width(120f));
        GUILayout.Label(asset.ModifiedTime.ToString("yyyy-MM-dd HH:mm"), EditorStyles.miniLabel);

        GUILayout.EndHorizontal();
      }
    }

    private Texture2D GetPreviewIcon(ProjectAsset asset)
    {
      if (asset.Type != null && IconNames.TryGetValue(asset.Type, out var iconName))
        return EditorGUIUtility.FindTexture(iconName);

      return EditorGUIUtility.FindTexture("DefaultAsset Icon");
    }

    private void SelectAsset(ProjectAsset asset)
    {
      _selectedAsset = asset.InstanceID;
      var obj = AssetDatabase.LoadMainAssetAtPath(asset.Path);
      if (obj != null)
      {
        Selection.SetActiveObject(obj);
        Selection.activeInstanceID = asset.InstanceID;
      }

      if (InspectorWindow.instance != null)
        InspectorWindow.instance.Repaint();
    }

    private void OpenAsset(ProjectAsset asset)
    {
      if (asset == null || string.IsNullOrEmpty(asset.Path)) return;

      var ext = Path.GetExtension(asset.Path).ToLowerInvariant();

      // Prefab Mode — double-click .prefab enters PrefabStage (Isolation)
      if (ext == ".prefab")
      {
        var stage = PrefabStage.OpenPrefab(asset.Path, PrefabStageMode.InIsolation);
        if (stage != null)
        {
          SceneView.ShowWindow();
          HierarchyWindow.ShowWindow();
          return;
        }
      }

      if (asset.Type == typeof(SceneAsset) || ext == ".unity")
      {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
          EditorSceneManager.OpenScene(asset.Path);
        }
        return;
      }

      // Media: select AudioClip / VideoClip so Inspector can preview
      if (MediaFormatUtility.IsSupportedAudioExtension(ext) ||
          MediaFormatUtility.IsSupportedVideoExtension(ext))
      {
        var mediaObj = AssetDatabase.LoadMainAssetAtPath(asset.Path);
        if (mediaObj == null && MediaFormatUtility.IsSupportedAudioExtension(ext))
        {
          mediaObj = AudioClip.CreateFromFile(asset.Path);
          if (mediaObj != null)
            AssetDatabase.CreateAsset(mediaObj, asset.Path);
        }
        if (mediaObj == null && MediaFormatUtility.IsSupportedVideoExtension(ext))
        {
          mediaObj = MediaFormatUtility.CreateVideoClipFromPath(asset.Path);
          if (mediaObj != null)
            AssetDatabase.CreateAsset(mediaObj, asset.Path);
        }
        if (mediaObj != null)
          Selection.SetActiveObject(mediaObj);
        return;
      }

      var obj = AssetDatabase.LoadMainAssetAtPath(asset.Path);
      if (obj != null)
      {
        Selection.SetActiveObject(obj);
      }
    }

    private string FormatSize(long bytes)
    {
      if (bytes < 1024) return $"{bytes} B";
      if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
      if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
      return $"{bytes / (1024f * 1024f * 1024f):F1} GB";
    }

    private void ShowCreateMenu()
    {
      var menu = new GenericMenu();
      menu.AddItem("Folder", false, (GenericMenu.MenuFunction)(() => CreateFolder()));
      menu.AddSeparator();
      menu.AddItem("C# Script", false, (GenericMenu.MenuFunction)(() => CreateAsset("NewBehaviourScript", "cs")));
      menu.AddItem("Shader", false, (GenericMenu.MenuFunction)(() => CreateAsset("NewShader", "shader")));
      menu.AddItem("Material", false, (GenericMenu.MenuFunction)(() => CreateAsset("New Material", "mat")));
      menu.AddItem("Scene", false, (GenericMenu.MenuFunction)(() => CreateScene()));
      menu.AddSeparator();
      menu.AddItem("Animation", false, (GenericMenu.MenuFunction)(() => { }));
      menu.AddItem("Animator Controller", false, (GenericMenu.MenuFunction)(() => { }));
      menu.AddItem("Assembly Definition", false, (GenericMenu.MenuFunction)(() => { }));
      menu.ShowAsContext();
    }

    private void CreateFolder()
    {
      string newFolderPath = AssetDatabase.CreateFolder(_selectedFolderPath, "New Folder");
      RefreshFolderTree();
      RefreshAssetList();
    }

    private void CreateAsset(string name, string extension)
    {
      string path = Path.Combine(_selectedFolderPath, $"{name}.{extension}").Replace('\\', '/');
      path = AssetDatabase.GenerateUniqueAssetPath(path);
      AssetDatabase.CreateAsset(new TextAsset(), path);
      RefreshAssetList();
    }

    private void CreateScene()
    {
      string path = Path.Combine(_selectedFolderPath, "New Scene.unity").Replace('\\', '/');
      path = AssetDatabase.GenerateUniqueAssetPath(path);
      var sceneAsset = new SceneAsset();
      AssetDatabase.CreateAsset(sceneAsset, path);
      RefreshAssetList();
    }

    public void RefreshFolderTree()
    {
      BuildDefaultTree();
    }

    public void RefreshAssetList()
    {
      _assets.Clear();
      var allPaths = AssetDatabase.GetAllAssetPaths();

      foreach (var path in allPaths)
      {
        if (!path.StartsWith(_selectedFolderPath + "/", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(path, _selectedFolderPath, StringComparison.OrdinalIgnoreCase))
          continue;

        var fileName = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var asset = AssetDatabase.LoadMainAssetAtPath(path);

        if (ext == string.Empty || (asset == null && AssetDatabase.IsValidFolder(path)))
          continue;

        bool isDirectChild = Path.GetDirectoryName(path)?.Replace('\\', '/') == _selectedFolderPath;
        if (!isDirectChild) continue;

        var type = asset?.GetType() ?? GetTypeFromExtension(ext);

        long size = 0;
        DateTime modified = DateTime.Now;
        try
        {
          if (File.Exists(path))
          {
            var fileInfo = new FileInfo(path);
            size = fileInfo.Length;
            modified = fileInfo.LastWriteTime;
          }
        }
        catch { }

        _assets.Add(new ProjectAsset
        {
          InstanceID = path.GetHashCode(),
          Name = fileName,
          Guid = AssetDatabase.AssetPathToGUID(path),
          Path = path,
          Type = type ?? typeof(UnityEngine.Object),
          Size = size,
          ModifiedTime = modified
        });
      }
    }

    private Type GetTypeFromExtension(string ext)
    {
      switch (ext)
      {
        case ".unity": return typeof(SceneAsset);
        case ".cs": return typeof(MonoScript);
        case ".mat": return typeof(Material);
        case ".png":
        case ".jpg":
        case ".jpeg":
        case ".tga":
        case ".psd": return typeof(Texture2D);
        case ".shader": return typeof(Shader);
        case ".anim": return typeof(AnimationClip);
        case ".wav":
        case ".mp3":
        case ".ogg":
        case ".oga":
        case ".aac":
        case ".m4a":
        case ".flac": return typeof(AudioClip);
        case ".mp4":
        case ".m4v":
        case ".webm":
        case ".mov":
        case ".avi": return typeof(UnityEngine.Video.VideoClip);
        case ".fbx":
        case ".obj": return typeof(Mesh);
        case ".prefab": return typeof(GameObject);
        case ".asset": return typeof(ScriptableObject);
        // Compressed texture import types (ASTC / ETC / DXT / BC)
        case ".astc":
        case ".ktx":
        case ".ktx2":
        case ".dds":
        case ".pvr": return typeof(Texture2D);
        default: return typeof(UnityEngine.Object);
      }
    }

    private void BuildDefaultTree()
    {
      _folders.Clear();
      _expandedFolders.Add(1);

      var assets = new ProjectFolder { InstanceID = 1, Name = "Assets", Path = "Assets" };

      var allPaths = AssetDatabase.GetAllAssetPaths();
      var allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var path in allPaths)
      {
        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        while (!string.IsNullOrEmpty(dir) && dir != ".")
        {
          allFolders.Add(dir);
          dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
        }
      }

      var folderMap = new Dictionary<string, ProjectFolder>(StringComparer.OrdinalIgnoreCase)
      {
        ["Assets"] = assets
      };

      int nextId = 2;
      var sortedFolders = allFolders
        .Where(f => f.StartsWith("Assets/"))
        .OrderBy(f => f.Count(c => c == '/'))
        .ThenBy(f => f, StringComparer.OrdinalIgnoreCase);

      foreach (var folderPath in sortedFolders)
      {
        var parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        var folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrEmpty(parentPath) || !folderMap.ContainsKey(parentPath))
          continue;

        var folder = new ProjectFolder
        {
          InstanceID = nextId++,
          Name = folderName,
          Path = folderPath
        };
        folderMap[folderPath] = folder;
        folderMap[parentPath].Children.Add(folder);
      }

      _folders.Add(assets);
      RefreshAssetList();
    }

    private ProjectFolder? FindFolder(int instanceID)
    {
      return FindFolderRecursive(_folders, instanceID);
    }

    private ProjectFolder? FindFolderRecursive(List<ProjectFolder> folders, int instanceID)
    {
      foreach (var folder in folders)
      {
        if (folder.InstanceID == instanceID) return folder;
        var found = FindFolderRecursive(folder.Children, instanceID);
        if (found != null) return found;
      }
      return null;
    }

    protected override void OnProjectChange()
    {
      RefreshFolderTree();
      RefreshAssetList();
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
    public string Path = string.Empty;
    public bool Expanded;
    public List<ProjectFolder> Children = new();
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

  [UnityEngine.Bindings.NativeType("Editor/Mono/MonoScript.bindings.h")]
  [UnityEngine.ExcludeFromPreset]
  [UnityEngine.NativeClass(null)]
  public class MonoScript : UnityEngine.TextAsset
  {
    private static readonly Dictionary<Type, MonoScript> ScriptsByType = new();
    private static readonly object ScriptsLock = new();
    private readonly Type? _class;

    public MonoScript() { }
    private MonoScript(Type scriptClass)
    {
      _class = scriptClass;
      name = scriptClass.Name;
    }

    public Type? GetClass() => _class;

    public static MonoScript? FromMonoBehaviour(MonoBehaviour behaviour)
      => behaviour == null ? null : GetOrCreate(behaviour.GetType());

    public static MonoScript? FromScriptableObject(ScriptableObject scriptableObject)
      => scriptableObject == null ? null : GetOrCreate(scriptableObject.GetType());

    private static MonoScript GetOrCreate(Type scriptClass)
    {
      lock (ScriptsLock)
      {
        if (!ScriptsByType.TryGetValue(scriptClass, out var script))
          ScriptsByType.Add(scriptClass, script = new MonoScript(scriptClass));
        return script;
      }
    }
  }
}
