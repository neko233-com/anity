using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Anity.Agent;

/// <summary>
/// Official Anity Agent runtime — independent extension package (like Unity UGUI).
/// Does not live inside Anity.Core engine assembly.
/// </summary>
public sealed class AgentRuntime : IDisposable
{
    private static readonly Lazy<AgentRuntime> _default = new(() => new AgentRuntime());
    public static AgentRuntime Default => _default.Value;

    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly AgentToolRegistry _tools = new();
    private readonly IAgentProvider? _provider;
    private TimeSpan _remoteToolTimeout = TimeSpan.FromSeconds(30);
    private IAgentToolAuthorizationPolicy _toolAuthorizationPolicy =
        AllowAllAgentToolAuthorizationPolicy.Instance;
    private IAgentToolAuditSink _toolAuditSink = NullAgentToolAuditSink.Instance;
    private AgentAuditFailureMode _auditFailureMode = AgentAuditFailureMode.FailClosed;

    public AgentToolRegistry Tools => _tools;
    public int SessionCount => _sessions.Count;
    public IAgentToolAuthorizationPolicy ToolAuthorizationPolicy
    {
        get => _toolAuthorizationPolicy;
        set => _toolAuthorizationPolicy = value
            ?? throw new ArgumentNullException(nameof(value));
    }
    public IAgentToolAuditSink ToolAuditSink
    {
        get => _toolAuditSink;
        set => _toolAuditSink = value ?? throw new ArgumentNullException(nameof(value));
    }
    public AgentAuditFailureMode AuditFailureMode
    {
        get => _auditFailureMode;
        set
        {
            if (!Enum.IsDefined(typeof(AgentAuditFailureMode), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            _auditFailureMode = value;
        }
    }
    public TimeSpan RemoteToolTimeout
    {
        get => _remoteToolTimeout;
        set
        {
            if (value <= TimeSpan.Zero || value > TimeSpan.FromMinutes(10))
                throw new ArgumentOutOfRangeException(nameof(value));
            _remoteToolTimeout = value;
        }
    }

    public AgentRuntime()
        : this(null)
    {
    }

    public AgentRuntime(IAgentProvider? provider)
    {
        _provider = provider;
        // Built-in tools (screenshot, scene query hooks)
        _tools.Register(new ScreenshotAgentTool());
        _tools.Register(new EchoAgentTool());
        _tools.Register(new SystemInfoAgentTool());
    }

    public AgentRuntime(AgentConnectionOptions options, System.Net.Http.HttpClient? httpClient = null)
        : this(new OpenAiCompatibleAgentProvider(options, httpClient))
    {
    }

    public AgentSession CreateSession(string? id = null)
    {
        id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!;
        if (id.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(id),
                "Agent session id must be at most 128 characters.");
        if (id.Any(char.IsControl))
            throw new ArgumentException(
                "Agent session id cannot contain control characters.", nameof(id));
        return _sessions.GetOrAdd(id, key => new AgentSession(this, key));
    }

    public AgentSession? GetSession(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _sessions.TryGetValue(id, out var s) ? s : null;
    }

    public bool DestroySession(string id) => _sessions.TryRemove(id, out _);

    public IReadOnlyCollection<string> ListSessionIds() => _sessions.Keys.ToArray();

    internal IAgentProvider? Provider => _provider;

    public void Dispose()
    {
        foreach (var session in _sessions.Values) session.Close();
        _sessions.Clear();
        if (_provider is IDisposable disposable) disposable.Dispose();
        if (_toolAuditSink is IDisposable auditDisposable) auditDisposable.Dispose();
    }
}

public sealed class AgentSession
{
    private readonly AgentRuntime _runtime;
    private readonly List<AgentMessage> _history = new();
    private readonly AgentMemory _memory = new();
    private readonly SemaphoreSlim _turnGate = new(1, 1);
    private int _closed;

    public string Id { get; }
    public AgentMemory Memory => _memory;
    public IReadOnlyList<AgentMessage> History
    {
        get
        {
            lock (_history) return _history.ToArray();
        }
    }
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    internal AgentSession(AgentRuntime runtime, string id)
    {
        _runtime = runtime;
        Id = id;
    }

    public AgentMessage RunTurn(string userPrompt, CancellationToken ct = default)
        => RunTurnAsync(userPrompt, ct).GetAwaiter().GetResult();

    public async Task<AgentMessage> RunTurnAsync(string userPrompt, CancellationToken ct = default)
    {
        string prompt = ValidatePrompt(userPrompt);

        await _turnGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsClosed) throw new InvalidOperationException("Session closed");
            ct.ThrowIfCancellationRequested();

            var user = new AgentMessage(AgentRole.User, prompt);
            IReadOnlyList<AgentMessage> request = SnapshotWith(user);
            IReadOnlyList<AgentToolDefinition> remoteTools = _runtime.Tools.RemoteDefinitions;
            if (_runtime.Provider is IToolCallingAgentProvider toolProvider
                && remoteTools.Count > 0
                && !prompt.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            {
                ToolTurnResult result = await ResolveToolCallingTurnAsync(
                    user, request, toolProvider, remoteTools, ct).ConfigureAwait(false);
                CommitMessages(result.Messages, user);
                return result.FinalAssistant;
            }
            string content = await ResolveTurnAsync(prompt, request, ct).ConfigureAwait(false);

            var assistant = new AgentMessage(AgentRole.Assistant, content);
            CommitTurn(user, assistant);
            return assistant;
        }
        finally
        {
            _turnGate.Release();
        }
    }

    public async IAsyncEnumerable<AgentStreamUpdate> RunTurnStreamAsync(
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string prompt = ValidatePrompt(userPrompt);
        await _turnGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsClosed) throw new InvalidOperationException("Session closed");
            ct.ThrowIfCancellationRequested();

            var user = new AgentMessage(AgentRole.User, prompt);
            IReadOnlyList<AgentMessage> request = SnapshotWith(user);
            IReadOnlyList<AgentToolDefinition> remoteTools = _runtime.Tools.RemoteDefinitions;
            if (_runtime.Provider is IToolCallingAgentProvider toolProvider
                && remoteTools.Count > 0
                && !prompt.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            {
                await foreach (AgentStreamUpdate update in RunToolCallingStreamAsync(
                    user, request, toolProvider, remoteTools, ct).ConfigureAwait(false))
                    yield return update;
                yield break;
            }
            if (_runtime.Provider is not IStreamingAgentProvider streaming
                || prompt.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            {
                string content = await ResolveTurnAsync(prompt, request, ct)
                    .ConfigureAwait(false);
                CommitTurn(user, new AgentMessage(AgentRole.Assistant, content));
                yield return new AgentStreamUpdate(content, isCompleted: true);
                yield break;
            }

            var contentBuilder = new StringBuilder();
            AgentTokenUsage? usage = null;
            string finishReason = string.Empty;
            await foreach (AgentStreamUpdate update in streaming.StreamAsync(request, ct)
                .ConfigureAwait(false))
            {
                if (update.ContentDelta.Length > 0)
                    contentBuilder.Append(update.ContentDelta);
                if (update.Usage is not null) usage = update.Usage;
                if (update.FinishReason.Length > 0) finishReason = update.FinishReason;
                if (update.IsCompleted)
                {
                    CommitTurn(user, new AgentMessage(
                        AgentRole.Assistant, contentBuilder.ToString(),
                        finishReason: finishReason, usage: usage));
                    yield return update;
                    yield break;
                }
                yield return update;
            }

            CommitTurn(user, new AgentMessage(
                AgentRole.Assistant, contentBuilder.ToString(),
                finishReason: finishReason, usage: usage));
            yield return new AgentStreamUpdate(
                usage: usage, isCompleted: true, finishReason: finishReason);
        }
        finally
        {
            _turnGate.Release();
        }
    }

    private static string ValidatePrompt(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            throw new ArgumentException("prompt required", nameof(userPrompt));
        return userPrompt.Trim();
    }

    private IReadOnlyList<AgentMessage> SnapshotWith(AgentMessage user)
    {
        lock (_history)
        {
            var result = new AgentMessage[_history.Count + 1];
            _history.CopyTo(result, 0);
            result[result.Length - 1] = user;
            return result;
        }
    }

    private async Task<string> ResolveTurnAsync(
        string prompt, IReadOnlyList<AgentMessage> request,
        CancellationToken cancellationToken)
    {
        if (prompt.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
        {
            string rest = prompt.Substring(5).Trim();
            int separator = rest.IndexOf(' ');
            string name = separator < 0 ? rest : rest.Substring(0, separator);
            string args = separator < 0 ? string.Empty : rest.Substring(separator + 1);
            return _runtime.Tools.Invoke(name, args, this);
        }
        if (_runtime.Provider != null)
            return await _runtime.Provider.CompleteAsync(request, cancellationToken)
                .ConfigureAwait(false);
        return $"agent-ack: {prompt}";
    }

    private async Task<ToolTurnResult> ResolveToolCallingTurnAsync(
        AgentMessage user, IReadOnlyList<AgentMessage> request,
        IToolCallingAgentProvider provider,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        var conversation = request.ToList();
        var turnMessages = new List<AgentMessage> { user };
        var seenToolCallIds = new HashSet<string>(StringComparer.Ordinal);
        int totalToolCalls = 0;
        for (int round = 0; round < 8; ++round)
        {
            AgentModelTurn turn = await provider.CompleteWithToolsAsync(
                conversation, tools, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AgentToolCall> calls =
                AgentToolCallAssembler.ValidateComplete(turn.ToolCalls);
            EnsureNewToolCallIds(calls, seenToolCallIds);
            var assistant = new AgentMessage(
                AgentRole.Assistant, turn.Content, toolCalls: calls,
                finishReason: turn.FinishReason, usage: turn.Usage);
            conversation.Add(assistant);
            turnMessages.Add(assistant);
            if (calls.Count == 0)
                return new ToolTurnResult(turnMessages, assistant);

            totalToolCalls += calls.Count;
            if (totalToolCalls > 32)
                throw new AgentProviderException(
                    "Agent exceeded the per-turn tool-call limit.");
            foreach (AgentToolCall call in calls)
            {
                string result = await InvokeRemoteToolWithTimeoutAsync(
                    call, cancellationToken).ConfigureAwait(false);
                var toolMessage = new AgentMessage(
                    AgentRole.Tool, result, call.Id, call.Name);
                conversation.Add(toolMessage);
                turnMessages.Add(toolMessage);
            }
        }
        throw new AgentProviderException(
            "Agent exceeded the maximum of 8 tool-call rounds.");
    }

    private async IAsyncEnumerable<AgentStreamUpdate> RunToolCallingStreamAsync(
        AgentMessage user, IReadOnlyList<AgentMessage> request,
        IToolCallingAgentProvider provider,
        IReadOnlyList<AgentToolDefinition> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversation = request.ToList();
        var turnMessages = new List<AgentMessage> { user };
        var seenToolCallIds = new HashSet<string>(StringComparer.Ordinal);
        int totalToolCalls = 0;
        for (int round = 0; round < 8; ++round)
        {
            var assembler = new AgentToolCallAssembler();
            var content = new StringBuilder();
            AgentStreamUpdate? completion = null;
            await foreach (AgentStreamUpdate update in provider.StreamWithToolsAsync(
                conversation, tools, cancellationToken).ConfigureAwait(false))
            {
                if (update.ContentDelta.Length > 0)
                    content.Append(update.ContentDelta);
                if (update.ToolCallDeltas.Count > 0)
                    assembler.Apply(update.ToolCallDeltas);
                if (update.IsCompleted)
                {
                    completion = update;
                    break;
                }
                yield return update;
            }

            IReadOnlyList<AgentToolCall> calls = assembler.Complete();
            EnsureNewToolCallIds(calls, seenToolCallIds);
            var assistant = new AgentMessage(
                AgentRole.Assistant, content.ToString(), toolCalls: calls,
                finishReason: completion?.FinishReason,
                usage: completion?.Usage);
            conversation.Add(assistant);
            turnMessages.Add(assistant);
            if (calls.Count == 0)
            {
                CommitMessages(turnMessages, user);
                yield return completion ?? new AgentStreamUpdate(isCompleted: true);
                yield break;
            }

            totalToolCalls += calls.Count;
            if (totalToolCalls > 32)
                throw new AgentProviderException(
                    "Agent exceeded the per-turn tool-call limit.");
            foreach (AgentToolCall call in calls)
            {
                string result = await InvokeRemoteToolWithTimeoutAsync(
                    call, cancellationToken).ConfigureAwait(false);
                var toolMessage = new AgentMessage(
                    AgentRole.Tool, result, call.Id, call.Name);
                conversation.Add(toolMessage);
                turnMessages.Add(toolMessage);
            }
        }
        throw new AgentProviderException(
            "Agent exceeded the maximum of 8 tool-call rounds.");
    }

    private static void EnsureNewToolCallIds(
        IReadOnlyList<AgentToolCall> calls, HashSet<string> seenIds)
    {
        foreach (AgentToolCall call in calls)
            if (!seenIds.Add(call.Id))
                throw new AgentProviderException(
                    "Agent reused a tool-call id across multiple rounds.");
    }

    private async Task<string> InvokeRemoteToolWithTimeoutAsync(
        AgentToolCall call, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await WriteAuditAsync(AgentToolAuditEvent.Create(
            AgentToolAuditPhase.Requested, AgentToolAuditOutcome.Pending,
            this, call)).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(_runtime.RemoteToolTimeout);
        try
        {
            bool authorized = await _runtime.ToolAuthorizationPolicy.AuthorizeAsync(
                new AgentToolAuthorizationRequest(call, this), timeout.Token)
                .ConfigureAwait(false);
            if (!authorized)
            {
                string denied = $"error: remote tool '{call.Name}' was denied by the Anity permission policy";
                await WriteCompletedAuditAsync(
                    call, AgentToolAuditOutcome.Denied, denied, stopwatch)
                    .ConfigureAwait(false);
                return denied;
            }
            AgentRemoteToolResult result = await _runtime.Tools.InvokeRemoteWithResultAsync(
                call, this, timeout.Token).ConfigureAwait(false);
            await WriteCompletedAuditAsync(call, result.Outcome, result.Content, stopwatch)
                .ConfigureAwait(false);
            return result.Content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            string timedOut = $"error: remote tool timed out after {_runtime.RemoteToolTimeout.TotalSeconds:0.###} seconds";
            await WriteCompletedAuditAsync(
                call, AgentToolAuditOutcome.TimedOut, timedOut, stopwatch)
                .ConfigureAwait(false);
            return timedOut;
        }
        catch (OperationCanceledException)
        {
            await WriteCompletedAuditAsync(
                call, AgentToolAuditOutcome.Canceled, string.Empty, stopwatch)
                .ConfigureAwait(false);
            throw;
        }
        catch (AgentAuditException)
        {
            throw;
        }
        catch
        {
            await WriteCompletedAuditAsync(
                call, AgentToolAuditOutcome.AuthorizationError,
                string.Empty, stopwatch).ConfigureAwait(false);
            throw;
        }
    }

    private Task WriteCompletedAuditAsync(
        AgentToolCall call,
        AgentToolAuditOutcome outcome,
        string result,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        int resultBytes = Math.Min(
            Encoding.UTF8.GetByteCount(result ?? string.Empty), 4 * 1024 * 1024);
        long duration = Math.Min(stopwatch.ElapsedMilliseconds, 10 * 60 * 1000L);
        return WriteAuditAsync(AgentToolAuditEvent.Create(
            AgentToolAuditPhase.Completed, outcome, this, call,
            resultBytes, duration));
    }

    private async Task WriteAuditAsync(AgentToolAuditEvent auditEvent)
    {
        try
        {
            await _runtime.ToolAuditSink.WriteAsync(
                auditEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_runtime.AuditFailureMode == AgentAuditFailureMode.FailClosed)
                throw new AgentAuditException(
                    "Anity Agent tool audit could not be recorded; execution was stopped.", ex);
        }
    }

    private void CommitTurn(AgentMessage user, AgentMessage assistant)
    {
        lock (_history)
        {
            if (IsClosed) throw new InvalidOperationException("Session closed");
            _history.Add(user);
            _history.Add(assistant);
        }
        _memory.Remember("last_user", user.Content);
    }

    private void CommitMessages(
        IReadOnlyList<AgentMessage> messages, AgentMessage user)
    {
        lock (_history)
        {
            if (IsClosed) throw new InvalidOperationException("Session closed");
            _history.AddRange(messages);
        }
        _memory.Remember("last_user", user.Content);
    }

    public void Close()
    {
        lock (_history) Volatile.Write(ref _closed, 1);
    }

    private sealed class ToolTurnResult
    {
        public IReadOnlyList<AgentMessage> Messages { get; }
        public AgentMessage FinalAssistant { get; }

        public ToolTurnResult(
            IReadOnlyList<AgentMessage> messages,
            AgentMessage finalAssistant)
        {
            Messages = messages;
            FinalAssistant = finalAssistant;
        }
    }
}

public enum AgentRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3
}

public sealed class AgentMessage
{
    public AgentRole Role { get; }
    public string Content { get; }
    public string? ToolCallId { get; }
    public string? Name { get; }
    public IReadOnlyList<AgentToolCall> ToolCalls { get; }
    public string FinishReason { get; }
    public AgentTokenUsage? Usage { get; }
    public DateTime Utc { get; } = DateTime.UtcNow;

    public AgentMessage(
        AgentRole role, string content, string? toolCallId = null,
        string? name = null, IReadOnlyList<AgentToolCall>? toolCalls = null,
        string? finishReason = null, AgentTokenUsage? usage = null)
    {
        Role = role;
        Content = content ?? string.Empty;
        ToolCallId = toolCallId;
        Name = name;
        ToolCalls = toolCalls?.ToArray() ?? Array.Empty<AgentToolCall>();
        FinishReason = finishReason ?? string.Empty;
        Usage = usage;
    }
}

public sealed class AgentMemory
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public void Remember(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        _store[key] = value ?? string.Empty;
    }

    public bool TryGet(string key, out string value) => _store.TryGetValue(key, out value!);

    public bool Forget(string key) => _store.TryRemove(key, out _);

    public int Count => _store.Count;

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_store);
}

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    string Invoke(string args, AgentSession session);
}

