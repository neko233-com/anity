using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class BuildPipelineBundleUtilityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-bundle-utils-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    public BuildPipelineBundleUtilityTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }
    [Fact] public void PublicCrcSignatureExists() => Assert.NotNull(typeof(BuildPipeline).GetMethod(nameof(BuildPipeline.GetCRCForAssetBundle)));
    [Fact] public void PublicHashSignatureExists() => Assert.NotNull(typeof(BuildPipeline).GetMethod(nameof(BuildPipeline.GetHashForAssetBundle)));
    [Fact] public void CrcForBundle_Succeeds() { var file = Bundle("one"); Assert.True(BuildPipeline.GetCRCForAssetBundle(file, out var crc)); Assert.NotEqual(0u, crc); }
    [Fact] public void CrcForBundle_MatchesBytes() { var file = Bundle("one"); Assert.True(BuildPipeline.GetCRCForAssetBundle(file, out var crc)); Assert.Equal(AssetBundleFormat.ComputeCrc(File.ReadAllBytes(file)), crc); }
    [Fact] public void HashForBundle_Succeeds() { var file = Bundle("one"); Assert.True(BuildPipeline.GetHashForAssetBundle(file, out var hash)); Assert.NotEqual(default, hash); }
    [Fact] public void HashChangesWithContent() { var first = Bundle("one"); var second = Bundle("two"); Assert.True(BuildPipeline.GetHashForAssetBundle(first, out var a)); Assert.True(BuildPipeline.GetHashForAssetBundle(second, out var b)); Assert.NotEqual(a, b); }
    [Fact] public void CrcRejectsMissingPath() => Assert.False(BuildPipeline.GetCRCForAssetBundle(Path.Combine(_dir, "missing"), out _));
    [Fact] public void HashRejectsMissingPath() => Assert.False(BuildPipeline.GetHashForAssetBundle(Path.Combine(_dir, "missing"), out _));
    [Fact] public void CrcRejectsEmptyPath() => Assert.False(BuildPipeline.GetCRCForAssetBundle(string.Empty, out _));
    [Fact] public void HashRejectsNonBundleFile() { var file = Path.Combine(_dir, "plain"); File.WriteAllText(file, "plain"); Assert.False(BuildPipeline.GetHashForAssetBundle(file, out _)); }
    private string Bundle(string body) { var asset = "Assets/Utilities/" + Guid.NewGuid().ToString("N") + ".txt"; AssetDatabase.CreateAsset(new TextAsset(body), asset); var output = Path.Combine(_dir, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); BuildPipeline.BuildAssetBundles(output, new[] { new AssetBundleBuild { assetBundleName = "content", assetNames = new[] { asset } } }, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); return Path.Combine(output, "content"); }
}
