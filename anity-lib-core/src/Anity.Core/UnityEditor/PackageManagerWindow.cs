using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

namespace UnityEditor;

public sealed class PackageManagerWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private string _searchFilter = string.Empty;
    private PackageInfo[]? _packages;
    private int _selectedPackage = -1;

    public static PackageManagerWindow? instance { get; private set; }

    public PackageManagerWindow()
    {
        titleContent = new GUIContent("Package Manager");
        minSize = new Vector2(700f, 500f);
        instance = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshPackages();
    }

    private void RefreshPackages()
    {
        _packages = Client.GetAllPackages();
    }

    protected override void OnGUI()
    {
        DrawToolbar();
        GUILayout.BeginHorizontal();
        DrawPackageList();
        DrawPackageDetails();
        GUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
        if (GUILayout.Button("×", EditorStyles.toolbarSearchFieldCancelButton, GUILayout.Width(16f)))
            _searchFilter = string.Empty;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("↻ Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            RefreshPackages();

        if (GUILayout.Button("+ Add", EditorStyles.toolbarButton, GUILayout.Width(50f)))
        {
        }

        GUILayout.EndHorizontal();
    }

    private void DrawPackageList()
    {
        GUILayout.BeginVertical(GUILayout.Width(250f));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        if (_packages != null)
        {
            for (int i = 0; i < _packages.Length; i++)
            {
                var pkg = _packages[i];
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !pkg.name.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase) &&
                    !pkg.displayName.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                bool selected = _selectedPackage == i;
                var style = selected ? EditorStyles.selectedLabel : EditorStyles.label;
                if (GUILayout.Button(pkg.displayName, style))
                {
                    _selectedPackage = i;
                }
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawPackageDetails()
    {
        GUILayout.BeginVertical();
        if (_selectedPackage >= 0 && _packages != null && _selectedPackage < _packages.Length)
        {
            var pkg = _packages[_selectedPackage];
            GUILayout.Label(pkg.displayName, EditorStyles.boldLabel);
            GUILayout.Label(pkg.name, EditorStyles.miniLabel);
            GUILayout.Label($"v{pkg.version}", EditorStyles.miniLabel);
            GUILayout.Space(8f);
            if (!string.IsNullOrEmpty(pkg.description))
            {
                GUILayout.Label(pkg.description, EditorStyles.wordWrappedLabel);
            }
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a package", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndVertical();
    }

    [MenuItem("Window/Package Manager")]
    public static PackageManagerWindow ShowWindow()
    {
        return GetWindow<PackageManagerWindow>("Package Manager");
    }
}
