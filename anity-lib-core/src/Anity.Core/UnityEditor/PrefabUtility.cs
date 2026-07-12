using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityEditor;

public static class PrefabUtility
{
  private static readonly Dictionary<string, GameObject> _loadedPrefabs = new();
  private static readonly Dictionary<GameObject, string> _instanceToAsset = new();

  public static GameObject? LoadPrefabContents(string assetPath)
  {
    if (string.IsNullOrWhiteSpace(assetPath))
    {
      return null;
    }

    if (_loadedPrefabs.TryGetValue(assetPath, out var existing))
    {
      return existing;
    }

    var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    if (loaded is not null)
    {
      _loadedPrefabs[assetPath] = loaded;
    }

    return loaded;
  }

  public static void UnloadPrefabContents(GameObject prefabContents)
  {
    if (prefabContents is null)
    {
      return;
    }

    foreach (var kvp in _loadedPrefabs)
    {
      if (ReferenceEquals(kvp.Value, prefabContents))
      {
        _loadedPrefabs.Remove(kvp.Key);
        break;
      }
    }
  }

  public static GameObject? GetOutermostPrefabInstanceRoot(Object targetObject)
  {
    if (targetObject is null)
    {
      return null;
    }

    var go = targetObject as GameObject ?? (targetObject as Component)?.gameObject;
    if (go is null)
    {
      return null;
    }

    var current = go;
    while (current.transform.parent is not null)
    {
      if (_instanceToAsset.ContainsKey(current))
      {
        return current;
      }
      current = current.transform.parent.gameObject;
    }

    return _instanceToAsset.ContainsKey(current) ? current : null;
  }

  public static GameObject? InstantiatePrefab(GameObject? original)
  {
    if (original is null)
    {
      return null;
    }

    var clone = (GameObject)UnityEngine.Object.Instantiate(original);
    clone.name = original.name;
    return clone;
  }

  public static GameObject? InstantiatePrefab(string assetPath)
  {
    if (string.IsNullOrWhiteSpace(assetPath)) return null;
    var original = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    if (original is null) return null;
    return InstantiatePrefab(original);
  }

  public static GameObject InstantiatePrefab(GameObject original, GameObject? parent)
  {
    if (original is null)
    {
      return new GameObject("PrefabInstance");
    }

    var clone = (GameObject)UnityEngine.Object.Instantiate(original);
    clone.name = original.name;

    if (parent is not null)
    {
      clone.transform.SetParent(parent.transform, false);
    }

    return clone;
  }

  public static GameObject InstantiatePrefab(GameObject original, Transform? parent)
  {
    if (original is null)
    {
      return new GameObject("PrefabInstance");
    }

    var clone = (GameObject)UnityEngine.Object.Instantiate(original);
    clone.name = original.name;

    if (parent is not null)
    {
      clone.transform.SetParent(parent, false);
    }

    return clone;
  }

  public static GameObject InstantiatePrefab(GameObject original, Transform? parent, bool worldPositionStays)
  {
    if (original is null)
    {
      return new GameObject("PrefabInstance");
    }

    var clone = (GameObject)UnityEngine.Object.Instantiate(original);
    clone.name = original.name;

    if (parent is not null)
    {
      clone.transform.SetParent(parent, worldPositionStays);
    }

    return clone;
  }

  public static GameObject InstantiatePrefab(GameObject original, Vector3 position, Quaternion rotation)
  {
    if (original is null)
    {
      return new GameObject("PrefabInstance");
    }

    var clone = UnityEngine.Object.Instantiate(original, position, rotation) as GameObject;
    if (clone is null)
    {
      return new GameObject("PrefabInstance");
    }
    clone.name = original.name;
    return clone;
  }

