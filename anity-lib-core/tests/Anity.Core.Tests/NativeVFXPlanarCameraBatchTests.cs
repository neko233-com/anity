using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXPlanarCameraBatchTests
{
    private const string SystemName = "BatchPlanarParticles";
    private const int Capacity = 2;
    private const int StrideWords = 28;

    [Fact]
    public void CameraBatchAbiSizesMatchNativeContract()
    {
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarEffectDesc>());
        Assert.Equal(88, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarCameraBatchDesc>());
        Assert.Equal(160, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarCameraDrawInfo>());
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarSubmissionStats>());
    }

    [Theory]
    [InlineData("Background", 1000)]
    [InlineData("Geometry+12", 2012)]
    [InlineData("AlphaTest-7", 2443)]
    [InlineData("GeometryLast", 2500)]
    [InlineData("Transparent", 3000)]
    [InlineData("Overlay+100", 4100)]
    public void UnityRenderQueueNamesMapToNumericQueues(string value, int expected)
        => Assert.Equal(expected, NativeGraphicsDevice.ParseVFXRenderQueue(value));

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("Transparent+")]
    [InlineData("Overlay+1001")]
    public void MalformedRenderQueuesAreRejected(string value)
        => Assert.Throws<FormatException>(() => NativeGraphicsDevice.ParseVFXRenderQueue(value));

    [Fact]
    public void EmptyCameraBatchIsValidOnNullBackend()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, Array.Empty<AnityNative.GraphicsVFXPlanarEffectDesc>(), out var info));
            Assert.Equal(0, info.effectCount);
            Assert.Equal(0, info.outputCount);
            Assert.Equal(0, info.commandBufferCount);
        });

    [Fact]
    public void DuplicateEffectIdentityIsRejectedTransactionally()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Set(device, 0x801, Desc(0x801, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            var effect = Effect(0x801, layer: 0, sortOrder: 0);
            Assert.Equal(AnityNative.Result.InvalidArg,
                Draw(device, ref camera, new[] { effect, effect }, out _));
        });

    [Fact]
    public void NonFiniteEffectMatrixIsRejected()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Set(device, 0x802, Desc(0x802, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            AnityNative.GraphicsVFXPlanarEffectDesc effect = Effect(0x802, 0, 0);
            effect.localToWorld00 = float.NaN;
            Assert.Equal(AnityNative.Result.InvalidArg,
                Draw(device, ref camera, new[] { effect }, out _));
        });

    [Fact]
    public void InvalidLayerIsRejectedBeforeRegistrySnapshot()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            Assert.Equal(AnityNative.Result.InvalidArg,
                Draw(device, ref camera, new[] { Effect(0x803, 32, 0) }, out _));
        });

    [Fact]
    public void MissingInstalledEffectIsRejected()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            Assert.Equal(AnityNative.Result.InvalidArg,
                Draw(device, ref camera, new[] { Effect(0x804, 0, 0) }, out _));
        });

    [Fact]
    public void NativeOutputQueueRangeIsValidated()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarOutputDesc output = Desc(0x805, 10);
            output.renderQueue = 5001;
            Assert.Equal(AnityNative.Result.InvalidArg,
                AnityNative.Graphics_SetVFXPlanarOutputs(device.Handle, 0x805, new[] { output }, 1));
        });

    [Fact]
    public void UnusedInternalZTestCodeIsRejectedTransactionally()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarOutputDesc output = Desc(0x8052, 10, zTest: 7);
            Assert.Equal(AnityNative.Result.InvalidArg,
                AnityNative.Graphics_SetVFXPlanarOutputs(
                    device.Handle, 0x8052, new[] { output }, 1));
            Assert.False(device.TryGetVFXPlanarOutputCount(0x8052, out _));
        });

    [Fact]
    public void SharedParticleSystemRejectsConflictingAliveLayout()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarOutputDesc first = Desc(0x8051, 10);
            AnityNative.GraphicsVFXPlanarOutputDesc second = Desc(0x8051, 11);
            Set(device, 0x8051, first);
            second.aliveOffsetBytes = sizeof(uint);
            Assert.Equal(AnityNative.Result.InvalidArg,
                AnityNative.Graphics_SetVFXPlanarOutputs(
                    device.Handle, 0x8051, new[] { first, second }, 2));
            Assert.True(device.TryGetVFXPlanarOutputCount(0x8051, out int count));
            Assert.Equal(1, count);
        });

    [Fact]
    public void NullBackendAggregatesVisibleEffectsAndFiltersLayers()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Set(device, 0x806, Desc(0x806, 10));
            Set(device, 0x807, Desc(0x807, 11), Desc(0x807, 12));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false, cullingMask: 1 << 1);
            var effects = new[] { Effect(0x806, 1, 0), Effect(0x807, 2, 1) };
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var info));
            Assert.Equal(1, info.effectCount);
            Assert.Equal(1, info.outputCount);
            Assert.Equal(1, info.skippedOutputCount);
            Assert.True(info.submissionGeneration > 0);
        });

    [Fact]
    public void MetalSubmitsTwoEffectsInOneCommandBufferAndRenderPass()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x808, 1, 0, 0);
            SpawnResident(device, 0x809, 0, 1, 0);
            Set(device, 0x808, Desc(0x808, 10));
            Set(device, 0x809, Desc(0x809, 11));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x808, 0, 0), Effect(0x809, 0, 1) }, out var info));
            Assert.Equal(2, info.drawCount);
            Assert.Equal(2, info.particleCount);
            Assert.Equal(12, info.vertexCount);
            Assert.Equal(1, info.commandBufferCount);
            Assert.Equal(1, info.renderPassCount);
            Assert.Equal(2, info.backendKind);
        });

    [Fact]
    public void RenderQueueOrdersOpaquePlanarOutputsAcrossEffects()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x80A, 1, 0, 0);
            SpawnResident(device, 0x80B, 0, 1, 0);
            Set(device, 0x80A, Desc(0x80A, 10, renderQueue: 3100));
            Set(device, 0x80B, Desc(0x80B, 11, renderQueue: 2900));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x80A, 0, 0), Effect(0x80B, 0, 1) }, out _));
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void SortOrderBreaksEqualQueueTiesBeforeEffectIdentity()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x80C, 1, 0, 0);
            SpawnResident(device, 0x80D, 0, 1, 0);
            Set(device, 0x80C, Desc(0x80C, 10));
            Set(device, 0x80D, Desc(0x80D, 11));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x80C, 0, 9), Effect(0x80D, 0, -4) }, out _));
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void PerEffectTransformsArePreservedInsideCameraBatch()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x80E, 1, 0, 0);
            SpawnResident(device, 0x80F, 0, 1, 0);
            Set(device, 0x80E, Desc(0x80E, 10));
            Set(device, 0x80F, Desc(0x80F, 11));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            var left = Effect(0x80E, 0, 0); left.localToWorld03 = -0.5f;
            var right = Effect(0x80F, 0, 1); right.localToWorld03 = 0.5f;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, new[] { left, right }, out _));
            AssertPixel(Read(device), 16, 32, 255, 0, 0, 255);
            AssertPixel(Read(device), 48, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void FirstMetalDrawBuildsStableAliveCompactionAndIndirectArguments()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x810, 1, 0, 0);
            Set(device, 0x810, Desc(0x810, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x810, 0, 0) }, out var info));
            Assert.Equal(1, info.aliveCompactionCount);
            Assert.Equal(0, info.aliveCompactionCacheHitCount);
            Assert.Equal(1, info.alivePrefixPassCount);
            Assert.Equal(1, info.indirectArgumentCount);
        });

    [Fact]
    public void SecondMetalDrawReusesResidentGenerationCompaction()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x811, 1, 0, 0);
            Set(device, 0x811, Desc(0x811, 10));
            var effects = new[] { Effect(0x811, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var cached));
            Assert.Equal(0, cached.aliveCompactionCount);
            Assert.Equal(1, cached.aliveCompactionCacheHitCount);
            Assert.Equal(0, cached.alivePrefixPassCount);
            Assert.Equal(1, cached.indirectArgumentCount);
        });

    [Fact]
    public void TwoOutputsSharingParticleSystemCompactOnlyOnce()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x812, 1, 0, 0);
            Set(device, 0x812, Desc(0x812, 10), Desc(0x812, 11));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x812, 0, 0) }, out var info));
            Assert.Equal(2, info.drawCount);
            Assert.Equal(1, info.aliveCompactionCount);
            Assert.Equal(1, info.aliveCompactionCacheHitCount);
            Assert.Equal(2, info.indirectArgumentCount);
        });

    [Fact]
    public void PublishingNewResidentGenerationInvalidatesCompactionCache()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x813, 1, 0, 0);
            Set(device, 0x813, Desc(0x813, 10));
            var effects = new[] { Effect(0x813, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            PublishResident(device, 0x813, usesDeadList: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var next));
            Assert.Equal(1, next.aliveCompactionCount);
            Assert.Equal(0, next.aliveCompactionCacheHitCount);
        });

    [Fact]
    public void SparsePhysicalParticleRendersThroughCompactedIndex()
        => WithMetal(device =>
        {
            SpawnSparseSecondParticle(device, 0x814);
            Set(device, 0x814, Desc(0x814, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x814, 0, 0) }, out var info));
            Assert.Equal(1, info.particleCount);
            Assert.Equal(6, info.vertexCount);
            AssertPixel(Read(device), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void CapacityVertexCountReportsAvoidedSubmission()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x815, 1, 0, 0);
            Set(device, 0x815, Desc(0x815, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x815, 0, 0) }, out var info));
            Assert.Equal(12ul, info.capacityVertexCount);
            Assert.Equal(6, info.vertexCount);
        });

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 6)]
    [InlineData(2, 18)]
    public void IndirectArgumentsSupportEveryPlanarPrimitive(int primitive, int vertices)
        => WithMetal(device =>
        {
            ulong effectId = 0x816ul + (uint)primitive;
            SpawnResident(device, effectId, 0, 0, 1);
            Set(device, effectId, Desc(effectId, 10, primitiveType: primitive));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(vertices, info.vertexCount);
            Assert.Equal(1, info.indirectArgumentCount);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EveryCompiledPlanarZTestCreatesMetalDepthState(int zTest)
        => WithMetal(device =>
        {
            ulong effectId = 0x820ul + (uint)zTest;
            SpawnResident(device, effectId, 0, 0, 1);
            Set(device, effectId, Desc(effectId, 10, zTest: zTest));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(effectId, 0.5f) }, out var info));
            Assert.Equal(1, info.drawCount);
            Assert.Equal(1, info.depthStateChangeCount);
            Assert.Equal(zTest == 6 ? 0 : 1, info.depthTestOutputCount);
            Assert.Equal(1, info.depthClearCount);
        });

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void ZWriteModeIsSubmittedAndReported(int zWrite, int expectedWrites)
        => WithMetal(device =>
        {
            ulong effectId = 0x830ul + (uint)zWrite;
            SpawnResident(device, effectId, 0, 0, 1);
            Set(device, effectId, Desc(effectId, 10, zTest: 6, zWrite: zWrite));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(effectId, 0.5f) }, out var info));
            Assert.Equal(expectedWrites, info.depthWriteOutputCount);
            Assert.Equal(1, info.depthStateChangeCount);
        });

    [Fact]
    public void ExplicitIndirectOutputUsesImplementedGpuPath()
        => WithMetal(device =>
        {
            const ulong effectId = 0x840;
            SpawnResident(device, effectId, 0, 0, 1);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 8u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(1, info.drawCount);
            Assert.Equal(1, info.indirectArgumentCount);
            Assert.Equal(0, info.skippedOutputCount);
        });

    [Fact]
    public void FirstSortingOutputBuildsGpuStableSortStages()
        => WithMetal(device =>
        {
            const ulong effectId = 0x841;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(1, info.sortedOutputCount);
            Assert.Equal(0, info.sortCacheHitCount);
            Assert.Equal(1, info.sortMapDispatchCount);
            Assert.Equal(1, info.sortStageDispatchCount);
            Assert.Equal(1, info.sortExtractDispatchCount);
            Assert.Equal(2, info.sortPaddedParticleCount);
            Assert.Equal(1, info.sortCacheInsertCount);
            Assert.Equal(0, info.sortCacheEvictionCount);
            Assert.Equal(1, info.sortCacheEntryCount);
            Assert.Equal(4, info.sortCacheCapacityPerSystem);
            Assert.NotEqual(0ul, info.submissionId);
            Assert.Equal(1, info.asyncSubmissionCount);
            Assert.Equal(0, info.synchronousWaitCount);
        });

    [Fact]
    public void CameraDrawReturnsObservableAsyncSubmissionIdentity()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8412;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.NotEqual(0ul, info.submissionId);
            Assert.Equal(1, info.asyncSubmissionCount);
            Assert.Equal(0, info.synchronousWaitCount);
            AnityNative.GraphicsVFXPlanarSubmissionStats stats = SubmissionStats(device);
            Assert.Equal(2, stats.backendKind);
            Assert.True(stats.submissionCount >= 1);
            Assert.True(stats.lastSubmittedId >= info.submissionId);
            Assert.True(stats.maxInFlightCount >= 1);
        });

    [Fact]
    public void CameraConsumesCommittedInFlightUpdateThroughMetalQueueDependency()
        => WithMetal(device =>
        {
            const ulong effectId = 0x84121;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            Assert.True(device.BeginVFXFrame(out uint frame));
            Assert.True(device.PrepareVFXEffectManualFrame(
                effectId, frame, 0.1f, out _));
            var operation = new VFXRuntimeUpdateOperationData(
                VFXRuntimeUpdateOperationKind.SetAttribute,
                1, -1, -1, -1, -1,
                VFXRuntimeValueType.Float3,
                VFXRuntimeInitializeComposition.Overwrite,
                VFXRuntimeInitializeRandomMode.Off,
                false, new[] { F(0.25f), F(0), F(0) },
                Array.Empty<uint>(), F(1));
            var update = new VFXRuntimeUpdateKernelData(
                52, SystemName, Capacity, StrideWords, true, false, 0, 27,
                new[] { operation });
            Assert.True(device.BeginVFXUpdateKernels(
                effectId, new[] { update }, 0.1f, 17, out ulong ticket));
            Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out var submitted));
            Assert.True(device.CommitVFXEffectFrame(effectId, frame, out _));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);

            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var draw));

            Assert.Equal(submitted.submitGeneration, draw.residentGeneration);
            Assert.True(device.TryGetVFXUpdateBackendStats(
                effectId, Shader.PropertyToID(SystemName), out var stats));
            Assert.Equal(1ul, stats.asynchronousResidentPublishCount);
            Assert.Equal(1ul, stats.cameraDependencyCount);
            Assert.Equal(1ul, stats.pendingUpdateCount);
            Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out _));
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(draw.submissionId));
            Assert.True(device.CompleteVFXUpdateKernels(ticket));
        });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void SingleEffectDrawsEveryResidentParticleSystemInOneCameraSubmission(
        int systemCount)
        => WithMetal(device =>
        {
            const ulong effectId = 0x84122;
            var outputs = new List<AnityNative.GraphicsVFXPlanarOutputDesc>();
            var systemNames = new List<string>();
            for (int index = 0; index < systemCount; ++index)
            {
                string systemName = $"{SystemName}_{index}";
                systemNames.Add(systemName);
                SpawnResident(
                    device, effectId, index == 0 ? 1 : 0,
                    index == 1 ? 1 : 0, index >= 2 ? 1 : 0,
                    systemName: systemName,
                    initializeContextId: 41 + index,
                    updateContextId: 51 + index);
                outputs.Add(Desc(
                    effectId, 100 + index, renderQueue: 3000 + index,
                    systemName: systemName));
            }
            Set(device, effectId, outputs.ToArray());
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);

            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var draw));

            Assert.Equal(1, draw.effectCount);
            Assert.Equal(systemCount, draw.outputCount);
            Assert.Equal(systemCount, draw.drawCount);
            Assert.Equal(systemCount, draw.particleCount);
            Assert.Equal(systemCount, draw.indirectArgumentCount);
            Assert.Equal(0, draw.skippedOutputCount);
            Assert.Equal(1, draw.commandBufferCount);
            Assert.Equal(1, draw.renderPassCount);
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(draw.submissionId));
            foreach (string systemName in systemNames)
            {
                Assert.True(device.TryGetVFXUpdateBackendStats(
                    effectId, Shader.PropertyToID(systemName), out var stats));
                Assert.Equal(2, stats.backendKind);
                Assert.NotEqual(0ul, stats.residentGeneration);
            }
        });

    [Fact]
    public void CameraSubmissionIdsIncreaseMonotonically()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8413;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var first));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var second));
            Assert.True(second.submissionId > first.submissionId);
            Assert.Equal(1, second.asyncSubmissionCount);
        });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void CameraCommandFailure_IsReportedPerSubmissionAndNextCameraRecovers(
        int effectCount)
        => WithMetal(device =>
        {
            var effects = new List<AnityNative.GraphicsVFXPlanarEffectDesc>();
            for (int index = 0; index < effectCount; ++index)
            {
                ulong effectId = 0x8700ul + (uint)index;
                SpawnResident(device, effectId, 1, 0, 0);
                Set(device, effectId, Desc(effectId, 10 + index));
                effects.Add(Effect(effectId, index % 4, index));
            }
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.True(device.InjectVFXFailure(
                AnityNative.GraphicsVFXFailurePoint.PlanarCameraCommand));

            Assert.Equal(AnityNative.Result.Ok, Draw(
                device, ref camera, effects.ToArray(), out var failed));
            Assert.Equal(AnityNative.Result.DeviceLost,
                device.WaitForVFXPlanarSubmissions(failed.submissionId));
            AnityNative.GraphicsVFXPlanarSubmissionStats failedStats =
                SubmissionStats(device);
            Assert.Equal(1ul, failedStats.failureCount);
            Assert.Equal(failed.submissionId, failedStats.lastFailedId);
            Assert.Equal(0, failedStats.deviceLost);
            Assert.Equal(0, failedStats.inFlightCount);

            Assert.Equal(AnityNative.Result.Ok, Draw(
                device, ref camera, effects.ToArray(), out var recovered));
            Assert.True(recovered.submissionId > failed.submissionId);
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(recovered.submissionId));
            AnityNative.GraphicsVFXPlanarSubmissionStats recoveredStats =
                SubmissionStats(device);
            Assert.Equal(2ul, recoveredStats.completionCount);
            Assert.Equal(1ul, recoveredStats.failureCount);
            Assert.Equal(0, recoveredStats.deviceLost);
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    [Theory]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(1026)]
    [InlineData(1027)]
    [InlineData(1028)]
    [InlineData(1029)]
    [InlineData(1030)]
    [InlineData(1031)]
    [InlineData(1032)]
    [InlineData(1033)]
    public void EvictedSubmissionFailureNeverDegradesIntoFalseSuccess(
        int additionalSubmissionCount)
        => WithMetal(device =>
        {
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            var noEffects = Array.Empty<AnityNative.GraphicsVFXPlanarEffectDesc>();
            Assert.True(device.InjectVFXFailure(
                AnityNative.GraphicsVFXFailurePoint.PlanarCameraCommand));
            Assert.Equal(AnityNative.Result.Ok,
                Draw(device, ref camera, noEffects, out var failed));
            Assert.Equal(AnityNative.Result.DeviceLost,
                device.WaitForVFXPlanarSubmissions(failed.submissionId));

            AnityNative.GraphicsVFXPlanarCameraDrawInfo latest = default;
            for (int index = 0; index < additionalSubmissionCount; ++index)
                Assert.Equal(AnityNative.Result.Ok,
                    Draw(device, ref camera, noEffects, out latest));
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(latest.submissionId));

            AnityNative.GraphicsVFXPlanarSubmissionStats stats =
                SubmissionStats(device);
            Assert.Equal((ulong)additionalSubmissionCount + 1, stats.submissionCount);
            Assert.Equal(stats.submissionCount, stats.completionCount);
            Assert.Equal(1ul, stats.failureCount);
            Assert.Equal(failed.submissionId, stats.lastFailedId);
            Assert.Equal((ulong)(additionalSubmissionCount - 1023),
                stats.resultEvictionCount);
            Assert.Equal(0, stats.deviceLost);
            Assert.Equal(0, stats.inFlightCount);
            Assert.Equal(AnityNative.Result.InvalidArg,
                device.WaitForVFXPlanarSubmissions(failed.submissionId, 0));
        });

    [Fact]
    public void CameraDeviceRemoval_CompletesSubmittedWorkThenPoisonsDevice()
        => WithMetal(device =>
        {
            const ulong effectId = 0x87FF;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.True(device.InjectVFXFailure(
                AnityNative.GraphicsVFXFailurePoint.DeviceRemoval));

            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var submitted));
            Assert.Equal(AnityNative.Result.DeviceLost,
                device.WaitForVFXPlanarSubmissions(submitted.submissionId));

            AnityNative.GraphicsVFXPlanarSubmissionStats stats =
                SubmissionStats(device);
            Assert.Equal(1, stats.deviceLost);
            Assert.Equal(1ul, stats.failureCount);
            Assert.Equal(0, stats.inFlightCount);
            Assert.Equal(AnityNative.Result.DeviceLost, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out _));
        });

    [Fact]
    public void WaitThroughSubmissionCompletesExactFence()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8414;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(info.submissionId));
            AnityNative.GraphicsVFXPlanarSubmissionStats stats = SubmissionStats(device);
            Assert.True(stats.lastCompletedId >= info.submissionId);
            Assert.Equal(stats.submissionCount, stats.completionCount);
            Assert.Equal(0, stats.inFlightCount);
            Assert.Equal(0ul, stats.failureCount);
        });

    [Fact]
    public void ZeroSubmissionFenceTargetsLatestSubmittedCamera()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8415;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var latest));
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(0));
            AnityNative.GraphicsVFXPlanarSubmissionStats stats = SubmissionStats(device);
            Assert.Equal(latest.submissionId, stats.lastSubmittedId);
            Assert.Equal(latest.submissionId, stats.lastCompletedId);
            Assert.True(stats.waitCount >= 1);
        });

    [Fact]
    public void ZeroTimeoutPollIsNonBlockingAndCanBeCompletedLater()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8416;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            AnityNative.Result poll =
                device.WaitForVFXPlanarSubmissions(info.submissionId, 0);
            Assert.True(poll is AnityNative.Result.Ok or AnityNative.Result.Timeout);
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(info.submissionId));
        });

    [Fact]
    public void FutureSubmissionFenceIsRejected()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8417;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(AnityNative.Result.InvalidArg,
                device.WaitForVFXPlanarSubmissions(info.submissionId + 1, 0));
        });

    [Fact]
    public void TimeoutBelowInfiniteSentinelIsRejected()
        => WithMetal(device =>
        {
            Assert.Equal(AnityNative.Result.InvalidArg,
                device.WaitForVFXPlanarSubmissions(0, -2));
        });

    [Fact]
    public void NullBackendReportsNoAsyncCameraSubmissions()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.True(device.TryGetVFXPlanarSubmissionStats(out var stats));
            Assert.Equal(0, stats.backendKind);
            Assert.Equal(0ul, stats.submissionCount);
            Assert.Equal(0ul, stats.completionCount);
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(0, 0));
            Assert.Equal(AnityNative.Result.InvalidArg,
                device.WaitForVFXPlanarSubmissions(1, 0));
        });

    [Fact]
    public void SwapchainReadbackWaitsForLatestCameraSubmission()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8418;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
            AnityNative.GraphicsVFXPlanarSubmissionStats stats = SubmissionStats(device);
            Assert.True(stats.lastCompletedId >= info.submissionId);
            Assert.Equal(0, stats.inFlightCount);
            Assert.True(stats.waitCount >= 1);
        });

    [Fact]
    public void QueuedProjectionSubmissionsPreserveOrderBeforeReadbackFence()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8419;
            SpawnTwoAlphaParticles(device, effectId, equalDepth: false);
            Set(device, effectId, Desc(
                effectId, 10, blendMode: 1, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc normal = Camera(clear: true);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc reversed = normal;
            reversed.cameraId++;
            reversed.worldToClip22 = -1;
            reversed.worldToClip23 = 1;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref normal, effects, out var first));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref reversed, effects, out var second));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref normal, effects, out var last));
            Assert.True(first.submissionId < second.submissionId);
            Assert.True(second.submissionId < last.submissionId);
            AssertPixelDominance(Read(device), redDominant: true);
            AnityNative.GraphicsVFXPlanarSubmissionStats stats = SubmissionStats(device);
            Assert.Equal(last.submissionId, stats.lastCompletedId);
            Assert.Equal(0ul, stats.failureCount);
        });

    [Fact]
    public void ExplicitWaitAndReadbackAreBothCountedAsFences()
        => WithMetal(device =>
        {
            const ulong effectId = 0x841A;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(AnityNative.Result.Ok,
                device.WaitForVFXPlanarSubmissions(info.submissionId));
            _ = Read(device);
            Assert.True(SubmissionStats(device).waitCount >= 2);
        });

    [Fact]
    public void DeviceDisposeWaitsForQueuedCameraSubmissions()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 64, 64, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        if (device.Handle == IntPtr.Zero)
        {
            device.Dispose();
            return;
        }
        Assert.True(device.CreateSwapchain(64, 64, imageCount: 3, hdr: false));
        const ulong effectId = 0x841B;
        SpawnResident(device, effectId, 1, 0, 0);
        Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
        var effects = new[] { Effect(effectId, 0, 0) };
        AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
        for (int index = 0; index < 32; ++index)
        {
            camera.cameraId = (ulong)(700 + index);
            Assert.Equal(AnityNative.Result.Ok,
                Draw(device, ref camera, effects, out _));
        }
        Assert.Equal(32ul, SubmissionStats(device).submissionCount);
        device.Dispose();
        Assert.Equal(IntPtr.Zero, device.Handle);
    }

    [Fact]
    public void NonPowerOfTwoCapacitySortsThroughPaddedBitonicDomain()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8411;
            SpawnAlphaParticles(device, effectId, new[]
            {
                (Depth: 0.1f, Red: 1f, Green: 0f, Blue: 0f),
                (Depth: 0.9f, Red: 0f, Green: 1f, Blue: 0f),
                (Depth: 0.5f, Red: 0f, Green: 0f, Blue: 1f)
            });
            Set(device, effectId, Desc(
                effectId, 10, particleCapacity: 3, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(3, info.particleCount);
            Assert.Equal(18, info.vertexCount);
            Assert.Equal(4, info.sortPaddedParticleCount);
            Assert.Equal(3, info.sortStageDispatchCount);
            Assert.Equal(1, info.sortExtractDispatchCount);
        });

    [Fact]
    public void RepeatedCameraSortReusesGenerationAndMatrixCache()
        => WithMetal(device =>
        {
            const ulong effectId = 0x842;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var cached));
            Assert.Equal(1, cached.sortedOutputCount);
            Assert.Equal(1, cached.sortCacheHitCount);
            Assert.Equal(0, cached.sortMapDispatchCount);
            Assert.Equal(0, cached.sortStageDispatchCount);
            Assert.Equal(0, cached.sortExtractDispatchCount);
            Assert.Equal(0, cached.sortCacheInsertCount);
            Assert.Equal(1, cached.sortCacheEntryCount);
            Assert.Equal(4, cached.sortCacheCapacityPerSystem);
        });

    [Fact]
    public void CameraIdentityChangeAddsIndependentSortCacheEntry()
        => WithMetal(device =>
        {
            const ulong effectId = 0x843;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            camera.cameraId++;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var changed));
            Assert.Equal(0, changed.sortCacheHitCount);
            Assert.Equal(1, changed.sortMapDispatchCount);
            Assert.Equal(1, changed.sortCacheInsertCount);
            Assert.Equal(0, changed.sortCacheEvictionCount);
            Assert.Equal(2, changed.sortCacheEntryCount);
        });

    [Fact]
    public void CameraProjectionChangeAddsIndependentSortCacheEntry()
        => WithMetal(device =>
        {
            const ulong effectId = 0x844;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            camera.worldToClip22 = -1;
            camera.worldToClip23 = 1;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var changed));
            Assert.Equal(0, changed.sortCacheHitCount);
            Assert.Equal(1, changed.sortMapDispatchCount);
            Assert.Equal(2, changed.sortCacheEntryCount);
        });

    [Fact]
    public void AlternatingCamerasReuseIndependentSortEntries()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8441;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc cameraA = Camera(clear: true);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc cameraB = cameraA;
            cameraB.cameraId++;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref cameraA, effects, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref cameraB, effects, out var second));
            Assert.Equal(2, second.sortCacheEntryCount);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref cameraA, effects, out var reused));
            Assert.Equal(1, reused.sortCacheHitCount);
            Assert.Equal(0, reused.sortMapDispatchCount);
            Assert.Equal(0, reused.sortCacheInsertCount);
            Assert.Equal(2, reused.sortCacheEntryCount);
        });

    [Fact]
    public void FourCameraWorkingSetFitsWithoutEviction()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8442;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            for (int index = 0; index < 4; ++index)
            {
                AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
                camera.cameraId = (ulong)(100 + index);
                Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var info));
                Assert.Equal(1, info.sortCacheInsertCount);
                Assert.Equal(0, info.sortCacheEvictionCount);
                Assert.Equal(index + 1, info.sortCacheEntryCount);
                Assert.Equal(4, info.sortCacheCapacityPerSystem);
            }
        });

    [Fact]
    public void FifthCameraEvictsExactlyOneSortEntry()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8443;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraDrawInfo last = default;
            for (int index = 0; index < 5; ++index)
            {
                AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
                camera.cameraId = (ulong)(200 + index);
                Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out last));
            }
            Assert.Equal(1, last.sortCacheInsertCount);
            Assert.Equal(1, last.sortCacheEvictionCount);
            Assert.Equal(4, last.sortCacheEntryCount);
        });

    [Fact]
    public void RecentlyTouchedCameraIsProtectedFromLruEviction()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8444;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            for (ulong cameraId = 300; cameraId < 304; ++cameraId)
            {
                AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
                camera.cameraId = cameraId;
                Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            }
            AnityNative.GraphicsVFXPlanarCameraBatchDesc protectedCamera = Camera(clear: true);
            protectedCamera.cameraId = 300;
            Assert.Equal(AnityNative.Result.Ok,
                Draw(device, ref protectedCamera, effects, out var touched));
            Assert.Equal(1, touched.sortCacheHitCount);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc fifth = Camera(clear: true);
            fifth.cameraId = 304;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref fifth, effects, out var inserted));
            Assert.Equal(1, inserted.sortCacheEvictionCount);
            Assert.Equal(AnityNative.Result.Ok,
                Draw(device, ref protectedCamera, effects, out var retained));
            Assert.Equal(1, retained.sortCacheHitCount);
            Assert.Equal(0, retained.sortMapDispatchCount);
        });

    [Fact]
    public void EvictedLeastRecentlyUsedCameraIsRebuilt()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8445;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            for (ulong cameraId = 400; cameraId < 405; ++cameraId)
            {
                AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
                camera.cameraId = cameraId;
                Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            }
            AnityNative.GraphicsVFXPlanarCameraBatchDesc evicted = Camera(clear: true);
            evicted.cameraId = 400;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref evicted, effects, out var rebuilt));
            Assert.Equal(0, rebuilt.sortCacheHitCount);
            Assert.Equal(1, rebuilt.sortMapDispatchCount);
            Assert.Equal(1, rebuilt.sortCacheInsertCount);
            Assert.Equal(1, rebuilt.sortCacheEvictionCount);
        });

    [Fact]
    public void ProjectionVariantsCanReturnToEarlierCachedEntry()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8446;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc normal = Camera(clear: true);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc reversed = normal;
            reversed.worldToClip22 = -1;
            reversed.worldToClip23 = 1;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref normal, effects, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref reversed, effects, out var changed));
            Assert.Equal(2, changed.sortCacheEntryCount);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref normal, effects, out var returned));
            Assert.Equal(1, returned.sortCacheHitCount);
            Assert.Equal(0, returned.sortMapDispatchCount);
        });

    [Fact]
    public void TransformVariantsCanReturnToEarlierCachedEntry()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8447;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarEffectDesc near = EffectAtDepth(effectId, 0.1f);
            AnityNative.GraphicsVFXPlanarEffectDesc far = EffectAtDepth(effectId, 0.8f);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, new[] { near }, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, new[] { far }, out var changed));
            Assert.Equal(2, changed.sortCacheEntryCount);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, new[] { near }, out var returned));
            Assert.Equal(1, returned.sortCacheHitCount);
            Assert.Equal(0, returned.sortMapDispatchCount);
        });

    [Fact]
    public void ResidentGenerationPurgesEntireCameraWorkingSet()
        => WithMetal(device =>
        {
            const ulong effectId = 0x8448;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            for (ulong cameraId = 500; cameraId < 504; ++cameraId)
            {
                AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
                camera.cameraId = cameraId;
                Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            }
            PublishResident(device, effectId, usesDeadList: true);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc next = Camera(clear: true);
            next.cameraId = 504;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref next, effects, out var rebuilt));
            Assert.Equal(1, rebuilt.aliveCompactionCount);
            Assert.Equal(1, rebuilt.sortCacheEntryCount);
            Assert.Equal(1, rebuilt.sortCacheInsertCount);
            Assert.Equal(0, rebuilt.sortCacheEvictionCount);
        });

    [Fact]
    public void MultipleParticleSystemsReportAggregateCacheEntries()
        => WithMetal(device =>
        {
            const ulong first = 0x8449;
            const ulong second = 0x844A;
            SpawnResident(device, first, 1, 0, 0);
            SpawnResident(device, second, 0, 1, 0);
            Set(device, first, Desc(first, 10, flags: 1u | 4u));
            Set(device, second, Desc(second, 11, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(first, 0, 0), Effect(second, 0, 0) }, out var info));
            Assert.Equal(2, info.sortedOutputCount);
            Assert.Equal(2, info.sortCacheInsertCount);
            Assert.Equal(2, info.sortCacheEntryCount);
            Assert.Equal(4, info.sortCacheCapacityPerSystem);
        });

    [Fact]
    public void AlternatingProjectionCachePreservesFramebufferOrder()
        => WithMetal(device =>
        {
            const ulong effectId = 0x844B;
            SpawnTwoAlphaParticles(device, effectId, equalDepth: false);
            Set(device, effectId, Desc(
                effectId, 10, blendMode: 1, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc normal = Camera(clear: true);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc reversed = normal;
            reversed.cameraId++;
            reversed.worldToClip22 = -1;
            reversed.worldToClip23 = 1;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref normal, effects, out _));
            AssertPixelDominance(Read(device), redDominant: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref reversed, effects, out _));
            AssertPixelDominance(Read(device), redDominant: false);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref normal, effects, out var cached));
            Assert.Equal(1, cached.sortCacheHitCount);
            Assert.Equal(0, cached.sortMapDispatchCount);
            AssertPixelDominance(Read(device), redDominant: true);
        });

    [Fact]
    public void SortCacheRemainsBoundedAcrossManyCameras()
        => WithMetal(device =>
        {
            const ulong effectId = 0x844C;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            for (int index = 0; index < 16; ++index)
            {
                AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
                camera.cameraId = (ulong)(600 + index);
                Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var info));
                Assert.InRange(info.sortCacheEntryCount, 1, 4);
                Assert.Equal(4, info.sortCacheCapacityPerSystem);
                Assert.Equal(index < 4 ? 0 : 1, info.sortCacheEvictionCount);
            }
        });

    [Fact]
    public void ResidentGenerationChangeInvalidatesSortCache()
        => WithMetal(device =>
        {
            const ulong effectId = 0x845;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            PublishResident(device, effectId, usesDeadList: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var changed));
            Assert.Equal(0, changed.sortCacheHitCount);
            Assert.Equal(1, changed.sortMapDispatchCount);
            Assert.Equal(1, changed.aliveCompactionCount);
            Assert.Equal(1, changed.sortCacheEntryCount);
            Assert.Equal(1, changed.sortCacheInsertCount);
            Assert.Equal(0, changed.sortCacheEvictionCount);
        });

    [Fact]
    public void TwoSortedOutputsSharingSystemSortOnlyOnce()
        => WithMetal(device =>
        {
            const ulong effectId = 0x846;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId,
                Desc(effectId, 10, flags: 1u | 4u),
                Desc(effectId, 11, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(2, info.sortedOutputCount);
            Assert.Equal(1, info.sortMapDispatchCount);
            Assert.Equal(1, info.sortCacheHitCount);
            Assert.Equal(1, info.sortCacheInsertCount);
            Assert.Equal(1, info.sortCacheEntryCount);
        });

    [Fact]
    public void SortingConsumesSparseCompactedPhysicalIndex()
        => WithMetal(device =>
        {
            const ulong effectId = 0x847;
            SpawnSparseSecondParticle(device, effectId);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(1, info.sortedOutputCount);
            Assert.Equal(1, info.particleCount);
            AssertPixel(Read(device), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void SortingAndExplicitIndirectFlagsCompose()
        => WithMetal(device =>
        {
            const ulong effectId = 0x848;
            SpawnResident(device, effectId, 0, 0, 1);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u | 8u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(1, info.sortedOutputCount);
            Assert.Equal(1, info.indirectArgumentCount);
            Assert.Equal(1, info.drawCount);
        });

    [Fact]
    public void AlphaClipStillTruthfullySkipsBeforeSorting()
        => WithMetal(device =>
        {
            const ulong effectId = 0x849;
            SpawnResident(device, effectId, 1, 0, 0);
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 2u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(0, info.sortedOutputCount);
            Assert.Equal(0, info.sortMapDispatchCount);
            Assert.Equal(0, info.sortCacheEntryCount);
            Assert.Equal(0, info.sortCacheCapacityPerSystem);
            Assert.Equal(1, info.skippedOutputCount);
        });

    [Fact]
    public void NullBackendDoesNotClaimGpuSorting()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            const ulong effectId = 0x84A;
            Set(device, effectId, Desc(effectId, 10, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(0, info.sortedOutputCount);
            Assert.Equal(0, info.sortMapDispatchCount);
            Assert.Equal(0, info.sortCacheInsertCount);
            Assert.Equal(0, info.sortCacheEntryCount);
            Assert.Equal(0, info.sortCacheCapacityPerSystem);
            Assert.Equal(1, info.skippedOutputCount);
        });

    [Fact]
    public void SharedParticleSystemRejectsConflictingPositionLayout()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            const ulong effectId = 0x84B;
            AnityNative.GraphicsVFXPlanarOutputDesc first = Desc(effectId, 10);
            AnityNative.GraphicsVFXPlanarOutputDesc second = Desc(effectId, 11);
            Set(device, effectId, first);
            second.positionOffsetBytes += sizeof(uint);
            Assert.Equal(AnityNative.Result.InvalidArg,
                AnityNative.Graphics_SetVFXPlanarOutputs(
                    device.Handle, effectId, new[] { first, second }, 2));
            Assert.True(device.TryGetVFXPlanarOutputCount(effectId, out int count));
            Assert.Equal(1, count);
        });

    [Fact]
    public void SortedAlphaParticlesBlendFarToNear()
        => WithMetal(device =>
        {
            const ulong effectId = 0x84C;
            SpawnTwoAlphaParticles(device, effectId, equalDepth: false);
            Set(device, effectId, Desc(
                effectId, 10, blendMode: 1, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out var info));
            Assert.Equal(1, info.sortedOutputCount);
            AssertPixelDominance(Read(device), redDominant: true);
        });

    [Fact]
    public void EqualDepthSortPreservesOriginalCompactOrdinal()
        => WithMetal(device =>
        {
            const ulong effectId = 0x84D;
            SpawnTwoAlphaParticles(device, effectId, equalDepth: true);
            Set(device, effectId, Desc(
                effectId, 10, blendMode: 1, flags: 1u | 4u));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(effectId, 0, 0) }, out _));
            AssertPixelDominance(Read(device), redDominant: false);
        });

    [Fact]
    public void ProjectionChangeReversesSortedAlphaOrder()
        => WithMetal(device =>
        {
            const ulong effectId = 0x84E;
            SpawnTwoAlphaParticles(device, effectId, equalDepth: false);
            Set(device, effectId, Desc(
                effectId, 10, blendMode: 1, flags: 1u | 4u));
            var effects = new[] { Effect(effectId, 0, 0) };
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out _));
            AssertPixelDominance(Read(device), redDominant: true);
            camera.worldToClip22 = -1;
            camera.worldToClip23 = 1;
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera, effects, out var changed));
            Assert.Equal(1, changed.sortMapDispatchCount);
            AssertPixelDominance(Read(device), redDominant: false);
        });

    [Fact]
    public void NearDepthWriteOccludesLaterFartherLEqualParticle()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x850, 1, 0, 0);
            SpawnResident(device, 0x851, 0, 1, 0);
            Set(device, 0x850, Desc(0x850, 10, renderQueue: 2900, zTest: 2, zWrite: 1));
            Set(device, 0x851, Desc(0x851, 11, renderQueue: 3000, zTest: 2));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(0x850, 0.25f), EffectAtDepth(0x851, 0.75f) }, out var info));
            Assert.Equal(1, info.depthWriteOutputCount);
            Assert.Equal(2, info.depthTestOutputCount);
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void NearParticlePassesAfterFarDepthWrite()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x852, 1, 0, 0);
            SpawnResident(device, 0x853, 0, 1, 0);
            Set(device, 0x852, Desc(0x852, 10, renderQueue: 2900, zTest: 2, zWrite: 1));
            Set(device, 0x853, Desc(0x853, 11, renderQueue: 3000, zTest: 2));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(0x852, 0.75f), EffectAtDepth(0x853, 0.25f) }, out _));
            AssertPixel(Read(device), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void AlwaysDepthTestIgnoresPreviouslyWrittenDepth()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x854, 1, 0, 0);
            SpawnResident(device, 0x855, 0, 1, 0);
            Set(device, 0x854, Desc(0x854, 10, renderQueue: 2900, zTest: 2, zWrite: 1));
            Set(device, 0x855, Desc(0x855, 11, renderQueue: 3000, zTest: 6));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(0x854, 0.25f), EffectAtDepth(0x855, 0.75f) }, out _));
            AssertPixel(Read(device), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void DepthPersistsAcrossCameraSubmissionWithoutClear()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x856, 1, 0, 0);
            SpawnResident(device, 0x857, 0, 1, 0);
            Set(device, 0x856, Desc(0x856, 10, zTest: 2, zWrite: 1));
            Set(device, 0x857, Desc(0x857, 11, zTest: 2));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc first = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref first,
                new[] { EffectAtDepth(0x856, 0.25f) }, out var initial));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc second = Camera(clear: false);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref second,
                new[] { EffectAtDepth(0x857, 0.75f) }, out var preserved));
            Assert.Equal(1, initial.depthClearCount);
            Assert.Equal(0, preserved.depthClearCount);
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    [Fact]
    public void CameraClearResetsDepthBeforeNextSubmission()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x858, 1, 0, 0);
            SpawnResident(device, 0x859, 0, 1, 0);
            Set(device, 0x858, Desc(0x858, 10, zTest: 2, zWrite: 1));
            Set(device, 0x859, Desc(0x859, 11, zTest: 2));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(0x858, 0.25f) }, out _));
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { EffectAtDepth(0x859, 0.75f) }, out var reset));
            Assert.Equal(1, reset.depthClearCount);
            AssertPixel(Read(device), 32, 32, 0, 255, 0, 255);
        });

    [Fact]
    public void ZeroAliveOutputDoesNotBuildCompactionOrIndirectArguments()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x819, 1, 0, 0, alive: false);
            Set(device, 0x819, Desc(0x819, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x819, 0, 0) }, out var info));
            Assert.Equal(0, info.aliveCompactionCount);
            Assert.Equal(0, info.indirectArgumentCount);
            Assert.Equal(1, info.skippedOutputCount);
        });

    [Fact]
    public void UnsupportedAlphaClipDoesNotPolluteAliveCompactionCache()
        => WithMetal(device =>
        {
            SpawnResident(device, 0x81A, 1, 0, 0);
            AnityNative.GraphicsVFXPlanarOutputDesc output = Desc(0x81A, 10);
            output.flags |= 2u;
            Set(device, 0x81A, output);
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x81A, 0, 0) }, out var info));
            Assert.Equal(0, info.aliveCompactionCount);
            Assert.Equal(0, info.aliveCompactionCacheHitCount);
            Assert.Equal(0, info.indirectArgumentCount);
        });

    [Fact]
    public void NullBackendDoesNotClaimGpuCompaction()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Set(device, 0x81B, Desc(0x81B, 10));
            AnityNative.GraphicsVFXPlanarCameraBatchDesc camera = Camera(clear: false);
            Assert.Equal(AnityNative.Result.Ok, Draw(device, ref camera,
                new[] { Effect(0x81B, 0, 0) }, out var info));
            Assert.Equal(0, info.aliveCompactionCount);
            Assert.Equal(0, info.indirectArgumentCount);
            Assert.Equal(1, info.skippedOutputCount);
        });

    private static AnityNative.Result Draw(
        NativeGraphicsDevice device,
        ref AnityNative.GraphicsVFXPlanarCameraBatchDesc camera,
        AnityNative.GraphicsVFXPlanarEffectDesc[] effects,
        out AnityNative.GraphicsVFXPlanarCameraDrawInfo info)
        => AnityNative.Graphics_DrawVFXPlanarCamera(
            device.Handle, ref camera, effects, effects.Length, out info);

    private static void Set(
        NativeGraphicsDevice device, ulong effectId,
        params AnityNative.GraphicsVFXPlanarOutputDesc[] outputs)
        => Assert.Equal(AnityNative.Result.Ok,
            AnityNative.Graphics_SetVFXPlanarOutputs(device.Handle, effectId, outputs, outputs.Length));

    private static AnityNative.GraphicsVFXPlanarCameraBatchDesc Camera(bool clear, int cullingMask = -1)
        => new()
        {
            cameraId = 7,
            worldToClip00 = 1, worldToClip11 = 1,
            worldToClip22 = 1, worldToClip33 = 1,
            cullingMask = cullingMask,
            flags = clear ? 1 : 0
        };

    private static AnityNative.GraphicsVFXPlanarEffectDesc Effect(
        ulong effectId, int layer, int sortOrder)
        => new()
        {
            effectId = effectId,
            localToWorld00 = 1, localToWorld11 = 1,
            localToWorld22 = 1, localToWorld33 = 1,
            layer = layer,
            sortOrder = sortOrder
        };

    private static AnityNative.GraphicsVFXPlanarEffectDesc EffectAtDepth(
        ulong effectId, float depth)
    {
        AnityNative.GraphicsVFXPlanarEffectDesc effect = Effect(effectId, 0, 0);
        effect.localToWorld23 = depth;
        return effect;
    }

    private static AnityNative.GraphicsVFXPlanarOutputDesc Desc(
        ulong effectId, long contextId, int renderQueue = 3000,
        int primitiveType = 1, int zTest = 6, int zWrite = 0,
        int blendMode = 3, int particleCapacity = Capacity, uint flags = 1,
        string systemName = SystemName)
        => new()
        {
            version = 1,
            flags = flags,
            effectId = effectId,
            contextId = contextId,
            particleSystemId = Shader.PropertyToID(systemName),
            primitiveType = primitiveType,
            particleCapacity = particleCapacity,
            attributeStrideBytes = StrideWords * sizeof(uint),
            aliveOffsetBytes = 0,
            positionOffsetBytes = 1 * sizeof(uint),
            colorOffsetBytes = 4 * sizeof(uint),
            alphaOffsetBytes = 7 * sizeof(uint),
            axisXOffsetBytes = 8 * sizeof(uint),
            axisYOffsetBytes = 11 * sizeof(uint),
            axisZOffsetBytes = 14 * sizeof(uint),
            angleXOffsetBytes = 17 * sizeof(uint),
            angleYOffsetBytes = 18 * sizeof(uint),
            angleZOffsetBytes = 19 * sizeof(uint),
            pivotXOffsetBytes = 20 * sizeof(uint),
            pivotYOffsetBytes = 21 * sizeof(uint),
            pivotZOffsetBytes = 22 * sizeof(uint),
            sizeOffsetBytes = 23 * sizeof(uint),
            scaleXOffsetBytes = 24 * sizeof(uint),
            scaleYOffsetBytes = 25 * sizeof(uint),
            scaleZOffsetBytes = 26 * sizeof(uint),
            blendMode = blendMode,
            zWrite = zWrite,
            zTest = zTest,
            renderQueue = renderQueue
        };

    private static void SpawnResident(
        NativeGraphicsDevice device, ulong effectId, float red, float green, float blue,
        bool alive = true, string systemName = SystemName,
        long initializeContextId = 41, long updateContextId = 51)
    {
        VFXRuntimeInitializeAttributeData[] attributes =
        {
            Attribute("alive", VFXRuntimeValueType.Boolean, 0, alive ? 1u : 0u),
            Attribute("position", VFXRuntimeValueType.Float3, 1, F(0), F(0), F(0)),
            Attribute("color", VFXRuntimeValueType.Float3, 4, F(red), F(green), F(blue)),
            Attribute("alpha", VFXRuntimeValueType.Float, 7, F(1)),
            Attribute("axisX", VFXRuntimeValueType.Float3, 8, F(1), F(0), F(0)),
            Attribute("axisY", VFXRuntimeValueType.Float3, 11, F(0), F(1), F(0)),
            Attribute("axisZ", VFXRuntimeValueType.Float3, 14, F(0), F(0), F(1)),
            Attribute("angleX", VFXRuntimeValueType.Float, 17, F(0)),
            Attribute("angleY", VFXRuntimeValueType.Float, 18, F(0)),
            Attribute("angleZ", VFXRuntimeValueType.Float, 19, F(0)),
            Attribute("pivotX", VFXRuntimeValueType.Float, 20, F(0)),
            Attribute("pivotY", VFXRuntimeValueType.Float, 21, F(0)),
            Attribute("pivotZ", VFXRuntimeValueType.Float, 22, F(0)),
            Attribute("size", VFXRuntimeValueType.Float, 23, F(1)),
            Attribute("scaleX", VFXRuntimeValueType.Float, 24, F(1)),
            Attribute("scaleY", VFXRuntimeValueType.Float, 25, F(1)),
            Attribute("scaleZ", VFXRuntimeValueType.Float, 26, F(1)),
            Attribute("seed", VFXRuntimeValueType.UInt32, 27, 123u)
        };
        var kernel = new VFXRuntimeInitializeKernelData(
            initializeContextId, Capacity, StrideWords, 1, true, attributes,
            Array.Empty<VFXRuntimeInitializeOperationData>());
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = effectId,
            sequence = 1,
            initializeContextId = initializeContextId,
            sourceSpawnerContextId = 31,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(systemName),
            spawnSystemId = 13,
            recordCount = 1,
            strideBytes = sizeof(uint)
        };
        Assert.True(device.SubmitVFXInitializeKernels(
            new[] { dispatch }, new VFXRuntimeInitializeKernelData?[] { kernel },
            new byte[sizeof(uint)], 17));
        PublishResident(
            device, effectId, usesDeadList: true, systemName: systemName,
            updateContextId: updateContextId);
    }

    private static void SpawnSparseSecondParticle(
        NativeGraphicsDevice device, ulong effectId)
    {
        VFXRuntimeInitializeAttributeData[] attributes =
        {
            Attribute("alive", VFXRuntimeValueType.Boolean, 0, 1u),
            Attribute("position", VFXRuntimeValueType.Float3, 1, F(0), F(0), F(0)),
            Attribute("color", VFXRuntimeValueType.Float3, 4, F(0), F(1), F(0)),
            Attribute("alpha", VFXRuntimeValueType.Float, 7, F(1)),
            Attribute("axisX", VFXRuntimeValueType.Float3, 8, F(1), F(0), F(0)),
            Attribute("axisY", VFXRuntimeValueType.Float3, 11, F(0), F(1), F(0)),
            Attribute("axisZ", VFXRuntimeValueType.Float3, 14, F(0), F(0), F(1)),
            Attribute("angleX", VFXRuntimeValueType.Float, 17, F(0)),
            Attribute("angleY", VFXRuntimeValueType.Float, 18, F(0)),
            Attribute("angleZ", VFXRuntimeValueType.Float, 19, F(0)),
            Attribute("pivotX", VFXRuntimeValueType.Float, 20, F(0)),
            Attribute("pivotY", VFXRuntimeValueType.Float, 21, F(0)),
            Attribute("pivotZ", VFXRuntimeValueType.Float, 22, F(0)),
            Attribute("size", VFXRuntimeValueType.Float, 23, F(1)),
            Attribute("scaleX", VFXRuntimeValueType.Float, 24, F(1)),
            Attribute("scaleY", VFXRuntimeValueType.Float, 25, F(1)),
            Attribute("scaleZ", VFXRuntimeValueType.Float, 26, F(1)),
            Attribute("seed", VFXRuntimeValueType.UInt32, 27, 123u)
        };
        var aliveFromEvent = new VFXRuntimeInitializeOperationData(
            0, 0, VFXRuntimeValueType.Boolean,
            VFXRuntimeInitializeValueSource.Source,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            Array.Empty<uint>(), Array.Empty<uint>(), F(1));
        var kernel = new VFXRuntimeInitializeKernelData(
            41, Capacity, StrideWords, 1, false, attributes,
            new[] { aliveFromEvent });
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = effectId,
            sequence = 1,
            initializeContextId = 41,
            sourceSpawnerContextId = 31,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(SystemName),
            spawnSystemId = 13,
            recordCount = 2,
            strideBytes = sizeof(uint)
        };
        byte[] records = new byte[2 * sizeof(uint)];
        BitConverter.GetBytes(1u).CopyTo(records, sizeof(uint));
        Assert.True(device.SubmitVFXInitializeKernels(
            new[] { dispatch }, new VFXRuntimeInitializeKernelData?[] { kernel },
            records, 17));
        PublishResident(device, effectId, usesDeadList: false);
    }

    private static void SpawnTwoAlphaParticles(
        NativeGraphicsDevice device, ulong effectId, bool equalDepth)
        => SpawnAlphaParticles(device, effectId, new[]
        {
            (Depth: 0.25f, Red: 1f, Green: 0f, Blue: 0f),
            (Depth: equalDepth ? 0.25f : 0.75f, Red: 0f, Green: 1f, Blue: 0f)
        });

    private static void SpawnAlphaParticles(
        NativeGraphicsDevice device, ulong effectId,
        IReadOnlyList<(float Depth, float Red, float Green, float Blue)> particles)
    {
        Assert.NotEmpty(particles);
        VFXRuntimeInitializeAttributeData[] attributes =
        {
            Attribute("alive", VFXRuntimeValueType.Boolean, 0, 1u),
            Attribute("position", VFXRuntimeValueType.Float3, 1, F(0), F(0), F(0)),
            Attribute("color", VFXRuntimeValueType.Float3, 4, F(1), F(1), F(1)),
            Attribute("alpha", VFXRuntimeValueType.Float, 7, F(0.5f)),
            Attribute("axisX", VFXRuntimeValueType.Float3, 8, F(1), F(0), F(0)),
            Attribute("axisY", VFXRuntimeValueType.Float3, 11, F(0), F(1), F(0)),
            Attribute("axisZ", VFXRuntimeValueType.Float3, 14, F(0), F(0), F(1)),
            Attribute("angleX", VFXRuntimeValueType.Float, 17, F(0)),
            Attribute("angleY", VFXRuntimeValueType.Float, 18, F(0)),
            Attribute("angleZ", VFXRuntimeValueType.Float, 19, F(0)),
            Attribute("pivotX", VFXRuntimeValueType.Float, 20, F(0)),
            Attribute("pivotY", VFXRuntimeValueType.Float, 21, F(0)),
            Attribute("pivotZ", VFXRuntimeValueType.Float, 22, F(0)),
            Attribute("size", VFXRuntimeValueType.Float, 23, F(1)),
            Attribute("scaleX", VFXRuntimeValueType.Float, 24, F(1)),
            Attribute("scaleY", VFXRuntimeValueType.Float, 25, F(1)),
            Attribute("scaleZ", VFXRuntimeValueType.Float, 26, F(1)),
            Attribute("seed", VFXRuntimeValueType.UInt32, 27, 123u)
        };
        VFXRuntimeInitializeOperationData[] operations =
        {
            SourceInitialize(0, 0, VFXRuntimeValueType.Boolean),
            SourceInitialize(1, 1, VFXRuntimeValueType.Float3),
            SourceInitialize(4, 4, VFXRuntimeValueType.Float3),
            SourceInitialize(7, 7, VFXRuntimeValueType.Float)
        };
        var kernel = new VFXRuntimeInitializeKernelData(
            41, (uint)particles.Count, StrideWords, 8, false, attributes, operations);
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = effectId,
            sequence = 1,
            initializeContextId = 41,
            sourceSpawnerContextId = 31,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(SystemName),
            spawnSystemId = 13,
            recordCount = particles.Count,
            strideBytes = 8 * sizeof(uint)
        };
        uint[] words = new uint[particles.Count * 8];
        for (int index = 0; index < particles.Count; ++index)
        {
            (float depth, float red, float green, float blue) = particles[index];
            int start = index * 8;
            words[start + 0] = 1u;
            words[start + 1] = F(0);
            words[start + 2] = F(0);
            words[start + 3] = F(depth);
            words[start + 4] = F(red);
            words[start + 5] = F(green);
            words[start + 6] = F(blue);
            words[start + 7] = F(0.5f);
        }
        byte[] records = new byte[words.Length * sizeof(uint)];
        Buffer.BlockCopy(words, 0, records, 0, records.Length);
        Assert.True(device.SubmitVFXInitializeKernels(
            new[] { dispatch }, new VFXRuntimeInitializeKernelData?[] { kernel },
            records, 17));
        PublishResident(
            device, effectId, usesDeadList: false, capacity: particles.Count);
    }

    private static VFXRuntimeInitializeOperationData SourceInitialize(
        int targetOffset, int sourceOffset, VFXRuntimeValueType type)
        => new(targetOffset, sourceOffset, type,
            VFXRuntimeInitializeValueSource.Source,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            Array.Empty<uint>(), Array.Empty<uint>(), F(1));

    private static void PublishResident(
        NativeGraphicsDevice device, ulong effectId, bool usesDeadList,
        int capacity = Capacity, string systemName = SystemName,
        long updateContextId = 51)
    {
        var operation = new VFXRuntimeUpdateOperationData(
            VFXRuntimeUpdateOperationKind.SetAttribute,
            23, -1, -1, -1, -1,
            VFXRuntimeValueType.Float,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            false, new[] { F(1) }, Array.Empty<uint>(), F(1));
        var update = new VFXRuntimeUpdateKernelData(
            updateContextId, systemName, (uint)capacity, StrideWords,
            usesDeadList, false, 0, 27,
            new[] { operation });
        Assert.True(device.DispatchVFXUpdateKernels(effectId, new[] { update }, 0f, 17));
    }

    private static VFXRuntimeInitializeAttributeData Attribute(
        string name, VFXRuntimeValueType type, int offset, params uint[] defaults)
        => new(new VFXRuntimeAttributeData(name, type, offset, defaults.Length), defaults);

    private static uint F(float value) => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static byte[] Read(NativeGraphicsDevice device)
    {
        Assert.True(device.TryReadbackSwapchainRGBA8(out byte[] pixels));
        return pixels;
    }

    private static AnityNative.GraphicsVFXPlanarSubmissionStats SubmissionStats(
        NativeGraphicsDevice device)
    {
        Assert.True(device.TryGetVFXPlanarSubmissionStats(out var stats));
        return stats;
    }

    private static void AssertPixel(byte[] pixels, int x, int y, byte r, byte g, byte b, byte a)
    {
        int offset = (y * 64 + x) * 4;
        Assert.Equal((r, g, b, a),
            (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]));
    }

    private static void AssertPixelDominance(byte[] pixels, bool redDominant)
    {
        int offset = (32 * 64 + 32) * 4;
        byte red = pixels[offset];
        byte green = pixels[offset + 1];
        Assert.True(redDominant ? red > green : green > red,
            $"Expected {(redDominant ? "red" : "green")} dominance, got R={red}, G={green}.");
        Assert.Equal(0, pixels[offset + 2]);
        Assert.True(pixels[offset + 3] > 0);
    }

    private static void WithNative(GraphicsDeviceType type, Action<NativeGraphicsDevice> action)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(type, 64, 64, false);
        bool available = device.Handle != IntPtr.Zero;
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(available);
        if (available) action(device);
    }

    private static void WithMetal(Action<NativeGraphicsDevice> action)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        WithNative(GraphicsDeviceType.Metal, device =>
        {
            Assert.True(device.CreateSwapchain(64, 64, imageCount: 3, hdr: false));
            action(device);
        });
    }
}
