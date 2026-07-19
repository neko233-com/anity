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