/// <summary>Explicit opt-in for tools that a remote model may invoke.</summary>
public interface IRemoteAgentTool : IAgentTool
{
    string ParametersJsonSchema { get; }
    Task<string> InvokeRemoteAsync(
        string argumentsJson, AgentSession session,
        CancellationToken cancellationToken = default);
}

public sealed class AgentToolRegistry
{
    private readonly ConcurrentDictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IAgentTool tool)
    {
        if (tool == null || string.IsNullOrEmpty(tool.Name)) return;
        if (tool is IRemoteAgentTool remote)
            _ = new AgentToolDefinition(
                remote.Name, remote.Description, remote.ParametersJsonSchema);
        _tools[tool.Name] = tool;
    }

    public bool Unregister(string name) => _tools.TryRemove(name, out _);

    public bool Contains(string name) => _tools.ContainsKey(name);

    public IReadOnlyCollection<string> Names => _tools.Keys.ToArray();

    public IReadOnlyList<AgentToolDefinition> RemoteDefinitions
        => _tools.Values.OfType<IRemoteAgentTool>()
            .Select(tool => new AgentToolDefinition(
                tool.Name, tool.Description, tool.ParametersJsonSchema))
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray();

    public string Invoke(string name, string args, AgentSession session)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return $"error: unknown tool '{name}'";
        try { return tool.Invoke(args ?? string.Empty, session); }
        catch (Exception ex) { return "error: " + ex.Message; }
    }

    public async Task<string> InvokeRemoteAsync(
        AgentToolCall call, AgentSession session,
        CancellationToken cancellationToken = default)
        => (await InvokeRemoteWithResultAsync(
            call, session, cancellationToken).ConfigureAwait(false)).Content;

    internal async Task<AgentRemoteToolResult> InvokeRemoteWithResultAsync(
        AgentToolCall call, AgentSession session,
        CancellationToken cancellationToken = default)
    {
        if (Encoding.UTF8.GetByteCount(call.ArgumentsJson) > 64 * 1024)
            return new AgentRemoteToolResult(
                "error: tool arguments exceed 64 KiB",
                AgentToolAuditOutcome.InvalidArguments);
        if (!_tools.TryGetValue(call.Name, out IAgentTool? tool)
            || tool is not IRemoteAgentTool remote)
            return new AgentRemoteToolResult(
                $"error: remote tool '{call.Name}' is unavailable",
                AgentToolAuditOutcome.Unavailable);
        try
        {
            using JsonDocument arguments = JsonDocument.Parse(
                call.ArgumentsJson,
                new JsonDocumentOptions { MaxDepth = 64 });
            if (arguments.RootElement.ValueKind != JsonValueKind.Object)
                return new AgentRemoteToolResult(
                    "error: tool arguments must be a JSON object",
                    AgentToolAuditOutcome.InvalidArguments);
        }
        catch (JsonException)
        {
            return new AgentRemoteToolResult(
                "error: invalid tool arguments JSON",
                AgentToolAuditOutcome.InvalidArguments);
        }
        try
        {
            string content = await remote.InvokeRemoteAsync(
                call.ArgumentsJson, session, cancellationToken).ConfigureAwait(false);
            content ??= string.Empty;
            if (Encoding.UTF8.GetByteCount(content) > 64 * 1024)
                return new AgentRemoteToolResult(
                    "error: remote tool result exceeds 64 KiB",
                    AgentToolAuditOutcome.ToolError);
            return new AgentRemoteToolResult(
                content,
                content.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
                    ? AgentToolAuditOutcome.ToolError
                    : AgentToolAuditOutcome.Succeeded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AgentRemoteToolResult(
                "error: " + ex.Message,
                AgentToolAuditOutcome.ToolError);
        }
    }
}

