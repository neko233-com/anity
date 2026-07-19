using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor.Compilation;
using EventType = UnityEngine.EventType;

namespace UnityEditor
{
  public sealed class ConsoleWindow : EditorWindow
  {
    private Vector2 _scrollPosition;
    private Vector2 _stackTraceScroll;
    private readonly List<ConsoleEntry> _entries = new List<ConsoleEntry>();
    private readonly List<CollapsedEntry> _collapsedEntries = new List<CollapsedEntry>();
    private int _selectedEntryIndex = -1;
    private int _selectedCollapsedIndex = -1;
    private bool _collapse;
    private bool _clearOnPlay = true;
    private bool _errorPause;
    private bool _showLog = true;
    private bool _showWarning = true;
    private bool _showError = true;
    private string _searchFilter = string.Empty;
    private bool _autoScrollToBottom = true;
    private int _errorCount;
    private int _warningCount;
    private int _compileErrorCount;
    private int _compileWarningCount;
    private bool _isCompiling;
    private double _lastClickTime;
    private int _lastClickedIndex;

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
      CompilationPipeline.compilationStarted += OnCompilationStarted;
      CompilationPipeline.compilationFinished += OnCompilationFinished;
    }

    protected override void OnDisable()
    {
      base.OnDisable();
      Application.logMessageReceived -= OnLogMessage;
      CompilationPipeline.compilationStarted -= OnCompilationStarted;
      CompilationPipeline.compilationFinished -= OnCompilationFinished;
    }

    private void OnCompilationStarted(string assemblyPath, CompilerMessage[] messages)
    {
      _isCompiling = true;
      _compileErrorCount = 0;
      _compileWarningCount = 0;
      Repaint();
    }

