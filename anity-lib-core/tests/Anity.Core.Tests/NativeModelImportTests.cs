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

    [Theory]
    [InlineData("m_LocalPosition.x", 0f, -0.01f, 0.01f)]
    [InlineData("m_LocalPosition.y", 0f, 0.02f, -0.02f)]
    [InlineData("m_LocalPosition.z", 0f, 0.03f, -0.03f)]
    [InlineData("localEulerAnglesRaw.x", 0f, 10f, -10f)]
    [InlineData("localEulerAnglesRaw.y", 0f, -20f, 20f)]
    [InlineData("localEulerAnglesRaw.z", 0f, -30f, 30f)]
    [InlineData("m_LocalScale.x", 1f, 1.1f, 0.9f)]
    [InlineData("m_LocalScale.y", 1f, 1.2f, 0.8f)]
    [InlineData("m_LocalScale.z", 1f, 1.3f, 0.7f)]
    public void NonResampledTransformCurveMatchesUnity2022SourceKeys(
        string property, float first, float middle, float last)
    {
        var clip = ImportNonResampledAnimation().Clip;
        var keys = Curve(clip, property).keys;
        Assert.Equal(new[] { 0f, 13f, 23f }, keys.Select(key => key.time * clip.frameRate).ToArray());
        Assert.Equal(first, keys[0].value, 5);
        Assert.Equal(middle, keys[1].value, 5);
        Assert.Equal(last, keys[2].value, 5);
        Assert.All(keys, key =>
        {
            Assert.Equal(0f, key.inTangent, 5);
            Assert.Equal(0f, key.outTangent, 5);
        });
    }

    [Fact]
    public void NonResampledTransformUsesNineRawEulerBindings()
    {
        var clip = ImportNonResampledAnimation().Clip;
        var properties = clip.bindings.Select(binding => binding.propertyName).ToArray();
        Assert.Equal(9, properties.Length);
        Assert.Contains("localEulerAnglesRaw.x", properties);
        Assert.Contains("localEulerAnglesRaw.y", properties);
        Assert.Contains("localEulerAnglesRaw.z", properties);
        Assert.DoesNotContain(properties, property => property.StartsWith("m_LocalRotation.", StringComparison.Ordinal));
    }

    [Fact]
    public void NonResampledTransformSamplesUnityConvertedPose()
    {
        var imported = ImportNonResampledAnimation();
        imported.Clip.SampleAnimation(imported.Root, 13f / 24f);
        Assert.Equal(-0.01f, imported.Root.transform.localPosition.x, 5);
        Assert.Equal(0.02f, imported.Root.transform.localPosition.y, 5);
        Assert.Equal(0.03f, imported.Root.transform.localPosition.z, 5);
        Assert.Equal(1.1f, imported.Root.transform.localScale.x, 5);
        Assert.Equal(1.2f, imported.Root.transform.localScale.y, 5);
        Assert.Equal(1.3f, imported.Root.transform.localScale.z, 5);
        Assert.InRange(Quaternion.Angle(Quaternion.Euler(10f, -20f, -30f), imported.Root.transform.localRotation), 0f, 0.001f);
    }

    [Fact]
    public void ResampledTransformUsesQuaternionBindingsAndTwentyFourFramesWhenUncompressed()
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        var properties = imported.Clip.bindings.Select(binding => binding.propertyName).ToArray();
        Assert.Contains("m_LocalRotation.w", properties);
        Assert.DoesNotContain(properties, property => property.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal));
        var keys = Curve(imported.Clip, "m_LocalRotation.x").keys;
        Assert.Equal(24, keys.Length);
        Assert.Equal(Enumerable.Range(0, 24), Frames(Curve(imported.Clip,
            "m_LocalRotation.x"), imported.Clip.frameRate));
    }

    [Fact]
    public void ResampledTransformQuaternionMatchesUnity2022AtMiddleSourceKey()
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        Assert.Equal(0.038134575f, Curve(imported.Clip, "m_LocalRotation.x").keys[13].value, 6);
        Assert.Equal(-0.18930785f, Curve(imported.Clip, "m_LocalRotation.y").keys[13].value, 6);
        Assert.Equal(-0.23929834f, Curve(imported.Clip, "m_LocalRotation.z").keys[13].value, 6);
        Assert.Equal(0.9515485f, Curve(imported.Clip, "m_LocalRotation.w").keys[13].value, 6);
    }

    [Theory]
    [InlineData(0.01f, "0,1,3,5,6,8,9,11,12,13,14,15,16,17,18,19,20,22,23")]
    [InlineData(0.1f, "0,1,7,12,14,17,19,22,23")]
    [InlineData(0.5f, "0,5,11,14,17,19,22,23")]
    [InlineData(1f, "0,7,13,18,20,22,23")]
    public void ResampledQuaternionReductionMatchesUnity2022RotationErrorFrames(
        float rotationError, string expectedFrames)
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = rotationError;
        });
        var expected = expectedFrames.Split(',').Select(int.Parse).ToArray();
        foreach (var property in QuaternionProperties)
        {
            Assert.Equal(expected, Frames(Curve(imported.Clip, property), imported.Clip.frameRate));
        }
    }

    [Theory]
    [InlineData("m_LocalRotation.x")]
    [InlineData("m_LocalRotation.y")]
    [InlineData("m_LocalRotation.z")]
    [InlineData("m_LocalRotation.w")]
    public void ResampledQuaternionReductionSynchronizesComponentKeyTimes(string property)
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = 0.5f;
        });
        Assert.Equal(Frames(Curve(imported.Clip, "m_LocalRotation.x"), imported.Clip.frameRate),
            Frames(Curve(imported.Clip, property), imported.Clip.frameRate));
    }

    [Fact]
    public void ResampledQuaternionReductionPreservesUnity2022RetainedValues()
    {
        var (original, reduced) = ImportOriginalAndReducedQuaternionCurves();
        foreach (var property in QuaternionProperties)
        foreach (var retained in reduced[property])
            Assert.Equal(original[property].Single(key => key.time == retained.time).value, retained.value);
    }

    [Fact]
    public void ResampledQuaternionReductionPreservesUnity2022RetainedTangents()
    {
        var (original, reduced) = ImportOriginalAndReducedQuaternionCurves();
        foreach (var property in QuaternionProperties)
        foreach (var retained in reduced[property])
        {
            var source = original[property].Single(key => key.time == retained.time);
            Assert.Equal(source.inTangent, retained.inTangent);
            Assert.Equal(source.outTangent, retained.outTangent);
        }
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

    private (GameObject Root, AnimationClip Clip) ImportNonResampledAnimation() =>
        ReimportAnimated(importer => importer.resampleCurves = false);

    private (GameObject Root, AnimationClip Clip) ReimportAnimated(Action<ModelImporter> configure)
    {
        var path = ImportAnimatedFbx();
        var importer = ModelImporter.GetAtPath(path);
        configure(importer);
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private static AnimationCurve Curve(AnimationClip clip, string property) =>
        clip.bindings.Single(binding => string.Equals(binding.propertyName, property, StringComparison.Ordinal)).curve;

    private static readonly string[] QuaternionProperties =
    {
        "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
    };

    private static int[] Frames(AnimationCurve curve, float frameRate) =>
        curve.keys.Select(key => (int)MathF.Round(key.time * frameRate)).ToArray();

    private (Dictionary<string, Keyframe[]> Original, Dictionary<string, Keyframe[]> Reduced)
        ImportOriginalAndReducedQuaternionCurves()
    {
        var path = ImportAnimatedFbx();
        var importer = ModelImporter.GetAtPath(path);
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var originalClip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
        var original = QuaternionProperties.ToDictionary(property => property,
            property => Curve(originalClip, property).keys, StringComparer.Ordinal);

        importer = ModelImporter.GetAtPath(path);
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.animationRotationError = 0.5f;
        importer.SaveAndReimport();
        var reducedClip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
        var reduced = QuaternionProperties.ToDictionary(property => property,
            property => Curve(reducedClip, property).keys, StringComparer.Ordinal);
        return (original, reduced);
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
