namespace Anity.Editor.Host.Models;

public sealed record EditorSession(
  string SessionId,
  string ProjectPath,
  DateTime StartedAtUtc,
  string State,
  IReadOnlyList<string>? OpenWindows = null,
  string? ActiveWindow = null);

public sealed record EditorStatus(
  bool IsRunning,
  string State,
  string? SessionId,
  DateTime? StartedAtUtc,
  int Tick,
  IReadOnlyList<string> OpenWindows,
  IReadOnlyList<string> RegisteredMenus);
