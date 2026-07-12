using System;
using System.IO;
using Anity.Core.Runtime.Il2Cpp;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>
/// End-to-end IL2CPP packaging path (shipped Il2CppPackagePipeline) — ≥12 cases.
/// </summary>
public class Il2CppPackagePipelineTests : IDisposable
{
    private readonly string _dir;

    public Il2CppPackagePipelineTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "il2pkg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Package_ProducesRequiredArtifacts()
    {
        var r = Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false);
        Assert.True(r.success, r.error);
        Assert.True(r.hasLinkXml);
        Assert.True(r.hasMetadata);
        Assert.True(r.hasConfigHeader);
        Assert.True(r.hasPlayerMain);
        Assert.True(r.hasManagedMarker);
        Assert.True(File.Exists(Path.Combine(_dir, "package.report.txt")));
    }

    [Fact]
    public void Package_AndLaunch_ExitZero_Il2CppActive()
    {
        var r = Il2CppPackagePipeline.Package(_dir, tryNativeLink: true, launch: true);
        Assert.True(r.success, r.error + "\n" + r.log);
        Assert.True(r.launchOk);
        Assert.Equal(0, r.exitCode);
        Assert.True(Il2CppRuntime.IsIl2Cpp);
        Assert.True(Il2CppPackagePipeline.lastLaunchOk);
    }

    [Fact]
    public void ValidatePackageLayout_TrueAfterPackage()
    {
        Assert.True(Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false).success);
        Assert.True(Il2CppPackagePipeline.ValidatePackageLayout(_dir));
    }

    [Fact]
    public void ValidatePackageLayout_FalseWhenEmpty()
    {
        Assert.False(Il2CppPackagePipeline.ValidatePackageLayout(Path.Combine(_dir, "nope")));
    }

    [Fact]
    public void Package_EmptyDir_Fails()
    {
        var r = Il2CppPackagePipeline.Package("  ", launch: false);
        Assert.False(r.success);
        Assert.Contains("outputDirectory", r.error);
    }

    [Fact]
    public void Package_NullDir_Fails()
    {
        var r = Il2CppPackagePipeline.Package(null!, launch: false);
        Assert.False(r.success);
    }

    [Fact]
    public void PackageForPlayer_WritesSidecarJson()
    {
        string player = Path.Combine(_dir, "Game.exe");
        var r = Il2CppPackagePipeline.PackageForPlayer(player, tryNativeLink: false, launch: false);
        Assert.True(r.success, r.error);
        Assert.True(File.Exists(player + ".il2cpp.json"));
        string json = File.ReadAllText(player + ".il2cpp.json");
        Assert.Contains("il2cppOutput", json);
        Assert.True(Directory.Exists(Path.Combine(_dir, "Il2CppOutputProject")));
    }

    [Fact]
    public void Package_Metadata_NotEmpty()
    {
        Assert.True(Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false).success);
        var meta = File.ReadAllText(Path.Combine(_dir, "Il2CppMetadata.map"));
        Assert.Contains("types:", meta);
        Assert.True(meta.Length > 20);
    }

    [Fact]
    public void Package_LinkXml_HasLinker()
    {
        Assert.True(Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false).success);
        Assert.Contains("<linker>", File.ReadAllText(Path.Combine(_dir, "link.xml")));
    }

    [Fact]
    public void Package_CMake_HasPlayerTarget()
    {
        Assert.True(Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false).success);
        Il2CppToolchain.EmitToolchainFiles(_dir);
        Assert.Contains("AnityIl2CppPlayer", File.ReadAllText(Path.Combine(_dir, "CMakeLists.txt")));
    }

    [Fact]
    public void Package_LastOutputDirectory_Set()
    {
        Assert.True(Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false).success);
        Assert.Equal(_dir, Il2CppPackagePipeline.lastOutputDirectory);
    }

    [Fact]
    public void Package_ArtifactsList_NonEmpty()
    {
        var r = Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: false);
        Assert.True(r.success);
        Assert.NotEmpty(r.artifacts);
        Assert.Contains("link.xml", r.artifacts);
    }

    [Fact]
    public void Package_Report_ContainsIsIl2Cpp()
    {
        var r = Il2CppPackagePipeline.Package(_dir, tryNativeLink: false, launch: true);
        Assert.True(r.success, r.error);
        Assert.Contains("IsIl2Cpp=", r.log);
        Assert.Contains("True", r.log); // player mode active
    }
}