internal sealed class AgentRemoteToolResult
{
    public string Content { get; }
    public AgentToolAuditOutcome Outcome { get; }

    public AgentRemoteToolResult(string content, AgentToolAuditOutcome outcome)
    {
        Content = content ?? string.Empty;
        Outcome = outcome;
    }
}

internal sealed class AgentToolCallAssembler
{
    private const int MaxToolCalls = 16;
    private const int MaxArgumentsBytes = 64 * 1024;
    private readonly SortedDictionary<int, Builder> _builders = new();

    public void Apply(IReadOnlyList<AgentToolCallDelta> deltas)
    {
        foreach (AgentToolCallDelta delta in deltas)
        {
            if (delta.Index >= MaxToolCalls)
                throw new AgentProviderException("Agent returned too many tool calls in one turn.");
            if (!_builders.TryGetValue(delta.Index, out Builder? builder))
            {
                builder = new Builder();
                _builders.Add(delta.Index, builder);
            }
            builder.Id.Append(delta.IdDelta);
            builder.Name.Append(delta.NameDelta);
            builder.Arguments.Append(delta.ArgumentsDelta);
            if (builder.Id.Length > 128 || builder.Name.Length > 64
                || Encoding.UTF8.GetByteCount(builder.Arguments.ToString()) > MaxArgumentsBytes)
                throw new AgentProviderException("Agent tool-call metadata exceeded its configured limit.");
        }
    }

