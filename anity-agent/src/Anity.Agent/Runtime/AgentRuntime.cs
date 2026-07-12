using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Anity.Agent;

/// <summary>
/// Official Anity Agent runtime — independent extension package (like Unity UGUI).
/// Does not live inside Anity.Core engine assembly.
/// </summary>
public sealed class AgentRuntime
{
    private static readonly Lazy<AgentRuntime> _default = new(() => new AgentRuntime());
    public static AgentRuntime Default => _default.Value;

    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly AgentToolRegistry _tools = new();

    public AgentToolRegistry Tools => _tools;
    public int SessionCount => _sessions.Count;

    public AgentRuntime()
    {
        // Built-in tools (screenshot, scene query hooks)
        _tools.Register(new ScreenshotAgentTool());
        _tools.Register(new EchoAgentTool());
        _tools.Register(new SystemInfoAgentTool());
    }

    public AgentSession CreateSession(string? id = null)
    {
        id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!;
        var session = new AgentSession(this, id);
        _sessions[id] = session;
        return session;
    }

    public AgentSession? GetSession(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _sessions.TryGetValue(id, out var s) ? s : null;
    }

    public bool DestroySession(string id) => _sessions.TryRemove(id, out _);

    public IReadOnlyCollection<string> ListSessionIds() => _sessions.Keys.ToArray();
}

public sealed class AgentSession
{
    private readonly AgentRuntime _runtime;
    private readonly List<AgentMessage> _history = new();
    private readonly AgentMemory _memory = new();

    public string Id { get; }
    public AgentMemory Memory => _memory;
    public IReadOnlyList<AgentMessage> History => _history;
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    public bool IsClosed { get; private set; }

    internal AgentSession(AgentRuntime runtime, string id)
    {
        _runtime = runtime;
        Id = id;
    }

    public AgentMessage RunTurn(string userPrompt, CancellationToken ct = default)
    {
        if (IsClosed) throw new InvalidOperationException("Session closed");
        if (string.IsNullOrWhiteSpace(userPrompt))
            throw new ArgumentException("prompt required", nameof(userPrompt));

        var user = new AgentMessage(AgentRole.User, userPrompt.Trim());
        _history.Add(user);
        _memory.Remember("last_user", userPrompt);

        // Tool routing: prefix "tool:name args"
        string content;
        if (userPrompt.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = userPrompt.Substring(5).Trim();
            var sp = rest.IndexOf(' ');
            string name = sp < 0 ? rest : rest.Substring(0, sp);
            string args = sp < 0 ? string.Empty : rest.Substring(sp + 1);
            content = _runtime.Tools.Invoke(name, args, this);
        }
        else
        {
            content = $"agent-ack: {userPrompt}";
            // attach memory hint
            if (_memory.TryGet("last_user", out var last) && last != userPrompt)
                content += $" | prev={last}";
        }

        var assistant = new AgentMessage(AgentRole.Assistant, content);
        _history.Add(assistant);
        return assistant;
    }

    public Task<AgentMessage> RunTurnAsync(string userPrompt, CancellationToken ct = default)
        => Task.Run(() => RunTurn(userPrompt, ct), ct);

    public void Close() => IsClosed = true;
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
    public DateTime Utc { get; } = DateTime.UtcNow;

    public AgentMessage(AgentRole role, string content)
    {
        Role = role;
        Content = content ?? string.Empty;
    }
}

public sealed class AgentMemory
{
    private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public void Remember(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        _store[key] = value ?? string.Empty;
    }

    public bool TryGet(string key, out string value) => _store.TryGetValue(key, out value!);

    public bool Forget(string key) => _store.Remove(key);

    public int Count => _store.Count;

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_store);
}

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    string Invoke(string args, AgentSession session);
}

public sealed class AgentToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IAgentTool tool)
    {
        if (tool == null || string.IsNullOrEmpty(tool.Name)) return;
        _tools[tool.Name] = tool;
    }

    public bool Unregister(string name) => _tools.Remove(name);

    public bool Contains(string name) => _tools.ContainsKey(name);

    public IReadOnlyCollection<string> Names => _tools.Keys.ToArray();

    public string Invoke(string name, string args, AgentSession session)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return $"error: unknown tool '{name}'";
        try { return tool.Invoke(args ?? string.Empty, session); }
        catch (Exception ex) { return "error: " + ex.Message; }
    }
}

internal sealed class EchoAgentTool : IAgentTool
{
    public string Name => "echo";
    public string Description => "Echo arguments";
    public string Invoke(string args, AgentSession session) => args ?? string.Empty;
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

internal sealed class SystemInfoAgentTool : IAgentTool
{
    public string Name => "systeminfo";
    public string Description => "Return device/graphics summary";
    public string Invoke(string args, AgentSession session)
    {
        return $"device={UnityEngine.SystemInfo.deviceModel}; gfx={UnityEngine.SystemInfo.graphicsDeviceType}; os={UnityEngine.SystemInfo.operatingSystem}";
    }
}
