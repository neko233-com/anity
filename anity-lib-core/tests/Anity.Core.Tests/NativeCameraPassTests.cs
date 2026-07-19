using System;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeCameraPassTests
{
    [Fact]
    public void RecordCameraPass_RoundTripsTargetViewportAndColor()
    {
        using var device = NewDevice();

        Assert.True(device.TryRecordCameraPass(42, 400, 200, new Rect(10, 20, 300, 100), new Color(0.1f, 0.2f, 0.3f, 0.4f), true, true, 4, true, true));

        var pass = device.LastCameraPass;
        Assert.Equal((ulong)42, pass.desc.targetId);
        Assert.Equal(400, pass.desc.targetWidth);
        Assert.Equal(200, pass.desc.targetHeight);
        Assert.Equal(10f, pass.desc.viewportX);
        Assert.Equal(20f, pass.desc.viewportY);
        Assert.Equal(300f, pass.desc.viewportWidth);
        Assert.Equal(100f, pass.desc.viewportHeight);
        Assert.Equal(0.1f, pass.desc.clearR);
        Assert.Equal(0.4f, pass.desc.clearA);
    }

    [Fact]
    public void RecordCameraPass_SequencesAreMonotonic()
    {
        using var device = NewDevice();
        Assert.True(Record(device));
        ulong first = device.LastCameraPass.sequence;
        Assert.True(Record(device));

        Assert.Equal(first + 1, device.LastCameraPass.sequence);
    }

    [Fact]
    public void RecordCameraPass_UsesCurrentNativeFrameId()
    {
        using var device = NewDevice();
        device.BeginFrame();

        Assert.True(Record(device));
        Assert.True(device.LastCameraPass.frameId > 0);
    }

    [Fact]
    public void RecordCameraPass_EncodesClearAndStoreFlags()
    {
        using var device = NewDevice();
        Assert.True(device.TryRecordCameraPass(2, 16, 16, new Rect(0, 0, 16, 16), Color.red, true, true, 1, false, false));

        var flags = device.LastCameraPass.desc.flags;
        Assert.True(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.ClearColor));
        Assert.True(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.ClearDepth));
        Assert.True(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.StoreColor));
        Assert.True(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.StoreDepth));
    }

    [Fact]
    public void RecordCameraPass_OmitsOptionalFlagsWhenDisabled()
    {
        using var device = NewDevice();
        Assert.True(device.TryRecordCameraPass(2, 16, 16, new Rect(0, 0, 16, 16), Color.black, false, false, 1, false, false));

        var flags = device.LastCameraPass.desc.flags;
        Assert.False(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.ClearColor));
        Assert.False(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.ClearDepth));
        Assert.False(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.Final));
        Assert.False(flags.HasFlag(AnityNative.GraphicsCameraPassFlags.Hdr));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void RecordCameraPass_AcceptsUnityMsaaSampleCounts(int samples)
    {
        using var device = NewDevice();

        Assert.True(device.TryRecordCameraPass(2, 16, 16, new Rect(0, 0, 16, 16), Color.black, true, true, samples, false, false));
        Assert.Equal(samples, device.LastCameraPass.desc.msaaSamples);
    }

    [Fact]
    public void RecordCameraPass_RejectsUnsupportedMsaaCount()
    {
        using var device = NewDevice();

        Assert.False(device.TryRecordCameraPass(2, 16, 16, new Rect(0, 0, 16, 16), Color.black, true, true, 3, false, false));
    }

    [Fact]
    public void RecordCameraPass_RejectsInvalidTargetDimensions()
    {
        using var device = NewDevice();

        Assert.False(device.TryRecordCameraPass(2, 0, 16, new Rect(0, 0, 16, 16), Color.black, true, true, 1, false, false));
        Assert.False(device.TryRecordCameraPass(2, 16, -1, new Rect(0, 0, 16, 16), Color.black, true, true, 1, false, false));
    }

    [Fact]
    public void RecordCameraPass_RejectsNegativeOrNonFiniteViewportExtent()
    {
        using var device = NewDevice();

        Assert.False(device.TryRecordCameraPass(2, 16, 16, new Rect(0, 0, -1, 1), Color.black, true, true, 1, false, false));
        Assert.False(device.TryRecordCameraPass(2, 16, 16, new Rect(float.NaN, 0, 1, 1), Color.black, true, true, 1, false, false));
    }

    [Fact]
    public void RecordCameraPass_NativeLastPassQueryMatchesManagedSnapshot()
    {
        using var device = NewDevice();
        Assert.True(Record(device));

        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_GetLastCameraPass(device.Handle, out var native));
        Assert.Equal(device.LastCameraPass.sequence, native.sequence);
        Assert.Equal(device.LastCameraPass.desc.targetId, native.desc.targetId);
    }

    [Fact]
    public void MetalCameraTarget_ClearThenOverlayLoad_PreservesBasePixels()
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        Assert.True(device.CreateSwapchain(8, 8, imageCount: 2));

        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8), Color.red, true, true, 1, false, false));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8), Color.black, true, false, 1, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));

        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
        Assert.Equal((byte)255, pixels[3]);
        Assert.True(device.LastCameraPass.desc.flags.HasFlag(AnityNative.GraphicsCameraPassFlags.Final));
        Assert.False(device.LastCameraPass.desc.flags.HasFlag(AnityNative.GraphicsCameraPassFlags.ClearColor));
    }

    [Fact]
    public void MetalCameraTarget_PartialViewportClear_PreservesPixelsOutsideCameraRect()
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        Assert.True(device.CreateSwapchain(8, 8, imageCount: 2));

        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8), Color.red, true, true, 1, false, false));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 4, 4), Color.blue, true, true, 1, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));

        int lowerLeft = (7 * 8) * 4;
        Assert.Equal((byte)0, pixels[lowerLeft]);
        Assert.Equal((byte)0, pixels[lowerLeft + 1]);
        Assert.Equal((byte)255, pixels[lowerLeft + 2]);
        Assert.Equal((byte)255, pixels[lowerLeft + 3]);
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa2x_ClearResolvesToHeadlessReadback()
    {
        using var device = NewMetalCameraTargetDevice(2);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.green, true, true, 2, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        Assert.Equal((byte)255, pixels[1]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa4x_ClearResolvesToHeadlessReadback()
    {
        using var device = NewMetalCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.blue, true, true, 4, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa4x_OverlayLoadPreservesBaseColor()
    {
        using var device = NewMetalCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 4, false, false));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, false, 4, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa4x_PartialClearPreservesPixelsOutsideViewport()
    {
        using var device = NewMetalCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 4, false, false));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 4, 4),
            Color.blue, true, true, 4, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        int lowerLeft = 7 * 8 * 4;
        Assert.Equal((byte)255, pixels[lowerLeft + 2]);
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa4x_FinalPassFlagSurvivesResolve()
    {
        using var device = NewMetalCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, true, 4, true, false));
        Assert.True(device.LastCameraPass.desc.flags.HasFlag(
            AnityNative.GraphicsCameraPassFlags.Final));
        Assert.True(device.TryReadbackSwapchainRGBA8(out _));
    }

    [Fact]
    public void MetalCameraTarget_Msaa4x_FullClearReplacesPriorResolvedColor()
    {
        using var device = NewMetalCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 4, false, false));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.green, true, true, 4, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)255, pixels[1]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa4x_BeginFrameDoesNotDiscardStoredAttachment()
    {
        using var device = NewMetalCameraTargetDevice(4);
        device.BeginFrame();
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 4, false, false));
        device.BeginFrame();
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, false, 4, true, false));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        Assert.Equal((byte)255, pixels[0]);
    }

    [Fact]
    public void MetalCameraTarget_Msaa8x_ReturnsCapabilityBoundaryWithoutCrashing()
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false, msaa: 8);
        if (!device.CreateSwapchain(8, 8, imageCount: 2)) return;
        if (!device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
                Color.blue, true, true, 8, true, false)) return;
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_MsaaSampleMismatchIsRejected()
    {
        using var device = NewMetalCameraTargetDevice(4);
        Assert.False(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, true, 2, false, false));
    }

    [Fact]
    public void MetalCameraTarget_InvalidMsaaCreateIsRejectedBeforeEncoding()
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false, msaa: 3);
        Assert.False(device.CreateSwapchain(8, 8, imageCount: 2));
    }

    [Fact]
    public void MetalRenderTexture_Clear_ReadsBackNativePixels()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.green, true));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)255, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
        Assert.Equal((byte)255, pixels[3]);
    }

    [Fact]
    public void MetalRenderTexture_OverlayLoad_PreservesBaseColor()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(target));

        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.red, true));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, false));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_PartialClear_UsesUnityBottomLeftViewport()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(target));

        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.red, true));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

        int lowerLeft = 7 * 8 * 4;
        Assert.Equal((byte)0, pixels[lowerLeft]);
        Assert.Equal((byte)0, pixels[lowerLeft + 1]);
        Assert.Equal((byte)255, pixels[lowerLeft + 2]);
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_TargetsAreIsolated()
    {
        using var device = NewMetalDevice();
        var first = NewRenderTexture(4, 4);
        var second = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(first));
        Assert.True(device.EnsureCameraRenderTarget(second));

        Assert.True(RecordTarget(device, first, new Rect(0, 0, 4, 4), Color.red, true));
        Assert.True(RecordTarget(device, second, new Rect(0, 0, 4, 4), Color.blue, true));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(first, out var firstPixels));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(second, out var secondPixels));

        Assert.Equal((byte)255, firstPixels[0]);
        Assert.Equal((byte)0, firstPixels[2]);
        Assert.Equal((byte)0, secondPixels[0]);
        Assert.Equal((byte)255, secondPixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_DescriptorResize_RecreatesAttachment()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        target.descriptor = new RenderTextureDescriptor(6, 2, RenderTextureFormat.ARGB32, 24);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 6, 2), Color.red, true));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

        Assert.Equal(6 * 2 * 4, pixels.Length);
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
    }

    [Fact]
    public void MetalRenderTexture_Release_DestroysNativeAttachment()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        target.Release();

        Assert.False(device.TryReadbackCameraRenderTargetRGBA8(target, out _));
        Assert.False(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, true));
    }

    [Fact]
    public void MetalRenderTexture_HdrAttachmentDoesNotSilentlyToneMapReadback()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        Assert.False(device.TryReadbackCameraRenderTargetRGBA8(target, out _));
    }

    [Fact]
    public void MetalRenderTexture_HdrExplicitToneMappedReadback_UsesAcesAndSrgb()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[0], pixels[2]);
        Assert.Equal((byte)255, pixels[3]);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(1.75f)]
    [InlineData(2f)]
    [InlineData(2.25f)]
    [InlineData(2.5f)]
    public void MetalRenderTexture_HdrTex2DArray_PostProcessGradesEveryEyeLayer(float rightEyeIntensity)
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        ulong targetId = unchecked((ulong)(uint)target.GetInstanceID());

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(device.TryRecordCameraPass(targetId, 4, 4, new Rect(0, 0, 4, 4), Color.red,
            true, true, 1, false, true, isCameraTarget: false, depthSlice: 0));
        Assert.True(device.TryRecordCameraPass(targetId, 4, 4, new Rect(0, 0, 4, 4),
            new Color(0f, rightEyeIntensity, 0f, 1f), true, true, 1, false, true,
            isCameraTarget: false, depthSlice: 1));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));

        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var left, 0));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var right, 1));
        Assert.InRange(left[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, left[1]);
        Assert.Equal((byte)0, left[2]);
        Assert.Equal((byte)0, right[0]);
        Assert.InRange(right[1], (byte)1, (byte)254);
        Assert.Equal((byte)0, right[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrToneMappedReadback_PreservesBlack()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, true, hdr: true));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
        Assert.Equal((byte)255, pixels[3]);
    }

    [Fact]
    public void MetalRenderTexture_HdrToneMappedReadback_PreservesPrimaryHue()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrToneMappedReadback_CompressesBrightHighlights()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), new Color(4f, 0f, 0f, 1f), true, hdr: true));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)245, (byte)255);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrMsaaToneMappedReadback_ResolvesBeforeConversion()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR) { msaaSamples = 4 };

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true,
            hdr: true, msaaSamples: 4));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.InRange(pixels[1], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrMsaaToneMappedReadback_OverlayLoadKeepsBaseColor()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR) { msaaSamples = 4 };

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true,
            hdr: true, msaaSamples: 4));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, false,
            hdr: true, msaaSamples: 4));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_LdrToneMappedReadback_IsRejected()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(4, 4);

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true));
        Assert.False(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out _));
    }

    [Fact]
    public void MetalCameraTarget_HdrRawReadbackIsRejectedButExplicitToneMapSucceeds()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);

        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        Assert.False(device.TryReadbackSwapchainRGBA8(out _));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_HdrToneMappedReadback_PreservesOverlayContents()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);

        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.green, true, true, 1, false, true));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, false, 1, true, true));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.InRange(pixels[1], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_HdrMsaaToneMappedReadback_ResolvesToHeadlessTarget()
    {
        using var device = NewMetalHdrCameraTargetDevice(4);

        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.blue, true, true, 4, true, true));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_HdrPostProcess_ExecutesAcesOnNativeAttachment()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[0], pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrPostProcess_PreservesBlack()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrPostProcess_AppliesExposureBeforeToneMap()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.postExposure = 2f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)245, (byte)255);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrPostProcess_NeutralModeUsesItsOwnCurve()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.tonemapMode = 1;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)235, (byte)245);
    }

    [Fact]
    public void MetalRenderTexture_HdrPostProcess_MsaaResolvesBeforeCompute()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true,
            hdr: true, msaaSamples: 4));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.InRange(pixels[1], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_HdrPostProcess_OverlayLoadPreservesFinalColor()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true,
            hdr: true, msaaSamples: 4));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, false,
            hdr: true, msaaSamples: 4));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_LdrPostProcess_IsRejected()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true));
        Assert.False(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));
    }

    [Fact]
    public void MetalCameraTarget_HdrPostProcess_ExecutesOnHeadlessSwapchain()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        Assert.True(device.TryProcessSwapchainHDR(AcesGrade()));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[1]);
    }

    [Fact]
    public void MetalCameraTarget_HdrPostProcess_MsaaResolvesBeforeCompute()
    {
        using var device = NewMetalHdrCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.blue, true, true, 4, true, true));
        Assert.True(device.TryProcessSwapchainHDR(AcesGrade()));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalCameraTarget_HdrPostProcess_AfterOverlayUsesFinalStackContents()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.green, true, true, 1, false, true));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, false, 1, true, true));
        Assert.True(device.TryProcessSwapchainHDR(AcesGrade()));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.InRange(pixels[1], (byte)225, (byte)240);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void MetalRenderTexture_HdrBloom_IntensityBrightensThresholdedHighlights(float intensity)
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.1f;
        grade.bloomIntensity = intensity;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)235, (byte)255);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_ThresholdRejectsDimPixels()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), new Color(0.1f, 0f, 0f, 1f), true, hdr: true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 4f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)90, (byte)110);
        Assert.Equal((byte)0, pixels[1]);
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_TwoLevelsSpreadIntoBlackNeighbourhood()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 0f, 0f, 1f), true, hdr: true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        // The Unity lower-left clear lives in the lower Metal bloom quadrant;
        // sample a black pixel from that same quarter-resolution bloom cell.
        int outsideHighlight = (4 * 8 + 3) * 4;
        Assert.True(pixels[outsideHighlight] > 0);
        Assert.Equal((byte)0, pixels[outsideHighlight + 1]);
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_MsaaResolveFeedsBothLevels()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true,
            hdr: true, msaaSamples: 4));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.05f;
        grade.bloomIntensity = 1f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[2], (byte)225, (byte)255);
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_OverlayLoadFeedsFinalPassOnly()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true,
            hdr: true, msaaSamples: 4));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, false,
            hdr: true, msaaSamples: 4));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[1], (byte)235, (byte)255);
    }

    [Fact]
    public void MetalCameraTarget_HdrBloom_ExecutesTwoLevelChain()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.1f;
        grade.bloomIntensity = 1f;
        Assert.True(device.TryProcessSwapchainHDR(grade));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.InRange(pixels[0], (byte)235, (byte)255);
    }

    [Fact]
    public void MetalCameraTarget_HdrBloom_MsaaResolveFeedsTwoLevelChain()
    {
        using var device = NewMetalHdrCameraTargetDevice(4);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.blue, true, true, 4, true, true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.05f;
        grade.bloomIntensity = 1f;
        Assert.True(device.TryProcessSwapchainHDR(grade));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.InRange(pixels[2], (byte)225, (byte)255);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void MetalRenderTexture_HdrBloom_ConfiguredIterationCountPreservesHighlight(int iterations)
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(32, 32);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 32, 32), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.1f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = iterations;
        grade.bloomScatter = 0.7f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)235, (byte)255);
        Assert.Equal((byte)0, pixels[1]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public void MetalRenderTexture_HdrBloom_DownscaleAndFilteringModesExecute(int downscale, int highQuality)
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(16, 16);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 16, 16), Color.blue, true, hdr: true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.1f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = 6;
        grade.bloomDownscale = downscale;
        grade.bloomHighQualityFiltering = highQuality;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.InRange(pixels[2], (byte)225, (byte)255);
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0)]
    [InlineData(0f, 1f, 0f, 1)]
    [InlineData(0f, 0f, 1f, 2)]
    public void MetalRenderTexture_HdrBloom_TintControlsBloomContribution(
        float tintR, float tintG, float tintB, int dominantChannel)
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = 6;
        grade.bloomScatter = 1f;
        grade.bloomTintR = tintR;
        grade.bloomTintG = tintG;
        grade.bloomTintB = tintB;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        int outsideHighlight = (4 * 8 + 3) * 4;
        Assert.True(pixels[outsideHighlight + dominantChannel] > 0);
        for (int channel = 0; channel < 3; channel++)
            if (channel != dominantChannel)
                Assert.Equal((byte)0, pixels[outsideHighlight + channel]);
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_ScatterRaisesContributionFromLowerMips()
    {
        byte RenderWithScatter(float scatter)
        {
            using var device = NewMetalDevice();
            var target = NewHdrRenderTexture(8, 8);
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
            var grade = AcesGrade();
            grade.bloomThreshold = 0.5f;
            grade.bloomIntensity = 1f;
            grade.bloomMaxIterations = 6;
            grade.bloomScatter = scatter;
            Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
            Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
            return pixels[(4 * 8 + 3) * 4];
        }

        byte focused = RenderWithScatter(0f);
        byte scattered = RenderWithScatter(1f);
        Assert.True(scattered > focused, $"Expected scatter to add lower-mip bloom: {scattered} <= {focused}.");
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_LensDirtTextureModulatesBloomByChannel()
    {
        byte[] RenderWithDirt(float intensity)
        {
            using var device = NewMetalDevice();
            var dirt = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            dirt.SetPixel(0, 0, Color.red);
            dirt.Apply(false, false);
            Assert.True(device.EnsureTexture(dirt));

            var target = NewHdrRenderTexture(8, 8);
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
            var grade = AcesGrade();
            grade.bloomThreshold = 0.5f;
            grade.bloomIntensity = 1f;
            grade.bloomMaxIterations = 6;
            grade.bloomScatter = 1f;
            grade.bloomDirtTextureId = unchecked((ulong)(uint)dirt.GetInstanceID());
            grade.bloomDirtIntensity = intensity;
            Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
            Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
            int outsideHighlight = (4 * 8 + 3) * 4;
            return new[]
            {
                pixels[outsideHighlight], pixels[outsideHighlight + 1], pixels[outsideHighlight + 2]
            };
        }

        byte[] baseline = RenderWithDirt(0f);
        byte[] dirt = RenderWithDirt(1f);
        Assert.Equal(baseline[1], baseline[2]);
        Assert.True(dirt[0] > dirt[1], $"Expected red dirt to raise only the red bloom contribution: {dirt[0]}, {dirt[1]}, {dirt[2]}.");
        Assert.Equal(dirt[1], dirt[2]);
        Assert.True(dirt[0] > baseline[0]);
    }

    [Fact]
    public void MetalRenderTexture_HdrBloom_LensDirtMipMapBiasSelectsCoarserUploadedMip()
    {
        byte[] Render(float mipMapBias)
        {
            using var device = NewMetalDevice();
            var dirt = new Texture2D(8, 8, TextureFormat.RGBA32, true, true)
            {
                filterMode = FilterMode.Point,
                mipMapBias = mipMapBias
            };
            dirt.SetPixels(FilledColors(64, Color.red), 0);
            for (int mip = 1; mip < dirt.mipmapCount; mip++)
                dirt.SetPixels(FilledColors(
                    dirt.GetMipWidth(mip) * dirt.GetMipHeight(mip), Color.green), mip);
            dirt.Apply(false, false);
            Assert.True(device.EnsureTexture(dirt));

            var target = NewHdrRenderTexture(8, 8);
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
            var grade = AcesGrade();
            grade.bloomThreshold = 0.5f;
            grade.bloomIntensity = 1f;
            grade.bloomMaxIterations = 6;
            grade.bloomScatter = 1f;
            grade.bloomDirtTextureId = unchecked((ulong)(uint)dirt.GetInstanceID());
            grade.bloomDirtIntensity = 1f;
            Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
            Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
            int offset = (4 * 8 + 3) * 4;
            return new[] { pixels[offset], pixels[offset + 1], pixels[offset + 2] };
        }

        byte[] baseMip = Render(0f);
        byte[] nextMip = Render(1f);
        Assert.True(baseMip[0] > baseMip[1],
            $"Expected base red Lens Dirt mip: {baseMip[0]}, {baseMip[1]}, {baseMip[2]}.");
        Assert.True(nextMip[1] > nextMip[0],
            $"Expected +1 mip bias to select green mip: {nextMip[0]}, {nextMip[1]}, {nextMip[2]}.");
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0.125f, 0)]
    [InlineData(1f, 0f, 0f, 0.25f, 0)]
    [InlineData(1f, 0f, 0f, 0.5f, 0)]
    [InlineData(1f, 0f, 0f, 1f, 0)]
    [InlineData(1f, 0f, 0f, 2f, 0)]
    [InlineData(0f, 1f, 0f, 0.5f, 1)]
    [InlineData(0f, 1f, 0f, 1f, 1)]
    [InlineData(0f, 0f, 1f, 0.5f, 2)]
    [InlineData(0f, 0f, 1f, 1f, 2)]
    [InlineData(1f, 1f, 1f, 1f, -1)]
    public void MetalRenderTexture_HdrBloom_LensDirtIntensityAndChannelsAreObservable(
        float dirtR, float dirtG, float dirtB, float intensity, int dominantChannel)
    {
        byte[] Render(float dirtIntensity)
        {
            using var device = NewMetalDevice();
            var dirt = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            dirt.SetPixel(0, 0, new Color(dirtR, dirtG, dirtB, 1f));
            dirt.Apply(false, false);
            Assert.True(device.EnsureTexture(dirt));

            var target = NewHdrRenderTexture(8, 8);
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
            Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
            var grade = AcesGrade();
            grade.bloomThreshold = 0.5f;
            grade.bloomIntensity = 1f;
            grade.bloomMaxIterations = 6;
            grade.bloomScatter = 1f;
            grade.bloomDirtTextureId = unchecked((ulong)(uint)dirt.GetInstanceID());
            grade.bloomDirtIntensity = dirtIntensity;
            Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
            Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
            int outsideHighlight = (4 * 8 + 3) * 4;
            return new[]
            {
                pixels[outsideHighlight], pixels[outsideHighlight + 1], pixels[outsideHighlight + 2]
            };
        }

        byte[] baseline = Render(0f);
        byte[] dirt = Render(intensity);
        if (dominantChannel >= 0)
        {
            Assert.True(dirt[dominantChannel] > baseline[dominantChannel]);
            for (int channel = 0; channel < 3; channel++)
                if (channel != dominantChannel)
                    Assert.Equal(baseline[channel], dirt[channel]);
        }
        else
        {
            for (int channel = 0; channel < 3; channel++)
                Assert.True(dirt[channel] > baseline[channel]);
        }
    }

    [Theory]
    [InlineData(255, 0, 0, 32, 0)]
    [InlineData(255, 0, 0, 64, 0)]
    [InlineData(255, 0, 0, 128, 0)]
    [InlineData(255, 0, 0, 255, 0)]
    [InlineData(255, 0, 0, 510, 0)]
    [InlineData(0, 255, 0, 128, 1)]
    [InlineData(0, 255, 0, 255, 1)]
    [InlineData(0, 0, 255, 128, 2)]
    [InlineData(0, 0, 255, 255, 2)]
    [InlineData(255, 255, 255, 255, -1)]
    public void NativeHdrProcessFrame_LensDirtRgba8ModulatesBloomChannels(
        int dirtR, int dirtG, int dirtB, int intensityPercent, int dominantChannel)
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = 6;
        grade.bloomScatter = 1f;
        grade.bloomDirtIntensity = intensityPercent / 255f;
        byte[] dirt = { (byte)dirtR, (byte)dirtG, (byte)dirtB, 255 };

        float[] baseline = new float[input.Length];
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 8, 8, ref grade, baseline, 0));
        float[] output = ProcessCpuLensDirt(input, grade, dirt, 1, 1);
        int outsideHighlight = (4 * 8 + 3) * 4;
        if (dominantChannel >= 0)
        {
            Assert.True(output[outsideHighlight + dominantChannel] > baseline[outsideHighlight + dominantChannel]);
            for (int channel = 0; channel < 3; channel++)
                if (channel != dominantChannel)
                    Assert.Equal(baseline[outsideHighlight + channel], output[outsideHighlight + channel]);
        }
        else
        {
            for (int channel = 0; channel < 3; channel++)
                Assert.True(output[outsideHighlight + channel] > baseline[outsideHighlight + channel]);
        }
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtRgba8RejectsTruncatedBuffer()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        float[] output = new float[input.Length];
        var grade = AcesGrade();
        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.HDR_ProcessFrameWithLensDirtRGBA8(input, 8, 8, ref grade,
                new byte[3], 1, 1, (int)FilterMode.Point, (int)UnityEngine.TextureWrapMode.Clamp,
                (int)UnityEngine.TextureWrapMode.Clamp, 1, 3, output, 0));
    }

    [Fact]
    public void HdrUtilities_CpuLensDirtHelperUsesTexture2DBaseMip()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var dirt = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        dirt.SetPixel(0, 0, Color.red);
        dirt.Apply(false, false);
        float[] output = new float[input.Length];
        Assert.True(HDRUtilities.ProcessFrameWithBloomLensDirt(
            input, 8, 8, 0f, 1f, 2, dirt, 1f, output));

        var expectedGrade = AcesGrade();
        expectedGrade.bloomIntensity = 1f;
        expectedGrade.bloomScatter = 0.7f;
        expectedGrade.bloomMaxIterations = 6;
        expectedGrade.bloomDownscale = 1;
        expectedGrade.bloomHighQualityFiltering = 1;
        expectedGrade.bloomDirtIntensity = 1f;
        float[] expected = ProcessCpuLensDirt(input, expectedGrade, new byte[] { 255, 0, 0, 255 }, 1, 1);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtPointAndBilinearUseDifferentSampling()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomDirtIntensity = 1f;
        byte[] dirt =
        {
            0, 0, 0, 255, 255, 0, 0, 255,
            255, 0, 0, 255, 0, 0, 0, 255
        };

        float[] point = ProcessCpuLensDirt(input, grade, dirt, 2, 2, FilterMode.Point);
        float[] bilinear = ProcessCpuLensDirt(input, grade, dirt, 2, 2, FilterMode.Bilinear);
        int sample = (4 * 8 + 3) * 4;
        Assert.True(point[sample] > bilinear[sample]);
        Assert.Equal(point[sample + 1], bilinear[sample + 1]);
        Assert.Equal(point[sample + 2], bilinear[sample + 2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtBilinearHonorsRepeatAndClamp()
    {
        float[] input = BloomInput(new Color(0.25f, 0.25f, 0.25f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.01f;
        grade.bloomIntensity = 1f;
        grade.bloomDirtIntensity = 1f;
        byte[] dirt =
        {
            0, 0, 0, 255, 255, 0, 0, 255,
            0, 0, 0, 255, 255, 0, 0, 255
        };

        float[] clamp = ProcessCpuLensDirt(input, grade, dirt, 2, 2,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.Clamp);
        float[] repeat = ProcessCpuLensDirt(input, grade, dirt, 2, 2,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.Repeat);
        int sample = (6 * 8) * 4;
        Assert.True(repeat[sample] > clamp[sample]);
        Assert.Equal(clamp[sample + 1], repeat[sample + 1]);
        Assert.Equal(clamp[sample + 2], repeat[sample + 2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtBilinearHonorsMirrorAndMirrorOnceAtEdge()
    {
        float[] input = BloomInput(new Color(0.25f, 0.25f, 0.25f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.01f;
        grade.bloomIntensity = 1f;
        grade.bloomDirtIntensity = 1f;
        byte[] dirt =
        {
            0, 0, 0, 255, 255, 0, 0, 255,
            0, 0, 0, 255, 255, 0, 0, 255
        };

        float[] clamp = ProcessCpuLensDirt(input, grade, dirt, 2, 2,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.Clamp);
        float[] mirror = ProcessCpuLensDirt(input, grade, dirt, 2, 2,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.Mirror);
        float[] mirrorOnce = ProcessCpuLensDirt(input, grade, dirt, 2, 2,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.MirrorOnce);
        int sample = (6 * 8) * 4;
        Assert.Equal(clamp[sample], mirror[sample]);
        Assert.Equal(clamp[sample], mirrorOnce[sample]);
        Assert.Equal(clamp[sample + 1], mirror[sample + 1]);
        Assert.Equal(clamp[sample + 2], mirrorOnce[sample + 2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtDecodesNonLinearTextureData()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomDirtIntensity = 1f;
        byte[] dirt = { 128, 0, 0, 255 };

        float[] linear = ProcessCpuLensDirt(input, grade, dirt, 1, 1,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.Clamp, true);
        float[] gamma = ProcessCpuLensDirt(input, grade, dirt, 1, 1,
            FilterMode.Bilinear, UnityEngine.TextureWrapMode.Clamp, false);
        int sample = (4 * 8 + 3) * 4;
        Assert.True(linear[sample] > gamma[sample]);
        Assert.Equal(linear[sample + 1], gamma[sample + 1]);
        Assert.Equal(linear[sample + 2], gamma[sample + 2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtTrilinearBlendsAdjacentMipLevels()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomDirtIntensity = 1f;
        byte[] dirt = PackedDirtMips(12, 12, 4, 255, 0, 0);

        float[] bilinear = ProcessCpuLensDirtMips(input, grade, dirt, 12, 12, 4,
            FilterMode.Bilinear);
        float[] trilinear = ProcessCpuLensDirtMips(input, grade, dirt, 12, 12, 4,
            FilterMode.Trilinear);
        int sample = (4 * 8 + 3) * 4;
        Assert.True(bilinear[sample] > trilinear[sample]);
        Assert.Equal(bilinear[sample + 1], trilinear[sample + 1]);
        Assert.Equal(bilinear[sample + 2], trilinear[sample + 2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtMipBiasSelectsCoarserLevel()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomDirtIntensity = 1f;
        byte[] dirt = PackedDirtMips(12, 12, 4, 255, 0, 0);

        float[] unbiased = ProcessCpuLensDirtMips(input, grade, dirt, 12, 12, 4,
            FilterMode.Bilinear);
        float[] biased = ProcessCpuLensDirtMips(input, grade, dirt, 12, 12, 4,
            FilterMode.Bilinear, mipBias: 1f);
        int sample = (4 * 8 + 3) * 4;
        Assert.True(unbiased[sample] > biased[sample]);
        Assert.Equal(unbiased[sample + 1], biased[sample + 1]);
        Assert.Equal(unbiased[sample + 2], biased[sample + 2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtMipAbiRejectsTruncatedMipChain()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        float[] output = new float[input.Length];
        var grade = AcesGrade();
        byte[] baseMipOnly = new byte[12 * 12 * 4];
        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.HDR_ProcessFrameWithLensDirtRGBA8Mips(input, 8, 8, ref grade,
                baseMipOnly, 12, 12, 2, (int)FilterMode.Trilinear,
                (int)UnityEngine.TextureWrapMode.Clamp, (int)UnityEngine.TextureWrapMode.Clamp,
                1, baseMipOnly.Length, output, 0));
    }

    [Fact]
    public void NativeHdrProcessFrame_LensDirtMipAbiRejectsLevelsPastOneByOneTail()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        float[] output = new float[input.Length];
        var grade = AcesGrade();
        byte[] overSpecified = PackedDirtMips(2, 2, 3, 255, 0, 0);
        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.HDR_ProcessFrameWithLensDirtRGBA8Mips(input, 8, 8, ref grade,
                overSpecified, 2, 2, 3, (int)FilterMode.Trilinear,
                (int)UnityEngine.TextureWrapMode.Clamp, (int)UnityEngine.TextureWrapMode.Clamp,
                1, overSpecified.Length, output, 0));
    }

    [Fact]
    public void HdrUtilities_CpuLensDirtHelperPreservesTexture2DMipChain()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var dirt = new Texture2D(12, 12, TextureFormat.RGBA32, true, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = UnityEngine.TextureWrapMode.Clamp,
            mipMapBias = 0.5f
        };
        dirt.SetPixels(FilledColors(12 * 12, Color.red), 0);
        for (int mip = 1; mip < dirt.mipmapCount; mip++)
            dirt.SetPixels(FilledColors(dirt.GetMipWidth(mip) * dirt.GetMipHeight(mip), Color.black), mip);
        dirt.Apply(false, false);

        float[] output = new float[input.Length];
        Assert.True(HDRUtilities.ProcessFrameWithBloomLensDirt(
            input, 8, 8, 0f, 1f, 2, dirt, 1f, output));
        var grade = AcesGrade();
        grade.bloomIntensity = 1f;
        grade.bloomScatter = 0.7f;
        grade.bloomMaxIterations = 6;
        grade.bloomDownscale = 1;
        grade.bloomHighQualityFiltering = 1;
        grade.bloomDirtIntensity = 1f;
        float[] expected = ProcessCpuLensDirtMips(input, grade, dirt.GetRawTextureData(),
            dirt.width, dirt.height, dirt.mipmapCount, FilterMode.Trilinear, mipBias: dirt.mipMapBias);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(0, 0, 0, 0f)]
    [InlineData(6, 0, 1, 1f)]
    [InlineData(8, 1, 1, 0.7f)]
    public void NativeHdrProcessFrame_LensDirtRgba8TracksMetalPixels(
        int iterations, int downscale, int highQuality, float scatter)
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = iterations;
        grade.bloomDownscale = downscale;
        grade.bloomHighQualityFiltering = highQuality;
        grade.bloomScatter = scatter;
        grade.bloomDirtIntensity = 1f;
        byte[] dirtPixels = { 255, 0, 0, 255 };
        float[] cpu = ProcessCpuLensDirt(input, grade, dirtPixels, 1, 1);

        using var device = NewMetalDevice();
        var dirt = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        dirt.SetPixel(0, 0, Color.red);
        dirt.Apply(false, false);
        Assert.True(device.EnsureTexture(dirt));
        grade.bloomDirtTextureId = unchecked((ulong)(uint)dirt.GetInstanceID());
        var target = NewHdrRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var metal));
        foreach (int pixel in new[] { 6 * 8, 3 * 8 + 3, 4 * 8 + 3, 7 * 8 + 7 })
        for (int channel = 0; channel < 3; channel++)
        {
            int expected = (int)MathF.Round(cpu[pixel * 4 + channel] * 255f);
            Assert.True(Math.Abs(metal[pixel * 4 + channel] - expected) <= 2,
                $"pixel={pixel} channel={channel} cpu={expected} metal={metal[pixel * 4 + channel]}");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void NativeHdrProcessFrame_BloomPyramidHonorsIterationCount(int iterations)
    {
        float[] input = BloomInput(new Color(4f, 0f, 0f, 1f));
        float[] output = new float[input.Length];
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = iterations;
        grade.bloomScatter = 0.7f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 8, 8, ref grade, output, 0));
        int highlight = 6 * 8 * 4;
        Assert.InRange(output[highlight], 0.9f, 1f);
        Assert.Equal(0f, output[highlight + 1]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public void NativeHdrProcessFrame_BloomPyramidHonorsDownscaleAndFilter(int downscale, int highQuality)
    {
        float[] input = BloomInput(new Color(0f, 0f, 4f, 1f));
        float[] output = new float[input.Length];
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = 6;
        grade.bloomDownscale = downscale;
        grade.bloomHighQualityFiltering = highQuality;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 8, 8, ref grade, output, 0));
        int highlight = 6 * 8 * 4;
        Assert.Equal(0f, output[highlight]);
        Assert.InRange(output[highlight + 2], 0.9f, 1f);
    }

    [Fact]
    public void NativeHdrProcessFrame_BloomPyramidScatterAddsLowerMipContribution()
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        float[] Render(float scatter)
        {
            float[] output = new float[input.Length];
            var grade = AcesGrade();
            grade.bloomThreshold = 0.5f;
            grade.bloomIntensity = 1f;
            grade.bloomMaxIterations = 6;
            grade.bloomScatter = scatter;
            Assert.Equal(AnityNative.Result.Ok,
                AnityNative.HDR_ProcessFrame(input, 8, 8, ref grade, output, 0));
            return output;
        }

        float[] focused = Render(0f);
        float[] scattered = Render(1f);
        int outsideHighlight = (4 * 8 + 3) * 4;
        Assert.True(scattered[outsideHighlight] > focused[outsideHighlight]);
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0)]
    [InlineData(0f, 1f, 0f, 1)]
    [InlineData(0f, 0f, 1f, 2)]
    public void NativeHdrProcessFrame_BloomPyramidTintControlsContribution(
        float tintR, float tintG, float tintB, int dominantChannel)
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        float[] output = new float[input.Length];
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = 6;
        grade.bloomScatter = 1f;
        grade.bloomTintR = tintR;
        grade.bloomTintG = tintG;
        grade.bloomTintB = tintB;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 8, 8, ref grade, output, 0));
        int outsideHighlight = (4 * 8 + 3) * 4;
        Assert.True(output[outsideHighlight + dominantChannel] > 0f);
        for (int channel = 0; channel < 3; channel++)
            if (channel != dominantChannel)
                Assert.Equal(0f, output[outsideHighlight + channel]);
    }

    [Theory]
    [InlineData(1, 0, 0, 0f)]
    [InlineData(2, 0, 0, 0.5f)]
    [InlineData(6, 0, 1, 1f)]
    [InlineData(8, 1, 1, 0.7f)]
    public void NativeHdrProcessFrame_BloomPyramidTracksMetalPixels(
        int iterations, int downscale, int highQuality, float scatter)
    {
        float[] input = BloomInput(new Color(4f, 4f, 4f, 1f));
        var grade = AcesGrade();
        grade.bloomThreshold = 0.5f;
        grade.bloomIntensity = 1f;
        grade.bloomMaxIterations = iterations;
        grade.bloomDownscale = downscale;
        grade.bloomHighQualityFiltering = highQuality;
        grade.bloomScatter = scatter;
        float[] cpu = new float[input.Length];
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 8, 8, ref grade, cpu, 0));

        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.black, true, hdr: true));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 2, 2), new Color(4f, 4f, 4f, 1f), true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var metal));

        foreach (int pixel in new[] { 0, 3 * 8 + 3, 4 * 8 + 3, 7 * 8 + 7 })
        for (int channel = 0; channel < 3; channel++)
        {
            int expected = (int)MathF.Round(cpu[pixel * 4 + channel] * 255f);
            Assert.True(Math.Abs(metal[pixel * 4 + channel] - expected) <= 2,
                $"pixel={pixel} channel={channel} cpu={expected} metal={metal[pixel * 4 + channel]}");
        }
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_NegativeSaturationDesaturatesPrimary()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.saturation = -100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(Math.Abs(pixels[0] - pixels[1]), 0, 2);
        Assert.InRange(Math.Abs(pixels[1] - pixels[2]), 0, 2);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_PositiveSaturationKeepsPrimaryDominant()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), new Color(0.8f, 0.3f, 0.1f, 1f), true, hdr: true));
        var grade = AcesGrade();
        grade.saturation = 100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1]);
        Assert.True(pixels[0] > pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_WarmTemperatureRaisesRedOverBlue()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.temperature = 100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_CoolTemperatureRaisesBlueOverRed()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.temperature = -100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[2] > pixels[0]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_MagentaTintReducesGreen()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.tint = 100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1], $"RGB was {pixels[0]}, {pixels[1]}, {pixels[2]}");
        Assert.True(pixels[2] > pixels[1], $"RGB was {pixels[0]}, {pixels[1]}, {pixels[2]}");
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_GreenTintRaisesGreen()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.tint = -100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[1] > pixels[0]);
        Assert.True(pixels[1] > pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_WhiteBalanceSurvivesMsaaResolve()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true,
            hdr: true, msaaSamples: 4));
        var grade = AcesGrade();
        grade.temperature = 100f;
        grade.tint = 100f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1]);
        Assert.True(pixels[1] > pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_HdrGrade_SaturationExecutesOnHeadlessSwapchain()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        var grade = AcesGrade();
        grade.saturation = -100f;
        Assert.True(device.TryProcessSwapchainHDR(grade));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.InRange(Math.Abs(pixels[0] - pixels[1]), 0, 2);
        Assert.InRange(Math.Abs(pixels[1] - pixels[2]), 0, 2);
    }

    [Fact]
    public void NativeHdrProcessFrame_WhiteBalanceMatchesExpectedChannelOrdering()
    {
        var input = new[] { 1f, 1f, 1f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.temperature = 100f;
        grade.tint = 100f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.True(output[0] > output[1]);
        Assert.True(output[1] > output[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_SaturationMatchesExpectedChannelOrdering()
    {
        var input = new[] { 1f, 0f, 0f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.saturation = -100f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.InRange(Math.Abs(output[0] - output[1]), 0f, 0.001f);
        Assert.InRange(Math.Abs(output[1] - output[2]), 0f, 0.001f);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_HueShiftPositive120TurnsRedGreen()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.hueShift = 120f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[1] > pixels[0]);
        Assert.True(pixels[1] > pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_HueShiftNegative120TurnsRedBlue()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.hueShift = -120f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[2] > pixels[0]);
        Assert.True(pixels[2] > pixels[1]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_HueShift180TurnsRedCyan()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.hueShift = 180f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[1] > pixels[0]);
        Assert.True(pixels[2] > pixels[0]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_HueShiftWrapsAtFullTurn()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.hueShift = 360f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1]);
        Assert.True(pixels[0] > pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ColorFilterKeepsOnlyBlue()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.colorFilterR = 0f;
        grade.colorFilterG = 0f;
        grade.colorFilterB = 1f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ColorFilterClampsNegativeChannels()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = AcesGrade();
        grade.colorFilterR = -3f;
        grade.colorFilterG = 1f;
        grade.colorFilterB = -1f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.InRange(pixels[1], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ColorFilterSurvivesMsaaResolve()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true,
            hdr: true, msaaSamples: 4));
        var grade = AcesGrade();
        grade.colorFilterR = 1f;
        grade.colorFilterG = 0f;
        grade.colorFilterB = 0f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraTarget_HdrGrade_HueShiftExecutesOnHeadlessSwapchain()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        var grade = AcesGrade();
        grade.hueShift = 120f;
        Assert.True(device.TryProcessSwapchainHDR(grade));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.True(pixels[1] > pixels[0]);
        Assert.True(pixels[1] > pixels[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_HueShiftMatchesExpectedChannelOrdering()
    {
        var input = new[] { 1f, 0f, 0f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.hueShift = 120f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.True(output[1] > output[0]);
        Assert.True(output[1] > output[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_ColorFilterMatchesExpectedChannelOrdering()
    {
        var input = new[] { 1f, 1f, 1f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.colorFilterR = 0f;
        grade.colorFilterG = 1f;
        grade.colorFilterB = 0f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.Equal(0f, output[0]);
        Assert.True(output[1] > output[0]);
        Assert.Equal(0f, output[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_HueShiftRunsBeforeSaturation()
    {
        var input = new[] { 1f, 0f, 0f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.hueShift = 120f;
        grade.saturation = -100f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.InRange(Math.Abs(output[0] - output[1]), 0f, 0.001f);
        Assert.InRange(Math.Abs(output[1] - output[2]), 0f, 0.001f);
    }

    [Fact]
    public void NativeHdrProcessFrame_ColorFilterRunsBeforeHueShift()
    {
        var input = new[] { 1f, 1f, 1f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.colorFilterR = 1f;
        grade.colorFilterG = 0f;
        grade.colorFilterB = 0f;
        grade.hueShift = 120f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.True(output[1] > output[0]);
        Assert.True(output[1] > output[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ChannelMixerRoutesGreenIntoRedOutput()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true, hdr: true));
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerRedG = 1f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueB = 0f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ChannelMixerRoutesRedIntoBlueOutput()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueR = 1f;
        grade.mixerBlueB = 0f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ChannelMixerIdentityPreservesPrimary()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, AcesGrade()));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ChannelMixerSupportsPositiveGain()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.mixerRedR = 2f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)245, (byte)255);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ChannelMixerNegativeContributionClampsAtOutput()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = AcesGrade();
        grade.mixerRedR = -1f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueB = 0f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ChannelMixerSurvivesMsaaResolve()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true,
            hdr: true, msaaSamples: 4));
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerRedG = 1f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueB = 0f;
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)225, (byte)240);
        Assert.Equal((byte)0, pixels[1]);
    }

    [Fact]
    public void MetalCameraTarget_HdrGrade_ChannelMixerExecutesOnHeadlessSwapchain()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueR = 1f;
        grade.mixerBlueB = 0f;
        Assert.True(device.TryProcessSwapchainHDR(grade));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.InRange(pixels[2], (byte)225, (byte)240);
    }

    [Fact]
    public void NativeHdrProcessFrame_ChannelMixerMatchesExpectedRows()
    {
        var input = new[] { 0f, 1f, 0f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerRedG = 1f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueB = 0f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.True(output[0] > output[1]);
        Assert.True(output[0] > output[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_ChannelMixerRunsBeforeHueShift()
    {
        var input = new[] { 0f, 1f, 0f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerRedG = 1f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueB = 0f;
        grade.hueShift = 120f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.True(output[1] > output[0]);
        Assert.True(output[1] > output[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_ChannelMixerPreservesAlpha()
    {
        var input = new[] { 1f, 0f, 0f, 0.25f };
        var output = new float[4];
        var grade = AcesGrade();
        grade.mixerRedR = 0f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueR = 1f;
        grade.mixerBlueB = 0f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.Equal(0.25f, output[3]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_MasterCurveDarkensAllChannels()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = CurveGrade(0, 0.5f);
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.InRange(pixels[0], (byte)185, (byte)215);
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[1], pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_RedCurveCanSuppressRed()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        var grade = CurveGrade(1, 0f);
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_GreenCurveOnlyAffectsGreen()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = CurveGrade(2, 0f);
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1]);
        Assert.True(pixels[2] > pixels[1]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_BlueCurveOnlyAffectsBlue()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true, hdr: true));
        var grade = CurveGrade(3, 0f);
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[2], $"RGB was {pixels[0]}, {pixels[1]}, {pixels[2]}");
        Assert.True(pixels[1] > pixels[2], $"RGB was {pixels[0]}, {pixels[1]}, {pixels[2]}");
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_CurvesSurviveMsaaResolve()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true,
            hdr: true, msaaSamples: 4));
        var grade = CurveGrade(1, 0f);
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
    }

    [Fact]
    public void MetalCameraTarget_HdrGrade_CurvesExecuteOnHeadlessSwapchain()
    {
        using var device = NewMetalHdrCameraTargetDevice(1);
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.red, true, true, 1, true, true));
        var grade = CurveGrade(1, 0f);
        Assert.True(device.TryProcessSwapchainHDR(grade));
        Assert.True(device.TryReadbackSwapchainToneMappedRGBA8(out var pixels));
        Assert.Equal((byte)0, pixels[0]);
    }

    [Fact]
    public void NativeHdrProcessFrame_CurveLutInterpolatesBetweenSamples()
    {
        var input = new[] { 0.5f, 0f, 0f, 1f };
        var output = new float[4];
        var grade = CurveGrade(0, 1f);
        for (int index = 0; index < PostProcessRuntime.ColorCurveSamples; index++)
        {
            float x = index / (float)(PostProcessRuntime.ColorCurveSamples - 1);
            grade.curveLut[index] = x * x;
        }
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.InRange(output[0], 0.55f, 0.72f);
    }

    [Fact]
    public void NativeHdrProcessFrame_CurveLutDisabledKeepsIdentity()
    {
        var input = new[] { 1f, 0f, 0f, 1f };
        var output = new float[4];
        var grade = AcesGrade();
        Array.Fill(grade.curveLut, 0f);
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.True(output[0] > output[1]);
        Assert.True(output[0] > output[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_ChannelMixerFeedsSingleChannelCurve()
    {
        var input = new[] { 0f, 1f, 0f, 1f };
        var output = new float[4];
        var grade = CurveGrade(1, 0f);
        grade.mixerRedR = 0f;
        grade.mixerRedG = 1f;
        grade.mixerGreenG = 0f;
        grade.mixerBlueB = 0f;
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.Equal(0f, output[0]);
        Assert.Equal(0f, output[1]);
        Assert.Equal(0f, output[2]);
    }

    [Fact]
    public void TextureCurve_LoopAndZeroValueSemanticsAreSampledBeforeBake()
    {
        var curve = new TextureCurve(AnimationCurve.Linear(0f, 0f, 1f, 1f), 1f, true, Color.white);
        Assert.Equal(0.25f, curve.Evaluate(1.25f), 3);
        Assert.Equal(1f, curve.Evaluate(0f), 3);
    }

    [Fact]
    public void ColorCurves_DefaultVolumeIsInactiveButNonLinearCurveIsActive()
    {
        var curves = new ColorCurves();
        Assert.False(curves.IsActive());
        curves.red.value = new TextureCurve(AnimationCurve.Linear(0f, 0f, 1f, 0f), 0f, false, Color.red);
        Assert.True(curves.IsActive());
    }

    [Fact]
    public void ColorCurves_BakesMasterAndRedTextureCurvesIntoExpectedLutRows()
    {
        var curves = new ColorCurves();
        curves.master.value = new TextureCurve(AnimationCurve.Linear(0f, 0f, 1f, 0.5f), 0f, false, Color.white);
        curves.red.value = new TextureCurve(AnimationCurve.Linear(0f, 0f, 1f, 0f), 0f, false, Color.red);
        float[] lut = PostProcessRuntime.BakeColorCurves(curves);
        int last = PostProcessRuntime.ColorCurveSamples - 1;
        Assert.Equal(0.5f, lut[last], 4);
        Assert.Equal(0f, lut[PostProcessRuntime.ColorCurveSamples + last], 4);
        Assert.Equal(1f, lut[PostProcessRuntime.ColorCurveSamples * 2 + last], 4);
        Assert.Equal(1f, lut[PostProcessRuntime.ColorCurveSamples * 3 + last], 4);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_HueVsHueCurveRedirectsGreenToRed()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, CurveGrade(4, 0f)));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1]);
        Assert.True(pixels[0] > pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_HueVsHueCurveRedirectsBlueToRed()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, CurveGrade(4, 0f)));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.True(pixels[0] > pixels[1]);
        Assert.True(pixels[0] > pixels[2]);
    }

    [Theory]
    [InlineData(5, 255, 0, 0)]
    [InlineData(6, 0, 255, 0)]
    [InlineData(7, 0, 0, 255)]
    public void MetalRenderTexture_HdrGrade_SaturationModifierCurveDesaturatesPrimary(
        int curveIndex, byte red, byte green, byte blue)
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        var color = new Color(red / 255f, green / 255f, blue / 255f, 1f);
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), color, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, CurveGrade(curveIndex, 0f)));
        Assert.True(device.TryReadbackCameraRenderTargetToneMappedRGBA8(target, out var pixels));
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[1], pixels[2]);
    }

    [Fact]
    public void NativeHdrProcessFrame_HsvCurvesPreserveAlpha()
    {
        var input = new[] { 0f, 1f, 0f, 0.25f };
        var output = new float[4];
        var grade = CurveGrade(5, 0f);
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrame(input, 1, 1, ref grade, output, 0));
        Assert.Equal(0.25f, output[3]);
        Assert.Equal(output[0], output[1], 4);
        Assert.Equal(output[1], output[2], 4);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void ColorCurves_NonIdentitySaturationModifiersMarkVolumeActive(int curveIndex)
    {
        var curves = new ColorCurves();
        var disabled = new TextureCurve(AnimationCurve.Constant(0f, 1f, 0f), 0f, false, Color.white);
        switch (curveIndex)
        {
            case 5: curves.hueVsSat.value = disabled; break;
            case 6: curves.satVsSat.value = disabled; break;
            case 7: curves.lumVsSat.value = disabled; break;
            default: throw new ArgumentOutOfRangeException(nameof(curveIndex));
        }
        Assert.True(curves.IsActive());
    }

    [Fact]
    public void ColorCurves_HueVsHueCurveMarksVolumeActive()
    {
        var curves = new ColorCurves();
        curves.hueVsHue.value = new TextureCurve(AnimationCurve.Constant(0f, 1f, 0f), 0f, false, Color.red);
        Assert.True(curves.IsActive());
    }

    [Fact]
    public void ColorCurves_BakesHsvAndLuminanceRowsIntoExpectedLutSlots()
    {
        var curves = new ColorCurves();
        var zero = new TextureCurve(AnimationCurve.Constant(0f, 1f, 0f), 0f, false, Color.white);
        curves.hueVsHue.value = zero;
        curves.hueVsSat.value = zero;
        curves.satVsSat.value = zero;
        curves.lumVsSat.value = zero;
        float[] lut = PostProcessRuntime.BakeColorCurves(curves);
        int last = PostProcessRuntime.ColorCurveSamples - 1;
        for (int curve = 4; curve < 8; curve++)
            Assert.Equal(0f, lut[curve * PostProcessRuntime.ColorCurveSamples + last], 4);
    }

    [Fact]
    public void ColorCurves_IdentityLutUsesHueRampAndUnitSaturationModifiers()
    {
        float[] lut = PostProcessRuntime.CreateIdentityCurveLut();
        int last = PostProcessRuntime.ColorCurveSamples - 1;
        Assert.Equal((last / 2) / (float)last,
            lut[4 * PostProcessRuntime.ColorCurveSamples + last / 2], 4);
        for (int curve = 5; curve < 8; curve++)
            Assert.Equal(1f, lut[curve * PostProcessRuntime.ColorCurveSamples + last], 4);
    }

    [Fact]
    public void ColorCurves_Uses128SamplesForEachOfEightNativeCurveRows()
    {
        Assert.Equal(128, PostProcessRuntime.ColorCurveSamples);
        Assert.Equal(128 * 8, PostProcessRuntime.CreateIdentityCurveLut().Length);
    }

    [Fact]
    public void ColorCurves_ReusesManagedLutUntilCurveKeysChange()
    {
        var curves = new ColorCurves();
        float[] first = PostProcessRuntime.BakeColorCurves(curves);
        float[] reuse = PostProcessRuntime.BakeColorCurves(curves);
        Assert.Same(first, reuse);

        curves.red.value.curve.keys =
        [
            new Keyframe(0f, 0f),
            new Keyframe(1f, 0.25f)
        ];
        float[] changed = PostProcessRuntime.BakeColorCurves(curves);
        Assert.NotSame(first, changed);
        Assert.Equal(0.25f, changed[PostProcessRuntime.ColorCurveSamples * 2 - 1], 4);
    }

    [Fact]
    public void ColorCurves_ManagedLutCacheInvalidatesForLoopZeroAndWrapSettings()
    {
        var curves = new ColorCurves();
        float[] initial = PostProcessRuntime.BakeColorCurves(curves);
        curves.hueVsHue.value.loop = 1f;
        float[] loopChanged = PostProcessRuntime.BakeColorCurves(curves);
        Assert.NotSame(initial, loopChanged);

        curves.hueVsHue.value.zeroValueIsOne = true;
        float[] zeroChanged = PostProcessRuntime.BakeColorCurves(curves);
        Assert.NotSame(loopChanged, zeroChanged);

        curves.hueVsHue.value.curve.preWrapMode = WrapMode.Loop;
        float[] wrapChanged = PostProcessRuntime.BakeColorCurves(curves);
        Assert.NotSame(zeroChanged, wrapChanged);
    }

    [Fact]
    public void MetalRenderTexture_HdrGrade_ReusesUnchangedCurveLutBuffer()
    {
        using var device = NewMetalDevice();
        var target = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(device.TryGetHDRPostProcessStats(out var before));
        Assert.Equal(2, before.backendKind);

        var grade = CurveGrade(1, 0.5f);
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryGetHDRPostProcessStats(out var firstUpload));
        Assert.Equal(128, firstUpload.curveLutSamplesPerCurve);
        Assert.Equal((ulong)(128 * 8 * sizeof(float)), firstUpload.curveLutByteCapacity);
        Assert.True(firstUpload.curveLutUploadCount > before.curveLutUploadCount);

        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true, hdr: true));
        Assert.True(device.TryProcessCameraRenderTargetHDR(target, grade));
        Assert.True(device.TryGetHDRPostProcessStats(out var reuse));
        Assert.Equal(firstUpload.curveLutUploadCount, reuse.curveLutUploadCount);
        Assert.True(reuse.curveLutCacheHitCount > firstUpload.curveLutCacheHitCount);
    }

    [Fact]
    public void MetalRenderTexture_TargetIdTwoIsNotMistakenForCameraTarget()
    {
        using var device = NewMetalDevice();
        var desc = new AnityNative.GraphicsCameraRenderTargetDesc
        {
            targetId = 2,
            width = 4,
            height = 4,
            msaaSamples = 1,
            hdrEnabled = 0
        };
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.Graphics_EnsureCameraRenderTarget(device.Handle, ref desc));

        Assert.True(device.TryRecordCameraPass(2, 4, 4, new Rect(0, 0, 4, 4),
            Color.magenta, true, true, 1, false, false, isCameraTarget: false));
        var pixels = new byte[4 * 4 * 4];
        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_ReadbackCameraRenderTargetRGBA8(
            device.Handle, 2, pixels, pixels.Length, out int written));
        Assert.Equal(pixels.Length, written);
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_RecordWithoutRegistrationIsRejected()
    {
        using var device = NewMetalDevice();

        Assert.False(device.TryRecordCameraPass(9999, 4, 4, new Rect(0, 0, 4, 4),
            Color.black, true, true, 1, false, false, isCameraTarget: false));
    }

    [Fact]
    public void MetalRenderTexture_MsaaAttachment_AllocatesNativeResolvePair()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(4, 4);
        target.msaaSamples = 4;

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true,
            msaaSamples: 4));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
        Assert.Equal((byte)255, pixels[0]);
    }

    [Fact]
    public void MetalRenderTexture_Msaa2x_ResolvesToLdrReadback()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(4, 4, 2);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.green, true,
            msaaSamples: 2));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
        Assert.Equal((byte)255, pixels[1]);
    }

    [Fact]
    public void MetalRenderTexture_Msaa8x_RespectsDeviceSampleSupport()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(4, 4, 8);
        if (!device.EnsureCameraRenderTarget(target))
            return; // Device's native capability boundary is a normal NotSupported result.
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true,
            msaaSamples: 8));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
        Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_MsaaOverlayLoad_PreservesResolvedBaseColor()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true,
            msaaSamples: 4));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, false,
            msaaSamples: 4));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_MsaaPartialClear_PreservesResolvedPixelsOutsideRect()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(8, 8, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 8, 8), Color.red, true,
            msaaSamples: 4));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.blue, true,
            msaaSamples: 4));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
        int lowerLeft = 7 * 8 * 4;
        Assert.Equal((byte)255, pixels[lowerLeft + 2]);
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_MsaaResize_RecreatesSampledAttachments()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        target.descriptor = new RenderTextureDescriptor(6, 2, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = 2
        };
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 6, 2), Color.red, true,
            msaaSamples: 2));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
        Assert.Equal(6 * 2 * 4, pixels.Length);
        Assert.Equal((byte)255, pixels[0]);
    }

    [Fact]
    public void MetalRenderTexture_MsaaRelease_ReleasesResolveAndRenderAttachments()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.red, true,
            msaaSamples: 4));
        target.Release();
        Assert.False(device.TryReadbackCameraRenderTargetRGBA8(target, out _));
    }

    [Fact]
    public void MetalRenderTexture_MsaaAndSingleSampleTargetsRemainIsolated()
    {
        using var device = NewMetalDevice();
        var msaa = NewMsaaRenderTexture(4, 4, 4);
        var single = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(msaa));
        Assert.True(device.EnsureCameraRenderTarget(single));
        Assert.True(RecordTarget(device, msaa, new Rect(0, 0, 4, 4), Color.red, true,
            msaaSamples: 4));
        Assert.True(RecordTarget(device, single, new Rect(0, 0, 4, 4), Color.blue, true));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(msaa, out var msaaPixels));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(single, out var singlePixels));
        Assert.Equal((byte)255, msaaPixels[0]);
        Assert.Equal((byte)255, singlePixels[2]);
    }

    [Fact]
    public void MetalRenderTexture_MsaaHdr_DoesNotImplicitlyToneMapReadback()
    {
        using var device = NewMetalDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.DefaultHDR)
        {
            msaaSamples = 4
        };
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.white, true,
            hdr: true, msaaSamples: 4));
        Assert.False(device.TryReadbackCameraRenderTargetRGBA8(target, out _));
    }

    [Fact]
    public void MetalRenderTexture_MsaaRecordSampleMismatch_IsRejected()
    {
        using var device = NewMetalDevice();
        var target = NewMsaaRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.False(RecordTarget(device, target, new Rect(0, 0, 4, 4), Color.black, true,
            msaaSamples: 2));
    }

    [Fact]
    public void MetalCameraColorCopy_CopiesLdrRenderTextureWithoutCpuReadback()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.red, true));

        Assert.True(device.TryCopyCameraRenderTargetColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraColorCopy_PreservesGreenChannel()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.green, true));

        Assert.True(device.TryCopyCameraRenderTargetColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.Equal((byte)0, pixels[0]);
        Assert.Equal((byte)255, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
    }

    [Fact]
    public void MetalCameraColorCopy_CopiesActiveCameraTarget()
    {
        using var device = NewMetalCameraTargetDevice(1);
        var destination = NewRenderTexture(8, 8);
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8),
            Color.blue, true, true, 1, false, false));

        Assert.True(device.TryCopyCameraRenderTargetColor(null, true, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void MetalCameraColorCopy_UsesResolvedMsaaSource()
    {
        using var device = NewMetalDevice();
        var source = NewMsaaRenderTexture(4, 4, 4);
        var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.magenta, true,
            msaaSamples: 4));

        Assert.True(device.TryCopyCameraRenderTargetColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void MetalCameraColorCopy_WritesResolvedMsaaDestination()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewMsaaRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), new Color(1f, 1f, 0f, 1f), true));

        Assert.True(device.TryCopyCameraRenderTargetColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.Equal((byte)255, pixels[0]);
        Assert.Equal((byte)255, pixels[1]);
    }

    [Fact]
    public void MetalCameraColorCopy_RejectsDifferentDimensions()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewRenderTexture(2, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.white, true));

        Assert.False(device.TryCopyCameraRenderTargetColor(source, false, destination));
    }

    [Fact]
    public void MetalCameraColorCopy_RejectsLdrHdrFormatMismatch()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.white, true));

        Assert.False(device.TryCopyCameraRenderTargetColor(source, false, destination));
    }

    [Fact]
    public void MetalCameraColorCopy_RejectsSelfCopy()
    {
        using var device = NewMetalDevice();
        var target = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(target));

        Assert.False(device.TryCopyCameraRenderTargetColor(target, false, target));
    }

    [Fact]
    public void MetalCameraColorCopy_RejectsReleasedDestination()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        destination.Release();

        Assert.False(device.TryCopyCameraRenderTargetColor(source, false, destination));
    }

    [Fact]
    public void CameraOpaqueTexturePass_PublishesGpuCopiedTextureAndCleansItUp()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.cyan, true));
        var data = new RenderingData
        {
            cameraData = new CameraData
            {
                nativeTargetTexture = source,
                requiresOpaqueTexture = true,
                cameraTargetDescriptor = new RenderTextureDescriptor(4, 4, RenderTextureFormat.ARGB32, 24)
            }
        };
        var pass = new CameraOpaqueTexturePass();

        pass.Execute(default, ref data);

        Assert.NotNull(data.cameraData.opaqueTexture);
        Assert.Same(data.cameraData.opaqueTexture, Shader.GetGlobalTexture(CameraOpaqueTexturePass.GlobalTextureName));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(data.cameraData.opaqueTexture, out var pixels));
        Assert.Equal((byte)255, pixels[1]);
        Assert.Equal((byte)255, pixels[2]);

        var opaque = data.cameraData.opaqueTexture;
        pass.OnCameraCleanup(null);
        Assert.Null(Shader.GetGlobalTexture(CameraOpaqueTexturePass.GlobalTextureName));
        Assert.False(opaque.IsCreated());
    }

    [Fact]
    public void MetalCameraDepthCopy_EncodesSingleSampleClearedDepthIntoRed()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4);
        var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()),
            4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 1, false, false,
            isCameraTarget: false, clearDepthValue: 0.25f));

        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)62, (byte)66);
        Assert.Equal((byte)0, pixels[1]);
        Assert.Equal((byte)0, pixels[2]);
        Assert.Equal((byte)255, pixels[3]);
    }

    [Fact]
    public void MetalCameraDepthCopy_EncodesDifferentDepthValue()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 1, false, false, false, 0.75f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)189, (byte)193);
    }

    [Fact]
    public void MetalCameraDepthCopy_UsesActiveCameraTarget()
    {
        using var device = NewMetalCameraTargetDevice(1);
        var destination = NewRenderTexture(8, 8); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(2, 8, 8, new Rect(0, 0, 8, 8), Color.black, true, false, 1, false, false, true, 0.5f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(null, true, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)126, (byte)129);
    }

    [Fact]
    public void MetalCameraDepthCopy_UsesMsaaSourceSampleZero()
    {
        using var device = NewMetalDevice();
        var source = NewMsaaRenderTexture(4, 4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 4, false, false, false, 0.5f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)126, (byte)129);
    }

    [Fact]
    public void MetalCameraDepthCopy_WritesMsaaDestinationResolve()
    {
        using var device = NewMetalDevice();
        var source = NewRenderTexture(4, 4); var destination = NewMsaaRenderTexture(4, 4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 1, false, false, false, 0.25f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)62, (byte)66);
    }

    [Fact]
    public void MetalCameraDepthCopy_RejectsDifferentDimensions()
    {
        using var device = NewMetalDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(2, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
    }

    [Fact]
    public void MetalCameraDepthCopy_RejectsSelfCopy()
    {
        using var device = NewMetalDevice(); var target = NewRenderTexture(4, 4); Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(target, false, target));
    }

    [Fact]
    public void MetalCameraDepthCopy_RejectsReleasedDestination()
    {
        using var device = NewMetalDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination)); destination.Release();
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
    }

    [Fact]
    public void MetalCameraDepthCopy_RejectsUnregisteredSource()
    {
        using var device = NewMetalDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
    }

    [Fact]
    public void CameraDepthTexturePass_PublishesGpuDepthAndCleansItUp()
    {
        using var device = NewMetalDevice(); var source = NewRenderTexture(4, 4); Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 1, false, false, false, 0.5f));
        var data = new RenderingData { cameraData = new CameraData { nativeTargetTexture = source, requiresDepthTexture = true, cameraTargetDescriptor = new RenderTextureDescriptor(4, 4, RenderTextureFormat.DefaultHDR, 24) } };
        var pass = new CameraDepthTexturePass(); pass.Execute(default, ref data);
        Assert.NotNull(data.cameraData.depthTexture); Assert.Same(data.cameraData.depthTexture, Shader.GetGlobalTexture(CameraDepthTexturePass.GlobalTextureName));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(data.cameraData.depthTexture, out var pixels)); Assert.InRange(pixels[0], (byte)126, (byte)129);
        var depth = data.cameraData.depthTexture; pass.OnCameraCleanup(null);
        Assert.Null(Shader.GetGlobalTexture(CameraDepthTexturePass.GlobalTextureName)); Assert.False(depth.IsCreated());
    }

    [Fact]
    public void CameraNormalsTexturePass_PublishesGpuNormalsAndCleansItUp()
    {
        using var device = NewMetalDevice(); var source = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4,
            new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
        Assert.True(device.TryDrawCameraMesh(source,
            new[] { new Vector3(-.9f, -.9f, .5f), new Vector3(.9f, -.9f, .5f), new Vector3(0f, .9f, .5f) },
            new[] { Vector3.forward, Vector3.forward, Vector3.forward },
            new[] { Color.white, Color.white, Color.white }, new[] { 0, 1, 2 }, Matrix4x4.identity));
        var data = new RenderingData { cameraData = new CameraData { nativeTargetTexture = source,
            requiresNormalsTexture = true, cameraTargetDescriptor = new RenderTextureDescriptor(4, 4, RenderTextureFormat.DefaultHDR, 24) } };
        var pass = new CameraNormalsTexturePass(); pass.Execute(default, ref data);
        Assert.NotNull(data.cameraData.normalsTexture);
        Assert.Equal(GraphicsFormat.R8G8B8A8_SNorm, data.cameraData.normalsTexture.graphicsFormat);
        Assert.Same(data.cameraData.normalsTexture, Shader.GetGlobalTexture(CameraNormalsTexturePass.GlobalTextureName));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(data.cameraData.normalsTexture, out var pixels));
        int center = ((2 * 4) + 2) * 4; Assert.InRange(pixels[center], (byte)120, (byte)136); Assert.True(pixels[center + 2] > 240);
        var normals = data.cameraData.normalsTexture; pass.OnCameraCleanup(null);
        Assert.Null(Shader.GetGlobalTexture(CameraNormalsTexturePass.GlobalTextureName)); Assert.False(normals.IsCreated());
    }

    [Fact]
    public void CameraMotionVectorsTexturePass_PublishesGpuMotionAndCleansItUp()
    {
        using var device = NewMetalDevice(); var source = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4,
            new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
        Assert.True(device.TryDrawCameraMesh(source,
            new[] { new Vector3(-.9f, -.9f, .5f), new Vector3(.9f, -.9f, .5f), new Vector3(0f, .9f, .5f) },
            new[] { Vector3.forward, Vector3.forward, Vector3.forward },
            new[] { Color.white, Color.white, Color.white }, new[] { 0, 1, 2 }, Matrix4x4.identity, null,
            Matrix4x4.Translate(new Vector3(-.5f, 0f, 0f))));
        var data = new RenderingData { cameraData = new CameraData { nativeTargetTexture = source,
            requiresMotionVectors = true, cameraTargetDescriptor = new RenderTextureDescriptor(4, 4, RenderTextureFormat.DefaultHDR, 24) } };
        var pass = new CameraMotionVectorsTexturePass(); pass.Execute(default, ref data);
        Assert.NotNull(data.cameraData.motionVectorTexture);
        Assert.Equal(GraphicsFormat.R16G16_SFloat, data.cameraData.motionVectorTexture.graphicsFormat);
        Assert.Same(data.cameraData.motionVectorTexture, Shader.GetGlobalTexture(CameraMotionVectorsTexturePass.GlobalTextureName));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(data.cameraData.motionVectorTexture, out var pixels));
        int center = ((2 * 4) + 2) * 4; Assert.InRange(pixels[center], (byte)153, (byte)165); Assert.InRange(pixels[center + 1], (byte)120, (byte)136);
        var motion = data.cameraData.motionVectorTexture; pass.OnCameraCleanup(null);
        Assert.Null(Shader.GetGlobalTexture(CameraMotionVectorsTexturePass.GlobalTextureName)); Assert.False(motion.IsCreated());
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.2f)]
    [InlineData(0.3f)]
    [InlineData(0.4f)]
    [InlineData(0.5f)]
    [InlineData(0.6f)]
    [InlineData(0.7f)]
    [InlineData(0.8f)]
    [InlineData(0.9f)]
    [InlineData(1.0f)]
    public void VulkanCameraTex2DArray_StoresIndependentEyeSlices(float rightEyeGreen)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        ulong targetId = unchecked((ulong)(uint)target.GetInstanceID());

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(device.TryRecordCameraPass(targetId, 4, 4, new Rect(0, 0, 4, 4), Color.red,
            true, true, 1, false, false, isCameraTarget: false, depthSlice: 0));
        Assert.True(device.TryRecordCameraPass(targetId, 4, 4, new Rect(0, 0, 4, 4),
            new Color(0f, rightEyeGreen, 0f, 1f), true, true, 1, false, false,
            isCameraTarget: false, depthSlice: 1));

        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var left, 0));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var right, 1));
        Assert.Equal((byte)255, left[0]);
        Assert.Equal((byte)0, left[1]);
        Assert.Equal((byte)0, right[0]);
        int expectedGreen = (int)MathF.Round(rightEyeGreen * 255f);
        Assert.InRange(right[1], (byte)Math.Max(0, expectedGreen - 1), (byte)Math.Min(255, expectedGreen + 1));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.2f)]
    [InlineData(0.3f)]
    [InlineData(0.4f)]
    [InlineData(0.5f)]
    [InlineData(0.6f)]
    [InlineData(0.7f)]
    [InlineData(0.8f)]
    [InlineData(0.9f)]
    [InlineData(1.0f)]
    public void VulkanCameraMesh_RastersVertexColorIntoCameraTarget(float green)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        ulong targetId = unchecked((ulong)(uint)target.GetInstanceID());
        var leftColor = new Color(0f, green, 0.25f, 1f);
        var rightColor = new Color(0.25f, 0f, green, 1f);
        var leftPositions = new[]
        {
            new Vector3(-1f, -1f, 0.25f), new Vector3(3f, -1f, 0.25f), new Vector3(-1f, 3f, 0.25f)
        };
        var rightPositions = new[]
        {
            new Vector3(-1f, -1f, 0.75f), new Vector3(3f, -1f, 0.75f), new Vector3(-1f, 3f, 0.75f)
        };

        Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.True(device.TryRecordCameraPass(targetId, 4, 4, new Rect(0, 0, 4, 4), Color.black,
            true, true, 1, false, false, isCameraTarget: false, depthSliceCount: 2));
        Assert.True(device.TryDrawCameraMesh(target, leftPositions,
            new[] { Vector3.forward, Vector3.forward, Vector3.forward },
            new[] { leftColor, leftColor, leftColor }, new[] { 0, 1, 2 }, Matrix4x4.identity));
        Assert.True(device.TryDrawCameraMesh(target, rightPositions,
            new[] { Vector3.forward, Vector3.forward, Vector3.forward },
            new[] { rightColor, rightColor, rightColor }, new[] { 0, 1, 2 }, Matrix4x4.identity,
            depthSlice: 1));
        var opaque = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        Assert.True(device.EnsureCameraRenderTarget(opaque));
        Assert.True(device.TryCopyCameraRenderTargetColor(target, false, opaque, sourceSlice: 0, destinationSlice: 0));
        Assert.True(device.TryCopyCameraRenderTargetColor(target, false, opaque, sourceSlice: 1, destinationSlice: 1));
        var depth = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        Assert.True(device.EnsureCameraRenderTarget(depth));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(target, false, depth, sourceSlice: 0, destinationSlice: 0));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(target, false, depth, sourceSlice: 1, destinationSlice: 1));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var left, 0));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var right, 1));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(opaque, out var opaqueLeft, 0));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(opaque, out var opaqueRight, 1));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(depth, out var depthLeft, 0));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(depth, out var depthRight, 1));

        int center = ((2 * 4) + 2) * 4;
        Assert.Equal((byte)0, left[center]);
        int expectedGreen = (int)MathF.Round(green * 255f);
        Assert.InRange(left[center + 1], (byte)Math.Max(0, expectedGreen - 1), (byte)Math.Min(255, expectedGreen + 1));
        Assert.InRange(left[center + 2], (byte)62, (byte)65);
        Assert.Equal((byte)255, left[center + 3]);
        Assert.InRange(right[center], (byte)62, (byte)65);
        Assert.Equal((byte)0, right[center + 1]);
        Assert.InRange(right[center + 2], (byte)Math.Max(0, expectedGreen - 1), (byte)Math.Min(255, expectedGreen + 1));
        Assert.Equal((byte)255, right[center + 3]);
        Assert.Equal(left[center], opaqueLeft[center]);
        Assert.Equal(left[center + 1], opaqueLeft[center + 1]);
        Assert.Equal(right[center], opaqueRight[center]);
        Assert.Equal(right[center + 2], opaqueRight[center + 2]);
        Assert.InRange(depthLeft[center], (byte)62, (byte)66);
        Assert.InRange(depthRight[center], (byte)189, (byte)193);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.2f)]
    [InlineData(0.3f)]
    [InlineData(0.4f)]
    [InlineData(0.5f)]
    [InlineData(0.6f)]
    [InlineData(0.7f)]
    [InlineData(0.8f)]
    [InlineData(0.9f)]
    [InlineData(1.0f)]
    public void VulkanCameraMesh_BaseMapSamplesNativeTextureRegistry(float green)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = NewRenderTexture(4, 4);
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        texture.SetPixel(0, 0, new Color(.25f, green, .5f, 1f));
        texture.Apply(false, false);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)target.GetInstanceID()), 4, 4,
                new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-1f, -1f, .5f), new Vector3(3f, -1f, .5f), new Vector3(-1f, 3f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.white, Color.white, Color.white }, new[] { 0, 1, 2 }, Matrix4x4.identity,
                uvs: new[] { Vector2.zero, Vector2.zero, Vector2.zero }, baseTexture: texture));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

            int center = ((2 * 4) + 2) * 4;
            int expectedGreen = (int)MathF.Round(green * 255f);
            Assert.InRange(pixels[center], (byte)62, (byte)66);
            Assert.InRange(pixels[center + 1], (byte)Math.Max(0, expectedGreen - 1), (byte)Math.Min(255, expectedGreen + 1));
            Assert.InRange(pixels[center + 2], (byte)126, (byte)129);
            Assert.Equal((byte)255, pixels[center + 3]);
        }
        finally
        {
            device.ReleaseCameraRenderTarget(target);
            target.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Theory]
    [InlineData(1f, 0f, 255)]
    [InlineData(1f, .99f, 0)]
    [InlineData(1f, 1.2f, 255)]
    [InlineData(2f, 0f, 255)]
    [InlineData(2f, .5f, 0)]
    [InlineData(2f, 1f, 255)]
    [InlineData(.5f, 0f, 255)]
    [InlineData(.5f, .5f, 0)]
    [InlineData(.5f, .99f, 0)]
    [InlineData(3f, .99f, 0)]
    public void VulkanCameraMesh_BaseMapSTTransformsUvBeforeSampling(
        float scaleX, float offsetX, int expectedRed)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = NewRenderTexture(4, 4);
        var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = UnityEngine.TextureWrapMode.Repeat
        };
        texture.SetPixel(0, 0, Color.red);
        texture.SetPixel(1, 0, Color.black);
        texture.Apply(false, false);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)target.GetInstanceID()), 4, 4,
                new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-1f, -1f, .5f), new Vector3(3f, -1f, .5f), new Vector3(-1f, 3f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.white, Color.white, Color.white }, new[] { 0, 1, 2 }, Matrix4x4.identity,
                uvs: new[] { Vector2.zero, Vector2.zero, Vector2.zero }, baseTexture: texture,
                baseMapScale: new Vector2(scaleX, 1f), baseMapOffset: new Vector2(offsetX, 0f)));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

            int center = ((2 * 4) + 2) * 4;
            Assert.InRange(pixels[center], (byte)Math.Max(0, expectedRed - 12), (byte)Math.Min(255, expectedRed + 12));
        }
        finally
        {
            device.ReleaseCameraRenderTarget(target);
            target.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Theory]
    [InlineData(0, 1f, .5f, 245, 10)]
    [InlineData(1, 1f, .5f, 120, 120)]
    [InlineData(2, .5f, .5f, 120, 120)]
    [InlineData(3, 1f, .5f, 120, 245)]
    [InlineData(4, 1f, 1f, 0, 10)]
    public void VulkanCameraMesh_ExecutesUnityBlendModes(int blendMode,
        float sourceRed, float sourceAlpha, int expectedRed, int expectedBlue)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = NewRenderTexture(4, 4);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)target.GetInstanceID()), 4, 4,
                new Rect(0, 0, 4, 4), Color.blue, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-1f, -1f, .5f), new Vector3(3f, -1f, .5f), new Vector3(-1f, 3f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { new Color(sourceRed, 0f, 0f, sourceAlpha), new Color(sourceRed, 0f, 0f, sourceAlpha), new Color(sourceRed, 0f, 0f, sourceAlpha) },
                new[] { 0, 1, 2 }, Matrix4x4.identity, blendMode: blendMode, depthWriteEnabled: false));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

            int center = ((2 * 4) + 2) * 4;
            Assert.InRange(pixels[center], (byte)Math.Max(0, expectedRed - 12), (byte)Math.Min(255, expectedRed + 12));
            Assert.InRange(pixels[center + 2], (byte)Math.Max(0, expectedBlue - 12), (byte)Math.Min(255, expectedBlue + 12));
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(.49f, .5f, false)]
    [InlineData(.5f, .5f, true)]
    [InlineData(1f, 0f, true)]
    public void VulkanCameraMesh_AlphaClipUsesUnityCutoffBoundary(float alpha, float cutoff, bool visible)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = NewRenderTexture(4, 4);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)target.GetInstanceID()), 4, 4,
                new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-1f, -1f, .5f), new Vector3(3f, -1f, .5f), new Vector3(-1f, 3f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { new Color(1f, 0f, 0f, alpha), new Color(1f, 0f, 0f, alpha), new Color(1f, 0f, 0f, alpha) },
                new[] { 0, 1, 2 }, Matrix4x4.identity, alphaClipEnabled: true, alphaClipThreshold: cutoff));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

            Assert.Equal(visible, pixels[((2 * 4) + 2) * 4] > 220);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(true, 255, 0)]
    [InlineData(false, 0, 255)]
    public void VulkanCameraMesh_ZWriteControlsLaterDepthTest(bool firstDrawWritesDepth,
        int expectedRed, int expectedGreen)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var target = NewRenderTexture(4, 4);
        var near = new[] { new Vector3(-1f, -1f, .25f), new Vector3(3f, -1f, .25f), new Vector3(-1f, 3f, .25f) };
        var far = new[] { new Vector3(-1f, -1f, .75f), new Vector3(3f, -1f, .75f), new Vector3(-1f, 3f, .75f) };
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)target.GetInstanceID()), 4, 4,
                new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, near,
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.red, Color.red, Color.red }, new[] { 0, 1, 2 }, Matrix4x4.identity,
                depthWriteEnabled: firstDrawWritesDepth));
            Assert.True(device.TryDrawCameraMesh(target, far,
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.green, Color.green, Color.green }, new[] { 0, 1, 2 }, Matrix4x4.identity));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));

            int center = ((2 * 4) + 2) * 4;
            Assert.InRange(pixels[center], (byte)Math.Max(0, expectedRed - 1), (byte)Math.Min(255, expectedRed + 1));
            Assert.InRange(pixels[center + 1], (byte)Math.Max(0, expectedGreen - 1), (byte)Math.Min(255, expectedGreen + 1));
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(0f, 0f, 1f)]
    [InlineData(1f, 0f, 0f)]
    [InlineData(-1f, 0f, 0f)]
    [InlineData(0f, 1f, 0f)]
    [InlineData(0f, -1f, 0f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(-1f, 1f, 1f)]
    [InlineData(1f, -1f, 1f)]
    [InlineData(1f, 1f, -1f)]
    [InlineData(-1f, -1f, -1f)]
    public void VulkanCameraMesh_CopiesWorldNormalsToUrpTransient(float x, float y, float z)
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice();
        var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        var normal = new Vector3(x, y, z).normalized;
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source));
            Assert.True(device.EnsureCameraRenderTarget(destination));
            Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4,
                new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source,
                new[] { new Vector3(-1f, -1f, .5f), new Vector3(3f, -1f, .5f), new Vector3(-1f, 3f, .5f) },
                new[] { normal, normal, normal }, new[] { Color.white, Color.white, Color.white },
                new[] { 0, 1, 2 }, Matrix4x4.identity));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(source, false, destination));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = ((2 * 4) + 2) * 4;
            int Expected(float component) => (int)MathF.Round((component * .5f + .5f) * 255f);
            void AssertNormal(int channel, float component) => Assert.InRange(pixels[center + channel],
                (byte)Math.Max(0, Expected(component) - 2), (byte)Math.Min(255, Expected(component) + 2));
            AssertNormal(0, normal.x);
            AssertNormal(1, normal.y);
            AssertNormal(2, normal.z);
        }
        finally { device.ReleaseCameraRenderTarget(destination); device.ReleaseCameraRenderTarget(source); destination.Release(); source.Release(); }
    }

    [Fact]
    public void VulkanCameraDepthCopy_ConvertsSingleSampleClearToRed()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.black, true));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels)); Assert.InRange(pixels[0], (byte)254, byte.MaxValue);
    }

    [Fact]
    public void VulkanCameraDepthCopy_PreservesQuarterClearDepth()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 1, false, false, false, 0.25f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)62, (byte)66);
    }

    [Fact]
    public void VulkanCameraDepthCopy_UsesMsaa2SampleZero()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewMsaaRenderTexture(4, 4, 2); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 2, false, false, false, 0.5f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)126, (byte)129);
    }

    [Fact]
    public void VulkanCameraDepthCopy_UsesMsaa4SampleZero()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewMsaaRenderTexture(4, 4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 4, false, false, false, 0.75f));
        Assert.True(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.InRange(pixels[0], (byte)189, (byte)193);
    }

    [Fact]
    public void VulkanCameraDepthCopy_RejectsDifferentDimensions()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(2, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
    }

    [Fact]
    public void VulkanCameraDepthCopy_RejectsHdrDestination()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); var destination = NewHdrRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.black, true));
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
    }

    [Fact]
    public void VulkanCameraDepthCopy_RejectsSelfCopy()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var target = NewRenderTexture(4, 4); Assert.True(device.EnsureCameraRenderTarget(target));
        Assert.False(device.TryCopyCameraRenderTargetDepthToColor(target, false, target));
    }

    [Fact]
    public void VulkanCameraDepthCopy_RejectsUnregisteredSource()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(destination)); Assert.False(device.TryCopyCameraRenderTargetDepthToColor(source, false, destination));
    }

    [Fact]
    public void VulkanCameraOpaqueCopy_CopiesResolvedColorWithoutCpuReadback()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); var destination = NewRenderTexture(4, 4);
        Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
        Assert.True(RecordTarget(device, source, new Rect(0, 0, 4, 4), Color.magenta, true));
        Assert.True(device.TryCopyCameraRenderTargetColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
        Assert.Equal((byte)255, pixels[0]); Assert.Equal((byte)0, pixels[1]); Assert.Equal((byte)255, pixels[2]);
    }

    [Fact]
    public void VulkanCameraDepthTexturePass_PublishesAndCleansUp()
    {
        if (!HasVulkanCameraBackend()) return;
        using var device = NewVulkanDevice(); var source = NewRenderTexture(4, 4); Assert.True(device.EnsureCameraRenderTarget(source));
        Assert.True(device.TryRecordCameraPass(unchecked((ulong)(uint)source.GetInstanceID()), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, false, 1, false, false, false, 0.5f));
        var data = new RenderingData { cameraData = new CameraData { nativeTargetTexture = source, requiresDepthTexture = true, cameraTargetDescriptor = new RenderTextureDescriptor(4, 4, RenderTextureFormat.DefaultHDR, 24) } };
        Assert.Same(device, NativeGraphicsDevice.Current);
        var pass = new CameraDepthTexturePass(); pass.Execute(default, ref data);
        Assert.NotNull(data.cameraData.depthTexture); Assert.Same(data.cameraData.depthTexture, Shader.GetGlobalTexture(CameraDepthTexturePass.GlobalTextureName));
        Assert.True(device.TryReadbackCameraRenderTargetRGBA8(data.cameraData.depthTexture, out var pixels)); Assert.InRange(pixels[0], (byte)126, (byte)129);
        var depth = data.cameraData.depthTexture; pass.OnCameraCleanup(null); Assert.Null(Shader.GetGlobalTexture(CameraDepthTexturePass.GlobalTextureName)); Assert.False(depth.IsCreated());
    }

    private static NativeGraphicsDevice NewMetalDevice() =>
        NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);

    private static NativeGraphicsDevice NewVulkanDevice()
    {
        var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 8, 8, false);
        Assert.Equal(GraphicsDeviceType.Vulkan, device.DeviceType);
        Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static bool HasVulkanCameraBackend()
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 8, 8, false);
        bool available = device.DeviceType == GraphicsDeviceType.Vulkan &&
                         device.Handle != IntPtr.Zero &&
                         device.CreateSwapchain(8, 8, imageCount: 2, hdr: false) &&
                         device.SwapchainBackendKind == 1;
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_VULKAN") == "1")
            Assert.True(available);
        return available;
    }

    private static NativeGraphicsDevice NewMetalCameraTargetDevice(int samples)
    {
        var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false, msaa: samples);
        Assert.True(device.CreateSwapchain(8, 8, imageCount: 2));
        return device;
    }

    private static NativeGraphicsDevice NewMetalHdrCameraTargetDevice(int samples)
    {
        var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, true, msaa: samples);
        Assert.True(device.CreateSwapchain(8, 8, imageCount: 2, hdr: true));
        return device;
    }

    private static RenderTexture NewRenderTexture(int width, int height) =>
        new(width, height, 24, RenderTextureFormat.ARGB32);

    private static RenderTexture NewMsaaRenderTexture(int width, int height, int samples) =>
        new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            msaaSamples = samples
        };

    private static RenderTexture NewHdrRenderTexture(int width, int height, int samples = 1) =>
        new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
        {
            msaaSamples = samples
        };

    private static float[] BloomInput(Color highlight)
    {
        var input = new float[8 * 8 * 4];
        // Native camera passes use Unity's lower-left viewport origin; keep
        // this CPU fixture in the same orientation as the Metal target.
        for (int y = 6; y < 8; y++)
        for (int x = 0; x < 2; x++)
        {
            int offset = (y * 8 + x) * 4;
            input[offset] = highlight.r;
            input[offset + 1] = highlight.g;
            input[offset + 2] = highlight.b;
            input[offset + 3] = highlight.a;
        }
        return input;
    }

    private static float[] ProcessCpuLensDirt(float[] input,
        AnityNative.HDRColorGrade grade, byte[] dirt, int width, int height,
        FilterMode filterMode = FilterMode.Bilinear,
        UnityEngine.TextureWrapMode wrapMode = UnityEngine.TextureWrapMode.Clamp,
        bool linear = true)
    {
        var output = new float[input.Length];
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrameWithLensDirtRGBA8(input, 8, 8, ref grade,
                dirt, width, height, (int)filterMode,
                (int)wrapMode, (int)wrapMode,
                linear ? 1 : 0, dirt.Length, output, 0));
        return output;
    }

    private static float[] ProcessCpuLensDirtMips(float[] input,
        AnityNative.HDRColorGrade grade, byte[] dirt, int width, int height, int mipCount,
        FilterMode filterMode, UnityEngine.TextureWrapMode wrapMode = UnityEngine.TextureWrapMode.Clamp,
        bool linear = true, float mipBias = 0f)
    {
        var output = new float[input.Length];
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.HDR_ProcessFrameWithLensDirtRGBA8MipsBias(input, 8, 8, ref grade,
                dirt, width, height, mipCount, (int)filterMode,
                (int)wrapMode, (int)wrapMode, linear ? 1 : 0, mipBias, dirt.Length, output, 0));
        return output;
    }

    private static byte[] PackedDirtMips(int width, int height, int mipCount,
        byte r, byte g, byte b)
    {
        int byteCount = 0;
        int mipWidth = width;
        int mipHeight = height;
        for (int mip = 0; mip < mipCount; mip++)
        {
            byteCount += mipWidth * mipHeight * 4;
            mipWidth = Math.Max(1, mipWidth >> 1);
            mipHeight = Math.Max(1, mipHeight >> 1);
        }
        byte[] result = new byte[byteCount];
        int baseLevelBytes = width * height * 4;
        for (int offset = 0; offset < baseLevelBytes; offset += 4)
        {
            result[offset] = r;
            result[offset + 1] = g;
            result[offset + 2] = b;
            result[offset + 3] = 255;
        }
        return result;
    }

    private static Color[] FilledColors(int count, Color color)
    {
        var result = new Color[count];
        Array.Fill(result, color);
        return result;
    }

    private static AnityNative.HDRColorGrade AcesGrade() => new()
    {
        colorFilterR = 1f,
        colorFilterG = 1f,
        colorFilterB = 1f,
        mixerRedR = 1f,
        mixerGreenG = 1f,
        mixerBlueB = 1f,
        curveLut = PostProcessRuntime.CreateIdentityCurveLut(),
        bloomThreshold = 0.9f,
        bloomScatter = 1f,
        bloomMaxIterations = 2,
        bloomDownscale = 0,
        bloomHighQualityFiltering = 0,
        bloomTintR = 1f,
        bloomTintG = 1f,
        bloomTintB = 1f,
        tonemapMode = 2
    };

    private static AnityNative.HDRColorGrade CurveGrade(int curveIndex, float scale)
    {
        var grade = AcesGrade();
        grade.curveEnabled = 1;
        int offset = curveIndex * PostProcessRuntime.ColorCurveSamples;
        for (int index = 0; index < PostProcessRuntime.ColorCurveSamples; index++)
        {
            float x = index / (float)(PostProcessRuntime.ColorCurveSamples - 1);
            grade.curveLut[offset + index] = x * scale;
        }
        return grade;
    }

    private static bool RecordTarget(NativeGraphicsDevice device, RenderTexture target,
        Rect viewport, Color color, bool clearColor, bool hdr = false, int msaaSamples = 1) =>
        device.TryRecordCameraPass(unchecked((ulong)(uint)target.GetInstanceID()),
            target.width, target.height, viewport, color, true, clearColor,
            msaaSamples, false, hdr, isCameraTarget: false);

    private static NativeGraphicsDevice NewDevice() => NativeGraphicsDevice.Create(GraphicsDeviceType.Null, 64, 64, false);

    private static bool Record(NativeGraphicsDevice device) => device.TryRecordCameraPass(2, 64, 64, new Rect(0, 0, 64, 64), Color.black, true, true, 1, false, false);
}