  public static GameObject InstantiatePrefab(GameObject original, Vector3 position, Quaternion rotation, Transform? parent)
  {
    if (original is null)
    {
      return new GameObject("PrefabInstance");
    }

    var clone = UnityEngine.Object.Instantiate(original, position, rotation) as GameObject;
    if (clone is null)
    {
      return new GameObject("PrefabInstance");
    }
    clone.name = original.name;

    if (parent is not null)
    {
      clone.transform.SetParent(parent, false);
    }

    return clone;
  }

  public static string? GetPrefabAssetPathOfNearestInstanceRoot(Object componentOrGameObject)
  {
    if (componentOrGameObject is null)
    {
      return null;
    }

    var go = componentOrGameObject as GameObject ?? (componentOrGameObject as Component)?.gameObject;
    if (go is null)
    {
      return null;
    }

    return _instanceToAsset.TryGetValue(go, out var assetPath) ? assetPath : null;
  }

  public static T? GetCorrespondingObjectFromSource<T>(T source) where T : Object
  {
    return GetCorrespondingObjectFromSource((Object)source) as T;
  }

  public static Object? GetCorrespondingObjectFromSource(Object source)
  {
    if (source is null)
    {
      return null;
    }
    return GetCorrespondingObjectFromOriginalSource(source);
  }

  public static Object? GetCorrespondingObjectFromSource(Object source, bool outermostPrefab)
  {
    if (source is null) return null;
    _ = outermostPrefab;
    return GetCorrespondingObjectFromOriginalSource(source);
  }

  public static T? GetCorrespondingObjectFromOriginalSource<T>(T source) where T : Object
  {
    return GetCorrespondingObjectFromOriginalSource((Object)source) as T;
  }

  public static Object? GetCorrespondingObjectFromOriginalSource(Object source)
  {
    if (source is null)
    {
      return null;
    }

    return source;
  }

  public static Object? GetOriginalSourceObjectFromObject(Object targetObject)
  {
    if (targetObject is null)
    {
      return null;
    }

    return targetObject;
  }

  public static bool IsPartOfPrefabInstance(Object targetObject)
  {
    if (targetObject is null)
    {
      return false;
    }

    var go = targetObject as GameObject ?? (targetObject as Component)?.gameObject;
    return go is not null && _instanceToAsset.ContainsKey(go);
  }

  public static bool IsAnyPrefabInstanceLoaded()
  {
    return _loadedPrefabs.Count > 0;
  }

  public static bool IsPartOfImmutablePrefab(Object targetObject)
  {
    return false;
  }

  public static bool IsPartOfPrefabAsset(Object targetObject)
  {
    if (targetObject is null)
    {
      return false;
    }

    var go = targetObject as GameObject ?? (targetObject as Component)?.gameObject;
    return go is not null && _loadedPrefabs.ContainsValue(go);
  }

  public static bool IsPrefabInstance(Object targetObject)
  {
    return IsPartOfPrefabInstance(targetObject);
  }

  public static GameObject? GetNearestPrefabInstanceRoot(Object componentOrGameObject)
  {
    if (componentOrGameObject is null) return null;
    var go = componentOrGameObject as GameObject ?? (componentOrGameObject as Component)?.gameObject;
    if (go is null) return null;

    var current = go;
    while (current is not null)
    {
      if (_instanceToAsset.ContainsKey(current))
        return current;
      current = current.transform.parent?.gameObject;
    }
    return null;
  }

  public static bool IsPartOfNonAssetPrefabInstance(Object targetObject)
  {
    if (targetObject is null) return false;
    if (IsPartOfPrefabAsset(targetObject)) return false;
    return IsPartOfPrefabInstance(targetObject);
  }

  public static bool IsPrefabAsset(Object targetObject)
  {
    if (targetObject is null) return false;
    var assetPath = AssetDatabase.GetAssetPath(targetObject);
    return !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
  }

  public static string SaveAsPrefabAsset(GameObject root, string savePath)
  {
    bool success;
    return SaveAsPrefabAsset(root, savePath, out success);
  }

