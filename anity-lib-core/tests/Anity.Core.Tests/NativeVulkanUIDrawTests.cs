using System;
using Anity.Core.Runtime.Native;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVulkanUIDrawTests
{
    private const int Width = 64;
    private const int Height = 64;
    private static readonly uint[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

    [Fact]
    public void RedQuadRasterizesCenterPixel()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(8, 8, 56, 56, 255, 0, 0, 255), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 0, 255);
            Assert.Equal(1, device.LastUIUploadStats.drawCount);
        });

    [Fact]
    public void PixelsOutsideGeometryRemainTransparent()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(16, 16, 48, 48, 255, 0, 0, 255), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 2, 2, 0, 0, 0, 0);
        });

    [Fact]
    public void VertexBlueColorSurvivesGpuPipeline()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(8, 8, 56, 56, 0, 0, 255, 255), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 0, 0, 255, 255);
        });

    [Fact]
    public void SourceAlphaBlendsAgainstTransparentTarget()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(8, 8, 56, 56, 255, 0, 0, 128), QuadIndices));
            (byte r, byte g, byte b, byte a) = Pixel(SubmitAndRead(device, canvas), 32, 32);
            Assert.InRange(r, (byte)127, (byte)129);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.InRange(a, (byte)127, (byte)129);
        });

    [Fact]
    public void RectClipUsesDynamicScissor()
        => WithVulkan((device, canvas) =>
        {
            var desc = Desc(1, flags: AnityNative.UIRenderCommandFlags.Visible |
                AnityNative.UIRenderCommandFlags.RectClip);
            desc.clipXMin = 32;
            desc.clipYMin = 0;
            desc.clipXMax = 64;
            desc.clipYMax = 64;
            Assert.True(canvas.Upsert(desc, Quad(8, 8, 56, 56, 255, 0, 0, 255), QuadIndices));
            byte[] pixels = SubmitAndRead(device, canvas);
            AssertPixel(pixels, 16, 32, 0, 0, 0, 0);
            AssertPixel(pixels, 48, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void EmptySubmissionClearsPreviousFramebuffer()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(8, 8, 56, 56, 255, 0, 0, 255), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 0, 255);
            Assert.True(canvas.Clear());
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 0, 0, 0, 0);
            Assert.Equal(0, device.LastUIUploadStats.drawCount);
        });

    [Fact]
    public void DifferentMaterialsEncodeSeparateIndexedDraws()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1, material: 10),
                Quad(4, 8, 28, 56, 255, 0, 0, 255), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, material: 20),
                Quad(36, 8, 60, 56, 0, 255, 0, 255), QuadIndices));
            byte[] pixels = SubmitAndRead(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 255, 0, 255);
            Assert.Equal(2, device.LastUIUploadStats.drawCount);
        });

    [Fact]
    public void SixFramesReuseTripleRingOnlyAfterFenceCompletion()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(8, 8, 56, 56, 255, 0, 0, 255), QuadIndices));
            Assert.True(device.AttachUICanvas(canvas));
            for (int frame = 1; frame <= 6; frame++)
            {
                device.BeginFrame();
                device.EndFrame();
                AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
                Assert.Equal(frame % 3, device.LastUIUploadStats.ringIndex);
            }
        });

    [Fact]
    public void ReadbackIsTightlyPackedTopToBottomRgba8()
        => WithVulkan((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(4, 4, 24, 24, 0, 255, 0, 255), QuadIndices));
            byte[] pixels = SubmitAndRead(device, canvas);
            Assert.Equal(Width * Height * 4, pixels.Length);
            AssertPixel(pixels, 10, 10, 0, 255, 0, 255);
            AssertPixel(pixels, 10, 50, 0, 0, 0, 0);
        });

    [Fact]
    public void UnsupportedMaskPacketIsNotMisreportedAsColorDraw()
        => WithVulkan((device, canvas) =>
        {
            var flags = AnityNative.UIRenderCommandFlags.Visible |
                AnityNative.UIRenderCommandFlags.Mask;
            Assert.True(canvas.Upsert(Desc(1, flags: flags),
                Quad(8, 8, 56, 56, 255, 0, 0, 255), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 0, 0, 0, 0);
            Assert.Equal(0, device.LastUIUploadStats.drawCount);
        });

    private static byte[] SubmitAndRead(NativeGraphicsDevice device, NativeUICanvas canvas)
    {
        Assert.True(device.SubmitUICanvas(canvas));
        return Read(device);
    }

    private static byte[] Read(NativeGraphicsDevice device)
    {
        Assert.True(device.TryReadbackSwapchainRGBA8(out byte[] pixels));
        return pixels;
    }

    private static AnityNative.UIRenderCommandDesc Desc(
        ulong renderer, ulong material = 1,
        AnityNative.UIRenderCommandFlags flags = AnityNative.UIRenderCommandFlags.Visible)
        => new()
        {
            rendererId = renderer,
            materialId = material,
            sortDepth = checked((int)renderer),
            flags = flags,
            clipXMax = Width,
            clipYMax = Height,
            effectiveAlpha = 1
        };

    private static AnityNative.UIPackedVertex[] Quad(
        float xMin, float yMin, float xMax, float yMax,
        byte r, byte g, byte b, byte a)
    {
        var color = new AnityNative.UIColor32(r, g, b, a);
        return new[]
        {
            Vertex(xMin, yMin, color), Vertex(xMax, yMin, color),
            Vertex(xMax, yMax, color), Vertex(xMin, yMax, color)
        };
    }

    private static AnityNative.UIPackedVertex Vertex(
        float x, float y, AnityNative.UIColor32 color)
        => new() { position = new AnityNative.UIVector3(x, y, 0), color = color };

    private static void AssertPixel(
        byte[] pixels, int x, int y, byte r, byte g, byte b, byte a)
        => Assert.Equal((r, g, b, a), Pixel(pixels, x, y));

    private static (byte r, byte g, byte b, byte a) Pixel(byte[] pixels, int x, int y)
    {
        int offset = (y * Width + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }

    private static void WithVulkan(Action<NativeGraphicsDevice, NativeUICanvas> action)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Vulkan, Width, Height, false);
        using NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        bool resolved = device.Handle != IntPtr.Zero &&
            device.DeviceType == GraphicsDeviceType.Vulkan && canvas is not null &&
            device.CreateSwapchain(Width, Height, imageCount: 3, hdr: false) &&
            device.SwapchainBackendKind == 1;
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_VULKAN") == "1")
            Assert.True(resolved);
        if (!resolved) return;
        action(device, canvas!);
    }
}
