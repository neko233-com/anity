using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.AssetImporters;

public class AssetImportContext
{
  private readonly Dictionary<string, (UnityEngine.Object Asset, Texture2D Thumbnail)> _objects = new(StringComparer.Ordinal);
  private readonly HashSet<string> _sourceDependencies = new(StringComparer.Ordinal);
  private readonly BuildTarget _selectedBuildTarget = EditorUserBuildSettings.activeBuildTarget;
  private UnityEngine.Object _mainObject;

  private AssetImportContext()
  {
  }

  public string assetPath { get; internal set; }

  public UnityEngine.Object mainObject => _mainObject;

  public BuildTarget selectedBuildTarget => _selectedBuildTarget;

  public void AddObjectToAsset(string identifier, UnityEngine.Object obj)
  {
    AddObjectToAsset(identifier, obj, null);
  }

  public void AddObjectToAsset(string identifier, UnityEngine.Object obj, Texture2D thumbnail)
  {
    if (string.IsNullOrEmpty(identifier)) throw new ArgumentException("Identifier must not be null or empty.", nameof(identifier));
    if (obj is null) throw new ArgumentNullException(nameof(obj));
    if (_objects.ContainsKey(identifier)) throw new ArgumentException($"An asset with identifier '{identifier}' has already been added.", nameof(identifier));
    _objects.Add(identifier, (obj, thumbnail));
  }

  public void DependsOnSourceAsset(string path)
  {
    if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be null or empty.", nameof(path));
    _sourceDependencies.Add(path.Replace('\\', '/'));
  }

  public void GetObjects(List<UnityEngine.Object> objects)
  {
    if (objects is null) throw new ArgumentNullException(nameof(objects));
    objects.Clear();
    foreach (var entry in _objects.Values) objects.Add(entry.Asset);
  }

  public void LogImportError(string msg, UnityEngine.Object obj = null) => Debug.LogError((object)msg, obj);

  public void LogImportWarning(string msg, UnityEngine.Object obj = null) => Debug.LogWarning((object)msg, obj);

  public void SetMainObject(UnityEngine.Object obj)
  {
    if (obj is null) throw new ArgumentNullException(nameof(obj));
    if (!_objects.Values.Any(entry => ReferenceEquals(entry.Asset, obj)))
      throw new InvalidOperationException("The main object must first be added with AddObjectToAsset.");
    _mainObject = obj;
  }

  internal static AssetImportContext Create(string path, UnityEngine.Object mainObject = null)
  {
    var context = new AssetImportContext { assetPath = path };
    if (mainObject is not null)
    {
      context._objects.Add("main", (mainObject, null));
      context._mainObject = mainObject;
    }
    return context;
  }
}
