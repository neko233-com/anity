using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Anity.Agent;

namespace Anity.Editor.Host.Services.Agent;

public sealed class AgentEditorSettings
{
  public AgentConnectionProfile Connection { get; set; } = new();
  public AgentToolPermission DefaultToolPermission { get; set; } = AgentToolPermission.Ask;
  public Dictionary<string, AgentToolPermission> ToolPermissions { get; set; } =
    new(StringComparer.OrdinalIgnoreCase)
    {
      ["echo"] = AgentToolPermission.Allow,
      ["systeminfo"] = AgentToolPermission.Ask
    };

  public AgentEditorSettings Snapshot()
  {
    if (!Enum.IsDefined(typeof(AgentToolPermission), DefaultToolPermission))
      throw new InvalidDataException("Default Agent tool permission is invalid.");
    if (ToolPermissions is null)
      throw new InvalidDataException("Agent tool permissions are required.");
    if (ToolPermissions.Count > 128)
      throw new InvalidDataException("Agent tool permission count exceeds 128.");

    var policy = new AgentToolPermissionPolicy(DefaultToolPermission);
    foreach ((string name, AgentToolPermission permission) in ToolPermissions)
      policy.SetPermission(name, permission);

    return new AgentEditorSettings
    {
      Connection = (Connection ?? throw new InvalidDataException(
        "Agent connection profile is required.")).Snapshot(),
      DefaultToolPermission = DefaultToolPermission,
      ToolPermissions = policy.Snapshot().ToDictionary(
        pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
    };
  }

  public AgentToolPermissionPolicy CreatePermissionPolicy(
    Func<AgentToolAuthorizationRequest, System.Threading.CancellationToken,
      System.Threading.Tasks.Task<bool>>? prompt = null)
  {
    AgentEditorSettings snapshot = Snapshot();
    var policy = new AgentToolPermissionPolicy(snapshot.DefaultToolPermission)
    {
      PromptAsync = prompt
    };
    foreach ((string name, AgentToolPermission permission) in snapshot.ToolPermissions)
      policy.SetPermission(name, permission);
    return policy;
  }
}

/// <summary>Atomic, bounded persistence for non-secret project Agent settings.</summary>
public sealed class AgentEditorSettingsStore
{
  private const int MaxSettingsBytes = 64 * 1024;
  private readonly object _sync = new();
  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow
  };

  public string ProjectPath { get; }
  public string SettingsPath { get; }

  public AgentEditorSettingsStore(string projectPath)
  {
    if (string.IsNullOrWhiteSpace(projectPath))
      throw new ArgumentException("Project path is required.", nameof(projectPath));
    ProjectPath = Path.GetFullPath(projectPath);
    SettingsPath = Path.Combine(ProjectPath, "ProjectSettings", "AnityAgentSettings.json");
  }

  public AgentEditorSettings Load()
  {
    lock (_sync)
    {
      if (!File.Exists(SettingsPath)) return new AgentEditorSettings().Snapshot();
      var info = new FileInfo(SettingsPath);
      if (info.Length > MaxSettingsBytes)
        throw new InvalidDataException("Agent editor settings exceed 64 KiB.");
      byte[] bytes = File.ReadAllBytes(SettingsPath);
      if (bytes.Length > MaxSettingsBytes)
        throw new InvalidDataException("Agent editor settings exceed 64 KiB.");
      try
      {
        AgentEditorSettings? settings = JsonSerializer.Deserialize<AgentEditorSettings>(bytes, _jsonOptions);
        return (settings ?? throw new InvalidDataException(
          "Agent editor settings are empty.")).Snapshot();
      }
      catch (JsonException ex)
      {
        throw new InvalidDataException("Agent editor settings contain invalid JSON.", ex);
      }
    }
  }

  public void Save(AgentEditorSettings settings)
  {
    if (settings is null) throw new ArgumentNullException(nameof(settings));
    AgentEditorSettings snapshot = settings.Snapshot();
    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, _jsonOptions);
    if (bytes.Length > MaxSettingsBytes)
      throw new InvalidDataException("Agent editor settings exceed 64 KiB.");

    lock (_sync)
    {
      string directory = Path.GetDirectoryName(SettingsPath)
        ?? throw new InvalidOperationException("Agent settings directory is unavailable.");
      Directory.CreateDirectory(directory);
      string temporaryPath = SettingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
      try
      {
        using (var stream = new FileStream(
          temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
          4096, FileOptions.WriteThrough))
        {
          stream.Write(bytes, 0, bytes.Length);
          stream.Flush(true);
        }
        if (File.Exists(SettingsPath))
        {
          string backupPath = SettingsPath + ".bak-" + Guid.NewGuid().ToString("N");
          try { File.Replace(temporaryPath, SettingsPath, backupPath); }
          finally { if (File.Exists(backupPath)) File.Delete(backupPath); }
        }
        else
        {
          File.Move(temporaryPath, SettingsPath);
        }
      }
      finally
      {
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        Array.Clear(bytes, 0, bytes.Length);
      }
    }
  }
}