    public IReadOnlyList<AgentToolCall> Complete()
    {
        var calls = new List<AgentToolCall>(_builders.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int expectedIndex = 0;
        foreach ((int index, Builder builder) in _builders)
        {
            if (index != expectedIndex++)
                throw new AgentProviderException("Agent tool-call indices were not contiguous.");
            string id = builder.Id.ToString();
            string name = builder.Name.ToString();
            string arguments = builder.Arguments.Length == 0
                ? "{}" : builder.Arguments.ToString();
            ValidateIdentity(id, name, ids);
            calls.Add(new AgentToolCall(id, name, arguments));
        }
        return calls;
    }

    public static IReadOnlyList<AgentToolCall> ValidateComplete(
        IReadOnlyList<AgentToolCall> calls)
    {
        if (calls.Count > MaxToolCalls)
            throw new AgentProviderException("Agent returned too many tool calls in one turn.");
        var result = new List<AgentToolCall>(calls.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (AgentToolCall call in calls)
        {
            ValidateIdentity(call.Id, call.Name, ids);
            if (Encoding.UTF8.GetByteCount(call.ArgumentsJson) > MaxArgumentsBytes)
                throw new AgentProviderException("Agent tool arguments exceeded 64 KiB.");
            result.Add(call);
        }
        return result;
    }

    private static void ValidateIdentity(
        string id, string name, HashSet<string> ids)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id))
            throw new AgentProviderException("Agent returned an invalid or duplicate tool-call id.");
        _ = new AgentToolDefinition(name, string.Empty, "{}");
    }

    private sealed class Builder
    {
        public StringBuilder Id { get; } = new();
        public StringBuilder Name { get; } = new();
        public StringBuilder Arguments { get; } = new();
    }
}

