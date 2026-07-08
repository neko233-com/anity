namespace Anity.Core.Runtime;

public sealed record EditorArtifact(
  string ArtifactId,
  string Version,
  string Sha256,
  string DownloadUrl);

public sealed record PackageManifest(
  string PackageId,
  string Name,
  string Version,
  string Description,
  string[] Dependencies);
