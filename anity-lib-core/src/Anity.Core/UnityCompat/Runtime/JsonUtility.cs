using System.Text.Json;

namespace UnityEngine;

public static class JsonUtility
{
  public static string ToJson(object? obj, bool prettyPrint = false)
  {
    if (obj is null) return "{}";
    var option = prettyPrint
      ? new JsonSerializerOptions { WriteIndented = true }
      : new JsonSerializerOptions();
    return JsonSerializer.Serialize(obj, obj.GetType(), option);
  }

  public static T? FromJson<T>(string json)
  {
    if (string.IsNullOrWhiteSpace(json)) return default;
    return JsonSerializer.Deserialize<T>(json);
  }

  public static void FromJsonOverwrite(string json, object objectToOverwrite)
  {
    _ = json;
    _ = objectToOverwrite;
  }
}

