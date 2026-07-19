using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseImplicitAssetBundleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-implicit-bundle-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseImplicitAssetBundleTests() { Directory.CreateDirectory(_dir); EditorApplication.OpenProject(_dir); }
    public void Dispose() { EditorApplication.OpenProject(_originalDirectory); try { Directory.Delete(_dir, true); } catch { } }

    [Fact] public void ImplicitName_ReturnsExplicitAssetAssignment() { var path = Create("direct.txt"); AssetDatabase.SetAssetBundleNameAndVariant(path, "direct"); Assert.Equal("direct", AssetDatabase.GetImplicitAssetBundleName(path)); }
    [Fact] public void ImplicitVariant_ReturnsExplicitAssetAssignment() { var path = Create("direct-variant.txt"); AssetDatabase.SetAssetBundleNameAndVariant(path, "direct", "hd"); Assert.Equal("hd", AssetDatabase.GetImplicitAssetBundleVariantName(path)); }
    [Fact] public void ImplicitName_InheritsParentFolder() { var path = Create("parent/child.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/parent", "parent"); Assert.Equal("parent", AssetDatabase.GetImplicitAssetBundleName(path)); }
    [Fact] public void ImplicitVariant_InheritsParentFolder() { var path = Create("variant/child.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/variant", "parent", "mobile"); Assert.Equal("mobile", AssetDatabase.GetImplicitAssetBundleVariantName(path)); }
    [Fact] public void ImplicitName_NearestFolderWins() { var path = Create("outer/inner/child.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/outer", "outer"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/outer/inner", "inner"); Assert.Equal("inner", AssetDatabase.GetImplicitAssetBundleName(path)); }
    [Fact] public void ImplicitName_EmptyWithoutAssignment() { Assert.Equal(string.Empty, AssetDatabase.GetImplicitAssetBundleName(Create("none.txt"))); }
    [Fact] public void ImplicitName_UsesDirectAssignmentBeforeFolder() { var path = Create("precedence/child.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/precedence", "folder"); AssetDatabase.SetAssetBundleNameAndVariant(path, "asset"); Assert.Equal("asset", AssetDatabase.GetImplicitAssetBundleName(path)); }
    [Fact] public void ImplicitName_ClearedAssetFallsBackToFolder() { var path = Create("fallback/child.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/fallback", "folder"); AssetDatabase.SetAssetBundleNameAndVariant(path, "asset"); AssetDatabase.SetAssetBundleNameAndVariant(path, string.Empty); Assert.Equal("folder", AssetDatabase.GetImplicitAssetBundleName(path)); }
    [Fact] public void DefaultBuild_IncludesFolderInheritedAsset() { Create("build/child.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/build", "folder-build"); var output = Output(); var manifest = BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.Contains("folder-build", manifest.GetAllAssetBundles()); Assert.True(File.Exists(Path.Combine(output, "folder-build"))); }
    [Fact] public void DefaultBuild_SeparatesExplicitChildFromFolderBundle() { var child = Create("split/child.txt"); Create("split/sibling.txt"); AssetDatabase.SetAssetBundleNameAndVariant("Assets/Implicit/split", "folder"); AssetDatabase.SetAssetBundleNameAndVariant(child, "child"); var manifest = BuildPipeline.BuildAssetBundles(Output(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX); Assert.Contains("folder", manifest.GetAllAssetBundles()); Assert.Contains("child", manifest.GetAllAssetBundles()); }

    private string Create(string name) { var path = "Assets/Implicit/" + name; AssetDatabase.CreateAsset(new TextAsset(name), path); return path; }
    private string Output() { var output = Path.Combine(_dir, "Bundles", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output); return output; }
}
