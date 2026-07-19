using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;
using Anity.Editor.Host.Models;
using UnityEditor;
using UnityEngine;
using Anity.Editor.Host.Services.Windows;

namespace Anity.Editor.Host.Services;

public sealed class EditorHost
{
  private readonly Dictionary<string, EditorSessionState> _sessions = new();
  private readonly Dictionary<string, Func<bool>> _menuItems = new(StringComparer.Ordinal);
  private readonly Dictionary<string, Func<EditorWindow>> _windowFactories = new(StringComparer.OrdinalIgnoreCase);
  private string? _activeSessionId;
  private int _tick;
  private bool _isRunning;
  private string _state = "stopped";

  public EditorHost()
  {
    RegisterWindowFactories();
    RegisterMenus();
    RegisterMenuItemAttributes();
    EditorApplication.update += OnEditorUpdate;
  }

  public Task<EditorSession> StartSessionAsync(string projectPath)
  {
    EditorApplication.OpenProject(projectPath);
    var sessionId = Guid.NewGuid().ToString("N");
    var opened = new Dictionary<string, EditorWindow>(StringComparer.OrdinalIgnoreCase);

    var state = new EditorSessionState(sessionId, projectPath, DateTime.UtcNow, "running");
    state.OpenWindows = opened;
    _sessions[sessionId] = state;
    _activeSessionId = sessionId;
    _isRunning = true;
    _state = "running";
    EditorApplication.isPlaying = false;
    _tick = 0;

    // Unity 2022 default layout: Scene + Game + Hierarchy + Project + Console + Inspector
    OpenWindowInternal(sessionId, "Scene View");
    OpenWindowInternal(sessionId, "Game View");
    OpenWindowInternal(sessionId, "Hierarchy");
    OpenWindowInternal(sessionId, "Project");
    OpenWindowInternal(sessionId, "Inspector");
    OpenWindowInternal(sessionId, "Console");
    // Ensure Quick Search providers are ready (Ctrl+K)
    UnityEditor.Search.SearchService.EnsureProviders();

    return Task.FromResult(ToSession(state));
  }

  public async Task<EditorSession?> StopAsync(string? sessionId = null)
  {
    if (sessionId is null)
    {
      sessionId = _activeSessionId;
    }

    if (sessionId is null || !_sessions.TryGetValue(sessionId, out var state))
    {
      return null;
    }

    _isRunning = false;
    _state = "stopped";
    EditorApplication.isPlaying = false;

    foreach (var entry in state.OpenWindows.Values.ToList())
    {
      entry.Close();
    }

    state.State = "stopped";
    state.OpenWindows.Clear();

    await Task.Yield();
    _sessions.Remove(sessionId);
    if (_activeSessionId == sessionId)
    {
      _activeSessionId = null;
    }

    return state.ToSession();
  }

  public Task<EditorSession> GetSessionAsync(string sessionId)
  {
    if (_sessions.TryGetValue(sessionId, out var state))
    {
      return Task.FromResult(state.ToSession());
    }

    throw new KeyNotFoundException($"Session '{sessionId}' not found");
  }

  public string GetStatus()
  {
    EditorSession? session = null;
    if (_activeSessionId is not null && _sessions.TryGetValue(_activeSessionId, out var state))
    {
      session = state.ToSession();
    }

    return JsonSerializer.Serialize(BuildStatus(session));
  }

  public IReadOnlyList<string> GetMenus()
  {
    return _menuItems.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
  }

  public bool ExecuteMenu(string menuPath)
  {
    if (!_menuItems.TryGetValue(menuPath, out var action))
    {
      return false;
    }

    action();
    return true;
  }

  public IReadOnlyList<string> GetWindowCatalog()
  {
    return _windowFactories.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
  }

  public bool OpenWindow(string windowName)
  {
    if (_activeSessionId is null) return false;
    return OpenWindowInternal(_activeSessionId, windowName);
  }

