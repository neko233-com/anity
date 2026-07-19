using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>URP 14 camera-stack ownership, target, renderer and post-FX coverage.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class UrpCameraStackTests
{
    [Fact]
    public void GetUniversalAdditionalCameraData_IsStablePerCamera()
    {
        var camera = new Camera();

        Assert.Same(camera.GetUniversalAdditionalCameraData(), camera.GetUniversalAdditionalCameraData());
    }

    [Fact]
    public void CameraStack_RendersBaseThenOverlayInDeclaredOrder()
    {
        var trace = new List<string>();
        var data = NewData(trace);
        using var scope = UseAsset(NewAsset(data));
        var baseCamera = new Camera { name = "base" };
        var firstOverlay = Overlay("first");
        var secondOverlay = Overlay("second");
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(firstOverlay);
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(secondOverlay);

        baseCamera.Render();

        Assert.Equal(new[] { "base", "first", "second" }, trace);
    }

    [Fact]
    public void OverlayCamera_RenderedDirectly_IsSkippedWithoutBaseOwner()
    {
        var trace = new List<string>();
        var data = NewData(trace);
        using var scope = UseAsset(NewAsset(data));

        Overlay("orphan").Render();

        Assert.Empty(trace);
    }

    [Fact]
    public void CameraStack_ExcludesSelfNullDisabledAndDuplicateOverlays()
    {
        var trace = new List<string>();
        var data = NewData(trace);
        using var scope = UseAsset(NewAsset(data));
        var baseCamera = new Camera { name = "base" };
        var overlay = Overlay("overlay");
        var disabled = Overlay("disabled");
        disabled.enabled = false;
        var stack = baseCamera.GetUniversalAdditionalCameraData().cameraStack;
        stack.Add(baseCamera);
        stack.Add(null!);
        stack.Add(overlay);
        stack.Add(overlay);
        stack.Add(disabled);

        baseCamera.Render();

        Assert.Equal(new[] { "base", "overlay" }, trace);
    }

    [Fact]
    public void CameraStack_ExcludesCamerasWhoseRenderTypeIsBase()
    {
        var trace = new List<string>();
        var data = NewData(trace);
        using var scope = UseAsset(NewAsset(data));
        var baseCamera = new Camera { name = "base" };
        var accidentalBase = new Camera { name = "not-overlay" };
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(accidentalBase);

        baseCamera.Render();

        Assert.Equal(new[] { "base" }, trace);
    }

    [Fact]
    public void CameraStack_EnablesPostProcessingOnlyForLastCamera()
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var baseCamera = new Camera { name = "base" };
        var overlay = Overlay("overlay");
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

        baseCamera.Render();

        Assert.Collection(records,
            record => { Assert.Equal("base", record.Name); Assert.False(record.PostProcessing); Assert.False(record.LastInStack); },
            record => { Assert.Equal("overlay", record.Name); Assert.True(record.PostProcessing); Assert.True(record.LastInStack); });
    }

    [Fact]
    public void CameraStack_UsesOverlayClearDepthSettingWithoutColorOwnership()
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var baseCamera = new Camera { name = "base" };
        var overlay = Overlay("overlay");
        overlay.GetUniversalAdditionalCameraData().clearDepth = false;
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

        baseCamera.Render();

        Assert.Collection(records,
            record => Assert.True(record.ClearDepth),
            record => Assert.False(record.ClearDepth));
    }

    [Fact]
    public void CameraStack_UsesBaseTargetTextureAndDescriptorForOverlays()
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var target = new RenderTexture(320, 180, 24);
        var baseCamera = new Camera { name = "base", targetTexture = target };
        var overlay = Overlay("overlay");
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

        baseCamera.Render();

        Assert.All(records, record =>
        {
            Assert.Equal(target.GetInstanceID(), record.TargetNameId);
            Assert.Equal(320, record.Width);
            Assert.Equal(180, record.Height);
        });
    }

    [Theory]
    [InlineData(.002f)]
    [InlineData(.004f)]
    [InlineData(.006f)]
    [InlineData(.008f)]
    [InlineData(.010f)]
    [InlineData(.012f)]
    [InlineData(.014f)]
    [InlineData(.016f)]
    [InlineData(.018f)]
    [InlineData(.020f)]
    public void StereoArrayTarget_RendersEachEyeToItsOwnUrpSlice(float separation)
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var descriptor = new RenderTextureDescriptor(320, 180, RenderTextureFormat.ARGB32, 24)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        var camera = new Camera { name = "stereo", targetTexture = new RenderTexture(descriptor), stereoSeparation = separation };

        camera.Render();

        Assert.Collection(records,
            left => { Assert.True(left.Stereo); Assert.Equal(Camera.StereoscopicEye.Left, left.Eye); Assert.Equal(0, left.DepthSlice); },
            right => { Assert.True(right.Stereo); Assert.Equal(Camera.StereoscopicEye.Right, right.Eye); Assert.Equal(1, right.DepthSlice); });
    }

    [Theory]
    [InlineData(.002f)]
    [InlineData(.004f)]
    [InlineData(.006f)]
    [InlineData(.008f)]
    [InlineData(.010f)]
    [InlineData(.012f)]
    [InlineData(.014f)]
    [InlineData(.016f)]
    [InlineData(.018f)]
    [InlineData(.020f)]
    public void MetalTex2DArrayTarget_UsesOneUrpSinglePassInstancedCameraPass(float separation)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 16, 16, false);
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var descriptor = new RenderTextureDescriptor(16, 16, RenderTextureFormat.ARGB32, 24)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        var target = new RenderTexture(descriptor);
        var camera = new Camera { name = "metal-single-pass", targetTexture = target, stereoSeparation = separation };
        try
        {
            camera.Render();

            Assert.Collection(records, only =>
            {
                Assert.True(only.Stereo);
                Assert.Equal(Camera.StereoscopicEye.Left, only.Eye);
                Assert.Equal(0, only.DepthSlice);
            });
            Assert.Equal(0, device.LastCameraPass.desc.depthSlice);
            Assert.Equal(2, device.LastCameraPass.desc.depthSliceCount);
        }
        finally
        {
            device.ReleaseCameraRenderTarget(target);
            target.Release();
        }
    }

    [Theory]
    [InlineData(.5f)]
    [InlineData(.6f)]
    [InlineData(.7f)]
    [InlineData(.8f)]
    [InlineData(.9f)]
    [InlineData(1f)]
    [InlineData(1.1f)]
    [InlineData(1.2f)]
    [InlineData(1.3f)]
    [InlineData(1.5f)]
    public void MetalProviderHonoursExplicitMultipassRequest(float dynamicResolutionScale)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 16, 16, false);
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var display = new XRDisplaySubsystem();
        var camera = new Camera { name = "provider-multipass" };
        display.SetDynamicResolutionScale(dynamicResolutionScale);
        var target = display.ConfigureStereoFrame(camera, 16, 16, RenderTextureFormat.ARGB32);
        try
        {
            camera.Render();
            Assert.Single(records);
            Assert.True(records[0].PostProcessing);
            Assert.Equal(UnityEngine.TextureDimension.Tex2DArray, records[0].TargetDimension);
            Assert.Equal(2, records[0].TargetVolumeDepth);
            Assert.Equal(UnityEngine.VRTextureUsage.TwoEyes, records[0].TargetVrUsage);
            Assert.True(records[0].TargetUsesDynamicScale);
            Assert.Equal(2, device.LastCameraPass.desc.depthSliceCount);

            records.Clear();
            display.singlePassRenderingDisabled = true;
            camera.Render();
            Assert.Equal(XRTextureLayout.Texture2DArray, display.textureLayout);
            Assert.Collection(records,
                left => { Assert.Equal(Camera.StereoscopicEye.Left, left.Eye); Assert.Equal(0, left.DepthSlice); Assert.False(left.PostProcessing); Assert.Equal(UnityEngine.TextureDimension.Tex2DArray, left.TargetDimension); Assert.Equal(2, left.TargetVolumeDepth); Assert.True(left.TargetUsesDynamicScale); },
                right => { Assert.Equal(Camera.StereoscopicEye.Right, right.Eye); Assert.Equal(1, right.DepthSlice); Assert.True(right.PostProcessing); Assert.Equal(UnityEngine.TextureDimension.Tex2DArray, right.TargetDimension); Assert.Equal(2, right.TargetVolumeDepth); Assert.True(right.TargetUsesDynamicScale); });
            Assert.Equal(1, device.LastCameraPass.desc.depthSlice);
            Assert.Equal(1, device.LastCameraPass.desc.depthSliceCount);

            records.Clear();
            display.singlePassRenderingDisabled = false;
            camera.Render();
            Assert.Equal(XRTextureLayout.SinglePassInstanced, display.textureLayout);
            Assert.Single(records);
            Assert.True(records[0].PostProcessing);
            Assert.Equal(2, device.LastCameraPass.desc.depthSliceCount);
        }
        finally
        {
            device.ReleaseCameraRenderTarget(target);
            target.Release();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void MetalSinglePassInstanced_CullsTheUnionOfBothEyeFrusta(int caseId)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 16, 16, false);
        var visibleRendererIds = new List<int>();
        var data = new ForwardRendererData();
        data.rendererFeatures.Add(new CullingRecordFeature(visibleRendererIds));
        using var scope = UseAsset(NewAsset(data));
        var descriptor = new RenderTextureDescriptor(16, 16, RenderTextureFormat.ARGB32, 24)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        var target = new RenderTexture(descriptor);
        var camera = new Camera { name = "single-pass-union-cull", targetTexture = target };
        var objectToCull = new GameObject($"right-eye-only-{caseId}");
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-.25f, -.25f, 0f), new Vector3(.25f, -.25f, 0f), new Vector3(0f, .25f, 0f) },
            triangles = new[] { 0, 1, 2 }
        };
        try
        {
            objectToCull.transform.position = new Vector3(2.5f, 0f, 0f);
            objectToCull.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = objectToCull.AddComponent<MeshRenderer>();
            // The mesh is outside left eye's translated clip volume and only
            // inside the right eye.  A left-eye-only cull would omit it.
            camera.SetStereoProjectionMatrix(Camera.StereoscopicEye.Left, Matrix4x4.Translate(new Vector3(4f, 0f, 0f)));
            camera.SetStereoProjectionMatrix(Camera.StereoscopicEye.Right, Matrix4x4.Translate(new Vector3(-2.5f, 0f, 0f)));

            camera.Render();

            Assert.Contains(meshRenderer.GetInstanceID(), visibleRendererIds);
        }
        finally
        {
            camera.ResetStereoProjectionMatrices();
            device.ReleaseCameraRenderTarget(target);
            target.Release();
            UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(objectToCull);
        }
    }

    [Fact]
    public void CameraStack_UsesEachCameraViewportInsideTheSharedTarget()
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var target = new RenderTexture(400, 200, 24);
        var baseCamera = new Camera
        {
            name = "base",
            targetTexture = target,
            rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f)
        };
        var overlay = Overlay("overlay");
        overlay.rect = new Rect(0f, 0f, 0.25f, 0.25f);
        baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

        baseCamera.Render();

        Assert.Collection(records,
            record => Assert.Equal(new Rect(100f, 50f, 200f, 100f), record.PixelRect),
            record => Assert.Equal(new Rect(0f, 0f, 100f, 50f), record.PixelRect));
    }

    [Fact]
    public void BaseCamera_WithoutTargetTextureUsesCameraTarget()
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));

        new Camera { name = "base" }.Render();

        Assert.Single(records);
        Assert.Equal((int)BuiltinRenderTextureType.CameraTarget, records[0].TargetNameId);
    }

    [Fact]
    public void CameraRendererIndex_SelectsRequestedRenderer()
    {
        var firstTrace = new List<string>();
        var secondTrace = new List<string>();
        var first = NewData(firstTrace);
        var second = NewData(secondTrace);
        using var scope = UseAsset(NewAsset(first, second));
        var camera = new Camera { name = "camera" };
        camera.GetUniversalAdditionalCameraData().rendererIndex = 1;

        camera.Render();

        Assert.Empty(firstTrace);
        Assert.Equal(new[] { "camera" }, secondTrace);
    }

    [Fact]
    public void InvalidRendererIndex_FallsBackToAssetDefaultRenderer()
    {
        var firstTrace = new List<string>();
        var secondTrace = new List<string>();
        var first = NewData(firstTrace);
        var second = NewData(secondTrace);
        var asset = NewAsset(first, second);
        asset.defaultRendererIndex = 1;
        using var scope = UseAsset(asset);
        var camera = new Camera { name = "camera" };
        camera.GetUniversalAdditionalCameraData().rendererIndex = 77;

        camera.Render();

        Assert.Empty(firstTrace);
        Assert.Equal(new[] { "camera" }, secondTrace);
    }

    [Fact]
    public void BaseCameraPostProcessingDisabled_SuppressesFinalPostProcessing()
    {
        var records = new List<CameraRecord>();
        var data = NewData(records: records);
        using var scope = UseAsset(NewAsset(data));
        var baseCamera = new Camera { name = "base" };
        baseCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        baseCamera.Render();

        Assert.Single(records);
        Assert.True(records[0].LastInStack);
        Assert.False(records[0].PostProcessing);
    }

    private static Camera Overlay(string name)
    {
        var camera = new Camera { name = name };
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        return camera;
    }

    private static ForwardRendererData NewData(List<string>? trace = null, List<CameraRecord>? records = null)
    {
        var data = new ForwardRendererData();
        data.rendererFeatures.Add(new CameraRecordFeature(trace, records));
        return data;
    }

    private static UniversalRenderPipelineAsset NewAsset(params ScriptableRendererData[] rendererData)
    {
        return new UniversalRenderPipelineAsset { rendererDataList = rendererData, defaultRendererIndex = 0 };
    }

    private static IDisposable UseAsset(UniversalRenderPipelineAsset asset)
    {
        var previous = QualitySettings.renderPipeline;
        QualitySettings.renderPipeline = asset;
        return new Scope(() => QualitySettings.renderPipeline = previous);
    }

    private sealed class CameraRecordFeature : ScriptableRendererFeature
    {
        private readonly List<string>? _trace;
        private readonly List<CameraRecord>? _records;

        public CameraRecordFeature(List<string>? trace, List<CameraRecord>? records)
        {
            _trace = trace;
            _records = records;
        }

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _trace?.Add(renderingData.cameraData.camera.name);
            _records?.Add(new CameraRecord(
                renderingData.cameraData.camera.name,
                renderingData.postProcessingEnabled,
                renderingData.cameraData.isLastCameraInStack,
                renderingData.cameraData.clearDepth,
                renderingData.cameraData.targetTexture.m_NameID,
                renderingData.cameraData.cameraTargetDescriptor.width,
                renderingData.cameraData.cameraTargetDescriptor.height,
                renderingData.cameraData.pixelRect,
                renderingData.cameraData.isStereoEnabled,
                renderingData.cameraData.stereoEye,
                renderingData.cameraData.xrDepthSlice,
                renderingData.cameraData.cameraTargetDescriptor.dimension,
                renderingData.cameraData.cameraTargetDescriptor.volumeDepth,
                renderingData.cameraData.cameraTargetDescriptor.vrUsage,
                renderingData.cameraData.cameraTargetDescriptor.useDynamicScale));
        }
    }

    private sealed class CullingRecordFeature : ScriptableRendererFeature
    {
        private readonly List<int> _visibleRendererIds;
        public CullingRecordFeature(List<int> visibleRendererIds) => _visibleRendererIds = visibleRendererIds;
        public override void Create() { }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            foreach (var visibleRenderer in renderingData.cullResults.visibleRendererSnapshot)
                _visibleRendererIds.Add(visibleRenderer.GetInstanceID());
        }
    }

    private readonly record struct CameraRecord(string Name, bool PostProcessing, bool LastInStack, bool ClearDepth, int TargetNameId, int Width, int Height, Rect PixelRect, bool Stereo, Camera.StereoscopicEye Eye, int DepthSlice, UnityEngine.TextureDimension TargetDimension, int TargetVolumeDepth, UnityEngine.VRTextureUsage TargetVrUsage, bool TargetUsesDynamicScale);

    private sealed class Scope : IDisposable
    {
        private readonly Action _dispose;
        public Scope(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}
