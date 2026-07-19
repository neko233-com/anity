using System;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeMetalUITextureTests
{
    private const int Width = 64;
    private const int Height = 64;
    private static readonly uint[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

    [Fact]
    public void SolidMainTextureColorsWhiteGeometry()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, texture), Quad(Color.white), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void VertexColorMultipliesSampledTexture()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[] { new Color32(0, 255, 0, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, texture),
                Quad(new Color32(128, 255, 255, 255)), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void MainTextureAlphaParticipatesInSourceAlphaBlend()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[] { new Color32(255, 0, 0, 128) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, texture), Quad(Color.white), QuadIndices));
            var pixel = Pixel(SubmitAndRead(device, canvas), 32, 32);
            Assert.InRange(pixel.r, (byte)127, (byte)129);
            Assert.Equal(0, pixel.g);
            Assert.InRange(pixel.a, (byte)127, (byte)129);
        });

    [Fact]
    public void SeparateAlphaTextureRedChannelModulatesCoverage()
        => WithMetal((device, canvas) =>
        {
            Texture2D main = Texture(new[] { new Color32(0, 0, 255, 255) }, 1, 1);
            Texture2D alpha = Texture(new[] { new Color32(64, 255, 255, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, main, alpha), Quad(Color.white), QuadIndices));
            var pixel = Pixel(SubmitAndRead(device, canvas), 32, 32);
            Assert.InRange(pixel.b, (byte)63, (byte)65);
            Assert.InRange(pixel.a, (byte)63, (byte)65);
        });

    [Fact]
    public void AlphaTextureWorksWithImplicitWhiteMainTexture()
        => WithMetal((device, canvas) =>
        {
            Texture2D alpha = Texture(new[] { new Color32(128, 0, 0, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, null, alpha), Quad(Color.white), QuadIndices));
            var pixel = Pixel(SubmitAndRead(device, canvas), 32, 32);
            Assert.InRange(pixel.r, (byte)127, (byte)129);
            Assert.InRange(pixel.g, (byte)127, (byte)129);
            Assert.InRange(pixel.b, (byte)127, (byte)129);
            Assert.InRange(pixel.a, (byte)127, (byte)129);
        });

    [Fact]
    public void ApplyReplacesGpuTextureWithoutChangingCommandId()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, texture), Quad(Color.white), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 0, 255);
            texture.SetPixel(0, 0, Color.green);
            texture.Apply(false, false);
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void MetalTextureExposesBackendNativeHandle()
        => WithMetal((device, _) =>
        {
            Texture2D texture = Texture(new[] { new Color32(1, 2, 3, 4) }, 1, 1);
            Assert.NotEqual(IntPtr.Zero, texture.GetNativeTexturePtr());
            Assert.True(device.TryGetTextureInfo(texture, out var info));
            Assert.Equal(2, info.backendKind);
        });

    [Fact]
    public void PointSamplerSelectsNearestTexel()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255)
            }, 2, 1);
            texture.filterMode = FilterMode.Point;
            texture.anisoLevel = 16;
            Assert.True(device.EnsureTexture(texture));
            Assert.True(canvas.Upsert(Desc(1, texture), Quad(Color.white), QuadIndices));
            byte[] pixels = SubmitAndRead(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 0, 255, 255);
        });

    [Fact]
    public void RepeatSamplerHandlesUvBeyondOne()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255)
            }, 2, 1);
            texture.filterMode = FilterMode.Point;
            texture.wrapModeU = TextureWrapMode.Repeat;
            Assert.True(device.EnsureTexture(texture));
            Assert.True(canvas.Upsert(Desc(1, texture), Quad(Color.white, 1f, 2f), QuadIndices));
            byte[] pixels = SubmitAndRead(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void MirrorOnceSamplerReflectsNegativeUvInsteadOfClamping()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255)
            }, 2, 1);
            texture.filterMode = FilterMode.Point;
            texture.wrapModeU = TextureWrapMode.Clamp;
            Assert.True(device.EnsureTexture(texture));
            Assert.True(canvas.Upsert(Desc(1, texture),
                Quad(Color.white, -0.75f, -0.75f), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 0, 255);

            texture.wrapModeU = TextureWrapMode.MirrorOnce;
            Assert.True(device.EnsureTexture(texture));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void DifferentTextureIdsBindPerDrawPacket()
        => WithMetal((device, canvas) =>
        {
            Texture2D red = Texture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Texture2D blue = Texture(new[] { new Color32(0, 0, 255, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, red),
                Quad(Color.white, 0, 1, 4, 8, 28, 56), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, blue),
                Quad(Color.white, 0, 1, 36, 8, 60, 56), QuadIndices));
            byte[] pixels = SubmitAndRead(device, canvas);
            AssertPixel(pixels, 16, 32, 255, 0, 0, 255);
            AssertPixel(pixels, 48, 32, 0, 0, 255, 255);
            Assert.Equal(2, device.LastUIUploadStats.drawCount);
        });

    [Fact]
    public void DestroyedTextureFallsBackToWhiteWithoutStaleGpuUse()
        => WithMetal((device, canvas) =>
        {
            Texture2D texture = Texture(new[] { new Color32(255, 0, 0, 255) }, 1, 1);
            Assert.True(canvas.Upsert(Desc(1, texture), Quad(Color.white), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 0, 255);
            UnityEngine.Object.DestroyImmediate(texture);
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 255, 255, 255);
        });

    [Fact]
    public void MinifiedTextureSamplesUploadedNonzeroMipLevel()
        => WithMetal((device, canvas) =>
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
            Assert.True(canvas.Upsert(Desc(1, texture),
                Quad(Color.white, 0, 1, 28, 28, 36, 36), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 255, 0, 255);
        });

    [Fact]
    public void MipMapBiasSelectsTheNextCoarserUploadedMipLevel()
        => WithMetal((device, canvas) =>
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, true, true)
            {
                filterMode = FilterMode.Point,
                mipMapBias = 1.0f
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
            Assert.True(canvas.Upsert(Desc(1, texture),
                Quad(Color.white, 0, 1, 28, 28, 36, 36), QuadIndices));
            AssertPixel(SubmitAndRead(device, canvas), 32, 32, 255, 0, 255, 255);
        });

    private static AnityNative.UIRenderCommandDesc Desc(
        ulong renderer, Texture2D? main = null, Texture2D? alpha = null)
        => new()
        {
            rendererId = renderer,
            materialId = renderer,
            textureId = Id(main),
            alphaTextureId = Id(alpha),
            sortDepth = checked((int)renderer),
            flags = AnityNative.UIRenderCommandFlags.Visible,
            clipXMax = Width,
            clipYMax = Height,
            effectiveAlpha = 1
        };

    private static AnityNative.UIPackedVertex[] Quad(
        Color color, float uMin = 0, float uMax = 1,
        float xMin = 8, float yMin = 8, float xMax = 56, float yMax = 56)
        => Quad((Color32)color, uMin, uMax, xMin, yMin, xMax, yMax);

    private static AnityNative.UIPackedVertex[] Quad(
        Color32 color, float uMin = 0, float uMax = 1,
        float xMin = 8, float yMin = 8, float xMax = 56, float yMax = 56)
        => new[]
        {
            Vertex(xMin, yMin, uMin, 0, color),
            Vertex(xMax, yMin, uMax, 0, color),
            Vertex(xMax, yMax, uMax, 1, color),
            Vertex(xMin, yMax, uMin, 1, color)
        };

    private static AnityNative.UIPackedVertex Vertex(
        float x, float y, float u, float v, Color32 color)
        => new()
        {
            position = new AnityNative.UIVector3(x, y, 0),
            color = new AnityNative.UIColor32(color.r, color.g, color.b, color.a),
            uv0 = new AnityNative.UIVector4(u, v, 0, 0)
        };

    private static Texture2D Texture(Color32[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static byte[] SubmitAndRead(NativeGraphicsDevice device, NativeUICanvas canvas)
    {
        Assert.True(device.SubmitUICanvas(canvas));
        Assert.True(device.TryReadbackSwapchainRGBA8(out byte[] pixels));
        return pixels;
    }

    private static void AssertPixel(
        byte[] pixels, int x, int y, byte r, byte g, byte b, byte a)
        => Assert.Equal((r, g, b, a), Pixel(pixels, x, y));

    private static (byte r, byte g, byte b, byte a) Pixel(byte[] pixels, int x, int y)
    {
        int offset = (y * Width + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }

    private static ulong Id(UnityEngine.Object? value)
        => value is null ? 0UL : unchecked((ulong)(uint)value.GetInstanceID());

    private static void WithMetal(Action<NativeGraphicsDevice, NativeUICanvas> action)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, Width, Height, false);
        using NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        bool resolved = device.Handle != IntPtr.Zero && canvas is not null;
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.True(resolved);
        if (!resolved) return;
        Assert.True(device.CreateSwapchain(Width, Height, imageCount: 3, hdr: false));
        action(device, canvas!);
    }
}
