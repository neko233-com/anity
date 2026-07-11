using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public sealed class ProfilerWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private float _cpuUsage = 12.5f;
    private float _renderingUsage = 8.3f;
    private float _memoryUsage = 256f;
    private bool _showCpu = true;
    private bool _showRendering = true;
    private bool _showMemory = true;
    private readonly List<float> _cpuHistory = new();
    private readonly List<float> _renderHistory = new();
    private readonly List<float> _memoryHistory = new();

    public static ProfilerWindow? instance { get; private set; }

    public float cpuUsage => _cpuUsage;
    public float renderingUsage => _renderingUsage;
    public float memoryUsageMB => _memoryUsage;

    public ProfilerWindow()
    {
        titleContent = new GUIContent("Profiler");
        minSize = new Vector2(400f, 300f);
        instance = this;
    }

    protected override void OnGUI()
    {
        DrawToolbar();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        DrawTimeline();
        DrawStats();

        GUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        _showCpu = GUILayout.Toggle(_showCpu, "CPU", EditorStyles.toolbarButton);
        _showRendering = GUILayout.Toggle(_showRendering, "Rendering", EditorStyles.toolbarButton);
        _showMemory = GUILayout.Toggle(_showMemory, "Memory", EditorStyles.toolbarButton);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
        {
            _cpuHistory.Clear();
            _renderHistory.Clear();
            _memoryHistory.Clear();
        }

        GUILayout.EndHorizontal();
    }

    private void DrawTimeline()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Profiler Timeline", EditorStyles.boldLabel);
        GUILayout.Space(100f);
        var rect = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        GUILayout.EndVertical();
    }

    private void DrawStats()
    {
        if (_showCpu)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("CPU:", GUILayout.Width(80f));
            _cpuUsage = EditorGUILayout.FloatField(_cpuUsage, GUILayout.Width(80f));
            GUILayout.Label("ms", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        if (_showRendering)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rendering:", GUILayout.Width(80f));
            _renderingUsage = EditorGUILayout.FloatField(_renderingUsage, GUILayout.Width(80f));
            GUILayout.Label("ms", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        if (_showMemory)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Memory:", GUILayout.Width(80f));
            _memoryUsage = EditorGUILayout.FloatField(_memoryUsage, GUILayout.Width(80f));
            GUILayout.Label("MB", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }
    }

    public void RecordSample(float cpuMs, float renderMs, float memMb)
    {
        _cpuUsage = cpuMs;
        _renderingUsage = renderMs;
        _memoryUsage = memMb;
        _cpuHistory.Add(cpuMs);
        _renderHistory.Add(renderMs);
        _memoryHistory.Add(memMb);
        if (_cpuHistory.Count > 300) _cpuHistory.RemoveAt(0);
        if (_renderHistory.Count > 300) _renderHistory.RemoveAt(0);
        if (_memoryHistory.Count > 300) _memoryHistory.RemoveAt(0);
        Repaint();
    }

    [MenuItem("Window/Analysis/Profiler")]
    public static ProfilerWindow ShowWindow()
    {
        return GetWindow<ProfilerWindow>("Profiler");
    }
}
