using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public enum AnimatorWindowTab
{
    BaseLayer,
    Parameters,
    Layers
}

public sealed class AnimatorWindow : EditorWindow
{
    private AnimatorWindowTab _activeTab;
    private Vector2 _scrollPosition;
    private GameObject? _selectedGameObject;
    private Avatar? _previewAvatar;
    private bool _showAvatarPreview = true;
    private bool _showStateMachine = true;
    private Vector2 _stateMachineScroll;
    private float _previewScale = 1f;
    private Quaternion _previewRotation = Quaternion.identity;
    private readonly List<string> _parameters = new() { "Speed", "Jump", "IsGrounded" };
    private readonly List<AnimatorStateNode> _stateNodes = new();

    public static AnimatorWindow? instance { get; private set; }

    public AnimatorWindow()
    {
        titleContent = new GUIContent("Animator");
        minSize = new Vector2(400f, 400f);
        instance = this;
        InitializeDefaultStates();
    }

    private void InitializeDefaultStates()
    {
        _stateNodes.Clear();
        _stateNodes.Add(new AnimatorStateNode { name = "Entry", position = new Vector2(20f, 100f), isEntry = true });
        _stateNodes.Add(new AnimatorStateNode { name = "Idle", position = new Vector2(150f, 100f), isDefault = true });
        _stateNodes.Add(new AnimatorStateNode { name = "Walk", position = new Vector2(300f, 100f) });
        _stateNodes.Add(new AnimatorStateNode { name = "Run", position = new Vector2(450f, 100f) });
        _stateNodes.Add(new AnimatorStateNode { name = "Jump", position = new Vector2(300f, 250f) });
        _stateNodes.Add(new AnimatorStateNode { name = "Any State", position = new Vector2(20f, 250f), isAnyState = true });
        _stateNodes.Add(new AnimatorStateNode { name = "Exit", position = new Vector2(600f, 100f), isExit = true });
    }

    protected override void OnSelectionChange()
    {
        base.OnSelectionChange();
        var selected = Selection.activeGameObject as GameObject;
        if (selected != null)
        {
            SetSelectionGameObject(selected);
        }
        Repaint();
    }

    protected override void OnGUI()
    {
        DrawToolbar();
        DrawTabs();

        GUILayout.BeginHorizontal();

        if (_showAvatarPreview)
        {
            DrawAvatarPreview();
        }

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        switch (_activeTab)
        {
            case AnimatorWindowTab.BaseLayer:
                DrawStateMachineView();
                break;
            case AnimatorWindowTab.Parameters:
                DrawParametersTab();
                break;
            case AnimatorWindowTab.Layers:
                DrawLayersTab();
                break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(24f))) { }
        if (GUILayout.Button("▌▌", EditorStyles.toolbarButton, GUILayout.Width(24f))) { }

        GUILayout.Space(4f);
        _showAvatarPreview = GUILayout.Toggle(_showAvatarPreview, "Preview", EditorStyles.toolbarButton, GUILayout.Width(60f));
        _showStateMachine = GUILayout.Toggle(_showStateMachine, "States", EditorStyles.toolbarButton, GUILayout.Width(55f));

        GUILayout.FlexibleSpace();

        if (_selectedGameObject != null)
        {
            GUILayout.Label(_selectedGameObject.name, EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("No selection", EditorStyles.miniLabel);
        }

        GUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        string[] tabNames = { "Base Layer", "Parameters", "Layers" };
        int selected = (int)_activeTab;
        int newSelected = GUILayout.Toolbar(selected, tabNames, EditorStyles.toolbarButton);
        if (newSelected != selected)
        {
            _activeTab = (AnimatorWindowTab)newSelected;
        }
        GUILayout.EndHorizontal();
    }

