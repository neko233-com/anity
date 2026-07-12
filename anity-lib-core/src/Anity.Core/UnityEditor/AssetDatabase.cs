using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

public static class AssetDatabase
{
  private static readonly Dictionary<string, object?> _assets = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, HashSet<string>> _children = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<object, string[]> _labels = new();
  private static int _guidCounter;
  private static bool _isEditing;

  public static string CreateAsset(object? asset, string path)
  {
    path = Normalize(path);
    var guid = GuidFromPath(path);
    _assets[path] = asset;
    _assetGuid[path] = guid;
    return guid;
  }

  public static void AddObjectToAsset(object? asset, string path)
  {
    _ = asset;
    if (string.IsNullOrWhiteSpace(path))
    {
      return;
    }

    path = Normalize(path);
    _assets[path] = asset;
    IndexChild(Path.GetDirectoryName(path), path);
  }

  public static void AddObjectToAsset(Object asset, string path, int flags)
  {
    _ = flags;
    AddObjectToAsset((object?)asset, path);
  }

  public static bool DeleteAsset(string path)
  {
    path = Normalize(path);
    if (_assets.Remove(path))
    {
      _assetGuid.Remove(path);
      RemoveFromParentIndex(path);
      return true;
    }

    return _assets.Remove(path);
  }

  public static AssetDeleteResult DeleteAssets(string[] assetPathNames, bool allowUndo = false)
  {
    _ = allowUndo;
    var removed = 0;
    foreach (var p in assetPathNames)
    {
      if (DeleteAsset(p))
      {
        removed++;
      }
    }
    return removed > 0 ? AssetDeleteResult.Deleted : AssetDeleteResult.DidNotDelete;
  }

  public static string GUIDToAssetPath(string guid)
  {
    var found = _assetGuid.FirstOrDefault(kv => string.Equals(kv.Value, guid, StringComparison.Ordinal));
    return found.Key ?? string.Empty;
  }

  public static string GetAssetPathFromGUID(string guid)
  {
    return GUIDToAssetPath(guid);
  }

  public static T? LoadAssetAtPath<T>(string assetPath) where T : class
  {
    return LoadAssetAtPath(assetPath) as T;
  }

  public static object? LoadAssetAtPath(string path)
  {
    path = Normalize(path);
    _assets.TryGetValue(path, out var value);
    return value;
  }

  public static Object? LoadAssetAtPath(string path, Type type)
  {
    var value = LoadAssetAtPath(path);
    return value as Object;
  }

  public static object? LoadMainAssetAtPath(string path)
  {
    return LoadAssetAtPath(path);
  }

  public static T? LoadMainAssetAtPath<T>(string path) where T : class
  {
    return LoadAssetAtPath(path) as T;
  }

  public static Type? GetMainAssetTypeAtPath(string assetPath)
  {
    var obj = LoadAssetAtPath(assetPath);
    return obj?.GetType();
  }

  public static T? LoadAssetAtPath<T>(string assetPath, string? optionalType) where T : class
  {
    _ = optionalType;
    return LoadAssetAtPath<T>(assetPath);
  }

  public static string GetAssetPath(object asset)
  {
    foreach (var kv in _assets)
    {
      if (ReferenceEquals(kv.Value, asset))
      {
        return kv.Key;
      }
    }
    return string.Empty;
  }

  public static bool Contains(Object? asset)
  {
    return _assets.Values.Any(v => ReferenceEquals(v, asset));
  }

  public static string AssetPathToGUID(string assetPath)
  {
    return GuidFromPath(Normalize(assetPath));
  }

  public static string[] GetAllAssetPaths()
  {
    return _assets.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
  }

  public static string[] GetAssetPathsFromAssetBundle(string assetBundleName)
  {
    _ = assetBundleName;
    return Array.Empty<string>();
  }

  public static string[] GetAllAssetBundleNames()
  {
    return Array.Empty<string>();
  }

  public static string[] GetUnusedAssetBundleNames()
  {
    return Array.Empty<string>();
  }

  public static string[] GetAssetPathsFromCollection(string path)
  {
    _ = path;
    return Array.Empty<string>();
  }

