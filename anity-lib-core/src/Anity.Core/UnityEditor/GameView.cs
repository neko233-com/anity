using UnityEngine;

namespace UnityEditor;

public sealed class GameView : EditorWindow
{
    private int _targetDisplay;
    private bool _vSyncEnabled = true;
    private Color _playModeTint = new Color(0.8f, 0.8f, 1f, 1f);
    private Vector2 _scrollPosition;

    public static GameView? instance { get; private set; }

    public int targetDisplay
    {
        get => _targetDisplay;
        set => _targetDisplay = value;
    }

    public bool vSyncEnabled
    {
        get => _vSyncEnabled;
        set => _vSyncEnabled = value;
    }

    public Color playModeTint
    {
        get => _playModeTint;
        set => _playModeTint = value;
    }

    public GameView()
    {
        titleContent = new GUIContent("Game");
        minSize = new Vector2(300f, 200f);
        instance = this;
    }

    protected override void OnGUI()
    {
        DrawToolbar();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Game View", EditorStyles.centeredGreyMiniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Display:", EditorStyles.miniLabel, GUILayout.Width(50f));
        _targetDisplay = EditorGUILayout.IntField(_targetDisplay, GUILayout.Width(30f));

        GUILayout.Space(8f);

        _vSyncEnabled = GUILayout.Toggle(_vSyncEnabled, "VSync", EditorStyles.toolbarButton, GUILayout.Width(50f));

        GUILayout.Space(8f);

        GUILayout.Label("Play Tint:", EditorStyles.miniLabel);
        _playModeTint = EditorGUILayout.ColorField(_playModeTint, GUILayout.Width(60f));

        GUILayout.FlexibleSpace();

        GUILayout.EndHorizontal();
    }

    [MenuItem("Window/General/Game")]
    public static GameView ShowWindow()
    {
        return GetWindow<GameView>("Game");
    }
}
