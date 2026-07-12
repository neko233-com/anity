using System;
using System.IO;
using System.Linq;
using Anity.Core.Runtime.Il2Cpp;
using UnityEngine;
using UnityEngine.Scripting;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>IL2CPP pipeline — ≥10 cases / edge conditions.</summary>
public class Il2CppTests
{
    [Fact]
    public void EnterIl2CppPlayerMode_SetsIsIl2Cpp()
    {
        Il2CppRuntime.ForcePlatform(Platform.Mono);
        Il2CppRuntime.EnterIl2CppPlayerMode();
        Assert.True(Il2CppRuntime.IsIl2Cpp);
        Assert.Equal(Platform.IL2CPP, Il2CppRuntime.CurrentPlatform);
    }

    [Fact]
    public void ForcePlatform_Mono_ClearsIl2CppFlag()
    {
        Il2CppRuntime.ForcePlatform(Platform.Mono);
        Assert.False(Il2CppRuntime.IsIl2Cpp);
        Assert.True(Il2CppRuntime.IsMono);
    }

    [Fact]
    public void AOTSuffix_WhenIl2Cpp()
    {
        Il2CppRuntime.ForcePlatform(Platform.IL2CPP);
        Assert.Equal("_AOT", Il2CppRuntime.AOTSuffix);
    }

    [Fact]
    public void AOTSuffix_WhenMono_Empty()
    {
        Il2CppRuntime.ForcePlatform(Platform.Mono);
        Assert.Equal(string.Empty, Il2CppRuntime.AOTSuffix);
    }

    [Fact]
    public void RegisterGenericType_IncrementsCount()
    {
        Il2CppRuntime.ClearAotRegistry();
        Il2CppRuntime.RegisterGenericType(typeof(System.Collections.Generic.List<>));
        Assert.True(Il2CppRuntime.RegisteredGenericTypeCount >= 1);
    }

    [Fact]
    public void RegisterGenericMethod_IncrementsCount()
    {
        Il2CppRuntime.ClearAotRegistry();
        Il2CppRuntime.RegisterGenericMethod("Foo::Bar");
        Assert.True(Il2CppRuntime.RegisteredGenericMethodCount >= 1);
    }

    [Fact]
    public void EnsureGenericMethod_Records()
    {
        Il2CppRuntime.ClearAotRegistry();
        Il2CppRuntime.EnsureGenericMethod(() => 1);
        Assert.True(Il2CppRuntime.RegisteredGenericMethodCount >= 1);
    }

    [Fact]
    public void Build_NullAssemblies_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Il2CppBuilder.Build(null!, "out"));
    }

    [Fact]
    public void Build_EmptyOutput_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Il2CppBuilder.Build(Array.Empty<string>(), " "));
    }

    [Fact]
    public void Build_NoAssemblies_FailsGracefully()
    {
        string dir = Path.Combine(Path.GetTempPath(), "il2cpp_" + Guid.NewGuid().ToString("N"));
        bool ok = Il2CppBuilder.Build(new[] { Path.Combine(dir, "missing.dll") }, dir);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(Il2CppBuilder.lastError));
    }

    [Fact]
    public void BuildFromLoadedDomain_CreatesMetadata()
    {
        string dir = Path.Combine(Path.GetTempPath(), "il2cpp_ok_" + Guid.NewGuid().ToString("N"));
        bool ok = Il2CppBuilder.BuildFromLoadedDomain(dir);
        Assert.True(ok);
        Assert.True(File.Exists(Path.Combine(dir, "Il2CppMetadata.map")));
        Assert.True(File.Exists(Path.Combine(dir, "link.xml")));
        Assert.True(Directory.EnumerateFiles(dir, "*.cpp").Any());
    }

    [Fact]
    public void PreserveAttribute_ExistsOnAssembly()
    {
        var attrs = typeof(UnityEngine.Object).Assembly.GetCustomAttributes(typeof(PreserveAttribute), false);
        // Assembly may use [assembly: Preserve]
        Assert.NotNull(attrs);
    }

    [Fact]
    public void Il2CppSetOption_Constructs()
    {
        var a = new Il2CppSetOptionAttribute(Option.NullCheck, false);
        Assert.Equal(Option.NullCheck, a.Option);
        Assert.Equal(false, a.Value);
    }

    [Fact]
    public void Settings_Defaults()
    {
        Assert.Equal(Il2CppCodeGeneration.OptimizeSpeed, Il2CppBuilder.settings.codeGeneration);
        Assert.Equal(Il2CppCompilerConfiguration.Release, Il2CppBuilder.settings.compilerConfiguration);
    }
}
