using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EventType = UnityEngine.EventType;

namespace UnityEditor
{
  public sealed class HierarchyWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private readonly List<HierarchyItem> _items = new();
    private int _selectedInstanceID;
    private string _searchFilter = string.Empty;
    private bool _alphabeticalSort;
    private int _contextMenuInstanceID;
    private bool _showContextMenu;
    private readonly Dictionary<int, bool> _foldoutStates = new();
    private int _hoveredInstanceID = -1;

    private static readonly string[] CreateMenuCategories = new[]
    {
      "3D Object/Cube",
      "3D Object/Sphere",
      "3D Object/Capsule",
      "3D Object/Cylinder",
      "3D Object/Plane",
      "3D Object/Quad",
      "2D Object/Sprite",
      "Light/Directional Light",
      "Light/Point Light",
      "Light/Spot Light",
      "Audio/Audio Source",
      "Audio/Audio Listener",
      "UI/Canvas",
      "UI/Panel",
      "UI/Button",
      "UI/Text",
      "UI/Image",
      "Create Empty",
      "Create Empty Child"
    };

    public static HierarchyWindow? instance { get; private set; }

    public HierarchyWindow()
    {
      titleContent = new GUIContent("Hierarchy");
      minSize = new Vector2(200f, 200f);
      instance = this;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
      Selection.selectionChanged += OnSelectionChanged;
      EditorApplication.hierarchyChanged += RebuildHierarchy;
      RebuildHierarchy();
    }

    protected override void OnDisable()
    {
      base.OnDisable();
      Selection.selectionChanged -= OnSelectionChanged;
      EditorApplication.hierarchyChanged -= RebuildHierarchy;
    }

