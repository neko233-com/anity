using System.Collections.Generic;

namespace UnityEditor;

public static class EditorUserSettings
{
  private static readonly Dictionary<string, string> _settings = new(StringComparer.OrdinalIgnoreCase);

  public static string GetConfigValue(string key, string defaultValue)
  {
    return _settings.TryGetValue(key, out var value) ? value : defaultValue;
  }

  public static string GetConfigValue(string key, string defaultValue, bool isSessionValue)
  {
    _ = isSessionValue;
    return GetConfigValue(key, defaultValue);
  }

  public static void SetConfigValue(string key, string value)
  {
    _settings[key] = value;
  }

  public static void SetConfigValue(string key, string value, bool isSessionValue)
  {
    _ = isSessionValue;
    SetConfigValue(key, value);
  }

  public static int GetInt(string key, int defaultValue)
  {
    return int.TryParse(GetConfigValue(key, defaultValue.ToString()), out var value) ? value : defaultValue;
  }

  public static int GetInt(string key, int defaultValue, bool isSessionValue)
  {
    _ = isSessionValue;
    return GetInt(key, defaultValue);
  }

  public static void SetInt(string key, int value)
  {
    SetConfigValue(key, value.ToString());
  }

  public static void SetInt(string key, int value, bool isSessionValue)
  {
    _ = isSessionValue;
    SetInt(key, value);
  }

  public static bool GetBool(string key, bool defaultValue)
  {
    if (bool.TryParse(GetConfigValue(key, defaultValue.ToString()), out var value))
    {
      return value;
    }

    return defaultValue;
  }

  public static bool GetBool(string key, bool defaultValue, bool isSessionValue)
  {
    _ = isSessionValue;
    return GetBool(key, defaultValue);
  }

  public static void SetBool(string key, bool value)
  {
    SetConfigValue(key, value.ToString());
  }

  public static void SetBool(string key, bool value, bool isSessionValue)
  {
    _ = isSessionValue;
    SetBool(key, value);
  }
}
