using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnityEditor.AssetImporters;
using UnityEngine;
using Bindings = UnityEngine.Bindings;

namespace UnityEditor;

[Flags]
public enum ExportPackageOptions
{
  Default = 0,
  Interactive = 1,
  Recurse = 2,
  IncludeDependencies = 4,
  IncludeLibraryAssets = 8,
}

[Bindings.NativeHeader("Editor/Src/Application/ApplicationFunctions.h")]
[Bindings.NativeHeader("Editor/Src/PackageUtility.h")]
[Bindings.NativeHeader("Editor/Src/VersionControl/VC_bindings.h")]
[Bindings.NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabase.h")]
[Bindings.NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabasePreventExecution.h")]
[Bindings.NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabaseUtility.h")]
[Bindings.NativeHeader("Modules/AssetDatabase/Editor/ScriptBindings/AssetDatabase.bindings.h")]
[Bindings.NativeHeader("Runtime/Core/PreventExecutionInState.h")]
[Bindings.StaticAccessor("AssetDatabaseBindings", Bindings.StaticAccessorType.DoubleColon)]
public sealed class AssetDatabase
{
  private static readonly Dictionary<string, object?> _assets = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, List<Object>> _subAssets = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, (string Name, string Variant)> _assetBundleAssignments = new(StringComparer.OrdinalIgnoreCase);
  private static readonly HashSet<string> _knownAssetBundleNames = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, AssetImporter> _importers = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, byte[]> _importedPackageMetadata = new(StringComparer.OrdinalIgnoreCase);
  private static string _projectRoot = Directory.GetCurrentDirectory();
  private static readonly Dictionary<string, HashSet<string>> _children = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<object, string[]> _labels = new();
  private static int _assetEditingDepth;
  private static readonly HashSet<string> _queuedImports = new(StringComparer.OrdinalIgnoreCase);
  private const string ImporterSettingsPrefix = "# ANITY_IMPORTER_SETTINGS: ";
  private static readonly JsonSerializerOptions ImporterSettingsJsonOptions = new() { IncludeFields = true };

  public delegate void ImportPackageCallback(string packageName);

  public delegate void ImportPackageFailedCallback(string packageName, string errorMessage);

  public enum RefreshImportMode
  {
    InProcess = 0,
    OutOfProcessPerQueue = 1,
  }

  public static Action<string[]>? onImportPackageItemsCompleted;

  public static event ImportPackageCallback? importPackageStarted;

  public static event ImportPackageCallback? importPackageCompleted;

  public static event ImportPackageCallback? importPackageCancelled;

  public static event ImportPackageFailedCallback? importPackageFailed;

  public AssetDatabase()
  {
  }

  [Bindings.NativeThrows]
  [Bindings.PreventExecutionInState(1, 2, "AssetDatabase.CreateAsset() was called as part of running an import. Please make sure this function is not called from ScriptedImporters or PostProcessors, as it is a source of non-determinism and will be disallowed in a forthcoming release.")]
  [Bindings.PreventExecutionInState(8, 1, "Assets may not be created during gathering of import dependencies")]
  public static void CreateAsset([Bindings.NotNull("ArgumentNullException")] Object asset, string path)
  {
    if (asset is null)
    {
      throw new ArgumentNullException(nameof(asset));
    }

    path = Normalize(path);
    var guid = GuidFromPath(path);
    _assets[path] = asset;
    _subAssets.Remove(path);
    EnsureImporter(path, asset);
    _assetGuid[path] = guid;
    IndexChild(Path.GetDirectoryName(path), path);
  }

  [Bindings.NativeThrows]
  public static void AddObjectToAsset([Bindings.NotNull("ArgumentNullException")] Object objectToAdd, string path)
  {
    if (objectToAdd is null)
    {
      throw new ArgumentNullException(nameof(objectToAdd));
    }

    if (string.IsNullOrWhiteSpace(path))
    {
      throw new ArgumentException("Asset path must not be empty.", nameof(path));
    }

    path = Normalize(path);
    if (FindAssetAtPath(path) is null)
    {
      throw new ArgumentException("Asset path is not stored in this AssetDatabase.", nameof(path));
    }

    if (!_subAssets.TryGetValue(path, out var subAssets))
    {
      subAssets = new List<Object>();
      _subAssets[path] = subAssets;
    }

    if (!subAssets.Any(existing => ReferenceEquals(existing, objectToAdd))) subAssets.Add(objectToAdd);
  }

  public static void AddObjectToAsset(Object objectToAdd, Object assetObject)
  {
    if (assetObject is null)
    {
      throw new ArgumentNullException(nameof(assetObject));
    }

    var path = GetAssetPath(assetObject);
    if (string.IsNullOrEmpty(path))
    {
      throw new ArgumentException("Asset object is not stored in this AssetDatabase.", nameof(assetObject));
    }

    AddObjectToAsset(objectToAdd, path);
  }

  public static bool DeleteAsset(string path)
  {
    path = Normalize(path);
    var removed = _assets.Remove(path);
    _subAssets.Remove(path);
    var removedFromDisk = false;
    if (TryGetProjectAssetFilePath(path, out var diskPath))
    {
      try
      {
        if (File.Exists(diskPath))
        {
          File.Delete(diskPath);
          removedFromDisk = true;
        }
        var metaPath = diskPath + ".meta";
        if (File.Exists(metaPath))
        {
          File.Delete(metaPath);
          removedFromDisk = true;
        }
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        Debug.LogError("Could not delete asset from disk: " + exception.Message);
        return false;
      }
    }

    if (!removed && !removedFromDisk) return false;
    _assetBundleAssignments.Remove(path);
    _assetGuid.Remove(path);
    _importers.Remove(path);
    _importedPackageMetadata.Remove(path);
    RemoveFromParentIndex(path);
    return true;
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

  public static T? LoadAssetAtPath<T>(string assetPath) where T : Object
  {
    return FindAssetAtPath(assetPath) as T;
  }

  [Bindings.NativeThrows]
  [Bindings.PreventExecutionInState(32, 1, "Assets may not be loaded while domain backup is running, as this will change the underlying state.")]
  [Bindings.PreventExecutionInState(8, 1, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
  [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedBySecondArgument)]
  public static Object? LoadAssetAtPath(string assetPath, Type type)
  {
    if (type is null)
    {
      throw new ArgumentNullException(nameof(type));
    }

    var value = FindAssetAtPath(assetPath);
    return value is not null && type.IsInstanceOfType(value) ? value : null;
  }

  [Bindings.PreventExecutionInState(8, 1, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
  public static Object? LoadMainAssetAtPath(string assetPath)
  {
    return FindAssetAtPath(assetPath);
  }

  public static Type? GetMainAssetTypeAtPath(string assetPath)
  {
    var obj = FindAssetAtPath(assetPath);
    return obj?.GetType();
  }

  private static string FindAssetPath(object asset)
  {
    foreach (var kv in _assets)
    {
      if (ReferenceEquals(kv.Value, asset))
      {
        return kv.Key;
      }
    }
    foreach (var (path, subAssets) in _subAssets)
    {
      if (subAssets.Any(subAsset => ReferenceEquals(subAsset, asset))) return path;
    }
    return string.Empty;
  }

  public static bool Contains(Object? asset)
  {
    return _assets.Values.Any(v => ReferenceEquals(v, asset))
      || _subAssets.Values.Any(subAssets => subAssets.Any(subAsset => ReferenceEquals(subAsset, asset)));
  }

  public static bool Contains(string assetPath)
  {
    if (string.IsNullOrWhiteSpace(assetPath)) return false;
    return _assets.ContainsKey(Normalize(assetPath));
  }

  public static string GetAssetOrScenePath(Object assetObject)
  {
    return GetAssetPath(assetObject);
  }

  public static AssetImporter[] GetImporters(string assetPath)
  {
    var importer = GetImporterAtPath(assetPath);
    return importer is null ? Array.Empty<AssetImporter>() : new[] { importer };
  }

  public static T[] GetImporters<T>(string assetPath) where T : AssetImporter
  {
    return GetImporterAtPath(assetPath) is T importer ? new[] { importer } : Array.Empty<T>();
  }

  public static event Action<string[]>? AssetPathChanged;

  internal static void OnAssetPathChanged(string[] paths)
  {
    AssetPathChanged?.Invoke(paths);
  }

  public static void SaveAssetIfDirty(Object asset)
  {
    _ = asset;
  }

  public static bool IsForeignAsset(Object obj)
  {
    return Contains(obj);
  }

  public static bool IsNativeAsset(Object obj)
  {
    return Contains(obj);
  }

  public static bool IsMainAssetTypeAtPath(string assetPath)
  {
    return !string.IsNullOrEmpty(assetPath) && _assets.ContainsKey(Normalize(assetPath));
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
    if (string.IsNullOrWhiteSpace(assetBundleName)) return Array.Empty<string>();
    return _assetBundleAssignments
      .Where(pair => string.Equals(GetFullAssetBundleName(pair.Value.Name, pair.Value.Variant), assetBundleName, StringComparison.OrdinalIgnoreCase))
      .Select(pair => pair.Key)
      .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
      .ToArray();
  }

  public static string[] GetAllAssetBundleNames()
  {
    return _knownAssetBundleNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
  }

  public static string[] GetUnusedAssetBundleNames()
  {
    var assigned = new HashSet<string>(
      _assetBundleAssignments.Values.Select(value => GetFullAssetBundleName(value.Name, value.Variant)),
      StringComparer.OrdinalIgnoreCase);
    return _knownAssetBundleNames
      .Where(name => !assigned.Contains(name))
      .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
      .ToArray();
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

  [Bindings.PreventExecutionInState(8, 1, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
  public static Object[] LoadAllAssetsAtPath(string assetPath)
  {
    assetPath = Normalize(assetPath);
    if (string.IsNullOrWhiteSpace(assetPath))
    {
      return Array.Empty<Object>();
    }

    if (FindAssetAtPath(assetPath) is not Object mainAsset) return Array.Empty<Object>();
    return _subAssets.TryGetValue(assetPath, out var subAssets)
      ? new[] { mainAsset }.Concat(subAssets).ToArray()
      : new[] { mainAsset };
  }

  [Bindings.PreventExecutionInState(8, 1, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
  public static Object[] LoadAllAssetRepresentationsAtPath(string assetPath)
  {
    assetPath = Normalize(assetPath);
    return _subAssets.TryGetValue(assetPath, out var subAssets) ? subAssets.ToArray() : Array.Empty<Object>();
  }

  public static Hash128 GetAssetDependencyHash(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return default;
    }

    using var payload = new MemoryStream();
    AppendDependencyHashPayload(payload, Normalize(path), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    using var sha256 = SHA256.Create();
    var digest = sha256.ComputeHash(payload.ToArray());
    return new Hash128(
      ReadHashUInt32(digest, 0),
      ReadHashUInt32(digest, 4),
      ReadHashUInt32(digest, 8),
      ReadHashUInt32(digest, 12));
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
    path = Normalize(path);
    var importer = GetImporterAtPath(path);
    if (importer is null || string.IsNullOrWhiteSpace(path)) return false;

    var assetPath = Path.Combine(_projectRoot, path);
    if (!File.Exists(assetPath)) return false;

    var metaPath = assetPath + ".meta";
    var existing = File.Exists(metaPath) ? File.ReadAllText(metaPath) : "fileFormatVersion: 2\n";
    if (TryGetUnityMetaGuid(Encoding.UTF8.GetBytes(existing), out var metaGuid)) _assetGuid[path] = metaGuid;
    var preservedLines = existing.Replace("\r\n", "\n").Split('\n')
      .Where(line => !line.StartsWith(ImporterSettingsPrefix, StringComparison.Ordinal))
      .ToList();
    while (preservedLines.Count > 0 && string.IsNullOrWhiteSpace(preservedLines[^1])) preservedLines.RemoveAt(preservedLines.Count - 1);

    var guid = GuidFromPath(path);
    var foundGuid = false;
    for (var index = 0; index < preservedLines.Count; index++)
    {
      if (!preservedLines[index].StartsWith("guid:", StringComparison.Ordinal)) continue;
      preservedLines[index] = "guid: " + guid;
      foundGuid = true;
      break;
    }
    if (!foundGuid) preservedLines.Insert(Math.Min(1, preservedLines.Count), "guid: " + guid);

    WriteUnityYamlImporterSettings(preservedLines, importer);
    WriteUnityYamlAssetBundleAssignment(preservedLines, importer, GetAssetBundleName(path), GetAssetBundleVariant(path));

    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(CreatePersistedImporterSettings(importer), ImporterSettingsJsonOptions)));
    preservedLines.Add(ImporterSettingsPrefix + payload);
    var serialized = string.Join("\n", preservedLines) + "\n";
    File.WriteAllText(metaPath, serialized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    _importedPackageMetadata[path] = Encoding.UTF8.GetBytes(serialized);
    importer.importSettingsMissing = false;
    return true;
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
    return GetAssetPathsFromAssetBundle(assetBundleName)
      .Where(path => string.Equals(Path.GetExtension(path), ".unity", StringComparison.OrdinalIgnoreCase) == isSceneAssetBundle)
      .ToArray();
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

    if (!TryMoveProjectAssetFiles(oldPath, newPath)) return string.Empty;

    var guid = GuidFromPath(oldPath);
    _assets.Remove(oldPath);
    _subAssets.TryGetValue(oldPath, out var subAssets);
    _subAssets.Remove(oldPath);
    _assetGuid.Remove(oldPath);
    _importers.TryGetValue(oldPath, out var importer);
    _importers.Remove(oldPath);
    _assetBundleAssignments.TryGetValue(oldPath, out var assetBundleAssignment);
    _assetBundleAssignments.Remove(oldPath);
    _importedPackageMetadata.TryGetValue(oldPath, out var metadata);
    _importedPackageMetadata.Remove(oldPath);
    RemoveFromParentIndex(oldPath);

    _assets[newPath] = value;
    if (subAssets is not null) _subAssets[newPath] = subAssets;
    _assetGuid[newPath] = guid;
    if (importer is not null)
    {
      importer.assetPath = newPath;
      _importers[newPath] = importer;
    }
    if (!string.IsNullOrEmpty(assetBundleAssignment.Name)) _assetBundleAssignments[newPath] = assetBundleAssignment;
    if (metadata is not null) _importedPackageMetadata[newPath] = metadata;
    IndexChild(Path.GetDirectoryName(newPath) ?? string.Empty, newPath);
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
    _subAssets.TryGetValue(path, out var subAssets);
    _subAssets.Remove(path);
    _assetGuid.Remove(path);
    _assetBundleAssignments.TryGetValue(path, out var assetBundleAssignment);
    _assetBundleAssignments.Remove(path);
    _assets[target] = value;
    if (subAssets is not null) _subAssets[target] = subAssets;
    _assetGuid[target] = GuidFromPath(target);
    if (!string.IsNullOrEmpty(assetBundleAssignment.Name)) _assetBundleAssignments[target] = assetBundleAssignment;
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

    if (!TryCopyProjectAssetFiles(path, newPath)) return false;

    var duplicated = value;
    _assets[newPath] = duplicated;
    var copiedGuid = Guid.NewGuid().ToString("N");
    _assetGuid[newPath] = copiedGuid;
    RewriteCopiedMetaGuid(newPath, copiedGuid);
    if (TryGetProjectAssetFilePath(newPath, out var diskPath) && File.Exists(diskPath))
    {
      ImportAssetNow(newPath);
    }
    else if (duplicated is Object duplicatedObject)
    {
      EnsureImporter(newPath, duplicatedObject);
    }
    IndexChild(Path.GetDirectoryName(newPath) ?? string.Empty, newPath);
    return true;
  }

  public static bool MoveAsset(string[] paths, string destinationFolder)
  {
    if (paths is null || paths.Length == 0) return false;

    destinationFolder = Normalize(destinationFolder);
    var destinationIsDiskFolder = TryGetProjectAssetFilePath(destinationFolder, out var destinationDiskPath)
      && Directory.Exists(destinationDiskPath);
    if (!IsValidFolder(destinationFolder) && !destinationIsDiskFolder) return false;

    var sources = paths.Select(Normalize).ToArray();
    if (sources.Any(string.IsNullOrEmpty)
      || sources.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length) return false;

    var sourceSet = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
    var targets = new List<(string Source, string Target)>();
    foreach (var source in sources)
    {
      if (!_assets.TryGetValue(source, out var asset) || asset is null) return false;
      if (string.Equals(source, destinationFolder, StringComparison.OrdinalIgnoreCase)
        || destinationFolder.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)) return false;

      var fileName = Path.GetFileName(source);
      if (string.IsNullOrWhiteSpace(fileName)) return false;
      var target = Normalize(destinationFolder + "/" + fileName);
      if (sourceSet.Contains(target) || _assets.ContainsKey(target)) return false;
      if (TryGetProjectAssetFilePath(target, out var targetDiskPath) && File.Exists(targetDiskPath)) return false;
      targets.Add((source, target));
    }

    if (targets.Select(pair => pair.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Count) return false;

    var moved = new List<(string Source, string Target)>();
    foreach (var (source, target) in targets)
    {
      if (string.IsNullOrEmpty(MoveAsset(source, target)))
      {
        foreach (var completed in moved.AsEnumerable().Reverse()) MoveAsset(completed.Target, completed.Source);
        return false;
      }
      moved.Add((source, target));
    }

    return true;
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
    _ = interactive;
    var name = string.IsNullOrWhiteSpace(packagePath)
      ? string.Empty
      : Path.GetFileNameWithoutExtension(packagePath);

    importPackageStarted?.Invoke(name);

    if (string.IsNullOrWhiteSpace(packagePath))
    {
      importPackageFailed?.Invoke(name, "Package path must not be empty.");
      return;
    }

    var fullPath = Path.GetFullPath(packagePath);
    if (!File.Exists(fullPath))
    {
      importPackageFailed?.Invoke(name, $"Package file does not exist: {fullPath}");
      return;
    }

    if (TryReadUnityPackage(fullPath, out var packageAssets, out var error))
    {
      var postprocessors = CreateAssetPostprocessors();
      var newlyCreatedImporters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try
      {
        foreach (var imported in packageAssets)
        {
          if (!_importers.ContainsKey(imported.Path)) newlyCreatedImporters.Add(imported.Path);
          PrepareAssetPostprocessors(postprocessors, imported.Path, null);
          foreach (var postprocessor in postprocessors) InvokeAssetPostprocessor(postprocessor, "OnPreprocessAsset");
        }
      }
      catch (Exception exception)
      {
        foreach (var importedPath in newlyCreatedImporters) _importers.Remove(importedPath);
        importPackageFailed?.Invoke(name, "Asset preprocessing failed: " + exception.Message);
        return;
      }

      if (!TryPersistUnityPackageAssets(packageAssets, out error))
      {
        importPackageFailed?.Invoke(name, error!);
        return;
      }

      // Parsing and validation complete before this point, so package failures
      // cannot leave a partially committed asset database transaction.
      foreach (var imported in packageAssets)
      {
        var importedObject = CreateImportedAsset(imported);
        _assets[imported.Path] = importedObject;
        _subAssets.Remove(imported.Path);
        var importer = EnsureImporter(imported.Path, importedObject);
        ApplyPersistedImporterSettings(importer, imported.MetaBytes);
        EnsureImportedModelAvatarSubAsset(imported.Path, importer);
        _assetGuid[imported.Path] = imported.Guid;
        _importedPackageMetadata[imported.Path] = imported.MetaBytes;
        IndexChild(Path.GetDirectoryName(imported.Path), imported.Path);
      }

      onImportPackageItemsCompleted?.Invoke(packageAssets.Select(asset => asset.Path).ToArray());

      InvokePostprocessAllAssets(
        postprocessors,
        packageAssets.Select(asset => asset.Path).ToArray(),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), false,
        "Asset postprocessor failed after package import: ");
    }
    else if (error is not null)
    {
      importPackageFailed?.Invoke(name, error);
      return;
    }
    else
    {
      // Preserve lifecycle compatibility for non-archive package providers.
      // Native package-manager integrations may surface a materialized file
      // without exposing the Unity tar stream to this managed facade.
      onImportPackageItemsCompleted?.Invoke(new[] { fullPath.Replace('\\', '/') });
    }

    importPackageCompleted?.Invoke(name);
  }

  public static bool MoveAssetToTrash(string path)
  {
    return DeleteAsset(path);
  }

  public static void StartAssetEditing()
  {
    _assetEditingDepth++;
  }

  public static void StopAssetEditing()
  {
    if (_assetEditingDepth == 0) return;
    _assetEditingDepth--;
    if (_assetEditingDepth == 0) FlushQueuedImports();
  }

  public static void ImportAsset(string path)
  {
    path = Normalize(path);
    if (string.IsNullOrEmpty(path)) return;
    if (_assetEditingDepth > 0)
    {
      _queuedImports.Add(path);
      return;
    }
    ImportAssetNow(path);
  }

  private static void ImportAssetNow(string path)
  {
    var diskPath = Path.Combine(_projectRoot, path);
    if (!File.Exists(diskPath)) return;

    var bytes = File.ReadAllBytes(diskPath);
    var metaPath = diskPath + ".meta";
    var metaBytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : Array.Empty<byte>();
    if (TryGetUnityMetaGuid(metaBytes, out var metaGuid)) _assetGuid[path] = metaGuid;
    var importer = EnsureImporterForPath(path);
    ApplyPersistedImporterSettings(importer, metaBytes);

    var processors = CreateAssetPostprocessors();
    try
    {
      PrepareAssetPostprocessors(processors, path, null);
      foreach (var processor in processors) InvokeAssetPostprocessor(processor, "OnPreprocessAsset");
      if (importer is ModelImporter)
        foreach (var processor in processors) InvokeAssetPostprocessor(processor, "OnPreprocessModel");
    }
    catch (Exception exception)
    {
      Debug.LogError("Asset import preprocessing failed: " + exception.Message);
      return;
    }

    UnityEngine.Object asset;
    ImportedModelAsset? importedModel = null;
    var modelError = string.Empty;
    if (importer is ModelImporter modelImporter && ModelAssetImportPipeline.TryImport(diskPath, path, modelImporter, out var decodedModel, out modelError))
    {
      importedModel = decodedModel;
      asset = decodedModel.MainObject;
      try
      {
        PrepareAssetPostprocessors(processors, path, asset);
        var gameObjectArguments = new object[] { decodedModel.MainObject };
        var gameObjectTypes = new[] { typeof(GameObject) };
        foreach (var processor in processors)
          InvokeAssetPostprocessor(processor, "OnPostprocessMeshHierarchy", gameObjectTypes, gameObjectArguments);
        foreach (var processor in processors) InvokeAssetPostprocessor(processor, "OnPreprocessAnimation");
        ModelAssetImportPipeline.ImportAnimations(decodedModel, modelImporter);
        foreach (var clip in decodedModel.AnimationClips)
        {
          var animationArguments = new object[] { decodedModel.MainObject, clip };
          var animationTypes = new[] { typeof(GameObject), typeof(AnimationClip) };
          foreach (var processor in processors)
            InvokeAssetPostprocessor(processor, "OnPostprocessAnimation", animationTypes, animationArguments);
        }
        foreach (var processor in processors)
          InvokeAssetPostprocessor(processor, "OnPostprocessModel", gameObjectTypes, gameObjectArguments);
      }
      catch (Exception exception)
      {
        Debug.LogError("Model import postprocessing failed: " + exception.Message);
        return;
      }
    }
    else
    {
      if (importer is ModelImporter) Debug.LogError("Model import failed: " + modelError);
      if (importer is ModelImporter && _assets.TryGetValue(path, out var previousModel) && previousModel is GameObject)
      {
        // Unity keeps the last successfully imported artifact when a source
        // update cannot be decoded. Do not replace a usable model with bytes.
        return;
      }
      // Invalid model sources retain the legacy binary TextAsset only as a
      // diagnostic carrier. Successfully decoded model files never enter this path.
      asset = CreateImportedAsset(new UnityPackageAsset(GuidFromPath(path), path, bytes, metaBytes));
    }

    _assets[path] = asset;
    if (importedModel is not null) _subAssets[path] = importedModel.SubAssets;
    else _subAssets.Remove(path);
    EnsureImportedModelAvatarSubAsset(path, importer);
    importer.importSettingsMissing = false;
    EnsureImporter(path, asset);

    InvokePostprocessAllAssets(
      processors, new[] { path }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), false,
      "Asset postprocessor failed after reimport: ");
  }

  public static void ImportAsset(string path, ImportAssetOptions options)
  {
    _ = options;
    ImportAsset(path);
  }

  public static void Refresh()
  {
    var diskAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var assetRoot = Path.Combine(_projectRoot, "Assets");
    if (Directory.Exists(assetRoot))
    {
      try
      {
        foreach (var diskPath in Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories))
        {
          if (diskPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(diskPath) == ".DS_Store") continue;
          var relativePath = Normalize(Path.GetRelativePath(_projectRoot, diskPath));
          if (!TryGetProjectAssetFilePath(relativePath, out _)) continue;
          diskAssetPaths.Add(relativePath);
          ImportAsset(relativePath);
        }
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        Debug.LogError("Could not scan project assets during refresh: " + exception.Message);
      }
    }

    foreach (var path in _assets.Keys.ToArray())
    {
      if (_assets[path] is null || !TryGetProjectAssetFilePath(path, out _)) continue;
      if (diskAssetPaths.Contains(path)) continue;
      _assets.Remove(path);
      _subAssets.Remove(path);
      _assetGuid.Remove(path);
      _importers.Remove(path);
      _importedPackageMetadata.Remove(path);
      RemoveFromParentIndex(path);
    }
    if (_assetEditingDepth == 0) FlushQueuedImports();
  }

  public static void Refresh(ImportAssetOptions options)
  {
    _ = options;
    Refresh();
  }

  private static void FlushQueuedImports()
  {
    var paths = _queuedImports.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    _queuedImports.Clear();
    foreach (var path in paths) ImportAssetNow(path);
  }

  public static string[] GetDependencies(string assetPath, bool recursive)
  {
    assetPath = Normalize(assetPath);
    if (string.IsNullOrWhiteSpace(assetPath)) return Array.Empty<string>();

    var result = new List<string> { assetPath };
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { assetPath };
    var pending = new Queue<string>();
    pending.Enqueue(assetPath);
    while (pending.Count > 0)
    {
      var currentPath = pending.Dequeue();
      foreach (var dependencyPath in GetDirectDependencyPaths(currentPath))
      {
        if (!visited.Add(dependencyPath)) continue;
        result.Add(dependencyPath);
        if (recursive) pending.Enqueue(dependencyPath);
      }
      if (!recursive) break;
    }
    return result.ToArray();
  }

  public static void ExportPackage(string assetPathName, string fileName)
  {
    ExportPackage(assetPathName, fileName, ExportPackageOptions.Default);
  }

  public static void ExportPackage(string assetPathName, string fileName, ExportPackageOptions flags)
  {
    if (assetPathName is null) throw new ArgumentNullException(nameof(assetPathName));
    ExportPackage(new[] { assetPathName }, fileName, flags);
  }

  public static void ExportPackage(string[] assetPathNames, string fileName)
  {
    ExportPackage(assetPathNames, fileName, ExportPackageOptions.Default);
  }

  public static void ExportPackage(string[] assetPathNames, string fileName, bool interactive)
  {
    ExportPackage(assetPathNames, fileName, interactive ? ExportPackageOptions.Interactive : ExportPackageOptions.Default);
  }

  public static void ExportPackage(string[] assetPathNames, string fileName, ExportPackageOptions flags)
  {
    if (assetPathNames is null || assetPathNames.Length == 0)
    {
      throw new ArgumentException("At least one asset path is required.", nameof(assetPathNames));
    }
    if (string.IsNullOrWhiteSpace(fileName))
    {
      throw new ArgumentException("Export package file name must not be empty.", nameof(fileName));
    }

    const ExportPackageOptions knownOptions = ExportPackageOptions.Interactive
      | ExportPackageOptions.Recurse
      | ExportPackageOptions.IncludeDependencies
      | ExportPackageOptions.IncludeLibraryAssets;
    if ((flags & ~knownOptions) != 0)
    {
      throw new ArgumentOutOfRangeException(nameof(flags));
    }
    if ((flags & ExportPackageOptions.IncludeLibraryAssets) != 0)
    {
      throw new NotSupportedException("ExportPackageOptions.IncludeLibraryAssets is not implemented yet.");
    }

    ExportPackageInternal(ExpandExportPackagePaths(assetPathNames, flags), fileName);
  }

  private static string[] ExpandExportPackagePaths(IEnumerable<string> assetPathNames, ExportPackageOptions flags)
  {
    var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rawPath in assetPathNames)
    {
      var path = Normalize(rawPath);
      if (string.IsNullOrEmpty(path)) throw new ArgumentException("Export package paths must not be empty.", nameof(assetPathNames));

      if (TryGetProjectAssetFilePath(path, out var diskPath) && Directory.Exists(diskPath))
      {
        if ((flags & ExportPackageOptions.Recurse) == 0)
        {
          throw new ArgumentException("Exporting a folder requires ExportPackageOptions.Recurse.", nameof(assetPathNames));
        }
        foreach (var childDiskPath in Directory.EnumerateFiles(diskPath, "*", SearchOption.AllDirectories))
        {
          if (childDiskPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(childDiskPath), ".DS_Store", StringComparison.Ordinal)) continue;
          var childPath = Normalize(Path.GetRelativePath(_projectRoot, childDiskPath));
          if (TryGetProjectAssetFilePath(childPath, out _)) paths.Add(childPath);
        }
      }
      else
      {
        paths.Add(path);
      }
    }

    if ((flags & ExportPackageOptions.IncludeDependencies) != 0)
    {
      foreach (var dependency in paths.SelectMany(path => GetDependencies(path, recursive: true)).ToArray()) paths.Add(dependency);
    }

    return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
  }

  private static void ExportPackageInternal(string[] assetPathNames, string fileName)
  {
    var packageAssets = new List<UnityPackageAsset>();
    foreach (var path in assetPathNames.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase))
    {
      if (string.IsNullOrEmpty(path) || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
      {
        throw new ArgumentException("Export package paths must identify asset files.", nameof(assetPathNames));
      }
      if (!TryGetProjectAssetFilePath(path, out var diskPath) || !File.Exists(diskPath))
      {
        throw new FileNotFoundException("Asset is not persisted on disk and cannot be exported.", path);
      }

      var assetBytes = File.ReadAllBytes(diskPath);
      var metaPath = diskPath + ".meta";
      var metaBytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : Array.Empty<byte>();
      var guid = TryGetUnityMetaGuid(metaBytes, out var metaGuid) ? metaGuid : GuidFromPath(path);
      if (metaBytes.Length == 0)
      {
        metaBytes = Encoding.UTF8.GetBytes("fileFormatVersion: 2\nguid: " + guid + "\n");
      }
      packageAssets.Add(new UnityPackageAsset(guid, path, assetBytes, metaBytes));
    }

    var outputPath = Path.GetFullPath(fileName);
    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (string.IsNullOrWhiteSpace(outputDirectory))
    {
      throw new ArgumentException("Export package path must include a directory.", nameof(fileName));
    }
    Directory.CreateDirectory(outputDirectory);
    var stagingPath = outputPath + ".anity-export-" + Guid.NewGuid().ToString("N");
    try
    {
      using (var output = File.Create(stagingPath))
      using (var gzip = new GZipStream(output, CompressionMode.Compress))
      {
        foreach (var asset in packageAssets.OrderBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase))
        {
          WriteTarEntry(gzip, asset.Guid + "/pathname", Encoding.UTF8.GetBytes(asset.Path));
          WriteTarEntry(gzip, asset.Guid + "/asset", asset.AssetBytes);
          WriteTarEntry(gzip, asset.Guid + "/asset.meta", asset.MetaBytes);
        }
        gzip.Write(new byte[1024], 0, 1024);
      }

      if (File.Exists(outputPath)) File.Delete(outputPath);
      File.Move(stagingPath, outputPath);
    }
    catch
    {
      try { if (File.Exists(stagingPath)) File.Delete(stagingPath); } catch { }
      throw;
    }
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
    var asset = FindAssetAtPath(path);
    if (asset is null)
    {
      return;
    }

    _labels[asset] = labels is null ? Array.Empty<string>() : labels;
  }

  public static string[] GetLabels(string path)
  {
    var asset = FindAssetAtPath(path);
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
    path = Normalize(path);
    return _assetBundleAssignments.TryGetValue(path, out var assignment) ? assignment.Name : string.Empty;
  }

  public static string GetAssetBundleName(Object asset)
  {
    return asset is null ? string.Empty : GetAssetBundleName(GetAssetPath(asset));
  }

  internal static string GetAssetBundleVariant(string path)
  {
    path = Normalize(path);
    return _assetBundleAssignments.TryGetValue(path, out var assignment) ? assignment.Variant : string.Empty;
  }

  public static string GetImplicitAssetBundleName(string assetPath)
  {
    return TryGetImplicitAssetBundleAssignment(assetPath, out var assignment) ? assignment.Name : string.Empty;
  }

  public static string GetImplicitAssetBundleVariantName(string assetPath)
  {
    return TryGetImplicitAssetBundleAssignment(assetPath, out var assignment) ? assignment.Variant : string.Empty;
  }

  public static void SetAssetBundleNameAndVariant(string assetPath, string assetBundleName, string variantName = "")
  {
    assetPath = Normalize(assetPath);
    assetBundleName = assetBundleName?.Trim() ?? string.Empty;
    variantName = variantName?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path must not be empty.", nameof(assetPath));

    if (string.IsNullOrEmpty(assetBundleName))
    {
      _assetBundleAssignments.Remove(assetPath);
      return;
    }

    var fullName = GetFullAssetBundleName(assetBundleName, variantName);
    _assetBundleAssignments[assetPath] = (assetBundleName, variantName);
    _knownAssetBundleNames.Add(fullName);
  }

  public static void RemoveUnusedAssetBundleNames()
  {
    var assigned = new HashSet<string>(
      _assetBundleAssignments.Values.Select(value => GetFullAssetBundleName(value.Name, value.Variant)),
      StringComparer.OrdinalIgnoreCase);
    _knownAssetBundleNames.RemoveWhere(name => !assigned.Contains(name));
  }

  internal static IReadOnlyList<(string Name, string Variant, string[] AssetPaths)> GetExplicitAssetBundleBuilds()
  {
    var groups = new Dictionary<string, (string Name, string Variant, List<string> AssetPaths)>(StringComparer.OrdinalIgnoreCase);
    foreach (var (assetPath, asset) in _assets)
    {
      if (asset is not Object || !TryGetImplicitAssetBundleAssignment(assetPath, out var assignment)) continue;
      var fullName = GetFullAssetBundleName(assignment.Name, assignment.Variant);
      if (!groups.TryGetValue(fullName, out var group))
      {
        group = (assignment.Name, assignment.Variant, new List<string>());
        groups.Add(fullName, group);
      }
      group.AssetPaths.Add(assetPath);
    }

    return groups.Values
      .OrderBy(group => GetFullAssetBundleName(group.Name, group.Variant), StringComparer.OrdinalIgnoreCase)
      .Select(group => (group.Name, group.Variant, group.AssetPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()))
      .ToArray();
  }

  private static bool TryGetImplicitAssetBundleAssignment(string assetPath, out (string Name, string Variant) assignment)
  {
    for (var currentPath = Normalize(assetPath); !string.IsNullOrEmpty(currentPath);)
    {
      if (_assetBundleAssignments.TryGetValue(currentPath, out assignment)) return true;
      var separator = currentPath.LastIndexOf('/');
      currentPath = separator < 0 ? string.Empty : currentPath[..separator];
    }

    assignment = default;
    return false;
  }

  public static object? LoadAssetByGUID(string guid)
  {
    if (string.IsNullOrWhiteSpace(guid))
    {
      return null;
    }

    var path = GUIDToAssetPath(guid);
    return string.IsNullOrEmpty(path) ? null : FindAssetAtPath(path);
  }

  internal static Avatar? ResolveAvatarByGuid(string guid)
  {
    var path = GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path)) return null;
    if (FindAssetAtPath(path) is Avatar mainAvatar) return mainAvatar;
    return _subAssets.TryGetValue(path, out var subAssets) ? subAssets.OfType<Avatar>().FirstOrDefault() : null;
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

    var main = FindAssetAtPath(path);
    if (main is null)
    {
      return Array.Empty<object>();
    }

    return _subAssets.TryGetValue(path, out var subAssets) ? subAssets.Cast<object>().ToArray() : Array.Empty<object>();
  }

  public static string GetAssetPath(UnityEngine.Object assetObject)
  {
    if (assetObject is null)
    {
      return string.Empty;
    }

    return FindAssetPath(assetObject);
  }

  public static bool IsOpenForEdit()
  {
    return _assetEditingDepth > 0;
  }

  public static bool IsOpenForEdit(string assetPath)
  {
    _ = assetPath;
    return _assetEditingDepth > 0;
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
    var newGuid = Guid.NewGuid().ToString("N");
    _assetGuid[path] = newGuid;
    return newGuid;
  }

  private static bool TryGetUnityMetaGuid(byte[] metaBytes, out string guid)
  {
    guid = string.Empty;
    if (metaBytes.Length == 0) return false;
    try
    {
      foreach (var rawLine in Encoding.UTF8.GetString(metaBytes).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
      {
        var line = rawLine.TrimStart();
        if (!line.StartsWith("guid:", StringComparison.Ordinal)) continue;
        var candidate = line["guid:".Length..].Trim();
        if (candidate.Length != 32 || candidate.Any(character => !Uri.IsHexDigit(character))) return false;
        guid = candidate.ToLowerInvariant();
        return true;
      }
    }
    catch (DecoderFallbackException)
    {
      // Binary or malformed metadata is not a Unity text meta file.
    }
    return false;
  }

  private static void AppendDependencyHashPayload(Stream payload, string assetPath, HashSet<string> recursionStack)
  {
    WriteDependencyHashPart(payload, assetPath);
    if (!recursionStack.Add(assetPath))
    {
      WriteDependencyHashPart(payload, "cycle");
      return;
    }

    try
    {
      if (!TryGetProjectAssetFilePath(assetPath, out var diskPath) || !File.Exists(diskPath))
      {
        WriteDependencyHashPart(payload, "missing");
        return;
      }

      WriteDependencyHashPart(payload, "asset");
      WriteDependencyHashPart(payload, File.ReadAllBytes(diskPath));
      var metaPath = diskPath + ".meta";
      WriteDependencyHashPart(payload, "meta");
      WriteDependencyHashPart(payload, File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : Array.Empty<byte>());

      foreach (var dependencyPath in GetDirectDependencyPaths(assetPath))
      {
        WriteDependencyHashPart(payload, "dependency");
        AppendDependencyHashPayload(payload, dependencyPath, recursionStack);
      }
    }
    finally
    {
      recursionStack.Remove(assetPath);
    }
  }

  private static void WriteDependencyHashPart(Stream payload, string value)
  {
    WriteDependencyHashPart(payload, Encoding.UTF8.GetBytes(value));
  }

  private static void WriteDependencyHashPart(Stream payload, byte[] value)
  {
    var length = value.Length;
    payload.WriteByte((byte)length);
    payload.WriteByte((byte)(length >> 8));
    payload.WriteByte((byte)(length >> 16));
    payload.WriteByte((byte)(length >> 24));
    payload.Write(value, 0, value.Length);
  }

  private static uint ReadHashUInt32(byte[] digest, int offset)
  {
    return ((uint)digest[offset] << 24)
      | ((uint)digest[offset + 1] << 16)
      | ((uint)digest[offset + 2] << 8)
      | digest[offset + 3];
  }

  private static string[] GetDirectDependencyPaths(string assetPath)
  {
    if (!TryGetProjectAssetFilePath(assetPath, out var diskPath) || !File.Exists(diskPath)) return Array.Empty<string>();
    try
    {
      var content = Encoding.UTF8.GetString(File.ReadAllBytes(diskPath));
      var metaPath = diskPath + ".meta";
      if (File.Exists(metaPath)) content += "\n" + Encoding.UTF8.GetString(File.ReadAllBytes(metaPath));
      var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var cursor = 0;
      while (cursor < content.Length)
      {
        var marker = content.IndexOf("guid:", cursor, StringComparison.Ordinal);
        if (marker < 0) break;
        var start = marker + "guid:".Length;
        while (start < content.Length && char.IsWhiteSpace(content[start])) start++;
        var end = start;
        while (end < content.Length && Uri.IsHexDigit(content[end])) end++;
        if (end - start == 32)
        {
          var guid = content[start..end].ToLowerInvariant();
          var dependencyPath = GUIDToAssetPath(guid);
          if (!string.IsNullOrEmpty(dependencyPath) && !string.Equals(dependencyPath, assetPath, StringComparison.OrdinalIgnoreCase)) dependencies.Add(dependencyPath);
        }
        cursor = Math.Max(end, marker + "guid:".Length);
      }
      return dependencies.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
    {
      Debug.LogWarning("Could not read asset dependencies: " + exception.Message);
      return Array.Empty<string>();
    }
  }

  private static string Normalize(string path)
  {
    if (string.IsNullOrWhiteSpace(path)) return string.Empty;
    return path.Replace("\\", "/").Trim().Trim('/');
  }

  private static string GetFullAssetBundleName(string name, string variant)
  {
    return string.IsNullOrEmpty(variant) ? name : name + "." + variant;
  }

  private static Object? FindAssetAtPath(string path)
  {
    path = Normalize(path);
    return _assets.TryGetValue(path, out var value) ? value as Object : null;
  }

  internal static void SetProjectRoot(string projectPath)
  {
    if (string.IsNullOrWhiteSpace(projectPath))
    {
      throw new ArgumentException("Project root must not be empty.", nameof(projectPath));
    }
    var newProjectRoot = Path.GetFullPath(projectPath);
    if (string.Equals(_projectRoot, newProjectRoot, StringComparison.OrdinalIgnoreCase)) return;
    _projectRoot = newProjectRoot;
    _assets.Clear();
    _subAssets.Clear();
    _assetBundleAssignments.Clear();
    _knownAssetBundleNames.Clear();
    _importers.Clear();
    _importedPackageMetadata.Clear();
    _assetGuid.Clear();
    _children.Clear();
    _labels.Clear();
    _queuedImports.Clear();
    _assetEditingDepth = 0;
  }

  internal static AssetImporter? GetImporterAtPath(string assetPath)
  {
    assetPath = Normalize(assetPath);
    if (string.IsNullOrEmpty(assetPath)) return null;
    if (_importers.TryGetValue(assetPath, out var importer)) return importer;
    var asset = FindAssetAtPath(assetPath);
    return asset is null ? null : EnsureImporter(assetPath, asset);
  }

  internal static void ReimportImporter(UnityEngine.AssetImporter importer)
  {
    if (importer is null || string.IsNullOrWhiteSpace(importer.assetPath)) return;
    ImportAsset(importer.assetPath);
  }

  private static AssetImporter EnsureImporter(string assetPath, Object asset)
  {
    assetPath = Normalize(assetPath);
    if (_importers.TryGetValue(assetPath, out var existing)) return existing;
    AssetImporter importer = asset switch
    {
      Texture2D => new TextureImporter(),
      AudioClip => new AudioImporter(),
      _ => CreateImporterForPath(assetPath),
    };
    importer.assetPath = assetPath;
    _importers[assetPath] = importer;
    return importer;
  }

  private static AssetImporter EnsureImporterForPath(string assetPath)
  {
    assetPath = Normalize(assetPath);
    if (_importers.TryGetValue(assetPath, out var existing)) return existing;
    var importer = CreateImporterForPath(assetPath);
    importer.assetPath = assetPath;
    _importers[assetPath] = importer;
    return importer;
  }

  private static AssetImporter CreateImporterForPath(string assetPath)
  {
    return Path.GetExtension(assetPath).ToLowerInvariant() switch
    {
      ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" or ".gif" or ".psd" or ".exr" or ".hdr" => new TextureImporter(),
      ".wav" or ".mp3" or ".ogg" or ".aac" or ".m4a" or ".flac" => new AudioImporter(),
      ".fbx" or ".obj" or ".dae" or ".blend" or ".3ds" or ".dxf" => new ModelImporter(),
      ".shader" => new ShaderImporter(),
      _ => new AssetImporter(),
    };
  }

  private static void EnsureImportedModelAvatarSubAsset(string assetPath, AssetImporter importer)
  {
    if (importer is not ModelImporter model || model.animationType == ModelImporterAnimationType.None || model.avatarSetup == ModelImporterAvatarSetup.NoAvatar) return;
    if (!_subAssets.TryGetValue(assetPath, out var subAssets))
    {
      subAssets = new List<Object>();
      _subAssets[assetPath] = subAssets;
    }
    if (subAssets.OfType<Avatar>().Any()) return;
    var source = model.sourceAvatar;
    var description = source is not null ? source.humanDescription : model.humanDescription;
    Avatar avatar = Avatar.Create(
      source is not null && source.isValid,
      model.animationType == ModelImporterAnimationType.Human,
      description);
    avatar.name = Path.GetFileNameWithoutExtension(assetPath) + "Avatar";
    subAssets.Add(avatar);
  }

  private static bool TryGetProjectAssetFilePath(string assetPath, out string fullPath)
  {
    fullPath = string.Empty;
    assetPath = Normalize(assetPath);
    if (assetPath != "Assets" && !assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return false;
    var root = Path.GetFullPath(_projectRoot);
    var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? root : root + Path.DirectorySeparatorChar;
    var candidate = Path.GetFullPath(Path.Combine(root, assetPath));
    if (!candidate.StartsWith(rootPrefix, StringComparison.Ordinal)) return false;
    fullPath = candidate;
    return true;
  }

  private static bool TryMoveProjectAssetFiles(string oldPath, string newPath)
  {
    if (!TryGetProjectAssetFilePath(oldPath, out var oldDiskPath) || !TryGetProjectAssetFilePath(newPath, out var newDiskPath)) return false;
    if (!File.Exists(oldDiskPath)) return !File.Exists(newDiskPath);
    if (File.Exists(newDiskPath)) return false;
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(newDiskPath)!);
      File.Move(oldDiskPath, newDiskPath);
      var oldMetaPath = oldDiskPath + ".meta";
      if (File.Exists(oldMetaPath)) File.Move(oldMetaPath, newDiskPath + ".meta");
      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      Debug.LogError("Could not move asset on disk: " + exception.Message);
      return false;
    }
  }

  private static bool TryCopyProjectAssetFiles(string sourcePath, string destinationPath)
  {
    if (!TryGetProjectAssetFilePath(sourcePath, out var sourceDiskPath) || !TryGetProjectAssetFilePath(destinationPath, out var destinationDiskPath)) return false;
    if (!File.Exists(sourceDiskPath)) return !File.Exists(destinationDiskPath);
    if (File.Exists(destinationDiskPath)) return false;
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(destinationDiskPath)!);
      File.Copy(sourceDiskPath, destinationDiskPath, overwrite: false);
      var sourceMetaPath = sourceDiskPath + ".meta";
      if (File.Exists(sourceMetaPath)) File.Copy(sourceMetaPath, destinationDiskPath + ".meta", overwrite: false);
      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      Debug.LogError("Could not copy asset on disk: " + exception.Message);
      return false;
    }
  }

  private static void RewriteCopiedMetaGuid(string assetPath, string guid)
  {
    if (!TryGetProjectAssetFilePath(assetPath, out var diskPath)) return;
    var metaPath = diskPath + ".meta";
    if (!File.Exists(metaPath)) return;
    try
    {
      var lines = File.ReadAllLines(metaPath).ToList();
      var foundGuid = false;
      for (var index = 0; index < lines.Count; index++)
      {
        if (!lines[index].StartsWith("guid:", StringComparison.Ordinal)) continue;
        lines[index] = "guid: " + guid;
        foundGuid = true;
        break;
      }
      if (!foundGuid) lines.Insert(Math.Min(1, lines.Count), "guid: " + guid);
      File.WriteAllText(metaPath, string.Join("\n", lines) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      Debug.LogWarning("Could not rewrite copied asset metadata GUID: " + exception.Message);
    }
  }

  private static PersistedImporterSettings CreatePersistedImporterSettings(AssetImporter importer)
  {
    var settings = new PersistedImporterSettings
    {
      Kind = importer.GetType().Name,
      EditorUserSettingsData = importer.editorUserSettingsData,
      AssetBundleName = GetAssetBundleName(importer.assetPath),
      AssetBundleVariant = GetAssetBundleVariant(importer.assetPath),
    };

    if (importer is TextureImporter texture)
    {
      settings.Texture = new PersistedTextureImporterSettings
      {
        TextureType = texture.textureType,
        TextureShape = texture.textureShape,
        SrgbTexture = texture.sRGBTexture,
        AlphaIsTransparency = texture.alphaIsTransparency,
        MipmapEnabled = texture.mipmapEnabled,
        Readable = texture.readable,
        Compression = texture.textureCompression,
        CompressionQuality = texture.compressionQuality,
        MaxTextureSize = texture.maxTextureSize,
        AnisoLevel = texture.anisoLevel,
        FilterMode = texture.filterMode,
        WrapMode = texture.wrapMode,
        WrapModeU = texture.wrapModeU,
        WrapModeV = texture.wrapModeV,
        WrapModeW = texture.wrapModeW,
      };
    }
    else if (importer is AudioImporter audio)
    {
      settings.Audio = new PersistedAudioImporterSettings
      {
        LoadInBackground = audio.loadInBackground,
        PreloadAudioData = audio.preloadAudioData,
        Ambisonic = audio.ambisonic,
        ForceToMono = audio.forceToMono,
        Normalize = audio.normalize,
        DefaultSampleSettings = audio.defaultSampleSettings,
      };
    }

    return settings;
  }

  private static void ApplyPersistedImporterSettings(AssetImporter importer, byte[] metaBytes)
  {
    if (metaBytes.Length == 0) return;
    var content = Encoding.UTF8.GetString(metaBytes);
    var hasYamlBundleName = TryReadUnityYamlAssetBundleScalar(content, "assetBundleName", out var yamlBundleName);
    var hasYamlBundleVariant = TryReadUnityYamlAssetBundleScalar(content, "assetBundleVariant", out var yamlBundleVariant);
    var hasPayload = false;
    try
    {
      var line = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
        .FirstOrDefault(value => value.StartsWith(ImporterSettingsPrefix, StringComparison.Ordinal));
      hasPayload = line is not null;
      if (line is not null)
      {
        var payload = line[ImporterSettingsPrefix.Length..].Trim();
        var settings = JsonSerializer.Deserialize<PersistedImporterSettings>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)), ImporterSettingsJsonOptions);
        if (settings is not null)
        {
          importer.editorUserSettingsData = settings.EditorUserSettingsData ?? string.Empty;
          SetAssetBundleNameAndVariant(importer.assetPath, settings.AssetBundleName ?? string.Empty, settings.AssetBundleVariant ?? string.Empty);

          if (importer is TextureImporter texture && settings.Texture is not null)
          {
            texture.textureType = settings.Texture.TextureType;
            texture.textureShape = settings.Texture.TextureShape;
            texture.sRGBTexture = settings.Texture.SrgbTexture;
            texture.alphaIsTransparency = settings.Texture.AlphaIsTransparency;
            texture.mipmapEnabled = settings.Texture.MipmapEnabled;
            texture.readable = settings.Texture.Readable;
            texture.textureCompression = settings.Texture.Compression;
            texture.compressionQuality = settings.Texture.CompressionQuality;
            texture.maxTextureSize = settings.Texture.MaxTextureSize;
            texture.anisoLevel = settings.Texture.AnisoLevel;
            texture.filterMode = settings.Texture.FilterMode;
            texture.wrapMode = settings.Texture.WrapMode;
            texture.wrapModeU = settings.Texture.WrapModeU;
            texture.wrapModeV = settings.Texture.WrapModeV;
            texture.wrapModeW = settings.Texture.WrapModeW;
          }
          else if (importer is AudioImporter audio && settings.Audio is not null)
          {
            audio.loadInBackground = settings.Audio.LoadInBackground;
            audio.preloadAudioData = settings.Audio.PreloadAudioData;
            audio.ambisonic = settings.Audio.Ambisonic;
            audio.forceToMono = settings.Audio.ForceToMono;
            audio.normalize = settings.Audio.Normalize;
            audio.defaultSampleSettings = settings.Audio.DefaultSampleSettings;
          }
        }
      }
    }
    catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or DecoderFallbackException)
    {
      Debug.LogWarning("Ignoring invalid Anity importer settings metadata: " + exception.Message);
    }

    // Unity YAML is the on-disk source of truth and must supersede the transitional payload.
    var hasUnityImporterYaml = ApplyUnityYamlImporterSettings(importer, content);

    // Unity's root-level fields are authoritative when an old Anity payload coexists.
    if (hasYamlBundleName || hasYamlBundleVariant)
    {
      SetAssetBundleNameAndVariant(importer.assetPath, hasYamlBundleName ? yamlBundleName : string.Empty, hasYamlBundleVariant ? yamlBundleVariant : string.Empty);
    }
    else if (!hasPayload || hasUnityImporterYaml)
    {
      // A genuine importer YAML block with no bundle fields represents Unity's empty assignment.
      SetAssetBundleNameAndVariant(importer.assetPath, string.Empty, string.Empty);
    }
  }

  private static bool ApplyUnityYamlImporterSettings(AssetImporter importer, string content)
  {
    var values = ReadUnityYamlScalars(content);
    if (importer is TextureImporter texture && values.ContainsKey("TextureImporter/serializedVersion"))
    {
      if (TryGetYamlEnum(values, "TextureImporter/textureType", out TextureImporterType textureType)) texture.textureType = textureType;
      if (TryGetYamlEnum(values, "TextureImporter/textureShape", out TextureImporterShape textureShape)) texture.textureShape = textureShape;
      if (TryGetYamlBool(values, "TextureImporter/mipmaps/enableMipMap", out var mipmapEnabled)) texture.mipmapEnabled = mipmapEnabled;
      if (TryGetYamlBool(values, "TextureImporter/mipmaps/sRGBTexture", out var srgbTexture)) texture.sRGBTexture = srgbTexture;
      if (TryGetYamlBool(values, "TextureImporter/alphaIsTransparency", out var alphaIsTransparency)) texture.alphaIsTransparency = alphaIsTransparency;
      if (TryGetYamlBool(values, "TextureImporter/isReadable", out var readable)) texture.readable = readable;
      if (TryGetYamlBool(values, "TextureImporter/streamingMipmaps", out var streamingMipmaps)) texture.streamingMipmaps = streamingMipmaps;
      if (TryGetYamlInt(values, "TextureImporter/streamingMipmapsPriority", out var streamingPriority)) texture.streamingMipmapsPriority = streamingPriority;
      if (TryGetYamlInt(values, "TextureImporter/maxTextureSize", out var maxTextureSize)) texture.maxTextureSize = maxTextureSize;
      if (TryGetYamlEnum(values, "TextureImporter/textureSettings/filterMode", out FilterMode filterMode)) texture.filterMode = filterMode;
      if (TryGetYamlInt(values, "TextureImporter/textureSettings/aniso", out var aniso)) texture.anisoLevel = aniso;
      if (TryGetYamlEnum(values, "TextureImporter/textureSettings/wrapU", out TextureWrapMode wrapU)) texture.wrapModeU = wrapU;
      if (TryGetYamlEnum(values, "TextureImporter/textureSettings/wrapV", out TextureWrapMode wrapV)) texture.wrapModeV = wrapV;
      if (TryGetYamlEnum(values, "TextureImporter/textureSettings/wrapW", out TextureWrapMode wrapW)) texture.wrapModeW = wrapW;
      if (TryGetYamlFloat(values, "TextureImporter/textureSettings/mipBias", out var mipBias)) texture.mipMapBias = mipBias;
      if (TryGetYamlEnum(values, "TextureImporter/nPOTScale", out TextureImporterNPOTScale npotScale)) texture.npotScale = npotScale;
      if (TryGetYamlInt(values, "TextureImporter/compressionQuality", out var compressionQuality)) texture.compressionQuality = compressionQuality;
      if (TryGetYamlEnum(values, "TextureImporter/spriteMode", out SpriteImportMode spriteMode)) texture.spriteImportMode = spriteMode;
      if (TryGetYamlEnum(values, "TextureImporter/spriteMeshType", out SpriteMeshType spriteMeshType)) texture.spriteMeshType = spriteMeshType;
      if (TryGetYamlInt(values, "TextureImporter/spriteExtrude", out var spriteExtrude) && spriteExtrude >= 0) texture.spriteExtrude = (uint)spriteExtrude;
      if (TryGetYamlFloat(values, "TextureImporter/spritePixelsToUnits", out var spritePixelsPerUnit)) texture.spritePixelsPerUnit = spritePixelsPerUnit;
      if (TryGetYamlBool(values, "TextureImporter/bumpmap/convertToNormalMap", out var convertToNormalMap)) texture.convertToNormalmap = convertToNormalMap;
      if (TryGetYamlEnum(values, "TextureImporter/bumpmap/normalMapFilter", out TextureImporterNormalFilter normalMapFilter)) texture.normalmapFilter = normalMapFilter;
      if (values.TryGetValue("TextureImporter/userData", out var textureUserData)) importer.editorUserSettingsData = textureUserData;
      ApplyUnityTexturePlatformSettings(texture, content);
      return true;
    }

    if (importer is AudioImporter audio && values.ContainsKey("AudioImporter/serializedVersion"))
    {
      var settings = audio.defaultSampleSettings;
      if (TryGetYamlEnum(values, "AudioImporter/defaultSettings/loadType", out AudioClipLoadType loadType)) settings.loadType = loadType;
      if (TryGetYamlEnum(values, "AudioImporter/defaultSettings/sampleRateSetting", out AudioSampleRateSetting sampleRateSetting)) settings.sampleRateSetting = sampleRateSetting;
      if (TryGetYamlInt(values, "AudioImporter/defaultSettings/sampleRateOverride", out var sampleRateOverride) && sampleRateOverride >= 0) settings.sampleRateOverride = (uint)sampleRateOverride;
      if (TryGetYamlEnum(values, "AudioImporter/defaultSettings/compressionFormat", out AudioCompressionFormat compressionFormat)) settings.compressionFormat = compressionFormat;
      if (TryGetYamlFloat(values, "AudioImporter/defaultSettings/quality", out var quality)) settings.quality = quality;
      audio.defaultSampleSettings = settings;
      if (TryGetYamlBool(values, "AudioImporter/defaultSettings/preloadAudioData", out var preloadAudioData)) audio.preloadAudioData = preloadAudioData;
      if (TryGetYamlBool(values, "AudioImporter/forceToMono", out var forceToMono)) audio.forceToMono = forceToMono;
      if (TryGetYamlBool(values, "AudioImporter/normalize", out var normalize)) audio.normalize = normalize;
      if (TryGetYamlBool(values, "AudioImporter/loadInBackground", out var loadInBackground)) audio.loadInBackground = loadInBackground;
      if (TryGetYamlBool(values, "AudioImporter/ambisonic", out var ambisonic)) audio.ambisonic = ambisonic;
      if (values.TryGetValue("AudioImporter/userData", out var audioUserData)) importer.editorUserSettingsData = audioUserData;
      return true;
    }

    if (importer is ModelImporter model && values.ContainsKey("ModelImporter/serializedVersion"))
    {
      if (TryGetYamlEnum(values, "ModelImporter/materials/materialImportMode", out ModelImporterMaterialImportMode materialImportMode)) model.materialImportMode = materialImportMode;
      else if (TryGetYamlBool(values, "ModelImporter/materials/importMaterials", out var importMaterials)) model.materialImportMode = importMaterials ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
      if (TryGetYamlEnum(values, "ModelImporter/materials/materialName", out ModelImporterMaterialName materialName)) model.materialName = materialName;
      if (TryGetYamlEnum(values, "ModelImporter/materials/materialSearch", out ModelImporterMaterialSearch materialSearch)) model.materialSearch = materialSearch;
      if (TryGetYamlEnum(values, "ModelImporter/materials/materialLocation", out ModelImporterMaterialLocation materialLocation)) model.materialLocation = materialLocation;
      if (TryGetYamlBool(values, "ModelImporter/animations/bakeSimulation", out var bakeSimulation)) model.bakeSimulation = bakeSimulation;
      if (TryGetYamlBool(values, "ModelImporter/animations/bakeIK", out var bakeIK)) model.bakeIK = bakeIK;
      if (TryGetYamlBool(values, "ModelImporter/animations/resampleCurves", out var resampleCurves)) model.resampleCurves = resampleCurves;
      if (TryGetYamlBool(values, "ModelImporter/animations/optimizeGameObjects", out var optimizeGameObjects)) model.optimizeGameObjects = optimizeGameObjects;
      if (TryGetYamlBool(values, "ModelImporter/animations/removeConstantScaleCurves", out var removeConstantScaleCurves)) model.removeConstantScaleCurves = removeConstantScaleCurves;
      if (TryGetYamlBool(values, "ModelImporter/animations/importAnimatedCustomProperties", out var importAnimatedCustomProperties)) model.importAnimatedCustomProperties = importAnimatedCustomProperties;
      if (TryGetYamlBool(values, "ModelImporter/animations/importConstraints", out var importConstraints)) model.importConstraints = importConstraints;
      if (TryGetYamlEnum(values, "ModelImporter/animations/animationCompression", out ModelImporterAnimationCompression animationCompression)) model.animationCompression = animationCompression;
      if (TryGetYamlFloat(values, "ModelImporter/animations/animationRotationError", out var rotationError)) model.animationRotationError = rotationError;
      if (TryGetYamlFloat(values, "ModelImporter/animations/animationPositionError", out var positionError)) model.animationPositionError = positionError;
      if (TryGetYamlFloat(values, "ModelImporter/animations/animationScaleError", out var scaleError)) model.animationScaleError = scaleError;
      if (TryGetYamlBool(values, "ModelImporter/animations/isReadable", out var readable)) model.isReadable = readable;
      if (TryGetYamlFloat(values, "ModelImporter/meshes/globalScale", out var globalScale)) model.globalScale = globalScale;
      if (TryGetYamlEnum(values, "ModelImporter/meshes/meshCompression", out ModelImporterMeshCompression meshCompression)) model.meshCompression = meshCompression;
      if (TryGetYamlBool(values, "ModelImporter/meshes/useSRGBMaterialColor", out var useSrgbMaterialColor)) model.useSRGBMaterialColor = useSrgbMaterialColor;
      if (TryGetYamlBool(values, "ModelImporter/meshes/addColliders", out var addCollider)) model.addCollider = addCollider;
      if (TryGetYamlBool(values, "ModelImporter/meshes/importVisibility", out var importVisibility)) model.importVisibility = importVisibility;
      if (TryGetYamlBool(values, "ModelImporter/meshes/importBlendShapes", out var importBlendShapes)) model.importBlendShapes = importBlendShapes;
      if (TryGetYamlBool(values, "ModelImporter/meshes/importCameras", out var importCameras)) model.importCameras = importCameras;
      if (TryGetYamlBool(values, "ModelImporter/meshes/importLights", out var importLights)) model.importLights = importLights;
      if (TryGetYamlBool(values, "ModelImporter/meshes/importPhysicalCameras", out var importPhysicalCameras)) model.importPhysicalCameras = importPhysicalCameras;
      if (TryGetYamlBool(values, "ModelImporter/meshes/sortHierarchyByName", out var sortHierarchyByName)) model.sortHierarchyByName = sortHierarchyByName;
      if (TryGetYamlBool(values, "ModelImporter/meshes/swapUVChannels", out var swapUvChannels)) model.swapUVChannels = swapUvChannels;
      if (TryGetYamlBool(values, "ModelImporter/meshes/generateSecondaryUV", out var generateSecondaryUv)) model.generateSecondaryUV = generateSecondaryUv;
      if (TryGetYamlBool(values, "ModelImporter/meshes/useFileUnits", out var useFileUnits)) model.useFileUnits = useFileUnits;
      if (TryGetYamlBool(values, "ModelImporter/meshes/optimizeMeshForGPU", out var optimizeMesh)) model.optimizeMesh = optimizeMesh;
      if (TryGetYamlBool(values, "ModelImporter/meshes/keepQuads", out var keepQuads)) model.keepQuads = keepQuads;
      if (TryGetYamlBool(values, "ModelImporter/meshes/weldVertices", out var weldVertices)) model.weldVertices = weldVertices;
      if (TryGetYamlEnum(values, "ModelImporter/meshes/indexFormat", out ModelImporterIndexFormat indexFormat)) model.indexFormat = indexFormat;
      if (TryGetYamlFloat(values, "ModelImporter/meshes/secondaryUVAngleDistortion", out var angleDistortion)) model.secondaryUVAngleDistortion = angleDistortion;
      if (TryGetYamlFloat(values, "ModelImporter/meshes/secondaryUVAreaDistortion", out var areaDistortion)) model.secondaryUVAreaDistortion = areaDistortion;
      if (TryGetYamlFloat(values, "ModelImporter/meshes/secondaryUVHardAngle", out var hardAngle)) model.secondaryUVHardAngle = hardAngle;
      if (TryGetYamlFloat(values, "ModelImporter/meshes/secondaryUVPackMargin", out var packMargin)) model.secondaryUVPackMargin = packMargin;
      if (TryGetYamlBool(values, "ModelImporter/meshes/useFileScale", out var useFileScale)) model.useFileScale = useFileScale;
      if (TryGetYamlBool(values, "ModelImporter/meshes/bakeAxisConversion", out var bakeAxisConversion)) model.bakeAxisConversion = bakeAxisConversion;
      if (TryGetYamlBool(values, "ModelImporter/meshes/preserveHierarchy", out var preserveHierarchy)) model.preserveHierarchy = preserveHierarchy;
      if (TryGetYamlBool(values, "ModelImporter/meshes/strictVertexDataChecks", out var strictVertexDataChecks)) model.strictVertexDataChecks = strictVertexDataChecks;
      if (TryGetYamlEnum(values, "ModelImporter/tangentSpace/normalImportMode", out ModelImporterNormals importNormals)) model.importNormals = importNormals;
      if (TryGetYamlEnum(values, "ModelImporter/tangentSpace/tangentImportMode", out ModelImporterTangents importTangents)) model.importTangents = importTangents;
      if (TryGetYamlBool(values, "ModelImporter/importAnimation", out var importAnimation)) model.importAnimation = importAnimation;
      if (TryGetYamlBool(values, "ModelImporter/importBlendShapeDeformPercent", out var importBlendShapeDeformPercent)) model.importBlendShapeDeformPercent = importBlendShapeDeformPercent;
      if (TryGetYamlEnum(values, "ModelImporter/avatarSetup", out ModelImporterAvatarSetup avatarSetup)) model.avatarSetup = avatarSetup;
      if (TryGetYamlBool(values, "ModelImporter/autoGenerateAvatarMappingIfUnspecified", out var autoAvatarMapping)) model.autoGenerateAvatarMappingIfUnspecified = autoAvatarMapping;
      if (values.TryGetValue("ModelImporter/humanDescription/rootMotionBoneName", out var motionNodeName)) model.motionNodeName = motionNodeName;
      var humanDescription = model.humanDescription;
      var hasHumanDescription = false;
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/armTwist", out var armTwist)) { humanDescription.upperArmTwist = armTwist; hasHumanDescription = true; }
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/foreArmTwist", out var foreArmTwist)) { humanDescription.lowerArmTwist = foreArmTwist; hasHumanDescription = true; }
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/upperLegTwist", out var upperLegTwist)) { humanDescription.upperLegTwist = upperLegTwist; hasHumanDescription = true; }
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/legTwist", out var legTwist)) { humanDescription.lowerLegTwist = legTwist; hasHumanDescription = true; }
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/armStretch", out var armStretch)) { humanDescription.armStretch = armStretch; hasHumanDescription = true; }
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/legStretch", out var legStretch)) { humanDescription.legStretch = legStretch; hasHumanDescription = true; }
      if (TryGetYamlFloat(values, "ModelImporter/humanDescription/feetSpacing", out var feetSpacing)) { humanDescription.feetSpacing = feetSpacing; hasHumanDescription = true; }
      if (TryGetYamlBool(values, "ModelImporter/humanDescription/hasTranslationDoF", out var translationDof)) { humanDescription.hasTranslationDoF = translationDof; hasHumanDescription = true; }
      if (hasHumanDescription) model.humanDescription = humanDescription;
      ApplyUnityHumanDescriptionBones(model, content);
      ApplyUnityHumanDescriptionSkeleton(model, content);
      ApplyUnityModelAvatarSource(model, content);
      if (TryGetYamlEnum(values, "ModelImporter/animationType", out ModelImporterAnimationType animationType)) model.animationType = animationType;
      if (values.TryGetValue("ModelImporter/userData", out var modelUserData)) importer.editorUserSettingsData = modelUserData;
      ApplyUnityModelClipAnimations(model, content);
      return true;
    }

    if (importer.GetType() == typeof(AssetImporter) && values.TryGetValue("DefaultImporter/userData", out var defaultUserData))
    {
      importer.editorUserSettingsData = defaultUserData;
      return true;
    }

    return false;
  }

  private static void ApplyUnityHumanDescriptionBones(ModelImporter model, string content)
  {
    var inModel = false;
    var inDescription = false;
    var inHuman = false;
    var inLimit = false;
    var hadHumanSection = false;
    var bones = new List<HumanBone>();
    HumanBone? current = null;
    void Commit() { if (current.HasValue) bones.Add(current.Value); }
    foreach (var source in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
    {
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal)) { if (inHuman) Commit(); inModel = line == "ModelImporter:"; inDescription = false; inHuman = false; current = null; continue; }
      if (!inModel) continue;
      if (indent == 2 && line == "humanDescription:") { inDescription = true; continue; }
      if (!inDescription) continue;
      if (indent == 4 && line.StartsWith("human:", StringComparison.Ordinal)) { hadHumanSection = true; inHuman = !line.EndsWith("[]", StringComparison.Ordinal); continue; }
      if (!inHuman) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal))
      {
        Commit();
        var initialBone = new HumanBone();
        var first = line[2..];
        var firstColon = first.IndexOf(':');
        if (firstColon > 0 && first[..firstColon].Trim() == "boneName") initialBone.boneName = DecodeUnityYamlScalar(first[(firstColon + 1)..].Trim());
        current = initialBone;
        inLimit = false;
        continue;
      }
      if (indent <= 4) { Commit(); inHuman = false; current = null; continue; }
      if (!current.HasValue) continue;
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var key = line[..colon].Trim();
      var value = DecodeUnityYamlScalar(line[(colon + 1)..].Trim());
      var bone = current.Value;
      if (indent == 6 && key == "boneName") bone.boneName = value;
      else if (indent == 6 && key == "humanName") bone.humanName = value;
      else if (indent == 6 && key == "limit") { inLimit = true; current = bone; continue; }
      else if (inLimit && indent >= 8)
      {
        var limit = bone.limit;
        if (key == "min" && TryParseUnityYamlVector3(value, out var min)) limit.min = min;
        else if (key == "max" && TryParseUnityYamlVector3(value, out var max)) limit.max = max;
        else if (key == "value" && TryParseUnityYamlVector3(value, out var center)) limit.center = center;
        else if (key == "length" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var length)) limit.axisLength = length;
        else if (key == "modified") limit.useDefaultValues = value is "0" or "false";
        bone.limit = limit;
      }
      current = bone;
    }
    if (inHuman) Commit();
    if (!hadHumanSection) return;
    var description = model.humanDescription;
    description.human = bones.ToArray();
    model.humanDescription = description;
  }

  private static void ApplyUnityHumanDescriptionSkeleton(ModelImporter model, string content)
  {
    var inModel = false;
    var inDescription = false;
    var inSkeleton = false;
    var hadSkeletonSection = false;
    var bones = new List<SkeletonBone>();
    SkeletonBone? current = null;
    void Commit() { if (current.HasValue) bones.Add(current.Value); }
    foreach (var source in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
    {
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        if (inSkeleton) Commit();
        inModel = line == "ModelImporter:";
        inDescription = false;
        inSkeleton = false;
        current = null;
        continue;
      }
      if (!inModel) continue;
      if (indent == 2 && line == "humanDescription:") { inDescription = true; continue; }
      if (!inDescription) continue;
      if (indent == 4 && line.StartsWith("skeleton:", StringComparison.Ordinal))
      {
        hadSkeletonSection = true;
        inSkeleton = !line.EndsWith("[]", StringComparison.Ordinal);
        continue;
      }
      if (!inSkeleton) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal))
      {
        Commit();
        var bone = new SkeletonBone();
        var first = line[2..];
        var firstColon = first.IndexOf(':');
        if (firstColon > 0 && first[..firstColon].Trim() == "name")
          bone.name = DecodeUnityYamlScalar(first[(firstColon + 1)..].Trim());
        current = bone;
        continue;
      }
      if (indent <= 4) { Commit(); inSkeleton = false; current = null; continue; }
      if (!current.HasValue) continue;
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var key = line[..colon].Trim();
      var value = DecodeUnityYamlScalar(line[(colon + 1)..].Trim());
      var boneValue = current.Value;
      if (indent == 6 && key == "name") boneValue.name = value;
      else if (indent == 6 && key == "position" && TryParseUnityYamlVector3(value, out var position)) boneValue.position = position;
      else if (indent == 6 && key == "rotation" && TryParseUnityYamlQuaternion(value, out var rotation)) boneValue.rotation = rotation;
      else if (indent == 6 && key == "scale" && TryParseUnityYamlVector3(value, out var scale)) boneValue.scale = scale;
      current = boneValue;
    }
    if (inSkeleton) Commit();
    if (!hadSkeletonSection) return;
    var description = model.humanDescription;
    description.skeleton = bones.ToArray();
    model.humanDescription = description;
  }

  private static bool TryParseUnityYamlVector3(string value, out Vector3 vector)
  {
    vector = default;
    var trimmed = value.Trim().TrimStart('{').TrimEnd('}');
    var values = new Dictionary<string, float>(StringComparer.Ordinal);
    foreach (var part in trimmed.Split(','))
    {
      var colon = part.IndexOf(':');
      if (colon <= 0 || !float.TryParse(part[(colon + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) continue;
      values[part[..colon].Trim()] = parsed;
    }
    if (!values.TryGetValue("x", out var x) || !values.TryGetValue("y", out var y) || !values.TryGetValue("z", out var z)) return false;
    vector = new Vector3(x, y, z);
    return true;
  }

  private static bool TryParseUnityYamlQuaternion(string value, out Quaternion quaternion)
  {
    quaternion = default;
    var trimmed = value.Trim().TrimStart('{').TrimEnd('}');
    var values = new Dictionary<string, float>(StringComparer.Ordinal);
    foreach (var part in trimmed.Split(','))
    {
      var colon = part.IndexOf(':');
      if (colon <= 0 || !float.TryParse(part[(colon + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) continue;
      values[part[..colon].Trim()] = parsed;
    }
    if (!values.TryGetValue("x", out var x) || !values.TryGetValue("y", out var y) || !values.TryGetValue("z", out var z) || !values.TryGetValue("w", out var w)) return false;
    quaternion = new Quaternion(x, y, z, w);
    return true;
  }

  private static void ApplyUnityModelAvatarSource(ModelImporter model, string content)
  {
    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    var inModelImporter = false;
    for (var index = 0; index < lines.Length; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        inModelImporter = line == "ModelImporter:";
        continue;
      }
      if (!inModelImporter || indent != 2 || !line.StartsWith("lastHumanDescriptionAvatarSource:", StringComparison.Ordinal)) continue;
      var reference = new StringBuilder(line[(line.IndexOf(':') + 1)..].Trim());
      while (!reference.ToString().Contains('}') && index + 1 < lines.Length)
      {
        var continuation = lines[index + 1];
        var continuationIndent = continuation.Length - continuation.TrimStart(' ').Length;
        if (continuationIndent <= 2) break;
        reference.Append(' ').Append(continuation.Trim());
        index++;
      }
      model.SetSourceAvatarReference(TryExtractUnityGuid(reference.ToString(), out var guid) ? guid : string.Empty);
      return;
    }
  }

  private static bool TryExtractUnityGuid(string value, out string guid)
  {
    guid = string.Empty;
    var marker = value.IndexOf("guid:", StringComparison.Ordinal);
    if (marker < 0) return false;
    var start = marker + "guid:".Length;
    while (start < value.Length && char.IsWhiteSpace(value[start])) start++;
    if (start + 32 > value.Length) return false;
    var candidate = value.Substring(start, 32);
    if (candidate.Length != 32 || candidate.Any(character => !Uri.IsHexDigit(character))) return false;
    guid = candidate.ToLowerInvariant();
    return true;
  }

  private static void ApplyUnityModelClipAnimations(ModelImporter model, string content)
  {
    var inModelImporter = false;
    var inAnimations = false;
    var inClips = false;
    var clips = new List<ModelImporterClipAnimation>();
    ModelImporterClipAnimation? current = null;
    var declaredEmpty = false;
    var hadClipSection = false;
    void Commit() { if (current is not null) clips.Add(current); }

    foreach (var source in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
    {
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        if (inClips) Commit();
        inModelImporter = line == "ModelImporter:";
        inAnimations = false;
        inClips = false;
        current = null;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "animations:") { inAnimations = true; continue; }
      if (!inAnimations) continue;
      if (indent == 4 && line.StartsWith("clipAnimations:", StringComparison.Ordinal))
      {
        inClips = true;
        hadClipSection = true;
        declaredEmpty = line.EndsWith("[]", StringComparison.Ordinal);
        continue;
      }
      if (!inClips) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal)) { Commit(); current = new ModelImporterClipAnimation(); continue; }
      if (indent <= 4) { Commit(); inClips = false; current = null; continue; }
      if (current is null) continue;
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var key = line[..colon].Trim();
      var value = DecodeUnityYamlScalar(line[(colon + 1)..].Trim());
      if (key == "name") current.name = value;
      else if (key == "takeName") current.takeName = value;
      else if (key == "firstFrame" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var firstFrame)) current.firstFrame = firstFrame;
      else if (key == "lastFrame" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lastFrame)) current.lastFrame = lastFrame;
      else if (key == "loopTime") current.loopTime = value is "1" or "true";
      else if (key == "loopPose") current.loopPose = value is "1" or "true";
      else if (key == "lockRootRotation") current.lockRootRotation = value is "1" or "true";
      else if (key == "lockRootHeightY") current.lockRootHeightY = value is "1" or "true";
      else if (key == "lockRootPositionXZ") current.lockRootPositionXZ = value is "1" or "true";
      else if (key == "mirror") current.mirror = value is "1" or "true";
      else if (key == "wrapMode" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wrapMode)) current.wrapMode = (WrapMode)wrapMode;
      else if (key == "cycleOffset" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cycleOffset)) current.cycleOffset = cycleOffset;
      else if (key == "keepOriginalOrientation") current.keepOriginalOrientation = value is "1" or "true";
      else if (key == "keepOriginalPositionY") current.keepOriginalPositionY = value is "1" or "true";
      else if (key == "keepOriginalPositionXZ") current.keepOriginalPositionXZ = value is "1" or "true";
      else if (key == "heightFromFeet") current.heightFromFeet = value is "1" or "true";
      else if (key == "hasAdditiveReferencePose") current.hasAdditiveReferencePose = value is "1" or "true";
    }
    if (inClips) Commit();
    if (hadClipSection || declaredEmpty) model.clipAnimations = clips.ToArray();
  }

  private static void ApplyUnityTexturePlatformSettings(TextureImporter texture, string content)
  {
    var inTextureImporter = false;
    var inPlatformSettings = false;
    TextureImporterPlatformSettings? current = null;
    void Commit()
    {
      if (current is not null && !string.IsNullOrWhiteSpace(current.name)) texture.SetPlatformTextureSettings(current);
    }

    foreach (var source in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
    {
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        if (inPlatformSettings) Commit();
        inTextureImporter = line == "TextureImporter:";
        inPlatformSettings = false;
        current = null;
        continue;
      }
      if (!inTextureImporter) continue;
      if (indent == 2 && line == "platformSettings:")
      {
        inPlatformSettings = true;
        continue;
      }
      if (!inPlatformSettings) continue;
      if (indent == 2 && line.StartsWith("- ", StringComparison.Ordinal))
      {
        Commit();
        current = new TextureImporterPlatformSettings();
        continue;
      }
      if (indent <= 2)
      {
        Commit();
        inPlatformSettings = false;
        current = null;
        continue;
      }
      if (current is null) continue;
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var key = line[..colon].Trim();
      var value = DecodeUnityYamlScalar(line[(colon + 1)..].Trim());
      if (key == "buildTarget") current.name = value;
      else if (key == "maxTextureSize" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxTextureSize)) current.maxTextureSize = maxTextureSize;
      else if (key == "textureFormat" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var textureFormat)) current.format = (TextureImporterFormat)textureFormat;
      else if (key == "textureCompression" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var textureCompression)) current.textureCompression = (TextureImporterCompression)textureCompression;
      else if (key == "compressionQuality" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var compressionQuality)) current.compressionQuality = compressionQuality;
      else if (key == "overridden") current.overridden = value is "1" or "true";
      else if (key == "crunchedCompression") current.crunchedCompression = value is "1" or "true";
      else if (key == "allowsAlphaSplitting") current.allowsAlphaSplitting = value is "1" or "true";
      else if (key == "androidETC2FallbackOverride" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var etcFallback)) current.androidETC2FallbackOverride = (AndroidETC2FallbackOverride)etcFallback;
    }
    if (inPlatformSettings) Commit();
  }

  private static bool TryReadUnityYamlAssetBundleScalar(string content, string key, out string value)
  {
    var values = ReadUnityYamlScalars(content);
    foreach (var (path, scalar) in values)
    {
      if (!path.EndsWith("/" + key, StringComparison.Ordinal)) continue;
      var slash = path.IndexOf('/');
      if (slash > 0 && path.EndsWith("Importer/" + key, StringComparison.Ordinal))
      {
        value = scalar;
        return true;
      }
    }

    // Anity versions predating native-style importer blocks wrote these fields at root.
    return TryReadUnityYamlRootScalar(content, key, out value);
  }

  private static void WriteUnityYamlAssetBundleAssignment(List<string> lines, AssetImporter importer, string name, string variant)
  {
    lines.RemoveAll(line =>
    {
      var trimmed = line.TrimStart(' ');
      return trimmed.StartsWith("assetBundleName:", StringComparison.Ordinal) || trimmed.StartsWith("assetBundleVariant:", StringComparison.Ordinal);
    });

    var importerHeader = importer switch
    {
      TextureImporter => "TextureImporter:",
      AudioImporter => "AudioImporter:",
      ModelImporter => "ModelImporter:",
      _ => "DefaultImporter:",
    };
    var headerIndex = lines.FindIndex(line => line == importerHeader);
    if (headerIndex < 0)
    {
      lines.Add(importerHeader);
      lines.Add("  serializedVersion: " + (importer is TextureImporter ? "13" : importer is AudioImporter ? "7" : importer is ModelImporter ? "23" : "1"));
      headerIndex = lines.Count - 2;
    }

    var insertAt = headerIndex + 1;
    while (insertAt < lines.Count && (string.IsNullOrWhiteSpace(lines[insertAt]) || char.IsWhiteSpace(lines[insertAt][0]))) insertAt++;
    lines.Insert(insertAt, "  assetBundleName: " + name);
    lines.Insert(insertAt + 1, "  assetBundleVariant: " + variant);
  }

  private static void WriteUnityYamlImporterSettings(List<string> lines, AssetImporter importer)
  {
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    if (importer is TextureImporter texture)
    {
      const string prefix = "TextureImporter/";
      values[prefix + "textureType"] = ((int)texture.textureType).ToString(CultureInfo.InvariantCulture);
      values[prefix + "textureShape"] = ((int)texture.textureShape).ToString(CultureInfo.InvariantCulture);
      values[prefix + "mipmaps/enableMipMap"] = UnityYamlBool(texture.mipmapEnabled);
      values[prefix + "mipmaps/sRGBTexture"] = UnityYamlBool(texture.sRGBTexture);
      values[prefix + "alphaIsTransparency"] = UnityYamlBool(texture.alphaIsTransparency);
      values[prefix + "isReadable"] = UnityYamlBool(texture.readable);
      values[prefix + "streamingMipmaps"] = UnityYamlBool(texture.streamingMipmaps);
      values[prefix + "streamingMipmapsPriority"] = texture.streamingMipmapsPriority.ToString(CultureInfo.InvariantCulture);
      values[prefix + "maxTextureSize"] = texture.maxTextureSize.ToString(CultureInfo.InvariantCulture);
      values[prefix + "textureSettings/filterMode"] = ((int)texture.filterMode).ToString(CultureInfo.InvariantCulture);
      values[prefix + "textureSettings/aniso"] = texture.anisoLevel.ToString(CultureInfo.InvariantCulture);
      values[prefix + "textureSettings/mipBias"] = texture.mipMapBias.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "textureSettings/wrapU"] = ((int)texture.wrapModeU).ToString(CultureInfo.InvariantCulture);
      values[prefix + "textureSettings/wrapV"] = ((int)texture.wrapModeV).ToString(CultureInfo.InvariantCulture);
      values[prefix + "textureSettings/wrapW"] = ((int)texture.wrapModeW).ToString(CultureInfo.InvariantCulture);
      values[prefix + "nPOTScale"] = ((int)texture.npotScale).ToString(CultureInfo.InvariantCulture);
      values[prefix + "compressionQuality"] = texture.compressionQuality.ToString(CultureInfo.InvariantCulture);
      values[prefix + "spriteMode"] = ((int)texture.spriteImportMode).ToString(CultureInfo.InvariantCulture);
      values[prefix + "spriteMeshType"] = ((int)texture.spriteMeshType).ToString(CultureInfo.InvariantCulture);
      values[prefix + "spriteExtrude"] = texture.spriteExtrude.ToString(CultureInfo.InvariantCulture);
      values[prefix + "spritePixelsToUnits"] = texture.spritePixelsPerUnit.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "bumpmap/convertToNormalMap"] = UnityYamlBool(texture.convertToNormalmap);
      values[prefix + "bumpmap/normalMapFilter"] = ((int)texture.normalmapFilter).ToString(CultureInfo.InvariantCulture);
      values[prefix + "userData"] = EncodeUnityYamlScalar(importer.editorUserSettingsData);
    }
    else if (importer is AudioImporter audio)
    {
      const string prefix = "AudioImporter/";
      var settings = audio.defaultSampleSettings;
      values[prefix + "defaultSettings/loadType"] = ((int)settings.loadType).ToString(CultureInfo.InvariantCulture);
      values[prefix + "defaultSettings/sampleRateSetting"] = ((int)settings.sampleRateSetting).ToString(CultureInfo.InvariantCulture);
      values[prefix + "defaultSettings/sampleRateOverride"] = settings.sampleRateOverride.ToString(CultureInfo.InvariantCulture);
      values[prefix + "defaultSettings/compressionFormat"] = ((int)settings.compressionFormat).ToString(CultureInfo.InvariantCulture);
      values[prefix + "defaultSettings/quality"] = settings.quality.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "defaultSettings/preloadAudioData"] = UnityYamlBool(audio.preloadAudioData);
      values[prefix + "forceToMono"] = UnityYamlBool(audio.forceToMono);
      values[prefix + "normalize"] = UnityYamlBool(audio.normalize);
      values[prefix + "loadInBackground"] = UnityYamlBool(audio.loadInBackground);
      values[prefix + "ambisonic"] = UnityYamlBool(audio.ambisonic);
      values[prefix + "userData"] = EncodeUnityYamlScalar(importer.editorUserSettingsData);
    }
    else if (importer is ModelImporter model)
    {
      const string prefix = "ModelImporter/";
      values[prefix + "materials/materialImportMode"] = ((int)model.materialImportMode).ToString(CultureInfo.InvariantCulture);
      values[prefix + "materials/materialName"] = ((int)model.materialName).ToString(CultureInfo.InvariantCulture);
      values[prefix + "materials/materialSearch"] = ((int)model.materialSearch).ToString(CultureInfo.InvariantCulture);
      values[prefix + "materials/materialLocation"] = ((int)model.materialLocation).ToString(CultureInfo.InvariantCulture);
      values[prefix + "animations/bakeSimulation"] = UnityYamlBool(model.bakeSimulation);
      values[prefix + "animations/bakeIK"] = UnityYamlBool(model.bakeIK);
      values[prefix + "animations/resampleCurves"] = UnityYamlBool(model.resampleCurves);
      values[prefix + "animations/optimizeGameObjects"] = UnityYamlBool(model.optimizeGameObjects);
      values[prefix + "animations/removeConstantScaleCurves"] = UnityYamlBool(model.removeConstantScaleCurves);
      values[prefix + "animations/importAnimatedCustomProperties"] = UnityYamlBool(model.importAnimatedCustomProperties);
      values[prefix + "animations/importConstraints"] = UnityYamlBool(model.importConstraints);
      values[prefix + "animations/animationCompression"] = ((int)model.animationCompression).ToString(CultureInfo.InvariantCulture);
      values[prefix + "animations/animationRotationError"] = model.animationRotationError.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "animations/animationPositionError"] = model.animationPositionError.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "animations/animationScaleError"] = model.animationScaleError.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "animations/isReadable"] = UnityYamlBool(model.isReadable);
      values[prefix + "meshes/globalScale"] = model.globalScale.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "meshes/meshCompression"] = ((int)model.meshCompression).ToString(CultureInfo.InvariantCulture);
      values[prefix + "meshes/useSRGBMaterialColor"] = UnityYamlBool(model.useSRGBMaterialColor);
      values[prefix + "meshes/addColliders"] = UnityYamlBool(model.addCollider);
      values[prefix + "meshes/importVisibility"] = UnityYamlBool(model.importVisibility);
      values[prefix + "meshes/importBlendShapes"] = UnityYamlBool(model.importBlendShapes);
      values[prefix + "meshes/importCameras"] = UnityYamlBool(model.importCameras);
      values[prefix + "meshes/importLights"] = UnityYamlBool(model.importLights);
      values[prefix + "meshes/importPhysicalCameras"] = UnityYamlBool(model.importPhysicalCameras);
      values[prefix + "meshes/sortHierarchyByName"] = UnityYamlBool(model.sortHierarchyByName);
      values[prefix + "meshes/swapUVChannels"] = UnityYamlBool(model.swapUVChannels);
      values[prefix + "meshes/generateSecondaryUV"] = UnityYamlBool(model.generateSecondaryUV);
      values[prefix + "meshes/useFileUnits"] = UnityYamlBool(model.useFileUnits);
      values[prefix + "meshes/optimizeMeshForGPU"] = UnityYamlBool(model.optimizeMesh);
      values[prefix + "meshes/keepQuads"] = UnityYamlBool(model.keepQuads);
      values[prefix + "meshes/weldVertices"] = UnityYamlBool(model.weldVertices);
      values[prefix + "meshes/indexFormat"] = ((int)model.indexFormat).ToString(CultureInfo.InvariantCulture);
      values[prefix + "meshes/secondaryUVAngleDistortion"] = model.secondaryUVAngleDistortion.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "meshes/secondaryUVAreaDistortion"] = model.secondaryUVAreaDistortion.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "meshes/secondaryUVHardAngle"] = model.secondaryUVHardAngle.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "meshes/secondaryUVPackMargin"] = model.secondaryUVPackMargin.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "meshes/useFileScale"] = UnityYamlBool(model.useFileScale);
      values[prefix + "meshes/bakeAxisConversion"] = UnityYamlBool(model.bakeAxisConversion);
      values[prefix + "meshes/preserveHierarchy"] = UnityYamlBool(model.preserveHierarchy);
      values[prefix + "meshes/strictVertexDataChecks"] = UnityYamlBool(model.strictVertexDataChecks);
      values[prefix + "tangentSpace/normalImportMode"] = ((int)model.importNormals).ToString(CultureInfo.InvariantCulture);
      values[prefix + "tangentSpace/tangentImportMode"] = ((int)model.importTangents).ToString(CultureInfo.InvariantCulture);
      values[prefix + "importAnimation"] = UnityYamlBool(model.importAnimation);
      values[prefix + "importBlendShapeDeformPercent"] = UnityYamlBool(model.importBlendShapeDeformPercent);
      values[prefix + "avatarSetup"] = ((int)model.avatarSetup).ToString(CultureInfo.InvariantCulture);
      values[prefix + "autoGenerateAvatarMappingIfUnspecified"] = UnityYamlBool(model.autoGenerateAvatarMappingIfUnspecified);
      var humanDescription = model.humanDescription;
      values[prefix + "humanDescription/armTwist"] = humanDescription.upperArmTwist.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/foreArmTwist"] = humanDescription.lowerArmTwist.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/upperLegTwist"] = humanDescription.upperLegTwist.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/legTwist"] = humanDescription.lowerLegTwist.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/armStretch"] = humanDescription.armStretch.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/legStretch"] = humanDescription.legStretch.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/feetSpacing"] = humanDescription.feetSpacing.ToString("R", CultureInfo.InvariantCulture);
      values[prefix + "humanDescription/hasTranslationDoF"] = UnityYamlBool(humanDescription.hasTranslationDoF);
      values[prefix + "humanDescription/rootMotionBoneName"] = EncodeUnityYamlScalar(model.motionNodeName);
      values[prefix + "animationType"] = ((int)model.animationType).ToString(CultureInfo.InvariantCulture);
      values[prefix + "userData"] = EncodeUnityYamlScalar(importer.editorUserSettingsData);
    }
    else if (importer.GetType() == typeof(AssetImporter))
    {
      values["DefaultImporter/userData"] = EncodeUnityYamlScalar(importer.editorUserSettingsData);
    }

    if (values.Count > 0) ReplaceExistingUnityYamlScalars(lines, values);
    if (importer is TextureImporter textureWithPlatforms) WriteExistingUnityTexturePlatformSettings(lines, textureWithPlatforms);
    if (importer is ModelImporter modelWithClips)
    {
      WriteExistingUnityHumanDescriptionBones(lines, modelWithClips);
      WriteExistingUnityHumanDescriptionSkeleton(lines, modelWithClips);
      WriteExistingUnityModelMotionNodeName(lines, modelWithClips);
      WriteExistingUnityModelAvatarSource(lines, modelWithClips);
      WriteExistingUnityModelClipAnimations(lines, modelWithClips);
    }
  }

  private static void WriteExistingUnityHumanDescriptionBones(List<string> lines, ModelImporter model)
  {
    var bones = model.humanDescription.human ?? Array.Empty<HumanBone>();
    var humanLine = -1;
    var insertAt = -1;
    var inModelImporter = false;
    var inHumanDescription = false;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        inModelImporter = line == "ModelImporter:";
        inHumanDescription = false;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "humanDescription:")
      {
        inHumanDescription = true;
        insertAt = index + 1;
        continue;
      }
      if (!inHumanDescription) continue;
      if (indent == 4 && line.StartsWith("human:", StringComparison.Ordinal))
      {
        humanLine = index;
        insertAt = index + 1;
        break;
      }
      if (indent <= 2) break;
      insertAt = index + 1;
    }

    if (!inHumanDescription) return;
    if (humanLine < 0)
    {
      humanLine = Math.Max(0, insertAt);
      lines.Insert(humanLine, bones.Length == 0 ? "    human: []" : "    human:");
    }

    var ranges = FindUnityHumanBoneItemRanges(lines);
    if (bones.Length == 0)
    {
      for (var index = ranges.Count - 1; index >= 0; index--)
        lines.RemoveRange(ranges[index].Start, ranges[index].Length);
      lines[humanLine] = "    human: []";
      return;
    }

    lines[humanLine] = "    human:";
    var existingCount = Math.Min(ranges.Count, bones.Length);
    for (var boneIndex = 0; boneIndex < existingCount; boneIndex++)
      WriteExistingUnityHumanBone(lines, ranges[boneIndex], bones[boneIndex]);

    ranges = FindUnityHumanBoneItemRanges(lines);
    for (var index = ranges.Count - 1; index >= bones.Length; index--)
      lines.RemoveRange(ranges[index].Start, ranges[index].Length);

    ranges = FindUnityHumanBoneItemRanges(lines);
    if (bones.Length <= ranges.Count) return;
    var appendAt = ranges.Count > 0 ? ranges[^1].Start + ranges[^1].Length : humanLine + 1;
    var yaml = new List<string>();
    foreach (var bone in bones.Skip(ranges.Count)) AppendUnityHumanBone(yaml, bone);
    lines.InsertRange(appendAt, yaml);
  }

  private static void WriteExistingUnityHumanBone(List<string> lines, (int Start, int Length) range, HumanBone bone)
  {
    var end = range.Start + range.Length;
    var inLimit = false;
    for (var index = range.Start; index < end; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (index == range.Start)
      {
        lines[index] = "    - boneName: " + EncodeUnityYamlScalar(bone.boneName);
        continue;
      }
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var key = line[..colon].Trim();
      if (indent == 6 && key == "limit") { inLimit = true; continue; }
      if (indent == 6) inLimit = false;
      string? replacement = null;
      if (indent == 6 && key == "boneName") replacement = EncodeUnityYamlScalar(bone.boneName);
      else if (indent == 6 && key == "humanName") replacement = EncodeUnityYamlScalar(bone.humanName);
      else if (inLimit && indent >= 8)
      {
        replacement = key switch
        {
          "min" => EncodeUnityYamlVector3(bone.limit.min),
          "max" => EncodeUnityYamlVector3(bone.limit.max),
          "value" => EncodeUnityYamlVector3(bone.limit.center),
          "length" => bone.limit.axisLength.ToString("R", CultureInfo.InvariantCulture),
          "modified" => UnityYamlBool(!bone.limit.useDefaultValues),
          _ => null,
        };
      }
      if (replacement is not null)
      {
        var sourceColon = source.IndexOf(':');
        lines[index] = source[..(sourceColon + 1)] + " " + replacement;
      }
    }
  }

  private static void AppendUnityHumanBone(List<string> yaml, HumanBone bone)
  {
    yaml.Add("    - boneName: " + EncodeUnityYamlScalar(bone.boneName));
    yaml.Add("      humanName: " + EncodeUnityYamlScalar(bone.humanName));
    yaml.Add("      limit:");
    yaml.Add("        min: " + EncodeUnityYamlVector3(bone.limit.min));
    yaml.Add("        max: " + EncodeUnityYamlVector3(bone.limit.max));
    yaml.Add("        value: " + EncodeUnityYamlVector3(bone.limit.center));
    yaml.Add("        length: " + bone.limit.axisLength.ToString("R", CultureInfo.InvariantCulture));
    yaml.Add("        modified: " + UnityYamlBool(!bone.limit.useDefaultValues));
  }

  private static string EncodeUnityYamlVector3(Vector3 value) =>
    "{x: " + value.x.ToString("R", CultureInfo.InvariantCulture) +
    ", y: " + value.y.ToString("R", CultureInfo.InvariantCulture) +
    ", z: " + value.z.ToString("R", CultureInfo.InvariantCulture) + "}";

  private static List<(int Start, int Length)> FindUnityHumanBoneItemRanges(IReadOnlyList<string> lines)
  {
    var ranges = new List<(int Start, int Length)>();
    var inModelImporter = false;
    var inHumanDescription = false;
    var inHuman = false;
    var start = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        if (start >= 0) ranges.Add((start, index - start));
        inModelImporter = line == "ModelImporter:";
        inHumanDescription = false;
        inHuman = false;
        start = -1;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "humanDescription:") { inHumanDescription = true; continue; }
      if (!inHumanDescription) continue;
      if (indent == 4 && line.StartsWith("human:", StringComparison.Ordinal))
      {
        inHuman = !line.EndsWith("[]", StringComparison.Ordinal);
        continue;
      }
      if (!inHuman) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal))
      {
        if (start >= 0) ranges.Add((start, index - start));
        start = index;
        continue;
      }
      if (indent <= 4)
      {
        if (start >= 0) ranges.Add((start, index - start));
        start = -1;
        break;
      }
    }
    if (start >= 0 && (ranges.Count == 0 || ranges[^1].Start != start)) ranges.Add((start, lines.Count - start));
    return ranges;
  }

  private static void WriteExistingUnityHumanDescriptionSkeleton(List<string> lines, ModelImporter model)
  {
    var bones = model.humanDescription.skeleton ?? Array.Empty<SkeletonBone>();
    var descriptionLine = -1;
    var skeletonLine = -1;
    var descriptionEnd = -1;
    var inModelImporter = false;
    var inHumanDescription = false;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        if (inHumanDescription && descriptionEnd < 0) descriptionEnd = index;
        inModelImporter = line == "ModelImporter:";
        inHumanDescription = false;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "humanDescription:")
      {
        descriptionLine = index;
        inHumanDescription = true;
        continue;
      }
      if (!inHumanDescription) continue;
      if (indent <= 2)
      {
        descriptionEnd = index;
        inHumanDescription = false;
        continue;
      }
      if (indent == 4 && line.StartsWith("skeleton:", StringComparison.Ordinal)) skeletonLine = index;
    }
    if (descriptionLine < 0) return;
    if (descriptionEnd < 0) descriptionEnd = lines.Count;

    if (skeletonLine < 0)
    {
      var humanRanges = FindUnityHumanBoneItemRanges(lines);
      if (humanRanges.Count > 0)
      {
        skeletonLine = humanRanges[^1].Start + humanRanges[^1].Length;
      }
      else
      {
        skeletonLine = descriptionLine + 1;
        for (var index = descriptionLine + 1; index < descriptionEnd; index++)
        {
          var source = lines[index];
          var indent = source.Length - source.TrimStart(' ').Length;
          var line = source.Trim();
          if (indent == 4 && (line.StartsWith("serializedVersion:", StringComparison.Ordinal) || line.StartsWith("human:", StringComparison.Ordinal)))
            skeletonLine = index + 1;
        }
      }
      lines.Insert(skeletonLine, bones.Length == 0 ? "    skeleton: []" : "    skeleton:");
    }

    var ranges = FindUnitySkeletonBoneItemRanges(lines);
    if (bones.Length == 0)
    {
      for (var index = ranges.Count - 1; index >= 0; index--)
        lines.RemoveRange(ranges[index].Start, ranges[index].Length);
      lines[skeletonLine] = "    skeleton: []";
      return;
    }

    lines[skeletonLine] = "    skeleton:";
    var existingCount = Math.Min(ranges.Count, bones.Length);
    for (var boneIndex = existingCount - 1; boneIndex >= 0; boneIndex--)
      WriteExistingUnitySkeletonBone(lines, ranges[boneIndex], bones[boneIndex]);

    ranges = FindUnitySkeletonBoneItemRanges(lines);
    for (var index = ranges.Count - 1; index >= bones.Length; index--)
      lines.RemoveRange(ranges[index].Start, ranges[index].Length);

    ranges = FindUnitySkeletonBoneItemRanges(lines);
    if (bones.Length <= ranges.Count) return;
    var appendAt = ranges.Count > 0 ? ranges[^1].Start + ranges[^1].Length : skeletonLine + 1;
    var yaml = new List<string>();
    foreach (var bone in bones.Skip(ranges.Count)) AppendUnitySkeletonBone(yaml, bone);
    lines.InsertRange(appendAt, yaml);
  }

  private static void WriteExistingUnitySkeletonBone(List<string> lines, (int Start, int Length) range, SkeletonBone bone)
  {
    var end = range.Start + range.Length;
    var hasPosition = false;
    var hasRotation = false;
    var hasScale = false;
    lines[range.Start] = "    - name: " + EncodeUnityYamlScalar(bone.name);
    for (var index = range.Start + 1; index < end; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      var colon = line.IndexOf(':');
      if (indent != 6 || colon <= 0) continue;
      var key = line[..colon].Trim();
      string? replacement = null;
      if (key == "name") replacement = EncodeUnityYamlScalar(bone.name);
      else if (key == "position") { replacement = EncodeUnityYamlVector3(bone.position); hasPosition = true; }
      else if (key == "rotation") { replacement = EncodeUnityYamlQuaternion(bone.rotation); hasRotation = true; }
      else if (key == "scale") { replacement = EncodeUnityYamlVector3(bone.scale); hasScale = true; }
      if (replacement is not null)
      {
        var sourceColon = source.IndexOf(':');
        lines[index] = source[..(sourceColon + 1)] + " " + replacement;
      }
    }
    var missing = new List<string>();
    if (!hasPosition) missing.Add("      position: " + EncodeUnityYamlVector3(bone.position));
    if (!hasRotation) missing.Add("      rotation: " + EncodeUnityYamlQuaternion(bone.rotation));
    if (!hasScale) missing.Add("      scale: " + EncodeUnityYamlVector3(bone.scale));
    if (missing.Count > 0) lines.InsertRange(end, missing);
  }

  private static void AppendUnitySkeletonBone(List<string> yaml, SkeletonBone bone)
  {
    yaml.Add("    - name: " + EncodeUnityYamlScalar(bone.name));
    yaml.Add("      parentName: ");
    yaml.Add("      position: " + EncodeUnityYamlVector3(bone.position));
    yaml.Add("      rotation: " + EncodeUnityYamlQuaternion(bone.rotation));
    yaml.Add("      scale: " + EncodeUnityYamlVector3(bone.scale));
  }

  private static string EncodeUnityYamlQuaternion(Quaternion value) =>
    "{x: " + value.x.ToString("R", CultureInfo.InvariantCulture) +
    ", y: " + value.y.ToString("R", CultureInfo.InvariantCulture) +
    ", z: " + value.z.ToString("R", CultureInfo.InvariantCulture) +
    ", w: " + value.w.ToString("R", CultureInfo.InvariantCulture) + "}";

  private static List<(int Start, int Length)> FindUnitySkeletonBoneItemRanges(IReadOnlyList<string> lines)
  {
    var ranges = new List<(int Start, int Length)>();
    var inModelImporter = false;
    var inHumanDescription = false;
    var inSkeleton = false;
    var start = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        if (start >= 0) ranges.Add((start, index - start));
        inModelImporter = line == "ModelImporter:";
        inHumanDescription = false;
        inSkeleton = false;
        start = -1;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "humanDescription:") { inHumanDescription = true; continue; }
      if (!inHumanDescription) continue;
      if (indent == 4 && line.StartsWith("skeleton:", StringComparison.Ordinal))
      {
        inSkeleton = !line.EndsWith("[]", StringComparison.Ordinal);
        continue;
      }
      if (!inSkeleton) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal))
      {
        if (start >= 0) ranges.Add((start, index - start));
        start = index;
        continue;
      }
      if (indent <= 4)
      {
        if (start >= 0) ranges.Add((start, index - start));
        start = -1;
        break;
      }
    }
    if (start >= 0 && (ranges.Count == 0 || ranges[^1].Start != start)) ranges.Add((start, lines.Count - start));
    return ranges;
  }

  private static void WriteExistingUnityModelAvatarSource(List<string> lines, ModelImporter model)
  {
    var guid = GetModelSourceAvatarGuid(model);
    var replacement = string.IsNullOrEmpty(guid)
      ? "  lastHumanDescriptionAvatarSource: {instanceID: 0}"
      : "  lastHumanDescriptionAvatarSource: {fileID: 9000000, guid: " + guid + ", type: 3}";
    var inModelImporter = false;
    var sourceLine = -1;
    var sourceLength = 0;
    var insertAt = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        inModelImporter = line == "ModelImporter:";
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line.StartsWith("lastHumanDescriptionAvatarSource:", StringComparison.Ordinal))
      {
        sourceLine = index;
        sourceLength = 1;
        var reference = line[(line.IndexOf(':') + 1)..].Trim();
        while (!reference.Contains('}') && sourceLine + sourceLength < lines.Count)
        {
          var continuation = lines[sourceLine + sourceLength];
          var continuationIndent = continuation.Length - continuation.TrimStart(' ').Length;
          if (continuationIndent <= 2) break;
          reference += " " + continuation.Trim();
          sourceLength++;
        }
        break;
      }
      if (indent == 2 && (line.StartsWith("autoGenerateAvatarMappingIfUnspecified:", StringComparison.Ordinal) || line.StartsWith("animationType:", StringComparison.Ordinal)))
        insertAt = insertAt < 0 ? index : insertAt;
    }
    if (sourceLine >= 0)
    {
      lines.RemoveRange(sourceLine, sourceLength);
      lines.Insert(sourceLine, replacement);
      return;
    }
    if (insertAt < 0)
    {
      insertAt = lines.FindLastIndex(line => line.StartsWith("  assetBundleName:", StringComparison.Ordinal));
      if (insertAt < 0) insertAt = lines.Count;
    }
    lines.Insert(insertAt, replacement);
  }

  private static void WriteExistingUnityModelMotionNodeName(List<string> lines, ModelImporter model)
  {
    var inModelImporter = false;
    var inHumanDescription = false;
    var insertAt = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        inModelImporter = line == "ModelImporter:";
        inHumanDescription = false;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "humanDescription:") { inHumanDescription = true; continue; }
      if (!inHumanDescription) continue;
      if (indent <= 2)
      {
        if (insertAt < 0) insertAt = index;
        break;
      }
      if (indent == 4 && line.StartsWith("rootMotionBoneName:", StringComparison.Ordinal)) return;
      if (indent == 4 && (line.StartsWith("rootMotionBoneRotation:", StringComparison.Ordinal) || line.StartsWith("hasTranslationDoF:", StringComparison.Ordinal)))
      {
        insertAt = index;
        break;
      }
      insertAt = index + 1;
    }
    if (insertAt >= 0) lines.Insert(insertAt, "    rootMotionBoneName: " + EncodeUnityYamlScalar(model.motionNodeName));
  }

  private static string GetModelSourceAvatarGuid(ModelImporter model)
  {
    var avatar = model.sourceAvatar;
    if (avatar is not null)
    {
      var path = GetAssetPath(avatar);
      if (!string.IsNullOrEmpty(path)) return AssetPathToGUID(path);
    }
    return model.SourceAvatarGuid;
  }

  private static void WriteExistingUnityModelClipAnimations(List<string> lines, ModelImporter model)
  {
    var clips = model.clipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
    if (clips.Length == 0) return;
    var inModelImporter = false;
    var inAnimations = false;
    var inClips = false;
    var clipIndex = -1;
    var clipInsertAt = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        inModelImporter = line == "ModelImporter:";
        inAnimations = false;
        inClips = false;
        continue;
      }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "animations:") { inAnimations = true; continue; }
      if (!inAnimations) continue;
      if (indent == 4 && line.StartsWith("clipAnimations:", StringComparison.Ordinal))
      {
        if (line.EndsWith("[]", StringComparison.Ordinal) && clips.Length > 0) lines[index] = source[..(source.IndexOf(':') + 1)];
        inClips = !line.EndsWith("[]", StringComparison.Ordinal) || clips.Length > 0;
        clipInsertAt = index + 1;
        continue;
      }
      if (!inClips) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal)) { clipIndex++; continue; }
      if (indent <= 4) { inClips = false; clipInsertAt = index; continue; }
      if (clipIndex < 0 || clipIndex >= clips.Length) continue;
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var clip = clips[clipIndex];
      var replacement = line[..colon].Trim() switch
      {
        "name" => EncodeUnityYamlScalar(clip.name),
        "takeName" => EncodeUnityYamlScalar(clip.takeName),
        "firstFrame" => clip.firstFrame.ToString("R", CultureInfo.InvariantCulture),
        "lastFrame" => clip.lastFrame.ToString("R", CultureInfo.InvariantCulture),
        "loopTime" => UnityYamlBool(clip.loopTime),
        "loopPose" => UnityYamlBool(clip.loopPose),
        "lockRootRotation" => UnityYamlBool(clip.lockRootRotation),
        "lockRootHeightY" => UnityYamlBool(clip.lockRootHeightY),
        "lockRootPositionXZ" => UnityYamlBool(clip.lockRootPositionXZ),
        "mirror" => UnityYamlBool(clip.mirror),
        "wrapMode" => ((int)clip.wrapMode).ToString(CultureInfo.InvariantCulture),
        "cycleOffset" => clip.cycleOffset.ToString("R", CultureInfo.InvariantCulture),
        "keepOriginalOrientation" => UnityYamlBool(clip.keepOriginalOrientation),
        "keepOriginalPositionY" => UnityYamlBool(clip.keepOriginalPositionY),
        "keepOriginalPositionXZ" => UnityYamlBool(clip.keepOriginalPositionXZ),
        "heightFromFeet" => UnityYamlBool(clip.heightFromFeet),
        "hasAdditiveReferencePose" => UnityYamlBool(clip.hasAdditiveReferencePose),
        _ => null,
      };
      if (replacement is not null)
      {
        var sourceColon = source.IndexOf(':');
        lines[index] = source[..(sourceColon + 1)] + " " + replacement;
      }
    }
    if (inClips) clipInsertAt = lines.Count;
    var existingCount = clipIndex + 1;
    if (clipInsertAt >= 0 && clips.Length > existingCount)
    {
      var yaml = new List<string>();
      foreach (var clip in clips.Skip(existingCount))
      {
        yaml.Add("    - serializedVersion: 16");
        yaml.Add("      name: " + EncodeUnityYamlScalar(clip.name));
        yaml.Add("      takeName: " + EncodeUnityYamlScalar(clip.takeName));
        yaml.Add("      firstFrame: " + clip.firstFrame.ToString("R", CultureInfo.InvariantCulture));
        yaml.Add("      lastFrame: " + clip.lastFrame.ToString("R", CultureInfo.InvariantCulture));
        yaml.Add("      loopTime: " + UnityYamlBool(clip.loopTime));
        yaml.Add("      loopPose: " + UnityYamlBool(clip.loopPose));
        yaml.Add("      lockRootRotation: " + UnityYamlBool(clip.lockRootRotation));
        yaml.Add("      lockRootHeightY: " + UnityYamlBool(clip.lockRootHeightY));
        yaml.Add("      lockRootPositionXZ: " + UnityYamlBool(clip.lockRootPositionXZ));
        yaml.Add("      mirror: " + UnityYamlBool(clip.mirror));
        yaml.Add("      wrapMode: " + ((int)clip.wrapMode).ToString(CultureInfo.InvariantCulture));
        yaml.Add("      cycleOffset: " + clip.cycleOffset.ToString("R", CultureInfo.InvariantCulture));
        yaml.Add("      keepOriginalOrientation: " + UnityYamlBool(clip.keepOriginalOrientation));
        yaml.Add("      keepOriginalPositionY: " + UnityYamlBool(clip.keepOriginalPositionY));
        yaml.Add("      keepOriginalPositionXZ: " + UnityYamlBool(clip.keepOriginalPositionXZ));
        yaml.Add("      heightFromFeet: " + UnityYamlBool(clip.heightFromFeet));
        yaml.Add("      hasAdditiveReferencePose: " + UnityYamlBool(clip.hasAdditiveReferencePose));
      }
      lines.InsertRange(clipInsertAt, yaml);
    }
    var ranges = FindUnityModelClipItemRanges(lines);
    for (var index = ranges.Count - 1; index >= clips.Length; index--) lines.RemoveRange(ranges[index].Start, ranges[index].Length);
  }

  private static List<(int Start, int Length)> FindUnityModelClipItemRanges(IReadOnlyList<string> lines)
  {
    var ranges = new List<(int Start, int Length)>();
    var inModelImporter = false;
    var inAnimations = false;
    var inClips = false;
    var start = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal)) { inModelImporter = line == "ModelImporter:"; inAnimations = false; inClips = false; start = -1; continue; }
      if (!inModelImporter) continue;
      if (indent == 2 && line == "animations:") { inAnimations = true; continue; }
      if (!inAnimations) continue;
      if (indent == 4 && line.StartsWith("clipAnimations:", StringComparison.Ordinal)) { inClips = !line.EndsWith("[]", StringComparison.Ordinal); continue; }
      if (!inClips) continue;
      if (indent == 4 && line.StartsWith("- ", StringComparison.Ordinal))
      {
        if (start >= 0) ranges.Add((start, index - start));
        start = index;
        continue;
      }
      if (indent <= 4)
      {
        if (start >= 0) ranges.Add((start, index - start));
        break;
      }
    }
    if (start >= 0 && (ranges.Count == 0 || ranges[^1].Start != start)) ranges.Add((start, lines.Count - start));
    return ranges;
  }

  private static void WriteExistingUnityTexturePlatformSettings(List<string> lines, TextureImporter texture)
  {
    var configured = texture.GetConfiguredPlatformTextureSettings().ToDictionary(setting => setting.name, StringComparer.Ordinal);
    var inTextureImporter = false;
    var inPlatformSettings = false;
    TextureImporterPlatformSettings? current = null;
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var platformInsertAt = -1;
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
      {
        inTextureImporter = line == "TextureImporter:";
        inPlatformSettings = false;
        current = null;
        continue;
      }
      if (!inTextureImporter) continue;
      if (indent == 2 && line == "platformSettings:") { inPlatformSettings = true; platformInsertAt = index + 1; continue; }
      if (!inPlatformSettings) continue;
      if (indent == 2 && line.StartsWith("- ", StringComparison.Ordinal)) { current = null; continue; }
      if (indent <= 2) { inPlatformSettings = false; current = null; platformInsertAt = index; continue; }
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      var key = line[..colon].Trim();
      if (key == "buildTarget")
      {
        var target = DecodeUnityYamlScalar(line[(colon + 1)..].Trim());
        configured.TryGetValue(target, out current);
        seen.Add(target);
        continue;
      }
      if (current is null)
      {
        if (key == "overridden")
        {
          var sourceColon = source.IndexOf(':');
          lines[index] = source[..(sourceColon + 1)] + " 0";
        }
        continue;
      }
      var replacement = key switch
      {
        "maxTextureSize" => current.maxTextureSize.ToString(CultureInfo.InvariantCulture),
        "textureFormat" => ((int)current.format).ToString(CultureInfo.InvariantCulture),
        "textureCompression" => ((int)current.textureCompression).ToString(CultureInfo.InvariantCulture),
        "compressionQuality" => current.compressionQuality.ToString(CultureInfo.InvariantCulture),
        "crunchedCompression" => UnityYamlBool(current.crunchedCompression),
        "allowsAlphaSplitting" => UnityYamlBool(current.allowsAlphaSplitting),
        "overridden" => UnityYamlBool(current.overridden),
        "androidETC2FallbackOverride" => ((int)current.androidETC2FallbackOverride).ToString(CultureInfo.InvariantCulture),
        _ => null,
      };
      if (replacement is not null)
      {
        var sourceColon = source.IndexOf(':');
        lines[index] = source[..(sourceColon + 1)] + " " + replacement;
      }
    }
    if (platformInsertAt < 0 || configured.Count == 0) return;
    var additions = configured.Values.Where(setting => !seen.Contains(setting.name)).OrderBy(setting => setting.name, StringComparer.Ordinal).ToList();
    if (additions.Count == 0) return;
    var yaml = new List<string>();
    foreach (var setting in additions)
    {
      yaml.Add("  - serializedVersion: 3");
      yaml.Add("    buildTarget: " + setting.name);
      yaml.Add("    maxTextureSize: " + setting.maxTextureSize.ToString(CultureInfo.InvariantCulture));
      yaml.Add("    resizeAlgorithm: 0");
      yaml.Add("    textureFormat: " + ((int)setting.format).ToString(CultureInfo.InvariantCulture));
      yaml.Add("    textureCompression: " + ((int)setting.textureCompression).ToString(CultureInfo.InvariantCulture));
      yaml.Add("    compressionQuality: " + setting.compressionQuality.ToString(CultureInfo.InvariantCulture));
      yaml.Add("    crunchedCompression: " + UnityYamlBool(setting.crunchedCompression));
      yaml.Add("    allowsAlphaSplitting: " + UnityYamlBool(setting.allowsAlphaSplitting));
      yaml.Add("    overridden: " + UnityYamlBool(setting.overridden));
      yaml.Add("    androidETC2FallbackOverride: " + ((int)setting.androidETC2FallbackOverride).ToString(CultureInfo.InvariantCulture));
    }
    lines.InsertRange(platformInsertAt, yaml);
  }

  private static void ReplaceExistingUnityYamlScalars(List<string> lines, IReadOnlyDictionary<string, string> values)
  {
    var scopes = new Stack<(int Indent, string Path)>();
    for (var index = 0; index < lines.Count; index++)
    {
      var source = lines[index];
      if (string.IsNullOrWhiteSpace(source) || source.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
      var indent = source.Length - source.TrimStart(' ').Length;
      var line = source.Trim();
      if (line.StartsWith("- ", StringComparison.Ordinal)) line = line[2..];
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      while (scopes.Count > 0 && scopes.Peek().Indent >= indent) scopes.Pop();
      var key = line[..colon].Trim();
      var path = scopes.Count == 0 ? key : scopes.Peek().Path + "/" + key;
      var scalar = line[(colon + 1)..].Trim();
      if (values.TryGetValue(path, out var replacement))
      {
        var sourceColon = source.IndexOf(':');
        lines[index] = source[..(sourceColon + 1)] + " " + replacement;
        scalar = replacement;
      }
      if (scalar.Length == 0) scopes.Push((indent, path));
    }
  }

  private static string UnityYamlBool(bool value) => value ? "1" : "0";

  private static string EncodeUnityYamlScalar(string? value)
  {
    value ??= string.Empty;
    return value.Length == 0 || value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '/')
      ? value
      : JsonSerializer.Serialize(value);
  }

  private static Dictionary<string, string> ReadUnityYamlScalars(string content)
  {
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    var scopes = new Stack<(int Indent, string Path)>();
    foreach (var sourceLine in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
    {
      if (string.IsNullOrWhiteSpace(sourceLine) || sourceLine.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
      var indent = sourceLine.Length - sourceLine.TrimStart(' ').Length;
      var line = sourceLine.Trim();
      if (line.StartsWith("- ", StringComparison.Ordinal)) line = line[2..];
      var colon = line.IndexOf(':');
      if (colon <= 0) continue;
      while (scopes.Count > 0 && scopes.Peek().Indent >= indent) scopes.Pop();
      var key = line[..colon].Trim();
      var path = scopes.Count == 0 ? key : scopes.Peek().Path + "/" + key;
      var value = line[(colon + 1)..].Trim();
      if (value.Length == 0)
      {
        scopes.Push((indent, path));
      }
      else
      {
        values[path] = DecodeUnityYamlScalar(value);
      }
    }
    return values;
  }

  private static string DecodeUnityYamlScalar(string value)
  {
    if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'') return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
    if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
    {
      try { return JsonSerializer.Deserialize<string>(value) ?? string.Empty; }
      catch (JsonException) { }
    }
    return value;
  }

  private static bool TryGetYamlInt(IReadOnlyDictionary<string, string> values, string path, out int value)
  {
    value = default;
    return values.TryGetValue(path, out var scalar) && int.TryParse(scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
  }

  private static bool TryGetYamlFloat(IReadOnlyDictionary<string, string> values, string path, out float value)
  {
    value = default;
    return values.TryGetValue(path, out var scalar) && float.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
  }

  private static bool TryGetYamlBool(IReadOnlyDictionary<string, string> values, string path, out bool value)
  {
    value = false;
    return values.TryGetValue(path, out var scalar) && (scalar == "1" ? (value = true) == true : scalar == "0" || bool.TryParse(scalar, out value));
  }

  private static bool TryGetYamlEnum<T>(IReadOnlyDictionary<string, string> values, string path, out T value) where T : struct, Enum
  {
    if (TryGetYamlInt(values, path, out var numeric))
    {
      value = (T)Enum.ToObject(typeof(T), numeric);
      return true;
    }
    value = default;
    return false;
  }

  private static bool TryReadUnityYamlRootScalar(string content, string key, out string value)
  {
    var prefix = key + ":";
    foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
    {
      if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
      value = line[prefix.Length..].Trim();
      if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'') value = value[1..^1].Replace("''", "'", StringComparison.Ordinal);
      else if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
      {
        try { value = JsonSerializer.Deserialize<string>(value) ?? string.Empty; }
        catch (JsonException) { }
      }
      return true;
    }

    value = string.Empty;
    return false;
  }

  private sealed class PersistedImporterSettings
  {
    public string Kind { get; set; } = string.Empty;
    public string? EditorUserSettingsData { get; set; }
    public string? AssetBundleName { get; set; }
    public string? AssetBundleVariant { get; set; }
    public PersistedTextureImporterSettings? Texture { get; set; }
    public PersistedAudioImporterSettings? Audio { get; set; }
  }

  private sealed class PersistedTextureImporterSettings
  {
    public TextureImporterType TextureType { get; set; }
    public TextureImporterShape TextureShape { get; set; }
    public bool SrgbTexture { get; set; }
    public bool AlphaIsTransparency { get; set; }
    public bool MipmapEnabled { get; set; }
    public bool Readable { get; set; }
    public TextureImporterCompression Compression { get; set; }
    public int CompressionQuality { get; set; }
    public int MaxTextureSize { get; set; }
    public int AnisoLevel { get; set; }
    public FilterMode FilterMode { get; set; }
    public TextureWrapMode WrapMode { get; set; }
    public TextureWrapMode WrapModeU { get; set; }
    public TextureWrapMode WrapModeV { get; set; }
    public TextureWrapMode WrapModeW { get; set; }
  }

  private sealed class PersistedAudioImporterSettings
  {
    public bool LoadInBackground { get; set; }
    public bool PreloadAudioData { get; set; }
    public bool Ambisonic { get; set; }
    public bool ForceToMono { get; set; }
    public bool Normalize { get; set; }
    public AudioImporterSampleSettings DefaultSampleSettings { get; set; }
  }

  private static bool TryPersistUnityPackageAssets(IReadOnlyList<UnityPackageAsset> assets, out string? error)
  {
    error = null;
    var root = Path.GetFullPath(_projectRoot);
    var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? root : root + Path.DirectorySeparatorChar;
    var staging = Path.Combine(root, "Library", "AnityPackageStaging", Guid.NewGuid().ToString("N"));
    var writes = new List<(string Target, string Staged, string? Backup)>();

    try
    {
      foreach (var asset in assets)
      {
        var assetTarget = Path.GetFullPath(Path.Combine(root, asset.Path));
        var metaTarget = assetTarget + ".meta";
        if (!assetTarget.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
          throw new InvalidDataException("Package target escapes the configured project root.");
        }

        var assetStaged = Path.Combine(staging, asset.Path);
        var metaStaged = assetStaged + ".meta";
        Directory.CreateDirectory(Path.GetDirectoryName(assetStaged)!);
        File.WriteAllBytes(assetStaged, asset.AssetBytes);
        File.WriteAllBytes(metaStaged, asset.MetaBytes);
        writes.Add((assetTarget, assetStaged, null));
        writes.Add((metaTarget, metaStaged, null));
      }

      for (var index = 0; index < writes.Count; index++)
      {
        var write = writes[index];
        Directory.CreateDirectory(Path.GetDirectoryName(write.Target)!);
        string? backup = null;
        if (File.Exists(write.Target))
        {
          backup = Path.Combine(staging, "backup", index.ToString());
          Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
          File.Copy(write.Target, backup, overwrite: true);
        }
        File.Copy(write.Staged, write.Target, overwrite: true);
        writes[index] = (write.Target, write.Staged, backup);
      }

      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
    {
      for (var index = writes.Count - 1; index >= 0; index--)
      {
        var write = writes[index];
        try
        {
          if (write.Backup is not null) File.Copy(write.Backup, write.Target, overwrite: true);
          else if (File.Exists(write.Target)) File.Delete(write.Target);
        }
        catch { }
      }
      error = "Could not persist Unity package transaction: " + exception.Message;
      return false;
    }
    finally
    {
      try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
    }
  }

  private static Object CreateImportedAsset(UnityPackageAsset imported)
  {
    var extension = Path.GetExtension(imported.Path).ToLowerInvariant();
    var name = Path.GetFileNameWithoutExtension(imported.Path);
    if (extension is ".png" or ".jpg" or ".jpeg" or ".tga")
    {
      var texture = new Texture2D();
      if (texture.LoadImage(imported.AssetBytes))
      {
        texture.name = name;
        return texture;
      }
    }

    if (extension is ".wav" or ".mp3" or ".ogg" or ".aac" or ".m4a" or ".flac")
    {
      var diskPath = Path.Combine(_projectRoot, imported.Path);
      var clip = AudioClip.CreateFromFile(diskPath);
      if (clip is not null)
      {
        clip.name = name;
        return clip;
      }
    }

    if (extension is ".mp4" or ".webm" or ".mov")
    {
      var clip = UnityEngine.Video.VideoClip.CreateFromFile(Path.Combine(_projectRoot, imported.Path));
      if (clip is not null)
      {
        clip.name = name;
        return clip;
      }
    }

    if (extension == ".mat")
    {
      return new Material();
    }

    return new TextAsset(imported.AssetBytes) { name = name };
  }

  private static List<AssetPostprocessor> CreateAssetPostprocessors()
  {
    var types = new List<Type>();
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      try
      {
        types.AddRange(assembly.GetTypes().Where(type =>
          !type.IsAbstract && typeof(AssetPostprocessor).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null));
      }
      catch (System.Reflection.ReflectionTypeLoadException exception)
      {
        types.AddRange(exception.Types.Where(type => type is not null && !type.IsAbstract &&
          typeof(AssetPostprocessor).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null)!);
      }
    }

    return types.Distinct().Select(type => (AssetPostprocessor)Activator.CreateInstance(type)!)
      .OrderBy(processor => processor.GetPostprocessOrder())
      .ThenBy(processor => processor.GetType().FullName, StringComparer.Ordinal)
      .ToList();
  }

  private static void PrepareAssetPostprocessors(
    IEnumerable<AssetPostprocessor> postprocessors,
    string assetPath,
    UnityEngine.Object mainObject)
  {
    _ = EnsureImporterForPath(assetPath);
    var context = AssetImportContext.Create(assetPath, mainObject);
    foreach (var postprocessor in postprocessors)
    {
      postprocessor.assetPath = assetPath;
      postprocessor.context = context;
    }
  }

  private static void InvokeAssetPostprocessor(AssetPostprocessor postprocessor, string callbackName)
  {
    var callback = FindAssetPostprocessorCallback(postprocessor.GetType(), callbackName, false, Type.EmptyTypes);
    if (callback is null) return;
    InvokeAssetPostprocessorMethod(callback, postprocessor, Array.Empty<object>());
  }

  private static void InvokeAssetPostprocessor(
    AssetPostprocessor postprocessor, string callbackName, Type[] parameterTypes, object[] arguments)
  {
    var callback = FindAssetPostprocessorCallback(postprocessor.GetType(), callbackName, false, parameterTypes);
    if (callback is null) return;
    InvokeAssetPostprocessorMethod(callback, postprocessor, arguments);
  }

  private static void InvokePostprocessAllAssets(
    IReadOnlyList<AssetPostprocessor> postprocessors,
    string[] importedAssets,
    string[] deletedAssets,
    string[] movedAssets,
    string[] movedFromAssetPaths,
    bool didDomainReload,
    string errorPrefix)
  {
    var fourArgumentTypes = new[] { typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[]) };
    var fiveArgumentTypes = new[] { typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[]), typeof(bool) };
    var fourArguments = new object[] { importedAssets, deletedAssets, movedAssets, movedFromAssetPaths };
    var fiveArguments = new object[] { importedAssets, deletedAssets, movedAssets, movedFromAssetPaths, didDomainReload };

    // Unity explicitly excludes OnPostprocessAllAssets from
    // GetPostprocessOrder. Keep its otherwise unspecified order deterministic;
    // callback dependency attributes are handled separately from import order.
    foreach (var postprocessor in postprocessors.OrderBy(
      processor => processor.GetType().FullName, StringComparer.Ordinal))
    {
      var type = postprocessor.GetType();
      try
      {
        var fiveArgumentCallback = FindAssetPostprocessorCallback(type, "OnPostprocessAllAssets", true, fiveArgumentTypes);
        if (fiveArgumentCallback is not null) InvokeAssetPostprocessorMethod(fiveArgumentCallback, null, fiveArguments);

        var fourArgumentCallback = FindAssetPostprocessorCallback(type, "OnPostprocessAllAssets", true, fourArgumentTypes);
        if (fourArgumentCallback is not null) InvokeAssetPostprocessorMethod(fourArgumentCallback, null, fourArguments);
      }
      catch (Exception exception)
      {
        Debug.LogError(errorPrefix + exception.Message);
      }
    }
  }

  private static MethodInfo? FindAssetPostprocessorCallback(Type processorType, string callbackName, bool isStatic, Type[] parameterTypes)
  {
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
      (isStatic ? BindingFlags.Static : BindingFlags.Instance);
    for (var type = processorType; type is not null && type != typeof(AssetPostprocessor); type = type.BaseType)
    {
      var callback = type.GetMethods(flags).FirstOrDefault(method =>
        method.Name == callbackName && method.ReturnType == typeof(void) &&
        ParametersMatch(method.GetParameters(), parameterTypes));
      if (callback is not null) return callback;
    }
    return null;
  }

  private static bool ParametersMatch(ParameterInfo[] parameters, Type[] parameterTypes)
  {
    if (parameters.Length != parameterTypes.Length) return false;
    for (var index = 0; index < parameters.Length; index++)
      if (parameters[index].ParameterType != parameterTypes[index]) return false;
    return true;
  }

  private static void InvokeAssetPostprocessorMethod(MethodInfo callback, object? target, object[] arguments)
  {
    try
    {
      callback.Invoke(target, arguments);
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
      ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
      throw;
    }
  }

  private sealed class UnityPackageAsset
  {
    public UnityPackageAsset(string guid, string path, byte[] assetBytes, byte[] metaBytes)
    {
      Guid = guid;
      Path = path;
      AssetBytes = assetBytes;
      MetaBytes = metaBytes;
    }

    public string Guid { get; }
    public string Path { get; }
    public byte[] AssetBytes { get; }
    public byte[] MetaBytes { get; }
  }

  private static bool TryReadUnityPackage(string packagePath, out List<UnityPackageAsset> assets, out string? error)
  {
    assets = new List<UnityPackageAsset>();
    error = null;

    using var source = File.OpenRead(packagePath);
    if (source.Length < 2 || source.ReadByte() != 0x1f || source.ReadByte() != 0x8b)
    {
      return false;
    }

    source.Position = 0;

    try
    {
      using var gzip = new GZipStream(source, CompressionMode.Decompress);
      var entries = ReadTarEntries(gzip);
      var groups = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.OrdinalIgnoreCase);
      foreach (var entry in entries)
      {
        var segments = entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || (segments[1] != "asset" && segments[1] != "asset.meta" && segments[1] != "pathname"))
        {
          continue;
        }

        if (!groups.TryGetValue(segments[0], out var group))
        {
          group = new Dictionary<string, byte[]>(StringComparer.Ordinal);
          groups[segments[0]] = group;
        }

        group[segments[1]] = entry.Data;
      }

      foreach (var (guid, group) in groups)
      {
        if (!group.TryGetValue("pathname", out var pathname) || !group.TryGetValue("asset", out var asset))
        {
          continue;
        }

        var assetPath = Normalize(Encoding.UTF8.GetString(pathname).Trim('\0', '\r', '\n', ' '));
        if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.Contains("../", StringComparison.Ordinal))
        {
          error = $"Package contains an unsafe asset path: {assetPath}";
          assets.Clear();
          return false;
        }

        assets.Add(new UnityPackageAsset(
          guid,
          assetPath,
          asset,
          group.TryGetValue("asset.meta", out var meta) ? meta : Array.Empty<byte>()));
      }

      if (assets.Count == 0)
      {
        error = "Unity package contains no importable asset/pathname entries.";
        return false;
      }

      return true;
    }
    catch (InvalidDataException exception)
    {
      error = "Invalid Unity package archive: " + exception.Message;
      return false;
    }
    catch (EndOfStreamException exception)
    {
      error = "Truncated Unity package archive: " + exception.Message;
      return false;
    }
  }

  private static void WriteTarEntry(Stream stream, string name, byte[] data)
  {
    var nameBytes = Encoding.UTF8.GetBytes(name);
    if (nameBytes.Length == 0 || nameBytes.Length > 100)
    {
      throw new InvalidDataException("Unity package tar entry name is invalid or too long.");
    }

    var header = new byte[512];
    WriteTarString(header, 0, 100, name);
    WriteTarOctal(header, 100, 8, 511);
    WriteTarOctal(header, 108, 8, 0);
    WriteTarOctal(header, 116, 8, 0);
    WriteTarOctal(header, 124, 12, data.Length);
    WriteTarOctal(header, 136, 12, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    for (var index = 148; index < 156; index++) header[index] = (byte)' ';
    header[156] = (byte)'0';
    WriteTarString(header, 257, 6, "ustar");
    WriteTarString(header, 263, 2, "00");
    var checksum = header.Sum(value => (int)value);
    WriteTarString(header, 148, 6, Convert.ToString(checksum, 8).PadLeft(6, '0'));
    header[154] = 0;
    header[155] = (byte)' ';

    stream.Write(header, 0, header.Length);
    stream.Write(data, 0, data.Length);
    var padding = (512 - data.Length % 512) % 512;
    if (padding > 0) stream.Write(new byte[padding], 0, padding);
  }

  private static void WriteTarString(byte[] target, int offset, int length, string value)
  {
    var bytes = Encoding.UTF8.GetBytes(value);
    if (bytes.Length > length)
    {
      throw new InvalidDataException("Tar field value is too long.");
    }
    Array.Copy(bytes, 0, target, offset, bytes.Length);
  }

  private static void WriteTarOctal(byte[] target, int offset, int length, long value)
  {
    if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    var octal = Convert.ToString(value, 8);
    if (octal.Length > length - 1) throw new InvalidDataException("Tar numeric field is too large.");
    WriteTarString(target, offset, length, octal.PadLeft(length - 1, '0'));
  }

  private static List<(string Name, byte[] Data)> ReadTarEntries(Stream stream)
  {
    var entries = new List<(string Name, byte[] Data)>();
    var header = new byte[512];
    while (TryReadExactly(stream, header, 0, header.Length))
    {
      if (header.All(value => value == 0))
      {
        break;
      }

      var name = ReadTarString(header, 0, 100);
      var prefix = ReadTarString(header, 345, 155);
      if (!string.IsNullOrEmpty(prefix))
      {
        name = prefix + "/" + name;
      }

      var size = ReadTarOctal(header, 124, 12);
      if (size < 0 || size > int.MaxValue)
      {
        throw new InvalidDataException("Tar entry size is invalid.");
      }

      var data = new byte[(int)size];
      if (!TryReadExactly(stream, data, 0, data.Length))
      {
        throw new EndOfStreamException("Tar entry data is incomplete.");
      }

      var padding = (512 - (size % 512)) % 512;
      if (padding > 0 && !TryReadExactly(stream, new byte[padding], 0, (int)padding))
      {
        throw new EndOfStreamException("Tar entry padding is incomplete.");
      }

      if (header[156] is 0 or (byte)'0')
      {
        entries.Add((name, data));
      }
    }

    return entries;
  }

  private static string ReadTarString(byte[] bytes, int offset, int length)
  {
    var end = offset;
    var limit = offset + length;
    while (end < limit && bytes[end] != 0)
    {
      end++;
    }
    return Encoding.UTF8.GetString(bytes, offset, end - offset);
  }

  private static long ReadTarOctal(byte[] bytes, int offset, int length)
  {
    long value = 0;
    var end = offset + length;
    for (var i = offset; i < end && bytes[i] != 0; i++)
    {
      if (bytes[i] is (byte)' ' or (byte)'\0')
      {
        continue;
      }
      if (bytes[i] < (byte)'0' || bytes[i] > (byte)'7')
      {
        throw new InvalidDataException("Tar size uses a non-octal character.");
      }
      value = checked(value * 8 + bytes[i] - (byte)'0');
    }
    return value;
  }

  private static bool TryReadExactly(Stream stream, byte[] buffer, int offset, int length)
  {
    while (length > 0)
    {
      var read = stream.Read(buffer, offset, length);
      if (read == 0)
      {
        return false;
      }
      offset += read;
      length -= read;
    }
    return true;
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
  DontDownloadFromCacheServer = 8,
  DontImportAssets = 16,
  ForceUncompressedImport = 32
}

public enum ForceReserializeAssetsOptions
{
  ReserializeAssets = 1,
  ReserializeMetadata = 2,
  ReserializeAssetsAndMetadata = 3
}
