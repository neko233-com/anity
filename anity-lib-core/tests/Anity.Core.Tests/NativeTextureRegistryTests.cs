using System;
using System.Threading.Tasks;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeTextureRegistryTests : IDisposable
{
    private readonly NativeGraphicsDevice _device;

    public NativeTextureRegistryTests()
    {
        _device = NativeGraphicsDevice.Create(GraphicsDeviceType.Null, 32, 32, false);
    }

    [Fact]
    public void ApplyCreatesDeviceOwnedTextureEntry()
    {
        if (!Ready) return;
        Texture2D texture = Solid(2, 3, new Color32(1, 2, 3, 4));
        Assert.True(_device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(Id(texture), info.desc.textureId);
        Assert.Equal(24, info.byteCount);
    }

    [Fact]
    public void UploadPreservesDimensionsMipAndSamplingState()
    {
        if (!Ready) return;
        var texture = new Texture2D(4, 2, TextureFormat.RGBA32, true, true)
        {
            filterMode = FilterMode.Point,
            wrapModeU = TextureWrapMode.Clamp,
            wrapModeV = TextureWrapMode.Mirror
        };
        texture.Apply();
        Assert.True(_device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(4, info.desc.width);
        Assert.Equal(2, info.desc.height);
        Assert.Equal(Texture.GenerateMipsCount(4, 2), info.desc.mipCount);
        Assert.Equal((int)FilterMode.Point, info.desc.filterMode);
        Assert.Equal((int)TextureWrapMode.Clamp, info.desc.wrapU);
        Assert.Equal((int)TextureWrapMode.Mirror, info.desc.wrapV);
        Assert.Equal(1, info.desc.linear);
    }

    [Fact]
    public void ApplyRevisionIsExposedInNativeDescriptor()
    {
        if (!Ready) return;
        var texture = new Texture2D(1, 1);
        texture.Apply();
        Assert.True(_device.TryGetTextureInfo(texture, out var first));
        texture.Apply();
        Assert.True(_device.TryGetTextureInfo(texture, out var second));
        Assert.Equal(first.desc.revision + 1, second.desc.revision);
        Assert.True(second.uploadGeneration > first.uploadGeneration);
    }

    [Fact]
    public void UnchangedTextureDoesNotUploadAgain()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, new Color32(10, 20, 30, 40));
        Assert.True(_device.TryGetTextureInfo(texture, out var first));
        Assert.True(_device.EnsureTexture(texture));
        Assert.True(_device.TryGetTextureInfo(texture, out var second));
        Assert.Equal(first.uploadGeneration, second.uploadGeneration);
    }

    [Fact]
    public void PixelMutationRequiresApplyBeforeNativeReplacement()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, new Color32(10, 20, 30, 40));
        Assert.True(_device.TryGetTextureInfo(texture, out var first));
        texture.SetPixel(0, 0, Color.red);
        Assert.True(_device.EnsureTexture(texture));
        Assert.True(_device.TryGetTextureInfo(texture, out var unchanged));
        Assert.Equal(first.uploadGeneration, unchanged.uploadGeneration);
        texture.Apply();
        Assert.True(_device.TryGetTextureInfo(texture, out var replaced));
        Assert.True(replaced.uploadGeneration > unchanged.uploadGeneration);
    }

    [Fact]
    public void SamplingStateChangeInvalidatesCachedUpload()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, Color.white);
        Assert.True(_device.TryGetTextureInfo(texture, out var first));
        texture.filterMode = FilterMode.Point;
        Assert.True(_device.EnsureTexture(texture));
        Assert.True(_device.TryGetTextureInfo(texture, out var second));
        Assert.True(second.uploadGeneration > first.uploadGeneration);
        Assert.Equal((int)FilterMode.Point, second.desc.filterMode);
    }

    [Fact]
    public void UploadPreservesMipBiasAndAnisoSamplingState()
    {
        if (!Ready) return;
        Texture2D texture = Solid(4, 4, Color.white);
        texture.mipMapBias = -0.75f;
        texture.anisoLevel = 8;
        Assert.True(_device.EnsureTexture(texture));
        Assert.True(_device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(-0.75f, info.desc.mipMapBias);
        Assert.Equal(8, info.desc.anisoLevel);
    }

    [Fact]
    public void MipBiasChangeInvalidatesCachedUpload()
    {
        if (!Ready) return;
        Texture2D texture = Solid(4, 4, Color.white);
        Assert.True(_device.TryGetTextureInfo(texture, out var first));
        texture.mipMapBias = 1.25f;
        Assert.True(_device.EnsureTexture(texture));
        Assert.True(_device.TryGetTextureInfo(texture, out var second));
        Assert.True(second.uploadGeneration > first.uploadGeneration);
        Assert.Equal(1.25f, second.desc.mipMapBias);
    }

    [Fact]
    public void AnisoChangeInvalidatesCachedUpload()
    {
        if (!Ready) return;
        Texture2D texture = Solid(4, 4, Color.white);
        Assert.True(_device.TryGetTextureInfo(texture, out var first));
        texture.anisoLevel = 12;
        Assert.True(_device.EnsureTexture(texture));
        Assert.True(_device.TryGetTextureInfo(texture, out var second));
        Assert.True(second.uploadGeneration > first.uploadGeneration);
        Assert.Equal(12, second.desc.anisoLevel);
    }

    [Fact]
    public void QualityDisableDisablesTextureAnisotropy()
    {
        WithAnisotropicPolicy(AnisotropicFiltering.Disable, 16, () =>
        {
            if (!Ready) return;
            Texture2D texture = Solid(4, 4, Color.white);
            texture.anisoLevel = 16;
            Assert.True(_device.EnsureTexture(texture));
            Assert.True(_device.TryGetTextureInfo(texture, out var info));
            Assert.Equal(1, info.desc.anisoLevel);
        });
    }

    [Fact]
    public void QualityEnableRetainsTextureAnisotropy()
    {
        WithAnisotropicPolicy(AnisotropicFiltering.Enable, 16, () =>
        {
            if (!Ready) return;
            Texture2D texture = Solid(4, 4, Color.white);
            texture.anisoLevel = 4;
            Assert.True(_device.EnsureTexture(texture));
            Assert.True(_device.TryGetTextureInfo(texture, out var info));
            Assert.Equal(4, info.desc.anisoLevel);
        });
    }

    [Fact]
    public void QualityForceEnableRaisesTextureAnisotropyToGlobalLevel()
    {
        WithAnisotropicPolicy(AnisotropicFiltering.ForceEnable, 8, () =>
        {
            if (!Ready) return;
            Texture2D texture = Solid(4, 4, Color.white);
            texture.anisoLevel = 1;
            Assert.True(_device.EnsureTexture(texture));
            Assert.True(_device.TryGetTextureInfo(texture, out var info));
            Assert.Equal(8, info.desc.anisoLevel);
        });
    }

    [Fact]
    public void NativeAbiNormalizesLegacyZeroAnisotropyToUnityDefault()
    {
        if (!Ready) return;
        var desc = ValidDesc(9201);
        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[4], 4));
        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_GetTextureInfo(
            _device.Handle, desc.textureId, out var info));
        Assert.Equal(1, info.desc.anisoLevel);
    }

    [Fact]
    public void NativeAbiRejectsNanMipBias()
    {
        if (!Ready) return;
        var desc = ValidDesc(9202);
        desc.mipMapBias = float.NaN;
        Assert.Equal(AnityNative.Result.InvalidArg, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[4], 4));
    }

    [Fact]
    public void NativeAbiRejectsInfiniteMipBias()
    {
        if (!Ready) return;
        var desc = ValidDesc(9203);
        desc.mipMapBias = float.PositiveInfinity;
        Assert.Equal(AnityNative.Result.InvalidArg, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[4], 4));
    }

    [Fact]
    public void NativeAbiRejectsNegativeAnisotropy()
    {
        if (!Ready) return;
        var desc = ValidDesc(9204);
        desc.anisoLevel = -1;
        Assert.Equal(AnityNative.Result.InvalidArg, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[4], 4));
    }

    [Fact]
    public void NativeAbiRejectsAnisotropyAboveHardwareContract()
    {
        if (!Ready) return;
        var desc = ValidDesc(9205);
        desc.anisoLevel = 17;
        Assert.Equal(AnityNative.Result.InvalidArg, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[4], 4));
    }

    [Fact]
    public void NativeAbiAcceptsNegativeMipBias()
    {
        if (!Ready) return;
        var desc = ValidDesc(9206);
        desc.mipMapBias = -2.0f;
        desc.anisoLevel = 16;
        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[4], 4));
        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_GetTextureInfo(
            _device.Handle, desc.textureId, out var info));
        Assert.Equal(-2.0f, info.desc.mipMapBias);
        Assert.Equal(16, info.desc.anisoLevel);
    }

    [Fact]
    public void NonReadableApplyStillUploadsBeforeCpuReadabilityIsDropped()
    {
        if (!Ready) return;
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, new Color(.25f, .5f, .75f, 1f));
        texture.Apply(false, true);
        Assert.False(texture.isReadable);
        Assert.True(_device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(4, info.byteCount);
    }

    [Fact]
    public void CanvasBuildAutomaticallyRegistersMainAndAlphaTextures()
    {
        if (!Ready) return;
        GameObject root = new("texture-canvas");
        root.AddComponent<RectTransform>();
        root.AddComponent<Canvas>();
        GameObject child = new("texture-renderer");
        child.AddComponent<RectTransform>();
        child.transform.SetParent(root.transform, false);
        CanvasRenderer renderer = child.AddComponent<CanvasRenderer>();
        renderer.SetMesh(Triangle());
        var main = new Texture2D(1, 1);
        var alpha = new Texture2D(1, 1);
        renderer.SetTexture(main);
        renderer.SetAlphaTexture(alpha);

        Assert.True(CanvasNativeRenderBridge.TryBuildCommands(
            renderer, _device, 0, out _));
        Assert.True(_device.TryGetTextureInfo(main, out _));
        Assert.True(_device.TryGetTextureInfo(alpha, out _));
        UnityEngine.Object.DestroyImmediate(root);
    }

    [Fact]
    public void DestroyTextureRemovesRegistryEntryIdempotently()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, Color.white);
        Assert.True(_device.ReleaseTexture(texture));
        Assert.False(_device.TryGetTextureInfo(texture, out _));
        Assert.True(_device.ReleaseTexture(texture));
    }

    [Fact]
    public void DestroyImmediateReleasesTextureFromEveryLiveDevice()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, Color.white);
        using NativeGraphicsDevice second = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 32, 32, false);
        Assert.True(second.EnsureTexture(texture));
        UnityEngine.Object.DestroyImmediate(texture);
        Assert.False(_device.TryGetTextureInfo(texture, out _));
        Assert.False(second.TryGetTextureInfo(texture, out _));
    }

    [Fact]
    public void TextureRegistriesAreIsolatedPerGraphicsDevice()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, Color.white);
        using NativeGraphicsDevice second = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 32, 32, false);
        Assert.False(second.TryGetTextureInfo(texture, out _));
        Assert.True(second.EnsureTexture(texture));
        Assert.True(second.TryGetTextureInfo(texture, out _));
        Assert.True(_device.TryGetTextureInfo(texture, out _));
    }

    [Fact]
    public void InvalidByteCountIsRejectedByNativeAbi()
    {
        if (!Ready) return;
        var desc = new AnityNative.GraphicsTextureDesc
        {
            textureId = 9001,
            revision = 1,
            width = 2,
            height = 2,
            mipCount = 1,
            filterMode = 1,
            wrapU = 0,
            wrapV = 0
        };
        Assert.Equal(AnityNative.Result.InvalidArg, AnityNative.Graphics_UploadTextureRGBA8(
            _device.Handle, ref desc, new byte[15], 15));
    }

    [Fact]
    public void ConcurrentReplacementsLeaveAValidAtomicEntry()
    {
        if (!Ready) return;
        const ulong textureId = 9101;
        Parallel.For(0, 32, revision =>
        {
            var desc = new AnityNative.GraphicsTextureDesc
            {
                textureId = textureId,
                revision = (ulong)revision,
                width = 1,
                height = 1,
                mipCount = 1,
                filterMode = 1,
                wrapU = 0,
                wrapV = 0
            };
            Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_UploadTextureRGBA8(
                _device.Handle, ref desc,
                new[] { (byte)revision, (byte)2, (byte)3, (byte)255 }, 4));
        });
        Assert.Equal(AnityNative.Result.Ok, AnityNative.Graphics_GetTextureInfo(
            _device.Handle, textureId, out var info));
        Assert.Equal(4, info.byteCount);
        Assert.True(info.uploadGeneration >= 32);
    }

    [Fact]
    public void NullBackendDoesNotExposeCpuStorageAsNativeGpuPointer()
    {
        if (!Ready) return;
        Texture2D texture = Solid(1, 1, Color.white);
        Assert.Equal(IntPtr.Zero, texture.GetNativeTexturePtr());
        Assert.True(_device.TryGetTextureInfo(texture, out var info));
        Assert.Equal(0, info.backendKind);
    }

    public void Dispose() => _device.Dispose();

    private bool Ready => AnityNative.Available && _device.Handle != IntPtr.Zero;

    private static Texture2D Solid(int width, int height, Color color)
    {
        var texture = new Texture2D(width, height);
        var pixels = new Color[width * height];
        Array.Fill(pixels, color);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static AnityNative.GraphicsTextureDesc ValidDesc(ulong textureId) => new()
    {
        textureId = textureId,
        revision = 1,
        width = 1,
        height = 1,
        mipCount = 1,
        filterMode = (int)FilterMode.Bilinear,
        wrapU = (int)TextureWrapMode.Repeat,
        wrapV = (int)TextureWrapMode.Repeat,
        linear = 1
    };

    private static void WithAnisotropicPolicy(
        AnisotropicFiltering policy, int level, Action action)
    {
        AnisotropicFiltering oldPolicy = QualitySettings.anisotropicFiltering;
        int oldLevel = QualitySettings.anisotropicFilteringLevel;
        try
        {
            QualitySettings.anisotropicFiltering = policy;
            QualitySettings.anisotropicFilteringLevel = level;
            action();
        }
        finally
        {
            QualitySettings.anisotropicFiltering = oldPolicy;
            QualitySettings.anisotropicFilteringLevel = oldLevel;
        }
    }

    private static Mesh Triangle()
    {
        var mesh = new Mesh
        {
            vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
            colors32 = new[]
            {
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255)
            }
        };
        mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
        return mesh;
    }

    private static ulong Id(UnityEngine.Object value)
        => unchecked((ulong)(uint)value.GetInstanceID());
}
