using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public enum LightingWindowTab
{
    Scene,
    RealtimeLightmaps,
    BakedLightmaps
}

public sealed class LightingWindow : EditorWindow
{
    private LightingWindowTab _activeTab = LightingWindowTab.Scene;
    private Vector2 _scrollPosition;
    private LightingSettings? _activeLightingSettings;
    private bool _showRealtimeLights = true;
    private bool _showMixedLights = true;
    private bool _showBakedLights = true;
    private bool _showReflectionProbes = true;
    private bool _autoGenerate = true;
    private float _bakeProgress;
    private bool _isBaking;

    private static readonly string[] TabNames = { "Scene", "Realtime Lightmaps", "Baked Lightmaps" };

    public static LightingWindow? instance { get; private set; }

    public LightingSettings? activeLightingSettings
    {
        get => _activeLightingSettings;
        set => _activeLightingSettings = value;
    }

    public LightingWindow()
    {
        titleContent = new GUIContent("Lighting");
        minSize = new Vector2(350f, 400f);
        instance = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _activeLightingSettings = Lightmapping.lightingSettings ?? new LightingSettings();
    }

    protected override void OnGUI()
    {
        DrawTabs();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        switch (_activeTab)
        {
            case LightingWindowTab.Scene:
                DrawSceneTab();
                break;
            case LightingWindowTab.RealtimeLightmaps:
                DrawRealtimeLightmapsTab();
                break;
            case LightingWindowTab.BakedLightmaps:
                DrawBakedLightmapsTab();
                break;
        }

        GUILayout.EndScrollView();
        DrawBakeButton();
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        int selected = (int)_activeTab;
        int newSelected = GUILayout.Toolbar(selected, TabNames, EditorStyles.toolbarButton);
        if (newSelected != selected)
        {
            _activeTab = (LightingWindowTab)newSelected;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSceneTab()
    {
        GUILayout.Label("Lighting Settings", EditorStyles.boldLabel);

        GUILayout.BeginVertical(EditorStyles.helpBox);
        _autoGenerate = GUILayout.Toggle(_autoGenerate, "Auto Generate", EditorStyles.toggle);
        GUILayout.Space(4f);

        GUILayout.Label("Realtime Global Illumination", EditorStyles.miniBoldLabel);
        Lightmapping.realtimeGI = GUILayout.Toggle(Lightmapping.realtimeGI, "Realtime GI");

        GUILayout.Label("Baked Global Illumination", EditorStyles.miniBoldLabel);
        Lightmapping.bakedGI = GUILayout.Toggle(Lightmapping.bakedGI, "Baked GI");

        GUILayout.Space(8f);
        DrawLights();
        DrawReflectionProbes();
        GUILayout.EndVertical();
    }

    public void DrawLights()
    {
        GUILayout.Label("Lighting", EditorStyles.boldLabel);
        _showRealtimeLights = EditorGUILayout.Foldout(_showRealtimeLights, "Realtime Lights");
        if (_showRealtimeLights)
        {
            var allLights = Object.FindObjectsOfType<Light>();
            int realtimeCount = 0, mixedCount = 0, bakedCount = 0;
            foreach (var light in allLights)
            {
                switch (light.lightmapBakeType)
                {
                    case LightmapBakeType.Realtime: realtimeCount++; break;
                    case LightmapBakeType.Mixed: mixedCount++; break;
                    case LightmapBakeType.Baked: bakedCount++; break;
                }
            }
            GUILayout.Label($"  Realtime: {realtimeCount}", EditorStyles.miniLabel);
            _showMixedLights = EditorGUILayout.Foldout(_showMixedLights, $"Mixed Lights ({mixedCount})");
            _showBakedLights = EditorGUILayout.Foldout(_showBakedLights, $"Baked Lights ({bakedCount})");
        }
    }

    public void DrawReflectionProbes()
    {
        GUILayout.Label("Reflection Probes", EditorStyles.boldLabel);
        _showReflectionProbes = EditorGUILayout.Foldout(_showReflectionProbes, "Reflection Probes");
        if (_showReflectionProbes)
        {
            var probes = Object.FindObjectsOfType<ReflectionProbe>();
            GUILayout.Label($"  Probe Count: {probes.Length}", EditorStyles.miniLabel);
        }
    }

    private void DrawRealtimeLightmapsTab()
    {
        GUILayout.Label("Realtime Lightmaps", EditorStyles.boldLabel);
        GUILayout.Label("No realtime lightmaps baked.", EditorStyles.miniLabel);

        var lightmaps = Lightmapping.lightmaps;
        if (lightmaps != null && lightmaps.Length > 0)
        {
            for (int i = 0; i < lightmaps.Length; i++)
            {
                GUILayout.Label($"Lightmap {i}", EditorStyles.miniBoldLabel);
                if (lightmaps[i].lightmapColor != null)
                {
                    GUILayout.Box(lightmaps[i].lightmapColor, GUILayout.Width(128f), GUILayout.Height(128f));
                }
            }
        }
    }

    private void DrawBakedLightmapsTab()
    {
        GUILayout.Label("Baked Lightmaps", EditorStyles.boldLabel);
        var baked = Lightmapping.bakedLightmaps;
        if (baked != null && baked.Length > 0)
        {
            for (int i = 0; i < baked.Length; i++)
            {
                GUILayout.Label($"Baked Lightmap {i}", EditorStyles.miniBoldLabel);
                if (baked[i].lightmapColor != null)
                {
                    GUILayout.Box(baked[i].lightmapColor, GUILayout.Width(128f), GUILayout.Height(128f));
                }
            }
        }
        else
        {
            GUILayout.Label("No baked lightmaps. Click 'Generate Lighting' to bake.", EditorStyles.miniLabel);
        }
    }

    public void DrawBakeButton()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (_isBaking)
        {
            GUILayout.Label($"Baking... {_bakeProgress * 100f:F0}%", EditorStyles.miniLabel);
            EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100f, 18f), _bakeProgress, "Baking");
            if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                Lightmapping.Cancel();
                _isBaking = false;
                _bakeProgress = 0f;
            }
        }
        else
        {
            if (GUILayout.Button("Generate Lighting", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                _isBaking = true;
                _bakeProgress = 0f;
                if (_autoGenerate)
                {
                    if (_activeLightingSettings != null)
                    {
                        Lightmapping.lightingSettings = _activeLightingSettings;
                    }
                    Lightmapping.Bake();
                    _isBaking = false;
                    _bakeProgress = 1f;
                }
                else
                {
                    Lightmapping.BakeAsync();
                    _isBaking = false;
                    _bakeProgress = 1f;
                }
            }

            if (GUILayout.Button("Cancel Bake", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Lightmapping.Cancel();
            }
        }

        GUILayout.EndHorizontal();
    }

    public static LightingWindow OpenWindow()
    {
        return GetWindow<LightingWindow>("Lighting");
    }
}
