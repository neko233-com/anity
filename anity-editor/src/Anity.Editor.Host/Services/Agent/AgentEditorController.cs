using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anity.Agent;

namespace Anity.Editor.Host.Services.Agent;

public sealed class AgentEditorTranscriptEntry
{
  public AgentRole Role { get; }
  public string Content { get; }
  public DateTime Utc { get; }
  public string FinishReason { get; }
  public AgentTokenUsage? Usage { get; }

  public AgentEditorTranscriptEntry(
    AgentRole role, string content, DateTime utc,
    string? finishReason = null, AgentTokenUsage? usage = null)
  {
    Role = role;
    Content = content ?? string.Empty;
    Utc = utc;
    FinishReason = finishReason ?? string.Empty;
    Usage = usage;
  }
}

/// <summary>Functional editor controller; owns the live runtime but never owns plaintext settings.</summary>
public sealed class AgentEditorController : IDisposable
{
  private readonly AgentEditorSettingsStore _settingsStore;
  private readonly IAgentCredentialVault _credentialVault;
  private readonly Func<AgentConnectionOptions, AgentRuntime> _runtimeFactory;
  private readonly List<AgentEditorTranscriptEntry> _transcript = new();
  private readonly SemaphoreSlim _operationGate = new(1, 1);
  private AgentRuntime? _runtime;
  private AgentSession? _session;
  private string _partialResponse = string.Empty;
  private int _disposed;

  public Func<AgentToolAuthorizationRequest, CancellationToken, Task<bool>>? PermissionPromptAsync { get; set; }
  public AgentEditorSettings Settings { get; private set; }
  public bool IsConnected => _runtime is not null && _session is not null && !_session.IsClosed;
  public string CredentialBackend => _credentialVault.BackendName;
  public bool IsCredentialBackendAvailable => _credentialVault.IsAvailable;
  public string AuditFilePath => Path.Combine(
    _settingsStore.ProjectPath, "Library", "AnityAgent", "Audit", "tool-audit.jsonl");
  public IReadOnlyList<AgentEditorTranscriptEntry> Transcript
  {
    get { lock (_transcript) return _transcript.ToArray(); }
  }
  public string PartialResponse
  {
    get { lock (_transcript) return _partialResponse; }
  }

  public AgentEditorController(
    AgentEditorSettingsStore settingsStore,
    IAgentCredentialVault credentialVault,
    Func<AgentConnectionOptions, AgentRuntime>? runtimeFactory = null)
  {
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    _credentialVault = credentialVault ?? throw new ArgumentNullException(nameof(credentialVault));
    _runtimeFactory = runtimeFactory ?? (options => new AgentRuntime(options));
    Settings = _settingsStore.Load();
  }

  public async Task SaveAsync(
    AgentEditorSettings settings,
    string? replacementApiKey = null,
    CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    AgentEditorSettings snapshot = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
    await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    string? previousSecret = null;
    bool credentialChanged = false;
    try
    {
      if (!string.IsNullOrEmpty(replacementApiKey))
      {
        _ = new AgentConnectionOptions
        {
          ApiKey = replacementApiKey,
          BaseUrl = snapshot.Connection.BaseUrl,
          Model = snapshot.Connection.Model,
          Timeout = snapshot.Connection.Timeout,
          MaxRetries = snapshot.Connection.MaxRetries,
          MaxResponseBytes = snapshot.Connection.MaxResponseBytes
        }.Validate();
        previousSecret = await _credentialVault.RetrieveAsync(
          snapshot.Connection.CredentialId, cancellationToken).ConfigureAwait(false);
        await _credentialVault.StoreAsync(
          snapshot.Connection.CredentialId, replacementApiKey, cancellationToken)
          .ConfigureAwait(false);
        credentialChanged = true;
      }
      try
      {
        cancellationToken.ThrowIfCancellationRequested();
        _settingsStore.Save(snapshot);
      }
      catch
      {
        if (credentialChanged)
        {
          if (previousSecret is null)
            await _credentialVault.DeleteAsync(
              snapshot.Connection.CredentialId, CancellationToken.None).ConfigureAwait(false);
          else
            await _credentialVault.StoreAsync(
              snapshot.Connection.CredentialId, previousSecret, CancellationToken.None)
              .ConfigureAwait(false);
        }
        throw;
      }
      Settings = snapshot;
    }
    finally
    {
      previousSecret = null;
      _operationGate.Release();
    }
  }

