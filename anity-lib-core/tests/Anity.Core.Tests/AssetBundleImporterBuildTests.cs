using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetBundleImporterBuildTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-importer-bundle-build-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetBundleImporterBuildTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void DefaultBuild_WritesAssignedBundle() { Assign("name.txt", "name"); var output = Output(); BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.True(File.Exists(Path.Combine(output, "name"))); }
    [Fact] public void DefaultBuild_ManifestContainsAssignedName() { Assign("manifest.txt", "manifest"); Assert.Contains("manifest", BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundles()); }
    [Fact] public void DefaultBuild_WritesVariantBundle() { Assign("variant.txt", "textures", "hd"); var output = Output(); BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.True(File.Exists(Path.Combine(output, "textures.hd"))); }
    [Fact] public void DefaultBuild_ManifestContainsVariant() { Assign("variant-manifest.txt", "textures", "mobile"); Assert.Contains("textures.mobile", BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundlesWithVariant()); }
    [Fact] public void DefaultBuild_PacksEveryAssetWithSameAssignment() { var first = Assign("first.txt", "shared"); var second = Assign("second.txt", "shared"); var output = Output(); BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); var bundle = AssetBundle.LoadFromFile(Path.Combine(output, "shared")); Assert.NotNull(bundle); Assert.Contains(first, bundle!.GetAllAssetNames()); Assert.Contains(second, bundle.GetAllAssetNames()); bundle.Unload(true); }
    [Fact] public void DefaultBuild_IgnoresUnassignedAsset() { Create("unassigned.txt"); Assign("assigned.txt", "assigned"); var manifest = BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.Equal(new[] { "assigned" }, manifest.GetAllAssetBundles()); }
    [Fact] public void DefaultBuild_ClearedAssignmentIsExcluded() { var path = Assign("clear.txt", "clear"); AssetDatabase.SetAssetBundleNameAndVariant(path, string.Empty); Assert.Empty(BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundles()); }
    [Fact] public void DefaultBuild_DryRunReportsWithoutWriting() { Assign("dry.txt", "dry"); var output = Output(); var manifest = BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.DryRunBuild, BuildTarget.StandaloneOSX); Assert.Contains("dry", manifest.GetAllAssetBundles()); Assert.False(File.Exists(Path.Combine(output, "dry"))); }
    [Fact] public void DefaultBuild_WritesEachAssignedName() { Assign("alpha.txt", "alpha"); Assign("beta.txt", "beta"); var output = Output(); BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.True(File.Exists(Path.Combine(output, "alpha"))); Assert.True(File.Exists(Path.Combine(output, "beta"))); }
    [Fact] public void DefaultBuild_UsesAssetImporterProperties() { var path = Create("property.txt"); EditorAssetImporter.GetAtPath(path).assetBundleName = "property"; Assert.Contains("property", BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX).GetAllAssetBundles()); }

    private string Assign(string fileName, string name, string variant = "") { var path = Create(fileName); AssetDatabase.SetAssetBundleNameAndVariant(path, name, variant); return path; }
    private string Create(string fileName) { var path = "Assets/BundleBuild/" + Guid.NewGuid().ToString("N") + "/" + fileName; AssetDatabase.CreateAsset(new TextAsset(fileName), path); return path; }
    private string Output() { var output = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); return output; }
}
