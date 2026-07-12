using Anity.Cli;
using Xunit;

namespace Anity.Cli.Tests;

/// <summary>CLI args — ≥10 cases / Unity-compatible switches.</summary>
public class CommandLineArgsTests
{
    [Fact]
    public void Parse_Empty_SetsHelp()
    {
        var a = CommandLineArgs.Parse(Array.Empty<string>());
        Assert.True(a.Help);
    }

    [Fact]
    public void Parse_BatchMode_Quit()
    {
        var a = CommandLineArgs.Parse(new[] { "-batchmode", "-quit" });
        Assert.True(a.BatchMode);
        Assert.True(a.Quit);
    }

    [Fact]
    public void Parse_ProjectPath()
    {
        var a = CommandLineArgs.Parse(new[] { "-projectPath", "." });
        Assert.False(string.IsNullOrEmpty(a.ProjectPath));
        Assert.True(Directory.Exists(a.ProjectPath));
    }

    [Fact]
    public void Parse_ExecuteMethod()
    {
        var a = CommandLineArgs.Parse(new[] { "-executeMethod", "Foo.Bar" });
        Assert.Equal("Foo.Bar", a.ExecuteMethod);
    }

    [Fact]
    public void Parse_BuildTarget()
    {
        var a = CommandLineArgs.Parse(new[] { "-buildTarget", "Android" });
        Assert.Equal("Android", a.BuildTarget);
    }

    [Fact]
    public void Parse_BuildWindows64Player()
    {
        var a = CommandLineArgs.Parse(new[] { "-buildWindows64Player", "out/game.exe" });
        Assert.Equal("out/game.exe", a.BuildWindows64Player);
    }

    [Fact]
    public void Parse_RunTests_TestResults()
    {
        var a = CommandLineArgs.Parse(new[] { "-runTests", "-testResults", "r.xml", "-testFilter", "Foo" });
        Assert.True(a.RunTests);
        Assert.Equal("r.xml", a.TestResults);
        Assert.Equal("Foo", a.TestFilter);
    }

    [Fact]
    public void Parse_Il2Cpp_Screenshot_Agent()
    {
        var a = CommandLineArgs.Parse(new[] { "-il2cpp", "-il2cppOutput", "tmp", "-screenshot", "a.png", "-screenshotSuperSize", "2", "-agent", "-agentPrompt", "hi" });
        Assert.True(a.Il2Cpp);
        Assert.Equal("tmp", a.Il2CppOutput);
        Assert.Equal("a.png", a.Screenshot);
        Assert.Equal(2, a.ScreenshotSuperSize);
        Assert.True(a.Agent);
        Assert.Equal("hi", a.AgentPrompt);
    }

    [Fact]
    public void Parse_NoGraphics_SilentCrashes()
    {
        var a = CommandLineArgs.Parse(new[] { "-nographics", "-silent-crashes" });
        Assert.True(a.NoGraphics);
        Assert.True(a.SilentCrashes);
    }

    [Fact]
    public void Parse_Version_Help()
    {
        Assert.True(CommandLineArgs.Parse(new[] { "-version" }).Version);
        Assert.True(CommandLineArgs.Parse(new[] { "-help" }).Help);
    }

    [Fact]
    public void HelpText_ContainsBatchmode()
    {
        Assert.Contains("-batchmode", CommandLineArgs.HelpText);
        Assert.Contains("-executeMethod", CommandLineArgs.HelpText);
        Assert.Contains("-il2cpp", CommandLineArgs.HelpText);
    }

    [Fact]
    public void CliHost_Version_Exit0()
    {
        var host = new CliHost();
        int code = host.Run(new[] { "-version" });
        Assert.Equal(0, code);
        Assert.Contains("Anity", host.LogText);
    }

    [Fact]
    public void CliHost_BatchmodeQuit_Exit0()
    {
        var host = new CliHost();
        int code = host.Run(new[] { "-batchmode", "-quit", "-nographics" });
        Assert.Equal(0, code);
        Assert.Contains("batchmode=1", host.LogText);
    }

    [Fact]
    public void CliHost_Il2Cpp_Package_AndLaunch()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "cli_il2_" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = new CliHost();
            int code = host.Run(new[] { "-batchmode", "-quit", "-nographics", "-il2cpp", "-il2cppOutput", outDir });
            Assert.Equal(0, code);
            Assert.Contains("il2cpp=1", host.LogText);
            Assert.Contains("il2cppOutput=", host.LogText);
            Assert.True(File.Exists(Path.Combine(outDir, "link.xml")));
            Assert.True(File.Exists(Path.Combine(outDir, "Il2CppMetadata.map")));
            Assert.True(File.Exists(Path.Combine(outDir, "PlayerMain.cpp")));
            Assert.True(
                File.Exists(Path.Combine(outDir, "player.managed"))
                || File.Exists(Path.Combine(outDir, "AnityIl2CppPlayer.exe"))
                || File.Exists(Path.Combine(outDir, "AnityIl2CppPlayer")));
            Assert.Contains("launchOk=True", host.LogText);
        }
        finally
        {
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void CliHost_BuildWindows64_WithIl2Cpp_Packages()
    {
        string root = Path.Combine(Path.GetTempPath(), "cli_bp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string player = Path.Combine(root, "Game.exe");
        try
        {
            var host = new CliHost();
            int code = host.Run(new[]
            {
                "-batchmode", "-quit", "-nographics",
                "-il2cpp",
                "-buildWindows64Player", player
            });
            Assert.Equal(0, code);
            Assert.True(Directory.Exists(Path.Combine(root, "Il2CppOutputProject"))
                        || host.LogText.Contains("il2cppPlayerPackage="));
            Assert.True(File.Exists(player + ".il2cpp.json") || host.LogText.Contains("il2cppPlayerPackage="));
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }
}
