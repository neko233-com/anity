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

    // Walk up to find the outermost prefab instance root
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

  public static Object? GetCorrespondingObjectFromOriginalSource(Object source)
  {
    if (source is null)
    {
      return null;
    }

    // In a real implementation, this would find the original asset
    return source;
  }

  public static Object? GetOriginalSourceObjectFromObject(Object targetObject)
  {
    if (targetObject is null)
    {
      return null;
    }

    // In a real implementation, this would find the original source
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
    // In a real implementation, this would check if the prefab is immutable
    return false;
  }

  public static bool IsPartOfPrefabAsset(Object targetObject)
  {
    if (targetObject is null)
    {
      return false;
    }

    // Check if this is part of a prefab asset (not an instance)
    return targetObject is GameObject go && _loadedPrefabs.ContainsValue(go);
  }

  public static string SaveAsPrefabAsset(GameObject root, string savePath)
  {
    if (root is null || string.IsNullOrWhiteSpace(savePath))
    {
      return string.Empty;
    }

    // Ensure directory exists
    var dir = Path.GetDirectoryName(savePath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
      Directory.CreateDirectory(dir);
    }

    // In a real implementation, this would serialize the prefab
    _instanceToAsset[root] = savePath;
    return savePath;
  }

  public static string SaveAsPrefabAssetAndConnect(GameObject root, string savePath)
  {
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

    // In a real implementation, this would apply changes back to the prefab asset
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

    // In a real implementation, this would revert changes from the prefab asset
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    if (prefab is not null)
    {
      // Copy prefab properties back to instance
    }
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

  public static void RecordPrefabInstancePropertyModifications(GameObject instanceRoot)
  {
    _ = instanceRoot;
  }
}

public enum PrefabInstanceStatus
{
  NotAPrefab,
  Connected,
  Disconnected,
  Missing,
  OutOfDate
}

public enum PrefabImportMode
{
  Normal,
  Experimental
}
