using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Material : Object
{
  private readonly Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);

  public string name { get; set; }
  public Shader? shader { get; set; }
  public int renderQueue { get; set; }

  public string[] shaderKeywords
  {
    get => _keywords.ToArray();
    set
    {
      _keywords.Clear();
      if (value != null)
      {
        foreach (var keyword in value)
        {
          _keywords.Add(keyword);
        }
      }
    }
  }

  public Material(Shader? shader = null)
  {
    this.shader = shader;
    name = shader?.name ?? "Unnamed";
  }

  public Material(string shaderName) : this(Shader.Find(shaderName))
  {
  }

  public float GetFloat(string propertyName)
  {
    if (_properties.TryGetValue(propertyName, out var value) && value is float f)
    {
      return f;
    }

    return 0f;
  }

  public void SetFloat(string propertyName, float value)
  {
    _properties[propertyName] = value;
  }

  public Color GetColor(string propertyName)
  {
    if (_properties.TryGetValue(propertyName, out var value) && value is Color c)
    {
      return c;
    }

    return default;
  }

  public void SetColor(string propertyName, Color value)
  {
    _properties[propertyName] = value;
  }

  public object? GetTexture(string propertyName)
  {
    return _properties.GetValueOrDefault(propertyName);
  }

  public void SetTexture(string propertyName, object texture)
  {
    _properties[propertyName] = texture;
  }

  public int GetInt(string propertyName)
  {
    if (_properties.TryGetValue(propertyName, out var value) && value is int i)
    {
      return i;
    }

    return 0;
  }

  public void SetInt(string propertyName, int value)
  {
    _properties[propertyName] = value;
  }

  public Vector4 GetVector(string propertyName)
  {
    if (_properties.TryGetValue(propertyName, out var value) && value is Vector4 v)
    {
      return v;
    }

    return default;
  }

  public void SetVector(string propertyName, Vector4 value)
  {
    _properties[propertyName] = value;
  }

  public Matrix4x4 GetMatrix(string propertyName)
  {
    if (_properties.TryGetValue(propertyName, out var value) && value is Matrix4x4 m)
    {
      return m;
    }

    return default;
  }

  public void SetMatrix(string propertyName, Matrix4x4 value)
  {
    _properties[propertyName] = value;
  }

  public void EnableKeyword(string keyword)
  {
    _keywords.Add(keyword);
  }

  public void DisableKeyword(string keyword)
  {
    _keywords.Remove(keyword);
  }

  public bool IsKeywordEnabled(string keyword)
  {
    return _keywords.Contains(keyword);
  }

  public bool HasProperty(string propertyName)
  {
    return _properties.ContainsKey(propertyName);
  }
}

