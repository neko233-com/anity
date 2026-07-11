using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEditor
{
  public sealed class InspectorWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private UnityEngine.Object? _selectedObject;
    private Editor? _activeEditor;
    private bool _locked;
    private readonly HashSet<int> _expandedComponents = new();
    private bool _showAddComponentMenu;
    private string _addComponentSearch = string.Empty;
    private Vector2 _addComponentScroll;
    private static readonly Type[] ComponentTypes;

    public static InspectorWindow? instance { get; private set; }

    public bool isLocked
    {
      get => _locked;
      set => _locked = value;
    }

    static InspectorWindow()
    {
      ComponentTypes = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a =>
        {
          try { return a.GetTypes(); }
          catch { return Array.Empty<Type>(); }
        })
        .Where(t => typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
        .ToArray();
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
        DrawPreview();
      }

      GUILayout.EndScrollView();

      if (_showAddComponentMenu)
        DrawAddComponentMenu();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      if (GUILayout.Button(_locked ? "Lock" : "Unlock", EditorStyles.toolbarButton, GUILayout.Width(50f)))
        _locked = !_locked;

      GUILayout.FlexibleSpace();

      if (GUILayout.Button("Layers", EditorStyles.toolbarDropDown)) { }
      if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(24f))) { }

      GUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
      if (_selectedObject == null) return;

      GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

      GUILayout.BeginHorizontal();
      var icon = EditorGUIUtility.FindTexture(_selectedObject.GetType().Name);
      GUILayout.Box(icon, GUILayout.Width(32f), GUILayout.Height(32f));

      GUILayout.BeginVertical();
      EditorGUI.BeginChangeCheck();
      var name = EditorGUILayout.TextField(_selectedObject.name, EditorStyles.boldLabel);
      if (EditorGUI.EndChangeCheck())
        _selectedObject.name = name;

      var typeName = _selectedObject.GetType().Name;
      GUILayout.Label(typeName, EditorStyles.miniLabel);
      GUILayout.EndVertical();

      GUILayout.EndHorizontal();

      GUILayout.Space(2f);

      if (_selectedObject is GameObject go)
      {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Static", GUILayout.Width(50f));
        EditorGUILayout.Toggle(go.isStatic);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Layer:", GUILayout.Width(50f));
        EditorGUILayout.LayerField(go.layer);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Tag:", GUILayout.Width(50f));
        EditorGUILayout.TagField(go.tag ?? "Untagged");
        GUILayout.EndHorizontal();
      }

      GUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawComponents()
    {
      if (_selectedObject is not GameObject go)
      {
        if (_activeEditor != null)
        {
          _activeEditor.OnInspectorGUI();
        }
        return;
      }

      var selectedObjs = Selection.objects;
      bool isMultiEditing = selectedObjs != null && selectedObjs.Length > 1;

      var components = go.GetComponents<Component>();
      for (int i = 0; i < components.Length; i++)
      {
        var component = components[i];
        if (component == null) continue;

        bool isExpanded = _expandedComponents.Contains(i);
        DrawComponentHeader(component, i, isExpanded, isMultiEditing);

        if (isExpanded)
        {
          DrawComponentFields(component, isMultiEditing, selectedObjs);
        }
      }

      GUILayout.Space(4f);
      if (GUILayout.Button("Add Component", EditorStyles.toolbarButton))
      {
        _showAddComponentMenu = !_showAddComponentMenu;
        _addComponentSearch = string.Empty;
      }
    }

    private void DrawComponentHeader(Component component, int index, bool expanded, bool isMultiEditing)
    {
      GUILayout.BeginHorizontal(EditorStyles.inspectorTitlebar);

      if (GUILayout.Button(expanded ? "v" : ">", EditorStyles.label, GUILayout.Width(16f)))
      {
        if (expanded)
          _expandedComponents.Remove(index);
        else
          _expandedComponents.Add(index);
      }

      if (component is Behaviour behaviour)
      {
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.Toggle(behaviour.enabled, GUILayout.Width(16f));
        if (EditorGUI.EndChangeCheck())
          behaviour.enabled = enabled;
      }
      else
      {
        GUILayout.Space(16f);
      }

      var type = component.GetType();
      var icon = EditorGUIUtility.FindTexture(type.Name);
      GUILayout.Box(icon, GUILayout.Width(16f), GUILayout.Height(16f));

      GUILayout.Label(type.Name, EditorStyles.boldLabel);
      if (isMultiEditing)
      {
        GUILayout.Label("-", EditorStyles.miniLabel, GUILayout.Width(16f));
      }
      GUILayout.FlexibleSpace();

      if (component is not Transform)
      {
        if (GUILayout.Button("=", EditorStyles.label, GUILayout.Width(16f)))
        {
          var menu = new GenericMenu();
          menu.AddItem("Reset", false, (GenericMenu.MenuFunction)(() => ResetComponent(component)));
          menu.AddItem("Remove Component", false, (GenericMenu.MenuFunction)(() => RemoveComponent(component)));
          menu.AddSeparator();
          menu.AddItem("Copy Component", false, (GenericMenu.MenuFunction)(() => { }));
          menu.AddItem("Paste Component Values", false, (GenericMenu.MenuFunction)(() => { }));
          menu.ShowAsContext();
        }
      }
      else
      {
        GUILayout.Space(16f);
      }

      GUILayout.EndHorizontal();
    }

    private void DrawComponentFields(Component component, bool isMultiEditing, UnityEngine.Object[]? selectedObjs)
    {
      if (component is Transform transform)
      {
        DrawTransformEditor(transform);
        return;
      }

      GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

      var type = component.GetType();
      var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

      foreach (var field in fields)
      {
        bool isSerializeField = field.GetCustomAttribute<SerializeField>() != null;
        bool isPublic = field.IsPublic;
        if (!isPublic && !isSerializeField) continue;
        if (field.IsNotSerialized) continue;

        bool showMixed = false;
        if (isMultiEditing && selectedObjs != null)
        {
          var gos = selectedObjs.OfType<GameObject>().ToArray();
          if (gos.Length > 1)
          {
            object? firstValue = null;
            bool hasFirst = false;
            foreach (var otherGo in gos)
            {
              var otherComp = otherGo.GetComponent(type);
              if (otherComp == null) continue;
              var val = field.GetValue(otherComp);
              if (!hasFirst)
              {
                firstValue = val;
                hasFirst = true;
              }
              else if (!Equals(firstValue, val))
              {
                showMixed = true;
                break;
              }
            }
          }
        }

        DrawSerializedField(component, field, showMixed);
      }

      GUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawSerializedField(Component component, FieldInfo field, bool showMixed)
    {
      string label = ObjectNames.NicifyVariableName(field.Name);
      object? value = field.GetValue(component);
      Type fieldType = field.FieldType;
      EditorGUI.showMixedValue = showMixed;

      EditorGUI.BeginChangeCheck();
      object? newValue = null;

      try
      {
        if (fieldType == typeof(bool))
          newValue = EditorGUILayout.Toggle(label, value != null && (bool)value);
        else if (fieldType == typeof(int))
          newValue = EditorGUILayout.IntField(label, value != null ? (int)value : 0);
        else if (fieldType == typeof(float))
          newValue = EditorGUILayout.FloatField(label, value != null ? (float)value : 0f);
        else if (fieldType == typeof(double))
          newValue = EditorGUILayout.DoubleField(label, value != null ? (double)value : 0.0);
        else if (fieldType == typeof(string))
          newValue = EditorGUILayout.TextField(label, value as string ?? string.Empty);
        else if (fieldType == typeof(Vector2))
          newValue = EditorGUILayout.Vector2Field(label, value != null ? (Vector2)value : Vector2.zero);
        else if (fieldType == typeof(Vector3))
          newValue = EditorGUILayout.Vector3Field(label, value != null ? (Vector3)value : Vector3.zero);
        else if (fieldType == typeof(Vector4))
          newValue = EditorGUILayout.Vector4Field(label, value != null ? (Vector4)value : Vector4.zero);
        else if (fieldType == typeof(Color))
          newValue = EditorGUILayout.ColorField(label, value != null ? (Color)value : Color.white);
        else if (fieldType == typeof(Quaternion))
        {
          var q = value != null ? (Quaternion)value : Quaternion.identity;
          var euler = EditorGUILayout.Vector3Field(label, q.eulerAngles);
          newValue = Quaternion.Euler(euler.x, euler.y, euler.z);
        }
        else if (fieldType == typeof(Rect))
          newValue = EditorGUILayout.RectField(label, value != null ? (Rect)value : new Rect());
        else if (fieldType == typeof(Bounds))
          newValue = EditorGUILayout.BoundsField(label, value != null ? (Bounds)value : new Bounds());
        else if (fieldType.IsEnum)
          newValue = EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.ToObject(fieldType, 0));
        else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
        {
          newValue = EditorGUILayout.ObjectField(label, value as UnityEngine.Object, fieldType, true);
        }
        else if (fieldType == typeof(Vector2Int))
          newValue = EditorGUILayout.Vector2IntField(label, value != null ? (Vector2Int)value : Vector2Int.zero);
        else if (fieldType == typeof(Vector3Int))
          newValue = EditorGUILayout.Vector3IntField(label, value != null ? (Vector3Int)value : Vector3Int.zero);
        else if (fieldType == typeof(LayerMask))
        {
          int mask = value != null ? (LayerMask)value : 0;
          newValue = EditorGUILayout.LayerMaskField(mask);
        }
        else
        {
          if (value != null)
            EditorGUILayout.LabelField(label, value.ToString() ?? fieldType.Name);
          else
            EditorGUILayout.LabelField(label, "(none)");
        }
      }
      catch
      {
        EditorGUILayout.LabelField(label, fieldType.Name);
      }

      EditorGUI.showMixedValue = false;

      if (EditorGUI.EndChangeCheck() && newValue != null)
      {
        Undo.RecordObject(component, $"Inspector - {label}");
        field.SetValue(component, newValue);
        EditorUtility.SetDirty(component);
      }
    }

    private void DrawTransformEditor(Transform transform)
    {
      GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

      EditorGUI.BeginChangeCheck();
      var pos = EditorGUILayout.Vector3Field("Position", transform.localPosition);
      if (EditorGUI.EndChangeCheck())
      {
        Undo.RecordObject(transform, "Move");
        transform.localPosition = pos;
      }

      EditorGUI.BeginChangeCheck();
      var rot = EditorGUILayout.Vector3Field("Rotation", transform.localEulerAngles);
      if (EditorGUI.EndChangeCheck())
      {
        Undo.RecordObject(transform, "Rotate");
        transform.localEulerAngles = rot;
      }

      EditorGUI.BeginChangeCheck();
      var scale = EditorGUILayout.Vector3Field("Scale", transform.localScale);
      if (EditorGUI.EndChangeCheck())
      {
        Undo.RecordObject(transform, "Scale");
        transform.localScale = scale;
      }

      GUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawPreview()
    {
      var asset = _selectedObject;
      if (asset == null) return;

      bool hasPreview = asset is Material || asset is Texture || asset is Texture2D || asset is AudioClip || asset is Mesh;
      if (!hasPreview) return;

      EditorGUILayout.Space();
      GUILayout.Box(string.Empty, GUILayout.Height(1f));
      GUILayout.Space(4f);

      GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

      float previewSize = Mathf.Min(128f, 200f);
      var previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

      if (asset is Texture || asset is Texture2D)
      {
        var tex = asset as Texture;
        if (tex != null)
          EditorGUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);
        else
          EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
      }
      else
      {
        var bgColor = asset is Material ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.2f, 0.2f, 0.2f);
        EditorGUI.DrawRect(previewRect, bgColor);
        var icon = EditorGUIUtility.FindTexture(asset.GetType().Name);
        var c = previewRect.center;
        var iconRect = new Rect(c.x - 32f, c.y - 32f, 64f, 64f);
        EditorGUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
      }

      GUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawAddComponentMenu()
    {
      float menuY = 80f;
      var rect = new Rect(0, menuY, position.width, Mathf.Max(200f, position.height - menuY));
      GUILayout.BeginArea(rect, string.Empty, EditorStyles.helpBox);

      GUILayout.BeginHorizontal(EditorStyles.toolbar);
      GUILayout.Label("Add Component", EditorStyles.boldLabel);
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        _showAddComponentMenu = false;
      GUILayout.EndHorizontal();

      _addComponentSearch = EditorGUILayout.TextField(_addComponentSearch, EditorStyles.toolbarSearchField);

      _addComponentScroll = GUILayout.BeginScrollView(_addComponentScroll);

      var search = _addComponentSearch?.ToLowerInvariant() ?? string.Empty;
      var filtered = string.IsNullOrEmpty(search)
        ? ComponentTypes
        : ComponentTypes.Where(t => t.Name.ToLowerInvariant().Contains(search)).ToArray();

      string? currentNamespace = null;
      foreach (var type in filtered.Take(200))
      {
        string ns = type.Namespace ?? "Scripts";
        if (ns != currentNamespace)
        {
          currentNamespace = ns;
          GUILayout.Label(ns, EditorStyles.miniBoldLabel);
        }

        if (GUILayout.Button(type.Name, EditorStyles.label))
        {
          AddComponent(type);
          _showAddComponentMenu = false;
        }
      }

      if (filtered.Length == 0)
      {
        GUILayout.Label("No components found", EditorStyles.centeredGreyMiniLabel);
      }

      GUILayout.EndScrollView();
      GUILayout.EndArea();
    }

    private void AddComponent(Type componentType)
    {
      if (_selectedObject is not GameObject go) return;

      Undo.RecordObject(go, "Add Component");
      go.AddComponent(componentType);
      int newIndex = go.GetComponents<Component>().Length - 1;
      _expandedComponents.Add(newIndex);
    }

    private void ResetComponent(Component component)
    {
      var go = component.gameObject;
      var type = component.GetType();
      Undo.RecordObject(component, "Reset Component");

      var newComponent = go.AddComponent(type);
      var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      foreach (var field in fields)
      {
        bool isSerialize = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        if (isSerialize && !field.IsNotSerialized)
        {
          try { field.SetValue(component, field.GetValue(newComponent)); }
          catch { }
        }
      }
      UnityEngine.Object.DestroyImmediate(newComponent);
      EditorUtility.SetDirty(component);
    }

    private void RemoveComponent(Component component)
    {
      if (component is Transform) return;
      Undo.DestroyObjectImmediate(component);
      Repaint();
    }

    private void OnSelectionChanged()
    {
      if (!_locked)
      {
        _selectedObject = Selection.activeObject;
        _activeEditor = _selectedObject != null ? Editor.CreateEditor(_selectedObject) : null;
        _expandedComponents.Clear();
        _expandedComponents.Add(0);
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