internal sealed class EchoAgentTool : IRemoteAgentTool
{
    public string Name => "echo";
    public string Description => "Echo arguments";
    public string ParametersJsonSchema =>
        "{\"type\":\"object\",\"properties\":{\"args\":{\"type\":\"string\"}},\"required\":[\"args\"],\"additionalProperties\":false}";
    public string Invoke(string args, AgentSession session) => args ?? string.Empty;
    public Task<string> InvokeRemoteAsync(
        string argumentsJson, AgentSession session,
        CancellationToken cancellationToken = default)
    {
        using JsonDocument document = JsonDocument.Parse(argumentsJson);
        string value = document.RootElement.TryGetProperty("args", out JsonElement args)
            && args.ValueKind == JsonValueKind.String
            ? args.GetString() ?? string.Empty
            : string.Empty;
        return Task.FromResult(value);
    }
}

internal sealed class ScreenshotAgentTool : IAgentTool
{
    public string Name => "screenshot";
    public string Description => "Capture screenshot to path (args = filename)";
    public string Invoke(string args, AgentSession session)
    {
        string path = string.IsNullOrWhiteSpace(args) ? "agent_capture.png" : args.Trim();
        UnityEngine.ScreenCapture.CaptureScreenshot(path, 1);
        session.Memory.Remember("last_screenshot", UnityEngine.ScreenCapture.lastCapturePath);
        return UnityEngine.ScreenCapture.lastCapturePath;
    }
}

internal sealed class SystemInfoAgentTool : IRemoteAgentTool
{
    public string Name => "systeminfo";
    public string Description => "Return device/graphics summary";
    public string ParametersJsonSchema =>
        "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
    public string Invoke(string args, AgentSession session)
    {
        return $"device={UnityEngine.SystemInfo.deviceModel}; gfx={UnityEngine.SystemInfo.graphicsDeviceType}; os={UnityEngine.SystemInfo.operatingSystem}";
    }
    public Task<string> InvokeRemoteAsync(
        string argumentsJson, AgentSession session,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Invoke(string.Empty, session));
}
