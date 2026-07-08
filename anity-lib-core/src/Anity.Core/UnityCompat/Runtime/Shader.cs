using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Shader
{
  private static readonly Dictionary<string, Shader> _cache = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

  public string name { get; }
  public int renderQueue { get; set; }

  private Shader(string name)
  {
    this.name = name;
  }

  public static Shader? Find(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return null;
    }

    if (_cache.TryGetValue(name, out var cached))
    {
      return cached;
    }

    cached = new Shader(name);
    _cache[name] = cached;
    return cached;
  }

  public object? GetProperty(string name) => _properties.GetValueOrDefault(name);

  public void SetProperty(string name, object value)
  {
    _properties[name] = value;
  }
}

