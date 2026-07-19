using System.Text;
using Anity.Agent;
using Anity.Editor.Host.Services.Agent;
using Anity.Editor.Host.Services;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class AgentEditorTests : IDisposable
{
  private readonly string _projectPath = Path.Combine(
    Path.GetTempPath(), "anity-agent-editor-tests-" + Guid.NewGuid().ToString("N"));

  public AgentEditorTests() => Directory.CreateDirectory(_projectPath);

  [Fact]
  public void MissingSettingsReturnsSecureDefaults()
  {
    AgentEditorSettings settings = NewStore().Load();

    Assert.Equal("https://api.openai.com/v1", settings.Connection.BaseUrl);
    Assert.Equal("default", settings.Connection.CredentialId);
    Assert.Equal(AgentToolPermission.Ask, settings.DefaultToolPermission);
    Assert.Equal(AgentToolPermission.Allow, settings.ToolPermissions["echo"]);
  }

  [Fact]
  public void SettingsRoundTripPreservesNonSecretConnectionAndPermissions()
  {
    AgentEditorSettingsStore store = NewStore();
    AgentEditorSettings expected = CustomSettings();
    store.Save(expected);

    AgentEditorSettings actual = store.Load();

    Assert.Equal("https://local.example/v1", actual.Connection.BaseUrl);
    Assert.Equal("local-model", actual.Connection.Model);
    Assert.Equal("project-key", actual.Connection.CredentialId);
    Assert.Equal(AgentToolPermission.Deny, actual.ToolPermissions["systeminfo"]);
  }

  [Fact]
  public void SettingsFileHasNoApiKeyFieldOrSecret()
  {
    AgentEditorSettingsStore store = NewStore();
    store.Save(CustomSettings());
    string json = File.ReadAllText(store.SettingsPath);

    Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("credentialId", json, StringComparison.Ordinal);
  }

  [Fact]
  public void MalformedJsonIsRejected()
  {
    AgentEditorSettingsStore store = NewStore();
    Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
    File.WriteAllText(store.SettingsPath, "{bad-json");
    Assert.Throws<InvalidDataException>(() => store.Load());
  }

  [Fact]
  public void OversizedSettingsFileIsRejectedBeforeParsing()
  {
    AgentEditorSettingsStore store = NewStore();
    Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
    File.WriteAllBytes(store.SettingsPath, new byte[65 * 1024]);
    Assert.Throws<InvalidDataException>(() => store.Load());
  }

  [Fact]
  public void InvalidToolNameCannotBePersisted()
  {
    AgentEditorSettings settings = CustomSettings();
    settings.ToolPermissions["../escape"] = AgentToolPermission.Allow;
    Assert.Throws<ArgumentException>(() => NewStore().Save(settings));
  }

  [Fact]
  public void InvalidBaseUrlCannotBePersisted()
  {
    AgentEditorSettings settings = CustomSettings();
    settings.Connection.BaseUrl = "file:///tmp/key";
    Assert.Throws<ArgumentException>(() => NewStore().Save(settings));
  }

  [Fact]
  public void AtomicSaveLeavesNoTemporaryOrBackupFiles()
  {
    AgentEditorSettingsStore store = NewStore();
    store.Save(CustomSettings());
    AgentEditorSettings changed = CustomSettings();
    changed.Connection.Model = "second-model";
    store.Save(changed);

    string directory = Path.GetDirectoryName(store.SettingsPath)!;
    Assert.Equal("second-model", store.Load().Connection.Model);
    Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
    Assert.Empty(Directory.GetFiles(directory, "*.bak-*"));
  }

  [Fact]
  public async Task ControllerStoresKeyInVaultButNeverInProjectFile()
  {
    var vault = new FakeVault();
    using var controller = NewController(vault);

    await controller.SaveAsync(CustomSettings(), "secret-key");

    Assert.Equal("secret-key", vault.Value);
    Assert.Equal("project-key", vault.LastStoredId);
    string json = File.ReadAllText(NewStore().SettingsPath);
    Assert.DoesNotContain("secret-key", json, StringComparison.Ordinal);
  }

  [Fact]
  public async Task InvalidReplacementKeyDoesNotTouchVaultOrSettings()
  {
    var vault = new FakeVault();
    using var controller = NewController(vault);

    await Assert.ThrowsAsync<ArgumentException>(
      () => controller.SaveAsync(CustomSettings(), "bad key with spaces"));

    Assert.Null(vault.Value);
    Assert.False(File.Exists(NewStore().SettingsPath));
  }

  [Fact]
  public async Task SettingsFailureRestoresPreviousVaultCredential()
  {
    string blockedProject = Path.Combine(_projectPath, "blocked");
    Directory.CreateDirectory(blockedProject);
    File.WriteAllText(Path.Combine(blockedProject, "ProjectSettings"), "not-a-directory");
    var vault = new FakeVault { Value = "old-key" };
    using var controller = new AgentEditorController(
      new AgentEditorSettingsStore(blockedProject), vault,
      _ => new AgentRuntime(new FixedProvider("ok")));

    await Assert.ThrowsAnyAsync<IOException>(
      () => controller.SaveAsync(CustomSettings(), "new-key"));

    Assert.Equal("old-key", vault.Value);
  }

  [Fact]
  public async Task ConnectResolvesCustomBaseUrlModelAndKey()
  {
    var vault = new FakeVault { Value = "vault-key" };
    AgentConnectionOptions? observed = null;
    using var controller = new AgentEditorController(NewStore(), vault, options =>
    {
      observed = options;
      return new AgentRuntime(new FixedProvider("connected"));
    });
    await controller.SaveAsync(CustomSettings());

    await controller.ConnectAsync();

    Assert.True(controller.IsConnected);
    Assert.Equal("vault-key", observed?.ApiKey);
    Assert.Equal("https://local.example/v1", observed?.BaseUrl);
    Assert.Equal("local-model", observed?.Model);
  }

  [Fact]
  public async Task ReconnectReleasesPreviousAuditLockAndReplacesSession()
  {
    var vault = new FakeVault { Value = "vault-key" };
    using var controller = NewController(vault);
    await controller.SaveAsync(CustomSettings());
    await controller.ConnectAsync();

    await controller.ConnectAsync();

    Assert.True(controller.IsConnected);
    Assert.True(File.Exists(Path.Combine(
      _projectPath, "Library", "AnityAgent", "Audit", "tool-audit.lock")));
  }

  [Fact]
  public async Task SendCommitsUserAndAssistantTranscript()
  {
    var vault = new FakeVault { Value = "vault-key" };
    using var controller = NewController(vault, _ => new AgentRuntime(new FixedProvider("answer")));
    await controller.SaveAsync(CustomSettings());
    await controller.ConnectAsync();

    string reply = await controller.SendAsync("question");

    Assert.Equal("answer", reply);
    Assert.Equal(new[] { AgentRole.User, AgentRole.Assistant },
      controller.Transcript.Select(entry => entry.Role));
  }

  [Fact]
  public async Task SendUsesStreamingProviderAndCommitsFinalAssistant()
  {
    var vault = new FakeVault { Value = "vault-key" };
    var provider = new ChunkedStreamingProvider();
    using var controller = NewController(vault, _ => new AgentRuntime(provider));
    await controller.SaveAsync(CustomSettings());
    await controller.ConnectAsync();

    string reply = await controller.SendAsync("question");

    Assert.Equal("hello", reply);
    Assert.Equal(1, provider.StreamCalls);
    Assert.Equal(0, provider.CompleteCalls);
    Assert.Equal(string.Empty, controller.PartialResponse);
    Assert.Equal("hello", controller.Transcript.Last().Content);
  }

  [Fact]
  public async Task FailedSendRollsBackEditorTranscript()
  {
    var vault = new FakeVault { Value = "vault-key" };
    using var controller = NewController(vault, _ => new AgentRuntime(new ThrowingProvider()));
    await controller.SaveAsync(CustomSettings());
    await controller.ConnectAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SendAsync("question"));

    Assert.Empty(controller.Transcript);
  }

  [Fact]
  public async Task DeleteCredentialDisconnectsLiveRuntime()
  {
    var vault = new FakeVault { Value = "vault-key" };
    using var controller = NewController(vault);
    await controller.SaveAsync(CustomSettings());
    await controller.ConnectAsync();
    Assert.True(controller.IsConnected);

    Assert.True(await controller.DeleteCredentialAsync());

    Assert.False(controller.IsConnected);
    Assert.Null(vault.Value);
  }

  [Fact]
  public async Task SettingsPermissionPolicyFailsClosedForAskWithoutWindowPrompt()
  {
    AgentEditorSettings settings = CustomSettings();
    settings.DefaultToolPermission = AgentToolPermission.Ask;
    settings.ToolPermissions.Clear();
    AgentToolPermissionPolicy policy = settings.CreatePermissionPolicy();
    var runtime = new AgentRuntime();
    var request = new AgentToolAuthorizationRequest(
      new AgentToolCall("id", "unknown_tool", "{}"), runtime.CreateSession());

    Assert.False(await policy.AuthorizeAsync(request));
  }

  [Fact]
  public void EditorHostCatalogAndMenuExposeAnityAgentWindow()
  {
    var host = new EditorHost();
    Assert.Contains("Anity Agent", host.GetWindowCatalog());
    Assert.Contains("Window/Anity/Agent", host.GetMenus());
  }

  [Fact]
  public async Task EditorHostCanOpenAndCloseAnityAgentWindow()
  {
    var host = new EditorHost();
    var session = await host.StartSessionAsync(_projectPath);
    Assert.True(host.OpenWindow("Anity Agent"));
    Assert.Contains("Anity Agent", host.DumpOpenWindows(), StringComparison.Ordinal);
    Assert.NotNull(await host.StopAsync(session.SessionId));
  }

  [Fact]
  public async Task DisposedControllerRejectsFurtherOperations()
  {
    var controller = NewController(new FakeVault { Value = "vault-key" });
    controller.Dispose();
    await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.ConnectAsync());
  }

  private AgentEditorSettingsStore NewStore() => new(_projectPath);

  private AgentEditorController NewController(
    FakeVault vault,
    Func<AgentConnectionOptions, AgentRuntime>? factory = null)
    => new(NewStore(), vault, factory ?? (_ => new AgentRuntime(new FixedProvider("ok"))));

  private static AgentEditorSettings CustomSettings() => new()
  {
    Connection = new AgentConnectionProfile
    {
      BaseUrl = "https://local.example/v1",
      Model = "local-model",
      CredentialId = "project-key",
      Timeout = TimeSpan.FromSeconds(12),
      MaxRetries = 1,
      MaxResponseBytes = 8192
    },
    DefaultToolPermission = AgentToolPermission.Ask,
    ToolPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
      ["echo"] = AgentToolPermission.Allow,
      ["systeminfo"] = AgentToolPermission.Deny
    }
  };

  public void Dispose()
  {
    try { if (Directory.Exists(_projectPath)) Directory.Delete(_projectPath, true); }
    catch { }
  }

  private sealed class FakeVault : IAgentCredentialVault
  {
    public string BackendName => "test vault";
    public bool IsAvailable => true;
    public string? Value { get; set; }
    public string? LastStoredId { get; private set; }

    public Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      LastStoredId = credentialId;
      Value = secret;
      return Task.CompletedTask;
    }

    public Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult(Value);
    }

    public Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      bool existed = Value is not null;
      Value = null;
      return Task.FromResult(existed);
    }
  }

  private sealed class FixedProvider : IAgentProvider
  {
    private readonly string _response;
    public FixedProvider(string response) => _response = response;
    public Task<string> CompleteAsync(
      IReadOnlyList<AgentMessage> messages,
      CancellationToken cancellationToken = default) => Task.FromResult(_response);
  }

  private sealed class ThrowingProvider : IAgentProvider
  {
    public Task<string> CompleteAsync(
      IReadOnlyList<AgentMessage> messages,
      CancellationToken cancellationToken = default)
      => Task.FromException<string>(new InvalidOperationException("provider failed"));
  }

  private sealed class ChunkedStreamingProvider : IStreamingAgentProvider
  {
    public int CompleteCalls { get; private set; }
    public int StreamCalls { get; private set; }

    public Task<string> CompleteAsync(
      IReadOnlyList<AgentMessage> messages,
      CancellationToken cancellationToken = default)
    {
      CompleteCalls++;
      return Task.FromResult("non-stream");
    }

    public async IAsyncEnumerable<AgentStreamUpdate> StreamAsync(
      IReadOnlyList<AgentMessage> messages,
      [System.Runtime.CompilerServices.EnumeratorCancellation]
      CancellationToken cancellationToken = default)
    {
      StreamCalls++;
      await Task.Yield();
      yield return new AgentStreamUpdate("hel");
      yield return new AgentStreamUpdate("lo");
      yield return new AgentStreamUpdate(isCompleted: true);
    }
  }
}
