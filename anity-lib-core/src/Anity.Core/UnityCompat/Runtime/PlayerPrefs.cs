using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace UnityEngine;

/// <summary>
/// UnityEngine.PlayerPrefs — local persistent key/value store (Unity 2022.3 Pro).
/// Cross-platform file backend under Application.persistentDataPath (Windows/Registry
/// equivalence is path-isolated per company/product like Unity player data).
/// </summary>
public enum PlayerPrefsKeyType
{
  String = 0,
  Int = 1,
  Float = 2
}

public static class PlayerPrefs
{
  private static readonly object _lock = new();
  // Unity keys are case-sensitive
  private static readonly Dictionary<string, PrefEntry> _data = new(StringComparer.Ordinal);
  private static string _savePath = string.Empty;
  private static bool _dirty;
  private static int _saveCount;

  private sealed class PrefEntry
  {
    public PlayerPrefsKeyType Type;
    public object Value = string.Empty;
  }

  static PlayerPrefs()
  {
    try
    {
      EnsurePath();
      Load();
    }
    catch
    {
      _savePath = Path.Combine(Path.GetTempPath(), "AnityPlayerPrefs.json");
    }
  }

  /// <summary>Absolute path of the prefs file (Anity diagnostic; useful for tests/CLI).</summary>
  public static string savePath
  {
    get
    {
      EnsurePath();
      return _savePath;
    }
  }

  public static int saveCount => _saveCount;

  private static void EnsurePath()
  {
    if (!string.IsNullOrEmpty(_savePath)) return;
    string root = Application.persistentDataPath;
    if (string.IsNullOrEmpty(root))
      root = Path.Combine(Path.GetTempPath(), "Anity", "PlayerPrefs");
    Directory.CreateDirectory(root);
    // Unity-like file name under company/product folder
    _savePath = Path.Combine(root, "PlayerPrefs.json");
  }

  /// <summary>Test/CI: redirect storage to an isolated directory and reload empty store.</summary>
  public static void SetSavePathForTests(string directory)
  {
    lock (_lock)
    {
      if (string.IsNullOrWhiteSpace(directory))
        throw new ArgumentException("directory required", nameof(directory));
      Directory.CreateDirectory(directory);
      _savePath = Path.Combine(directory, "PlayerPrefs.json");
      _data.Clear();
      _dirty = false;
      if (File.Exists(_savePath))
        LoadUnlocked();
    }
  }

  private static void Load()
  {
    lock (_lock) LoadUnlocked();
  }

