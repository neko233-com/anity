using System;
using System.IO;
using Anity.Core.Runtime.Il2Cpp;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>IL2CPP full link + player launch — ≥12 cases.</summary>
public class Il2CppPlayerTests : IDisposable
{
    private readonly string _dir;

    public Il2CppPlayerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "il2player_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void BuildPlayer_CreatesManagedMarker()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        Assert.True(File.Exists(Path.Combine(_dir, "player.managed")));
        Assert.True(File.Exists(Path.Combine(_dir, "PlayerMain.cpp")));
        Assert.True(File.Exists(Path.Combine(_dir, "player.manifest.json")));
        Assert.True(Il2CppPlayerHost.IsPlayerReady(_dir));
    }

    [Fact]
    public void BuildPlayer_EmitsMetadata()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        Assert.True(File.Exists(Path.Combine(_dir, "Il2CppMetadata.map")));
        Assert.True(File.Exists(Path.Combine(_dir, "link.xml")));
        Assert.True(File.Exists(Path.Combine(_dir, "CMakeLists.txt")));
    }

    [Fact]
    public void LaunchManaged_Succeeds()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        Assert.True(Il2CppPlayerHost.LaunchManaged(_dir));
        Assert.Equal(0, Il2CppPlayerHost.lastExitCode);
        Assert.True(Il2CppRuntime.IsIl2Cpp);
        Assert.Contains("managed player", Il2CppPlayerHost.lastLaunchLog);
    }

    [Fact]
    public void Launch_UsesManagedWhenNoNative()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        // remove any accidental native binary
        string exe = Il2CppToolchain.GetPlayerExecutablePath(_dir);
        if (File.Exists(exe)) File.Delete(exe);
        Assert.True(Il2CppPlayerHost.Launch(_dir));
        Assert.Equal(0, Il2CppPlayerHost.lastExitCode);
    }

    [Fact]
    public void LinkPlayer_DoesNotThrowWithoutCompiler()
    {
        File.WriteAllText(Path.Combine(_dir, "Il2CppBootstrap.cpp"),
            "#include \"il2cpp-config.h\"\nvoid anity_il2cpp_bootstrap(){}\n");
        File.WriteAllText(Path.Combine(_dir, "il2cpp-config.h"), "#pragma once\n");
        // may return false if no compiler — must not throw
        _ = Il2CppToolchain.LinkPlayer(_dir);
        Assert.NotNull(Il2CppToolchain.lastCompileLog);
    }

    [Fact]
    public void BuildAndLink_ThenLaunch()
    {
        // tryNativeLink true — uses compiler if present
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, Il2CppToolchain.TargetAbi.WinX64, tryNativeLink: true));
        Assert.True(Il2CppPlayerHost.IsPlayerReady(_dir));
        Assert.True(Il2CppPlayerHost.Launch(_dir));
        Assert.Equal(0, Il2CppPlayerHost.lastExitCode);
    }

    [Fact]
    public void GetPlayerExecutablePath_HasName()
    {
        string p = Il2CppToolchain.GetPlayerExecutablePath(_dir);
        Assert.Contains("AnityIl2CppPlayer", p);
    }

    [Fact]
    public void IsPlayerReady_FalseWhenEmpty()
    {
        Assert.False(Il2CppPlayerHost.IsPlayerReady(Path.Combine(_dir, "empty")));
    }

    [Fact]
    public void Launch_MissingDir_False()
    {
        Assert.False(Il2CppPlayerHost.Launch(Path.Combine(_dir, "nope")));
    }

    [Fact]
    public void PlayerMain_ContainsMain()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        string main = File.ReadAllText(Path.Combine(_dir, "PlayerMain.cpp"));
        Assert.Contains("int main", main);
        Assert.Contains("anity_il2cpp_bootstrap", main);
    }

    [Fact]
    public void CMake_HasExecutableTarget()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        // re-emit after PlayerMain exists
        Il2CppToolchain.EmitToolchainFiles(_dir);
        string cmake = File.ReadAllText(Path.Combine(_dir, "CMakeLists.txt"));
        Assert.Contains("AnityIl2CppPlayer", cmake);
    }

    [Fact]
    public void CompileAllUnits_NonNegative()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, tryNativeLink: false));
        int n = Il2CppToolchain.CompileAllUnits(_dir);
        Assert.True(n >= 0);
    }

    [Fact]
    public void Abi_Android_PlayerManifest()
    {
        Assert.True(Il2CppPlayerHost.BuildPlayer(_dir, Il2CppToolchain.TargetAbi.AndroidArm64, tryNativeLink: false));
        string man = File.ReadAllText(Path.Combine(_dir, "player.manifest.json"));
        Assert.Contains("AndroidArm64", man);
        Assert.Contains("IL2CPP", man);
    }
}
