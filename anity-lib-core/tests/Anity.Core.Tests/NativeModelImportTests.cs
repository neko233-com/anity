using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class NativeModelImportTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "anity-native-model-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public NativeModelImportTests()
    {
        Directory.CreateDirectory(Path.Combine(_project, "Assets", "Models"));
        EditorApplication.OpenProject(_project);
        NativeModelPostprocessorProbe.Reset();
    }

    public void Dispose()
    {
        NativeModelPostprocessorProbe.Reset();
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_project, true); } catch { }
    }

    [Fact]
    public void FbxImportCreatesGameObjectMainAssetInsteadOfTextAsset()
    {
        var path = ImportAnimatedFbx();
        Assert.IsType<GameObject>(AssetDatabase.LoadMainAssetAtPath(path));
        Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
    }

    [Fact]
    public void FbxImportCreatesIndexedMeshSubAsset()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportAnimatedFbx()).OfType<Mesh>());
        Assert.Equal(24, mesh.vertexCount);
        Assert.Equal(36, mesh.GetTriangles(0).Length);
        Assert.Equal(12, mesh.GetTriangles(0).Length / 3);
    }

    [Fact]
    public void FbxImportConvertsCentimetersToUnityMeters()
    {
        var path = ImportAnimatedFbx();
        var importer = ModelImporter.GetAtPath(path);
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
        Assert.Equal(0.01f, importer.fileScale, 5);
        Assert.Equal(0.01f, mesh.bounds.size.x, 4);
        Assert.Equal(0.01f, mesh.bounds.size.y, 4);
        Assert.Equal(0.01f, mesh.bounds.size.z, 4);
    }

    [Fact]
    public void ImportedMeshIsAttachedToRootMeshFilter()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(ImportAnimatedFbx())!;
        var filter = root.GetComponent<MeshFilter>();
        Assert.NotNull(filter);
        Assert.Same(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(root)).OfType<Mesh>().Single(), filter!.sharedMesh);
        Assert.NotNull(root.GetComponent<MeshRenderer>());
    }

    [Fact]
    public void FbxAnimationStackCreatesAnimationClip()
    {
        var path = ImportAnimatedFbx();
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        Assert.Equal("Take 001", clip.name);
        Assert.Equal(24f, clip.frameRate, 4);
        Assert.True(clip.length > 0f);
    }

    [Fact]
    public void ImportedAnimationContainsPositionRotationAndScaleCurves()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(ImportAnimatedFbx()).OfType<AnimationClip>().Single();
        var properties = clip.bindings.Select(binding => binding.propertyName).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("m_LocalPosition.x", properties);
        Assert.Contains("m_LocalRotation.w", properties);
        Assert.Contains("m_LocalScale.z", properties);
    }

    [Fact]
    public void ImportedAnimationSamplesDecodedTransformMotion()
    {
        var path = ImportAnimatedFbx();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path)!;
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
        var initial = root.transform.localPosition;
        clip.SampleAnimation(root, clip.length);
        Assert.NotEqual(initial, root.transform.localPosition);
    }

    [Fact]
    public void ImportAnimationFalseOmitsAnimationClip()
    {
        var path = CopyAnimatedFixture();
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importAnimation = false;
        importer.SaveAndReimport();
        Assert.Empty(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
    }

    [Fact]
    public void GlobalScaleScalesDecodedGeometry()
    {
        var path = CopyAnimatedFixture();
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.globalScale = 2f;
        importer.SaveAndReimport();
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
        Assert.Equal(0.02f, mesh.bounds.size.x, 4);
    }

    [Fact]
    public void CorruptReimportKeepsLastSuccessfulModelArtifact()
    {
        var path = ImportAnimatedFbx();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        File.WriteAllText(FullPath(path), "not an fbx");
        AssetDatabase.ImportAsset(path);
        Assert.Same(original, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
    }

    [Fact]
    public void ObjImportCreatesHierarchyAndTriangulatedMesh()
    {
        const string path = "Assets/Models/Quad.obj";
        File.WriteAllText(FullPath(path), "o Quad\nv -1 0 -1\nv 1 0 -1\nv 1 0 1\nv -1 0 1\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nvn 0 1 0\nf 1/1/1 2/2/1 3/3/1 4/4/1\n");
        AssetDatabase.ImportAsset(path);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
        Assert.NotNull(root);
        Assert.Equal(4, mesh.vertexCount);
        Assert.Equal(6, mesh.GetTriangles(0).Length);
    }

    [Fact]
    public void SpecializedModelCallbacksFollowUnityImportOrder()
    {
        var path = CopyAnimatedFixture();
        NativeModelPostprocessorProbe.TargetPath = path;
        NativeModelPostprocessorProbe.Enabled = true;
        AssetDatabase.ImportAsset(path);
        Assert.Equal(new[]
        {
            "pre-model", "post-mesh", "pre-animation", "post-animation:Take 001", "post-model"
        }, NativeModelPostprocessorProbe.Calls);
    }

    [Fact]
    public void OnPreprocessAnimationCanDisableClipConstruction()
    {
        var path = CopyAnimatedFixture();
        NativeModelPostprocessorProbe.TargetPath = path;
        NativeModelPostprocessorProbe.DisableAnimation = true;
        NativeModelPostprocessorProbe.Enabled = true;
        AssetDatabase.ImportAsset(path);
        Assert.Empty(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        Assert.DoesNotContain(NativeModelPostprocessorProbe.Calls, call => call.StartsWith("post-animation:", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultClipAnimationsExposeDecodedTakeMetadata()
    {
        var importer = ModelImporter.GetAtPath(ImportAnimatedFbx());
        var clip = Assert.Single(importer.defaultClipAnimations);
        Assert.Equal("Take 001", clip.name);
        Assert.Equal("Take 001", clip.takeName);
        Assert.True(clip.lastFrame > clip.firstFrame);
    }

    private string ImportAnimatedFbx()
    {
        var path = CopyAnimatedFixture();
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string CopyAnimatedFixture()
    {
        var name = "Animated-" + Guid.NewGuid().ToString("N") + ".fbx";
        var path = "Assets/Models/" + name;
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", "AnimatedCube.fbx"), FullPath(path));
        return path;
    }

    private string FullPath(string path) => Path.Combine(_project, path);
}

public sealed class NativeModelPostprocessorProbe : AssetPostprocessor
{
    internal static bool Enabled;
    internal static string TargetPath = string.Empty;
    internal static bool DisableAnimation;
    internal static readonly List<string> Calls = new();

    internal static void Reset()
    {
        Enabled = false;
        TargetPath = string.Empty;
        DisableAnimation = false;
        Calls.Clear();
    }

    private bool Active => Enabled && string.Equals(assetPath, TargetPath, StringComparison.Ordinal);
    private void OnPreprocessModel() { if (Active) Calls.Add("pre-model"); }
    private void OnPostprocessMeshHierarchy(GameObject root) { if (Active) Calls.Add("post-mesh"); }
    private void OnPreprocessAnimation()
    {
        if (!Active) return;
        Calls.Add("pre-animation");
        if (DisableAnimation) ((ModelImporter)assetImporter).importAnimation = false;
    }
    private void OnPostprocessAnimation(GameObject root, AnimationClip clip) { if (Active) Calls.Add("post-animation:" + clip.name); }
    private void OnPostprocessModel(GameObject root) { if (Active) Calls.Add("post-model"); }
}
