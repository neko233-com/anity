using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public static class Resources
{
  private static readonly Dictionary<string, object> _resources = new(StringComparer.Ordinal);

  public static void RegisterResource(string path, object asset)
  {
    if (string.IsNullOrEmpty(path) || asset == null)
      return;
    _resources[path] = asset;
  }

  public static void RegisterAsset(string key, object value)
  {
    _resources[key] = value;
  }

  public static void RegisterAsset<T>(string path, T asset) where T : Object
  {
    _resources[path] = asset;
  }

  public static T? Load<T>(string path) where T : Object
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

  public static T[] LoadAll<T>() where T : Object
  {
    return LoadAll<T>(string.Empty);
  }

  public static Object[] LoadAll()
  {
    return LoadAll(string.Empty, typeof(Object));
  }

  public static T[] LoadAll<T>(string path) where T : Object
  {
    var prefix = path?.Trim('/') ?? string.Empty;

    IEnumerable<T> items = _resources
      .Where(kv => string.IsNullOrEmpty(prefix) || kv.Key.StartsWith(prefix + "/", StringComparison.Ordinal) || kv.Key.Equals(prefix, StringComparison.Ordinal))
      .Select(kv => kv.Value)
      .OfType<T>();

    return items.ToArray();
  }

  public static Object[] LoadAll(string path)
  {
    return LoadAll(path, typeof(Object));
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

  public static Object[] LoadAll(Type type)
  {
    return LoadAll(string.Empty, type);
  }

  public static T? GetBuiltinResource<T>(string path) where T : Object
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

  public static T[] FindObjectsOfTypeAll<T>() where T : Object
  {
    return _resources.Values.OfType<T>().ToArray();
  }

  public static AsyncOperation UnloadUnusedAssets()
  {
    var op = new AsyncOperation(false);
    op.isDone = true;
    op.progress = 1f;
    return op;
  }

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

  public static ResourceRequest LoadAsync<T>(string path) where T : Object
  {
    var request = new ResourceRequest();
    request.Configure(path, typeof(T));
    return request;
  }

  public static ResourceRequest LoadAsync(string path, Type type)
  {
    var request = new ResourceRequest();
    request.Configure(path, type);
    return request;
  }

  public static AsyncOperation UnloadUnusedAssetsAsync()
  {
    var op = new AsyncOperation(true);
    op.operationName = "UnloadUnusedAssets";
    return op;
  }

  public static void Clear()
  {
    _resources.Clear();
  }

  public static IReadOnlyDictionary<string, object> AllLoaded => _resources;
}

[Scripting.RequiredByNativeCode]
public class ResourceRequest : AsyncOperation
{
  internal string m_Path = string.Empty;
  internal Type m_Type = typeof(Object);

  public Object? asset => GetResult();

  public ResourceRequest() : base(false)
  {
    operationName = "ResourceRequest";
  }

  protected virtual Object? GetResult()
  {
    return Resources.Load(m_Path, m_Type);
  }

  internal void Configure(string path, Type type)
  {
    m_Path = path;
    m_Type = type;
    operationName = $"LoadAsync({path})";
    Schedule(static () => { });
  }
}
