using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class XRDisplaySubsystemTests
{
    [Theory]
    [InlineData(64, 48, 1, 1f)]
    [InlineData(65, 49, 1, .5f)]
    [InlineData(80, 60, 1, 1.25f)]
    [InlineData(96, 64, 2, 1f)]
    [InlineData(128, 72, 2, .75f)]
    [InlineData(160, 90, 4, 1f)]
    [InlineData(192, 108, 4, .5f)]
    [InlineData(256, 144, 8, 1f)]
    [InlineData(320, 180, 8, .8f)]
    [InlineData(400, 225, 1, 1.1f)]
    public void ProviderStereoFrame_ProducesTwoEyeArrayPass(int width, int height, int samples, float scale)
    {
        var display = new XRDisplaySubsystem { scaleOfAllViewports = scale };
        var camera = new Camera();
        display.Start();
        var target = display.ConfigureStereoFrame(camera, width, height, RenderTextureFormat.DefaultHDR, samples);

        Assert.True(camera.stereoEnabled);
        Assert.Same(target, camera.targetTexture);
        Assert.Equal(TextureDimension.Tex2DArray, target.dimension);
        Assert.Equal(2, target.volumeDepth);
        Assert.Equal(VRTextureUsage.TwoEyes, target.vrUsage);
        Assert.True(target.useDynamicScale);
        Assert.True(target.descriptor.useDynamicScale);
        Assert.Equal(samples, target.msaaSamples);
        Assert.Equal(XRTextureLayout.SinglePassInstanced, display.textureLayout);
        Assert.Equal(1, display.renderPassCount);
        Assert.True(display.TryGetDisplayRefreshRate(out float refresh));
        Assert.Equal(60f, refresh);

        Assert.True(display.GetRenderPass(0, out var pass));
        Assert.False(display.GetRenderPass(1, out _));
        Assert.Equal(target.GetInstanceID(), pass.renderTarget.m_NameID);
        Assert.Equal(2, pass.renderParameterCount);
        pass.GetRenderParameter(camera, 0, out var leftEye);
        pass.GetRenderParameter(camera, 1, out var rightEye);
        Assert.Equal(0, leftEye.textureArraySlice);
        Assert.Equal(1, rightEye.textureArraySlice);
        Assert.Same(camera, leftEye.camera);
        Assert.True(display.GetCullingParameters(camera, 0, out var culling));
        Assert.Equal(leftEye.projection * leftEye.view, culling.cullingMatrix);
        Assert.True(culling.cullStereoSeparate);
        Assert.True(culling.stereoProjectionMatrix);
        Assert.False(display.GetCullingParameters(camera, 1, out _));
        target.Release();
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
    public void ProviderDynamicResolution_RebuildsTwoEyeTargetAtEffectiveScale(float dynamicScale)
    {
        var display = new XRDisplaySubsystem { scaleOfAllViewports = .8f };
        var camera = new Camera();
        var initial = display.ConfigureStereoFrame(camera, 200, 100);
        display.SetDynamicResolutionScale(dynamicScale);
        var target = display.ConfigureStereoFrame(camera, 200, 100);

        Assert.Equal(Math.Max(1, (int)Math.Round(200f * .8f * dynamicScale)), target.width);
        Assert.Equal(Math.Max(1, (int)Math.Round(100f * .8f * dynamicScale)), target.height);
        Assert.Equal(2, target.volumeDepth);
        Assert.Equal(VRTextureUsage.TwoEyes, target.vrUsage);
        Assert.True(target.useDynamicScale);
        Assert.Equal(XRTextureLayout.SinglePassInstanced, display.textureLayout);
        Assert.Equal(dynamicScale, display.dynamicResolutionScale);
        Assert.True(display.GetRenderPass(0, out var pass));
        Assert.Equal(target.width, pass.renderTargetDesc.width);
        if (dynamicScale != 1f) Assert.NotSame(initial, target);
        target.Release();
    }

    [Theory]
    [InlineData(1, .5f)]
    [InlineData(1, .6f)]
    [InlineData(1, .7f)]
    [InlineData(1, .8f)]
    [InlineData(2, .9f)]
    [InlineData(2, 1f)]
    [InlineData(2, 1.1f)]
    [InlineData(4, 1.2f)]
    [InlineData(4, 1.3f)]
    [InlineData(8, 1.5f)]
    public void ProviderOverlay_UsesBaseStereoArrayAndUrpStack(int samples, float dynamicScale)
    {
        var display = new XRDisplaySubsystem { scaleOfAllViewports = .75f };
        var baseCamera = new Camera();
        var overlay = new Camera();
        display.SetDynamicResolutionScale(dynamicScale);
        var target = display.ConfigureStereoFrame(baseCamera, 160, 80, RenderTextureFormat.DefaultHDR, samples);
        display.AttachOverlayCamera(baseCamera, overlay);

        var baseData = baseCamera.GetUniversalAdditionalCameraData();
        var overlayData = overlay.GetUniversalAdditionalCameraData();
        Assert.Same(target, overlay.targetTexture);
        Assert.True(overlay.stereoEnabled);
        Assert.Equal(Camera.StereoTargetEyeMask.Both, overlay.stereoTargetEye);
        Assert.Equal(CameraRenderType.Overlay, overlayData.renderType);
        Assert.Single(baseData.cameraStack);
        Assert.Same(overlay, baseData.cameraStack[0]);
        Assert.Equal(2, overlay.targetTexture!.volumeDepth);
        Assert.Equal(VRTextureUsage.TwoEyes, overlay.targetTexture.vrUsage);
        target.Release();
    }

    [Fact]
    public void ProviderRejectsInvalidFrameParameters()
    {
        var display = new XRDisplaySubsystem();
        var camera = new Camera();
        Assert.Throws<ArgumentOutOfRangeException>(() => display.ConfigureStereoFrame(camera, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => display.ConfigureStereoFrame(camera, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => display.ConfigureStereoFrame(camera, 10, 10, msaaSamples: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => display.SetDynamicResolutionScale(.4f));
        Assert.Throws<ArgumentOutOfRangeException>(() => display.SetDynamicResolutionScale(1.6f));
        Assert.Throws<InvalidOperationException>(() => display.AttachOverlayCamera(camera, new Camera()));
        Assert.False(display.GetRenderPass(0, out _));
        Assert.False(display.GetCullingParameters(camera, 0, out _));
    }

    [Fact]
    public void XRSettingsStartsAndStopsProvider()
    {
        XRSettings.StopDevice();
        Assert.False(XRSettings.isDeviceActive);
        XRSettings.LoadDeviceByName("Anity XR Display");
        XRSettings.StartDevice();
        Assert.True(XRSettings.isDeviceActive);
        XRSettings.StopDevice();
        Assert.False(XRSettings.isDeviceActive);
    }
}
