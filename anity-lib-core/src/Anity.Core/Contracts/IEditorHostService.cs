using Anity.Core.Runtime;

namespace Anity.Core.Contracts;

public interface IEditorHostService
{
  Task StartSessionAsync(ProjectSession session, CancellationToken cancellationToken = default);
  Task StopSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public sealed record ProjectSession(
  string SessionId,
  string ProjectPath,
  string EditorVersion,
  VersionInfo? EditorRuntime = null,
  PlatformProfile Platform = PlatformProfile.Windows);
