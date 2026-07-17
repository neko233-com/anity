using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Anity.Core.Runtime.Native;
using Xunit;

namespace Anity.Core.Tests;

public sealed class NativeUICanvasBatchTests
{
    [Fact]
    public void RenderCommandStructLayoutsMatchNativeAbi()
    {
        Assert.Equal(72, Marshal.SizeOf<AnityNative.UIRenderCommandDesc>());
        Assert.Equal(48, Marshal.SizeOf<AnityNative.UIBatchInfo>());
        Assert.Equal(40, Marshal.SizeOf<AnityNative.UICanvasStats>());
    }

    [Fact]
    public void CreateStartsWithEmptyPersistentState()
        => WithCanvas(canvas =>
        {
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(0, stats.commandCount);
            Assert.Equal(0, stats.batchCount);
            Assert.Equal(0UL, stats.generation);
        });

    [Fact]
    public void BeginFrameUpdatesFrameWithoutDroppingCommands()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(canvas.BeginFrame(42));
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(42UL, stats.frameId);
            Assert.Equal(1, stats.commandCount);
        });

    [Fact]
    public void UpsertAddsAndReplacementDoesNotDuplicateRenderer()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            ulong firstGeneration = canvas.GetStats().generation;
            Assert.True(canvas.Upsert(Desc(1, material: 9), Quad(10), QuadIndices));
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(1, stats.commandCount);
            Assert.Equal(firstGeneration + 1, stats.generation);
            Assert.True(canvas.BuildBatches());
            Assert.Equal(9UL, canvas.GetBatchInfo(0).materialId);
        });

    [Fact]
    public void RemoveIsIdempotentAndClearAdvancesGeneration()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1), Quad(0), QuadIndices));
            Assert.True(canvas.Remove(1));
            ulong afterRemove = canvas.GetStats().generation;
            Assert.True(canvas.Remove(1));
            Assert.Equal(afterRemove, canvas.GetStats().generation);
            Assert.True(canvas.Clear());
            Assert.Equal(afterRemove + 1, canvas.GetStats().generation);
        });

    [Fact]
    public void SameStateCommandsMergeAndRebaseIndices()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1, depth: 2), Quad(0), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, depth: 3), Quad(10), QuadIndices));
            Assert.True(canvas.BuildBatches());
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(1, stats.batchCount);
            Assert.Equal(8, stats.vertexCount);
            Assert.Equal(12, stats.indexCount);
            Assert.Equal(new uint[] { 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4 }, canvas.GetBatchIndices(0));
            Assert.Equal(2, canvas.GetBatchInfo(0).commandCount);
        });

    [Fact]
    public void BatchVertexCopyPreservesSortedCommandOrder()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1, depth: 20), Quad(20), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, depth: 10), Quad(10), QuadIndices));
            Assert.True(canvas.BuildBatches());
            AnityNative.UIPackedVertex[] vertices = canvas.GetBatchVertices(0);
            Assert.Equal(10, vertices[0].position.x);
            Assert.Equal(20, vertices[4].position.x);
            Assert.Equal(10, canvas.GetBatchInfo(0).firstSortDepth);
            Assert.Equal(20, canvas.GetBatchInfo(0).lastSortDepth);
        });

    [Fact]
    public void MaterialTextureAndAlphaTextureEachBreakBatch()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1, material: 1), Quad(0), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, material: 2), Quad(10), QuadIndices));
            Assert.True(canvas.Upsert(Desc(3, material: 2, texture: 7), Quad(20), QuadIndices));
            Assert.True(canvas.Upsert(Desc(4, material: 2, texture: 7, alphaTexture: 8), Quad(30), QuadIndices));
            Assert.True(canvas.BuildBatches());
            Assert.Equal(4, canvas.GetStats().batchCount);
        });

    [Fact]
    public void MatchingClipMergesButDifferentClipBreaksBatch()
        => WithCanvas(canvas =>
        {
            var first = Desc(1, flags: AnityNative.UIRenderCommandFlags.Visible | AnityNative.UIRenderCommandFlags.RectClip);
            var second = Desc(2, flags: first.flags);
            var third = Desc(3, flags: first.flags);
            third.clipXMax = 99;
            Assert.True(canvas.Upsert(first, Quad(0), QuadIndices));
            Assert.True(canvas.Upsert(second, Quad(10), QuadIndices));
            Assert.True(canvas.Upsert(third, Quad(20), QuadIndices));
            Assert.True(canvas.BuildBatches());
            Assert.Equal(2, canvas.GetStats().batchCount);
            Assert.Equal(2, canvas.GetBatchInfo(0).commandCount);
        });

    [Theory]
    [InlineData(AnityNative.UIRenderCommandFlags.Mask)]
    [InlineData(AnityNative.UIRenderCommandFlags.Pop)]
    public void MaskAndPopCommandsRemainIsolated(AnityNative.UIRenderCommandFlags stateFlag)
        => WithCanvas(canvas =>
        {
            var flags = AnityNative.UIRenderCommandFlags.Visible | stateFlag;
            Assert.True(canvas.Upsert(Desc(1, flags: flags), Quad(0), QuadIndices));
            Assert.True(canvas.Upsert(Desc(2, flags: flags), Quad(10), QuadIndices));
            Assert.True(canvas.BuildBatches());
            Assert.Equal(2, canvas.GetStats().batchCount);
        });

    [Fact]
    public void InvisibleAndTransparentCommandsAreExcludedFromBatches()
        => WithCanvas(canvas =>
        {
            Assert.True(canvas.Upsert(Desc(1, flags: 0), Quad(0), QuadIndices));
            var transparent = Desc(2);
            transparent.effectiveAlpha = .001f;
            Assert.True(canvas.Upsert(transparent, Quad(10), QuadIndices));
            Assert.True(canvas.Upsert(Desc(3), Quad(20), QuadIndices));
            Assert.True(canvas.BuildBatches());
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(3, stats.commandCount);
            Assert.Equal(1, stats.visibleCommandCount);
            Assert.Equal(1, stats.batchCount);
        });

    [Fact]
    public void InvalidIndexIsRejectedWithoutMutatingGeneration()
        => WithCanvas(canvas =>
        {
            ulong generation = canvas.GetStats().generation;
            Assert.False(canvas.Upsert(Desc(1), Quad(0), new uint[] { 0, 1, 9 }));
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(generation, stats.generation);
            Assert.Equal(0, stats.commandCount);
        });

    [Fact]
    public void ConcurrentUpsertsHaveNativeMutexOwnership()
        => WithCanvas(canvas =>
        {
            var failures = new ConcurrentQueue<ulong>();
            Parallel.For(1, 33, renderer =>
            {
                if (!canvas.Upsert(Desc((ulong)renderer, depth: renderer), Quad(renderer), QuadIndices))
                    failures.Enqueue((ulong)renderer);
            });
            Assert.Empty(failures);
            Assert.True(canvas.BuildBatches());
            AnityNative.UICanvasStats stats = canvas.GetStats();
            Assert.Equal(32, stats.commandCount);
            Assert.Equal(1, stats.batchCount);
            Assert.Equal(128, stats.vertexCount);
        });

    [Fact]
    public void DisposedCanvasRejectsManagedOperations()
    {
        NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        AssertNativeResolved(canvas is not null);
        if (canvas is null) return;
        canvas.Dispose();
        Assert.False(canvas.IsValid);
        Assert.False(canvas.BeginFrame(1));
        Assert.False(canvas.BuildBatches());
        canvas.Dispose();
    }

    private static readonly uint[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

    private static AnityNative.UIRenderCommandDesc Desc(
        ulong renderer, int depth = 0, ulong material = 1, ulong texture = 2,
        ulong alphaTexture = 3,
        AnityNative.UIRenderCommandFlags flags = AnityNative.UIRenderCommandFlags.Visible)
        => new()
        {
            rendererId = renderer,
            materialId = material,
            textureId = texture,
            alphaTextureId = alphaTexture,
            sortDepth = depth,
            flags = flags,
            clipXMin = 0,
            clipYMin = 0,
            clipXMax = 100,
            clipYMax = 100,
            softnessX = 1,
            softnessY = 2,
            effectiveAlpha = 1
        };

    private static AnityNative.UIPackedVertex[] Quad(float offset)
    {
        var result = new AnityNative.UIPackedVertex[4];
        for (int index = 0; index < result.Length; index++)
        {
            result[index].position = new AnityNative.UIVector3(offset + index, index, 0);
            result[index].color = new AnityNative.UIColor32(10, 20, 30, 255);
        }
        return result;
    }

    private static void WithCanvas(Action<NativeUICanvas> action)
    {
        using NativeUICanvas? canvas = NativeUICanvas.TryCreate();
        AssertNativeResolved(canvas is not null);
        if (canvas is not null) action(canvas);
    }

    private static void AssertNativeResolved(bool resolved)
    {
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.True(resolved);
    }
}
