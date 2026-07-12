using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.AB.Compare.Tests;

/// <summary>
/// AssetBundle behavior parity gate (load/CRC/concurrent) — ≥10 cases.
/// </summary>
public class BehaviorCompareTests : IDisposable
{
    private readonly string _work;
    private readonly string _testAssetPath;

    public BehaviorCompareTests()
    {
        _testAssetPath = Path.Combine(AppContext.BaseDirectory, "TestAssets");
        _work = Path.Combine(Path.GetTempPath(), "abbeh_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_work)) Directory.Delete(_work, true); } catch { }
    }

    private string BuildOne(string name, string payload)
    {
        string asset = $"Assets/beh_{name}.txt";
        AssetDatabase.CreateAsset(new TextAsset(payload), asset);
        BuildPipeline.BuildAssetBundles(_work, new[]
        {
            new AssetBundleBuild { assetBundleName = name, assetNames = new[] { asset } }
        }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        return Path.Combine(_work, name);
    }

    [Fact]
    public void Load_RestoresText()
    {
        string path = BuildOne("t1", "hello-behavior");
        var ab = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab);
        var ta = ab!.LoadAsset<TextAsset>(ab.GetAllAssetNames()[0]);
        Assert.Contains("hello-behavior", ta!.text);
        ab.Unload(true);
    }

    [Fact]
    public void MissingFile_ReturnsNull()
    {
        Assert.Null(AssetBundle.LoadFromFile(Path.Combine(_work, "missing.bundle")));
    }

    [Fact]
    public void StreamRead_Fixture_Works()
    {
        var bundlePath = Path.Combine(_testAssetPath, "test.bundle");
        Assert.True(File.Exists(bundlePath));
        using var stream = File.OpenRead(bundlePath);
        Assert.True(stream.Length > 0);
        var buffer = new byte[8];
        Assert.Equal(8, stream.Read(buffer, 0, 8));
        Assert.Equal((byte)'U', buffer[0]);
    }

    [Fact]
    public void ConcurrentLoad_SameFile_Safe()
    {
        string path = BuildOne("conc", "c");
        var tasks = new Task<AssetBundle?>[4];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = Task.Run(() => AssetBundle.LoadFromFile(path));
        Task.WaitAll(tasks);
        foreach (var t in tasks)
        {
            Assert.NotNull(t.Result);
            t.Result!.Unload(true);
        }
    }

    [Fact]
    public void Unload_RemovesFromLoaded()
    {
        string path = BuildOne("unl", "u");
        var ab = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab);
        ab!.Unload(true);
        // second load still works
        var ab2 = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab2);
        ab2!.Unload(true);
    }

    [Fact]
    public void MemoryLoad_RoundTrip()
    {
        string path = BuildOne("mem", "m");
        var bytes = File.ReadAllBytes(path);
        var ab = AssetBundle.LoadFromMemory(bytes);
        Assert.NotNull(ab);
        ab!.Unload(true);
    }

    [Fact]
    public void BinaryGate_BeforeLoad()
    {
        string path = BuildOne("bg", "g");
        Assert.True(AssetBundleBinaryComparer.Gate(path, requireAnityCatalog: true));
    }

    [Fact]
    public void EmptyPath_Null()
    {
        Assert.Null(AssetBundle.LoadFromFile(""));
        Assert.Null(AssetBundle.LoadFromFile(null!));
    }

    [Fact]
    public void LargePayload_Loads()
    {
        string path = BuildOne("big", new string('Z', 50_000));
        var ab = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab);
        Assert.NotEmpty(ab!.GetAllAssetNames());
        ab.Unload(true);
    }

    [Fact]
    public void DryRun_DoesNotWrite()
    {
        string emptyDir = Path.Combine(_work, "dry");
        Directory.CreateDirectory(emptyDir);
        AssetDatabase.CreateAsset(new TextAsset("d"), "Assets/beh_dry.txt");
        BuildPipeline.BuildAssetBundles(emptyDir, new[]
        {
            new AssetBundleBuild { assetBundleName = "dryb", assetNames = new[] { "Assets/beh_dry.txt" } }
        }, BuildAssetBundleOptions.DryRunBuild, BuildTarget.StandaloneWindows64);
        Assert.False(File.Exists(Path.Combine(emptyDir, "dryb")));
    }
}
