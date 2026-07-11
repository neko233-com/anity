using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public sealed class AnimationWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private float _currentTime;
    private float _startTime;
    private float _endTime = 1f;
    private bool _playing;
    private bool _recording;
    private AnimationClip? _activeClip;
    private readonly List<string> _curves = new()
    {
        "Position.x",
        "Position.y",
        "Position.z",
        "Rotation.x",
        "Rotation.y",
        "Rotation.z",
        "Scale.x",
        "Scale.y",
        "Scale.z"
    };
    private int _selectedCurve;

    public static AnimationWindow? instance { get; private set; }

    public float currentTime
    {
        get => _currentTime;
        set => _currentTime = Mathf.Clamp(value, _startTime, _endTime);
    }

    public bool playing
    {
        get => _playing;
        set => _playing = value;
    }

    public bool recording
    {
        get => _recording;
        set => _recording = value;
    }

    public AnimationClip? activeClip
    {
        get => _activeClip;
        set => _activeClip = value;
    }

    public AnimationWindow()
    {
        titleContent = new GUIContent("Animation");
        minSize = new Vector2(400f, 300f);
        instance = this;
    }

    protected override void OnGUI()
    {
        DrawToolbar();
        DrawTimeSlider();
        DrawCurveList();
        DrawDopeSheet();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button(_playing ? "▌▌" : "▶", EditorStyles.toolbarButton, GUILayout.Width(30f)))
        {
            _playing = !_playing;
        }

        _recording = GUILayout.Toggle(_recording, "● Record", EditorStyles.toolbarButton, GUILayout.Width(70f));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Add Curve", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            _curves.Add($"New Curve {_curves.Count + 1}");
        }

        GUILayout.EndHorizontal();
    }

    private void DrawTimeSlider()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Time:", EditorStyles.miniLabel, GUILayout.Width(40f));
        _currentTime = GUILayout.HorizontalSlider(_currentTime, _startTime, _endTime);
        GUILayout.Label(_currentTime.ToString("F2"), EditorStyles.miniLabel, GUILayout.Width(40f));
        GUILayout.EndHorizontal();
    }

    private void DrawCurveList()
    {
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150f));
        for (int i = 0; i < _curves.Count; i++)
        {
            bool selected = _selectedCurve == i;
            var style = selected ? EditorStyles.selectedLabel : EditorStyles.label;
            if (GUILayout.Button(_curves[i], style))
            {
                _selectedCurve = i;
            }
        }
        GUILayout.EndScrollView();
    }

    private void DrawDopeSheet()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Dope Sheet", EditorStyles.miniBoldLabel);
        GUILayout.Space(100f);
        var rect = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.15f));
        GUILayout.EndVertical();
    }

    [MenuItem("Window/Animation/Animation")]
    public static AnimationWindow ShowWindow()
    {
        return GetWindow<AnimationWindow>("Animation");
    }
}