  public static string SaveAsPrefabAsset(GameObject root, string savePath, out bool success)
  {
    success = false;
    if (root is null || string.IsNullOrWhiteSpace(savePath))
    {
      return string.Empty;
    }

    var dir = Path.GetDirectoryName(savePath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
      Directory.CreateDirectory(dir);
    }

    _loadedPrefabs[savePath] = root;
    success = true;
    return savePath;
  }

  public static bool SavePrefabAsset(GameObject root)
  {
    if (root is null) return false;

    string? existingPath = null;
    foreach (var kvp in _loadedPrefabs)
    {
      if (ReferenceEquals(kvp.Value, root))
      {
        existingPath = kvp.Key;
        break;
      }
    }

    if (string.IsNullOrEmpty(existingPath))
    {
      return false;
    }

    _loadedPrefabs[existingPath] = root;
    return true;
  }

  public static void UnpackPrefabInstance(GameObject instanceRoot, PrefabUnpackMode unpackMode)
  {
    UnpackPrefabInstance(instanceRoot, unpackMode, InteractionMode.AutomatedAction);
  }

  public static void UnpackPrefabInstance(GameObject instanceRoot, PrefabUnpackMode unpackMode, InteractionMode action)
  {
    if (instanceRoot is null) return;
    _ = unpackMode;
    _ = action;

    UnpackPrefabInstanceInternal(instanceRoot, unpackMode);
  }

  public static void UnpackPrefabInstanceAndReturnNewOutermostRoots(GameObject instanceRoot, PrefabUnpackMode unpackMode)
  {
    UnpackPrefabInstance(instanceRoot, unpackMode);
  }

  private static void UnpackPrefabInstanceInternal(GameObject instanceRoot, PrefabUnpackMode unpackMode)
  {
    _instanceToAsset.Remove(instanceRoot);

    if (unpackMode == PrefabUnpackMode.Completely)
    {
      for (int i = 0; i < instanceRoot.transform.childCount; i++)
      {
        var child = instanceRoot.transform.GetChild(i).gameObject;
        UnpackPrefabInstanceInternal(child, unpackMode);
      }
    }
    else
    {
      for (int i = 0; i < instanceRoot.transform.childCount; i++)
      {
        var child = instanceRoot.transform.GetChild(i).gameObject;
        if (_instanceToAsset.ContainsKey(child))
        {
          _instanceToAsset.Remove(child);
        }
      }
    }
  }

  public static string SaveAsPrefabAssetAndConnect(GameObject root, string savePath)
  {
    return SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
  }

  public static string SaveAsPrefabAssetAndConnect(GameObject root, string savePath, InteractionMode action)
  {
    _ = action;
    var result = SaveAsPrefabAsset(root, savePath);
    if (!string.IsNullOrEmpty(result))
    {
      _instanceToAsset[root] = savePath;
    }
    return result;
  }

  public static bool ReplacePrefab(GameObject instance, GameObject? targetPrefab)
  {
    if (instance is null)
    {
      return false;
    }

    if (targetPrefab is not null)
    {
      _instanceToAsset[instance] = UnityEditor.AssetDatabase.GetAssetPath(targetPrefab);
    }

    return true;
  }

  public static Object ReplacePrefab(GameObject instance, Object targetPrefab, ReplacePrefabOptions options)
  {
    _ = options;
    if (instance is null || targetPrefab is null) return null;
    if (targetPrefab is GameObject go)
    {
      ReplacePrefab(instance, go);
    }
    return targetPrefab;
  }

  public static void DisconnectPrefabInstance(Object objectToDisconnect)
  {
    if (objectToDisconnect is null)
    {
      return;
    }

    var go = objectToDisconnect as GameObject ?? (objectToDisconnect as Component)?.gameObject;
    if (go is not null)
    {
      _instanceToAsset.Remove(go);
    }
  }