  public async Task ConnectAsync(CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      AgentConnectionOptions options = await Settings.Connection.ResolveAsync(
        _credentialVault, cancellationToken).ConfigureAwait(false);
      AgentRuntime next = _runtimeFactory(options)
        ?? throw new InvalidOperationException("Agent runtime factory returned null.");
      try
      {
        AgentRuntime? previous = Interlocked.Exchange(ref _runtime, null);
        _session = null;
        previous?.Dispose();
        next.ToolAuthorizationPolicy = Settings.CreatePermissionPolicy(PermissionPromptAsync);
        next.ToolAuditSink = new HashChainedAgentToolAuditLog(_settingsStore.ProjectPath);
        next.AuditFailureMode = AgentAuditFailureMode.FailClosed;
        AgentSession nextSession = next.CreateSession("editor-" + Guid.NewGuid().ToString("N"));
        _runtime = next;
        _session = nextSession;
      }
      catch
      {
        next.Dispose();
        throw;
      }
    }
    finally
    {
      _operationGate.Release();
    }
  }

  public async Task<string> SendAsync(
    string prompt,
    CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    if (string.IsNullOrWhiteSpace(prompt))
      throw new ArgumentException("Agent prompt is required.", nameof(prompt));
    await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      AgentSession session = _session
        ?? throw new InvalidOperationException("Connect the Agent before sending a prompt.");
      lock (_transcript)
      {
        _partialResponse = string.Empty;
        _transcript.Add(new AgentEditorTranscriptEntry(
          AgentRole.User, prompt.Trim(), DateTime.UtcNow));
      }
      try
      {
        var streamed = new StringBuilder();
        await foreach (AgentStreamUpdate update in session.RunTurnStreamAsync(
          prompt, cancellationToken).ConfigureAwait(false))
        {
          if (update.ContentDelta.Length == 0) continue;
          streamed.Append(update.ContentDelta);
          lock (_transcript) _partialResponse = streamed.ToString();
        }
        AgentMessage reply = session.History.LastOrDefault(
          message => message.Role == AgentRole.Assistant)
          ?? throw new InvalidOperationException(
            "Agent stream completed without an assistant response.");
        lock (_transcript)
        {
          _partialResponse = string.Empty;
          _transcript.Add(new AgentEditorTranscriptEntry(
            AgentRole.Assistant, reply.Content, reply.Utc,
            reply.FinishReason, reply.Usage));
        }
        return reply.Content;
      }
      catch
      {
        lock (_transcript)
        {
          _partialResponse = string.Empty;
          if (_transcript.Count > 0 && _transcript[^1].Role == AgentRole.User)
            _transcript.RemoveAt(_transcript.Count - 1);
        }
        throw;
      }
    }
    finally
    {
      _operationGate.Release();
    }
  }

  public async Task<bool> DeleteCredentialAsync(CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      bool deleted = await _credentialVault.DeleteAsync(
        Settings.Connection.CredentialId, cancellationToken).ConfigureAwait(false);
      Disconnect();
      return deleted;
    }
    finally
    {
      _operationGate.Release();
    }
  }

  public void Disconnect()
  {
    AgentRuntime? runtime = Interlocked.Exchange(ref _runtime, null);
    _session = null;
    runtime?.Dispose();
  }

  public void ClearTranscript()
  {
    lock (_transcript)
    {
      _transcript.Clear();
      _partialResponse = string.Empty;
    }
  }

  public void Dispose()
  {
    if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
    Disconnect();
    _operationGate.Dispose();
  }

  private void ThrowIfDisposed()
  {
    if (Volatile.Read(ref _disposed) != 0)
      throw new ObjectDisposedException(nameof(AgentEditorController));
  }
}
