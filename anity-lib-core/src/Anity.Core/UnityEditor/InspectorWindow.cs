using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
  public sealed class InspectorWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private UnityEngine.Object _selectedObject;
    private Editor _activeEditor;
    private bool _locked;
    private bool _showMixedValue;
    private int _selectedComponentIndex;

    public static InspectorWindow instance { get; private set; }

    public bool isLocked
    {
      get => _locked;
      set => _locked = value;
    }

    public InspectorWindow()
    {
      titleContent = new GUIContent("Inspector");
      minSize = new Vector2(250f, 300f);
      instance = this;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
      Selection.selectionChanged += OnSelectionChanged;
    }

    protected override void OnDisable()
    {
      base.OnDisable();
      Selection.selectionChanged -= OnSelectionChanged;
    }

    protected override void OnSelectionChange()
    {
      if (!_locked)
      {
        _selectedObject = Selection.activeObject;
        _activeEditor = _selectedObject != null ? Editor.CreateEditor(_selectedObject) : null;
      }
    }

    protected override void OnInspectorUpdate()
    {
      Repaint();
    }

    protected override void OnGUI()
    {
      DrawToolbar();

      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

      if (_selectedObject == null)
      {
        GUILayout.FlexibleSpace();
        GUILayout.Label("Nothing selected", EditorStyles.centeredGreyMiniLabel);
        GUILayout.FlexibleSpace();
      }
      else
      {
        DrawHeader();
        GUILayout.Space(4f);
        DrawComponents();
      }

      GUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      if (GUILayout.Button(_locked ? "🔒" : "🔓", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        _locked = !_locked;

      _showMixedValue = GUILayout.Toggle(_showMixedValue, "≡", EditorStyles.toolbarButton, GUILayout.Width(24f));

      GUILayout.FlexibleSpace();

      if (GUILayout.Button("Layers", EditorStyles.toolbarDropDown)) { }
      if (GUILayout.Button("…", EditorStyles.toolbarButton, GUILayout.Width(24f))) { }

      GUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
      GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

      GUILayout.BeginHorizontal();
      var icon = EditorGUIUtility.FindTexture(_selectedObject.GetType().Name);
      GUILayout.Box(icon, GUILayout.Width(32f), GUILayout.Height(32f));

      GUILayout.BeginVertical();
      var name = EditorGUILayout.TextField(_selectedObject.name, EditorStyles.boldLabel);
      if (name != _selectedObject.name)
        _selectedObject.name = name;

      var typeName = _selectedObject.GetType().Name;
      GUILayout.Label(typeName, EditorStyles.miniLabel);
      GUILayout.EndVertical();

      GUILayout.EndHorizontal();

      GUILayout.Space(2f);

      GUILayout.BeginHorizontal();
      GUILayout.Label("Layer:", GUILayout.Width(40f));
      EditorGUILayout.LayerField(0);
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Tag:", GUILayout.Width(40f));
      EditorGUILayout.TagField("Untagged");
      GUILayout.EndHorizontal();

      GUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawComponents()
    {
      var go = _selectedObject as GameObject;
      if (go == null) return;

      for (int i = 0; i < 1; i++)
      {
        var expanded = _selectedComponentIndex == i;
        DrawComponentHeader(i, "Transform", expanded);
        if (expanded)
          DrawTransformEditor(go.transform);
      }

      GUILayout.Space(4f);
      if (GUILayout.Button("Add Component", EditorStyles.toolbarButton))
      {
      }
    }

    private void DrawComponentHeader(int index, string name, bool expanded)
    {
      GUILayout.BeginHorizontal(EditorStyles.inspectorTitlebar);

      if (GUILayout.Button(expanded ? "▼" : "▶", EditorStyles.label, GUILayout.Width(16f)))
        _selectedComponentIndex = expanded ? -1 : index;

      GUILayout.Label(name, EditorStyles.boldLabel);
      GUILayout.FlexibleSpace();

      if (GUILayout.Button("☰", EditorStyles.label, GUILayout.Width(16f))) { }

      GUILayout.EndHorizontal();
    }

    private void DrawTransformEditor(Transform transform)
    {
      EditorGUI.BeginChangeCheck();

      var pos = EditorGUILayout.Vector3Field("Position", transform.position);
      if (EditorGUI.EndChangeCheck())
        transform.position = pos;

      var rot = EditorGUILayout.Vector3Field("Rotation", transform.eulerAngles);
      if (EditorGUI.EndChangeCheck())
        transform.eulerAngles = rot;

      var scale = EditorGUILayout.Vector3Field("Scale", transform.localScale);
      if (EditorGUI.EndChangeCheck())
        transform.localScale = scale;
    }

    private void OnSelectionChanged()
    {
      if (!_locked)
      {
        _selectedObject = Selection.activeObject;
        _activeEditor = _selectedObject != null ? Editor.CreateEditor(_selectedObject) : null;
        Repaint();
      }
    }

    [MenuItem("Window/General/Inspector")]
    public static InspectorWindow ShowWindow()
    {
      return GetWindow<InspectorWindow>("Inspector");
    }
  }
}
