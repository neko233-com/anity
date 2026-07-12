using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor.SceneManagement;

public enum PrefabStageMode
{
    InIsolation = 0,
    InContext = 1
}

/// <summary>
/// Prefab Mode: editing a prefab asset in isolation or in context (Unity 2022 PrefabStage).
/// </summary>
public sealed class PrefabStage
{
    private static PrefabStage? _current;
    private static readonly List<PrefabStage> _stageStack = new();

    private readonly string _assetPath;
    private readonly GameObject _prefabContentsRoot;
    private readonly Scene _scene;
    private readonly PrefabStageMode _mode;
    private bool _isDirty;
    private bool _isValid = true;

    public static PrefabStage? currentPrefabStage => _current;
    public static event Action<PrefabStage>? prefabStageOpened;
    public static event Action<PrefabStage>? prefabStageClosing;
    public static event Action<PrefabStage>? prefabStageDirtied;
    public static event Action<PrefabStage>? prefabStageSaved;

    public string assetPath => _assetPath;
    public GameObject prefabContentsRoot => _prefabContentsRoot;
    public Scene scene => _scene;
    public PrefabStageMode mode => _mode;
    public bool isDirty => _isDirty;
    public bool isValid => _isValid;
    public string prefabAssetPath => _assetPath;

    private PrefabStage(string assetPath, GameObject root, Scene scene, PrefabStageMode mode)
    {
        _assetPath = assetPath;
        _prefabContentsRoot = root;
        _scene = scene;
        _mode = mode;
    }

    public static PrefabStage? OpenPrefab(string assetPath)
    {
        return OpenPrefab(assetPath, PrefabStageMode.InIsolation);
    }

    public static PrefabStage? OpenPrefab(string assetPath, PrefabStageMode mode)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return null;

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        GameObject root;
        if (asset != null)
        {
            root = UnityEngine.Object.Instantiate(asset);
            root.name = asset.name;
        }
        else
        {
            // create empty prefab contents shell
            root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(assetPath));
            AssetDatabase.CreateAsset(root, assetPath);
        }

        var scene = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(root, scene);

        var stage = new PrefabStage(assetPath, root, scene, mode);
        if (_current != null)
            _stageStack.Add(_current);
        _current = stage;

        Selection.activeGameObject = root;
        prefabStageOpened?.Invoke(stage);
        EditorApplication.RepaintHierarchyWindow();
        return stage;
    }

    public static bool CloseCurrent()
    {
        if (_current == null) return false;
        return _current.Close();
    }

    public bool Close()
    {
        if (!_isValid) return false;
        prefabStageClosing?.Invoke(this);

        if (_isDirty)
        {
            Save();
        }

        if (_prefabContentsRoot != null)
            UnityEngine.Object.DestroyImmediate(_prefabContentsRoot);

        EditorSceneManager.ClosePreviewScene(_scene);
        _isValid = false;

        if (_current == this)
        {
            _current = _stageStack.Count > 0
                ? _stageStack[_stageStack.Count - 1]
                : null;
            if (_stageStack.Count > 0)
                _stageStack.RemoveAt(_stageStack.Count - 1);
        }

        EditorApplication.RepaintHierarchyWindow();
        return true;
    }

    public void Save()
    {
        if (!_isValid || _prefabContentsRoot == null) return;

        // Replace asset with current contents
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
        if (existing != null)
        {
            PrefabUtility.SaveAsPrefabAsset(_prefabContentsRoot, _assetPath);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(_prefabContentsRoot, _assetPath);
        }

        _isDirty = false;
        prefabStageSaved?.Invoke(this);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public void MarkDirty()
    {
        if (!_isValid) return;
        if (!_isDirty)
        {
            _isDirty = true;
            prefabStageDirtied?.Invoke(this);
        }
    }

    public bool IsPartOfPrefabContents(GameObject go)
    {
        if (go == null || _prefabContentsRoot == null) return false;
        if (go == _prefabContentsRoot) return true;
        var t = go.transform;
        while (t != null)
        {
            if (t.gameObject == _prefabContentsRoot) return true;
            t = t.parent;
        }
        return false;
    }

    public static bool IsInPrefabStage() => _current != null && _current._isValid;

    public static GameObject? GetCurrentRoot() => _current?.prefabContentsRoot;
}

/// <summary>
/// Utility entry points used by Hierarchy / Project double-click and menu items.
/// </summary>
public static class PrefabStageUtility
{
    public static PrefabStage? GetCurrentPrefabStage() => PrefabStage.currentPrefabStage;

    public static PrefabStage? OpenPrefab(string assetPath) => PrefabStage.OpenPrefab(assetPath);

    public static PrefabStage? OpenPrefab(string assetPath, PrefabStageMode mode) =>
        PrefabStage.OpenPrefab(assetPath, mode);

    public static void GoToMainStage()
    {
        while (PrefabStage.currentPrefabStage != null)
            PrefabStage.CloseCurrent();
    }

    public static bool EnterPrefabMode(GameObject prefabAssetOrInstance)
    {
        if (prefabAssetOrInstance == null) return false;
        string path = AssetDatabase.GetAssetPath(prefabAssetOrInstance);
        if (string.IsNullOrEmpty(path))
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(prefabAssetOrInstance);
            if (root != null)
                path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        }
        if (string.IsNullOrEmpty(path)) return false;
        return PrefabStage.OpenPrefab(path) != null;
    }
}
