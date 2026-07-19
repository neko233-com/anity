using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EditorAnimations = UnityEditor.Animations;

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
    private EditorAnimations.AnimatorController? _controller;
    private int _activeLayerIndex;
    private readonly List<AnimatorStateNode> _stateNodes = new();
    private readonly List<AnimatorTransitionEdge> _transitionEdges = new();
    private readonly List<EditorAnimations.AnimatorStateMachine> _stateMachinePath = new();
    private EditorAnimations.AnimatorState? _selectedState;
    private EditorAnimations.AnimatorTransitionBase? _selectedTransition;
    private AnimatorStateNode? _draggedStateNode;
    private Vector2 _dragOffset;

    public static AnimatorWindow? instance { get; private set; }

    public AnimatorWindow()
    {
        titleContent = new GUIContent("Animator");
        minSize = new Vector2(400f, 400f);
        instance = this;
        RefreshControllerView();
    }

    public EditorAnimations.AnimatorController? animatorController => _controller;
    public int activeLayerIndex => _activeLayerIndex;
    internal IReadOnlyList<AnimatorStateNode> stateNodes => _stateNodes;
    internal IReadOnlyList<AnimatorTransitionEdge> transitionEdges => _transitionEdges;
    internal IReadOnlyList<EditorAnimations.AnimatorStateMachine> stateMachinePath => _stateMachinePath;
    public EditorAnimations.AnimatorState? selectedState => _selectedState;
    public EditorAnimations.AnimatorTransitionBase? selectedTransition => _selectedTransition;

    public void SetAnimatorController(EditorAnimations.AnimatorController? controller)
    {
        _controller = controller;
        _activeLayerIndex = 0;
        ResetStateMachinePath();
        RefreshControllerView();
        Repaint();
    }

    public void SetActiveLayer(int layerIndex)
    {
        if (_controller == null || layerIndex < 0 || layerIndex >= _controller.layers.Length) return;
        _activeLayerIndex = layerIndex;
        ResetStateMachinePath();
        RefreshControllerView();
        Repaint();
    }

    public EditorAnimations.AnimatorState? AddState(string name, Vector3 position)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || string.IsNullOrWhiteSpace(name)) return null;
        var state = stateMachine.AddState(name, position);
        RefreshControllerView();
        Repaint();
        return state;
    }

    public EditorAnimations.AnimatorStateMachine? AddStateMachine(string name, Vector3 position)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || string.IsNullOrWhiteSpace(name)) return null;
        var child = stateMachine.AddStateMachine(name, position);
        RefreshControllerView();
        Repaint();
        return child;
    }

    public bool NavigateToStateMachine(EditorAnimations.AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null || !TryBuildStateMachinePath(stateMachine, out var path)) return false;
        _stateMachinePath.Clear();
        _stateMachinePath.AddRange(path);
        _selectedState = null;
        _selectedTransition = null;
        RefreshControllerView();
        Repaint();
        return true;
    }

    public bool NavigateToParentStateMachine()
    {
        if (_stateMachinePath.Count <= 1) return false;
        _stateMachinePath.RemoveAt(_stateMachinePath.Count - 1);
        _selectedState = null;
        _selectedTransition = null;
        RefreshControllerView();
        Repaint();
        return true;
    }

    public void NavigateToRootStateMachine()
    {
        ResetStateMachinePath();
        _selectedState = null;
        _selectedTransition = null;
        RefreshControllerView();
        Repaint();
    }

    public bool SetStatePosition(EditorAnimations.AnimatorState state, Vector3 position)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || state == null) return false;
        var states = stateMachine.states;
        for (var index = 0; index < states.Length; index++)
        {
            if (!ReferenceEquals(states[index].state, state)) continue;
            states[index].position = position;
            stateMachine.states = states;
            RefreshControllerView();
            Repaint();
            return true;
        }
        return false;
    }

    public void SetDefaultState(EditorAnimations.AnimatorState state)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || state == null || !stateMachine.states.Any(item => ReferenceEquals(item.state, state))) return;
        stateMachine.defaultState = state;
        SelectState(state);
        RefreshControllerView();
        Repaint();
    }

    public void SelectState(EditorAnimations.AnimatorState? state)
    {
        _selectedState = state;
        _selectedTransition = null;
        Repaint();
    }

    public void SelectTransition(EditorAnimations.AnimatorTransitionBase? transition)
    {
        _selectedTransition = transition;
        _selectedState = null;
        Repaint();
    }

    public EditorAnimations.AnimatorStateTransition? CreateTransition(EditorAnimations.AnimatorState source, EditorAnimations.AnimatorState destination, bool defaultExitTime = false)
    {
        if (!ContainsActiveState(source) || !ContainsActiveState(destination)) return null;
        var transition = source.AddTransition(destination, defaultExitTime);
        RefreshControllerView();
        SelectTransition(transition);
        return transition;
    }

    public EditorAnimations.AnimatorStateTransition? CreateAnyStateTransition(EditorAnimations.AnimatorState destination)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || !ContainsActiveState(destination)) return null;
        var transition = stateMachine.AddAnyStateTransition(destination);
        RefreshControllerView();
        SelectTransition(transition);
        return transition;
    }

    public EditorAnimations.AnimatorTransition? CreateEntryTransition(EditorAnimations.AnimatorState destination)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || !ContainsActiveState(destination)) return null;
        var transition = stateMachine.AddEntryTransition(destination);
        RefreshControllerView();
        SelectTransition(transition);
        return transition;
    }

    public EditorAnimations.AnimatorStateTransition? CreateExitTransition(EditorAnimations.AnimatorState source, bool defaultExitTime = false)
    {
        if (!ContainsActiveState(source)) return null;
        var transition = source.AddExitTransition(defaultExitTime);
        RefreshControllerView();
        SelectTransition(transition);
        return transition;
    }

    public EditorAnimations.AnimatorTransition? CreateStateMachineTransition(EditorAnimations.AnimatorStateMachine source, EditorAnimations.AnimatorStateMachine? destination = null)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || source == null || !stateMachine.stateMachines.Any(child => ReferenceEquals(child.stateMachine, source))) return null;
        if (destination != null && !stateMachine.stateMachines.Any(child => ReferenceEquals(child.stateMachine, destination))) return null;
        var transition = destination == null ? stateMachine.AddStateMachineTransition(source) : stateMachine.AddStateMachineTransition(source, destination);
        RefreshControllerView();
        SelectTransition(transition);
        return transition;
    }

    public EditorAnimations.AnimatorTransition? CreateStateMachineExitTransition(EditorAnimations.AnimatorStateMachine source)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || source == null || !stateMachine.stateMachines.Any(child => ReferenceEquals(child.stateMachine, source))) return null;
        var transition = stateMachine.AddStateMachineExitTransition(source);
        RefreshControllerView();
        SelectTransition(transition);
        return transition;
    }

    public bool RemoveTransition(EditorAnimations.AnimatorTransitionBase transition)
    {
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null || transition == null) return false;
        var removed = false;
        if (transition is EditorAnimations.AnimatorStateTransition stateTransition)
        {
            foreach (var child in stateMachine.states)
            {
                if (child.state == null || !child.state.transitions.Contains(stateTransition)) continue;
                child.state.RemoveTransition(stateTransition);
                removed = true;
                break;
            }
            if (!removed) removed = stateMachine.RemoveAnyStateTransition(stateTransition);
        }
        else if (transition is EditorAnimations.AnimatorTransition graphTransition)
        {
            removed = stateMachine.RemoveEntryTransition(graphTransition);
            if (!removed)
            {
                foreach (var child in stateMachine.stateMachines)
                {
                    if (child.stateMachine == null || !stateMachine.GetStateMachineTransitions(child.stateMachine).Contains(graphTransition)) continue;
                    removed = stateMachine.RemoveStateMachineTransition(child.stateMachine, graphTransition);
                    break;
                }
            }
        }
        if (!removed) return false;
        if (ReferenceEquals(_selectedTransition, transition)) _selectedTransition = null;
        RefreshControllerView();
        Repaint();
        return true;
    }

    public bool AddTransitionCondition(EditorAnimations.AnimatorTransitionBase transition, EditorAnimations.AnimatorConditionMode mode, float threshold, string parameter)
    {
        if (transition == null || string.IsNullOrWhiteSpace(parameter) || _controller == null || !_controller.parameters.Any(item => item.name == parameter)) return false;
        transition.AddCondition(mode, threshold, parameter);
        SelectTransition(transition);
        return true;
    }

    public bool RemoveTransitionCondition(EditorAnimations.AnimatorTransitionBase transition, EditorAnimations.AnimatorCondition condition)
    {
        if (transition == null || !transition.conditions.Contains(condition)) return false;
        transition.RemoveCondition(condition);
        SelectTransition(transition);
        return true;
    }

    public void AddParameter(string name, AnimatorControllerParameterType type)
    {
        if (_controller == null || string.IsNullOrWhiteSpace(name)) return;
        foreach (var parameter in _controller.parameters)
            if (parameter.name == name) return;
        _controller.AddParameter(name, type);
        Repaint();
    }

    public EditorAnimations.AnimatorControllerLayer? AddLayer(string name)
    {
        if (_controller == null || string.IsNullOrWhiteSpace(name)) return null;
        _controller.AddLayer(name);
        var layer = _controller.layers[^1];
        _activeLayerIndex = _controller.layers.Length - 1;
        RefreshControllerView();
        Repaint();
        return layer;
    }

    private EditorAnimations.AnimatorStateMachine? GetActiveStateMachine()
    {
        if (_controller == null || _activeLayerIndex < 0 || _activeLayerIndex >= _controller.layers.Length) return null;
        var root = _controller.layers[_activeLayerIndex].stateMachine;
        if (_stateMachinePath.Count == 0 || !ReferenceEquals(_stateMachinePath[0], root) || !TryBuildStateMachinePath(_stateMachinePath[^1], out _))
        {
            _stateMachinePath.Clear();
            _stateMachinePath.Add(root);
        }
        return _stateMachinePath[^1];
    }

    private void ResetStateMachinePath()
    {
        _stateMachinePath.Clear();
        if (_controller != null && _activeLayerIndex >= 0 && _activeLayerIndex < _controller.layers.Length)
            _stateMachinePath.Add(_controller.layers[_activeLayerIndex].stateMachine);
    }

    private bool TryBuildStateMachinePath(EditorAnimations.AnimatorStateMachine target, out List<EditorAnimations.AnimatorStateMachine> path)
    {
        path = new List<EditorAnimations.AnimatorStateMachine>();
        if (_controller == null || _activeLayerIndex < 0 || _activeLayerIndex >= _controller.layers.Length || target == null) return false;
        return TryBuildStateMachinePath(_controller.layers[_activeLayerIndex].stateMachine, target, path);
    }

    private static bool TryBuildStateMachinePath(EditorAnimations.AnimatorStateMachine current, EditorAnimations.AnimatorStateMachine target, List<EditorAnimations.AnimatorStateMachine> path)
    {
        path.Add(current);
        if (ReferenceEquals(current, target)) return true;
        foreach (var child in current.stateMachines)
            if (child.stateMachine != null && TryBuildStateMachinePath(child.stateMachine, target, path)) return true;
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private void RefreshControllerView()
    {
        var previousTransition = _selectedTransition;
        _stateNodes.Clear();
        _transitionEdges.Clear();
        _selectedState = _selectedState != null && ContainsActiveState(_selectedState) ? _selectedState : null;
        var stateMachine = GetActiveStateMachine();
        if (stateMachine == null) return;
        _stateNodes.Add(new AnimatorStateNode { name = "Entry", position = new Vector2(stateMachine.entryPosition.x, stateMachine.entryPosition.y), isEntry = true });
        _stateNodes.Add(new AnimatorStateNode { name = "Any State", position = new Vector2(stateMachine.anyStatePosition.x, stateMachine.anyStatePosition.y), isAnyState = true });
        _stateNodes.Add(new AnimatorStateNode { name = "Exit", position = new Vector2(stateMachine.exitPosition.x, stateMachine.exitPosition.y), isExit = true });
        foreach (var child in stateMachine.states)
        {
            if (child.state == null) continue;
            _stateNodes.Add(new AnimatorStateNode
            {
                name = child.state.name,
                position = new Vector2(child.position.x, child.position.y),
                isDefault = ReferenceEquals(child.state, stateMachine.defaultState),
                state = child.state
            });
        }
        foreach (var child in stateMachine.stateMachines)
        {
            if (child.stateMachine == null) continue;
            _stateNodes.Add(new AnimatorStateNode
            {
                name = child.stateMachine.name,
                position = new Vector2(child.position.x, child.position.y),
                stateMachine = child.stateMachine
            });
        }
        foreach (var child in stateMachine.states)
        {
            if (child.state == null) continue;
            foreach (var transition in child.state.transitions)
                _transitionEdges.Add(new AnimatorTransitionEdge { source = child.state, destination = transition.destinationState, destinationStateMachine = transition.destinationStateMachine, transition = transition, isExit = transition.isExit });
        }
        foreach (var transition in stateMachine.anyStateTransitions)
            _transitionEdges.Add(new AnimatorTransitionEdge { destination = transition.destinationState, destinationStateMachine = transition.destinationStateMachine, transition = transition, isAnyState = true, isExit = transition.isExit });
        foreach (var transition in stateMachine.entryTransitions)
            _transitionEdges.Add(new AnimatorTransitionEdge { destination = transition.destinationState, destinationStateMachine = transition.destinationStateMachine, transition = transition, isEntry = true, isExit = transition.isExit });
        foreach (var child in stateMachine.stateMachines)
        {
            if (child.stateMachine == null) continue;
            foreach (var transition in stateMachine.GetStateMachineTransitions(child.stateMachine))
                _transitionEdges.Add(new AnimatorTransitionEdge { sourceStateMachine = child.stateMachine, destination = transition.destinationState, destinationStateMachine = transition.destinationStateMachine, transition = transition, isExit = transition.isExit });
        }
        _selectedTransition = previousTransition != null && _transitionEdges.Any(edge => ReferenceEquals(edge.transition, previousTransition)) ? previousTransition : null;
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

        if (_controller == null)
        {
            GUILayout.Label("Select an Animator Controller asset or a GameObject with Animator.", EditorStyles.helpBox);
            return;
        }

        _stateMachineScroll = GUILayout.BeginScrollView(_stateMachineScroll, GUILayout.ExpandHeight(true));

        var canvasRect = GUILayoutUtility.GetRect(650f, 350f);
        EditorGUI.DrawRect(canvasRect, new Color(0.2f, 0.2f, 0.23f, 1f));

        ProcessStateMachineEvent(Event.current, canvasRect);
        foreach (var node in _stateNodes)
        {
            DrawStateNode(node, canvasRect);
        }

        DrawStateTransitions(canvasRect);

        GUILayout.EndScrollView();
    }

    private void DrawStateNode(AnimatorStateNode node, Rect canvasRect)
    {
        var nodeRect = GetNodeRect(node, canvasRect);

        Color nodeColor = ReferenceEquals(node.state, _selectedState)
            ? new Color(0.75f, 0.5f, 0.15f, 0.95f)
            : node.stateMachine != null
            ? new Color(0.34f, 0.42f, 0.62f, 0.95f)
            : node.isDefault
            ? new Color(0.3f, 0.6f, 0.3f, 0.9f)
            : node.isEntry ? new Color(0.4f, 0.4f, 0.6f, 0.9f)
            : node.isExit ? new Color(0.6f, 0.3f, 0.3f, 0.9f)
            : node.isAnyState ? new Color(0.5f, 0.5f, 0.3f, 0.9f)
            : new Color(0.3f, 0.3f, 0.4f, 0.9f);

        EditorGUI.DrawRect(nodeRect, nodeColor);

        var style = node.isDefault ? EditorStyles.whiteBoldLabel : EditorStyles.whiteLabel;
        EditorGUI.LabelField(new Rect(nodeRect.x + 4f, nodeRect.y + 6f, nodeRect.width - 8f, 20f), node.name, style);
    }

    private void DrawStateTransitions(Rect canvasRect)
    {
        foreach (var edge in _transitionEdges)
        {
            var from = GetTransitionAnchor(edge.source, edge.sourceStateMachine, edge.isAnyState, edge.isEntry, edge.isExit, canvasRect);
            var to = GetTransitionAnchor(edge.destination, edge.destinationStateMachine, false, false, false, canvasRect);
            var selected = ReferenceEquals(edge.transition, _selectedTransition);
            Handles.DrawBezier(from, from + new Vector3(30f, 0f, 0f), to - new Vector3(30f, 0f, 0f), to, selected ? new Color(1f, .6f, .15f, 1f) : Color.white, selected ? 3f : 1.5f, null, 1f);
        }
        if (_selectedTransition != null)
        {
            var conditions = _selectedTransition.conditions;
            UnityEngine.Object displaySource = _selectedState != null ? _selectedState : _selectedGameObject != null ? _selectedGameObject : new GameObject();
            GUILayout.Label($"Transition: {_selectedTransition.GetDisplayName(displaySource)} ({conditions.Length} conditions)", EditorStyles.miniLabel);
        }
    }

    internal bool ProcessStateMachineEvent(Event evt, Rect canvasRect)
    {
        if (evt == null || _controller == null) return false;
        if (evt.type == EventType.MouseDown && evt.button == 0 && canvasRect.Contains(evt.mousePosition))
        {
            var node = _stateNodes.LastOrDefault(candidate => GetNodeRect(candidate, canvasRect).Contains(evt.mousePosition));
            if (node != null)
            {
                if (node.stateMachine != null && evt.clickCount >= 2)
                {
                    NavigateToStateMachine(node.stateMachine);
                }
                else if (node.state != null)
                {
                    SelectState(node.state);
                    _draggedStateNode = node;
                    var localPosition = evt.mousePosition - new Vector2(canvasRect.x, canvasRect.y);
                    _dragOffset = localPosition - node.position;
                }
                evt.Use();
                return true;
            }
            var transition = FindTransitionAt(evt.mousePosition, canvasRect);
            if (transition != null)
            {
                SelectTransition(transition.transition);
                evt.Use();
                return true;
            }
            SelectState(null);
            evt.Use();
            return true;
        }
        if (evt.type == EventType.MouseDrag && _draggedStateNode?.state != null)
        {
            var localPosition = evt.mousePosition - new Vector2(canvasRect.x, canvasRect.y);
            SetStatePosition(_draggedStateNode.state, new Vector3(Mathf.Max(0f, localPosition.x - _dragOffset.x), Mathf.Max(0f, localPosition.y - _dragOffset.y), 0f));
            evt.Use();
            return true;
        }
        if (evt.type == EventType.MouseUp && _draggedStateNode != null)
        {
            _draggedStateNode = null;
            evt.Use();
            return true;
        }
        if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Delete && _selectedTransition != null)
        {
            var transition = _selectedTransition;
            RemoveTransition(transition);
            evt.Use();
            return true;
        }
        return false;
    }

    private AnimatorTransitionEdge? FindTransitionAt(Vector2 mousePosition, Rect canvasRect)
    {
        const float hitDistance = 8f;
        foreach (var edge in _transitionEdges.AsEnumerable().Reverse())
        {
            var from = GetTransitionAnchor(edge.source, edge.sourceStateMachine, edge.isAnyState, edge.isEntry, edge.isExit, canvasRect);
            var to = GetTransitionAnchor(edge.destination, edge.destinationStateMachine, false, false, false, canvasRect);
            if (DistanceToLineSegment(mousePosition, new Vector2(from.x, from.y), new Vector2(to.x, to.y)) <= hitDistance) return edge;
        }
        return null;
    }

    private static float DistanceToLineSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var squaredLength = segment.x * segment.x + segment.y * segment.y;
        if (squaredLength <= float.Epsilon) return (point - start).magnitude;
        var projection = Mathf.Clamp01(((point.x - start.x) * segment.x + (point.y - start.y) * segment.y) / squaredLength);
        return (point - (start + segment * projection)).magnitude;
    }

    private bool ContainsActiveState(EditorAnimations.AnimatorState? state) => state != null && GetActiveStateMachine()?.states.Any(item => ReferenceEquals(item.state, state)) == true;
    private Rect GetNodeRect(AnimatorStateNode node, Rect canvasRect)
    {
        var width = node.isEntry || node.isAnyState || node.isExit ? 80f : node.stateMachine != null ? 120f : 100f;
        return new Rect(canvasRect.x + node.position.x, canvasRect.y + node.position.y, width, 30f);
    }

    private Vector3 GetTransitionAnchor(EditorAnimations.AnimatorState? state, EditorAnimations.AnimatorStateMachine? stateMachine, bool anyState, bool entry, bool exit, Rect canvasRect)
    {
        if (entry) return new Vector3(canvasRect.x + GetActiveStateMachine()!.entryPosition.x + 80f, canvasRect.y + GetActiveStateMachine()!.entryPosition.y + 15f, 0f);
        if (anyState) return new Vector3(canvasRect.x + GetActiveStateMachine()!.anyStatePosition.x + 80f, canvasRect.y + GetActiveStateMachine()!.anyStatePosition.y + 15f, 0f);
        if (exit) return new Vector3(canvasRect.x + GetActiveStateMachine()!.exitPosition.x + 40f, canvasRect.y + GetActiveStateMachine()!.exitPosition.y + 15f, 0f);
        var node = _stateNodes.FirstOrDefault(item =>
            (state != null && ReferenceEquals(item.state, state)) ||
            (stateMachine != null && ReferenceEquals(item.stateMachine, stateMachine)));
        var rect = node == null ? default : GetNodeRect(node, canvasRect);
        return node == null ? Vector3.zero : new Vector3(rect.x + rect.width * .5f, rect.y + rect.height * .5f, 0f);
    }

    private void DrawParametersTab()
    {
        GUILayout.Label("Parameters", EditorStyles.boldLabel);
        if (_controller == null)
        {
            GUILayout.Label("No Animator Controller selected.", EditorStyles.helpBox);
            return;
        }

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
        GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
        GUILayout.Label("Value", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        {
            AddParameter("NewParam" + (_controller.parameters.Length + 1), AnimatorControllerParameterType.Float);
        }
        GUILayout.EndHorizontal();

        foreach (var parameter in _controller.parameters)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(parameter.name, EditorStyles.miniLabel, GUILayout.Width(150f));
            GUILayout.Label(parameter.type.ToString(), EditorStyles.miniLabel, GUILayout.Width(80f));
            GUILayout.HorizontalSlider(0f, -1f, 1f);
            GUILayout.EndHorizontal();
        }
    }

    private void DrawLayersTab()
    {
        GUILayout.Label("Layers", EditorStyles.boldLabel);
        if (_controller == null)
        {
            GUILayout.Label("No Animator Controller selected.", EditorStyles.helpBox);
            return;
        }

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
        GUILayout.Label("Weight", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
        GUILayout.Label("Mask", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        {
            AddLayer("Layer " + _controller.layers.Length);
        }
        GUILayout.EndHorizontal();

        foreach (var layer in _controller.layers)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(layer.name, EditorStyles.miniLabel, GUILayout.Width(150f));
            GUILayout.Label(layer.defaultWeight.ToString("0.##"), EditorStyles.miniLabel, GUILayout.Width(80f));
            GUILayout.Label(layer.avatarMask == null ? "Everything" : layer.avatarMask.name, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }
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
                SetAnimatorController(animator.runtimeAnimatorController as EditorAnimations.AnimatorController);
            }
            else
            {
                _previewAvatar = null;
                SetAnimatorController(null);
            }
        }
        else
        {
            _previewAvatar = null;
            SetAnimatorController(null);
        }
    }

    public static AnimatorWindow OpenWindow()
    {
        return GetWindow<AnimatorWindow>("Animator");
    }
}

internal sealed class AnimatorStateNode
{
    public string name = string.Empty;
    public Vector2 position;
    public bool isEntry;
    public bool isExit;
    public bool isAnyState;
    public bool isDefault;
    public EditorAnimations.AnimatorState? state;
    public EditorAnimations.AnimatorStateMachine? stateMachine;
}

internal sealed class AnimatorTransitionEdge
{
    public EditorAnimations.AnimatorState? source;
    public EditorAnimations.AnimatorState? destination;
    public EditorAnimations.AnimatorStateMachine? sourceStateMachine;
    public EditorAnimations.AnimatorStateMachine? destinationStateMachine;
    public EditorAnimations.AnimatorTransitionBase? transition;
    public bool isAnyState;
    public bool isEntry;
    public bool isExit;
}
