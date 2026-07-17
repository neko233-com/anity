using System.Security.Cryptography;
using System.Text;
using Anity.Agent;
using Xunit;

namespace Anity.Agent.Tests;

public sealed class AgentAuditTests
{
    [Fact]
    public void AuditEventContainsDigestAndSizesButNoRawArgumentsProperty()
    {
        using var runtime = new AgentRuntime();
        AgentSession session = runtime.CreateSession("audit-session");
        var call = new AgentToolCall("call-1", "echo", "{\"secret\":\"value\"}");

        AgentToolAuditEvent audit = AgentToolAuditEvent.Create(
            AgentToolAuditPhase.Requested, AgentToolAuditOutcome.Pending,
            session, call);

        string expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(call.ArgumentsJson))).ToLowerInvariant();
        Assert.Equal(expected, audit.ArgumentsSha256);
        Assert.Equal(Encoding.UTF8.GetByteCount(call.ArgumentsJson), audit.ArgumentsBytes);
        Assert.DoesNotContain(typeof(AgentToolAuditEvent).GetProperties(),
            property => property.Name.Contains("ArgumentsJson", StringComparison.Ordinal));
    }

    [Fact]
    public void AuditEventRejectsNonUtcTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new AgentToolAuditEvent(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)),
            AgentToolAuditPhase.Requested,
            AgentToolAuditOutcome.Pending, "session", "call", "tool",
            new string('0', 64), 0, 0, 0));
    }

    [Fact]
    public void RuntimeRejectsInvalidAuditModeAndOversizedSessionId()
    {
        using var runtime = new AgentRuntime();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            runtime.AuditFailureMode = (AgentAuditFailureMode)99);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            runtime.CreateSession(new string('s', 129)));
    }

    [Fact]
    public async Task SuccessfulToolWritesRequestedAndSucceededEvents()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "{}");
        var tool = new ConfigurableTool(_ => Task.FromResult("ok"));
        using var runtime = NewRuntime(provider, sink, tool);

        AgentMessage reply = await runtime.CreateSession("success").RunTurnAsync("run");

        Assert.Equal("final", reply.Content);
        Assert.Equal(1, tool.Invocations);
        Assert.Equal(
            new[] { AgentToolAuditOutcome.Pending, AgentToolAuditOutcome.Succeeded },
            sink.Events.Select(entry => entry.Outcome));
        Assert.Equal(2, sink.Events.Count);
    }

    [Fact]
    public async Task DeniedToolIsAuditedWithoutExecution()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "{}");
        var tool = new ConfigurableTool(_ => Task.FromResult("ok"));
        using var runtime = NewRuntime(provider, sink, tool);
        runtime.ToolAuthorizationPolicy = new AgentToolPermissionPolicy(AgentToolPermission.Deny);

        await runtime.CreateSession("denied").RunTurnAsync("run");

        Assert.Equal(0, tool.Invocations);
        Assert.Equal(AgentToolAuditOutcome.Denied, sink.Events.Last().Outcome);
        Assert.Contains("denied", provider.ToolResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidArgumentsHaveStructuredAuditOutcome()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "not-json");
        var tool = new ConfigurableTool(_ => Task.FromResult("ok"));
        using var runtime = NewRuntime(provider, sink, tool);

        await runtime.CreateSession("invalid").RunTurnAsync("run");

        Assert.Equal(0, tool.Invocations);
        Assert.Equal(AgentToolAuditOutcome.InvalidArguments, sink.Events.Last().Outcome);
    }

    [Fact]
    public async Task UnavailableToolHasStructuredAuditOutcome()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("not_registered", "{}");
        using var runtime = NewRuntime(
            provider, sink, new ConfigurableTool(_ => Task.FromResult("ok")));

        await runtime.CreateSession("unavailable").RunTurnAsync("run");

        Assert.Equal(AgentToolAuditOutcome.Unavailable, sink.Events.Last().Outcome);
        Assert.Contains("unavailable", provider.ToolResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToolExceptionIsConvertedAndAuditedAsToolError()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "{}");
        using var runtime = NewRuntime(provider, sink,
            new ConfigurableTool(_ => Task.FromException<string>(new InvalidOperationException("boom"))));

        await runtime.CreateSession("error").RunTurnAsync("run");

        Assert.Equal(AgentToolAuditOutcome.ToolError, sink.Events.Last().Outcome);
        Assert.Contains("boom", provider.ToolResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedToolResultIsBoundedAndAuditedAsError()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "{}");
        using var runtime = NewRuntime(provider, sink,
            new ConfigurableTool(_ => Task.FromResult(new string('x', 70 * 1024))));

        await runtime.CreateSession("large-result").RunTurnAsync("run");

        Assert.Equal(AgentToolAuditOutcome.ToolError, sink.Events.Last().Outcome);
        Assert.Equal("error: remote tool result exceeds 64 KiB", provider.ToolResult);
        Assert.True(sink.Events.Last().ResultBytes < 1024);
    }

    [Fact]
    public async Task TimeoutIsAuditedAndModelCanRecover()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "{}");
        using var runtime = NewRuntime(provider, sink,
            new ConfigurableTool(async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "never";
            }));
        runtime.RemoteToolTimeout = TimeSpan.FromMilliseconds(25);

        AgentMessage reply = await runtime.CreateSession("timeout").RunTurnAsync("run");

        Assert.Equal("final", reply.Content);
        Assert.Equal(AgentToolAuditOutcome.TimedOut, sink.Events.Last().Outcome);
    }

    [Fact]
    public async Task CallerCancellationIsAuditedAndHistoryRemainsAtomic()
    {
        var sink = new RecordingAuditSink();
        var provider = new ToolProvider("audit_tool", "{}");
        using var runtime = NewRuntime(provider, sink,
            new ConfigurableTool(async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "never";
            }));
        AgentSession session = runtime.CreateSession("canceled");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunTurnAsync("run", cancellation.Token));

        Assert.Empty(session.History);
        Assert.Equal(AgentToolAuditOutcome.Canceled, sink.Events.Last().Outcome);
    }

    [Fact]
    public async Task FailClosedAuditStopsToolBeforeExecution()
    {
        var provider = new ToolProvider("audit_tool", "{}");
        var tool = new ConfigurableTool(_ => Task.FromResult("ok"));
        using var runtime = new AgentRuntime(provider)
        {
            ToolAuditSink = new ThrowingAuditSink(),
            AuditFailureMode = AgentAuditFailureMode.FailClosed
        };
        runtime.Tools.Register(tool);
        AgentSession session = runtime.CreateSession("audit-fail");

        await Assert.ThrowsAsync<AgentAuditException>(() => session.RunTurnAsync("run"));

        Assert.Equal(0, tool.Invocations);
        Assert.Empty(session.History);
    }

    [Fact]
    public async Task ContinueAuditModeAllowsExecutionWhenSinkFails()
    {
        var provider = new ToolProvider("audit_tool", "{}");
        var tool = new ConfigurableTool(_ => Task.FromResult("ok"));
        using var runtime = new AgentRuntime(provider)
        {
            ToolAuditSink = new ThrowingAuditSink(),
            AuditFailureMode = AgentAuditFailureMode.Continue
        };
        runtime.Tools.Register(tool);

        AgentMessage reply = await runtime.CreateSession("audit-continue").RunTurnAsync("run");

        Assert.Equal("final", reply.Content);
        Assert.Equal(1, tool.Invocations);
    }

    [Fact]
    public async Task NonStreamingToolTurnsRetainFinishReasonAndUsage()
    {
        var provider = new ToolProvider("audit_tool", "{}", includeUsage: true);
        using var runtime = NewRuntime(
            provider, new RecordingAuditSink(),
            new ConfigurableTool(_ => Task.FromResult("ok")));
        AgentSession session = runtime.CreateSession("metadata");

        AgentMessage reply = await session.RunTurnAsync("run");

        Assert.Equal("stop", reply.FinishReason);
        Assert.Equal(9, reply.Usage?.TotalTokens);
        AgentMessage intermediate = session.History.First(message => message.ToolCalls.Count > 0);
        Assert.Equal("tool_calls", intermediate.FinishReason);
        Assert.Equal(3, intermediate.Usage?.TotalTokens);
    }

    [Fact]
    public async Task StreamingTurnRetainsFinishReasonAndUsage()
    {
        using var runtime = new AgentRuntime(new MetadataStreamingProvider());
        AgentSession session = runtime.CreateSession("stream-metadata");

        await foreach (AgentStreamUpdate _ in session.RunTurnStreamAsync("hello")) { }

        AgentMessage reply = session.History.Last();
        Assert.Equal("stop", reply.FinishReason);
        Assert.Equal(12, reply.Usage?.TotalTokens);
    }

    private static AgentRuntime NewRuntime(
        ToolProvider provider, IAgentToolAuditSink sink, ConfigurableTool tool)
    {
        var runtime = new AgentRuntime(provider) { ToolAuditSink = sink };
        runtime.Tools.Register(tool);
        return runtime;
    }

    private sealed class RecordingAuditSink : IAgentToolAuditSink
    {
        private readonly List<AgentToolAuditEvent> _events = new();
        public IReadOnlyList<AgentToolAuditEvent> Events
        {
            get { lock (_events) return _events.ToArray(); }
        }

        public Task WriteAsync(
            AgentToolAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            lock (_events) _events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditSink : IAgentToolAuditSink
    {
        public Task WriteAsync(
            AgentToolAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
            => Task.FromException(new IOException("audit unavailable"));
    }

    private sealed class ConfigurableTool : IRemoteAgentTool
    {
        private readonly Func<CancellationToken, Task<string>> _invoke;
        public string Name => "audit_tool";
        public string Description => "audit test";
        public string ParametersJsonSchema => "{\"type\":\"object\"}";
        public int Invocations { get; private set; }

        public ConfigurableTool(Func<CancellationToken, Task<string>> invoke) => _invoke = invoke;
        public string Invoke(string args, AgentSession session) => "local";
        public Task<string> InvokeRemoteAsync(
            string argumentsJson, AgentSession session,
            CancellationToken cancellationToken = default)
        {
            Invocations++;
            return _invoke(cancellationToken);
        }
    }

    private sealed class ToolProvider : IToolCallingAgentProvider
    {
        private readonly string _toolName;
        private readonly string _arguments;
        private readonly bool _includeUsage;
        private int _round;
        public string ToolResult { get; private set; } = string.Empty;

        public ToolProvider(string toolName, string arguments, bool includeUsage = false)
        {
            _toolName = toolName;
            _arguments = arguments;
            _includeUsage = includeUsage;
        }

        public Task<string> CompleteAsync(
            IReadOnlyList<AgentMessage> messages,
            CancellationToken cancellationToken = default) => Task.FromResult("unused");

        public Task<AgentModelTurn> CompleteWithToolsAsync(
            IReadOnlyList<AgentMessage> messages,
            IReadOnlyList<AgentToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _round) == 1)
                return Task.FromResult(new AgentModelTurn(
                    string.Empty,
                    new[] { new AgentToolCall("call-1", _toolName, _arguments) },
                    "tool_calls",
                    _includeUsage ? new AgentTokenUsage(1, 2, 3) : null));
            ToolResult = messages.Last(message => message.Role == AgentRole.Tool).Content;
            return Task.FromResult(new AgentModelTurn(
                "final", finishReason: "stop",
                usage: _includeUsage ? new AgentTokenUsage(4, 5, 9) : null));
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

    private sealed class MetadataStreamingProvider : IStreamingAgentProvider
    {
        public Task<string> CompleteAsync(
            IReadOnlyList<AgentMessage> messages,
            CancellationToken cancellationToken = default) => Task.FromResult("unused");

        public async IAsyncEnumerable<AgentStreamUpdate> StreamAsync(
            IReadOnlyList<AgentMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new AgentStreamUpdate("hello");
            yield return new AgentStreamUpdate(
                usage: new AgentTokenUsage(7, 5, 12),
                isCompleted: true,
                finishReason: "stop");
        }
    }
}