    public void DrawAvatarPreview()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(180f), GUILayout.ExpandHeight(true));
        GUILayout.Label("Preview", EditorStyles.miniBoldLabel);

        var previewRect = GUILayoutUtility.GetRect(160f, 200f);
        EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.18f, 1f));

        if (_previewAvatar != null)
        {
            GUILayout.Label("Avatar: " + _previewAvatar.name, EditorStyles.miniLabel);
        }
        else if (_selectedGameObject != null)
        {
            var animator = _selectedGameObject.GetComponent<Animator>();
            if (animator != null && animator.avatar != null)
            {
                GUILayout.Label("Avatar: " + animator.avatar.name, EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label("No Avatar", EditorStyles.miniLabel);
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("Preview Scale", EditorStyles.miniLabel);
        _previewScale = GUILayout.HorizontalSlider(_previewScale, 0.1f, 3f);

        GUILayout.Space(4f);
        if (GUILayout.Button("Reset Preview", EditorStyles.miniButton))
        {
            _previewScale = 1f;
            _previewRotation = Quaternion.identity;
        }

        GUILayout.EndVertical();
    }

    public void DrawStateMachineView()
    {
        GUILayout.Label("State Machine", EditorStyles.boldLabel);

        _stateMachineScroll = GUILayout.BeginScrollView(_stateMachineScroll, GUILayout.ExpandHeight(true));

        var canvasRect = GUILayoutUtility.GetRect(650f, 350f);
        EditorGUI.DrawRect(canvasRect, new Color(0.2f, 0.2f, 0.23f, 1f));

        foreach (var node in _stateNodes)
        {
            DrawStateNode(node);
        }

        DrawStateTransitions();

        GUILayout.EndScrollView();
    }

    private void DrawStateNode(AnimatorStateNode node)
    {
        var nodeRect = new Rect(node.position.x, node.position.y, node.isEntry || node.isAnyState || node.isExit ? 80f : 100f, 30f);

        Color nodeColor = node.isDefault
            ? new Color(0.3f, 0.6f, 0.3f, 0.9f)
            : node.isEntry ? new Color(0.4f, 0.4f, 0.6f, 0.9f)
            : node.isExit ? new Color(0.6f, 0.3f, 0.3f, 0.9f)
            : node.isAnyState ? new Color(0.5f, 0.5f, 0.3f, 0.9f)
            : new Color(0.3f, 0.3f, 0.4f, 0.9f);

        EditorGUI.DrawRect(nodeRect, nodeColor);

        var style = node.isDefault ? EditorStyles.whiteBoldLabel : EditorStyles.whiteLabel;
        EditorGUI.LabelField(new Rect(nodeRect.x + 4f, nodeRect.y + 6f, nodeRect.width - 8f, 20f), node.name, style);
    }

    private void DrawStateTransitions()
    {
        // State transitions are drawn as conceptual lines between nodes
        // Entry -> Idle, Idle -> Walk -> Run, Any State -> Jump
    }

    private void DrawParametersTab()
    {
        GUILayout.Label("Parameters", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
        GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
        GUILayout.Label("Value", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        {
            _parameters.Add("NewParam" + (_parameters.Count + 1));
        }
        GUILayout.EndHorizontal();

        for (int i = 0; i < _parameters.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_parameters[i], EditorStyles.miniLabel, GUILayout.Width(150f));
            GUILayout.Label("Float", EditorStyles.miniLabel, GUILayout.Width(80f));
            GUILayout.HorizontalSlider(0f, -1f, 1f);
            if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(24f)))
            {
                _parameters.RemoveAt(i);
                break;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawLayersTab()
    {
        GUILayout.Label("Layers", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
        GUILayout.Label("Weight", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
        GUILayout.Label("Mask", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        {
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Base Layer", EditorStyles.miniLabel, GUILayout.Width(150f));
        GUILayout.Label("1", EditorStyles.miniLabel, GUILayout.Width(80f));
        GUILayout.Label("Everything", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();
    }

    public void SetSelectionGameObject(GameObject? go)
    {
        _selectedGameObject = go;
        if (go != null)
        {
            var animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                _previewAvatar = animator.avatar;
            }
        }
        else
        {
            _previewAvatar = null;
        }
    }

    public static AnimatorWindow OpenWindow()
    {
        return GetWindow<AnimatorWindow>("Animator");
    }
}

internal class AnimatorStateNode
{
    public string name = string.Empty;
    public Vector2 position;
    public bool isEntry;
    public bool isExit;
    public bool isAnyState;
    public bool isDefault;
}
