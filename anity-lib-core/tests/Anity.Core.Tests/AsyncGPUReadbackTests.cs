using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>AsyncGPUReadback Unity 2022 behavior: deferred completion, data, failure and native containers.</summary>
[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class AsyncGPUReadbackTests
{
    [Fact]
    public void TextureRequest_IsDeferredUntilPlayerLoop()
    {
        var texture = MakeTexture(2, 2);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, null!);
        Assert.False(request.done);
        UnityRuntime.Tick(0.001f);
        Assert.True(request.done);
        Assert.False(request.hasError);
    }

    [Fact]
    public void TextureRequest_CallbackReceivesCompletedRequestExactlyOnce()
    {
        int calls = 0;
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(MakeTexture(2, 2), 0, completed =>
        {
            calls++;
            Assert.True(completed.done);
            Assert.False(completed.hasError);
        });
        UnityRuntime.Tick(0.001f);
        UnityRuntime.Tick(0.001f);
        Assert.True(request.done);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TextureRequest_GetDataByte_ReturnsRgbaPixels()
    {
        var texture = MakeTexture(2, 1);
        texture.SetPixel(0, 0, new Color32(1, 2, 3, 4));
        texture.SetPixel(1, 0, new Color32(5, 6, 7, 8));
        texture.Apply(false);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, null!);
        request.WaitForCompletion();
        NativeArray<byte> data = request.GetData<byte>();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, data.ToArray());
    }

    [Fact]
    public void TextureRequest_GetDataColor32_ReturnsTypedPixels()
    {
        var texture = MakeTexture(1, 1);
        texture.SetPixel(0, 0, new Color32(12, 34, 56, 78));
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, null!);
        request.WaitForCompletion();
        NativeArray<Color32> data = request.GetData<Color32>();
        Assert.Single(data.ToArray());
        Assert.Equal(new Color32(12, 34, 56, 78), data[0]);
    }

    [Fact]
    public void TextureRegionRequest_ReportsDimensionsAndOnlyRegion()
    {
        var texture = MakeTexture(4, 2);
        texture.SetPixel(2, 1, new Color32(31, 32, 33, 34));
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, 2, 1, 1, 1, 0, 1, null!);
        request.WaitForCompletion();
        Assert.Equal(1, request.width);
        Assert.Equal(1, request.height);
        Assert.Equal(1, request.depth);
        Assert.Equal(1, request.layerCount);
        Assert.Equal(4, request.layerDataSize);
        Assert.Equal(new byte[] { 31, 32, 33, 34 }, request.GetData<byte>().ToArray());
    }

    [Fact]
    public void TextureMipRequest_UsesMipDimensions()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true);
        texture.SetPixels32(new[] { new Color32(9, 8, 7, 6), new Color32(1, 1, 1, 1), new Color32(1, 1, 1, 1), new Color32(1, 1, 1, 1) }, 1);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 1, null!);
        request.WaitForCompletion();
        Assert.Equal(2, request.width);
        Assert.Equal(2, request.height);
        Assert.Equal(16, request.GetData<byte>().Length);
        Assert.Equal((byte)9, request.GetData<byte>()[0]);
    }

    [Fact]
    public void InvalidTextureRegion_CompletesWithErrorAndInvokesCallback()
    {
        bool callbackError = false;
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(MakeTexture(2, 2), 0, 1, 2, 1, 2, 0, 1, completed => callbackError = completed.hasError);
        request.WaitForCompletion();
        Assert.True(request.done);
        Assert.True(request.hasError);
        Assert.True(callbackError);
        Assert.Throws<InvalidOperationException>(() => request.GetData<byte>());
    }

    [Fact]
    public void ComputeBufferRequest_ReadsRequestedByteRange()
    {
        using var buffer = new ComputeBuffer(4, sizeof(int));
        buffer.SetData(new[] { 10, 20, 30, 40 });
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(buffer, sizeof(int) * 2, sizeof(int), null!);
        request.WaitForCompletion();
        Assert.Equal(new[] { 20, 30 }, request.GetData<int>().ToArray());
    }

    [Fact]
    public void GraphicsBufferRequest_ReadsData()
    {
        using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(int));
        buffer.SetData(new[] { 101, 202 });
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(buffer, null!);
        request.WaitForCompletion();
        Assert.Equal(new[] { 101, 202 }, request.GetData<int>().ToArray());
    }

    [Fact]
    public void RequestIntoNativeArray_CopiesIntoCallerAllocation()
    {
        using var buffer = new ComputeBuffer(2, sizeof(int));
        buffer.SetData(new[] { 7, 11 });
        var destination = new NativeArray<int>(2, Allocator.Temp);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(ref destination, buffer, null!);
        request.WaitForCompletion();
        Assert.False(request.hasError);
        Assert.Equal(new[] { 7, 11 }, destination.ToArray());
    }

    [Fact]
    public void RequestIntoNativeSlice_CopiesOnlySlice()
    {
        using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(int));
        buffer.SetData(new[] { 70, 110 });
        var backing = new NativeArray<int>(4, Allocator.Temp);
        var destination = new NativeSlice<int>(backing, 1, 2);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeSlice(ref destination, buffer, null!);
        request.WaitForCompletion();
        Assert.False(request.hasError);
        Assert.Equal(70, backing[1]);
        Assert.Equal(110, backing[2]);
    }

    [Fact]
    public void UpdateAndWaitAllRequests_CompleteQueuedRequests()
    {
        AsyncGPUReadbackRequest first = AsyncGPUReadback.Request(MakeTexture(1, 1), 0, null!);
        AsyncGPUReadbackRequest second = AsyncGPUReadback.Request(MakeTexture(1, 1), 0, null!);
        first.Update();
        Assert.True(first.done);
        Assert.False(second.done);
        AsyncGPUReadback.WaitAllRequests();
        Assert.True(second.done);
    }

    [Fact]
    public void ForcePlayerLoopUpdateAndSystemInfoAreAvailable()
    {
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(MakeTexture(1, 1), 0, null!);
        request.forcePlayerLoopUpdate = true;
        Assert.True(request.forcePlayerLoopUpdate);
        Assert.True(SystemInfo.supportsAsyncGPUReadback);
        UnityRuntime.Tick(0.001f);
        Assert.True(request.done);
    }

    [Fact]
    public void GetDataBeforeCompletion_Throws()
    {
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(MakeTexture(1, 1), 0, null!);
        Assert.Throws<InvalidOperationException>(() => request.GetData<byte>());
        request.WaitForCompletion();
    }

    [Fact]
    public void Texture2DArrayRequest_ReturnsIndependentLayers()
    {
        var texture = new Texture2DArray(1, 1, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { new Color(1f, 0f, 0f, 1f) }, 0);
        texture.SetPixels(new[] { new Color(0f, 1f, 0f, 1f) }, 1);
        texture.Apply(false);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, null!);
        request.WaitForCompletion();
        Assert.Equal(2, request.depth);
        Assert.Equal(2, request.layerCount);
        Assert.Equal(4, request.layerDataSize);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, request.GetData<byte>(0).ToArray());
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, request.GetData<byte>(1).ToArray());
    }

    [Fact]
    public void Texture3DRegionRequest_RespectsZSlices()
    {
        var texture = new Texture3D(2, 1, 3, TextureFormat.RGBA32, false);
        texture.SetPixel(1, 0, 1, new Color32(12, 34, 56, 255));
        texture.SetPixel(1, 0, 2, new Color32(78, 90, 12, 255));
        texture.Apply(false);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, 1, 1, 0, 1, 1, 2, null!);
        request.WaitForCompletion();
        Assert.Equal(2, request.depth);
        Assert.Equal(2, request.layerCount);
        Assert.Equal(new byte[] { 12, 34, 56, 255 }, request.GetData<byte>(0).ToArray());
        Assert.Equal(new byte[] { 78, 90, 12, 255 }, request.GetData<byte>(1).ToArray());
    }

    [Fact]
    public void CubemapRequest_MapsZToFaces()
    {
        var texture = new Cubemap(1, TextureFormat.RGBA32, false);
        texture.SetPixel(UnityEngine.CubemapFace.PositiveY, 0, 0, new Color32(21, 22, 23, 255));
        texture.Apply(false);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, 0, 1, 0, 1, (int)UnityEngine.CubemapFace.PositiveY, 1, null!);
        request.WaitForCompletion();
        Assert.Equal(new byte[] { 21, 22, 23, 255 }, request.GetData<byte>().ToArray());
    }

    [Fact]
    public void CubemapArrayRequest_MapsZAcrossCubemapAndFace()
    {
        var texture = new CubemapArray(1, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { new Color(0.25f, 0.5f, 0.75f, 1f) }, UnityEngine.CubemapFace.PositiveX, 1);
        texture.Apply(false);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, 0, 1, 0, 1, 6, 1, null!);
        request.WaitForCompletion();
        Assert.Equal(new byte[] { 64, 128, 191, 255 }, request.GetData<byte>().ToArray());
    }

    [Fact]
    public void LayerIndexOutsideRequest_Throws()
    {
        var texture = new Texture2DArray(1, 1, 2, TextureFormat.RGBA32, false);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, null!);
        request.WaitForCompletion();
        Assert.Throws<ArgumentOutOfRangeException>(() => request.GetData<byte>(2));
    }

    private static Texture2D MakeTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 255), width * height).ToArray());
        texture.Apply(false);
        return texture;
    }
}
