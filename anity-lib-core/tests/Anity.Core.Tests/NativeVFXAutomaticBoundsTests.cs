using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXAutomaticBoundsTests
{
    [Fact]
    public void NativeLayouts_MatchAutomaticBoundsCAbi()
    {
        Assert.Equal(56, Marshal.SizeOf<AnityNative.GraphicsVFXBoundsReductionDesc>());
        Assert.Equal(56, Marshal.SizeOf<AnityNative.GraphicsVFXBoundsReductionResult>());
    }

    [Fact]
    public void CpuReduction_IncludesSizeScaleAndPadding()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 1, 1, new Vector3(-4f, 2f, 10f), size: 2f,
            scale: new Vector3(1f, 2f, 3f));
        Spawn(device, 1, 2, new Vector3(6f, -2f, 2f), size: 4f,
            scale: new Vector3(2f, 1f, 0.5f));

        Assert.True(Reduce(device, 1, Padding(1f, 2f, 3f), out Bounds bounds, out int backend));

        Assert.Equal(0, backend);
        AssertVector(new Vector3(2.5f, 0f, 7f), bounds.center);
        AssertVector(new Vector3(17f, 12f, 18f), bounds.size);
    }

    [Fact]
    public void CpuReduction_IgnoresUnusedDeadListSlots()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 2, 1, new Vector3(3f, 4f, 5f), usesDeadList: true);

        Assert.True(Reduce(device, 2, Metadata(), out Bounds bounds, out _));
        AssertVector(new Vector3(3f, 4f, 5f), bounds.center);
        AssertVector(Vector3.one, bounds.size);
    }

    [Fact]
    public void CpuReduction_WithoutAliveAttributeUsesSequentialOccupancy()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(includeAlive: false, usesDeadList: false);
        Assert.True(Submit(device, 3, 1, new Vector3(7f, 8f, 9f), kernel));
        VFXParticleCullingBounds metadata = Metadata() with
        {
            AliveOffsetWords = -1,
            PositionOffsetWords = 0,
            SizeOffsetWords = 3,
            ScaleXOffsetWords = 4,
            ScaleYOffsetWords = 5,
            ScaleZOffsetWords = 6
        };

        Assert.True(Reduce(device, 3, metadata, out Bounds bounds, out _));
        AssertVector(new Vector3(7f, 8f, 9f), bounds.center);
    }

    [Fact]
    public void ZeroAliveParticlesRemainConservativelyUnbounded()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(aliveDefault: false);
        Assert.True(Submit(device, 4, 1, Vector3.zero, kernel));

        Assert.False(Reduce(device, 4, Metadata(), out _, out _));
    }

    [Fact]
    public void NonFiniteAlivePositionDoesNotPublishUnsafeBounds()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 5, 1, new Vector3(float.NaN, 0f, 0f));

        Assert.False(Reduce(device, 5, Metadata(), out _, out _));
    }

    [Fact]
    public void InvalidAttributeOffsetIsRejectedBeforeReadback()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 6, 1, Vector3.zero);

        Assert.False(Reduce(
            device, 6, Metadata() with { PositionOffsetWords = 999 }, out _, out _));
    }

    [Fact]
    public void PreparedFrameRejectsUncommittedBoundsReadback()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 7, 1, Vector3.zero);
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            7, frame, 0.02f, 1f, 0.02f, 0.1f, false, out _));

        Assert.False(Reduce(device, 7, Metadata(), out _, out _));
        Assert.True(device.AbortVFXEffectFrame(7, frame));
    }

    [Fact]
    public void AbortRestoresPreviouslyCommittedAutomaticBounds()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 8, 1, new Vector3(1f, 2f, 3f));
        Assert.True(Reduce(device, 8, Metadata(), out Bounds before, out _));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            8, frame, 0.02f, 1f, 0.02f, 0.1f, false, out _));
        Spawn(device, 8, 2, new Vector3(100f, 200f, 300f));
        Assert.True(device.AbortVFXEffectFrame(8, frame));

        Assert.True(Reduce(device, 8, Metadata(), out Bounds after, out _));
        AssertVector(before.center, after.center);
        AssertVector(before.size, after.size);
    }

    [Fact]
    public void CommitPublishesNewAutomaticBoundsGeneration()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 9, 1, Vector3.zero);
        Assert.True(Reduce(device, 9, Metadata(), out Bounds before, out _, out ulong generationBefore));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            9, frame, 0.02f, 1f, 0.02f, 0.1f, false, out _));
        Spawn(device, 9, 2, new Vector3(10f, 0f, 0f));
        Assert.True(device.CommitVFXEffectFrame(9, frame, out _));

        Assert.True(Reduce(device, 9, Metadata(), out Bounds after, out _, out ulong generationAfter));
        Assert.True(generationAfter > generationBefore);
        Assert.True(after.size.x > before.size.x);
        Assert.Equal(5f, after.center.x, 4);
    }

    [Fact]
    public void RuntimeV13_RoundTripsAutomaticLayoutAndPadding()
    {
        VFXRuntimeSystemData system = RuntimeSystem(worldSpace: true);
        VFXRuntimeAssetData data = RuntimeData(system);

        VFXRuntimeSystemData restored = Assert.Single(
            VFXRuntimeAssetData.Deserialize(data.Serialize()).Systems);

        Assert.True(restored.HasAutomaticBounds);
        Assert.True(restored.BoundsInWorldSpace);
        Assert.Equal((1, 0, 4, 5, 6, 7),
            (restored.PositionOffsetWords, restored.AliveOffsetWords,
             restored.SizeOffsetWords, restored.ScaleXOffsetWords,
             restored.ScaleYOffsetWords, restored.ScaleZOffsetWords));
        Assert.Equal((1f, 2f, 3f),
            (restored.AutomaticBoundsPaddingX,
             restored.AutomaticBoundsPaddingY,
             restored.AutomaticBoundsPaddingZ));
    }

    [Fact]
    public void RuntimeV13_RejectsAutomaticLayoutThatDoesNotMatchInitializeKernel()
    {
        VFXRuntimeSystemData system = RuntimeSystem() with { PositionOffsetWords = 2 };

        Assert.Throws<InvalidDataException>(() => RuntimeData(system).Serialize());
    }

    [Fact]
    public void VisualEffect_LocalAutomaticBoundsUseFullComponentTransform()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(RuntimeSystem()).Serialize());
        var gameObject = new GameObject("Automatic local bounds");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        effect.transform!.position = new Vector3(10f, 20f, 30f);
        effect.transform.localScale = new Vector3(2f, 3f, 4f);
        try
        {
            Send(effect, device, new Vector3(2f, 3f, 4f));

            Assert.True(effect.TryGetWorldCullingBounds(device, out Bounds bounds));
            AssertVector(new Vector3(14f, 29f, 46f), bounds.center);
            AssertVector(new Vector3(6f, 15f, 28f), bounds.size);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void VisualEffect_WorldAutomaticBoundsIgnoreComponentTransform()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(RuntimeSystem(worldSpace: true)).Serialize());
        var gameObject = new GameObject("Automatic world bounds");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        effect.transform!.position = new Vector3(100f, 200f, 300f);
        try
        {
            Send(effect, device, new Vector3(2f, 3f, 4f));

            Assert.True(effect.TryGetWorldCullingBounds(device, out Bounds bounds));
            AssertVector(new Vector3(2f, 3f, 4f), bounds.center);
            AssertVector(new Vector3(3f, 5f, 7f), bounds.size);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void MetalReduction_UsesComputeBackendAndMatchesCpuContract()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 12, 1, new Vector3(-2f, 3f, 4f), size: 2f,
            scale: new Vector3(1f, 2f, 3f));
        Spawn(device, 12, 2, new Vector3(8f, -1f, 2f), size: 4f,
            scale: new Vector3(2f, 1f, 0.5f));

        Assert.True(Reduce(device, 12, Padding(1f, 2f, 3f), out Bounds bounds, out int backend));
        Assert.Equal(2, backend);
        AssertVector(new Vector3(4.5f, 1f, 4f), bounds.center);
        AssertVector(new Vector3(17f, 12f, 12f), bounds.size);
    }

    [Fact]
    public void MetalResidentBounds_ReusesCommittedUpdateParticleBuffer()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 20, 1, Vector3.zero);
        Assert.True(DispatchPosition(device, 20, new Vector3(4f, 5f, 6f)));

        Assert.True(Reduce(device, 20, Metadata(), out Bounds bounds, out int backend));

        Assert.Equal(2, backend);
        AssertVector(new Vector3(4f, 5f, 6f), bounds.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 20);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsResidentHitCount);
        Assert.Equal(0ul, stats.boundsParticleUploadCount);
        Assert.Equal(1ul, stats.boundsCompletionCount);
    }

    [Fact]
    public void MetalResidentBounds_RepeatedDescriptorUsesResultCache()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(21);

        Assert.True(Reduce(device, 21, Metadata(), out Bounds first, out _));
        Assert.True(Reduce(device, 21, Metadata(), out Bounds second, out _));

        AssertVector(first.center, second.center);
        AssertVector(first.size, second.size);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 21);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsCompletionCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_PaddingChangeInvalidatesResultCacheOnly()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(22);
        Assert.True(Reduce(device, 22, Metadata(), out _, out _));

        Assert.True(Reduce(device, 22, Padding(1f, 2f, 3f), out Bounds padded, out _));

        AssertVector(new Vector3(3f, 5f, 7f), padded.size);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 22);
        Assert.Equal(2ul, stats.boundsDispatchCount);
        Assert.Equal(2ul, stats.boundsResidentHitCount);
        Assert.Equal(0ul, stats.boundsParticleUploadCount);
        Assert.Equal(0ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_WorldSpaceFlagParticipatesInCacheKey()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(23);
        Assert.True(Reduce(device, 23, Metadata(), out _, out _));

        Assert.True(Reduce(
            device, 23, Metadata() with { WorldSpace = true }, out _, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 23);
        Assert.Equal(2ul, stats.boundsDispatchCount);
        Assert.Equal(2ul, stats.boundsResidentHitCount);
        Assert.Equal(0ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_InitializeKeepsGenerationResident()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(24);
        Assert.True(Reduce(device, 24, Metadata(), out _, out _));
        Spawn(device, 24, 2, new Vector3(9f, 0f, 0f));

        Assert.True(Reduce(device, 24, Metadata(), out Bounds bounds, out _));

        Assert.Equal(5f, bounds.center.x, 4);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 24);
        Assert.Equal(2ul, stats.boundsDispatchCount);
        Assert.Equal(2ul, stats.boundsResidentHitCount);
        Assert.Equal(0ul, stats.boundsParticleUploadCount);
        Assert.Equal(1ul, stats.residentInitializeCount);
    }

    [Fact]
    public void MetalResidentBounds_CommittedUpdateInvalidatesPreviousResult()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(25);
        Assert.True(Reduce(device, 25, Metadata(), out _, out _));
        Assert.True(DispatchPosition(device, 25, new Vector3(8f, 0f, 0f)));

        Assert.True(Reduce(device, 25, Metadata(), out Bounds bounds, out _));

        Assert.Equal(8f, bounds.center.x, 4);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 25);
        Assert.Equal(2ul, stats.boundsDispatchCount);
        Assert.Equal(2ul, stats.boundsResidentHitCount);
        Assert.Equal(0ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_CancelledUpdatePreservesCommittedCache()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(26);
        Assert.True(Reduce(device, 26, Metadata(), out Bounds before, out _));
        ulong ticket = BeginPosition(device, 26, new Vector3(100f, 0f, 0f));
        Assert.True(device.CancelVFXUpdateKernels(ticket));

        Assert.True(Reduce(device, 26, Metadata(), out Bounds after, out _));

        AssertVector(before.center, after.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 26);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_AbortedFramePreservesCommittedCache()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(27);
        Assert.True(Reduce(device, 27, Metadata(), out Bounds before, out _));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(27, frame, 0.1f, out _));
        _ = BeginPosition(device, 27, new Vector3(100f, 0f, 0f));
        Assert.True(device.AbortVFXEffectFrame(27, frame));

        Assert.True(Reduce(device, 27, Metadata(), out Bounds after, out _));

        AssertVector(before.center, after.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 27);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_InvalidDescriptorDoesNotTouchBackendCounters()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(28);
        Assert.True(Reduce(device, 28, Metadata(), out _, out _));
        AnityNative.GraphicsVFXUpdateBackendStats before = Stats(device, 28);

        Assert.False(Reduce(
            device, 28, Metadata() with { PositionOffsetWords = 999 },
            out _, out _));

        AnityNative.GraphicsVFXUpdateBackendStats after = Stats(device, 28);
        Assert.Equal(before.boundsDispatchCount, after.boundsDispatchCount);
        Assert.Equal(before.boundsResidentHitCount, after.boundsResidentHitCount);
        Assert.Equal(before.boundsResultCacheHitCount, after.boundsResultCacheHitCount);
    }

    [Fact]
    public void MetalResidentBounds_InvalidResultIsCachedConservatively()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 29, 1, Vector3.zero);
        Assert.True(DispatchPosition(
            device, 29, new Vector3(float.NaN, 0f, 0f)));

        Assert.False(Reduce(device, 29, Metadata(), out _, out _));
        Assert.False(Reduce(device, 29, Metadata(), out _, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 29);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void PendingBounds_CommitPublishesTargetGenerationWithoutStandaloneDispatch()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(40);
        ulong ticket = BeginPositionWithBounds(
            device, 40, new Vector3(7f, 8f, 9f), Metadata());

        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Assert.True(Reduce(
            device, 40, Metadata(), out Bounds bounds, out int backend,
            out ulong generation));

        Assert.Equal(2, backend);
        AssertVector(new Vector3(7f, 8f, 9f), bounds.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 40);
        Assert.Equal(stats.residentGeneration, generation);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsCompletionCount);
        Assert.Equal(0ul, stats.boundsResidentHitCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
        Assert.Equal(1ul, stats.boundsPendingDispatchCount);
        Assert.Equal(1ul, stats.boundsPendingPublishCount);
        Assert.Equal(0ul, stats.boundsPendingDiscardCount);
    }

    [Fact]
    public void PendingBounds_CancelDiscardsResultAndPreservesCommittedCache()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(41);
        Assert.True(Reduce(device, 41, Metadata(), out Bounds before, out _));
        ulong ticket = BeginPositionWithBounds(
            device, 41, new Vector3(100f, 0f, 0f), Metadata());

        Assert.True(device.CancelVFXUpdateKernels(ticket));
        Assert.True(Reduce(device, 41, Metadata(), out Bounds after, out _));

        AssertVector(before.center, after.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 41);
        Assert.Equal(2ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
        Assert.Equal(1ul, stats.boundsPendingDispatchCount);
        Assert.Equal(0ul, stats.boundsPendingPublishCount);
        Assert.Equal(1ul, stats.boundsPendingDiscardCount);
    }

    [Fact]
    public void PendingBounds_AbortDiscardsResultAndRestoresFrameCache()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(42);
        Assert.True(Reduce(device, 42, Metadata(), out Bounds before, out _));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(42, frame, 0.1f, out _));
        _ = BeginPositionWithBounds(
            device, 42, new Vector3(100f, 0f, 0f), Metadata());

        Assert.True(device.AbortVFXEffectFrame(42, frame));
        Assert.True(Reduce(device, 42, Metadata(), out Bounds after, out _));

        AssertVector(before.center, after.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 42);
        Assert.Equal(1ul, stats.boundsPendingDiscardCount);
        Assert.Equal(0ul, stats.boundsPendingPublishCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void PendingBounds_PaddingDescriptorPublishesMatchingCachedExtents()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(43);
        VFXParticleCullingBounds metadata = Padding(1f, 2f, 3f);
        ulong ticket = BeginPositionWithBounds(
            device, 43, new Vector3(4f, 5f, 6f), metadata);

        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Assert.True(Reduce(device, 43, metadata, out Bounds bounds, out _));

        AssertVector(new Vector3(3f, 5f, 7f), bounds.size);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 43);
        Assert.Equal(1ul, stats.boundsPendingPublishCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void PendingBounds_WorldSpaceDescriptorPublishesMatchingCacheKey()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(44);
        VFXParticleCullingBounds metadata = Metadata() with { WorldSpace = true };
        ulong ticket = BeginPositionWithBounds(
            device, 44, new Vector3(4f, 5f, 6f), metadata);

        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Assert.True(Reduce(device, 44, metadata, out Bounds bounds, out _));

        AssertVector(new Vector3(4f, 5f, 6f), bounds.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 44);
        Assert.Equal(1ul, stats.boundsPendingPublishCount);
        Assert.Equal(1ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void PendingBounds_NonFinitePaddingIsRejectedBeforeTicketCreation()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(45);
        VFXParticleCullingBounds invalid = Metadata() with
        {
            AutomaticPadding = new Vector3(float.NaN, 0f, 0f)
        };

        Assert.False(device.BeginVFXUpdateKernels(
            45, new[] { PositionKernel(Vector3.one) },
            new VFXParticleCullingBounds?[] { invalid },
            0.1f, 17, out ulong ticket));

        Assert.Equal(0ul, ticket);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 45);
        Assert.Equal(0ul, stats.boundsPendingDispatchCount);
        Assert.Equal(0ul, stats.boundsPendingPublishCount);
    }

    [Fact]
    public void PendingBounds_DescriptorCountMustMatchKernelCount()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(46);

        Assert.False(device.BeginVFXUpdateKernels(
            46, new[] { PositionKernel(Vector3.one) },
            Array.Empty<VFXParticleCullingBounds?>(),
            0.1f, 17, out ulong ticket));

        Assert.Equal(0ul, ticket);
        Assert.Equal(0ul, Stats(device, 46).boundsPendingDispatchCount);
    }

    [Fact]
    public void PendingBounds_NullDescriptorKeepsLegacyOnDemandReduction()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(47);
        Assert.True(device.BeginVFXUpdateKernels(
            47, new[] { PositionKernel(new Vector3(6f, 7f, 8f)) },
            new VFXParticleCullingBounds?[] { null },
            0.1f, 17, out ulong ticket));

        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Assert.True(Reduce(device, 47, Metadata(), out Bounds bounds, out _));

        AssertVector(new Vector3(6f, 7f, 8f), bounds.center);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 47);
        Assert.Equal(0ul, stats.boundsPendingDispatchCount);
        Assert.Equal(0ul, stats.boundsPendingPublishCount);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(1ul, stats.boundsResidentHitCount);
    }

    [Fact]
    public void PendingBounds_SecondCommitAtomicallyReplacesGenerationCache()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(48);
        ulong first = BeginPositionWithBounds(
            device, 48, new Vector3(2f, 0f, 0f), Metadata());
        Assert.True(device.CompleteVFXUpdateKernels(first));
        Assert.True(Reduce(
            device, 48, Metadata(), out _, out _, out ulong firstGeneration));
        ulong second = BeginPositionWithBounds(
            device, 48, new Vector3(9f, 0f, 0f), Metadata());

        Assert.True(device.CompleteVFXUpdateKernels(second));
        Assert.True(Reduce(
            device, 48, Metadata(), out Bounds bounds, out _,
            out ulong secondGeneration));

        Assert.True(secondGeneration > firstGeneration);
        Assert.Equal(9f, bounds.center.x, 4);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 48);
        Assert.Equal(2ul, stats.boundsPendingDispatchCount);
        Assert.Equal(2ul, stats.boundsPendingPublishCount);
        Assert.Equal(2ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void PendingBounds_InvalidOutputPublishesConservativeCachedResult()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(49);
        ulong ticket = BeginPositionWithBounds(
            device, 49, new Vector3(float.NaN, 0f, 0f), Metadata());

        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Assert.False(Reduce(device, 49, Metadata(), out _, out _));
        Assert.False(Reduce(device, 49, Metadata(), out _, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 49);
        Assert.Equal(1ul, stats.boundsPendingPublishCount);
        Assert.Equal(1ul, stats.boundsDispatchCount);
        Assert.Equal(2ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void PendingBounds_InitializeMutationInvalidatesCacheWithoutUpload()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(50);
        ulong ticket = BeginPositionWithBounds(
            device, 50, new Vector3(1f, 0f, 0f), Metadata());
        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Spawn(device, 50, 2, new Vector3(9f, 0f, 0f));

        Assert.True(Reduce(device, 50, Metadata(), out Bounds bounds, out _));

        Assert.Equal(5f, bounds.center.x, 4);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, 50);
        Assert.Equal(1ul, stats.boundsPendingPublishCount);
        Assert.Equal(2ul, stats.boundsDispatchCount);
        Assert.Equal(0ul, stats.boundsParticleUploadCount);
        Assert.Equal(1ul, stats.residentInitializeCount);
        Assert.Equal(0ul, stats.boundsResultCacheHitCount);
    }

    [Fact]
    public void VisualEffect_UpdateAutomaticallyPublishesPendingBoundsForCulling()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        VFXRuntimeAssetData runtime = RuntimeData(RuntimeSystem()) with
        {
            UpdateKernels = new[]
            {
                PositionKernel(new Vector3(7f, 8f, 9f))
            }
        };
        asset.ImportRuntimeData(runtime.Serialize());
        var gameObject = new GameObject("Pending automatic bounds product path");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Send(effect, device, Vector3.zero);
            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(0.1f, frame, device);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));

            effect.CompleteVfxFrame(device);
            Assert.True(effect.TryGetWorldCullingBounds(device, out Bounds bounds));

            AssertVector(new Vector3(7f, 8f, 9f), bounds.center);
            AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, effectId);
            Assert.Equal(1ul, stats.boundsPendingDispatchCount);
            Assert.Equal(1ul, stats.boundsPendingPublishCount);
            Assert.Equal(1ul, stats.boundsResultCacheHitCount);
            Assert.Equal(0ul, stats.boundsResidentHitCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void VisualEffect_BoundsRetiresInitializeAndUpdateAsOneNativeDependency()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        VFXRuntimeAssetData runtime = RuntimeData(RuntimeSystem()) with
        {
            UpdateKernels = new[]
            {
                PositionKernel(new Vector3(7f, 8f, 9f))
            }
        };
        asset.ImportRuntimeData(runtime.Serialize());
        var gameObject = new GameObject("Initialize update bounds dependency");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Send(effect, device, Vector3.zero);
            Assert.True(DispatchPosition(device, effectId, Vector3.zero));
            using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
            attribute.SetVector3("position", new Vector3(2f, 3f, 4f));
            effect.SendEvent("Spawn", attribute);
            Assert.Equal(1, effect.ProcessInputEvents(
                device, deferInitializeCompletion: true));
            Assert.Equal(1ul, Stats(device, effectId).pendingInitializeCount);
            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(0.1f, frame, device);
            Assert.Equal(1ul, Stats(device, effectId).pendingInitializeCount);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));
            Assert.True(device.CommitVFXEffectFrame(effectId, frame, out _));
            AnityNative.GraphicsVFXUpdateBackendStats published =
                Stats(device, effectId);
            Assert.Equal(1ul, published.pendingInitializeCount);
            Assert.Equal(1ul, published.pendingUpdateCount);

            Assert.True(effect.TryGetWorldCullingBounds(
                device, out Bounds bounds));

            AssertVector(new Vector3(7f, 8f, 9f), bounds.center);
            AnityNative.GraphicsVFXUpdateBackendStats completed =
                Stats(device, effectId);
            Assert.Equal(0ul, completed.pendingInitializeCount);
            Assert.Equal(0ul, completed.pendingUpdateCount);
            Assert.Equal(1ul,
                completed.asynchronousInitializeResidentCompletionCount);
            Assert.Equal(1ul, completed.boundsPendingPublishCount);
            Assert.Equal(1ul, completed.boundsResultCacheHitCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void MetalResidentBounds_ClearEffectRemovesBoundsCacheAndStats()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = ResidentDevice(30);
        Assert.True(Reduce(device, 30, Metadata(), out _, out _));

        Assert.True(device.ClearVFXEffectState(30));

        Assert.False(device.TryGetVFXUpdateBackendStats(
            30, ParticleSystemId, out _));
    }

    [Fact]
    public void CpuBounds_RemainsNativeFallbackWithoutMetalResidentStats()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Spawn(device, 31, 1, Vector3.zero);
        Assert.True(DispatchPosition(device, 31, new Vector3(2f, 3f, 4f)));

        Assert.True(Reduce(device, 31, Metadata(), out Bounds bounds, out int backend));

        Assert.Equal(0, backend);
        AssertVector(new Vector3(2f, 3f, 4f), bounds.center);
        Assert.False(device.TryGetVFXUpdateBackendStats(
            31, ParticleSystemId, out _));
    }

    [Fact]
    public void VisualEffect_MetalCullingUsesResidentGenerationAndCachedResult()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(RuntimeSystem()).Serialize());
        var gameObject = new GameObject("Metal resident automatic bounds");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Send(effect, device, Vector3.zero);
            Assert.True(DispatchPosition(
                device, effectId, new Vector3(3f, 4f, 5f)));

            Assert.True(effect.TryGetWorldCullingBounds(device, out Bounds first));
            Assert.True(effect.TryGetWorldCullingBounds(device, out Bounds second));

            AssertVector(new Vector3(3f, 4f, 5f), first.center);
            AssertVector(first.center, second.center);
            AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, effectId);
            Assert.Equal(1ul, stats.boundsResidentHitCount);
            Assert.Equal(1ul, stats.boundsDispatchCount);
            Assert.Equal(1ul, stats.boundsResultCacheHitCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void Send(
        VisualEffect effect,
        NativeGraphicsDevice device,
        Vector3 position)
    {
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetVector3("position", position);
        effect.SendEvent("Spawn", attribute);
        Assert.Equal(1, effect.ProcessInputEvents(device));
    }

    private static NativeGraphicsDevice ResidentDevice(ulong effectId)
    {
        NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return device;
        Spawn(device, effectId, 1, Vector3.zero);
        Assert.True(DispatchPosition(device, effectId, new Vector3(1f, 2f, 3f)));
        return device;
    }

    private static bool DispatchPosition(
        NativeGraphicsDevice device,
        ulong effectId,
        Vector3 position)
        => device.DispatchVFXUpdateKernels(
            effectId, new[] { PositionKernel(position) }, 0.1f, 17);

    private static ulong BeginPosition(
        NativeGraphicsDevice device,
        ulong effectId,
        Vector3 position)
    {
        Assert.True(device.BeginVFXUpdateKernels(
            effectId, new[] { PositionKernel(position) }, 0.1f, 17,
            out ulong ticket));
        Assert.NotEqual(0ul, ticket);
        return ticket;
    }

    private static ulong BeginPositionWithBounds(
        NativeGraphicsDevice device,
        ulong effectId,
        Vector3 position,
        VFXParticleCullingBounds metadata)
    {
        Assert.True(device.BeginVFXUpdateKernels(
            effectId, new[] { PositionKernel(position) },
            new VFXParticleCullingBounds?[] { metadata },
            0.1f, 17, out ulong ticket));
        Assert.NotEqual(0ul, ticket);
        return ticket;
    }

    private static VFXRuntimeUpdateKernelData PositionKernel(Vector3 position)
        => new(50, "Particles", 8, 8, false, false, 0, -1, new[]
        {
            new VFXRuntimeUpdateOperationData(
                VFXRuntimeUpdateOperationKind.SetAttribute,
                1, -1, -1, -1, -1,
                VFXRuntimeValueType.Float3,
                VFXRuntimeInitializeComposition.Overwrite,
                VFXRuntimeInitializeRandomMode.Off,
                false,
                new[] { Float(position.x), Float(position.y), Float(position.z) },
                Array.Empty<uint>(), Float(1f))
        });

    private static AnityNative.GraphicsVFXUpdateBackendStats Stats(
        NativeGraphicsDevice device,
        ulong effectId)
    {
        Assert.True(device.TryGetVFXUpdateBackendStats(
            effectId, ParticleSystemId, out var stats));
        Assert.Equal(effectId, stats.effectId);
        Assert.Equal(ParticleSystemId, stats.particleSystemId);
        Assert.Equal(2, stats.backendKind);
        return stats;
    }

    private static void Spawn(
        NativeGraphicsDevice device,
        ulong effectId,
        ulong sequence,
        Vector3 position,
        float size = 1f,
        Vector3? scale = null,
        bool usesDeadList = false)
    {
        VFXRuntimeInitializeKernelData kernel = Kernel(
            size, scale ?? Vector3.one, includeAlive: true,
            usesDeadList: usesDeadList);
        Assert.True(Submit(device, effectId, sequence, position, kernel));
    }

    private static bool Submit(
        NativeGraphicsDevice device,
        ulong effectId,
        ulong sequence,
        Vector3 position,
        VFXRuntimeInitializeKernelData kernel)
    {
        byte[] source = Words(Float(position.x), Float(position.y), Float(position.z));
        var desc = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = effectId,
            sequence = sequence,
            initializeContextId = 40,
            sourceSpawnerContextId = 30,
            eventNameId = 12,
            particleSystemId = ParticleSystemId,
            spawnSystemId = 13,
            startEventIndex = 0,
            recordCount = 1,
            strideBytes = source.Length
        };
        return device.SubmitVFXInitializeKernels(
            new[] { desc }, new VFXRuntimeInitializeKernelData?[] { kernel }, source, 17);
    }

    private static VFXRuntimeInitializeKernelData Kernel(
        float size = 1f,
        Vector3? scale = null,
        bool includeAlive = true,
        bool aliveDefault = true,
        bool usesDeadList = false)
    {
        Vector3 resolvedScale = scale ?? Vector3.one;
        var attributes = new List<VFXRuntimeInitializeAttributeData>();
        if (includeAlive)
            attributes.Add(Attribute("alive", VFXRuntimeValueType.Boolean, aliveDefault ? 1u : 0u));
        attributes.Add(Attribute(
            "position", VFXRuntimeValueType.Float3, Float(0f), Float(0f), Float(0f)));
        attributes.Add(Attribute("size", VFXRuntimeValueType.Float, Float(size)));
        attributes.Add(Attribute("scaleX", VFXRuntimeValueType.Float, Float(resolvedScale.x)));
        attributes.Add(Attribute("scaleY", VFXRuntimeValueType.Float, Float(resolvedScale.y)));
        attributes.Add(Attribute("scaleZ", VFXRuntimeValueType.Float, Float(resolvedScale.z)));
        int offset = 0;
        var packed = new List<VFXRuntimeInitializeAttributeData>();
        foreach (VFXRuntimeInitializeAttributeData attribute in attributes)
        {
            packed.Add(attribute with { Layout = attribute.Layout with { OffsetWords = offset } });
            offset += attribute.Layout.SizeWords;
        }
        int positionOffset = includeAlive ? 1 : 0;
        var sourcePosition = new VFXRuntimeInitializeOperationData(
            positionOffset, 0, VFXRuntimeValueType.Float3,
            VFXRuntimeInitializeValueSource.Source,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            Array.Empty<uint>(), Array.Empty<uint>(), Float(1f));
        return new VFXRuntimeInitializeKernelData(
            40, 8, offset, 3, usesDeadList, packed,
            new[] { sourcePosition }, -1);
    }

    private static VFXRuntimeInitializeAttributeData Attribute(
        string name,
        VFXRuntimeValueType type,
        params uint[] defaults)
        => new(
            new VFXRuntimeAttributeData(name, type, 0, defaults.Length),
            defaults);

    private static VFXParticleCullingBounds Metadata()
        => new(default, false, true, false, Vector3.zero, 1, 0, 4, 5, 6, 7);

    private static VFXParticleCullingBounds Padding(float x, float y, float z)
        => Metadata() with { AutomaticPadding = new Vector3(x, y, z) };

    private static bool Reduce(
        NativeGraphicsDevice device,
        ulong effectId,
        VFXParticleCullingBounds metadata,
        out Bounds bounds,
        out int backend)
        => Reduce(device, effectId, metadata, out bounds, out backend, out _);

    private static bool Reduce(
        NativeGraphicsDevice device,
        ulong effectId,
        VFXParticleCullingBounds metadata,
        out Bounds bounds,
        out int backend,
        out ulong generation)
        => device.TryReduceVFXParticleBounds(
            effectId, ParticleSystemId, metadata,
            out bounds, out backend, out generation);

    private static VFXRuntimeSystemData RuntimeSystem(bool worldSpace = false)
        => new("Particles", VFXRuntimeSystemKind.Particle, 8)
        {
            HasAutomaticBounds = true,
            BoundsInWorldSpace = worldSpace,
            PositionOffsetWords = 1,
            AliveOffsetWords = 0,
            SizeOffsetWords = 4,
            ScaleXOffsetWords = 5,
            ScaleYOffsetWords = 6,
            ScaleZOffsetWords = 7,
            AutomaticBoundsPaddingX = 1f,
            AutomaticBoundsPaddingY = 2f,
            AutomaticBoundsPaddingZ = 3f
        };

    private static VFXRuntimeAssetData RuntimeData(VFXRuntimeSystemData system)
    {
        VFXRuntimeInitializeKernelData kernel = Kernel();
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        return new VFXRuntimeAssetData(
            new[]
            {
                new VFXRuntimeAttributeData("position", VFXRuntimeValueType.Float3, 0, 3)
            },
            new[] { "Spawn" }, new[] { input }, new[] { system },
            Array.Empty<VFXRuntimeOutputEventData>());
    }

    private static NativeGraphicsDevice CreateDevice(GraphicsDeviceType type)
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(type, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static byte[] Words(params uint[] words)
    {
        var bytes = new byte[words.Length * sizeof(uint)];
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(index * sizeof(uint), sizeof(uint)), words[index]);
        return bytes;
    }

    private static uint Float(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.x, actual.x, 4);
        Assert.Equal(expected.y, actual.y, 4);
        Assert.Equal(expected.z, actual.z, 4);
    }

    private static readonly int ParticleSystemId = Shader.PropertyToID("Particles");
}
