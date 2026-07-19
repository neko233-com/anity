using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class NativeSkinnedModelImportTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "anity-skinned-model-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public NativeSkinnedModelImportTests()
    {
        Directory.CreateDirectory(Path.Combine(_project, "Assets", "Models"));
        EditorApplication.OpenProject(_project);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_project, true); } catch { }
    }

    [Fact]
    public void SkinClusterCreatesSkinnedRendererInsteadOfStaticRenderer()
    {
        var root = Import("SkinnedPlane.fbx");
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        Assert.Null(renderer.gameObject.GetComponent<MeshFilter>());
        Assert.Null(renderer.gameObject.GetComponent<MeshRenderer>());
    }

    [Fact]
    public void SkinClusterPreservesBoneHierarchy()
    {
        var root = Import("SkinnedPlane.fbx");
        var names = root.GetComponentsInChildren<Transform>().Select(transform => transform.name).ToArray();
        Assert.Contains("Armature", names);
        Assert.Contains("Bone", names);
        Assert.Contains("Bone_end", names);
    }

    [Fact]
    public void RendererBonesAndRootBoneMatchUnityFixture()
    {
        var renderer = Assert.Single(Import("SkinnedPlane.fbx").GetComponentsInChildren<SkinnedMeshRenderer>());
        var bone = Assert.Single(renderer.bones);
        Assert.Equal("Bone", bone.name);
        Assert.Same(bone, renderer.rootBone);
    }

    [Fact]
    public void GenericAvatarIsValidatedFromDecodedHierarchy()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(ImportPath("SkinnedPlane.fbx"));
        var avatar = Assert.Single(assets.OfType<Avatar>());
        Assert.True(avatar.isValid);
        Assert.False(avatar.isHuman);
    }

    [Fact]
    public void BindposeMatchesUnityCoordinateConversion()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportPath("SkinnedPlane.fbx")).OfType<Mesh>());
        var bindpose = Assert.Single(mesh.bindposes);
        Assert.Equal(1f, bindpose.m00, 5);
        Assert.Equal(1f, bindpose.m12, 5);
        Assert.Equal(-1f, bindpose.m21, 5);
        Assert.Equal(1f, bindpose.m33, 5);
    }

    [Fact]
    public void SkinWeightsAreMappedThroughDeduplicatedVertices()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportPath("SkinnedPlane.fbx")).OfType<Mesh>());
        Assert.Equal(4, mesh.vertexCount);
        Assert.Equal(4, mesh.boneWeights.Length);
        Assert.All(mesh.boneWeights, weight =>
        {
            Assert.Equal(0, weight.boneIndex0);
            Assert.Equal(1f, weight.weight0, 6);
        });
    }

    [Fact]
    public void SkinnedLocalBoundsMatchImportedMeshBounds()
    {
        var renderer = Assert.Single(Import("SkinnedPlane.fbx").GetComponentsInChildren<SkinnedMeshRenderer>());
        Assert.Equal(renderer.sharedMesh!.bounds, renderer.localBounds);
    }

    [Fact]
    public void BlendShapeOnlyMeshStillCreatesSkinnedRenderer()
    {
        var root = Import("BlendShapeCube.fbx");
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        Assert.Empty(renderer.bones);
        Assert.Null(renderer.rootBone);
    }

    [Fact]
    public void BlendShapeNamesAndOrderMatchUnityFixture()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportPath("BlendShapeCube.fbx")).OfType<Mesh>());
        Assert.Equal(2, mesh.blendShapeCount);
        Assert.Equal("TopH", mesh.GetBlendShapeName(0));
        Assert.Equal("TopV", mesh.GetBlendShapeName(1));
    }

    [Fact]
    public void BlendShapeFramesUseUnityPercentWeights()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportPath("BlendShapeCube.fbx")).OfType<Mesh>());
        Assert.Equal(1, mesh.GetBlendShapeFrameCount(0));
        Assert.Equal(100f, mesh.GetBlendShapeFrameWeight(0, 0), 5);
        Assert.Equal(100f, mesh.GetBlendShapeFrameWeight(1, 0), 5);
    }

    [Fact]
    public void BlendShapeDeltasFollowSplitVerticesAndFileScale()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportPath("BlendShapeCube.fbx")).OfType<Mesh>());
        var positions = new Vector3[mesh.vertexCount];
        var normals = new Vector3[mesh.vertexCount];
        var tangents = new Vector3[mesh.vertexCount];
        mesh.GetBlendShapeFrameVertices(0, 0, positions, normals, tangents);
        Assert.Contains(positions, delta => MathF.Abs(MathF.Abs(delta.x) - 0.00317f) < 0.00001f);
        Assert.All(positions, delta => Assert.True(MathF.Abs(delta.y) < 0.000001f && MathF.Abs(delta.z) < 0.000001f));
    }

    [Fact]
    public void BlendShapeWeightChangesBakedVertices()
    {
        var renderer = Assert.Single(Import("BlendShapeCube.fbx").GetComponentsInChildren<SkinnedMeshRenderer>());
        var original = renderer.sharedMesh!.vertices;
        renderer.SetBlendShapeWeight(0, 100f);
        var baked = new Mesh();
        renderer.BakeMesh(baked, false);
        Assert.Contains(original.Zip(baked.vertices), pair => pair.First != pair.Second);
    }

    [Fact]
    public void ImportBlendShapesFalseReturnsStaticMeshRenderer()
    {
        var path = CopyFixture("BlendShapeCube.fbx");
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importBlendShapes = false;
        importer.SaveAndReimport();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path)!;
        Assert.Empty(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        Assert.Single(root.GetComponentsInChildren<MeshRenderer>());
        Assert.Equal(0, AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single().blendShapeCount);
    }

    [Fact]
    public void ImportedBlendShapeClipUsesUnityRendererBindings()
    {
        var clip = BlendShapeClip(out _);
        var bindings = AnimationUtility.GetCurveBindings(clip);
        Assert.Contains(bindings, binding => binding.path == string.Empty && binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName == "blendShape.TopH");
        Assert.Contains(bindings, binding => binding.path == string.Empty && binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName == "blendShape.TopV");
    }

    [Fact]
    public void ImportedBlendShapeClipUsesSourceRateAndUnityTrimmedDuration()
    {
        var clip = BlendShapeClip(out _);
        Assert.Equal(24f, clip.frameRate, 5);
        Assert.Equal(89f / 24f, clip.length, 5);
    }

    [Fact]
    public void TopHCurvePreservesUnityPercentSamples()
    {
        var curve = BlendCurve(BlendShapeClip(out _), "TopH");
        Assert.Equal(100f, curve.Evaluate(0f), 4);
        Assert.Equal(87.87704f, curve.Evaluate(5f / 24f), 3);
        Assert.Equal(0f, curve.Evaluate(23f / 24f), 4);
    }

    [Fact]
    public void TopVCurvePreservesUnityPercentSamples()
    {
        var curve = BlendCurve(BlendShapeClip(out _), "TopV");
        Assert.Equal(100f, curve.Evaluate(0f), 4);
        Assert.Equal(63.31698f, curve.Evaluate(16f / 24f), 3);
        Assert.Equal(0f, curve.Evaluate(39f / 24f), 4);
    }

    [Fact]
    public void DefaultCompressionMatchesUnityBlendShapeKeyCounts()
    {
        var clip = BlendShapeClip(out _);
        Assert.Equal(8, BlendCurve(clip, "TopH").length);
        Assert.Equal(7, BlendCurve(clip, "TopV").length);
    }

    [Fact]
    public void DefaultTopHCompressionMatchesUnityKeyTimes()
        => AssertKeyTimes(BlendCurve(BlendShapeClip(out _), "TopH"),
            0, 5, 21, 22, 23, 24, 58, 59);

    [Fact]
    public void DefaultTopVCompressionMatchesUnityKeyTimes()
        => AssertKeyTimes(BlendCurve(BlendShapeClip(out _), "TopV"),
            0, 16, 38, 39, 40, 88, 89);

    [Fact]
    public void DefaultCompressionPreservesUnitySourceTangents()
    {
        var clip = BlendShapeClip(out _);
        var horizontal = BlendCurve(clip, "TopH").keys;
        var vertical = BlendCurve(clip, "TopV").keys;
        Assert.Equal(-13.216f, horizontal[0].outTangent, 3);
        Assert.Equal(-106.1232f, horizontal[1].inTangent, 3);
        Assert.Equal(-89.25299f, vertical[1].outTangent, 3);
        Assert.Equal(5.606412f, vertical[^2].inTangent, 3);
    }

    [Fact]
    public void CompressionOffKeepsEveryUnityResampledKey()
    {
        var clip = ReimportBlendShape(importer => importer.animationCompression = ModelImporterAnimationCompression.Off);
        Assert.Equal(60, BlendCurve(clip, "TopH").length);
        Assert.Equal(90, BlendCurve(clip, "TopV").length);
    }

    [Fact]
    public void CompressionOffUsesUnityCentralDifferenceTangents()
    {
        var clip = ReimportBlendShape(importer => importer.animationCompression = ModelImporterAnimationCompression.Off);
        var horizontal = BlendCurve(clip, "TopH").keys;
        var vertical = BlendCurve(clip, "TopV").keys;
        Assert.Equal(-13.216f, horizontal[0].inTangent, 3);
        Assert.Equal(-106.1232f, horizontal[5].outTangent, 3);
        Assert.Equal(-89.25299f, vertical[16].inTangent, 3);
        Assert.Equal(2.841624f, vertical[^1].outTangent, 3);
    }

    [Theory]
    [InlineData(ModelImporterAnimationCompression.KeyframeReduction)]
    [InlineData(ModelImporterAnimationCompression.KeyframeReductionAndCompression)]
    [InlineData(ModelImporterAnimationCompression.Optimal)]
    public void UnityReductionModesExposeTheSameBlendShapeKeys(ModelImporterAnimationCompression mode)
    {
        var clip = ReimportBlendShape(importer => importer.animationCompression = mode);
        AssertKeyTimes(BlendCurve(clip, "TopH"), 0, 5, 21, 22, 23, 24, 58, 59);
        AssertKeyTimes(BlendCurve(clip, "TopV"), 0, 16, 38, 39, 40, 88, 89);
    }

    [Fact]
    public void OnePercentErrorMatchesUnityReducedKeyTimes()
    {
        var clip = ReimportBlendShape(importer =>
        {
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationPositionError = 1f;
        });
        AssertKeyTimes(BlendCurve(clip, "TopH"), 0, 11, 22, 23, 24, 58, 59);
        AssertKeyTimes(BlendCurve(clip, "TopV"), 0, 28, 38, 39, 40, 88, 89);
    }

    [Fact]
    public void TenthPercentErrorMatchesUnityReducedKeyTimes()
    {
        var clip = ReimportBlendShape(importer =>
        {
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationPositionError = 0.1f;
        });
        AssertKeyTimes(BlendCurve(clip, "TopH"), 0, 1, 17, 20, 21, 22, 23, 24, 25, 28, 58, 59);
        AssertKeyTimes(BlendCurve(clip, "TopV"), 0, 3, 35, 37, 38, 39, 40, 41, 48, 88, 89);
    }

    [Fact]
    public void HundredthPercentErrorMatchesUnityReducedKeyTimes()
    {
        var clip = ReimportBlendShape(importer =>
        {
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationPositionError = 0.01f;
        });
        AssertKeyTimes(BlendCurve(clip, "TopH"), 0, 1, 6, 10, 12, 14, 15, 16, 17, 18, 19, 20,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 31, 34, 40, 53, 58, 59);
        AssertKeyTimes(BlendCurve(clip, "TopV"), 0, 1, 20, 27, 31, 33, 34, 35, 36, 37, 38, 39,
            40, 41, 42, 43, 44, 46, 50, 61, 88, 89);
    }

    [Fact]
    public void DisablingResampleCurvesPreservesThreeSourceMayaKeys()
    {
        var clip = ReimportBlendShape(importer =>
        {
            importer.resampleCurves = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        });
        AssertKeyTimes(BlendCurve(clip, "TopH"), 0, 23, 59);
        AssertKeyTimes(BlendCurve(clip, "TopV"), 0, 39, 89);
    }

    [Fact]
    public void DisablingResampleCurvesPreservesSourceBezierTangents()
    {
        var clip = ReimportBlendShape(importer => importer.resampleCurves = false);
        Assert.All(BlendCurve(clip, "TopH").keys, key => Assert.InRange(MathF.Abs(key.inTangent), 0f, 0.0001f));
        Assert.All(BlendCurve(clip, "TopV").keys, key => Assert.InRange(MathF.Abs(key.outTangent), 0f, 0.0001f));
    }

    [Fact]
    public void DefaultTakeFramesMatchUnitySourceTimeline()
    {
        var path = ImportPath("BlendShapeCube.fbx");
        var take = Assert.Single(ModelImporter.GetAtPath(path).defaultClipAnimations);
        Assert.Equal(1f, take.firstFrame, 5);
        Assert.Equal(120f, take.lastFrame, 5);
    }

    [Fact]
    public void SampleAnimationAppliesImportedBlendShapeWeights()
    {
        var clip = BlendShapeClip(out var root);
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        clip.SampleAnimation(root, 23f / 24f);
        Assert.Equal(0f, renderer.GetBlendShapeWeight(0), 4);
        Assert.InRange(renderer.GetBlendShapeWeight(1), 35f, 45f);
    }

    [Fact]
    public void SampleAnimationChangesBakedMorphGeometry()
    {
        var clip = BlendShapeClip(out var root);
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        var original = renderer.sharedMesh!.vertices;
        clip.SampleAnimation(root, 0f);
        var baked = new Mesh();
        renderer.BakeMesh(baked, false);
        Assert.Contains(original.Zip(baked.vertices), pair => pair.First != pair.Second);
    }

    [Fact]
    public void ImportBlendShapeDeformPercentFalseKeepsGeometryButOmitsCurves()
    {
        var path = CopyFixture("BlendShapeCube.fbx");
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importBlendShapeDeformPercent = false;
        importer.SaveAndReimport();
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Assert.Equal(2, Assert.Single(assets.OfType<Mesh>()).blendShapeCount);
        var clip = Assert.Single(assets.OfType<AnimationClip>());
        Assert.DoesNotContain(AnimationUtility.GetCurveBindings(clip), binding => binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportBlendShapesFalseOmitsBlendShapeCurves()
    {
        var path = CopyFixture("BlendShapeCube.fbx");
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importBlendShapes = false;
        importer.SaveAndReimport();
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        Assert.DoesNotContain(AnimationUtility.GetCurveBindings(clip), binding => binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal));
    }

    [Fact]
    public void CustomClipFrameRangeSlicesAndShiftsBlendShapeCurves()
    {
        var path = ImportPath("BlendShapeCube.fbx");
        var importer = ModelImporter.GetAtPath(path);
        importer.clipAnimations = new[]
        {
            new ModelImporterClipAnimation { name = "Slice", takeName = "Take 001", firstFrame = 24f, lastFrame = 48f }
        };
        importer.SaveAndReimport();
        var sliced = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        var slicedCurve = BlendCurve(sliced, "TopH");
        Assert.Equal("Slice", sliced.name);
        Assert.Equal(1f, sliced.length, 5);
        Assert.Equal(3, slicedCurve.length);
        Assert.Equal(0f, slicedCurve.Evaluate(0f), 4);
        Assert.Equal(74.08532f, slicedCurve.Evaluate(1f), 3);
        Assert.Equal(0f, slicedCurve.keys[0].time, 5);
        Assert.Equal(1f, slicedCurve.keys[^1].time, 5);
    }

    [Fact]
    public void AnimatorAppliesImportedBlendShapeCurve()
    {
        var clip = BlendShapeClip(out var root);
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        var controller = new AnimatorController();
        var state = controller.layers[0].stateMachine.AddState("Take");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Play("Take");
        animator.Update(5f / 24f);
        Assert.Equal(87.87704f, renderer.GetBlendShapeWeight(0), 3);
    }

    [Fact]
    public void MeshGetBlendShapeIndexUsesOrdinalNameMatching()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportPath("BlendShapeCube.fbx")).OfType<Mesh>());
        Assert.Equal(0, mesh.GetBlendShapeIndex("TopH"));
        Assert.Equal(1, mesh.GetBlendShapeIndex("TopV"));
        Assert.Equal(-1, mesh.GetBlendShapeIndex("toph"));
        Assert.Equal(-1, mesh.GetBlendShapeIndex("Missing"));
        Assert.Equal(-1, mesh.GetBlendShapeIndex(null!));
    }

    [Fact]
    public void MultipleCustomClipsCanSliceTheSameTake()
    {
        var path = ImportPath("BlendShapeCube.fbx");
        var importer = ModelImporter.GetAtPath(path);
        importer.clipAnimations = new[]
        {
            new ModelImporterClipAnimation { name = "First", takeName = "Take 001", firstFrame = 1f, lastFrame = 25f },
            new ModelImporterClipAnimation { name = "Second", takeName = "Take 001", firstFrame = 25f, lastFrame = 49f },
        };
        importer.SaveAndReimport();
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().OrderBy(clip => clip.name).ToArray();
        Assert.Equal(2, clips.Length);
        Assert.Equal("First", clips[0].name);
        Assert.Equal("Second", clips[1].name);
        Assert.All(clips, clip => Assert.Equal(1f, clip.length, 5));
        Assert.NotEqual(BlendCurve(clips[0], "TopH").Evaluate(0f), BlendCurve(clips[1], "TopH").Evaluate(0f));
    }

    [Fact]
    public void AnimatorOverrideLayerBlendsBlendShapeWeights()
    {
        var root = Import("BlendShapeCube.fbx");
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        var controller = new AnimatorController();
        controller.AddAnimationClip(ConstantBlendClip("Base", 100f), "Base");
        var upper = controller.AddLayer("Upper");
        upper.weight = 0.25f;
        upper.stateMachine.AddState("Upper").motion = ConstantBlendClip("Upper", 0f);
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        animator.Update(0.25f);
        Assert.Equal(75f, renderer.GetBlendShapeWeight(0), 4);
    }

    [Fact]
    public void AnimatorAdditiveLayerUsesBlendShapeReferencePose()
    {
        var root = Import("BlendShapeCube.fbx");
        var renderer = Assert.Single(root.GetComponentsInChildren<SkinnedMeshRenderer>());
        var baseClip = ConstantBlendClip("Base", 50f);
        var additiveClip = ConstantBlendClip("Additive", 80f);
        var referenceClip = ConstantBlendClip("Reference", 20f);
        additiveClip.MarkMecanimDataBuilt();
        referenceClip.MarkMecanimDataBuilt();
        AnimationUtility.SetAdditiveReferencePose(additiveClip, referenceClip, 0f);
        var controller = new AnimatorController();
        controller.AddAnimationClip(baseClip, "Base");
        var upper = controller.AddLayer("Additive");
        upper.weight = 0.5f;
        upper.blendingMode = AnimatorLayerBlendingMode.Additive;
        upper.stateMachine.AddState("Additive").motion = additiveClip;
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        animator.Update(0.25f);
        Assert.Equal(80f, renderer.GetBlendShapeWeight(0), 4);
    }

    private AnimationClip BlendShapeClip(out GameObject root)
    {
        var path = ImportPath("BlendShapeCube.fbx");
        root = AssetDatabase.LoadAssetAtPath<GameObject>(path)!;
        return Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
    }

    private AnimationClip ReimportBlendShape(Action<ModelImporter> configure)
    {
        var path = ImportPath("BlendShapeCube.fbx");
        var importer = ModelImporter.GetAtPath(path);
        configure(importer);
        importer.SaveAndReimport();
        return Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
    }

    private static AnimationCurve BlendCurve(AnimationClip clip, string name)
        => Assert.IsType<AnimationCurve>(AnimationUtility.GetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(SkinnedMeshRenderer), "blendShape." + name)));

    private static AnimationClip ConstantBlendClip(string name, float value)
    {
        var clip = new AnimationClip { name = name, length = 1f };
        clip.SetCurve(string.Empty, typeof(SkinnedMeshRenderer), "blendShape.TopH",
            AnimationCurve.Linear(0f, value, 1f, value));
        return clip;
    }

    private static void AssertKeyTimes(AnimationCurve curve, params int[] frames)
    {
        Assert.Equal(frames.Length, curve.length);
        for (var index = 0; index < frames.Length; index++)
            Assert.Equal(frames[index] / 24f, curve.keys[index].time, 5);
    }

    private GameObject Import(string fixture) => AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath(fixture))!;

    private string ImportPath(string fixture)
    {
        var path = CopyFixture(fixture);
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string CopyFixture(string fixture)
    {
        var path = "Assets/Models/" + Path.GetFileNameWithoutExtension(fixture) + "-" + Guid.NewGuid().ToString("N") + ".fbx";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", fixture), Path.Combine(_project, path));
        return path;
    }
}
