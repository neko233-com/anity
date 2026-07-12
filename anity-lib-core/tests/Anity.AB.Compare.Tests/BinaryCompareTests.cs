using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.AB.Compare.Tests;

/// <summary>
/// AssetBundle binary gate — UnityFS magic + Anity catalog structure (≥12 cases).
/// CI job ab-compare requires this assembly green.
/// </summary>
public class BinaryCompareTests : IDisposable
{
    private readonly string _testAssetPath;
    private readonly string _work;

    public BinaryCompareTests()
    {
        _testAssetPath = Path.Combine(AppContext.BaseDirectory, "TestAssets");
        _work = Path.Combine(Path.GetTempPath(), "abcmp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_work)) Directory.Delete(_work, true); } catch { }
    }

    [Fact]
    public void TestAssetDirectory_Exists()
    {
        Assert.True(Directory.Exists(_testAssetPath),
            $"Test asset directory not found: {_testAssetPath}");
    }

    [Fact]
    public void Fixture_test_bundle_HasUnityFsMagic()
    {
        var path = Path.Combine(_testAssetPath, "test.bundle");
        Assert.True(File.Exists(path), "test.bundle fixture required");
        Assert.True(AssetBundleBinaryComparer.HasUnityFsMagic(path));
        var r = AssetBundleBinaryComparer.ValidateFile(path);
        Assert.True(r.hasUnityFsMagic);
        Assert.True(r.passed, string.Join("; ", r.errors));
    }

    [Fact]
    public void UnityFsMagic_Bytes_Exact()
    {
        Assert.Equal(8, AssetBundleBinaryComparer.UnityFsMagic.Length);
        Assert.Equal((byte)'U', AssetBundleBinaryComparer.UnityFsMagic[0]);
        Assert.Equal((byte)' ', AssetBundleBinaryComparer.UnityFsMagic[7]);
    }

    [Fact]
    public void AnityBuilt_Uncompressed_PassesGateWithCatalog()
    {
        AssetDatabase.CreateAsset(new TextAsset("gate-payload"), "Assets/abcmp_gate.txt");
        BuildPipeline.BuildAssetBundles(_work, new[]
        {
            new AssetBundleBuild { assetBundleName = "gateb", assetNames = new[] { "Assets/abcmp_gate.txt" } }
        }, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);

        string path = Path.Combine(_work, "gateb");
        Assert.True(File.Exists(path));
        Assert.True(AssetBundleBinaryComparer.Gate(path, requireAnityCatalog: true),
            AssetBundleBinaryComparer.ValidateFile(path).ToString());
        var r = AssetBundleBinaryComparer.ValidateFile(path);
        Assert.True(r.catalogReadable, r.ToString());
        Assert.True(r.assetCount >= 1);
        Assert.StartsWith("55 6E 69 74 79 46 53", AssetBundleBinaryComparer.HexHeader(File.ReadAllBytes(path), 8));
    }

    [Fact]
    public void AnityBuilt_ChunkLz4_PassesGateAfterDecompress()
    {
        AssetDatabase.CreateAsset(new TextAsset("lz4-gate"), "Assets/abcmp_lz4.txt");
        BuildPipeline.BuildAssetBundles(_work, new[]
        {
            new AssetBundleBuild { assetBundleName = "lz4g", assetNames = new[] { "Assets/abcmp_lz4.txt" } }
        }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);

        string path = Path.Combine(_work, "lz4g");
        var r = AssetBundleBinaryComparer.ValidateFile(path);
        Assert.True(r.passed, string.Join("; ", r.errors));
        Assert.True(r.hasUnityFsMagic || r.warnings.Count > 0);
        // load round-trip still works
        var ab = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab);
        ab!.Unload(true);
    }

    [Fact]
    public void Validate_Empty_Fails()
    {
        var r = AssetBundleBinaryComparer.Validate(Array.Empty<byte>());
        Assert.False(r.passed);
        Assert.NotEmpty(r.errors);
    }

    [Fact]
    public void Validate_Garbage_FailsMagic()
    {
        var r = AssetBundleBinaryComparer.Validate(Encoding.ASCII.GetBytes("NOTUNITY"));
        Assert.False(r.hasUnityFsMagic);
        Assert.False(r.passed);
    }

    [Fact]
    public void CompareStructure_BothUnityFs_Passes()
    {
        var a = AssetBundleBinaryComparer.UnityFsMagic;
        var pad = new byte[16];
        Buffer.BlockCopy(a, 0, pad, 0, 8);
        var r = AssetBundleBinaryComparer.CompareStructure(pad, pad);
        Assert.True(r.passed);
    }

    [Fact]
    public void Gate_MissingFile_False()
    {
        Assert.False(AssetBundleBinaryComparer.Gate(Path.Combine(_work, "nope.bundle")));
    }

    [Fact]
    public void HexHeader_Formats()
    {
        var h = AssetBundleBinaryComparer.HexHeader(AssetBundleBinaryComparer.UnityFsMagic);
        Assert.Contains("55", h);
        Assert.Contains("20", h); // space
    }

    [Fact]
    public void OfficialMagic_AndAnityCatalog_DifferBytewise_ButBothValid()
    {
        // fixture = minimal official-style magic
        var fixture = File.ReadAllBytes(Path.Combine(_testAssetPath, "test.bundle"));
        AssetDatabase.CreateAsset(new TextAsset("x"), "Assets/abcmp_x.txt");
        BuildPipeline.BuildAssetBundles(_work, new[]
        {
            new AssetBundleBuild { assetBundleName = "full", assetNames = new[] { "Assets/abcmp_x.txt" } }
        }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        var anity = File.ReadAllBytes(Path.Combine(_work, "full"));

        Assert.True(AssetBundleBinaryComparer.HasUnityFsMagic(fixture));
        Assert.True(AssetBundleBinaryComparer.HasUnityFsMagic(anity));
        // not byte-identical — structural compare still passes
        var r = AssetBundleBinaryComparer.CompareStructure(fixture, anity);
        Assert.True(r.hasUnityFsMagic);
        Assert.True(r.passed);
    }

    [Fact]
    public void LoadFromFile_AfterGate_Succeeds()
    {
        AssetDatabase.CreateAsset(new TextAsset("load-me"), "Assets/abcmp_load.txt");
        BuildPipeline.BuildAssetBundles(_work, new[]
        {
            new AssetBundleBuild { assetBundleName = "loadb", assetNames = new[] { "Assets/abcmp_load.txt" } }
        }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        string path = Path.Combine(_work, "loadb");
        Assert.True(AssetBundleBinaryComparer.Gate(path, requireAnityCatalog: true));
        var ab = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab);
        Assert.NotEmpty(ab!.GetAllAssetNames());
        ab.Unload(true);
    }
}
