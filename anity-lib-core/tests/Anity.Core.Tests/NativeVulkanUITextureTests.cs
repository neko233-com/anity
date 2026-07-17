using System;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVulkanUITextureTests
{
    private const int Size = 64;
    private static readonly uint[] Indices = { 0, 1, 2, 2, 3, 0 };

    [Fact]
    public void SolidTextureColorsWhiteGeometry()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Put(canvas, 1, texture, null, Quad(Color.white));
            AssertPixel(Read(device, canvas), 32, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void VertexColorMultipliesTextureSample()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[] { new Color32(0, 255, 0, 255) }, 1, 1);
            Put(canvas, 1, texture, null, Quad(new Color32(128, 255, 255, 255)));
            AssertPixel(Read(device, canvas), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void MainTextureAlphaBlendsAgainstTransparentTarget()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[] { new Color32(255, 0, 0, 128) }, 1, 1);
            Put(canvas, 1, texture, null, Quad(Color.white));
            var pixel = Pixel(Read(device, canvas), 32, 32);
            Assert.InRange(pixel.r, (byte)127, (byte)129);
            Assert.InRange(pixel.a, (byte)127, (byte)129);
        });

    [Fact]
    public void AlphaTextureRedChannelModulatesCoverage()
        => WithVulkan((device, canvas) =>
        {
            Texture2D main = MakeTexture(new[] { new Color32(0, 0, 255, 255) }, 1, 1);
            Texture2D alpha = MakeTexture(new[] { new Color32(64, 255, 255, 255) }, 1, 1);
            Put(canvas, 1, main, alpha, Quad(Color.white));
            var pixel = Pixel(Read(device, canvas), 32, 32);
            Assert.InRange(pixel.b, (byte)63, (byte)65);
            Assert.InRange(pixel.a, (byte)63, (byte)65);
        });

    [Fact]
    public void ApplyReplacesSampledImageAndDescriptor()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Put(canvas, 1, texture, null, Quad(Color.white));
            AssertPixel(Read(device, canvas), 32, 32, 255, 0, 0, 255);
            texture.SetPixel(0, 0, Color.green);
            texture.Apply(false, false);
            AssertPixel(Read(device, canvas), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void VulkanTextureExposesVkImageNativeHandle()
        => WithVulkan((device, _) =>
        {
            Texture2D texture = MakeTexture(new[] { new Color32(1, 2, 3, 4) }, 1, 1);
            Assert.NotEqual(IntPtr.Zero, texture.GetNativeTexturePtr());
            Assert.True(device.TryGetTextureInfo(texture, out var info));
            Assert.Equal(1, info.backendKind);
        });

    [Fact]
    public void PointSamplerSelectsNearestTexel()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255)
            }, 2, 1);
            texture.filterMode = FilterMode.Point;
            Assert.True(device.EnsureTexture(texture));
            Put(canvas, 1, texture, null, Quad(Color.white));
            byte[] pixels = Read(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 0, 255, 255);
        });

    [Fact]
    public void RepeatSamplerWrapsUvBeyondOne()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255)
            }, 2, 1);
            texture.filterMode = FilterMode.Point;
            texture.wrapModeU = TextureWrapMode.Repeat;
            Assert.True(device.EnsureTexture(texture));
            Put(canvas, 1, texture, null, Quad(Color.white, 1, 2));
            byte[] pixels = Read(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void DifferentTextureIdsBindSeparateDescriptorSets()
        => WithVulkan((device, canvas) =>
        {
            Texture2D red = MakeTexture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Texture2D blue = MakeTexture(new[] { new Color32(0, 0, 255, 255) }, 1, 1);
            Put(canvas, 1, red, null, Quad(Color.white, 0, 1, 4, 8, 28, 56));
            Put(canvas, 2, blue, null, Quad(Color.white, 0, 1, 36, 8, 60, 56));
            byte[] pixels = Read(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 0, 255, 255);
            Assert.Equal(2, device.LastUIUploadStats.drawCount);
        });

    [Fact]
    public void DestroyedTextureCannotLeaveStaleDescriptorUse()
        => WithVulkan((device, canvas) =>
        {
            Texture2D texture = MakeTexture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Put(canvas, 1, texture, null, Quad(Color.white));
            AssertPixel(Read(device, canvas), 32, 32, 255, 0, 0, 255);
            UnityEngine.Object.DestroyImmediate(texture);
            AssertPixel(Read(device, canvas), 32, 32, 255, 255, 255, 255);
        });

    [Fact]
    public void MinifiedTextureSamplesUploadedNonzeroMipLevel()
        => WithVulkan((device, canvas) =>
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, true, true)
            {
                filterMode = FilterMode.Point
            };
            Color32[] colors =
            {
                new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 0, 255, 255),
                new(255, 255, 0, 255), new(255, 0, 255, 255), new(0, 255, 255, 255),
                new(255, 255, 255, 255)
            };
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                int side = Math.Max(1, 64 >> mip);
                var level = new Color32[side * side];
                Array.Fill(level, colors[mip]);
                texture.SetPixels32(level, mip);
            }
            texture.Apply(false, false);
            Put(canvas, 1, texture, null, Quad(Color.white, 0, 1, 28, 28, 36, 36));
            AssertPixel(Read(device, canvas), 32, 32, 255, 255, 0, 255);
        });

    private static void Put(NativeUICanvas canvas, ulong id, Texture2D? main,
        Texture2D? alpha, AnityNative.UIPackedVertex[] vertices)
    {
        var desc = new AnityNative.UIRenderCommandDesc
        {
            rendererId = id,
            materialId = id,
            textureId = Id(main),
            alphaTextureId = Id(alpha),
            sortDepth = checked((int)id),
            flags = AnityNative.UIRenderCommandFlags.Visible,
            clipXMax = Size,
            clipYMax = Size,
            effectiveAlpha = 1
        };
        Assert.True(canvas.Upsert(desc, vertices, Indices));
    }

    private static AnityNative.UIPackedVertex[] Quad(Color color, float u0 = 0, float u1 = 1,
        float x0 = 8, float y0 = 8, float x1 = 56, float y1 = 56)
        => Quad((Color32)color, u0, u1, x0, y0, x1, y1);

    private static AnityNative.UIPackedVertex[] Quad(Color32 color, float u0 = 0, float u1 = 1,
        float x0 = 8, float y0 = 8, float x1 = 56, float y1 = 56)
        => new[]
        {
            Vertex(x0, y0, u0, 0, color), Vertex(x1, y0, u1, 0, color),
            Vertex(x1, y1, u1, 1, color), Vertex(x0, y1, u0, 1, color)
        };

    private static AnityNative.UIPackedVertex Vertex(
        float x, float y, float u, float v, Color32 color)
        => new()
        {
            position = new AnityNative.UIVector3(x, y, 0),
            color = new AnityNative.UIColor32(color.r, color.g, color.b, color.a),
            uv0 = new AnityNative.UIVector4(u, v, 0, 0)
        };

    private static Texture2D MakeTexture(Color32[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static byte[] Read(NativeGraphicsDevice device, NativeUICanvas canvas)
    {
        Assert.True(device.SubmitUICanvas(canvas));
        Assert.True(device.TryReadbackSwapchainRGBA8(out byte[] pixels));
        return pixels;
    }

    private static void AssertPixel(byte[] pixels, int x, int y,
        byte r, byte g, byte b, byte a)
        => Assert.Equal((r, g, b, a), Pixel(pixels, x, y));

    private static (byte r, byte g, byte b, byte a) Pixel(byte[] pixels, int x, int y)
    {
        int offset = (y * Size + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }

    private static ulong Id(UnityEngine.Object? value)
        => value is null ? 0UL : unchecked((ulong)(uint)value.GetInstanceID());

    private static void WithVulkan(Action<NativeGraphicsDevice, NativeUICanvas> action)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Vulkan, Size, Size, false);
        using NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        bool resolved = device.Handle != IntPtr.Zero &&
            device.DeviceType == GraphicsDeviceType.Vulkan && canvas is not null &&
            device.CreateSwapchain(Size, Size, imageCount: 3, hdr: false) &&
            device.SwapchainBackendKind == 1;
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_VULKAN") == "1")
            Assert.True(resolved);
        if (!resolved) return;
        action(device, canvas!);
    }
}
