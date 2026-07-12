using Anity.Agent;
using Xunit;

namespace Anity.Agent.Tests;

/// <summary>Anity.Agent official extension — ≥10 cases (independent of Core engine assembly).</summary>
public class AgentRuntimeTests
{
    [Fact]
    public void Default_Runtime_NotNull()
    {
        Assert.NotNull(AgentRuntime.Default);
    }

    [Fact]
    public void CreateSession_UniqueIds()
    {
        var a = AgentRuntime.Default.CreateSession();
        var b = AgentRuntime.Default.CreateSession();
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void CreateSession_WithId()
    {
        var s = AgentRuntime.Default.CreateSession("fixed-id");
        Assert.Equal("fixed-id", s.Id);
        Assert.Same(s, AgentRuntime.Default.GetSession("fixed-id"));
    }

    [Fact]
    public void RunTurn_Empty_Throws()
    {
        var s = AgentRuntime.Default.CreateSession();
        Assert.ThrowsAny<ArgumentException>(() => s.RunTurn(" "));
    }

    [Fact]
    public void RunTurn_Ack()
    {
        var s = AgentRuntime.Default.CreateSession();
        var m = s.RunTurn("hello agent");
        Assert.Equal(AgentRole.Assistant, m.Role);
        Assert.Contains("hello agent", m.Content);
        Assert.Equal(2, s.History.Count);
    }

    [Fact]
    public void RunTurn_ToolEcho()
    {
        var s = AgentRuntime.Default.CreateSession();
        var m = s.RunTurn("tool:echo ping");
        Assert.Equal("ping", m.Content);
    }

    [Fact]
    public void RunTurn_UnknownTool()
    {
        var s = AgentRuntime.Default.CreateSession();
        var m = s.RunTurn("tool:nope");
        Assert.Contains("unknown tool", m.Content);
    }

    [Fact]
    public void Tool_Screenshot_CreatesFile()
    {
        var s = AgentRuntime.Default.CreateSession();
        string name = $"agent_sc_{Guid.NewGuid():N}.png";
        var m = s.RunTurn("tool:screenshot " + name);
        Assert.True(File.Exists(m.Content) || File.Exists(UnityEngine.ScreenCapture.lastCapturePath));
        if (File.Exists(UnityEngine.ScreenCapture.lastCapturePath))
            File.Delete(UnityEngine.ScreenCapture.lastCapturePath);
    }

    [Fact]
    public void Tool_SystemInfo()
    {
        var s = AgentRuntime.Default.CreateSession();
        var m = s.RunTurn("tool:systeminfo");
        Assert.Contains("device=", m.Content);
    }

    [Fact]
    public void Memory_Remember_TryGet()
    {
        var mem = new AgentMemory();
        mem.Remember("k", "v");
        Assert.True(mem.TryGet("k", out var v));
        Assert.Equal("v", v);
        Assert.True(mem.Forget("k"));
        Assert.Equal(0, mem.Count);
    }

    [Fact]
    public void DestroySession()
    {
        var s = AgentRuntime.Default.CreateSession();
        Assert.True(AgentRuntime.Default.DestroySession(s.Id));
        Assert.Null(AgentRuntime.Default.GetSession(s.Id));
    }

    [Fact]
    public void ClosedSession_Throws()
    {
        var s = AgentRuntime.Default.CreateSession();
        s.Close();
        Assert.Throws<InvalidOperationException>(() => s.RunTurn("x"));
    }

    [Fact]
    public void Tools_Register_Custom()
    {
        var rt = new AgentRuntime();
        rt.Tools.Register(new CustomTool());
        Assert.True(rt.Tools.Contains("custom"));
        var s = rt.CreateSession();
        Assert.Equal("ok", s.RunTurn("tool:custom").Content);
    }

    private sealed class CustomTool : IAgentTool
    {
        public string Name => "custom";
        public string Description => "c";
        public string Invoke(string args, AgentSession session) => "ok";
    }
}
