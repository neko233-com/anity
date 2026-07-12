using System;
using System.IO;
using System.Text;

namespace UnityEngine;

/// <summary>
/// Local file storage helpers aligned with Unity Application path layout
/// (persistentDataPath / temporaryCachePath / streamingAssetsPath / dataPath).
/// </summary>
public static class LocalStorage
{
  public static string persistentDataPath => Application.persistentDataPath;
  public static string temporaryCachePath => Application.temporaryCachePath;
  public static string streamingAssetsPath => Application.streamingAssetsPath;
  public static string dataPath => Application.dataPath;
  public static string consoleLogPath => Application.consoleLogPath;

  public static void EnsureDirectory(string path)
  {
    if (string.IsNullOrWhiteSpace(path)) return;
    Directory.CreateDirectory(path);
  }

  public static string CombinePersistent(params string[] parts)
  {
    string root = persistentDataPath;
    EnsureDirectory(root);
    if (parts == null || parts.Length == 0) return root;
    return Path.Combine(root, Path.Combine(parts));
  }

  public static string CombineTemp(params string[] parts)
  {
    string root = temporaryCachePath;
    EnsureDirectory(root);
    if (parts == null || parts.Length == 0) return root;
    return Path.Combine(root, Path.Combine(parts));
  }

  public static void WriteAllText(string relativeOrAbsolute, string contents, bool usePersistent = true)
  {
    string path = Resolve(relativeOrAbsolute, usePersistent);
    EnsureDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, contents ?? string.Empty, Encoding.UTF8);
  }

  public static string ReadAllText(string relativeOrAbsolute, bool usePersistent = true, string defaultValue = "")
  {
    string path = Resolve(relativeOrAbsolute, usePersistent);
    return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : defaultValue;
  }

  public static void WriteAllBytes(string relativeOrAbsolute, byte[] data, bool usePersistent = true)
  {
    string path = Resolve(relativeOrAbsolute, usePersistent);
    EnsureDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, data ?? Array.Empty<byte>());
  }

  public static byte[] ReadAllBytes(string relativeOrAbsolute, bool usePersistent = true)
  {
    string path = Resolve(relativeOrAbsolute, usePersistent);
    return File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
  }

  public static bool Exists(string relativeOrAbsolute, bool usePersistent = true)
  {
    return File.Exists(Resolve(relativeOrAbsolute, usePersistent));
  }

  public static bool Delete(string relativeOrAbsolute, bool usePersistent = true)
  {
    string path = Resolve(relativeOrAbsolute, usePersistent);
    if (!File.Exists(path)) return false;
    File.Delete(path);
    return true;
  }

  public static string Resolve(string relativeOrAbsolute, bool usePersistent)
  {
    if (string.IsNullOrEmpty(relativeOrAbsolute))
      return usePersistent ? persistentDataPath : temporaryCachePath;
    if (Path.IsPathRooted(relativeOrAbsolute))
      return relativeOrAbsolute;
    return usePersistent ? CombinePersistent(relativeOrAbsolute) : CombineTemp(relativeOrAbsolute);
  }
}
