namespace Anity.Editor.Host.Models;

public sealed class EditorSession
{
  public string SessionId { get; }
  public string ProjectPath { get; }
  public DateTime StartedAtUtc { get; }
  public string State { get; }
  public IReadOnlyList<string>? OpenWindows { get; }
  public string? ActiveWindow { get; }

  public EditorSession(
    string sessionId,
    string projectPath,
    DateTime startedAtUtc,
    string state,
    IReadOnlyList<string>? openWindows = null,
    string? activeWindow = null)
  {
    SessionId = sessionId;
    ProjectPath = projectPath;
    StartedAtUtc = startedAtUtc;
    State = state;
    OpenWindows = openWindows;
    ActiveWindow = activeWindow;
  }
}

public sealed class EditorStatus
{
  public bool IsRunning { get; }
  public string State { get; }
  public string? SessionId { get; }
  public DateTime? StartedAtUtc { get; }
  public int Tick { get; }
  public IReadOnlyList<string> OpenWindows { get; }
  public IReadOnlyList<string> RegisteredMenus { get; }

  public EditorStatus(
    bool isRunning,
    string state,
    string? sessionId,
    DateTime? startedAtUtc,
    int tick,
    IReadOnlyList<string> openWindows,
    IReadOnlyList<string> registeredMenus)
  {
    IsRunning = isRunning;
    State = state;
    SessionId = sessionId;
    StartedAtUtc = startedAtUtc;
    Tick = tick;
    OpenWindows = openWindows;
    RegisteredMenus = registeredMenus;
  }
}