  public async Task RunCompatibilityDemoAsync(int ticks = 60, int fps = 30)
  {
    var compatibilityProject = Path.Combine(Path.GetTempPath(), "anity-compat-sample");
    Directory.CreateDirectory(compatibilityProject);
    var host = await StartSessionAsync(compatibilityProject);
    var marker = new GameObject("compat-marker");
    marker.AddComponent<MonoBehaviour>();

    for (var i = 0; i < ticks; i++)
    {
      marker.transform.localPosition = new Vector3((float)Math.Sin(i * 0.1f), 0f, (float)Math.Cos(i * 0.1f));
      Debug.Log($"[{i}] time={Time.time:F2} pos={marker.transform.localPosition}");
      EditorApplication.Update();
      await Task.Delay(Math.Max(1, 1000 / Math.Max(1, fps)));
    }

    Debug.Log($"Demo done: windows={GetWindowCatalog().Count}, tick={_tick}");
    await StopAsync(host.SessionId);
  }

  public string DumpOpenWindows()
  {
    if (_activeSessionId is null || !_sessions.TryGetValue(_activeSessionId, out var state))
    {
      return JsonSerializer.Serialize(Array.Empty<string>());
    }

    return JsonSerializer.Serialize(state.OpenWindows.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
  }

  private bool OpenWindowInternal(string sessionId, string windowName)
  {
    if (!_sessions.TryGetValue(sessionId, out var state))
    {
      return false;
    }

    if (!_windowFactories.TryGetValue(windowName, out var factory))
    {
      return false;
    }

    var window = factory();
    if (!state.OpenWindows.ContainsKey(windowName))
    {
      state.OpenWindows[windowName] = window;
      window.Show();
    }

    if (_activeSessionId == sessionId)
    {
      var maybeLast = _activeSessionId is not null && state.OpenWindows.Count > 0
        ? state.OpenWindows.Values.LastOrDefault()
        : null;
      _ = maybeLast;
    }
    return true;
  }

  private void RegisterMenus()
  {
    _menuItems["File/New Project"] = () => OpenWindow("Project");
    _menuItems["File/Open Scene"] = () => OpenWindow("Scene View");
    _menuItems["Window/Scene View"] = () => OpenWindow("Scene View");
    _menuItems["Window/Game View"] = () => OpenWindow("Game View");
    _menuItems["Window/Hierarchy"] = () => OpenWindow("Hierarchy");
    _menuItems["Window/Project"] = () => OpenWindow("Project");
    _menuItems["Window/Console"] = () => OpenWindow("Console");
    _menuItems["Window/Inspector"] = () => OpenWindow("Inspector");
    _menuItems["Window/Search"] = () => OpenWindow("Search");
    _menuItems["Window/Anity/Agent"] = () => OpenWindow("Anity Agent");
    _menuItems["Edit/Search All..."] = () => OpenWindow("Search");

    _menuItems["Window/Play"] = () =>
    {
      _state = _isRunning ? "playing" : "paused";
      EditorApplication.isPlaying = !_isRunning;
      return true;
    };

    _menuItems["File/Stop"] = () =>
    {
      return StopAsync().GetAwaiter().GetResult() is not null;
    };

    _menuItems["File/Refresh"] = () =>
    {
      EditorApplication.ForceReload();
      return true;
    };
  }

  private void RegisterMenuItemAttributes()
  {
    foreach (var pair in DiscoverMenuCommands())
    {
      if (!_menuItems.ContainsKey(pair.MenuPath))
      {
        _menuItems[pair.MenuPath] = pair.Handler;
      }
    }
  }

  private IEnumerable<(string MenuPath, Func<bool> Handler)> DiscoverMenuCommands()
  {
    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
    foreach (var asm in assemblies)
    {
      Type[] types;
      try
      {
        types = asm.GetTypes();
      }
      catch
      {
        continue;
      }

      foreach (var type in types)
      {
        MethodInfo[] methods;
        try
        {
          methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }
        catch
        {
          continue;
        }

        foreach (var method in methods)
        {
          MenuItem? menu;
          ParameterInfo[] parameters;
          try
          {
            // Plugin/test assemblies can contain attributes whose dependency cannot be
            // loaded in this host. One bad method must not take down all editor menus.
            menu = method.GetCustomAttribute<MenuItem>();
            parameters = method.GetParameters();
          }
          catch (Exception exception) when (
            exception is TypeLoadException
            or FileLoadException
            or FileNotFoundException
            or BadImageFormatException
            or CustomAttributeFormatException)
          {
            continue;
          }
          if (menu is null || menu.isValidateFunction)
          {
            continue;
          }

          if (parameters.Length > 1)
          {
            continue;
          }

          if (parameters.Length == 1 && parameters[0].ParameterType != typeof(MenuCommand))
          {
            continue;
          }

          var menuPath = menu.itemName;
          yield return (menuPath, () => InvokeMenuMethod(method, parameters));
        }
      }
    }
  }

  private static bool InvokeMenuMethod(MethodInfo method, ParameterInfo[] parameters)
  {
    try
    {
      object? result;
      if (parameters.Length == 0)
      {
        result = method.Invoke(null, null);
      }
      else
      {
        var arg = new MenuCommand(Selection.activeObject);
        result = method.Invoke(null, new object[] { arg });
      }

      if (result is null)
      {
        return true;
      }

      if (result is bool handled)
      {
        return handled;
      }
    }
    catch
    {
      // noop
    }

    return false;
  }

  private void RegisterWindowFactories()
  {
    // Register full UnityEditor windows from Anity.Core (not host stubs)
    // EditorWindow owns Show/OnEnable/focus. Factories must only construct; calling
    // ShowWindow here would recursively enter the same registered factory.
    RegisterWindowFactory("Scene View", typeof(SceneView), () => new SceneView());
    RegisterWindowFactory("Game View", typeof(GameView), () => new GameView());
    RegisterWindowFactory("Hierarchy", typeof(HierarchyWindow), () => new HierarchyWindow());
    RegisterWindowFactory("Project", typeof(ProjectWindow), () => new ProjectWindow());
    RegisterWindowFactory("Inspector", typeof(InspectorWindow), () => new InspectorWindow());
    RegisterWindowFactory("Console", typeof(ConsoleWindow), () => new ConsoleWindow());
    RegisterWindowFactory("Search", typeof(UnityEditor.Search.SearchWindow),
      () => new UnityEditor.Search.SearchWindow());
    RegisterWindowFactory("Anity Agent", typeof(AgentEditorWindow),
      () => new AgentEditorWindow());
  }

  private void RegisterWindowFactory(string alias, Type windowType, Func<EditorWindow> factory)
  {
    EditorWindow.RegisterWindowFactory(windowType, factory);
    _windowFactories[alias] = factory;
  }

  private void OnEditorUpdate()
  {
    if (!_isRunning || _activeSessionId is null || !_sessions.ContainsKey(_activeSessionId))
    {
      return;
    }

    _tick++;
    Time.deltaTime = 0.016f;
    Time.timeScale = 1f;

    foreach (var window in EditorWindow.GetWindows())
    {
      window.Repaint();
    }
  }

  private EditorStatus BuildStatus(EditorSession? session)
  {
    var state = session?.State ?? _state;
    return new EditorStatus(
      _isRunning,
      state,
      session?.SessionId,
      session?.StartedAtUtc,
      _tick,
      session?.OpenWindows ?? Array.Empty<string>(),
      GetMenus()
    );
  }

  private EditorSession ToSession(EditorSessionState state)
  {
    return new EditorSession(
      state.SessionId,
      state.ProjectPath,
      state.StartedAtUtc,
      state.State,
      state.OpenWindows.Keys.ToArray(),
      state.OpenWindows.LastOrDefault().Key);
  }

  private sealed class EditorSessionState
  {
    public Dictionary<string, EditorWindow> OpenWindows { get; set; } = new();
    public string SessionId { get; }
    public string ProjectPath { get; }
    public DateTime StartedAtUtc { get; }
    public string State { get; set; }

    public EditorSessionState(string sessionId, string projectPath, DateTime startedAtUtc, string state)
    {
      SessionId = sessionId;
      ProjectPath = projectPath;
      StartedAtUtc = startedAtUtc;
      State = state;
    }

    public EditorSession ToSession()
    {
      return new EditorSession(
        SessionId,
        ProjectPath,
        StartedAtUtc,
        State,
        OpenWindows.Keys.ToArray(),
        OpenWindows.LastOrDefault().Key);
    }
  }
}
