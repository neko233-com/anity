using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class PlayerPrefs
{
  private static readonly Dictionary<string, int> _intValues = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, float> _floatValues = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, string> _stringValues = new(StringComparer.OrdinalIgnoreCase);

  public static void SetInt(string key, int value) => _intValues[key] = value;
  public static int GetInt(string key, int defaultValue = 0) => _intValues.GetValueOrDefault(key, defaultValue);
  public static void SetFloat(string key, float value) => _floatValues[key] = value;
  public static float GetFloat(string key, float defaultValue = 0f) => _floatValues.GetValueOrDefault(key, defaultValue);
  public static void SetString(string key, string value) => _stringValues[key] = value;
  public static string GetString(string key, string defaultValue = "") => _stringValues.GetValueOrDefault(key, defaultValue);
  public static bool HasKey(string key) => _intValues.ContainsKey(key) || _floatValues.ContainsKey(key) || _stringValues.ContainsKey(key);
  public static void DeleteKey(string key)
  {
    _ = _intValues.Remove(key);
    _ = _floatValues.Remove(key);
    _ = _stringValues.Remove(key);
  }
  public static void DeleteAll() { _intValues.Clear(); _floatValues.Clear(); _stringValues.Clear(); }
  public static void Save() {}
}

public sealed class PlayerPrefsException : Exception
{
  public PlayerPrefsException() {}
  public PlayerPrefsException(string message) : base(message) {}
}

