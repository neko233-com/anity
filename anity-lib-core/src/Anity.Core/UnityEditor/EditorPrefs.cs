using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityEditor;

public static class EditorPrefs
{
  private static readonly Dictionary<string, int> _intPrefs = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, float> _floatPrefs = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, string> _stringPrefs = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, bool> _boolPrefs = new(StringComparer.OrdinalIgnoreCase);

  public static void SetInt(string key, int value) => _intPrefs[key] = value;
  public static int GetInt(string key, int defaultValue = 0)
  {
    return _intPrefs.TryGetValue(key, out var value) ? value : defaultValue;
  }

  public static void SetFloat(string key, float value) => _floatPrefs[key] = value;
  public static float GetFloat(string key, float defaultValue = 0f)
  {
    return _floatPrefs.TryGetValue(key, out var value) ? value : defaultValue;
  }

  public static void SetString(string key, string value)
  {
    _stringPrefs[key] = value;
  }

  public static string GetString(string key, string defaultValue = "")
  {
    return _stringPrefs.TryGetValue(key, out var value) ? value : defaultValue;
  }

  public static void SetBool(string key, bool value) => _boolPrefs[key] = value;
  public static bool GetBool(string key, bool defaultValue = false)
  {
    return _boolPrefs.TryGetValue(key, out var value) ? value : defaultValue;
  }

  public static bool HasKey(string key)
  {
    return _intPrefs.ContainsKey(key) || _floatPrefs.ContainsKey(key) || _stringPrefs.ContainsKey(key) || _boolPrefs.ContainsKey(key);
  }

  public static void DeleteKey(string key)
  {
    _intPrefs.Remove(key);
    _floatPrefs.Remove(key);
    _stringPrefs.Remove(key);
    _boolPrefs.Remove(key);
  }

  public static void DeleteAll()
  {
    _intPrefs.Clear();
    _floatPrefs.Clear();
    _stringPrefs.Clear();
    _boolPrefs.Clear();
  }

  public static void Save()
  {
    try
    {
      var tempPath = Path.GetTempPath();
      var prefsPath = Path.Combine(tempPath, "anity-editorprefs.json");
      var data = new
      {
        IntPrefs = _intPrefs,
        FloatPrefs = _floatPrefs,
        StringPrefs = _stringPrefs,
        BoolPrefs = _boolPrefs
      };
      var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
      File.WriteAllText(prefsPath, json);
    }
    catch { }
  }
}