  private static void LoadUnlocked()
  {
    _data.Clear();
    if (string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath)) return;
    try
    {
      string json = File.ReadAllText(_savePath);
      using var doc = JsonDocument.Parse(json);
      if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
      foreach (var prop in doc.RootElement.EnumerateObject())
      {
        var el = prop.Value;
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("t", out var tEl))
        {
          string t = tEl.GetString() ?? "s";
          var entry = new PrefEntry();
          switch (t)
          {
            case "i":
              entry.Type = PlayerPrefsKeyType.Int;
              entry.Value = el.TryGetProperty("v", out var iv) && iv.TryGetInt32(out int i) ? i : 0;
              break;
            case "f":
              entry.Type = PlayerPrefsKeyType.Float;
              entry.Value = el.TryGetProperty("v", out var fv) ? fv.GetSingle() : 0f;
              break;
            default:
              entry.Type = PlayerPrefsKeyType.String;
              entry.Value = el.TryGetProperty("v", out var sv) ? (sv.GetString() ?? string.Empty) : string.Empty;
              break;
          }
          _data[prop.Name] = entry;
        }
        else
        {
          // legacy flat value
          _data[prop.Name] = new PrefEntry
          {
            Type = el.ValueKind == JsonValueKind.Number
              ? (el.TryGetInt32(out _) && !el.GetRawText().Contains('.') ? PlayerPrefsKeyType.Int : PlayerPrefsKeyType.Float)
              : PlayerPrefsKeyType.String,
            Value = el.ValueKind switch
            {
              JsonValueKind.String => el.GetString() ?? string.Empty,
              JsonValueKind.Number when el.TryGetInt32(out int i) && !el.GetRawText().Contains('.') => i,
              JsonValueKind.Number => el.GetSingle(),
              JsonValueKind.True => 1,
              JsonValueKind.False => 0,
              _ => string.Empty
            }
          };
        }
      }
    }
    catch (Exception ex)
    {
      throw new PlayerPrefsException("Failed to load PlayerPrefs: " + ex.Message, ex);
    }
  }

  public static void SetInt(string key, int value)
  {
    ValidateKey(key);
    lock (_lock)
    {
      _data[key] = new PrefEntry { Type = PlayerPrefsKeyType.Int, Value = value };
      _dirty = true;
    }
  }

  public static int GetInt(string key, int defaultValue = 0)
  {
    if (string.IsNullOrEmpty(key)) return defaultValue;
    lock (_lock)
    {
      if (!_data.TryGetValue(key, out var e)) return defaultValue;
      return e.Type switch
      {
        PlayerPrefsKeyType.Int => (int)e.Value,
        PlayerPrefsKeyType.Float => (int)(float)e.Value,
        PlayerPrefsKeyType.String when int.TryParse(e.Value as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
        _ => defaultValue
      };
    }
  }

  public static void SetFloat(string key, float value)
  {
    ValidateKey(key);
    lock (_lock)
    {
      _data[key] = new PrefEntry { Type = PlayerPrefsKeyType.Float, Value = value };
      _dirty = true;
    }
  }

  public static float GetFloat(string key, float defaultValue = 0f)
  {
    if (string.IsNullOrEmpty(key)) return defaultValue;
    lock (_lock)
    {
      if (!_data.TryGetValue(key, out var e)) return defaultValue;
      return e.Type switch
      {
        PlayerPrefsKeyType.Float => (float)e.Value,
        PlayerPrefsKeyType.Int => (int)e.Value,
        PlayerPrefsKeyType.String when float.TryParse(e.Value as string, NumberStyles.Float, CultureInfo.InvariantCulture, out float p) => p,
        _ => defaultValue
      };
    }
  }

  public static void SetString(string key, string value)
  {
    ValidateKey(key);
    lock (_lock)
    {
      _data[key] = new PrefEntry { Type = PlayerPrefsKeyType.String, Value = value ?? string.Empty };
      _dirty = true;
    }
  }

  public static string GetString(string key, string defaultValue = "")
  {
    if (string.IsNullOrEmpty(key)) return defaultValue ?? string.Empty;
    lock (_lock)
    {
      if (!_data.TryGetValue(key, out var e)) return defaultValue ?? string.Empty;
      if (e.Type == PlayerPrefsKeyType.String) return (string)e.Value;
      // Unity stringifies other types when GetString on non-string
      return Convert.ToString(e.Value, CultureInfo.InvariantCulture) ?? defaultValue ?? string.Empty;
    }
  }

  /// <summary>Anity extension (also used by some plugins); stored as int 0/1 like Unity pattern.</summary>
  public static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

  public static bool GetBool(string key, bool defaultValue = false) =>
    GetInt(key, defaultValue ? 1 : 0) != 0;

  public static bool HasKey(string key)
  {
    if (string.IsNullOrEmpty(key)) return false;
    lock (_lock) return _data.ContainsKey(key);
  }

  public static void DeleteKey(string key)
  {
    if (string.IsNullOrEmpty(key)) return;
    lock (_lock)
    {
      if (_data.Remove(key))
        _dirty = true;
    }
  }

  public static void DeleteAll()
  {
    lock (_lock)
    {
      if (_data.Count == 0) return;
      _data.Clear();
      _dirty = true;
    }
  }

  public static void Save()
  {
    lock (_lock)
    {
      EnsurePath();
      try
      {
        string? dir = Path.GetDirectoryName(_savePath);
        if (!string.IsNullOrEmpty(dir))
          Directory.CreateDirectory(dir);

        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var kvp in _data)
        {
          string t = kvp.Value.Type switch
          {
            PlayerPrefsKeyType.Int => "i",
            PlayerPrefsKeyType.Float => "f",
            _ => "s"
          };
          object v = kvp.Value.Value;
          if (kvp.Value.Type == PlayerPrefsKeyType.Float)
            v = Convert.ToSingle(v, CultureInfo.InvariantCulture);
          dict[kvp.Key] = new Dictionary<string, object> { ["t"] = t, ["v"] = v };
        }

        string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions
        {
          WriteIndented = true
        });
        // Atomic write
        string tmp = _savePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_savePath))
          File.Delete(_savePath);
        File.Move(tmp, _savePath);
        _dirty = false;
        _saveCount++;
      }
      catch (Exception ex)
      {
        throw new PlayerPrefsException("Failed to save PlayerPrefs: " + ex.Message, ex);
      }
    }
  }

  /// <summary>Flush if dirty (call from Application.Quit / CLI exit).</summary>
  public static void SaveIfDirty()
  {
    bool dirty;
    lock (_lock) dirty = _dirty;
    if (dirty) Save();
  }

  public static PlayerPrefsKeyType? GetKeyType(string key)
  {
    if (string.IsNullOrEmpty(key)) return null;
    lock (_lock)
      return _data.TryGetValue(key, out var e) ? e.Type : null;
  }

  public static string[] GetAllKeys()
  {
    lock (_lock)
    {
      var keys = new string[_data.Count];
      _data.Keys.CopyTo(keys, 0);
      return keys;
    }
  }

  private static void ValidateKey(string key)
  {
    if (key == null)
      throw new ArgumentNullException(nameof(key));
    // Unity allows empty string key on some platforms; we reject null only
  }
}

public sealed class PlayerPrefsException : Exception
{
  public PlayerPrefsException() { }
  public PlayerPrefsException(string message) : base(message) { }
  public PlayerPrefsException(string message, Exception innerException) : base(message, innerException) { }
}
