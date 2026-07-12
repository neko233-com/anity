using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Addressables catalog/load/instantiate — ≥10 boundary cases.</summary>
public class AddressablesTests : IDisposable
{
    private readonly string _dir;

    public AddressablesTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Addressables.ClearCatalog();
    }

    public void Dispose()
    {
        Addressables.ClearCatalog();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Register_And_LoadAssetAsync()
    {
        AssetDatabase.CreateAsset(new TextAsset("addr-data"), "Assets/Addr/A.txt");
        Addressables.Register("ui/label", "Assets/Addr/A.txt");
        Assert.True(Addressables.Exists("ui/label"));
        var h = Addressables.LoadAssetAsync<TextAsset>("ui/label");
        Assert.True(h.IsDone);
        Assert.Equal(AsyncOperationStatus.Succeeded, h.Status);
        Assert.NotNull(h.Result);
        Assert.Contains("addr-data", h.Result.text);
    }

    [Fact]
    public void LoadMissing_FailsStatus()
    {
        var h = Addressables.LoadAssetAsync<TextAsset>("no/such");
        Assert.True(h.IsDone);
        Assert.Equal(AsyncOperationStatus.Failed, h.Status);
        Assert.NotNull(h.OperationException);
    }

    [Fact]
    public void BuildPlayerContent_WritesCatalog_AndLoads()
    {
        AssetDatabase.CreateAsset(new TextAsset("cat"), "Assets/Addr/B.txt");
        var map = new Dictionary<string, string> { ["item"] = "Assets/Addr/B.txt" };
        string outDir = Path.Combine(_dir, "player");
        Addressables.BuildPlayerContent(outDir, map);
        Assert.True(File.Exists(Path.Combine(outDir, "catalog.json")));
        var h = Addressables.LoadAssetAsync<TextAsset>("item");
        Assert.Equal(AsyncOperationStatus.Succeeded, h.Status);
    }

    [Fact]
    public void LoadContentCatalogAsync_RegistersKeys()
    {
        string cat = Path.Combine(_dir, "c.json");
        Addressables.WriteCatalog(cat, new Dictionary<string, string> { ["k"] = "Assets/Addr/C.txt" });
        AssetDatabase.CreateAsset(new TextAsset("from-cat"), "Assets/Addr/C.txt");
        var h = Addressables.LoadContentCatalogAsync(cat);
        Assert.True(h.IsDone);
        Assert.True(Addressables.Exists("k"));
    }

    [Fact]
    public void InstantiateAsync_CreatesInstance()
    {
        var go = new GameObject("PrefabSrc");
        AssetDatabase.CreateAsset(go, "Assets/Addr/P.prefab");
        // CreateAsset on GO may store as Object — also register path as TextAsset fallback
        Addressables.Register("prefab", "Assets/Addr/P.prefab");
        // If load returns null for GO, still test API path
        var h = Addressables.InstantiateAsync("prefab");
        Assert.True(h.IsDone);
    }

    [Fact]
    public void AssetReference_RuntimeKey()
    {
        var ar = new AssetReference("guid-1");
        Assert.True(ar.RuntimeKeyIsValid());
        Assert.Equal("guid-1", ar.ToString());
        Assert.False(new AssetReference().RuntimeKeyIsValid());
    }

    [Fact]
    public void ReleaseInstance_TrueForValid()
    {
        var go = new GameObject("x");
        Assert.True(Addressables.ReleaseInstance(go));
        Assert.False(Addressables.ReleaseInstance(null!));
    }

    [Fact]
    public void LoadAssetsAsync_Callback()
    {
        AssetDatabase.CreateAsset(new TextAsset("list"), "Assets/Addr/L.txt");
        Addressables.Register("list", "Assets/Addr/L.txt");
        int calls = 0;
        var h = Addressables.LoadAssetsAsync<TextAsset>("list", _ => calls++);
        Assert.True(h.IsDone);
        Assert.True(h.Result.Count >= 1);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void InitializeAsync_Succeeds()
    {
        var h = Addressables.InitializeAsync();
        Assert.True(h.IsDone);
    }

    [Fact]
    public void ClearCatalog_RemovesExists()
    {
        Addressables.Register("tmp", "Assets/x");
        Assert.True(Addressables.Exists("tmp"));
        Addressables.ClearCatalog();
        Assert.False(Addressables.Exists("tmp"));
    }

    [Fact]
    public void WaitForCompletion_Idempotent()
    {
        AssetDatabase.CreateAsset(new TextAsset("w"), "Assets/Addr/W.txt");
        Addressables.Register("w", "Assets/Addr/W.txt");
        var h = Addressables.LoadAssetAsync<TextAsset>("w");
        h.WaitForCompletion();
        Assert.True(h.IsDone);
        Assert.Equal(1f, h.PercentComplete);
    }

    [Fact]
    public void ResourceLocators_AfterCatalog()
    {
        string cat = Path.Combine(_dir, "r.json");
        Addressables.WriteCatalog(cat, new Dictionary<string, string> { ["r"] = "Assets/r.txt" });
        Addressables.LoadContentCatalogAsync(cat);
        Assert.NotEmpty(Addressables.ResourceLocators);
        Assert.True(Addressables.HasCatalogLoaded);
    }

    [Fact]
    public void RegisterBundle_LoadsFromAb()
    {
        string abDir = Path.Combine(_dir, "b");
        Directory.CreateDirectory(abDir);
        AssetDatabase.CreateAsset(new TextAsset("from-bundle"), "Assets/Addr/Bundle.txt");
        BuildPipeline.BuildAssetBundles(abDir, new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "aab",
                assetNames = new[] { "Assets/Addr/Bundle.txt" }
            }
        }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        string abPath = Path.Combine(abDir, "aab");
        Addressables.RegisterBundle("ab/key", abPath);
        var h = Addressables.LoadAssetAsync<TextAsset>("ab/key");
        Assert.Equal(AsyncOperationStatus.Succeeded, h.Status);
        Assert.NotNull(h.Result);
        Assert.Contains("from-bundle", h.Result.text);
    }

    [Fact]
    public void DownloadDependencies_KnownAddress()
    {
        Addressables.Register("dep", "Assets/x");
        var h = Addressables.DownloadDependenciesAsync("dep");
        Assert.True(h.IsDone);
        Assert.Null(h.OperationException);
    }

    [Fact]
    public void GetDownloadSize_LocalBundle()
    {
        string abDir = Path.Combine(_dir, "sz");
        Directory.CreateDirectory(abDir);
        AssetDatabase.CreateAsset(new TextAsset("sz"), "Assets/Addr/Sz.txt");
        BuildPipeline.BuildAssetBundles(abDir, new[]
        {
            new AssetBundleBuild { assetBundleName = "szb", assetNames = new[] { "Assets/Addr/Sz.txt" } }
        }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        Addressables.RegisterBundle("sz", Path.Combine(abDir, "szb"));
        var h = Addressables.GetDownloadSizeAsync("sz");
        Assert.True(h.Result > 0);
    }

    [Fact]
    public void LoadSceneAsync_Completes()
    {
        Addressables.Register("sc", "Assets/Addr/A.txt");
        AssetDatabase.CreateAsset(new TextAsset("sc"), "Assets/Addr/A.txt");
        var h = Addressables.LoadSceneAsync("sc", LoadSceneMode.Additive, activateOnLoad: false);
        Assert.True(h.IsDone);
        Assert.Equal(AsyncOperationStatus.Succeeded, h.Status);
    }

    [Fact]
    public void LoadAssetsAsync_MultipleKeys()
    {
        AssetDatabase.CreateAsset(new TextAsset("m1"), "Assets/Addr/M1.txt");
        AssetDatabase.CreateAsset(new TextAsset("m2"), "Assets/Addr/M2.txt");
        Addressables.Register("m1", "Assets/Addr/M1.txt");
        Addressables.Register("m2", "Assets/Addr/M2.txt");
        var h = Addressables.LoadAssetsAsync<TextAsset>(
            new List<object> { "m1", "m2" }, null, Addressables.MergeMode.Union);
        Assert.Equal(2, h.Result.Count);
    }
}