    private void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
      _isCompiling = false;
      foreach (var msg in messages)
      {
        if (msg.type == CompilerMessageType.Error)
        {
          _compileErrorCount++;
          Log($"Compiler error at {msg.file}:{msg.line}: {msg.message}", string.Empty, LogType.Error);
        }
        else if (msg.type == CompilerMessageType.Warning)
        {
          _compileWarningCount++;
          Log($"Compiler warning at {msg.file}:{msg.line}: {msg.message}", string.Empty, LogType.Warning);
        }
      }
      Repaint();
    }

    protected override void OnGUI()
    {
      DrawToolbar();
      DrawLogList();
      DrawStackTrace();
      DrawStatusBar();
    }

    private void DrawToolbar()
    {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
        Clear();

      if (GUILayout.Button("⎘", EditorStyles.toolbarButton, GUILayout.Width(24f)))
        RequestRecompile();

      _collapse = GUILayout.Toggle(_collapse, "Collapse", EditorStyles.toolbarButton);
      _clearOnPlay = GUILayout.Toggle(_clearOnPlay, "Clear on Play", EditorStyles.toolbarButton);
      _errorPause = GUILayout.Toggle(_errorPause, "Error Pause", EditorStyles.toolbarButton);
      _autoScrollToBottom = GUILayout.Toggle(_autoScrollToBottom, "↧", EditorStyles.toolbarButton, GUILayout.Width(24f));

      GUILayout.FlexibleSpace();

      if (_isCompiling)
      {
        GUILayout.Label("⏳ Compiling...", EditorStyles.miniLabel);
      }

      _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150f));
      if (GUILayout.Button("×", EditorStyles.toolbarSearchFieldCancelButton, GUILayout.Width(16f)))
        _searchFilter = string.Empty;

      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal(EditorStyles.toolbar);

      _showLog = GUILayout.Toggle(_showLog, $"● Info ({CountByType(LogType.Log)})", EditorStyles.toolbarButton);
      _showWarning = GUILayout.Toggle(_showWarning, $"⚠ Warn ({CountByType(LogType.Warning)})", EditorStyles.toolbarButton);
      _showError = GUILayout.Toggle(_showError, $"❌ Error ({CountByType(LogType.Error)})", EditorStyles.toolbarButton);

      GUILayout.EndHorizontal();
    }

    private void DrawLogList()
    {
      float stackTraceHeight = _selectedEntryIndex >= 0 ? 150f : 0f;
      float listHeight = position.height - 80f - stackTraceHeight;

      _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(listHeight));

      if (_collapse)
      {
        DrawCollapsedLogList();
      }
      else
      {
        DrawNormalLogList();
      }

      GUILayout.EndScrollView();
    }

    private void DrawNormalLogList()
    {
      for (int i = 0; i < _entries.Count; i++)
      {
        var entry = _entries[i];

        if (!ShouldShowEntry(entry)) continue;

        bool isSelected = _selectedEntryIndex == i;
        var style = GetStyleForType(entry.Type);

        if (isSelected)
        {
          GUILayout.BeginVertical(EditorStyles.selectedLabel);
        }

        GUILayout.BeginHorizontal();

        var icon = GetIconForType(entry.Type);
        var timestamp = entry.Timestamp.ToString("HH:mm:ss");
        var coloredMessage = GetColoredMessage(entry.Message, entry.Type);
        var label = $"[{timestamp}] {icon} {coloredMessage}";

        var content = new GUIContent(label);
        if (GUILayout.Button(content, style, GUILayout.ExpandWidth(true)))
        {
          HandleEntryClick(i, false);
        }

        GUILayout.EndHorizontal();

        if (isSelected)
        {
          GUILayout.EndVertical();
        }
      }
    }

    private void DrawCollapsedLogList()
    {
      RebuildCollapsedEntries();

      for (int i = 0; i < _collapsedEntries.Count; i++)
      {
        var collapsed = _collapsedEntries[i];

        if (!ShouldShowEntry(collapsed.Entry)) continue;

        bool isSelected = _selectedCollapsedIndex == i;
        var style = GetStyleForType(collapsed.Entry.Type);

        if (isSelected)
        {
          GUILayout.BeginVertical(EditorStyles.selectedLabel);
        }

        GUILayout.BeginHorizontal();

        var icon = GetIconForType(collapsed.Entry.Type);
        var countBadge = collapsed.Count > 1 ? $" x{collapsed.Count}" : "";
        var label = $"{icon} {collapsed.Entry.Message}{countBadge}";

        if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
        {
          _selectedCollapsedIndex = i;
          _selectedEntryIndex = collapsed.FirstIndex;
        }

        GUILayout.EndHorizontal();

        if (isSelected)
        {
          GUILayout.EndVertical();
        }
      }
    }

    private void HandleEntryClick(int index, bool isCollapsed)
    {
      bool isDoubleClick = (Event.current.clickCount == 2);
      _selectedEntryIndex = index;

      if (isDoubleClick)
      {
        OpenEntryInEditor(_entries[index]);
      }

      if (Event.current.button == 1)
      {
        ShowEntryContextMenu(index);
      }
    }

    private void DrawStackTrace()
    {
      if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _entries.Count) return;

      var entry = _entries[_selectedEntryIndex];
      if (string.IsNullOrEmpty(entry.StackTrace)) return;

      GUILayout.Box(string.Empty, GUILayout.Height(1f));

      GUILayout.BeginHorizontal(EditorStyles.toolbar);
      GUILayout.Label($"Stack Trace - {entry.Timestamp:HH:mm:ss.fff}", EditorStyles.miniBoldLabel);
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(50f)))
      {
        CopyEntryToClipboard(entry);
      }
      GUILayout.EndHorizontal();

      _stackTraceScroll = GUILayout.BeginScrollView(_stackTraceScroll, GUILayout.Height(140f));
      var stackTraceStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
      GUILayout.Label(FormatStackTrace(entry.StackTrace), stackTraceStyle);
      GUILayout.EndScrollView();
    }

    private string FormatStackTrace(string stackTrace)
    {
      if (string.IsNullOrEmpty(stackTrace)) return string.Empty;
      var lines = stackTrace.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
      var sb = new StringBuilder();
      foreach (var line in lines)
      {
        if (line.Contains("(at "))
        {
          sb.AppendLine($"  <color=#888888>{line}</color>");
        }
        else
        {
          sb.AppendLine(line);
        }
      }
      return sb.ToString();
    }

    private void DrawStatusBar()
    {
      GUILayout.BeginHorizontal(EditorStyles.statusbar);

      GUILayout.Label($"Info: {_errorCount + _warningCount + _entries.Count - CountByType(LogType.Error) - CountByType(LogType.Warning)}", EditorStyles.miniLabel);
      GUILayout.Label($"Warnings: {_warningCount + _compileWarningCount}", EditorStyles.yellowLabel);
      GUILayout.Label($"Errors: {_errorCount + _compileErrorCount}", EditorStyles.redLabel);

      if (_compileErrorCount > 0 || _compileWarningCount > 0)
      {
        GUILayout.Label($"| Compile: {_compileErrorCount} errors, {_compileWarningCount} warnings", EditorStyles.miniLabel);
      }

      GUILayout.FlexibleSpace();
      GUILayout.Label($"{_entries.Count} entries", EditorStyles.miniLabel);

      GUILayout.EndHorizontal();
    }

    private bool ShouldShowEntry(ConsoleEntry entry)
    {
      if (!string.IsNullOrEmpty(_searchFilter) &&
          !entry.Message.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) &&
          !entry.StackTrace.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
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

    private void RebuildCollapsedEntries()
    {
      _collapsedEntries.Clear();
      var messageMap = new Dictionary<string, int>();

      for (int i = 0; i < _entries.Count; i++)
      {
        var entry = _entries[i];
        var key = $"{entry.Type}:{entry.Message}";

        if (messageMap.TryGetValue(key, out int existingIndex))
        {
          _collapsedEntries[existingIndex].Count++;
          _collapsedEntries[existingIndex].LastIndex = i;
        }
        else
        {
          messageMap[key] = _collapsedEntries.Count;
          _collapsedEntries.Add(new CollapsedEntry
          {
            Entry = entry,
            Count = 1,
            FirstIndex = i,
            LastIndex = i
          });
        }
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
        case LogType.Warning: return "⚠";
        case LogType.Log: return "●";
        case LogType.Exception: return "💥";
        default: return "●";
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

    private string GetColoredMessage(string message, LogType type)
    {
      string color;
      switch (type)
      {
        case LogType.Error:
        case LogType.Exception:
          color = "#ff5555";
          break;
        case LogType.Warning:
          color = "#ffcc00";
          break;
        default:
          return message;
      }
      return $"<color={color}>{message}</color>";
    }

    public void Log(string message, string stackTrace, LogType type)
    {
      var entry = new ConsoleEntry
      {
        Message = message,
        StackTrace = stackTrace,
        Type = type,
        Timestamp = DateTime.Now
      };
      _entries.Add(entry);

      if (type == LogType.Error || type == LogType.Exception)
        _errorCount++;
      else if (type == LogType.Warning)
        _warningCount++;

      if (_autoScrollToBottom)
      {
        EditorApplication.delayCall += () =>
        {
          _scrollPosition.y = float.MaxValue;
          Repaint();
        };
      }

      if (_errorPause && (type == LogType.Error || type == LogType.Exception))
      {
        EditorApplication.isPaused = true;
      }
    }

    public void Clear()
    {
      _entries.Clear();
      _collapsedEntries.Clear();
      _selectedEntryIndex = -1;
      _selectedCollapsedIndex = -1;
      _errorCount = 0;
      _warningCount = 0;
    }

    private void RequestRecompile()
    {
      CompilationPipeline.RequestScriptCompilation();
    }

    private void OpenEntryInEditor(ConsoleEntry entry)
    {
      if (string.IsNullOrEmpty(entry.StackTrace)) return;

      var lines = entry.StackTrace.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines)
      {
        int start = line.IndexOf("(at ", StringComparison.Ordinal);
        if (start < 0) continue;
        start += 4;
        int end = line.IndexOf(')', start);
        if (end < 0) continue;

        var location = line.Substring(start, end - start);
        int colon = location.LastIndexOf(':');
        if (colon > 0)
        {
          string filePath = location.Substring(0, colon);
          string lineStr = location.Substring(colon + 1);
          if (int.TryParse(lineStr, out int lineNumber))
          {
            var asset = AssetDatabase.LoadMainAssetAtPath(filePath);
            if (asset != null)
            {
              Selection.activeObject = asset;
              if (ProjectWindow.instance != null)
                ProjectWindow.instance.Repaint();
            }
            break;
          }
        }
      }
    }

    private void CopyEntryToClipboard(ConsoleEntry entry)
    {
      var sb = new StringBuilder();
      sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.Type}");
      sb.AppendLine(entry.Message);
      if (!string.IsNullOrEmpty(entry.StackTrace))
      {
        sb.AppendLine();
        sb.AppendLine("Stack Trace:");
        sb.AppendLine(entry.StackTrace);
      }
      EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    private void ShowEntryContextMenu(int index)
    {
      var menu = new GenericMenu();
      menu.AddItem("Copy", false, (GenericMenu.MenuFunction)(() => CopyEntryToClipboard(_entries[index])));
      menu.AddItem("Copy Message Only", false, (GenericMenu.MenuFunction)(() =>
      {
        EditorGUIUtility.systemCopyBuffer = _entries[index].Message;
      }));
      menu.AddSeparator();
      menu.AddItem("Clear", false, (GenericMenu.MenuFunction)Clear);
      menu.AddItem("Collapse Similar", false, (GenericMenu.MenuFunction)(() => { _collapse = true; }));
      menu.ShowAsContext();
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
    public string File = string.Empty;
    public int Line;
  }

  internal sealed class CollapsedEntry
  {
    public ConsoleEntry Entry;
    public int Count;
    public int FirstIndex;
    public int LastIndex;
  }
}
