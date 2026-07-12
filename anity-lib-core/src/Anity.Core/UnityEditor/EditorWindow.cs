using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

public abstract class EditorWindow
{
  private static readonly object _sync = new();
  private static readonly Dictionary<string, EditorWindow> _openWindows = new(StringComparer.OrdinalIgnoreCase);
  private static readonly Dictionary<string, Func<EditorWindow>> _windowFactories = new(StringComparer.OrdinalIgnoreCase);
  private static EditorWindow? _focusedWindow;

  private bool _initialized;
  private bool _isOpen;
  private bool _focused;
  private GUIContent? _notification;
  private bool _hasUnsavedChanges;

  public string name => GetType().Name;
  public GUIContent titleContent { get; set; } = new GUIContent("Window");
  public bool wantsMouseMove { get; set; }
  public bool wantsMouseEnterLeaveWindow { get; set; }
  public bool autoRepaintOnSceneChange { get; set; }
  public bool maximized { get; set; }
  public bool hasUnsavedChanges
  {
    get => _hasUnsavedChanges;
    set => _hasUnsavedChanges = value;
  }
  public string saveChangesMessage { get; set; } = string.Empty;
  public Vector2 minSize { get; set; } = new Vector2(160f, 120f);
  public Vector2 maxSize { get; set; } = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
  public Rect position { get; set; } = new Rect(0f, 0f, 100f, 100f);
  public bool hasFocus => _focused;
  public bool isDocked => true;
  public static int windowCount
  {
    get
    {
      lock (_sync) return _openWindows.Count;
    }
  }

  public static EditorWindow[] windows
  {
    get
    {
      lock (_sync) return _openWindows.Values.ToArray();
    }
  }

  public static EditorWindow? focusedWindow
  {
    get
    {
      lock (_sync) return _focusedWindow;
    }
  }

  public static EditorWindow? mouseOverWindow
  {
    get
    {
      return focusedWindow;
    }
  }

  public static EditorWindow? current { get; set; }

  protected virtual void OnEnable() {}
  protected virtual void OnDisable() {}
  protected virtual void OnGUI() {}
  protected virtual void OnFocus() {}
  protected virtual void OnLostFocus() {}
  protected virtual void OnDestroy() {}
  protected virtual void OnProjectChange() {}
  protected virtual void OnSelectionChange() {}
  protected virtual void OnInspectorUpdate() {}
  protected virtual void OnHierarchyChange() {}
  protected virtual void OnBeforeSerialize() {}
  public virtual void SaveChanges() { _hasUnsavedChanges = false; }
  public virtual void DiscardChanges() { _hasUnsavedChanges = false; }

  public static void BeginWindows() {}
  public static void EndWindows() {}

  public void Close()
  {
    var key = KeyForType(GetType());
    if (!_isOpen) return;
    lock (_sync)
    {
      if (!_openWindows.Remove(key))
      {
        return;
      }

      _isOpen = false;
      _focused = false;
      if (_focusedWindow == this)
      {
        _focusedWindow = null;
      }

      if (ReferenceEquals(current, this))
      {
        current = null;
      }

      OnDestroy();
      OnDisable();
    }
  }

  public void Focus()
  {
    lock (_sync)
    {
      _focused = true;
      _focusedWindow = this;
      current = this;
      OnFocus();
    }
  }

  public void Repaint()
  {
    OnGUI();
  }

  public void RepaintImmediately()
  {
    OnGUI();
  }

  public void RepaintAndFocus()
  {
    Repaint();
    Focus();
  }

  public void Show()
  {
    Show(false);
  }

  public void ShowModal() => Show(true);
  public void ShowPopup() => Show(true);
  public void ShowUtility() => Show(true);
  public void ShowTab() => Show(false);
  public void ShowAuxWindow() => Show(true);

  public void ShowAsDropDown(Rect buttonRect, Vector2 windowSize)
  {
    position = new Rect(buttonRect.x, buttonRect.yMax, windowSize.x, windowSize.y);
    Show(true);
  }

  public void ShowNotification(GUIContent notification)
  {
    ShowNotification(notification, 4.0);
  }

  public void ShowNotification(GUIContent notification, double fadeoutWait)
  {
    _notification = notification;
    _ = fadeoutWait;
  }

  public void RemoveNotification()
  {
    _notification = null;
  }

  public void SendEvent(Event e)
  {
    _ = e;
  }

  public bool SendEvent(EventType type)
  {
    _ = type;
    return true;
  }

