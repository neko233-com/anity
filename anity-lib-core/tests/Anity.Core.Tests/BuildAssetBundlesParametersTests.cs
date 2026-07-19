using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class BuildAssetBundlesParametersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-bundle-parameters-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    public BuildAssetBundlesParametersTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }
    [Fact] public void PublicStruct_HasRequiredFields() { Assert.NotNull(typeof(BuildAssetBundlesParameters).GetField("outputPath")); Assert.NotNull(typeof(BuildAssetBundlesParameters).GetField("bundleDefinitions")); Assert.NotNull(typeof(BuildAssetBundlesParameters).GetField("options")); Assert.NotNull(typeof(BuildAssetBundlesParameters).GetField("targetPlatform")); }
    [Fact] public void ParameterBuild_UsesDefinitions() { var m = Build(new BuildAssetBundlesParameters { outputPath = Output(), bundleDefinitions = Definitions(Create("one")), targetPlatform = BuildTarget.StandaloneOSX }); Assert.Contains("content", m.GetAllAssetBundles()); }
    [Fact] public void ParameterBuild_WritesBundle() { var output = Output(); Build(new BuildAssetBundlesParameters { outputPath = output, bundleDefinitions = Definitions(Create("one")), targetPlatform = BuildTarget.StandaloneOSX }); Assert.True(File.Exists(Path.Combine(output, "content"))); }
    [Fact] public void ParameterBuild_UsesOptions() { var output = Output(); Build(new BuildAssetBundlesParameters { outputPath = output, bundleDefinitions = Definitions(Create("one")), options = BuildAssetBundleOptions.DryRunBuild, targetPlatform = BuildTarget.StandaloneOSX }); Assert.False(File.Exists(Path.Combine(output, "content"))); }
    [Fact] public void ParameterBuild_NullDefinitionsUsesAssignments() { var asset = Create("one"); AssetDatabase.SetAssetBundleNameAndVariant(asset, "assigned"); var m = Build(new BuildAssetBundlesParameters { outputPath = Output(), targetPlatform = BuildTarget.StandaloneOSX }); Assert.Contains("assigned", m.GetAllAssetBundles()); }
    [Fact] public void ParameterBuild_EmptyDefinitionsProducesEmptyManifest() { Assert.Empty(Build(new BuildAssetBundlesParameters { outputPath = Output(), bundleDefinitions = Array.Empty<AssetBundleBuild>(), targetPlatform = BuildTarget.StandaloneOSX }).GetAllAssetBundles()); }
    [Fact] public void ParameterBuild_RejectsMissingOutput() { Assert.Throws<ArgumentException>(() => Build(new BuildAssetBundlesParameters { outputPath = Path.Combine(_dir, "missing"), bundleDefinitions = Array.Empty<AssetBundleBuild>(), targetPlatform = BuildTarget.StandaloneOSX })); }
    [Fact] public void ParameterBuild_PreservesAddressableName() { var output = Output(); Build(new BuildAssetBundlesParameters { outputPath = output, bundleDefinitions = new[] { new AssetBundleBuild { assetBundleName = "content", assetNames = new[] { Create("one") }, addressableNames = new[] { "alias" } }, }, targetPlatform = BuildTarget.StandaloneOSX }); var b = AssetBundle.LoadFromFile(Path.Combine(output, "content")); Assert.NotNull(b); Assert.NotNull(b!.LoadAsset("alias")); b.Unload(true); }
    [Fact] public void ParameterBuild_PreservesVariant() { var m = Build(new BuildAssetBundlesParameters { outputPath = Output(), bundleDefinitions = new[] { new AssetBundleBuild { assetBundleName = "content", assetBundleVariant = "hd", assetNames = new[] { Create("one") } }, }, targetPlatform = BuildTarget.StandaloneOSX }); Assert.Contains("content.hd", m.GetAllAssetBundlesWithVariant()); }
    [Fact] public void ParameterBuild_DefaultTargetIsAccepted() { Assert.NotNull(Build(new BuildAssetBundlesParameters { outputPath = Output(), bundleDefinitions = Array.Empty<AssetBundleBuild>() })); }
    private static AssetBundleBuild[] Definitions(string asset) => new[] { new AssetBundleBuild { assetBundleName = "content", assetNames = new[] { asset } } };
    private static AssetBundleManifest Build(BuildAssetBundlesParameters value) => BuildPipeline.BuildAssetBundles(value);
    private string Create(string name) { var path = "Assets/Parameters/" + Guid.NewGuid().ToString("N") + name + ".txt"; AssetDatabase.CreateAsset(new TextAsset(name), path); return path; }
    private string Output() { var path = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}
