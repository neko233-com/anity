using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class PlayerPrefs
{
  private static readonly Dictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);

  public static void SetInt(string key, int value)
  {
    _data[key] = value;
  }

  public static int GetInt(string key, int defaultValue = 0)
  {
    if (_data.TryGetValue(key, out var value))
    {
      if (value is int intVal) return intVal;
      if (value is float floatVal) return (int)floatVal;
      if (value is string strVal && int.TryParse(strVal, out var parsed)) return parsed;
    }
    return defaultValue;
  }

  public static void SetFloat(string key, float value)
  {
    _data[key] = value;
  }

  public static float GetFloat(string key, float defaultValue = 0f)
  {
    if (_data.TryGetValue(key, out var value))
    {
      if (value is float floatVal) return floatVal;
      if (value is int intVal) return intVal;
      if (value is string strVal && float.TryParse(strVal, out var parsed)) return parsed;
    }
    return defaultValue;
  }

  public static void SetString(string key, string value)
  {
    _data[key] = value ?? string.Empty;
  }

  public static string GetString(string key, string defaultValue = "")
  {
    if (_data.TryGetValue(key, out var value))
    {
      if (value is string strVal) return strVal;
      return value?.ToString() ?? defaultValue;
    }
    return defaultValue;
  }

  public static bool HasKey(string key)
  {
    return _data.ContainsKey(key);
  }

  public static void DeleteKey(string key)
  {
    _ = _data.Remove(key);
  }

  public static void DeleteAll()
  {
    _data.Clear();
  }

  public static void Save()
  {
  }
}

public sealed class PlayerPrefsException : Exception
{
  public PlayerPrefsException() {}
  public PlayerPrefsException(string message) : base(message) {}
}