  public static EditorWindow GetWindow(Type t, bool utility = false, string? title = null)
  {
    _ = utility;
    return GetOrCreateWindow(t, title);
  }

  public static EditorWindow GetWindow(Type t, bool utility, string title, bool focus)
  {
    _ = utility;
    return GetWindowWithFocus(t, title, focus);
  }

  public static T GetWindow<T>(bool utility = false, string? title = null) where T : EditorWindow, new()
  {
    _ = utility;
    var type = typeof(T);
    return (T)GetOrCreateWindow(type, title);
  }

  public static T GetWindow<T>(string? title) where T : EditorWindow, new()
  {
    return GetWindow<T>(false, title);
  }

  public static T GetWindow<T>(bool utility, string? title, bool focus) where T : EditorWindow, new()
  {
    _ = utility;
    return (T)GetWindowWithFocus(typeof(T), title, focus);
  }

  public static EditorWindow CreateWindow(Type t, string? title = null)
  {
    return GetOrCreateWindow(t, title);
  }

  public static EditorWindow CreateWindow(Type t, int desiredDockNextToWindowId)
  {
    _ = desiredDockNextToWindowId;
    return GetOrCreateWindow(t, null);
  }

  public static IReadOnlyList<EditorWindow> GetWindows()
  {
    lock (_sync) return _openWindows.Values.ToList();
  }

  public static EditorWindow? FindObjectOfType(Type type)
  {
    lock (_sync)
    {
      return _openWindows.Values.FirstOrDefault(w => w.GetType() == type || w.GetType().IsSubclassOf(type));
    }
  }

  public static T? FindObjectOfType<T>() where T : EditorWindow
  {
    return FindObjectOfType(typeof(T)) as T;
  }

  public static void FocusWindowIfItsOpen(Type t)
  {
    var window = FindObjectOfType(t);
    if (window is not null)
    {
      window.Focus();
    }
  }

  public static bool HasOpenInstances<T>() where T : EditorWindow
  {
    return FindObjectOfType(typeof(T)) is not null;
  }

  public static void FocusWindowIfItsOpen<T>() where T : EditorWindow
  {
    FocusWindowIfItsOpen(typeof(T));
  }

  public static void RegisterWindowFactory(Type type, Func<EditorWindow> factory)
  {
    var key = KeyForType(type);
    lock (_sync)
    {
      _windowFactories[key] = factory;
    }
  }

  protected static void ShowWindow(EditorWindow window)
  {
    var key = KeyForType(window.GetType());
    lock (_sync)
    {
      _openWindows[key] = window;
      if (!_windowFactories.ContainsKey(key))
      {
        _windowFactories[key] = () => window;
      }
    }
  }

  internal static string KeyForType(Type t)
  {
    return t.FullName ?? t.Name;
  }

  private static EditorWindow GetWindowWithFocus(Type type, string? title, bool focus)
  {
    var window = GetOrCreateWindow(type, title);
    if (focus)
    {
      window.Focus();
    }

    return window;
  }

  private static EditorWindow GetOrCreateWindow(Type type, string? title)
  {
    var key = KeyForType(type);
    lock (_sync)
    {
      if (_openWindows.TryGetValue(key, out var existing))
      {
        if (!string.IsNullOrWhiteSpace(title))
        {
          existing.titleContent.text = title!;
        }

        existing.Focus();
        return existing;
      }

      EditorWindow created;
      if (_windowFactories.TryGetValue(key, out var factory))
      {
        created = factory();
      }
      else
      {
        created = (EditorWindow)Activator.CreateInstance(type)!;
      }

      if (!string.IsNullOrWhiteSpace(title))
      {
        created.titleContent.text = title!;
      }

      if (!created._initialized)
      {
        created._initialized = true;
        created.OnEnable();
      }

      created._isOpen = true;
      _openWindows[key] = created;
      created.Focus();
      return created;
    }
  }

  internal static void Show(EditorWindow window)
  {
    lock (_sync)
    {
      var key = KeyForType(window.GetType());
      if (!window._initialized)
      {
        window._initialized = true;
        window.OnEnable();
      }

      window._isOpen = true;
      _openWindows[key] = window;
      window.Focus();
      window.Repaint();
    }
  }

  private void Show(bool hasFocus)
  {
    lock (_sync)
    {
      if (!_initialized)
      {
        _initialized = true;
        OnEnable();
      }

      _isOpen = true;
      if (hasFocus)
      {
        Focus();
      }
      else
      {
        current = this;
      }

      Repaint();
    }
  }
}
