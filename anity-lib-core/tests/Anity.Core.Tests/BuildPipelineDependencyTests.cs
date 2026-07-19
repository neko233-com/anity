using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class BuildPipelineDependencyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-build-dependencies-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public BuildPipelineDependencyTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void DirectDependencies_EmptyPathIsEmpty() => Assert.Empty(BuildPipeline.GetDirectDependencies(string.Empty));
    [Fact] public void DirectDependencies_ReturnsReferencedAsset() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); Assert.Equal(new[] { dep }, BuildPipeline.GetDirectDependencies(root)); }
    [Fact] public void DirectDependencies_ExcludesSourceAsset() { var root = Add("Root.asset", 1); Assert.Empty(BuildPipeline.GetDirectDependencies(root)); }
    [Fact] public void DirectDependencies_ExcludesTransitiveAsset() { var leaf = Add("Leaf.asset", 3); var dep = Add("Dep.asset", 2, Ref(leaf)); var root = Add("Root.asset", 1, Ref(dep)); Assert.Equal(new[] { dep }, BuildPipeline.GetDirectDependencies(root)); }
    [Fact] public void DirectDependencies_ArePathSorted() { var z = Add("Z.asset", 3); var a = Add("A.asset", 2); var root = Add("Root.asset", 1, Ref(z) + "\n" + Ref(a)); Assert.Equal(new[] { a, z }, BuildPipeline.GetDirectDependencies(root)); }
    [Fact] public void AllDependencies_IncludesTransitiveAssets() { var leaf = Add("Leaf.asset", 3); var dep = Add("Dep.asset", 2, Ref(leaf)); var root = Add("Root.asset", 1, Ref(dep)); Assert.Equal(new[] { dep, leaf }, BuildPipeline.GetAllDependencies(root)); }
    [Fact] public void AllDependencies_HandlesCyclesWithoutSource() { var first = Add("First.asset", 1); var second = Add("Second.asset", 2, Ref(first)); Rewrite(first, Ref(second)); Assert.Equal(new[] { second }, BuildPipeline.GetAllDependencies(first)); }
    [Fact] public void ExplicitBuild_ManifestRecordsDirectBundleDependency() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); var manifest = Build(new[] { new AssetBundleBuild { assetBundleName = "root", assetNames = new[] { root } }, new AssetBundleBuild { assetBundleName = "dep", assetNames = new[] { dep } } }); Assert.Equal(new[] { "dep" }, manifest.GetDirectDependencies("root")); }
    [Fact] public void DefaultBuild_ManifestRecordsImplicitBundleDependency() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); AssetDatabase.SetAssetBundleNameAndVariant(root, "root"); AssetDatabase.SetAssetBundleNameAndVariant(dep, "dep"); var manifest = BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.Equal(new[] { "dep" }, manifest.GetDirectDependencies("root")); }
    [Fact] public void Build_SameBundleDependencyIsNotSelfDependency() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); var manifest = Build(new[] { new AssetBundleBuild { assetBundleName = "both", assetNames = new[] { root, dep } } }); Assert.Empty(manifest.GetDirectDependencies("both")); }

    private AssetBundleManifest Build(AssetBundleBuild[] builds) => BuildPipeline.BuildAssetBundles(Output(), builds, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
    private string Add(string name, int id, string body = "") { var path = "Assets/BuildDependencies/" + name; Write(path, body); Write(path + ".meta", "fileFormatVersion: 2\nguid: " + GuidFor(id) + "\n"); AssetDatabase.ImportAsset(path); return path; }
    private void Rewrite(string path, string body) { Write(path, body); AssetDatabase.ImportAsset(path); }
    private static string GuidFor(int id) => id.ToString("x32");
    private static string Ref(string path) => "ref: { guid: " + AssetDatabase.AssetPathToGUID(path) + " }";
    private void Write(string path, string content) { var disk = Path.Combine(_dir, path); Directory.CreateDirectory(Path.GetDirectoryName(disk)!); File.WriteAllText(disk, content); }
    private string Output() { var output = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); return output; }
}
