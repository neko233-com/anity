using UnityEngine;
using UnityEngine.AI;
using UnityEditor.AI;

namespace UnityEditor;

public enum NavigationWindowTab
{
    Agents,
    Areas,
    Bake,
    Object
}

public sealed class NavigationWindow : EditorWindow
{
    private NavigationWindowTab _activeTab;
    private Vector2 _scrollPosition;
    private int _selectedAgentType;
    private float _agentRadius = 0.5f;
    private float _agentHeight = 2f;
    private float _agentSlope = 45f;
    private float _agentStepHeight = 0.4f;
    private float _voxelSize = 0.166f;
    private float _tileSize = 256f;
    private float _minRegionArea = 2f;
    private bool _showNavMesh = true;
    private bool _isBaking;
    private float _bakeProgress;

    private static readonly string[] TabNames = { "Agents", "Areas", "Bake", "Object" };

    public static NavigationWindow? instance { get; private set; }

    public NavigationWindow()
    {
        titleContent = new GUIContent("Navigation");
        minSize = new Vector2(300f, 400f);
        instance = this;
    }

    protected override void OnGUI()
    {
        DrawTabs();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        switch (_activeTab)
        {
            case NavigationWindowTab.Agents:
                DrawAgentsTab();
                break;
            case NavigationWindowTab.Areas:
                DrawAreasTab();
                break;
            case NavigationWindowTab.Bake:
                DrawBakeTab();
                break;
            case NavigationWindowTab.Object:
                DrawObjectTab();
                break;
        }

        GUILayout.EndScrollView();

        if (_activeTab == NavigationWindowTab.Bake)
        {
            DrawBakeButtons();
        }
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        int selected = (int)_activeTab;
        int newSelected = GUILayout.Toolbar(selected, TabNames, EditorStyles.toolbarButton);
        if (newSelected != selected)
        {
            _activeTab = (NavigationWindowTab)newSelected;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawAgentsTab()
    {
        GUILayout.Label("Agents", EditorStyles.boldLabel);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        string[] agentTypes = { "Humanoid" };
        _selectedAgentType = EditorGUILayout.Popup("Agent Type", _selectedAgentType, agentTypes);

        GUILayout.Space(8f);
        GUILayout.Label("Agent Radius", EditorStyles.miniBoldLabel);
        _agentRadius = EditorGUILayout.FloatField("Radius", _agentRadius);
        _agentHeight = EditorGUILayout.FloatField("Height", _agentHeight);

        GUILayout.Space(4f);
        GUILayout.Label("Stepping", EditorStyles.miniBoldLabel);
        _agentStepHeight = EditorGUILayout.FloatField("Step Height", _agentStepHeight);
        _agentSlope = EditorGUILayout.Slider("Max Slope", _agentSlope, 0f, 90f);

        GUILayout.EndVertical();
    }

    private void DrawAreasTab()
    {
        GUILayout.Label("Areas", EditorStyles.boldLabel);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        string[] areaNames = { "Walkable", "Not Walkable", "Jump" };
        float[] areaCosts = { NavMesh.GetAreaCost(0), NavMesh.GetAreaCost(1), NavMesh.GetAreaCost(2) };

        GUILayout.Label("Name", EditorStyles.miniBoldLabel);
        for (int i = 0; i < areaNames.Length; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(i.ToString(), EditorStyles.miniLabel, GUILayout.Width(20f));
            GUILayout.Label(areaNames[i], EditorStyles.miniLabel, GUILayout.Width(100f));
            areaCosts[i] = EditorGUILayout.FloatField(areaCosts[i]);
            NavMesh.SetAreaCost(i, areaCosts[i]);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    private void DrawBakeTab()
    {
        GUILayout.Label("Bake", EditorStyles.boldLabel);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        _voxelSize = EditorGUILayout.FloatField("Voxel Size", _voxelSize);
        _tileSize = EditorGUILayout.FloatField("Tile Size", _tileSize);
        _minRegionArea = EditorGUILayout.FloatField("Min Region Area", _minRegionArea);
        _showNavMesh = EditorGUILayout.Toggle("Show NavMesh", _showNavMesh);

        GUILayout.Space(8f);
        var buildSettings = NavMeshBuilder.GetBuildSettings();
        if (buildSettings != null)
        {
            GUILayout.Label($"Agent Radius: {buildSettings.agentRadius}", EditorStyles.miniLabel);
            GUILayout.Label($"Agent Height: {buildSettings.agentHeight}", EditorStyles.miniLabel);
        }

        GUILayout.EndVertical();
    }

    private void DrawObjectTab()
    {
        GUILayout.Label("Object", EditorStyles.boldLabel);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        var selected = Selection.activeGameObject as GameObject;
        if (selected != null)
        {
            GUILayout.Label($"Selected: {selected.name}", EditorStyles.miniLabel);
            bool navStatic = EditorGUILayout.Toggle("Navigation Static", selected.isStatic);
            selected.isStatic = navStatic;

            int selectedArea = EditorGUILayout.IntPopup("Navigation Area", 0,
                new[] { "Walkable", "Not Walkable", "Jump" }, new[] { 0, 1, 2 });
            _ = selectedArea;
        }
        else
        {
            GUILayout.Label("No object selected.", EditorStyles.miniLabel);
        }

        GUILayout.EndVertical();
    }

    private void DrawBakeButtons()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (_isBaking)
        {
            GUILayout.Label($"Baking... {_bakeProgress * 100f:F0}%", EditorStyles.miniLabel);
            EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100f, 18f), _bakeProgress, "Baking");
            if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                _isBaking = false;
                _bakeProgress = 0f;
            }
        }
        else
        {
            if (GUILayout.Button("Bake", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                _isBaking = true;
                _bakeProgress = 0f;
                NavMeshBuilder.BuildNavMesh(_voxelSize, _tileSize, _minRegionArea);
                _isBaking = false;
                _bakeProgress = 1f;
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                NavMeshBuilder.ClearAllNavMeshes();
            }
        }

        GUILayout.EndHorizontal();
    }

    public static NavigationWindow OpenWindow()
    {
        return GetWindow<NavigationWindow>("Navigation");
    }
}
