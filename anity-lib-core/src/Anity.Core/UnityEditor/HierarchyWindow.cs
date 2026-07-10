using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
  public sealed class HierarchyWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private readonly List<HierarchyItem> _items = new List<HierarchyItem>();
    private int _selectedInstanceID;
    private string _searchFilter = string.Empty;

    public static HierarchyWindow instance { get; private set; }

    public HierarchyWindow()
    {
      titleContent = new GUIContent("Hierarchy");
      minSize = new Vector2(200f, 200f);
      instance = this;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
      RebuildHierarchy();
    }

    protected override void OnGUI()
    {
      DrawToolbar();
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
      DrawItems();
      GUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);
      _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
      if (GUILayout.Button("", EditorStyles.toolbarSearchFieldCancelButton))
        _searchFilter = string.Empty;
      GUILayout.FlexibleSpace();
      GUILayout.Button("Create", EditorStyles.toolbarDropDown);
      GUILayout.EndHorizontal();
    }

    private void DrawItems()
    {
      foreach (var item in _items)
      {
        DrawItem(item, 0);
      }
    }

    private void DrawItem(HierarchyItem item, int depth)
    {
      var indent = depth * 16f;
      GUILayout.BeginHorizontal();
      GUILayout.Space(indent);

      if (item.Children.Count > 0)
      {
        if (GUILayout.Button(item.Expanded ? "▼" : "▶", GUILayout.Width(16f)))
          item.Expanded = !item.Expanded;
      }
      else
      {
        GUILayout.Space(16f);
      }

      var selected = _selectedInstanceID == item.InstanceID;
      var style = selected ? EditorStyles.highlightLabel : EditorStyles.label;
      if (GUILayout.Button(item.Name, style, GUILayout.ExpandWidth(true)))
      {
        _selectedInstanceID = item.InstanceID;
        Selection.activeInstanceID = item.InstanceID;
      }

      GUILayout.EndHorizontal();

      if (item.Expanded)
      {
        foreach (var child in item.Children)
          DrawItem(child, depth + 1);
      }
    }

    public void RebuildHierarchy()
    {
      _items.Clear();
    }

    public void AddItem(int instanceID, string name, int parentInstanceID = 0)
    {
      var item = new HierarchyItem { InstanceID = instanceID, Name = name };
      if (parentInstanceID == 0)
      {
        _items.Add(item);
      }
      else
      {
        var parent = FindItem(parentInstanceID);
        parent?.Children.Add(item);
      }
    }

    private HierarchyItem FindItem(int instanceID)
    {
      return FindItemRecursive(_items, instanceID);
    }

    private HierarchyItem FindItemRecursive(List<HierarchyItem> items, int instanceID)
    {
      foreach (var item in items)
      {
        if (item.InstanceID == instanceID) return item;
        var found = FindItemRecursive(item.Children, instanceID);
        if (found != null) return found;
      }
      return null;
    }

    protected override void OnSelectionChange()
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
    public List<HierarchyItem> Children = new List<HierarchyItem>();
  }
}
