using UnityEditor;

namespace UnityEditor.Build.Reporting;

public class BuildReport
{
  public BuildSummary summary { get; set; } = new();
  public BuildStep[] steps { get; set; } = Array.Empty<BuildStep>();
  public BuildFile[] files { get; set; } = Array.Empty<BuildFile>();
}

public class BuildSummary
{
  public long totalErrors { get; set; }
  public long totalWarnings { get; set; }
  public ulong totalSize { get; set; }
  public TimeSpan totalTime { get; set; }
  public string? outputPath { get; set; }
  public string? name { get; set; }
  public BuildResult result { get; set; } = BuildResult.Unknown;
  public Guid buildGuid { get; set; }
  public BuildTarget platform { get; set; }
  public BuildTargetGroup platformGroup { get; set; }
  public string platformDefaultExtension { get; set; }
}

public class BuildStep
{
  public string? name { get; set; }
  public int depth { get; set; }
  public TimeSpan duration { get; set; }
  public BuildStepMessage[] messages { get; set; } = Array.Empty<BuildStepMessage>();
  public BuildStep[] subSteps { get; set; } = Array.Empty<BuildStep>();
}

public class BuildStepMessage
{
  public string? content { get; set; }
  public MessageType type { get; set; }
}

public class BuildFile
{
  public string? path { get; set; }
  public Role role { get; set; }
}

public enum MessageType
{
  Info,
  Warning,
  Error,
}

public enum Role
{
  Source,
  Output,
  Intermediate,
}
