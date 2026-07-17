using System.Runtime.InteropServices;
using Anity.Agent;
using Anity.Editor.Host.Services.Agent;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class AgentAuditLogTests : IDisposable
{
  private readonly string _projectPath = Path.Combine(
    Path.GetTempPath(), "anity-audit-tests-" + Guid.NewGuid().ToString("N"));

  [Fact]
  public async Task SingleRecordRoundTripsAndVerifies()
  {
    using var log = NewLog();
    await log.WriteAsync(NewEvent("one"));

    AgentToolAuditVerificationResult result = await log.VerifyAsync();

    Assert.Equal(1, result.RecordCount);
    Assert.Equal(1, result.FirstSequence);
    Assert.Equal(1, result.LastSequence);
    Assert.Equal(64, result.LastHash.Length);
  }

  [Fact]
  public async Task RequestedAndCompletedRecordsFormContiguousChain()
  {
    using var log = NewLog();
    await log.WriteAsync(NewEvent("pair", AgentToolAuditPhase.Requested));
    await log.WriteAsync(NewEvent(
      "pair", AgentToolAuditPhase.Completed, AgentToolAuditOutcome.Succeeded));

    AgentToolAuditVerificationResult result = await log.VerifyAsync();

    Assert.Equal(2, result.RecordCount);
    Assert.Equal(2, result.LastSequence);
  }

  [Fact]
  public async Task AuditFileNeverContainsRawArgumentsOrToolResult()
  {
    const string rawSecret = "raw-secret-value";
    using var runtime = new AgentRuntime();
    AgentSession session = runtime.CreateSession("redacted");
    var call = new AgentToolCall("redacted-call", "echo", $"{{\"token\":\"{rawSecret}\"}}");
    AgentToolAuditEvent audit = AgentToolAuditEvent.Create(
      AgentToolAuditPhase.Completed, AgentToolAuditOutcome.Succeeded,
      session, call, resultBytes: 123, durationMilliseconds: 4);
    using var log = NewLog();

    await log.WriteAsync(audit);

    string json = File.ReadAllText(log.FilePath);
    Assert.DoesNotContain(rawSecret, json, StringComparison.Ordinal);
    Assert.DoesNotContain("ArgumentsJson", json, StringComparison.Ordinal);
    Assert.Contains("argumentsSha256", json, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ReopenContinuesSequenceAndHashChain()
  {
    string firstHash;
    using (var first = NewLog())
    {
      await first.WriteAsync(NewEvent("first"));
      firstHash = (await first.VerifyAsync()).LastHash;
    }
    using var second = NewLog();
    await second.WriteAsync(NewEvent("second"));

    AgentToolAuditVerificationResult result = await second.VerifyAsync();

    Assert.Equal(2, result.RecordCount);
    Assert.Equal(2, result.LastSequence);
    Assert.NotEqual(firstHash, result.LastHash);
  }

  [Fact]
  public void SecondWriterIsRejectedWhileProjectAuditIsOpen()
  {
    using var first = NewLog();
    Assert.Throws<IOException>(() => NewLog());
  }

  [Fact]
  public async Task PayloadTamperingIsDetectedOnReopen()
  {
    string path;
    using (var log = NewLog())
    {
      await log.WriteAsync(NewEvent("tamper-payload"));
      path = log.FilePath;
    }
    string text = File.ReadAllText(path);
    File.WriteAllText(path, text.Replace(
      "\"toolName\":\"echo\"", "\"toolName\":\"evil\"", StringComparison.Ordinal));

    Assert.Throws<InvalidDataException>(() => NewLog());
  }

  [Fact]
  public async Task EnvelopeHashTamperingIsDetectedOnReopen()
  {
    string path;
    using (var log = NewLog())
    {
      await log.WriteAsync(NewEvent("tamper-hash"));
      path = log.FilePath;
    }
    string text = File.ReadAllText(path);
    int marker = text.LastIndexOf("\"hash\":\"", StringComparison.Ordinal);
    int hashIndex = marker + "\"hash\":\"".Length;
    char replacement = text[hashIndex] == '0' ? '1' : '0';
    text = text.Substring(0, hashIndex) + replacement + text.Substring(hashIndex + 1);
    File.WriteAllText(path, text);

    Assert.Throws<InvalidDataException>(() => NewLog());
  }

  [Fact]
  public async Task ConcurrentAppendsRemainContiguous()
  {
    using var log = NewLog(maxFileBytes: 64 * 1024);
    Task[] writes = Enumerable.Range(0, 64)
      .Select(index => log.WriteAsync(NewEvent("concurrent-" + index)))
      .ToArray();

    await Task.WhenAll(writes);

    AgentToolAuditVerificationResult result = await log.VerifyAsync();
    Assert.Equal(64, result.RecordCount);
    Assert.Equal(64, result.LastSequence);
  }

  [Fact]
  public async Task RotationPreservesChainAcrossFiles()
  {
    using var log = NewLog(maxFileBytes: 1024, maxArchives: 8);
    for (int index = 0; index < 12; index++)
      await log.WriteAsync(NewEvent("rotation-" + index));

    AgentToolAuditVerificationResult result = await log.VerifyAsync();

    Assert.True(result.FileCount > 1);
    Assert.Equal(12, result.RecordCount);
    Assert.Equal(12, result.LastSequence);
  }

  [Fact]
  public async Task RotationEnforcesArchiveCapAndRetainedChainVerifies()
  {
    using var log = NewLog(maxFileBytes: 1024, maxArchives: 2);
    for (int index = 0; index < 30; index++)
      await log.WriteAsync(NewEvent("bounded-" + index));

    AgentToolAuditVerificationResult result = await log.VerifyAsync();

    Assert.True(result.FileCount <= 3);
    Assert.True(result.FirstSequence > 1);
    Assert.Equal(30, result.LastSequence);
    Assert.False(File.Exists(log.FilePath + ".3"));
  }

  [Theory]
  [InlineData(1000, 1)]
  [InlineData(67108865, 1)]
  [InlineData(4096, 0)]
  [InlineData(4096, 33)]
  public void InvalidBoundsAreRejected(int maxBytes, int archives)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      new HashChainedAgentToolAuditLog(_projectPath, maxBytes, archives));
  }

  [Fact]
  public void ArchiveGapIsRejected()
  {
    string directory = Path.Combine(_projectPath, "Library", "AnityAgent", "Audit");
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "tool-audit.jsonl.2"), string.Empty);
    File.WriteAllText(Path.Combine(directory, "tool-audit.jsonl"), string.Empty);

    Assert.Throws<InvalidDataException>(() => NewLog());
  }

  [Fact]
  public async Task UnixAuditDirectoryAndFileArePrivate()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
    using var log = NewLog();
    await log.WriteAsync(NewEvent("permissions"));

    UnixFileMode fileMode = File.GetUnixFileMode(log.FilePath) &
      (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
       UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
       UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
    UnixFileMode directoryMode = File.GetUnixFileMode(Path.GetDirectoryName(log.FilePath)!) &
      (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
       UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
       UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, fileMode);
    Assert.Equal(
      UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
      directoryMode);
  }

  [Fact]
  public async Task DisposedLogRejectsWrites()
  {
    var log = NewLog();
    log.Dispose();
    await Assert.ThrowsAsync<ObjectDisposedException>(() => log.WriteAsync(NewEvent("disposed")));
  }

  [Fact]
  public async Task EditorControllerWritesRealToolAuditPair()
  {
    var vault = new FakeVault { Value = "vault-key" };
    var provider = new StreamingToolProvider();
    var store = new AgentEditorSettingsStore(_projectPath);
    using var controller = new AgentEditorController(store, vault, _ =>
    {
      var runtime = new AgentRuntime(provider);
      runtime.Tools.Register(new EchoRemoteTool());
      return runtime;
    });
    AgentEditorSettings settings = new();
    settings.ToolPermissions["echo"] = AgentToolPermission.Allow;
    await controller.SaveAsync(settings);
    await controller.ConnectAsync();

    Assert.Equal("done", await controller.SendAsync("use echo"));
    controller.Disconnect();
    using var verifier = NewLog();
    AgentToolAuditVerificationResult result = await verifier.VerifyAsync();

    Assert.Equal(2, result.RecordCount);
    string json = File.ReadAllText(verifier.FilePath);
    Assert.Contains("\"outcome\":\"Succeeded\"", json, StringComparison.Ordinal);
    Assert.DoesNotContain("hello-secret", json, StringComparison.Ordinal);
  }

  private HashChainedAgentToolAuditLog NewLog(
    int maxFileBytes = 4 * 1024 * 1024,
    int maxArchives = 8)
    => new(_projectPath, maxFileBytes, maxArchives);

  private static AgentToolAuditEvent NewEvent(
    string callId,
    AgentToolAuditPhase phase = AgentToolAuditPhase.Requested,
    AgentToolAuditOutcome outcome = AgentToolAuditOutcome.Pending)
  {
    using var runtime = new AgentRuntime();
    AgentSession session = runtime.CreateSession("audit-session");
    return AgentToolAuditEvent.Create(
      phase, outcome, session,
      new AgentToolCall(callId, "echo", "{\"value\":1}"),
      phase == AgentToolAuditPhase.Completed ? 2 : 0,
      phase == AgentToolAuditPhase.Completed ? 1 : 0);
  }

  public void Dispose()
  {
    try { if (Directory.Exists(_projectPath)) Directory.Delete(_projectPath, true); }
    catch { }
  }

  private sealed class FakeVault : IAgentCredentialVault
  {
    public string BackendName => "fake";
    public bool IsAvailable => true;
    public string? Value { get; set; }
    public Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
    {
      Value = secret;
      return Task.CompletedTask;
    }
    public Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
      => Task.FromResult(Value);
    public Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
    {
      bool existed = Value is not null;
      Value = null;
      return Task.FromResult(existed);
    }
  }

  private sealed class EchoRemoteTool : IRemoteAgentTool
  {
    public string Name => "echo";
    public string Description => "echo";
    public string ParametersJsonSchema => "{\"type\":\"object\"}";
    public string Invoke(string args, AgentSession session) => args;
    public Task<string> InvokeRemoteAsync(
      string argumentsJson, AgentSession session,
      CancellationToken cancellationToken = default) => Task.FromResult("ok");
  }

  private sealed class StreamingToolProvider : IToolCallingAgentProvider
  {
    private int _round;
    public Task<string> CompleteAsync(
      IReadOnlyList<AgentMessage> messages,
      CancellationToken cancellationToken = default) => Task.FromResult("unused");
    public Task<AgentModelTurn> CompleteWithToolsAsync(
      IReadOnlyList<AgentMessage> messages,
      IReadOnlyList<AgentToolDefinition> tools,
      CancellationToken cancellationToken = default) => Task.FromResult(new AgentModelTurn("unused"));
    public async IAsyncEnumerable<AgentStreamUpdate> StreamAsync(
      IReadOnlyList<AgentMessage> messages,
      [System.Runtime.CompilerServices.EnumeratorCancellation]
      CancellationToken cancellationToken = default)
    {
      await Task.Yield();
      yield return new AgentStreamUpdate("unused", isCompleted: true);
    }
    public async IAsyncEnumerable<AgentStreamUpdate> StreamWithToolsAsync(
      IReadOnlyList<AgentMessage> messages,
      IReadOnlyList<AgentToolDefinition> tools,
      [System.Runtime.CompilerServices.EnumeratorCancellation]
      CancellationToken cancellationToken = default)
    {
      await Task.Yield();
      if (Interlocked.Increment(ref _round) == 1)
      {
        yield return new AgentStreamUpdate(toolCallDeltas: new[]
        {
          new AgentToolCallDelta(0, "call-1", "echo", "{\"value\":\"hello-secret\"}")
        });
        yield return new AgentStreamUpdate(isCompleted: true, finishReason: "tool_calls");
      }
      else
      {
        yield return new AgentStreamUpdate("done");
        yield return new AgentStreamUpdate(isCompleted: true, finishReason: "stop");
      }
    }
  }
}
