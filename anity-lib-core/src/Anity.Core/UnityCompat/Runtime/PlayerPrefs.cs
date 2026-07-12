using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityEngine;

public enum PlayerPrefsKeyType
{
  String,
  Int,
  Float
}

public static class PlayerPrefs
{
  private static readonly Dictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
  private static readonly string _savePath;
  private static readonly JsonSerializerOptions _jsonOptions = new()
  {
    WriteIndented = true,
    IncludeFields = true
  };

  static PlayerPrefs()
  {
    try
    {
      _savePath = Path.Combine(Application.persistentDataPath, "PlayerPrefs.json");
      Load();
    }
    catch
    {
      _savePath = "PlayerPrefs.json";
    }
  }

  private static void Load()
  {
    try
    {
      if (File.Exists(_savePath))
      {
        string json = File.ReadAllText(_savePath);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions);
        if (loaded != null)
        {
          foreach (var kvp in loaded)
          {
            _data[kvp.Key] = DeserializeElement(kvp.Value);
          }
        }
      }
    }
    catch { }
  }

  private static object DeserializeElement(JsonElement element)
  {
    return element.ValueKind switch
    {
      JsonValueKind.String => element.GetString() ?? string.Empty,
      JsonValueKind.Number when element.TryGetInt32(out int intVal) => intVal,
      JsonValueKind.Number => element.GetSingle(),
      JsonValueKind.True => 1,
      JsonValueKind.False => 0,
      _ => string.Empty
    };
  }

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

  public static void SetBool(string key, bool value)
  {
    SetInt(key, value ? 1 : 0);
  }

  public static bool GetBool(string key, bool defaultValue = false)
  {
    return GetInt(key, defaultValue ? 1 : 0) != 0;
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
    try
    {
      string? dir = Path.GetDirectoryName(_savePath);
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
      {
        Directory.CreateDirectory(dir);
      }
      string json = JsonSerializer.Serialize(_data, _jsonOptions);
      File.WriteAllText(_savePath, json);
    }
    catch { }
  }
}

public sealed class PlayerPrefsException : Exception
{
  public PlayerPrefsException() {}
  public PlayerPrefsException(string message) : base(message) {}
  public PlayerPrefsException(string message, Exception innerException) : base(message, innerException) {}
}
