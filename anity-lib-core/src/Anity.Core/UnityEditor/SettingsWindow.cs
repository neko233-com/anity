using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public sealed class SettingsWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private int _selectedProvider;
    private readonly List<SettingsProvider> _providers = new();

    public static SettingsWindow? instance { get; private set; }

    public SettingsWindow()
    {
        titleContent = new GUIContent("Project Settings");
        minSize = new Vector2(600f, 500f);
        instance = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshProviders();
    }

    private void RefreshProviders()
    {
        _providers.Clear();
        _providers.AddRange(SettingsProvider.GetSettingsProviders(SettingsScope.Project));
    }

    protected override void OnGUI()
    {
        GUILayout.BeginHorizontal();
        DrawProviderList();
        DrawProviderGUI();
        GUILayout.EndHorizontal();
    }

    private void DrawProviderList()
    {
        GUILayout.BeginVertical(GUILayout.Width(200f));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        for (int i = 0; i < _providers.Count; i++)
        {
            bool selected = _selectedProvider == i;
            var style = selected ? EditorStyles.selectedLabel : EditorStyles.label;
            if (GUILayout.Button(_providers[i].label, style))
            {
                _selectedProvider = i;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawProviderGUI()
    {
        GUILayout.BeginVertical();
        if (_selectedProvider >= 0 && _selectedProvider < _providers.Count)
        {
            GUILayout.Label(_providers[_selectedProvider].label, EditorStyles.boldLabel);
            _providers[_selectedProvider].OnGUI();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a settings category", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndVertical();
    }

    [MenuItem("Edit/Project Settings...")]
    public static SettingsWindow ShowWindow()
    {
        return GetWindow<SettingsWindow>("Project Settings");
    }
}
