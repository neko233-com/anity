using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

public static class EditorUtility
{
  private static readonly Dictionary<string, bool> _focusState = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<int, UnityEngine.Object> _instanceIDToObject = new();
  private static string _progressTitle = string.Empty;
  private static string _progressInfo = string.Empty;
  private static float _progress;

  public static float progress => _progress;

  public static void SetDirty(object? target)
  {
    _ = target;
  }

  public static bool DisplayDialog(string title, string message, string ok)
  {
    return DisplayDialog(title, message, ok, "Cancel");
  }

  public static bool DisplayDialog(string title, string message, string ok, string cancel)
  {
    _ = title;
    _ = message;
    _ = ok;
    _ = cancel;
    return true;
  }

  public static bool DisplayDialog(string title, string message, string ok, string cancel, string alt)
  {
    _ = title;
    _ = message;
    _ = ok;
    _ = cancel;
    _ = alt;
    return true;
  }

  public static int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt)
  {
    _ = title;
    _ = message;
    _ = ok;
    _ = cancel;
    _ = alt;
    return 0;
  }

  public static bool DisplayCancelableProgressBar(string title, string info, float progress)
  {
    _ = title;
    _ = info;
    _ = progress;
    _progressTitle = title;
    _progressInfo = info;
    _progress = Math.Clamp(progress, 0f, 1f);
    return false;
  }

  public static void DisplayProgressBar(string title, string info, float progress)
  {
    _ = title;
    _ = info;
    _ = progress;
    _progressTitle = title;
    _progressInfo = info;
    _progress = Math.Clamp(progress, 0f, 1f);
  }

  public static void ClearProgressBar()
  {
    _progress = 0f;
    _progressTitle = string.Empty;
    _progressInfo = string.Empty;
  }

  public static string OpenFilePanel(string title, string directory, string extension)
  {
    _ = title;
    _ = extension;
    return string.IsNullOrWhiteSpace(directory) ? string.Empty : Normalize(directory);
  }

  public static string OpenFilePanelWithFilters(string title, string directory, string[] filters, int filterIndex = 0)
  {
    _ = title;
    _ = filterIndex;
    return OpenFilePanel(title, directory, filters is { Length: > 0 } ? filters[0] : string.Empty);
  }

  public static string OpenFilePanelWithFilters(string title, string directory, string defaultName, string[] filters)
  {
    _ = defaultName;
    return OpenFilePanelWithFilters(title, directory, filters, 0);
  }

  public static string OpenFolderPanel(string title, string folder, string defaultName)
  {
    _ = title;
    _ = defaultName;
    return Normalize(folder);
  }

  public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
  {
    _ = title;
    _ = directory;
    _ = extension;
    return Normalize(defaultName);
  }

  public static string SaveFilePanelInProject(string filename, string extension, string mimeType)
  {
    _ = filename;
    _ = extension;
    _ = mimeType;
    return Normalize(filename);
  }

  public static void OpenWithPing(object? obj)
  {
    _ = obj;
  }

  public static bool IsPersistent(object? obj)
  {
    _ = obj;
    return true;
  }

  public static void CopySerializedManagedFieldsOnly(object source, object destination)
  {
    _ = source;
    _ = destination;
  }

  public static string? SaveFilePanelInProject(string title, string defaultName, string extension, string defaultDir)
  {
    _ = title;
    _ = defaultDir;
    return Normalize(defaultName);
  }

  public static bool HasAdvancedRenderPipeline()
  {
    return false;
  }

  public static void SetImportQueueMode(string path, int mode)
  {
    _ = path;
    _ = mode;
  }

  public static void RequestScriptReload()
  {
    // no-op in shell mode
  }

  public static void ShowNotification(Object? target, string message)
  {
    _ = target;
    _ = message;
  }

  public static void FocusProjectWindow()
  {
    _focusState["project"] = true;
  }

  public static void FocusProjectWindowIfNeeded()
  {
    _focusState["project"] = true;
  }

  public static void OpenWithDefaultApp(string path)
  {
    _ = path;
    try
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = path,
        UseShellExecute = true
      });
    }
    catch
    {
      // environment dependent fallback no-op
    }
  }

  public static void ForceReloadInspectors()
  {
    // no-op shim
  }

  public static void SetDirtyIfNotDirty(object? target)
  {
    _ = target;
  }

  public static string SaveFolderPanel(string title, string folder, string defaultName)
  {
    _ = title;
    _ = defaultName;
    return Normalize(folder);
  }

  public static bool CopySerialized(Object source, Object dest)
  {
    if (source == null || dest == null) return false;
    var sourceType = source.GetType();
    var destType = dest.GetType();
    if (sourceType != destType) return false;
    foreach (var prop in sourceType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
    {
      if (prop.CanRead && prop.CanWrite)
      {
        try { prop.SetValue(dest, prop.GetValue(source)); } catch { }
      }
    }
    foreach (var field in sourceType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
    {
      try { field.SetValue(dest, field.GetValue(source)); } catch { }
    }
    return true;
  }

  public static bool CopySerializedIfDifferent(Object source, Object dest)
  {
    if (source == null || dest == null) return false;
    return CopySerialized(source, dest);
  }

  public static int NaturalCompare(string a, string b)
  {
    if (a == null && b == null) return 0;
    if (a == null) return -1;
    if (b == null) return 1;
    return string.Compare(a, b, StringComparison.Ordinal);
  }

  public static int ObjectToInstanceID(Object obj)
  {
    if (obj == null) return 0;
    int id = obj.GetInstanceID();
    _instanceIDToObject[id] = obj;
    return id;
  }

  public static Object[] CollectDependencies(Object[] roots)
  {
    if (roots == null) return Array.Empty<Object>();
    var result = new HashSet<Object>();
    foreach (var root in roots)
    {
      if (root != null) result.Add(root);
      if (root is GameObject go)
      {
        foreach (var comp in go.GetComponents<Component>())
        {
          if (comp != null) result.Add(comp);
        }
        foreach (Transform child in go.transform)
        {
          result.Add(child.gameObject);
        }
      }
    }
    return result.ToArray();
  }

  public static void CompressTexture(Texture2D texture, TextureFormat format, int quality)
  {
    if (texture == null) return;
    _ = format;
    _ = quality;
  }

  public static UnityEngine.Object InstanceIDToObject(int instanceID)
  {
    if (instanceID == 0) return null;
    _instanceIDToObject.TryGetValue(instanceID, out var obj);
    return obj;
  }

  public static void RegisterInstanceID(UnityEngine.Object obj)
  {
    if (obj == null) return;
    _instanceIDToObject[obj.GetInstanceID()] = obj;
  }

  private static string Normalize(string path)
  {
    return string.IsNullOrWhiteSpace(path)
      ? string.Empty
      : path.Replace('\\', '/').Trim().Trim('/');
  }
}
