using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Anity.Core.Runtime.Native;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeGraphicsUIUploadTests
{
    [Fact]
    public void UploadStatsLayoutMatchesNativeAbi()
        => Assert.Equal(56, Marshal.SizeOf<AnityNative.GraphicsUIUploadStats>());

    [Fact]
    public void NewDeviceStartsWithZeroUploadStats()
        => WithNativeResources((device, _) =>
        {
            Assert.Equal(0UL, device.LastUIUploadStats.uploadGeneration);
            Assert.Equal(0, device.LastUIUploadStats.submitted);
        });

    [Fact]
    public void AttachedCanvasIsAutomaticallySubmittedAtEndFrame()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.AttachUICanvas(canvas));
            device.BeginFrame();
            device.EndFrame();

            AnityNative.GraphicsUIUploadStats stats = device.LastUIUploadStats;
            Assert.Equal(1, stats.submitted);
            Assert.Equal(1UL, stats.frameId);
            Assert.Equal(1UL, stats.uploadGeneration);
            Assert.Equal(1, stats.batchCount);
        });

    [Fact]
    public void UploadReportsExactVertexIndexCountsAndBytes()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            AnityNative.GraphicsUIUploadStats stats = device.LastUIUploadStats;
            Assert.Equal(4, stats.vertexCount);
            Assert.Equal(6, stats.indexCount);
            Assert.Equal(4 * Marshal.SizeOf<AnityNative.UIPackedVertex>(), stats.vertexBytes);
            Assert.Equal(6 * sizeof(uint), stats.indexBytes);
        });

    [Fact]
    public void TripleRingRotatesWithDeviceFrameId()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.AttachUICanvas(canvas));
            for (int expected = 1; expected <= 3; expected++)
            {
                device.BeginFrame();
                device.EndFrame();
                Assert.Equal(expected % 3, device.LastUIUploadStats.ringIndex);
                Assert.Equal((ulong)expected, device.LastUIUploadStats.frameId);
            }
        });

    [Fact]
    public void RepeatedSubmissionsAdvanceUploadGeneration()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.Equal(2UL, device.LastUIUploadStats.uploadGeneration);
        });

    [Fact]
    public void DirectSubmissionDoesNotAttachCanvas()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.False(device.HasAttachedUICanvas);
            Assert.Equal(0UL, device.LastUIUploadStats.frameId);
            Assert.Equal(0, device.LastUIUploadStats.ringIndex);
        });

    [Fact]
    public void DetachStopsEndFrameSubmission()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.AttachUICanvas(canvas));
            device.BeginFrame();
            device.EndFrame();
            ulong generation = device.LastUIUploadStats.uploadGeneration;
            Assert.True(device.AttachUICanvas(null));
            device.BeginFrame();
            device.EndFrame();
            Assert.Equal(generation, device.LastUIUploadStats.uploadGeneration);
            Assert.False(device.HasAttachedUICanvas);
        });

    [Fact]
    public void EmptyCanvasProducesSubmittedZeroLengthUpload()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(device.SubmitUICanvas(canvas));
            AnityNative.GraphicsUIUploadStats stats = device.LastUIUploadStats;
            Assert.Equal(1, stats.submitted);
            Assert.Equal(0, stats.batchCount);
            Assert.Equal(0, stats.vertexBytes);
            Assert.Equal(0, stats.indexBytes);
        });

    [Fact]
    public void MaterialSplitProducesTwoDrawRecords()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1, material: 11), Quad(0), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, material: 22), Quad(10), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.Equal(2, device.LastUIUploadStats.batchCount);
            Assert.Equal(0, device.LastUIUploadStats.drawCount);
            Assert.Equal(8, device.LastUIUploadStats.vertexCount);
        });

    [Fact]
    public void ReplacingRendererUpdatesNextUploadWithoutDuplicatingGeometry()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(7), Quad(0), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.True(canvas.Upsert(Desc(7, material: 99), Quad(100), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.Equal(1, device.LastUIUploadStats.batchCount);
            Assert.Equal(4, device.LastUIUploadStats.vertexCount);
            Assert.Equal(6, device.LastUIUploadStats.indexCount);
        });

    [Fact]
    public void InvisibleCommandsAreNotUploaded()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1, flags: 0), Quad(0), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2), Quad(10), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.Equal(1, device.LastUIUploadStats.batchCount);
            Assert.Equal(4, device.LastUIUploadStats.vertexCount);
        });

    [Fact]
    public void DisposedAttachedCanvasIsDetachedBeforeNativeFrameCall()
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 64, 64, false);
        NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        if (!AssertNativeResolved(device.Handle != IntPtr.Zero && canvas is not null))
        {
            canvas?.Dispose();
            return;
        }
        Assert.True(device.AttachUICanvas(canvas));
        canvas!.Dispose();
        device.BeginFrame();
        device.EndFrame();
        Assert.False(device.HasAttachedUICanvas);
        Assert.Equal(0UL, device.LastUIUploadStats.uploadGeneration);
    }

    [Fact]
    public void CpuFallbackUsesBackendKindZero()
        => WithNativeResources((device, canvas) =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(device.SubmitUICanvas(canvas));
            Assert.Equal(0, device.LastUIUploadStats.backendKind);
        });

    [Fact]
    public async Task SnapshotSubmissionIsAtomicAgainstConcurrentCommandUpdates()
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 64, 64, false);
        NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        try
        {
            if (!AssertNativeResolved(device.Handle != IntPtr.Zero && canvas is not null)) return;
            NativeUICanvas liveCanvas = canvas!;
            Assert.True(liveCanvas.Upsert(Desc(1), Quad(0), QuadIndices));
            bool submitFailed = false;
            bool updateFailed = false;
            Task submitter = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                    if (!device.SubmitUICanvas(liveCanvas)) submitFailed = true;
            });
            Task updater = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                    if (!liveCanvas.Upsert(Desc(1, material: (ulong)(i + 1)), Quad(i), QuadIndices))
                        updateFailed = true;
            });
            await Task.WhenAll(submitter, updater);
            Assert.False(submitFailed);
            Assert.False(updateFailed);
            Assert.True(device.SubmitUICanvas(liveCanvas));
            Assert.Equal(4, device.LastUIUploadStats.vertexCount);
            Assert.Equal(6, device.LastUIUploadStats.indexCount);
        }
        finally
        {
            canvas?.Dispose();
            device.Dispose();
        }
    }

    [Fact]
    public void MetalDeviceUploadsToMetalBufferOnMacOS()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 64, 64, false);
        using NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        if (!AssertNativeResolved(device.Handle != IntPtr.Zero && canvas is not null)) return;
        Assert.True(canvas!.Upsert(Desc(1), Quad(0), QuadIndices));
        Assert.True(device.SubmitUICanvas(canvas));
        Assert.Equal(2, device.LastUIUploadStats.backendKind);
    }

    private static readonly uint[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

    private static AnityNative.UIRenderCommandDesc Desc(
        ulong renderer, ulong material = 1,
        AnityNative.UIRenderCommandFlags flags = AnityNative.UIRenderCommandFlags.Visible)
        => new()
        {
            rendererId = renderer,
            materialId = material,
            textureId = 2,
            alphaTextureId = 3,
            sortDepth = checked((int)renderer),
            flags = flags,
            clipXMin = 0,
            clipYMin = 0,
            clipXMax = 100,
            clipYMax = 100,
            effectiveAlpha = 1
        };

    private static AnityNative.UIPackedVertex[] Quad(float offset)
    {
        var vertices = new AnityNative.UIPackedVertex[4];
        for (int index = 0; index < vertices.Length; index++)
        {
            vertices[index].position = new AnityNative.UIVector3(offset + index, index, 0);
            vertices[index].color = new AnityNative.UIColor32(255, 255, 255, 255);
        }
        return vertices;
    }

    private static void WithNativeResources(Action<NativeGraphicsDevice, NativeUICanvas> action)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 64, 64, false);
        using NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        if (!AssertNativeResolved(device.Handle != IntPtr.Zero && canvas is not null)) return;
        action(device, canvas!);
    }

    private static bool AssertNativeResolved(bool resolved)
    {
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.True(resolved);
        return resolved;
    }
}
