using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public sealed class PreferencesWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private int _selectedProvider;
    private readonly List<SettingsProvider> _providers = new();

    public static PreferencesWindow? instance { get; private set; }

    public PreferencesWindow()
    {
        titleContent = new GUIContent("Preferences");
        minSize = new Vector2(500f, 400f);
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
        _providers.AddRange(SettingsProvider.GetSettingsProviders(SettingsScope.User));
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
        GUILayout.BeginVertical(GUILayout.Width(180f));
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
            GUILayout.Label("Select a preference category", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndVertical();
    }

    [MenuItem("Edit/Preferences...")]
    public static PreferencesWindow ShowWindow()
    {
        return GetWindow<PreferencesWindow>("Preferences");
    }
}
