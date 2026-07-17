using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class Texture2DMipmapTests
{
    [Fact]
    public void MipmapConstructorAllocatesCompleteChain()
    {
        var texture = new Texture2D(8, 4, TextureFormat.RGBA32, true, true);
        Assert.Equal(4, texture.mipmapCount);
        Assert.Equal(32, texture.GetPixels32(0).Length);
        Assert.Equal(8, texture.GetPixels32(1).Length);
        Assert.Equal(2, texture.GetPixels32(2).Length);
        Assert.Single(texture.GetPixels32(3));
    }

    [Fact]
    public void ExplicitMipCountIsClampedToPhysicalChain()
    {
        var texture = new Texture2D(4, 2, TextureFormat.RGBA32, 99, true);
        Assert.Equal(3, texture.mipmapCount);
    }

    [Fact]
    public void SetPixels32TargetsOnlyRequestedMip()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
        texture.SetPixels32(Fill(4, new Color32(255, 0, 0, 255)), 1);
        Assert.Equal(new Color32(255, 0, 0, 255), texture.GetPixels32(1)[0]);
        Assert.Equal(default, texture.GetPixels32(0)[0]);
        Assert.Equal(default, texture.GetPixels32(2)[0]);
    }

    [Fact]
    public void SetPixelAndGetPixelAddressRequestedMipDimensions()
    {
        var texture = new Texture2D(8, 4, TextureFormat.RGBA32, true, true);
        texture.SetPixel(2, 1, Color.green, 1);
        Assert.Equal((Color32)Color.green, (Color32)texture.GetPixel(2, 1, 1));
        Assert.Equal((Color32)Color.clear, (Color32)texture.GetPixel(4, 1, 1));
    }

    [Fact]
    public void BilinearSamplingReadsRequestedMip()
    {
        var texture = new Texture2D(4, 2, TextureFormat.RGBA32, true, true);
        texture.SetPixels32(new[]
        {
            new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255)
        }, 1);
        Color sample = texture.GetPixelBilinear(0.5f, 0.5f, 1);
        Assert.InRange(sample.r, 0.49f, 0.51f);
        Assert.InRange(sample.b, 0.49f, 0.51f);
    }

    [Fact]
    public void ApplyWithUpdateMipmapsBuildsBoxFilteredLevel()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
        texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
        texture.Apply(true, false);
        Color mip = texture.GetPixel(0, 0, 1);
        Assert.InRange(mip.r, 0.49f, 0.51f);
        Assert.InRange(mip.g, 0.49f, 0.51f);
        Assert.InRange(mip.b, 0.49f, 0.51f);
        Assert.Equal(1f, mip.a);
    }

    [Fact]
    public void ApplyWithoutUpdateMipmapsPreservesManualLevels()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
        var manual = new Color32(12, 34, 56, 78);
        texture.SetPixels32(Fill(4, manual), 1);
        texture.SetPixels32(Fill(16, new Color32(255, 0, 0, 255)), 0);
        texture.Apply(false, false);
        Assert.Equal(manual, texture.GetPixels32(1)[0]);
    }

    [Fact]
    public void GeneratedChainRecursivelyReachesOneByOne()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
        Color[] pixels = new Color[16];
        pixels[0] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        Color finalMip = texture.GetPixel(0, 0, 2);
        Assert.InRange(finalMip.r, 0.0624f, 0.0626f);
        Assert.InRange(finalMip.a, 0.0624f, 0.0626f);
    }

    [Fact]
    public void RawTextureDataPacksLargestMipFirst()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
        texture.SetPixels32(Fill(16, new Color32(1, 2, 3, 4)), 0);
        texture.SetPixels32(Fill(4, new Color32(5, 6, 7, 8)), 1);
        texture.SetPixels32(new[] { new Color32(9, 10, 11, 12) }, 2);
        byte[] raw = texture.GetRawTextureData();
        Assert.Equal(84, raw.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, raw[..4]);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, raw[64..68]);
        Assert.Equal(new byte[] { 9, 10, 11, 12 }, raw[80..84]);
    }

    [Fact]
    public void LoadRawTextureDataPopulatesEveryMip()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
        byte[] raw = new byte[20];
        for (int index = 0; index < 16; index += 4)
        {
            raw[index] = 10; raw[index + 1] = 20; raw[index + 2] = 30; raw[index + 3] = 40;
        }
        raw[16] = 50; raw[17] = 60; raw[18] = 70; raw[19] = 80;
        texture.LoadRawTextureData(raw);
        Assert.Equal(new Color32(10, 20, 30, 40), texture.GetPixels32(0)[0]);
        Assert.Equal(new Color32(50, 60, 70, 80), texture.GetPixels32(1)[0]);
    }

    [Fact]
    public void NativeRegistryStoresPackedMipByteCount()
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 16, 16, false);
        if (device.Handle == System.IntPtr.Zero) return;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
        texture.Apply(true, false);
        Assert.True(device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(3, info.desc.mipCount);
        Assert.Equal(84, info.byteCount);
    }

    [Fact]
    public void NativeAbiRejectsBaseOnlyBytesForMipmappedDescriptor()
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 16, 16, false);
        if (device.Handle == System.IntPtr.Zero) return;
        var desc = new AnityNative.GraphicsTextureDesc
        {
            textureId = 9901,
            revision = 1,
            width = 4,
            height = 4,
            mipCount = 3,
            filterMode = 1,
            wrapU = 0,
            wrapV = 0,
            linear = 1
        };
        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.Graphics_UploadTextureRGBA8(device.Handle, ref desc, new byte[64], 64));
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.Graphics_UploadTextureRGBA8(device.Handle, ref desc, new byte[84], 84));
    }

    [Fact]
    public void NonPowerOfTwoChainUsesUnityFloorHalving()
    {
        var texture = new Texture2D(5, 3, TextureFormat.RGBA32, true, true);
        Assert.Equal(3, texture.mipmapCount);
        Assert.Equal(15, texture.GetPixels32(0).Length);
        Assert.Equal(2, texture.GetPixels32(1).Length);
        Assert.Single(texture.GetPixels32(2));
        Assert.Equal(72, texture.GetRawTextureData().Length);
    }

    [Fact]
    public void NonReadableApplyUploadsCompleteChainBeforeCpuGateCloses()
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 16, 16, false);
        if (device.Handle == System.IntPtr.Zero) return;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
        texture.SetPixels32(Fill(16, new Color32(25, 50, 75, 255)));
        texture.Apply(true, true);
        Assert.False(texture.isReadable);
        Assert.True(device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(84, info.byteCount);
    }

    private static Color32[] Fill(int count, Color32 value)
    {
        var result = new Color32[count];
        for (int index = 0; index < result.Length; index++) result[index] = value;
        return result;
    }
}
