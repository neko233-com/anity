using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class BuildPipelineOutputDirectoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-build-output-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public BuildPipelineOutputDirectoryTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void EmptyOutput_ReturnsEmptyManifest() => Assert.Empty(BuildPipeline.BuildAssetBundles(string.Empty, Array.Empty<AssetBundleBuild>(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundles());
    [Fact] public void WhitespaceOutput_ReturnsEmptyManifest() => Assert.Empty(BuildPipeline.BuildAssetBundles("  ", Array.Empty<AssetBundleBuild>(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundles());
    [Fact] public void MissingOutputDirectory_IsRejected() => Assert.Throws<ArgumentException>(() => BuildPipeline.BuildAssetBundles(Path.Combine(_dir, "missing"), Array.Empty<AssetBundleBuild>(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX));
    [Fact] public void MissingOutputDirectory_DryRunIsRejected() => Assert.Throws<ArgumentException>(() => BuildPipeline.BuildAssetBundles(Path.Combine(_dir, "missing"), Array.Empty<AssetBundleBuild>(), BuildAssetBundleOptions.DryRunBuild, BuildTarget.StandaloneOSX));
    [Fact] public void FileOutputPath_IsRejected() { var file = Path.Combine(_dir, "output-file"); File.WriteAllText(file, "not a directory"); Assert.Throws<ArgumentException>(() => BuildPipeline.BuildAssetBundles(file, Array.Empty<AssetBundleBuild>(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX)); }
    [Fact] public void ExistingOutput_AcceptsEmptyBuildMap() { var output = Output(); Assert.Empty(BuildPipeline.BuildAssetBundles(output, Array.Empty<AssetBundleBuild>(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundles()); }
    [Fact] public void ExistingOutput_WritesExplicitBundle() { var output = Output(); BuildPipeline.BuildAssetBundles(output, new[] { Map(Create("explicit")) }, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.True(File.Exists(Path.Combine(output, "content"))); }
    [Fact] public void ExistingNestedOutput_WritesExplicitBundle() { var output = Path.Combine(_dir, "nested", "output"); Directory.CreateDirectory(output); BuildPipeline.BuildAssetBundles(output, new[] { Map(Create("nested")) }, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.True(File.Exists(Path.Combine(output, "content"))); }
    [Fact] public void ExistingOutput_DryRunWritesNoBundleFile() { var output = Output(); BuildPipeline.BuildAssetBundles(output, new[] { Map(Create("dry")) }, BuildAssetBundleOptions.DryRunBuild, BuildTarget.StandaloneOSX); Assert.False(File.Exists(Path.Combine(output, "content"))); }
    [Fact] public void ExistingOutput_DefaultAssignmentBuildsBundle() { var asset = Create("default"); AssetDatabase.SetAssetBundleNameAndVariant(asset, "default-content"); var output = Output(); BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.True(File.Exists(Path.Combine(output, "default-content"))); }

    private string Create(string name) { var path = "Assets/Outputs/" + name + ".txt"; AssetDatabase.CreateAsset(new TextAsset(name), path); return path; }
    private static AssetBundleBuild Map(string asset) => new() { assetBundleName = "content", assetNames = new[] { asset } };
    private string Output() { var output = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); return output; }
}
