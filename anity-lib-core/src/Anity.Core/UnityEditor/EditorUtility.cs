using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace UnityEditor;

public static class EditorUtility
{
  private static readonly Dictionary<string, bool> _focusState = new(StringComparer.OrdinalIgnoreCase);
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

  private static string Normalize(string path)
  {
    return string.IsNullOrWhiteSpace(path)
      ? string.Empty
      : path.Replace('\\', '/').Trim().Trim('/');
  }
}
