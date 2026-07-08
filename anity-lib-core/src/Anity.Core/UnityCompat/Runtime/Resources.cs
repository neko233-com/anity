using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public static class Resources
{
  private static readonly Dictionary<string, object> _resources = new(StringComparer.Ordinal);

  public static void RegisterAsset(string key, object value)
  {
    _resources[key] = value;
  }

  public static T? Load<T>(string path) where T : class
  {
    return Load(path, typeof(T)) as T;
  }

  public static Object? Load(string path)
  {
    return Load(path, typeof(Object));
  }

  public static Object? Load(string path, Type type)
  {
    if (string.IsNullOrEmpty(path) || !_resources.TryGetValue(path, out var value))
    {
      return null;
    }

    if (value is null)
    {
      return null;
    }

    if (type is null)
    {
      return value as Object;
    }

    return type.IsAssignableFrom(value.GetType()) ? value as Object : null;
  }

  public static T[] LoadAll<T>(string path) where T : class
  {
    var prefix = path?.Trim('/') ?? string.Empty;

    IEnumerable<T> items = _resources
      .Where(kv => string.IsNullOrEmpty(prefix) || kv.Key.StartsWith(prefix + "/", StringComparison.Ordinal) || kv.Key.Equals(prefix, StringComparison.Ordinal))
      .Select(kv => kv.Value)
      .OfType<T>();

    return items.ToArray();
  }

  public static Object[] LoadAll(string path, Type type)
  {
    var prefix = path?.Trim('/') ?? string.Empty;
    return _resources
      .Where(kv => string.IsNullOrEmpty(prefix) || kv.Key.StartsWith(prefix + "/", StringComparison.Ordinal) || kv.Key.Equals(prefix, StringComparison.Ordinal))
      .Select(kv => kv.Value)
      .Where(value => type is null || type.IsAssignableFrom(value.GetType()))
      .OfType<Object>()
      .ToArray();
  }

  public static T? GetBuiltinResource<T>(string path) where T : class
  {
    return Load<T>(path);
  }

  public static Object? GetBuiltinResource(string path, Type type)
  {
    return Load(path, type);
  }

  public static Object[] FindObjectsOfTypeAll(Type type)
  {
    return _resources.Values
      .Where(value => type is null || type.IsAssignableFrom(value.GetType()))
      .OfType<Object>()
      .ToArray();
  }

  public static T[] FindObjectsOfTypeAll<T>() where T : class
  {
    return _resources.Values.OfType<T>().ToArray();
  }

  public static void UnloadUnusedAssets() {}

  public static void UnloadAsset(Object? asset)
  {
    if (asset is null)
    {
      return;
    }

    var keys = _resources
      .Where(kv => ReferenceEquals(kv.Value, asset))
      .Select(kv => kv.Key)
      .ToArray();

    foreach (var key in keys)
    {
      _ = _resources.Remove(key);
    }
  }

  public static void Clear()
  {
    _resources.Clear();
  }

  public static IReadOnlyDictionary<string, object> AllLoaded => _resources;
}
