using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class NativeModelTopologyImportTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "anity-model-topology-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public NativeModelTopologyImportTests()
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
    public void DefaultImportCreatesBothFbxCameras()
    {
        var imported = Import("CameraLight.fbx");
        Assert.Equal(new[] { "CameraNode", "OrthoCameraNode" },
            imported.Root.GetComponentsInChildren<Camera>(true)
                .Select(camera => camera.gameObject.name).OrderBy(name => name));
    }

    [Fact]
    public void DefaultImportCreatesAllFbxLights()
    {
        var imported = Import("CameraLight.fbx");
        Assert.Equal(new[] { "DirectionalLightNode", "LightNode", "PointLightNode" },
            imported.Root.GetComponentsInChildren<Light>(true)
                .Select(light => light.gameObject.name).OrderBy(name => name));
    }

    [Fact]
    public void ImportCamerasFalseKeepsNodesButOmitsCameraComponents()
    {
        var imported = Import("CameraLight.fbx", importer => importer.importCameras = false);
        Assert.Empty(imported.Root.GetComponentsInChildren<Camera>(true));
        var names = imported.Root.GetComponentsInChildren<Transform>(true)
            .Select(transform => transform.name).ToArray();
        Assert.Contains("CameraNode", names);
        Assert.Contains("OrthoCameraNode", names);
    }

    [Fact]
    public void ImportLightsFalseKeepsNodesButOmitsLightComponents()
    {
        var imported = Import("CameraLight.fbx", importer => importer.importLights = false);
        Assert.Empty(imported.Root.GetComponentsInChildren<Light>(true));
        var names = imported.Root.GetComponentsInChildren<Transform>(true)
            .Select(transform => transform.name).ToArray();
        Assert.Contains("LightNode", names);
        Assert.Contains("PointLightNode", names);
        Assert.Contains("DirectionalLightNode", names);
    }

    [Fact]
    public void CameraAndLightImportFlagsAreIndependent()
    {
        var imported = Import("CameraLight.fbx", importer => importer.importCameras = false);
        Assert.Empty(imported.Root.GetComponentsInChildren<Camera>(true));
        Assert.Equal(3, imported.Root.GetComponentsInChildren<Light>(true).Length);
    }

    [Fact]
    public void PerspectiveCameraMatchesUnity2022FbxImport()
    {
        var camera = Named<Camera>(Import("CameraLight.fbx").Root, "CameraNode");
        Assert.True(camera.enabled);
        Assert.False(camera.orthographic);
        Assert.Equal(47f, camera.fieldOfView, 5);
        Assert.Equal(0.7f, camera.nearClipPlane, 5);
        Assert.Equal(321f, camera.farClipPlane, 5);
        Assert.Equal(4f / 3f, camera.aspect, 5);
        Assert.Equal(5f, camera.orthographicSize, 5);
    }

    [Fact]
    public void OrthographicCameraMatchesUnity2022FbxImport()
    {
        var camera = Named<Camera>(Import("CameraLight.fbx").Root, "OrthoCameraNode");
        Assert.True(camera.enabled);
        Assert.True(camera.orthographic);
        Assert.Equal(60f, camera.fieldOfView, 5);
        Assert.Equal(2f, camera.nearClipPlane, 5);
        Assert.Equal(99f, camera.farClipPlane, 5);
        Assert.Equal(4f / 3f, camera.aspect, 5);
        Assert.Equal(5f, camera.orthographicSize, 5);
    }

    [Fact]
    public void SpotLightMatchesUnity2022FbxImport()
    {
        var light = Named<Light>(Import("CameraLight.fbx").Root, "LightNode");
        Assert.True(light.enabled);
        Assert.Equal(LightType.Spot, light.type);
        AssertColor(light.color, 0.25f, 0.5f, 0.75f);
        Assert.Equal(2.25f, light.intensity, 5);
        Assert.Equal(22f, light.range, 5);
        Assert.Equal(63f, light.spotAngle, 5);
        Assert.Equal(21.80208f, light.innerSpotAngle, 5);
        Assert.Equal(LightShadows.Hard, light.shadows);
    }

    [Fact]
    public void PointLightMatchesUnity2022FbxImport()
    {
        var light = Named<Light>(Import("CameraLight.fbx").Root, "PointLightNode");
        Assert.Equal(LightType.Point, light.type);
        AssertColor(light.color, 0.8f, 0.3f, 0.1f);
        Assert.Equal(3.5f, light.intensity, 5);
        Assert.Equal(18f, light.range, 5);
        Assert.Equal(45f, light.spotAngle, 5);
        Assert.Equal(LightShadows.None, light.shadows);
    }

    [Fact]
    public void DirectionalLightMatchesUnity2022FbxImport()
    {
        var light = Named<Light>(Import("CameraLight.fbx").Root, "DirectionalLightNode");
        Assert.Equal(LightType.Directional, light.type);
        AssertColor(light.color, 0.1f, 0.9f, 0.4f);
        Assert.Equal(1.75f, light.intensity, 5);
        Assert.Equal(10f, light.range, 5);
        Assert.Equal(45f, light.spotAngle, 5);
        Assert.Equal(LightShadows.None, light.shadows);
    }

    [Fact]
    public void CameraAndLightVisibilityDoesNotDisableComponents()
    {
        var imported = Import("CameraLight.fbx");
        Assert.All(imported.Root.GetComponentsInChildren<Camera>(true), camera => Assert.True(camera.enabled));
        Assert.All(imported.Root.GetComponentsInChildren<Light>(true), light => Assert.True(light.enabled));
    }

    [Fact]
    public void CameraAndLightVisibilityDoesNotCreateRendererEnabledBindings()
    {
        var imported = Import("CameraLight.fbx");
        Assert.All(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>(), clip =>
            Assert.DoesNotContain(AnimationUtility.GetCurveBindings(clip), binding =>
                binding.propertyName == "m_Enabled"));
    }

    [Fact]
    public void CameraAndLightOnlyVisibilityDoesNotCreateAnimationClip()
    {
        var imported = Import("CameraLight.fbx");
        Assert.Empty(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>());
    }

    [Fact]
    public void GlobalScaleScalesCameraClipPlanes()
    {
        var root = Import("CameraLight.fbx", importer => importer.globalScale = 2f).Root;
        var perspective = Named<Camera>(root, "CameraNode");
        var orthographic = Named<Camera>(root, "OrthoCameraNode");
        Assert.Equal(1.4f, perspective.nearClipPlane, 5);
        Assert.Equal(642f, perspective.farClipPlane, 5);
        Assert.Equal(4f, orthographic.nearClipPlane, 5);
        Assert.Equal(198f, orthographic.farClipPlane, 5);
    }

    [Fact]
    public void GlobalScaleScalesLightRanges()
    {
        var root = Import("CameraLight.fbx", importer => importer.globalScale = 2f).Root;
        Assert.Equal(44f, Named<Light>(root, "LightNode").range, 5);
        Assert.Equal(36f, Named<Light>(root, "PointLightNode").range, 5);
        Assert.Equal(20f, Named<Light>(root, "DirectionalLightNode").range, 5);
    }

    [Fact]
    public void LightInnerSpotAngleDefaultMatchesUnity2022()
    {
        var light = new GameObject("Light").AddComponent<Light>();
        Assert.Equal(21.80208f, light.innerSpotAngle, 5);
    }

    [Fact]
    public void InstancedFbxUsesOneSharedMeshSubAsset()
    {
        var imported = Import("InstancedVisibility.fbx");
        var filters = imported.Root.GetComponentsInChildren<MeshFilter>(true);
        Assert.Equal(2, filters.Length);
        Assert.Same(filters[0].sharedMesh, filters[1].sharedMesh);
        Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<Mesh>());
    }

    [Fact]
    public void InstancedFbxCreatesOneRendererPerNode()
    {
        var renderers = Import("InstancedVisibility.fbx").Root
            .GetComponentsInChildren<MeshRenderer>(true);
        Assert.Equal(new[] { "InstanceA", "InstanceB" },
            renderers.Select(renderer => renderer.gameObject.name).OrderBy(name => name));
        Assert.All(renderers, renderer => Assert.True(renderer.enabled));
    }

    [Fact]
    public void InstancedVisibilityCreatesSeparateRendererBindings()
    {
        var imported = Import("InstancedVisibility.fbx");
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>());
        var paths = AnimationUtility.GetCurveBindings(clip)
            .Where(binding => binding.type == typeof(Renderer) && binding.propertyName == "m_Enabled")
            .Select(binding => binding.path).OrderBy(path => path).ToArray();
        Assert.Equal(new[] { "InstanceA", "InstanceB" }, paths);
    }

    [Fact]
    public void FbxLodHierarchyDoesNotInventLodGroupComponent()
    {
        var root = Import("LodVisibility.fbx").Root;
        Assert.Empty(root.GetComponentsInChildren<LODGroup>(true));
        Assert.Equal(new[] { "LOD0", "LOD1" }, root.GetComponentsInChildren<MeshRenderer>(true)
            .Select(renderer => renderer.gameObject.name).OrderBy(name => name));
    }

    [Fact]
    public void FbxLodHierarchyUsesOneSharedMeshSubAsset()
    {
        var imported = Import("LodVisibility.fbx");
        var filters = imported.Root.GetComponentsInChildren<MeshFilter>(true);
        Assert.Equal(2, filters.Length);
        Assert.Same(filters[0].sharedMesh, filters[1].sharedMesh);
        Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<Mesh>());
    }

    [Fact]
    public void FbxLodStaticVisibilityMatchesUnity2022()
    {
        var renderers = Import("LodVisibility.fbx").Root.GetComponentsInChildren<MeshRenderer>(true);
        Assert.True(Assert.Single(renderers.Where(renderer => renderer.gameObject.name == "LOD0")).enabled);
        Assert.False(Assert.Single(renderers.Where(renderer => renderer.gameObject.name == "LOD1")).enabled);
    }

    [Fact]
    public void FbxLodVisibilityCreatesRendererBindingsOnly()
    {
        var imported = Import("LodVisibility.fbx");
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>());
        var bindings = AnimationUtility.GetCurveBindings(clip)
            .Where(binding => binding.propertyName == "m_Enabled").ToArray();
        Assert.Equal(new[] { "LOD0", "LOD1" }, bindings.Select(binding => binding.path).OrderBy(path => path));
        Assert.All(bindings, binding => Assert.Equal(typeof(Renderer), binding.type));
    }

    [Fact]
    public void SkinnedStaticVisibilityDoesNotDisableRenderer()
    {
        var renderer = Assert.Single(Import("SkinnedVisibility.fbx").Root
            .GetComponentsInChildren<SkinnedMeshRenderer>(true));
        Assert.True(renderer.enabled);
    }

    [Fact]
    public void SkinnedVisibilityUsesRendererEnabledBinding()
    {
        var imported = Import("SkinnedVisibility.fbx");
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>());
        var binding = Assert.Single(AnimationUtility.GetCurveBindings(clip).Where(binding =>
            binding.path == "Skin" && binding.type == typeof(Renderer) &&
            binding.propertyName == "m_Enabled"));
        Assert.Equal(typeof(bool), AnimationUtility.GetEditorCurveValueType(imported.Root, binding));
    }

    [Fact]
    public void SkinnedVisibilityAnimationAppliesToRenderer()
    {
        var imported = Import("SkinnedVisibility.fbx");
        var renderer = Assert.Single(imported.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>());
        clip.SampleAnimation(imported.Root, 0f);
        Assert.False(renderer.enabled);
        clip.SampleAnimation(imported.Root, 23f / 24f);
        Assert.True(renderer.enabled);
    }

    [Fact]
    public void ImportVisibilityFalseKeepsSkinnedRendererEnabledAndDropsBinding()
    {
        var imported = Import("SkinnedVisibility.fbx", importer => importer.importVisibility = false);
        Assert.True(Assert.Single(imported.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)).enabled);
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(imported.Path).OfType<AnimationClip>());
        Assert.DoesNotContain(AnimationUtility.GetCurveBindings(clip), binding =>
            binding.propertyName == "m_Enabled");
    }

    private Imported Import(string fixture, Action<ModelImporter>? configure = null)
    {
        var path = "Assets/Models/" + Path.GetFileNameWithoutExtension(fixture) + "-" +
            Guid.NewGuid().ToString("N") + ".fbx";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", fixture),
            Path.Combine(_project, path));
        AssetDatabase.ImportAsset(path);
        if (configure is not null)
        {
            var importer = ModelImporter.GetAtPath(path);
            configure(importer);
            importer.SaveAndReimport();
        }
        return new Imported(path, AssetDatabase.LoadAssetAtPath<GameObject>(path)!);
    }

    private static T Named<T>(GameObject root, string name) where T : Component
        => Assert.Single(root.GetComponentsInChildren<T>(true)
            .Where(component => component.gameObject.name == name));

    private static void AssertColor(Color color, float red, float green, float blue)
    {
        Assert.Equal(red, color.r, 5);
        Assert.Equal(green, color.g, 5);
        Assert.Equal(blue, color.b, 5);
        Assert.Equal(1f, color.a, 5);
    }

    private readonly record struct Imported(string Path, GameObject Root);
}
