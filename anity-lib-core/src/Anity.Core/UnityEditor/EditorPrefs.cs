using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace UnityEditor;

/// <summary>
/// UnityEditor.EditorPrefs — editor-only persistent prefs (Unity 2022.3 Pro).
/// Stored under LocalApplicationData/Anity/EditorPrefs (isolated from PlayerPrefs).
/// </summary>
public static class EditorPrefs
{
  private static readonly object _lock = new();
  private static readonly Dictionary<string, int> _intPrefs = new(StringComparer.Ordinal);
  private static readonly Dictionary<string, float> _floatPrefs = new(StringComparer.Ordinal);
  private static readonly Dictionary<string, string> _stringPrefs = new(StringComparer.Ordinal);
  private static readonly Dictionary<string, bool> _boolPrefs = new(StringComparer.Ordinal);
  private static string _savePath = string.Empty;
  private static bool _dirty;
  private static bool _loaded;

  static EditorPrefs()
  {
    try
    {
      EnsurePath();
      Load();
    }
    catch
    {
      _savePath = Path.Combine(Path.GetTempPath(), "anity-editorprefs.json");
    }
  }

  public static string savePath
  {
    get
    {
      EnsurePath();
      return _savePath;
    }
  }

  public static void SetSavePathForTests(string directory)
  {
    lock (_lock)
    {
      Directory.CreateDirectory(directory);
      _savePath = Path.Combine(directory, "anity-editorprefs.json");
      DeleteAllUnlocked();
      _loaded = true;
      if (File.Exists(_savePath))
        LoadUnlocked();
    }
  }

  private static void EnsurePath()
  {
    if (!string.IsNullOrEmpty(_savePath)) return;
    string root = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Anity", "Editor");
    Directory.CreateDirectory(root);
    _savePath = Path.Combine(root, "EditorPrefs.json");
  }

  private static void Load()
  {
    lock (_lock)
    {
      if (_loaded) return;
      _loaded = true;
      LoadUnlocked();
    }
  }

  private static void LoadUnlocked()
  {
    if (string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath)) return;
    try
    {
      string json = File.ReadAllText(_savePath);
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;
      if (root.TryGetProperty("IntPrefs", out var ints))
        foreach (var p in ints.EnumerateObject())
          if (p.Value.TryGetInt32(out int v)) _intPrefs[p.Name] = v;
      if (root.TryGetProperty("FloatPrefs", out var floats))
        foreach (var p in floats.EnumerateObject())
          _floatPrefs[p.Name] = p.Value.GetSingle();
      if (root.TryGetProperty("StringPrefs", out var strs))
        foreach (var p in strs.EnumerateObject())
          _stringPrefs[p.Name] = p.Value.GetString() ?? string.Empty;
      if (root.TryGetProperty("BoolPrefs", out var bools))
        foreach (var p in bools.EnumerateObject())
          _boolPrefs[p.Name] = p.Value.GetBoolean();
    }
    catch { /* corrupt file ignored like Unity soft fail */ }
  }

  public static void SetInt(string key, int value)
  {
    if (key == null) throw new ArgumentNullException(nameof(key));
    lock (_lock) { LoadUnlockedIfNeeded(); _intPrefs[key] = value; _dirty = true; }
  }

  public static int GetInt(string key, int defaultValue = 0)
  {
    if (string.IsNullOrEmpty(key)) return defaultValue;
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      return _intPrefs.TryGetValue(key, out var value) ? value : defaultValue;
    }
  }

  public static void SetFloat(string key, float value)
  {
    if (key == null) throw new ArgumentNullException(nameof(key));
    lock (_lock) { LoadUnlockedIfNeeded(); _floatPrefs[key] = value; _dirty = true; }
  }

  public static float GetFloat(string key, float defaultValue = 0f)
  {
    if (string.IsNullOrEmpty(key)) return defaultValue;
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      return _floatPrefs.TryGetValue(key, out var value) ? value : defaultValue;
    }
  }

  public static void SetString(string key, string value)
  {
    if (key == null) throw new ArgumentNullException(nameof(key));
    lock (_lock) { LoadUnlockedIfNeeded(); _stringPrefs[key] = value ?? string.Empty; _dirty = true; }
  }

  public static string GetString(string key, string defaultValue = "")
  {
    if (string.IsNullOrEmpty(key)) return defaultValue ?? string.Empty;
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      return _stringPrefs.TryGetValue(key, out var value) ? value : defaultValue ?? string.Empty;
    }
  }

  public static void SetBool(string key, bool value)
  {
    if (key == null) throw new ArgumentNullException(nameof(key));
    lock (_lock) { LoadUnlockedIfNeeded(); _boolPrefs[key] = value; _dirty = true; }
  }

  public static bool GetBool(string key, bool defaultValue = false)
  {
    if (string.IsNullOrEmpty(key)) return defaultValue;
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      return _boolPrefs.TryGetValue(key, out var value) ? value : defaultValue;
    }
  }

  public static bool HasKey(string key)
  {
    if (string.IsNullOrEmpty(key)) return false;
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      return _intPrefs.ContainsKey(key) || _floatPrefs.ContainsKey(key)
             || _stringPrefs.ContainsKey(key) || _boolPrefs.ContainsKey(key);
    }
  }

  public static void DeleteKey(string key)
  {
    if (string.IsNullOrEmpty(key)) return;
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      bool removed = _intPrefs.Remove(key) | _floatPrefs.Remove(key)
                     | _stringPrefs.Remove(key) | _boolPrefs.Remove(key);
      if (removed) _dirty = true;
    }
  }

  public static void DeleteAll()
  {
    lock (_lock)
    {
      LoadUnlockedIfNeeded();
      DeleteAllUnlocked();
      _dirty = true;
    }
  }

  private static void DeleteAllUnlocked()
  {
    _intPrefs.Clear();
    _floatPrefs.Clear();
    _stringPrefs.Clear();
    _boolPrefs.Clear();
  }

  private static void LoadUnlockedIfNeeded()
  {
    if (!_loaded)
    {
      _loaded = true;
      LoadUnlocked();
    }
  }

  public static void Save()
  {
    lock (_lock)
    {
      EnsurePath();
      try
      {
        var data = new
        {
          IntPrefs = _intPrefs,
          FloatPrefs = _floatPrefs,
          StringPrefs = _stringPrefs,
          BoolPrefs = _boolPrefs
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        string tmp = _savePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_savePath)) File.Delete(_savePath);
        File.Move(tmp, _savePath);
        _dirty = false;
      }
      catch { }
    }
  }

  public static void SaveIfDirty()
  {
    bool dirty;
    lock (_lock) dirty = _dirty;
    if (dirty) Save();
  }
}