    protected override void OnGUI()
    {
      DrawToolbar();
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
      DrawItems();
      GUILayout.EndScrollView();

      HandleContextMenu();
      HandleHoverAndRename();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150f));
      if (GUILayout.Button("X", EditorStyles.toolbarSearchFieldCancelButton, GUILayout.Width(16f)))
        _searchFilter = string.Empty;

      GUILayout.FlexibleSpace();

      if (GUILayout.Button(_alphabeticalSort ? "A-Z" : "I-|", EditorStyles.toolbarButton, GUILayout.Width(36f)))
      {
        _alphabeticalSort = !_alphabeticalSort;
        RebuildHierarchy();
      }

      if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24f)))
      {
        _contextMenuInstanceID = 0;
        _showContextMenu = true;
      }

      GUILayout.EndHorizontal();
    }

    private void DrawItems()
    {
      var allItems = FlattenItems(_items);

      foreach (var item in allItems)
      {
        if (!string.IsNullOrEmpty(_searchFilter) &&
            !item.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
          continue;

        DrawItem(item);
      }

      if (GUILayout.Button("+", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(24f)))
      {
        CreateGameObject("GameObject", null);
      }
    }

    private List<HierarchyItem> FlattenItems(List<HierarchyItem> items)
    {
      var result = new List<HierarchyItem>();
      foreach (var item in items)
      {
        result.Add(item);
        if (item.Expanded && item.Children.Count > 0)
        {
          result.AddRange(FlattenItems(item.Children));
        }
      }
      return result;
    }

    private void DrawItem(HierarchyItem item)
    {
      int depth = item.Depth;
      float indent = depth * 14f;
      bool isSelected = _selectedInstanceID == item.InstanceID;
      var evt = Event.current;

      var bgStyle = isSelected ? EditorStyles.treeViewItemSelected : EditorStyles.treeViewItem;

      GUILayout.BeginHorizontal(bgStyle);
      GUILayout.Space(indent);

      if (item.Children.Count > 0 || item.HasChildren)
      {
        if (GUILayout.Button(item.Expanded ? "v" : ">", EditorStyles.label, GUILayout.Width(14f), GUILayout.Height(16f)))
        {
          item.Expanded = !item.Expanded;
          _foldoutStates[item.InstanceID] = item.Expanded;
        }
      }
      else
      {
        GUILayout.Space(14f);
      }

      var go = EditorUtility.InstanceIDToObject(item.InstanceID) as GameObject;
      bool isActive = go != null ? go.activeSelf : item.IsActive;
      if (GUILayout.Button(isActive ? "[x]" : "[ ]", EditorStyles.label, GUILayout.Width(28f)))
      {
        if (go != null)
        {
          Undo.RecordObject(go, "Toggle Active");
          go.SetActive(!isActive);
        }
      }

      bool isStatic = go != null && go.isStatic;
      if (isStatic)
      {
        GUILayout.Label("S", EditorStyles.miniLabel, GUILayout.Width(14f));
      }
      else
      {
        GUILayout.Space(14f);
      }

      bool isPrefab = go != null && PrefabUtility.IsPartOfAnyPrefab(go);
      if (isPrefab)
      {
        var prefabIcon = EditorGUIUtility.FindTexture("PrefabModel Icon");
        GUILayout.Box(prefabIcon, GUILayout.Width(16f), GUILayout.Height(16f));
      }
      else
      {
        var icon = EditorGUIUtility.FindTexture(GetIconForGameObject(go));
        GUILayout.Box(icon, GUILayout.Width(16f), GUILayout.Height(16f));
      }

      int layer = go != null ? go.layer : 0;
      Color layerColor = GetLayerColor(layer);
      var layerColorRect = GUILayoutUtility.GetRect(4f, 16f);
      EditorGUI.DrawRect(layerColorRect, layerColor);

      var labelStyle = isSelected ? EditorStyles.whiteLabel : EditorStyles.label;
      if (GUILayout.Button(item.Name, labelStyle, GUILayout.ExpandWidth(true)))
      {
        if (evt != null && evt.button == 1)
        {
          _contextMenuInstanceID = item.InstanceID;
          _showContextMenu = true;
        }
        else
        {
          SelectItem(item);
        }
      }

      var itemRect = GUILayoutUtility.GetLastRect();
      if (evt != null && evt.type == UnityEngine.EventType.MouseUp && evt.button == 1 && itemRect.Contains(evt.mousePosition))
      {
        _contextMenuInstanceID = item.InstanceID;
        _showContextMenu = true;
        evt.Use();
      }

      GUILayout.EndHorizontal();
    }

    private string GetIconForGameObject(GameObject? go)
    {
      if (go == null) return "GameObject Icon";

      var components = go.GetComponents<Component>();
      foreach (var comp in components)
      {
        if (comp is Camera) return "Camera Icon";
        if (comp is Light) return "Light Icon";
        if (comp is MeshFilter) return "MeshFilter Icon";
        if (comp is Renderer) return "MeshRenderer Icon";
        if (comp is AudioSource) return "AudioSource Icon";
        if (comp is ParticleSystem) return "ParticleSystem Icon";
        if (comp is Canvas) return "Canvas Icon";
        if (comp is RectTransform) return "RectTransform Icon";
      }

      return "GameObject Icon";
    }

    private Color GetLayerColor(int layer)
    {
      switch (layer)
      {
        case 0: return new Color(0f, 0f, 0f, 0f);
        case 1: return new Color(0.5f, 0.8f, 1f, 0.8f);
        case 2: return new Color(1f, 0.9f, 0.3f, 0.8f);
        case 3: return new Color(0.4f, 1f, 0.4f, 0.8f);
        case 4: return new Color(1f, 0.5f, 0.5f, 0.8f);
        case 5: return new Color(1f, 0.6f, 0.2f, 0.8f);
        default: return new Color(0.7f, 0.7f, 0.7f, 0.5f);
      }
    }

    private void SelectItem(HierarchyItem item)
    {
      _selectedInstanceID = item.InstanceID;
      var go = EditorUtility.InstanceIDToObject(item.InstanceID) as GameObject;
      if (go != null)
      {
        Selection.SetActiveObject(go);
        Selection.activeInstanceID = item.InstanceID;
      }

      if (InspectorWindow.instance != null)
      {
        InspectorWindow.instance.Repaint();
      }
    }

    public void RebuildHierarchy()
    {
      _items.Clear();

      var scene = SceneManager.GetActiveScene();
      if (!scene.IsValid()) return;

      var roots = scene.GetRootGameObjects();
      var sortedRoots = _alphabeticalSort
        ? roots.OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase).ToArray()
        : roots.OrderBy(go => go.transform.GetSiblingIndex()).ToArray();

      foreach (var go in sortedRoots)
      {
        var item = CreateHierarchyItem(go, 0);
        if (item != null)
          _items.Add(item);
      }

      Repaint();
    }

    private HierarchyItem? CreateHierarchyItem(GameObject go, int depth)
    {
      if (go == null) return null;
      EditorUtility.RegisterInstanceID(go);

      bool expanded;
      if (!_foldoutStates.TryGetValue(go.GetInstanceID(), out expanded))
        expanded = depth < 2;

      var item = new HierarchyItem
      {
        InstanceID = go.GetInstanceID(),
        Name = go.name,
        Expanded = expanded,
        IsActive = go.activeSelf,
        Depth = depth,
        HasChildren = go.transform.childCount > 0
      };

      var children = new List<Transform>();
      for (int i = 0; i < go.transform.childCount; i++)
        children.Add(go.transform.GetChild(i));

      var sortedChildren = _alphabeticalSort
        ? children.OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase).ToArray()
        : children.OrderBy(t => t.GetSiblingIndex()).ToArray();

      foreach (var child in sortedChildren)
      {
        var childItem = CreateHierarchyItem(child.gameObject, depth + 1);
        if (childItem != null)
          item.Children.Add(childItem);
      }

      return item;
    }

    public void AddItem(int instanceID, string name, int parentInstanceID = 0)
    {
      var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
      if (go != null)
      {
        RebuildHierarchy();
        return;
      }

      var item = new HierarchyItem { InstanceID = instanceID, Name = name, Depth = 0 };
      if (parentInstanceID == 0)
      {
        _items.Add(item);
      }
      else
      {
        var parent = FindItem(parentInstanceID);
        if (parent != null)
        {
          parent.Children.Add(item);
          parent.HasChildren = true;
          parent.Expanded = true;
        }
      }
    }

    private HierarchyItem? FindItem(int instanceID)
    {
      return FindItemRecursive(_items, instanceID);
    }

    private HierarchyItem? FindItemRecursive(List<HierarchyItem> items, int instanceID)
    {
      foreach (var item in items)
      {
        if (item.InstanceID == instanceID) return item;
        var found = FindItemRecursive(item.Children, instanceID);
        if (found != null) return found;
      }
      return null;
    }

    private void HandleContextMenu()
    {
      if (!_showContextMenu) return;

      var menu = new GenericMenu();
      var selectedGo = EditorUtility.InstanceIDToObject(_contextMenuInstanceID) as GameObject;

      foreach (var entry in CreateMenuCategories)
      {
        string menuPath = entry;
        if (entry == "Create Empty")
        {
          menu.AddItem(entry, false, (GenericMenu.MenuFunction)(() => CreateGameObject("GameObject", null)));
        }
        else if (entry == "Create Empty Child")
        {
          if (selectedGo != null)
            menu.AddItem(entry, false, (GenericMenu.MenuFunction)(() => CreateGameObject("GameObject", selectedGo.transform)));
          else
            menu.AddDisabledItem(entry);
        }
        else
        {
          var capturedPath = menuPath;
          var capturedParent = selectedGo != null ? selectedGo.transform : null;
          var simpleName = menuPath.Split('/').Last();
          menu.AddItem(menuPath, false, (GenericMenu.MenuFunction)(() => CreateGameObject(simpleName, capturedParent)));
        }
      }

      if (selectedGo != null)
      {
        menu.AddSeparator();
        menu.AddItem("Rename", false, (GenericMenu.MenuFunction)(() => { }));
        menu.AddItem("Duplicate", false, (GenericMenu.MenuFunction)(() => DuplicateGameObject(selectedGo)));
        menu.AddItem("Delete", false, (GenericMenu.MenuFunction)(() => DeleteGameObject(selectedGo)));
        menu.AddSeparator();
        menu.AddItem("Copy", false, (GenericMenu.MenuFunction)(() => { }));
        menu.AddItem("Paste", false, (GenericMenu.MenuFunction)(() => { }));
        menu.AddSeparator();
        menu.AddItem("Select Children", false, (GenericMenu.MenuFunction)(() => SelectChildren(selectedGo)));
      }

      menu.ShowAsContext();
      _showContextMenu = false;
    }

    private void CreateGameObject(string name, Transform? parent)
    {
      var go = new GameObject(name);
      EditorUtility.RegisterInstanceID(go);
      if (parent != null)
      {
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
      }

      Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
      Selection.SetActiveObject(go);
      RebuildHierarchy();
    }

    private void DuplicateGameObject(GameObject go)
    {
      var duplicate = UnityEngine.Object.Instantiate(go, go.transform.parent);
      duplicate.name = go.name;
      EditorUtility.RegisterInstanceID(duplicate);
      Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate");
      Selection.SetActiveObject(duplicate);
      RebuildHierarchy();
    }

    private void DeleteGameObject(GameObject go)
    {
      Undo.DestroyObjectImmediate(go);
      RebuildHierarchy();
      Selection.SetActiveObject(null);
    }

    private void SelectChildren(GameObject go)
    {
      if (go.transform.childCount == 0) return;

      var children = new UnityEngine.Object[go.transform.childCount];
      for (int i = 0; i < go.transform.childCount; i++)
        children[i] = go.transform.GetChild(i).gameObject;

      Selection.SetSelection(children);
    }

    private void HandleHoverAndRename()
    {
      var evt = Event.current;
      if (evt == null) return;

      if (evt.type == UnityEngine.EventType.MouseDown && evt.button == 1)
      {
        _hoveredInstanceID = -1;
        var allItems = FlattenItems(_items);
        foreach (var item in allItems)
        {
          _hoveredInstanceID = item.InstanceID;
          break;
        }
      }
    }

    private void OnSelectionChanged()
    {
      _selectedInstanceID = Selection.activeInstanceID;
      Repaint();
    }

    [MenuItem("Window/General/Hierarchy")]
    public static HierarchyWindow ShowWindow()
    {
      return GetWindow<HierarchyWindow>("Hierarchy");
    }
  }

  internal sealed class HierarchyItem
  {
    public int InstanceID;
    public string Name = string.Empty;
    public bool Expanded = true;
    public bool IsSelected;
    public bool IsActive = true;
    public int IconIndex;
    public int Depth;
    public bool HasChildren;
    public List<HierarchyItem> Children = new();
  }
}
