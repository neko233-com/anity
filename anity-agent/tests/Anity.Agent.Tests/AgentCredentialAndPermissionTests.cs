using System.Collections.Concurrent;
using Anity.Agent;
using Xunit;

namespace Anity.Agent.Tests;

public sealed class AgentCredentialAndPermissionTests
{
    [Fact]
    public async Task ProfileResolvesCredentialWithoutPersistingSecretProperty()
    {
        var vault = new FakeVault { Value = "secret-key" };
        var profile = new AgentConnectionProfile
        {
            BaseUrl = "https://example.test/v1/",
            Model = " custom-model ",
            CredentialId = "project.main"
        };

        AgentConnectionOptions options = await profile.ResolveAsync(vault);

        Assert.Equal("secret-key", options.ApiKey);
        Assert.Equal("https://example.test/v1", options.BaseUrl);
        Assert.Equal("custom-model", options.Model);
        Assert.Equal("project.main", vault.LastRetrievedId);
        Assert.DoesNotContain("secret-key", profile.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingCredentialFailsClosed()
    {
        var profile = new AgentConnectionProfile { CredentialId = "missing" };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => profile.ResolveAsync(new FakeVault()));
        Assert.Contains("was not found", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedVaultCredentialIsRejectedBeforeHttpUse()
    {
        var profile = new AgentConnectionProfile();
        var vault = new FakeVault { Value = new string('a', 2049) };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => profile.ResolveAsync(vault));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("space key")]
    [InlineData("slash/key")]
    [InlineData("")]
    public void CredentialIdRejectsUnsafeValues(string credentialId)
    {
        var profile = new AgentConnectionProfile { CredentialId = credentialId };
        Assert.ThrowsAny<ArgumentException>(() => profile.Validate());
    }

    [Fact]
    public void UnsupportedProviderIsRejected()
    {
        var profile = new AgentConnectionProfile { ProviderId = "unknown" };
        Assert.Throws<ArgumentException>(() => profile.Validate());
    }

    [Fact]
    public async Task PermissionPolicyDefaultsToDeny()
    {
        var policy = new AgentToolPermissionPolicy();
        bool allowed = await policy.AuthorizeAsync(NewRequest("echo"));
        Assert.False(allowed);
    }

    [Fact]
    public async Task ExplicitAllowSkipsPrompt()
    {
        bool prompted = false;
        var policy = new AgentToolPermissionPolicy
        {
            PromptAsync = (_, _) => { prompted = true; return Task.FromResult(false); }
        };
        policy.SetPermission("echo", AgentToolPermission.Allow);

        Assert.True(await policy.AuthorizeAsync(NewRequest("echo")));
        Assert.False(prompted);
    }

    [Fact]
    public async Task AskWithoutPromptFailsClosed()
    {
        var policy = new AgentToolPermissionPolicy(AgentToolPermission.Ask);
        Assert.False(await policy.AuthorizeAsync(NewRequest("echo")));
    }

    [Fact]
    public async Task AskUsesPromptDecision()
    {
        AgentToolAuthorizationRequest? observed = null;
        var policy = new AgentToolPermissionPolicy(AgentToolPermission.Ask)
        {
            PromptAsync = (request, _) =>
            {
                observed = request;
                return Task.FromResult(true);
            }
        };

        Assert.True(await policy.AuthorizeAsync(NewRequest("systeminfo")));
        Assert.Equal("systeminfo", observed?.Call.Name);
    }

    [Fact]
    public async Task AuthorizationCancellationPropagates()
    {
        var policy = new AgentToolPermissionPolicy(AgentToolPermission.Ask)
        {
            PromptAsync = async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return true;
            }
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => policy.AuthorizeAsync(NewRequest("echo"), cancellation.Token));
    }

    [Fact]
    public void PermissionSnapshotIsIndependent()
    {
        var policy = new AgentToolPermissionPolicy();
        policy.SetPermission("echo", AgentToolPermission.Allow);
        IReadOnlyDictionary<string, AgentToolPermission> first = policy.Snapshot();
        policy.SetPermission("echo", AgentToolPermission.Deny);
        Assert.Equal(AgentToolPermission.Allow, first["echo"]);
        Assert.Equal(AgentToolPermission.Deny, policy.GetPermission("echo"));
    }

    [Fact]
    public async Task RuntimeDeniedToolReturnsErrorWithoutInvokingTool()
    {
        var provider = new OneToolProvider();
        using var runtime = new AgentRuntime(provider)
        {
            ToolAuthorizationPolicy = new AgentToolPermissionPolicy(AgentToolPermission.Deny)
        };
        var tool = new CountingRemoteTool();
        runtime.Tools.Register(tool);

        AgentMessage result = await runtime.CreateSession().RunTurnAsync("run it");

        Assert.Equal("recovered", result.Content);
        Assert.Equal(0, tool.InvokeCount);
        Assert.Contains("denied", provider.ToolResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeAllowedToolInvokesExactlyOnce()
    {
        var provider = new OneToolProvider();
        var policy = new AgentToolPermissionPolicy();
        policy.SetPermission("secure_tool", AgentToolPermission.Allow);
        using var runtime = new AgentRuntime(provider) { ToolAuthorizationPolicy = policy };
        var tool = new CountingRemoteTool();
        runtime.Tools.Register(tool);

        AgentMessage result = await runtime.CreateSession().RunTurnAsync("run it");

        Assert.Equal("recovered", result.Content);
        Assert.Equal(1, tool.InvokeCount);
        Assert.Equal("ok", provider.ToolResult);
    }

    private static AgentToolAuthorizationRequest NewRequest(string name)
    {
        var runtime = new AgentRuntime();
        AgentSession session = runtime.CreateSession();
        return new AgentToolAuthorizationRequest(
            new AgentToolCall("call-1", name, "{}"), session);
    }

    private sealed class FakeVault : IAgentCredentialVault
    {
        public string BackendName => "fake vault";
        public bool IsAvailable => true;
        public string? Value { get; set; }
        public string? LastRetrievedId { get; private set; }

        public Task StoreAsync(string credentialId, string secret, CancellationToken cancellationToken = default)
        {
            Value = secret;
            return Task.CompletedTask;
        }

        public Task<string?> RetrieveAsync(string credentialId, CancellationToken cancellationToken = default)
        {
            LastRetrievedId = credentialId;
            return Task.FromResult(Value);
        }

        public Task<bool> DeleteAsync(string credentialId, CancellationToken cancellationToken = default)
        {
            bool existed = Value is not null;
            Value = null;
            return Task.FromResult(existed);
        }
    }

    private sealed class CountingRemoteTool : IRemoteAgentTool
    {
        public string Name => "secure_tool";
        public string Description => "secure";
        public string ParametersJsonSchema => "{\"type\":\"object\"}";
        public int InvokeCount { get; private set; }
        public string Invoke(string args, AgentSession session) => "local";
        public Task<string> InvokeRemoteAsync(
            string argumentsJson, AgentSession session,
            CancellationToken cancellationToken = default)
        {
            InvokeCount++;
            return Task.FromResult("ok");
        }
    }

    private sealed class OneToolProvider : IToolCallingAgentProvider
    {
        private int _round;
        public string? ToolResult { get; private set; }

        public Task<string> CompleteAsync(
            IReadOnlyList<AgentMessage> messages,
            CancellationToken cancellationToken = default)
            => Task.FromResult("unused");

        public Task<AgentModelTurn> CompleteWithToolsAsync(
            IReadOnlyList<AgentMessage> messages,
            IReadOnlyList<AgentToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _round) == 1)
                return Task.FromResult(new AgentModelTurn(
                    string.Empty,
                    new[] { new AgentToolCall("call-1", "secure_tool", "{}") },
                    "tool_calls"));
            ToolResult = messages.Last(message => message.Role == AgentRole.Tool).Content;
            return Task.FromResult(new AgentModelTurn("recovered", finishReason: "stop"));
        }

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
            yield return new AgentStreamUpdate("unused", isCompleted: true);
        }
    }
}
