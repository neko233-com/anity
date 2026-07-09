using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
  public sealed class ConsoleWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private readonly List<ConsoleEntry> _entries = new List<ConsoleEntry>();
    private int _selectedEntryIndex = -1;
    private bool _collapse;
    private bool _clearOnPlay = true;
    private bool _errorPause;
    private bool _showLog = true;
    private bool _showWarning = true;
    private bool _showError = true;
    private string _searchFilter = string.Empty;

    public static ConsoleWindow instance { get; private set; }

    public ConsoleWindow()
    {
      titleContent = new GUIContent("Console");
      minSize = new Vector2(300f, 100f);
      instance = this;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
      Application.logMessageReceived += OnLogMessage;
    }

    protected override void OnDisable()
    {
      base.OnDisable();
      Application.logMessageReceived -= OnLogMessage;
    }

    protected override void OnGUI()
    {
      DrawToolbar();
      DrawLogList();
      DrawStatusBar();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
        Clear();

      _collapse = GUILayout.Toggle(_collapse, "Collapse", EditorStyles.toolbarButton);
      _clearOnPlay = GUILayout.Toggle(_clearOnPlay, "Clear on Play", EditorStyles.toolbarButton);
      _errorPause = GUILayout.Toggle(_errorPause, "Error Pause", EditorStyles.toolbarButton);

      GUILayout.FlexibleSpace();

      _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
      if (GUILayout.Button("", EditorStyles.toolbarSearchFieldCancelButton))
        _searchFilter = string.Empty;

      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      _showLog = GUILayout.Toggle(_showLog, $"Info ({CountByType(LogType.Log)})", EditorStyles.toolbarButton);
      _showWarning = GUILayout.Toggle(_showWarning, $"Warning ({CountByType(LogType.Warning)})", EditorStyles.toolbarButton);
      _showError = GUILayout.Toggle(_showError, $"Error ({CountByType(LogType.Error)})", EditorStyles.toolbarButton);

      GUILayout.EndHorizontal();
    }

    private void DrawLogList()
    {
      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

      for (int i = 0; i < _entries.Count; i++)
      {
        var entry = _entries[i];

        if (!ShouldShowEntry(entry)) continue;

        var selected = _selectedEntryIndex == i;
        var style = GetStyleForType(entry.Type);

        if (selected)
        {
          GUILayout.BeginVertical(EditorStyles.selectedLabel);
        }

        var icon = GetIconForType(entry.Type);
        var label = $"{icon} {entry.Message}";
        if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
        {
          _selectedEntryIndex = i;
        }

        if (selected)
        {
          GUILayout.Space(2f);
          GUILayout.Label(entry.StackTrace, EditorStyles.miniLabel);
          GUILayout.EndVertical();
        }
      }

      GUILayout.EndScrollView();
    }

    private void DrawStatusBar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);
      GUILayout.Label($"Info: {CountByType(LogType.Log)}  Warnings: {CountByType(LogType.Warning)}  Errors: {CountByType(LogType.Error)}", EditorStyles.miniLabel);
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
    }

    private bool ShouldShowEntry(ConsoleEntry entry)
    {
      if (!string.IsNullOrEmpty(_searchFilter) && !entry.Message.Contains(_searchFilter))
        return false;

      switch (entry.Type)
      {
        case LogType.Log:
        case LogType.Assert:
          return _showLog;
        case LogType.Warning:
          return _showWarning;
        case LogType.Error:
        case LogType.Exception:
          return _showError;
        default:
          return true;
      }
    }

    private int CountByType(LogType type)
    {
      int count = 0;
      for (int i = 0; i < _entries.Count; i++)
      {
        if (type == LogType.Log && (_entries[i].Type == LogType.Log || _entries[i].Type == LogType.Assert))
          count++;
        else if (type == LogType.Warning && _entries[i].Type == LogType.Warning)
          count++;
        else if (type == LogType.Error && (_entries[i].Type == LogType.Error || _entries[i].Type == LogType.Exception))
          count++;
      }
      return count;
    }

    private static string GetIconForType(LogType type)
    {
      switch (type)
      {
        case LogType.Error: return "❌";
        case LogType.Assert: return "⛔";
        case LogType.Warning: return "⚠️";
        case LogType.Log: return "ℹ️";
        case LogType.Exception: return "💥";
        default: return "ℹ️";
      }
    }

    private static GUIStyle GetStyleForType(LogType type)
    {
      switch (type)
      {
        case LogType.Error:
        case LogType.Exception:
          return EditorStyles.redLabel;
        case LogType.Warning:
          return EditorStyles.yellowLabel;
        default:
          return EditorStyles.label;
      }
    }

    public void Log(string message, string stackTrace, LogType type)
    {
      _entries.Add(new ConsoleEntry
      {
        Message = message,
        StackTrace = stackTrace,
        Type = type,
        Timestamp = DateTime.Now
      });
    }

    public void Clear()
    {
      _entries.Clear();
      _selectedEntryIndex = -1;
    }

    private void OnLogMessage(string message, string stackTrace, LogType type)
    {
      Log(message, stackTrace, type);
      Repaint();
    }

    [MenuItem("Window/General/Console")]
    public static ConsoleWindow ShowWindow()
    {
      return GetWindow<ConsoleWindow>("Console");
    }
  }

  internal sealed class ConsoleEntry
  {
    public string Message = string.Empty;
    public string StackTrace = string.Empty;
    public LogType Type;
    public DateTime Timestamp;
    public int Count = 1;
  }
}
