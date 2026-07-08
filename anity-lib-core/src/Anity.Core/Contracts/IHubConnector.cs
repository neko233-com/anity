using Anity.Core.Runtime;

namespace Anity.Core.Contracts;

public interface IHubConnector
{
  Task<IReadOnlyList<EditorArtifact>> QueryAvailableEditorsAsync(CancellationToken cancellationToken = default);
  Task<bool> RequestLaunchAsync(EditorArtifact artifact, CancellationToken cancellationToken = default);
}
