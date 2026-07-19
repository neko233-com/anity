using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetBundleBuildVariantTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-bundle-variants-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetBundleBuildVariantTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void PublicVariantField_IsSingleString() => Assert.Equal(typeof(string), typeof(AssetBundleBuild).GetField(nameof(AssetBundleBuild.assetBundleVariant))!.FieldType);
    [Fact] public void ExplicitVariant_ManifestUsesQualifiedName() { var asset = Add("Hero.asset", 1); var manifest = Build(new[] { Map("characters", asset, "hd") }); Assert.Contains("characters.hd", manifest.GetAllAssetBundles()); }
    [Fact] public void ExplicitVariant_ManifestTracksVariant() { var asset = Add("Hero.asset", 1); var manifest = Build(new[] { Map("characters", asset, "hd") }); Assert.Equal(new[] { "characters.hd" }, manifest.GetAllAssetBundlesWithVariant()); }
    [Fact] public void ExplicitVariant_WritesQualifiedBundleFile() { var asset = Add("Hero.asset", 1); var output = Output(); Build(new[] { Map("characters", asset, "hd") }, output); Assert.True(File.Exists(Path.Combine(output, "characters.hd"))); }
    [Fact] public void ExplicitVariant_AppendHashUsesQualifiedLogicalName() { var asset = Add("Hero.asset", 1); var output = Output(); Build(new[] { Map("characters", asset, "hd") }, output, BuildAssetBundleOptions.AppendHashToAssetBundleName); Assert.Contains(Directory.GetFiles(output).Select(Path.GetFileName), name => name!.StartsWith("characters.hd_", StringComparison.Ordinal)); Assert.False(File.Exists(Path.Combine(output, "characters.hd"))); }
    [Fact] public void ExplicitVariant_DirectDependencyUsesDependencyVariant() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); var manifest = Build(new[] { Map("root", root, "hd"), Map("dep", dep, "mobile") }); Assert.Equal(new[] { "dep.mobile" }, manifest.GetDirectDependencies("root.hd")); }
    [Fact] public void ExplicitVariant_SameQualifiedBundleIsNotSelfDependency() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); var manifest = Build(new[] { Map("both", new[] { root, dep }, "hd") }); Assert.Empty(manifest.GetDirectDependencies("both.hd")); }
    [Fact] public void DefaultVariant_DirectDependencyUsesQualifiedNames() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); AssetDatabase.SetAssetBundleNameAndVariant(root, "root", "hd"); AssetDatabase.SetAssetBundleNameAndVariant(dep, "dep", "mobile"); var manifest = BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.Equal(new[] { "dep.mobile" }, manifest.GetDirectDependencies("root.hd")); }
    [Fact] public void DryRunVariant_ReportsQualifiedBundleWithoutWriting() { var asset = Add("Hero.asset", 1); var output = Output(); var manifest = Build(new[] { Map("characters", asset, "hd") }, output, BuildAssetBundleOptions.DryRunBuild); Assert.Contains("characters.hd", manifest.GetAllAssetBundles()); Assert.Contains("characters.hd", manifest.GetAllAssetBundlesWithVariant()); Assert.False(File.Exists(Path.Combine(output, "characters.hd"))); }
    [Fact] public void EmptyVariant_KeepsUnqualifiedBundleName() { var asset = Add("Hero.asset", 1); var manifest = Build(new[] { Map("characters", asset) }); Assert.Contains("characters", manifest.GetAllAssetBundles()); Assert.Empty(manifest.GetAllAssetBundlesWithVariant()); }

    private AssetBundleManifest Build(AssetBundleBuild[] builds, string? output = null, BuildAssetBundleOptions options = BuildAssetBundleOptions.None) => BuildPipeline.BuildAssetBundles(output ?? Output(), builds, options, BuildTarget.StandaloneOSX);
    private static AssetBundleBuild Map(string name, string asset, string variant = "") => Map(name, new[] { asset }, variant);
    private static AssetBundleBuild Map(string name, string[] assets, string variant = "") => new() { assetBundleName = name, assetNames = assets, assetBundleVariant = variant };
    private string Add(string name, int id, string body = "") { var path = "Assets/Variants/" + name; Write(path, body); Write(path + ".meta", "fileFormatVersion: 2\nguid: " + GuidFor(id) + "\n"); AssetDatabase.ImportAsset(path); return path; }
    private static string GuidFor(int id) => id.ToString("x32");
    private static string Ref(string path) => "ref: { guid: " + AssetDatabase.AssetPathToGUID(path) + " }";
    private void Write(string path, string content) { var disk = Path.Combine(_dir, path); Directory.CreateDirectory(Path.GetDirectoryName(disk)!); File.WriteAllText(disk, content); }
    private string Output() { var output = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); return output; }
}