  public static string[] GetSubFolders(string folder)
  {
    var prefix = Normalize(folder);
    if (string.IsNullOrWhiteSpace(prefix))
    {
      return Array.Empty<string>();
    }

    var depth = prefix.Count(c => c == '/');
    var direct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var key in _assets.Keys)
    {
      if (string.Equals(key, prefix, StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      if (!key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var rest = key[(prefix.Length + 1)..];
      var first = rest.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
      if (string.IsNullOrEmpty(first))
      {
        continue;
      }

      var candidate = Normalize(prefix + "/" + first);
      var candidateDepth = candidate.Count(c => c == '/');
      if (candidateDepth == depth + 1)
      {
        direct.Add(candidate);
      }
    }

    return direct.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
  }

  public static string[] FindAssets(string filter, string[]? searchInFolders = null)
  {
    _ = searchInFolders;
    if (string.IsNullOrWhiteSpace(filter))
    {
      return _assets.Keys.Select(AssetPathToGUID).ToArray();
    }

    return _assets.Keys.Where(p => p.Contains(filter, StringComparison.OrdinalIgnoreCase))
      .Select(AssetPathToGUID)
      .ToArray();
  }

  public static string[] FindAssets(string filter)
  {
    return FindAssets(filter, (string[]?)null);
  }

  public static string[] FindAssets(string filter, int filterMode)
  {
    _ = filterMode;
    return FindAssets(filter, (string[]?)null);
  }

  public static Object[] LoadAllAssetsAtPath(string assetPath)
  {
    assetPath = Normalize(assetPath);
    if (string.IsNullOrWhiteSpace(assetPath))
    {
      return Array.Empty<Object>();
    }

    return LoadAssetAtPath(assetPath) is Object obj ? new[] { obj } : Array.Empty<Object>();
  }

  public static Hash128 GetAssetDependencyHash(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return default;
    }
    var normalized = Normalize(path);
    var hash = normalized.GetHashCode();
    return new Hash128((uint)hash, (uint)(hash >> 16), (uint)(hash >> 8), 0);
  }

  public static bool IsMainAsset(Object obj)
  {
    if (obj is null) return false;
    var path = GetAssetPath(obj);
    if (string.IsNullOrEmpty(path)) return false;
    var main = LoadMainAssetAtPath(path);
    return ReferenceEquals(main, obj);
  }

  public static bool IsSubAsset(Object obj)
  {
    if (obj is null) return false;
    return Contains(obj) && !IsMainAsset(obj);
  }

  public static void ForceReserializeAssets(string[] assetPaths)
  {
    ForceReserializeAssets(assetPaths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
  }

  public static void ForceReserializeAssets(string[] assetPaths, ForceReserializeAssetsOptions options)
  {
    _ = options;
    foreach (var path in assetPaths)
    {
      ImportAsset(path);
    }
  }

  public static void ForceReserializeAssets(IEnumerable<string> assetPaths, ForceReserializeAssetsOptions options = ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata)
  {
    ForceReserializeAssets(assetPaths.ToArray(), options);
  }

  public static bool WriteImportSettingsIfDirty(string path)
  {
    _ = path;
    return false;
  }

  public static bool ValidateMoveAsset(string oldPath, string newPath)
  {
    oldPath = Normalize(oldPath);
    newPath = Normalize(newPath);
    if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath)) return false;
    if (!_assets.ContainsKey(oldPath)) return false;
    if (_assets.ContainsKey(newPath)) return false;
    return true;
  }

  public static string CreateFolder(string parentFolder, string newFolderName)
  {
    var path = Normalize(parentFolder + "/" + newFolderName).TrimEnd('/');
    if (!_assets.ContainsKey(path))
    {
      _assets[path] = null;
      IndexChild(parentFolder, path);
    }
    return path;
  }

