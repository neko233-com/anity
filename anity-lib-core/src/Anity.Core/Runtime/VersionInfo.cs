namespace Anity.Core.Runtime;

public sealed record VersionInfo(
  int Major,
  int Minor,
  int Patch,
  string Tag = "stable")
{
  public override string ToString() => $"{Major}.{Minor}.{Patch}" + (string.IsNullOrWhiteSpace(Tag) ? string.Empty : $"-{Tag}");
}
