using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetBundleBuildAddressableTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-bundle-addresses-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetBundleBuildAddressableTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void PublicAddressableNamesField_IsStringArray() => Assert.Equal(typeof(string[]), typeof(AssetBundleBuild).GetField(nameof(AssetBundleBuild.addressableNames))!.FieldType);
    [Fact] public void AddressableName_LoadsAssetByAlias() { var asset = Add("Hero.txt", "hero"); var file = Build(Map(asset, "characters/hero")); var bundle = AssetBundle.LoadFromFile(file); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset<TextAsset>("characters/hero")); bundle.Unload(true); }
    [Fact] public void AddressableName_DoesNotKeepSourcePathAsLoadKey() { var asset = Add("Hero.txt", "hero"); var file = Build(Map(asset, "characters/hero")); var bundle = AssetBundle.LoadFromFile(file); Assert.NotNull(bundle); Assert.Null(bundle!.LoadAsset(asset)); bundle.Unload(true); }
    [Fact] public void EmptyAddressableName_FallsBackToSourcePath() { var asset = Add("Hero.txt", "hero"); var file = Build(Map(asset, "")); var bundle = AssetBundle.LoadFromFile(file); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset(asset)); bundle.Unload(true); }
    [Fact] public void MultipleAddressableNames_MapByAssetIndex() { var first = Add("First.txt", "first"); var second = Add("Second.txt", "second"); var file = Build(Map(new[] { first, second }, new[] { "aliases/first", "aliases/second" })); var bundle = AssetBundle.LoadFromFile(file); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset("aliases/first")); Assert.NotNull(bundle.LoadAsset("aliases/second")); bundle.Unload(true); }
    [Fact] public void AddressableNames_AppearInBundleAssetNames() { var asset = Add("Hero.txt", "hero"); var file = Build(Map(asset, "characters/hero")); var bundle = AssetBundle.LoadFromFile(file); Assert.NotNull(bundle); Assert.Equal(new[] { "characters/hero" }, bundle!.GetAllAssetNames()); bundle.Unload(true); }
    [Fact] public void AddressableName_ChangesBundleHash() { var asset = Add("Hero.txt", "hero"); var first = BuildManifest(Map(asset, "one")); var second = BuildManifest(Map(asset, "two")); Assert.NotEqual(first.GetAssetBundleHash("content"), second.GetAssetBundleHash("content")); }
    [Fact] public void MismatchedAddressableLength_IsRejected() { var asset = Add("Hero.txt", "hero"); var map = new AssetBundleBuild { assetBundleName = "content", assetNames = new[] { asset }, addressableNames = Array.Empty<string>() }; Assert.Throws<ArgumentException>(() => BuildManifest(map)); }
    [Fact] public void NullAddressableNames_UsesSourcePath() { var asset = Add("Hero.txt", "hero"); var file = Build(new AssetBundleBuild { assetBundleName = "content", assetNames = new[] { asset } }); var bundle = AssetBundle.LoadFromFile(file); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset(asset)); bundle.Unload(true); }
    [Fact] public void AddressableNames_WorkWithVariantBundle() { var asset = Add("Hero.txt", "hero"); var output = Output(); BuildPipeline.BuildAssetBundles(output, new[] { new AssetBundleBuild { assetBundleName = "content", assetBundleVariant = "hd", assetNames = new[] { asset }, addressableNames = new[] { "hero" } } }, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); var bundle = AssetBundle.LoadFromFile(Path.Combine(output, "content.hd")); Assert.NotNull(bundle); Assert.NotNull(bundle!.LoadAsset("hero")); bundle.Unload(true); }

    private AssetBundleBuild Map(string asset, string address) => Map(new[] { asset }, new[] { address });
    private static AssetBundleBuild Map(string[] assets, string[] addresses) => new() { assetBundleName = "content", assetNames = assets, addressableNames = addresses };
    private string Build(AssetBundleBuild map) { var output = Output(); BuildPipeline.BuildAssetBundles(output, new[] { map }, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); return Path.Combine(output, map.assetBundleName + (string.IsNullOrEmpty(map.assetBundleVariant) ? "" : "." + map.assetBundleVariant)); }
    private AssetBundleManifest BuildManifest(AssetBundleBuild map) => BuildPipeline.BuildAssetBundles(Output(), new[] { map }, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
    private string Add(string name, string body) { var path = "Assets/Addresses/" + name; var disk = Path.Combine(_dir, path); Directory.CreateDirectory(Path.GetDirectoryName(disk)!); File.WriteAllText(disk, body); File.WriteAllText(disk + ".meta", "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n"); AssetDatabase.ImportAsset(path); return path; }
    private string Output() { var output = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); return output; }
}