  public static bool IsValidFolder(string path)
  {
    path = Normalize(path);
    if (_assets.ContainsKey(path))
    {
      return true;
    }

    var hasChild = _assets.Keys.Any(k => k.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase));
    return hasChild;
  }

  public static string[] FindAssets(string filter, Type[] types)
  {
    _ = types;
    return FindAssets(filter, (string[]?)null);
  }

  public static string[] FindAssets(Type type)
  {
    if (type is null)
    {
      return Array.Empty<string>();
    }

    return _assets.Where(kv => kv.Value?.GetType() == type)
      .Select(kv => AssetPathToGUID(kv.Key))
      .ToArray();
  }

  public static string[] FindAssets(string filter, string[] searchInFolders, int type)
  {
    _ = type;
    return FindAssets(filter, searchInFolders);
  }

  public static string[] GetAssetPathsFromAssetBundle(string assetBundleName, bool isSceneAssetBundle)
  {
    _ = isSceneAssetBundle;
    return GetAssetPathsFromAssetBundle(assetBundleName);
  }

  public static string GenerateUniqueAssetPath(string path)
  {
    path = Normalize(path);
    if (!_assets.ContainsKey(path))
    {
      return path;
    }

    var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
    var fileName = Path.GetFileNameWithoutExtension(path);
    var extension = Path.GetExtension(path);
    if (string.IsNullOrWhiteSpace(directory))
    {
      directory = string.Empty;
    }

    for (var i = 1; i < 10000; i++)
    {
      var candidate = string.IsNullOrEmpty(directory)
        ? $"{fileName} {i}{extension}"
        : Normalize(directory + "/" + $"{fileName} {i}{extension}");
      if (!_assets.ContainsKey(candidate))
      {
        return candidate;
      }
    }

    return path;
  }

  public static string MoveAsset(string oldPath, string newPath)
  {
    oldPath = Normalize(oldPath);
    newPath = Normalize(newPath);
    if (!_assets.TryGetValue(oldPath, out var value))
    {
      return string.Empty;
    }

    _assets.Remove(oldPath);
    _assetGuid.Remove(oldPath);
    RemoveFromParentIndex(oldPath);

    _assets[newPath] = value;
    _assetGuid[newPath] = GuidFromPath(newPath);
    IndexChild(Path.GetDirectoryName(oldPath) ?? string.Empty, newPath);
    return newPath;
  }

  public static string RenameAsset(string path, string newName)
  {
    path = Normalize(path);
    if (!_assets.TryGetValue(path, out var value))
    {
      return path;
    }

    var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
    var parentPrefix = string.IsNullOrWhiteSpace(parent) ? string.Empty : Normalize(parent);
    var target = string.IsNullOrWhiteSpace(parentPrefix)
      ? Normalize(newName)
      : Normalize(parentPrefix + "/" + newName);
    _assets.Remove(path);
    _assetGuid.Remove(path);
    _assets[target] = value;
    _assetGuid[target] = GuidFromPath(target);
    IndexChild(parentPrefix, target);
    RemoveFromParentIndex(path);
    return target;
  }

  public static bool CopyAsset(string path, string newPath)
  {
    path = Normalize(path);
    newPath = Normalize(newPath);
    if (!_assets.TryGetValue(path, out var value))
    {
      return false;
    }

    var duplicated = value;
    _assets[newPath] = duplicated;
    _assetGuid[newPath] = GuidFromPath(newPath);
    IndexChild(Path.GetDirectoryName(path) ?? string.Empty, newPath);
    return true;
  }

  public static bool MoveAsset(string[] paths, string destinationFolder)
  {
    _ = paths;
    _ = destinationFolder;
    return false;
  }

  public static string MoveAssetToPath(string oldPath, string newPath)
  {
    return MoveAsset(oldPath, newPath);
  }

  public static bool CopyAssetToFolder(string sourcePath, string destinationPath)
  {
    return CopyAsset(sourcePath, destinationPath);
  }

  public static void Refresh(ImportAssetOptions options, bool forceUpdate)
  {
    _ = forceUpdate;
    Refresh(options);
  }

  public static void SaveAssets()
  {
    // in-memory database: no persistence needed
  }

  public static void ImportPackage(string packagePath, bool interactive)
  {
    _ = packagePath;
    _ = interactive;
  }

  public static bool MoveAssetToTrash(string path)
  {
    return DeleteAsset(path);
  }

  public static void StartAssetEditing()
  {
    _isEditing = true;
  }

  public static void StopAssetEditing()
  {
    _isEditing = false;
  }

  public static void ImportAsset(string path)
  {
    _ = path;
  }

  public static void ImportAsset(string path, ImportAssetOptions options)
  {
    _ = options;
    ImportAsset(path);
  }

  public static void Refresh()
  {
    // no-op in memory model
  }

  public static void Refresh(ImportAssetOptions options)
  {
    _ = options;
    Refresh();
  }

  public static string[] GetDependencies(string assetPath, bool recursive)
  {
    _ = recursive;
    assetPath = Normalize(assetPath);
    return string.IsNullOrWhiteSpace(assetPath)
      ? Array.Empty<string>()
      : new[] { assetPath };
  }

  public static void ExportPackage(string[] assetPathNames, string fileName, bool interactive)
  {
    _ = assetPathNames;
    _ = fileName;
    _ = interactive;
  }

  public static string[] GetAllAssetPathsInFolder(string folderPath)
  {
    folderPath = Normalize(folderPath);
    if (string.IsNullOrWhiteSpace(folderPath))
    {
      return Array.Empty<string>();
    }

    return _assets.Keys
      .Where(path => path.StartsWith(folderPath + "/", StringComparison.OrdinalIgnoreCase))
      .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
      .ToArray();
  }

  public static string GetTextMetaFilePathFromAssetPath(string path)
  {
    path = Normalize(path);
    return string.IsNullOrWhiteSpace(path) ? string.Empty : $"{path}.meta";
  }

  public static string GetAssetPathFromTextMetaFilePath(string path)
  {
    path = Normalize(path);
    return path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
      ? path[..^5]
      : path;
  }

  public static void SetLabels(string path, string[] labels)
  {
    var asset = LoadAssetAtPath(path);
    if (asset is null)
    {
      return;
    }

    _labels[asset] = labels is null ? Array.Empty<string>() : labels;
  }

  public static string[] GetLabels(string path)
  {
    var asset = LoadAssetAtPath(path);
    if (asset is null)
    {
      return Array.Empty<string>();
    }

    return _labels.TryGetValue(asset, out var labels) ? labels : Array.Empty<string>();
  }

  public static void SetLabels(Object asset, string[] labels)
  {
    if (asset is null)
    {
      return;
    }

    _labels[asset] = labels is null ? Array.Empty<string>() : labels;
  }

  public static void SetLabels(Object[] assets, string[] labels)
  {
    if (assets is null)
    {
      return;
    }

    var final = labels is null ? Array.Empty<string>() : labels;
    foreach (var asset in assets)
    {
      if (asset is not null)
      {
        _labels[asset] = final;
      }
    }
  }

  public static string[] GetLabels(Object? asset)
  {
    if (asset is null)
    {
      return Array.Empty<string>();
    }

    return _labels.TryGetValue(asset, out var labels) ? labels : Array.Empty<string>();
  }

  public static string GetAssetBundleName(string path)
  {
    _ = path;
    return string.Empty;
  }

  public static string GetAssetBundleName(Object asset)
  {
    _ = asset;
    return string.Empty;
  }

  public static string GetImplicitAssetBundleName(string assetPath)
  {
    _ = assetPath;
    return string.Empty;
  }

  public static void SetAssetBundleNameAndVariant(string assetPath, string assetBundleName, string variantName = "")
  {
    _ = assetPath;
    _ = assetBundleName;
    _ = variantName;
  }

  public static void RemoveUnusedAssetBundleNames()
  {
    // no-op shell
  }

  public static object? LoadAssetByGUID(string guid)
  {
    if (string.IsNullOrWhiteSpace(guid))
    {
      return null;
    }

    var path = GUIDToAssetPath(guid);
    return string.IsNullOrEmpty(path) ? null : LoadAssetAtPath(path);
  }

  public static object? GetMainObjectAtGUID(string guid)
  {
    return LoadAssetByGUID(guid);
  }

  public static object[] GetSubObjectsAtGUID(string guid)
  {
    if (string.IsNullOrWhiteSpace(guid))
    {
      return Array.Empty<object>();
    }

    var path = GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path))
    {
      return Array.Empty<object>();
    }

    var main = LoadAssetAtPath(path);
    if (main is null)
    {
      return Array.Empty<object>();
    }

    var subObjects = new List<object>();
    var prefix = path + "/";
    foreach (var kv in _assets)
    {
      if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && kv.Value is not null)
      {
        subObjects.Add(kv.Value);
      }
    }

    return subObjects.ToArray();
  }

  public static string GetAssetPath(UnityEngine.Object asset)
  {
    if (asset is null)
    {
      return string.Empty;
    }

    return GetAssetPath((object)asset);
  }

  public static bool IsOpenForEdit()
  {
    return _isEditing;
  }

  public static bool IsOpenForEdit(string assetPath)
  {
    _ = assetPath;
    return _isEditing;
  }

  public static string[] GetAvailableExtensions()
  {
    return _assets.Keys
      .Select(Path.GetExtension)
      .Where(ext => !string.IsNullOrEmpty(ext))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase)
      .ToArray();
  }

  private static void IndexChild(string? parentPath, string childPath)
  {
    var parent = Normalize(parentPath ?? string.Empty);
    if (string.IsNullOrWhiteSpace(parent))
    {
      return;
    }

    if (!_children.TryGetValue(parent, out var list))
    {
      list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      _children[parent] = list;
    }

    list.Add(childPath);
  }

  private static void RemoveFromParentIndex(string childPath)
  {
    var parent = Normalize(Path.GetDirectoryName(childPath) ?? string.Empty);
    if (_children.TryGetValue(parent, out var list))
    {
      _children[parent] = [.. list.Where(c => !string.Equals(c, childPath, StringComparison.OrdinalIgnoreCase))];
    }
  }

  private static readonly Dictionary<string, string> _assetGuid = new(StringComparer.OrdinalIgnoreCase);

  private static string GuidFromPath(string path)
  {
    if (_assetGuid.TryGetValue(path, out var guid))
    {
      return guid;
    }
    var newGuid = ($"anity-{++_guidCounter:00000}");
    _assetGuid[path] = newGuid;
    return newGuid;
  }

  private static string Normalize(string path)
  {
    if (string.IsNullOrWhiteSpace(path)) return string.Empty;
    return path.Replace("\\", "/").Trim().Trim('/');
  }
}

public enum AssetDeleteResult
{
  DidNotDelete = 0,
  Deleted = 1,
  Fail = 2
}

public enum ImportAssetOptions
{
  Default = 0,
  ForceSynchronousImport = 1,
  ForceUpdate = 2,
  ImportRecursive = 4,
  DontDownloadFromCacheServer = 8
}

public enum ForceReserializeAssetsOptions
{
  ReserializeAssets = 1,
  ReserializeMetadata = 2,
  ReserializeAssetsAndMetadata = 3
}