  public static PrefabInstanceStatus GetPrefabInstanceStatus(Object targetObject)
  {
    if (targetObject is null)
    {
      return PrefabInstanceStatus.NotAPrefab;
    }

    var go = targetObject as GameObject ?? (targetObject as Component)?.gameObject;
    if (go is null)
    {
      return PrefabInstanceStatus.NotAPrefab;
    }

    if (_instanceToAsset.ContainsKey(go))
    {
      return PrefabInstanceStatus.Connected;
    }

    return PrefabInstanceStatus.NotAPrefab;
  }

  public static bool ApplyPrefabInstance(GameObject instanceRoot)
  {
    if (instanceRoot is null)
    {
      return false;
    }

    if (!_instanceToAsset.TryGetValue(instanceRoot, out var assetPath))
    {
      return false;
    }

    return true;
  }

  public static bool ApplyPrefabInstance(GameObject instanceRoot, GameObject prefabAsset)
  {
    if (instanceRoot is null || prefabAsset is null)
    {
      return false;
    }

    var assetPath = UnityEditor.AssetDatabase.GetAssetPath(prefabAsset);
    if (string.IsNullOrEmpty(assetPath))
    {
      return false;
    }

    _instanceToAsset[instanceRoot] = assetPath;
    return true;
  }

  public static void ApplyPrefabInstance(GameObject instanceRoot, InteractionMode action)
  {
    if (instanceRoot is null) return;
    _ = action;
    ApplyPrefabInstance(instanceRoot);
  }

  public static void RevertPrefabInstance(GameObject instanceRoot)
  {
    if (instanceRoot is null)
    {
      return;
    }

    if (!_instanceToAsset.TryGetValue(instanceRoot, out var assetPath))
    {
      return;
    }
  }

  public static void RevertPrefabInstance(GameObject instanceRoot, InteractionMode action)
  {
    if (instanceRoot is null) return;
    _ = action;
    RevertPrefabInstance(instanceRoot);
  }

  public static GameObject FindPrefabInstanceRoot(GameObject instance)
  {
    if (instance is null)
    {
      return instance;
    }

    return GetOutermostPrefabInstanceRoot(instance) ?? instance;
  }

  public static bool IsPropertyOverriddenByPrefabInstance(GameObject instanceRoot, string propertyPath)
  {
    _ = instanceRoot;
    _ = propertyPath;
    return false;
  }

  public static void RecordPrefabInstancePropertyModifications(Object targetObject)
  {
    _ = targetObject;
  }

  public static bool IsPartOfAnyPrefab(Object targetObject)
  {
    return IsPartOfPrefabInstance(targetObject) || IsPartOfPrefabAsset(targetObject);
  }

  public static PrefabAssetType GetPrefabAssetType(GameObject gameObject)
  {
    if (gameObject is null) return PrefabAssetType.Missing;
    if (!IsPartOfPrefabInstance(gameObject)) return PrefabAssetType.NotAPrefab;
    return PrefabAssetType.Regular;
  }

  public static bool HasPrefabInstanceAnyOverrides(GameObject instanceRoot, bool includeDefaultOverride)
  {
    return false;
  }

  public static GameObject[] GetRemovedGameObjects(GameObject instanceRoot)
  {
    return Array.Empty<GameObject>();
  }

  public static GameObject[] GetAddedGameObjects(GameObject instanceRoot)
  {
    return Array.Empty<GameObject>();
  }
}

public enum PrefabInstanceStatus
{
  NotAPrefab,
  Connected,
  Disconnected,
  MissingAsset
}

public enum PrefabImportMode
{
  Normal,
  Experimental
}

public enum PrefabAssetType
{
  Missing,
  NotAPrefab,
  Regular,
  Model,
  Variant,
  MissingAsset
}

public enum InteractionMode
{
  AutomatedAction,
  UserAction
}

public enum PrefabUnpackMode
{
  OutermostRoot,
  Completely
}

[Flags]
public enum ReplacePrefabOptions
{
  Default = 0,
  ConnectToPrefab = 1,
  ReplaceNameBased = 2
}
