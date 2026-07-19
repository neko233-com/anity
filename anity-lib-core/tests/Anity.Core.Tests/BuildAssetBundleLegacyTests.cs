using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class BuildAssetBundleLegacyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-legacy-bundle-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public BuildAssetBundleLegacyTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void PublicOverload_UsesBooleanResult() { var method = typeof(BuildPipeline).GetMethod(nameof(BuildPipeline.BuildAssetBundle), new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object[]), typeof(string), typeof(BuildAssetBundleOptions), typeof(BuildTarget) }); Assert.NotNull(method); Assert.Equal(typeof(bool), method!.ReturnType); }
    [Fact] public void LegacyBuild_WritesBundleFile() { Assert.True(Build(Create("main"), Array.Empty<UnityEngine.Object>(), Output())); Assert.True(File.Exists(_lastOutput)); }
    [Fact] public void LegacyBuild_LoadsMainAssetByAssetPath() { var main = Create("main"); var path = Output(); Assert.True(Build(main, Array.Empty<UnityEngine.Object>(), path)); var bundle = AssetBundle.LoadFromFile(path); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset(AssetDatabase.GetAssetPath(main))); bundle.Unload(true); }
    [Fact] public void LegacyBuild_LoadsAdditionalAssetByAssetPath() { var main = Create("main"); var extra = Create("extra"); var path = Output(); Assert.True(Build(main, new[] { extra }, path)); var bundle = AssetBundle.LoadFromFile(path); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset(AssetDatabase.GetAssetPath(extra))); bundle.Unload(true); }
    [Fact] public void LegacyBuild_ReportsFinalCrc() { var path = Output(); Assert.True(Build(Create("main"), Array.Empty<UnityEngine.Object>(), path, out var crc)); Assert.NotEqual(0u, crc); Assert.Equal(AssetBundleFormat.ComputeCrc(File.ReadAllBytes(path)), crc); }
    [Fact] public void LegacyBuild_UsesObjectNameWhenAssetHasNoPath() { var main = new TextAsset("memory") { name = "memory-name" }; var path = Output(); Assert.True(Build(main, Array.Empty<UnityEngine.Object>(), path)); var bundle = AssetBundle.LoadFromFile(path); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset("memory-name")); bundle.Unload(true); }
    [Fact] public void LegacyBuild_RejectsNullMainAsset() { Assert.False(Build(null!, Array.Empty<UnityEngine.Object>(), Output())); }
    [Fact] public void LegacyBuild_RejectsNullAdditionalAsset() { Assert.False(Build(Create("main"), new UnityEngine.Object[] { null! }, Output())); }
    [Fact] public void LegacyBuild_RejectsDuplicateAssetName() { var main = Create("main"); Assert.False(Build(main, new[] { main }, Output())); }
    [Fact] public void LegacyBuild_RejectsMissingOutputParent() { Assert.False(Build(Create("main"), Array.Empty<UnityEngine.Object>(), Path.Combine(_dir, "missing", "bundle"))); }

    private string _lastOutput = string.Empty;
    private TextAsset Create(string name) { var path = "Assets/Legacy/" + Guid.NewGuid().ToString("N") + "/" + name + ".txt"; var asset = new TextAsset(name); AssetDatabase.CreateAsset(asset, path); return asset; }
#pragma warning disable CS0618
    private bool Build(UnityEngine.Object main, UnityEngine.Object[] assets, string output) { _lastOutput = output; return BuildPipeline.BuildAssetBundle(main, assets, output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); }
    private static bool Build(UnityEngine.Object main, UnityEngine.Object[] assets, string output, out uint crc) => BuildPipeline.BuildAssetBundle(main, assets, output, out crc, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
#pragma warning restore CS0618
    private string Output() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".bundle");
}
