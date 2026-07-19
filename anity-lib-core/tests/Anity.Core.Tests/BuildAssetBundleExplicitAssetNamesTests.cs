using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class BuildAssetBundleExplicitAssetNamesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-explicit-names-" + Guid.NewGuid().ToString("N"));

    public BuildAssetBundleExplicitAssetNamesTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void PublicOverloads_UseBooleanResult() { var method = typeof(BuildPipeline).GetMethod(nameof(BuildPipeline.BuildAssetBundleExplicitAssetNames), new[] { typeof(UnityEngine.Object[]), typeof(string[]), typeof(string), typeof(BuildAssetBundleOptions), typeof(BuildTarget) }); Assert.NotNull(method); Assert.Equal(typeof(bool), method!.ReturnType); }
    [Fact] public void ExplicitNames_WritesBundleFile() { var path = Output(); Assert.True(Build(new[] { new TextAsset("one") }, new[] { "one" }, path)); Assert.True(File.Exists(path)); }
    [Fact] public void ExplicitNames_LoadsByCustomName() { var path = Output(); Assert.True(Build(new[] { new TextAsset("one") }, new[] { "aliases/one" }, path)); var bundle = AssetBundle.LoadFromFile(path); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset<TextAsset>("aliases/one")); bundle.Unload(true); }
    [Fact] public void ExplicitNames_UsesEachNameByIndex() { var path = Output(); Assert.True(Build(new[] { new TextAsset("one"), new TextAsset("two") }, new[] { "one", "two" }, path)); var bundle = AssetBundle.LoadFromFile(path); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset("one")); Assert.NotNull(bundle.LoadAsset("two")); bundle.Unload(true); }
    [Fact] public void ExplicitNames_ReportsFinalCrc() { var path = Output(); Assert.True(Build(new[] { new TextAsset("one") }, new[] { "one" }, path, out var crc)); Assert.NotEqual(0u, crc); Assert.Equal(AssetBundleFormat.ComputeCrc(File.ReadAllBytes(path)), crc); }
    [Fact] public void ExplicitNames_RejectsMismatchedArrayLengths() { Assert.False(Build(new[] { new TextAsset("one") }, Array.Empty<string>(), Output())); }
    [Fact] public void ExplicitNames_RejectsEmptyAssetCollection() { Assert.False(Build(Array.Empty<UnityEngine.Object>(), Array.Empty<string>(), Output())); }
    [Fact] public void ExplicitNames_RejectsNullAsset() { Assert.False(Build(new UnityEngine.Object[] { null! }, new[] { "one" }, Output())); }
    [Fact] public void ExplicitNames_RejectsEmptyCustomName() { Assert.False(Build(new[] { new TextAsset("one") }, new[] { string.Empty }, Output())); }
    [Fact] public void ExplicitNames_RejectsMissingOutputParent() { var path = Path.Combine(_dir, "missing", "bundle"); Assert.False(Build(new[] { new TextAsset("one") }, new[] { "one" }, path)); }

#pragma warning disable CS0618
    private static bool Build(UnityEngine.Object[] assets, string[] names, string path) => BuildPipeline.BuildAssetBundleExplicitAssetNames(assets, names, path, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
    private static bool Build(UnityEngine.Object[] assets, string[] names, string path, out uint crc) => BuildPipeline.BuildAssetBundleExplicitAssetNames(assets, names, path, out crc, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
#pragma warning restore CS0618
    private string Output() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".bundle");
}
