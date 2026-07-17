using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXInitializeKernelTests
{
    [Fact]
    public void NativeLayouts_MatchKernelCAbi()
    {
        Assert.Equal(44, Marshal.SizeOf<AnityNative.GraphicsVFXInitializeKernelDesc>());
        Assert.Equal(32, Marshal.SizeOf<AnityNative.GraphicsVFXInitializeAttributeDesc>());
        Assert.Equal(68, Marshal.SizeOf<AnityNative.GraphicsVFXInitializeOperationDesc>());
        Assert.Equal(40, Marshal.SizeOf<AnityNative.GraphicsVFXParticleSystemInfo>());
    }

    [Fact]
    public void CpuReference_WritesDefaultsAndConstantOperation()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            capacity: 4,
            attributes: new[] { Attribute("size", VFXRuntimeValueType.Float, Float(0.1f)) },
            operations: new[] { Constant(0, VFXRuntimeValueType.Float, Float(2.5f)) });

        Assert.True(Submit(device, Desc(1, 1, 4), kernel, Words(10, 20), 17));
        NativeVFXParticleSystem state = Read(device, 1);

        Assert.Equal(1, state.Info.aliveCount);
        Assert.Equal(Float(2.5f), Word(state.AttributeRecords, 0));
    }

    [Fact]
    public void CpuReference_LoadsSourceRecordAtBatchedOffset()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4,
            new[] { Attribute("size", VFXRuntimeValueType.Float, Float(0.1f)) },
            new[] { Source(0, 0, VFXRuntimeValueType.Float) });

        Assert.True(Submit(device, Desc(2, 1, 4, start: 1), kernel, Words(Float(3f), Float(7f))));

        Assert.Equal(Float(7f), Word(Read(device, 2).AttributeRecords, 0));
    }

    [Fact]
    public void CpuReference_AppliesAddMultiplyAndBlendInProgramOrder()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            2,
            new[] { Attribute("size", VFXRuntimeValueType.Float, Float(2f)) },
            new[]
            {
                Constant(0, VFXRuntimeValueType.Float, Float(3f), VFXRuntimeInitializeComposition.Add),
                Constant(0, VFXRuntimeValueType.Float, Float(2f), VFXRuntimeInitializeComposition.Multiply),
                Constant(0, VFXRuntimeValueType.Float, Float(14f), VFXRuntimeInitializeComposition.Blend, 0.25f)
            });

        Assert.True(Submit(device, Desc(3, 1, 4), kernel, Words(0)));

        Assert.Equal(Float(11f), Word(Read(device, 3).AttributeRecords, 0));
    }

    [Fact]
    public void CpuReference_InitializesSystemParticleIdSeedAndSpawnIndex()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4,
            new[]
            {
                Attribute("particleId", VFXRuntimeValueType.UInt32, 99),
                Attribute("seed", VFXRuntimeValueType.UInt32, 0),
                Attribute("spawnIndex", VFXRuntimeValueType.UInt32, 99)
            },
            new[] { SystemValue(0, VFXRuntimeInitializeValueSource.ParticleId),
                SystemValue(1, VFXRuntimeInitializeValueSource.Seed),
                SystemValue(2, VFXRuntimeInitializeValueSource.SpawnIndex) });

        Assert.True(Submit(device, Desc(4, 1, 4, count: 2), kernel, Words(0, 0), 23));
        NativeVFXParticleSystem state = Read(device, 4);

        Assert.Equal(0u, Word(state.AttributeRecords, 0));
        Assert.NotEqual(0u, Word(state.AttributeRecords, 1));
        Assert.Equal(0u, Word(state.AttributeRecords, 2));
        Assert.Equal(1u, Word(state.AttributeRecords, 3));
        Assert.Equal(1u, Word(state.AttributeRecords, 5));
    }

    [Fact]
    public void CpuReference_ConsumesDeadListAndWritesSelectedPhysicalSlots()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4,
            new[]
            {
                Attribute("alive", VFXRuntimeValueType.Boolean, 1),
                Attribute("size", VFXRuntimeValueType.Float, Float(1f))
            },
            new[] { Constant(1, VFXRuntimeValueType.Float, Float(9f)) },
            usesDeadList: true);

        Assert.True(Submit(device, Desc(5, 1, 4, count: 2), kernel, Words(0, 0)));
        NativeVFXParticleSystem state = Read(device, 5);

        Assert.Equal(2, state.Info.aliveCount);
        Assert.Equal(2, state.Info.deadCount);
        Assert.Equal(new uint[] { 0, 1 }, state.DeadList);
        Assert.Equal(Float(9f), Word(state.AttributeRecords, 3 * 2 + 1));
        Assert.Equal(Float(9f), Word(state.AttributeRecords, 2 * 2 + 1));
    }

    [Fact]
    public void CpuReference_AliveFalseDoesNotConsumeDeadList()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            3,
            new[] { Attribute("alive", VFXRuntimeValueType.Boolean, 1) },
            new[] { Constant(0, VFXRuntimeValueType.Boolean, 0) },
            usesDeadList: true);

        Assert.True(Submit(device, Desc(6, 1, 4, count: 2), kernel, Words(0, 0)));
        NativeVFXParticleSystem state = Read(device, 6);

        Assert.Equal(0, state.Info.aliveCount);
        Assert.Equal(3, state.Info.deadCount);
        Assert.Equal(new uint[] { 0, 1, 2 }, state.DeadList);
    }

    [Fact]
    public void CpuReference_ClampsSpawnToParticleCapacity()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            2, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>());

        Assert.True(Submit(device, Desc(7, 1, 4, count: 3), kernel, Words(0, 0, 0)));

        Assert.Equal(2, Read(device, 7).Info.aliveCount);
    }

    [Fact]
    public void IdenticalKernelRetry_IsIdempotentAndDoesNotSpawnTwice()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>());
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(8, 1, 4);

        Assert.True(Submit(device, desc, kernel, Words(0)));
        ulong generation = Read(device, 8).Info.generation;
        Assert.True(Submit(device, desc, kernel, Words(0)));
        NativeVFXParticleSystem state = Read(device, 8);

        Assert.Equal(1, state.Info.aliveCount);
        Assert.Equal(generation, state.Info.generation);
    }

    [Fact]
    public void IdenticalTransactionRetry_AllowsDispatchOrderToChange()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData firstKernel = Kernel(
            4, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>());
        VFXRuntimeInitializeKernelData secondKernel = firstKernel with { ContextId = 41 };
        AnityNative.GraphicsVFXInitializeDispatchDesc first = Desc(81, 1, 4);
        AnityNative.GraphicsVFXInitializeDispatchDesc second = Desc(81, 1, 4, start: 1);
        second.initializeContextId = 41;
        byte[] source = Words(0, 0);

        Assert.True(device.SubmitVFXInitializeKernels(
            new[] { first, second }, new VFXRuntimeInitializeKernelData?[] { firstKernel, secondKernel },
            source, 0));
        ulong generation = Read(device, 81).Info.generation;
        Assert.True(device.SubmitVFXInitializeKernels(
            new[] { second, first }, new VFXRuntimeInitializeKernelData?[] { secondKernel, firstKernel },
            source, 0));
        NativeVFXParticleSystem state = Read(device, 81);

        Assert.Equal(2, state.Info.aliveCount);
        Assert.Equal(generation, state.Info.generation);
    }

    [Fact]
    public void NewerKernelDispatch_AppendsParticleState()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            new[] { Source(0, 0, VFXRuntimeValueType.Float) });

        Assert.True(Submit(device, Desc(9, 1, 4), kernel, Words(Float(2f))));
        Assert.True(Submit(device, Desc(9, 2, 4), kernel, Words(Float(3f))));
        NativeVFXParticleSystem state = Read(device, 9);

        Assert.Equal(2, state.Info.aliveCount);
        Assert.Equal(Float(2f), Word(state.AttributeRecords, 0));
        Assert.Equal(Float(3f), Word(state.AttributeRecords, 1));
    }

    [Fact]
    public void ClearEffectState_RemovesParticleBufferAndDeadList()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            2, new[] { Attribute("alive", VFXRuntimeValueType.Boolean, 1) },
            Array.Empty<VFXRuntimeInitializeOperationData>(), usesDeadList: true);
        Assert.True(Submit(device, Desc(10, 1, 4), kernel, Words(0)));

        Assert.True(device.ClearVFXEffectState(10));

        Assert.False(device.TryGetVFXParticleSystem(10, ParticleSystemId, out _));
    }

    [Fact]
    public void SpawnCount_SingleSourceRecordExpandsToMultipleParticles()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("spawnIndex", VFXRuntimeValueType.UInt32, 99) },
            new[] { SystemValue(0, VFXRuntimeInitializeValueSource.SpawnIndex) },
            spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(20, 1, 4), kernel, Words(Float(3f))));
        NativeVFXParticleSystem state = Read(device, 20);

        Assert.Equal(3, state!.Info.aliveCount);
        Assert.Equal(new uint[] { 0, 1, 2 }, state.AttributeRecords
            .Chunk(sizeof(uint)).Take(3).Select(bytes => Word(bytes, 0)));
    }

    [Fact]
    public void SpawnCount_InclusivePrefixMapsEachParticleToItsSourceRecord()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, 0) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(21, 1, 8, count: 3), kernel,
            Words(Float(2f), Float(7f), Float(0f), Float(8f), Float(3f), Float(9f))));
        NativeVFXParticleSystem state = Read(device, 21);

        Assert.Equal(5, state.Info.aliveCount);
        Assert.Equal(new[] { 7f, 7f, 9f, 9f, 9f },
            Enumerable.Range(0, 5).Select(index => BitConverter.UInt32BitsToSingle(
                Word(state.AttributeRecords, index))));
    }

    [Fact]
    public void SpawnCount_PrefixUsesBatchedStartEventIndex()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, 0) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(22, 1, 8, start: 1, count: 2), kernel,
            Words(Float(99f), Float(3f), Float(1f), Float(4f), Float(2f), Float(5f))));
        NativeVFXParticleSystem state = Read(device, 22);

        Assert.Equal(new[] { 4f, 5f, 5f }, Enumerable.Range(0, 3).Select(index =>
            BitConverter.UInt32BitsToSingle(Word(state.AttributeRecords, index))));
    }

    [Fact]
    public void SpawnCount_TruncatesPositiveFractionsAndIgnoresNegativeAndNaN()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, 0) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(23, 1, 8, count: 3), kernel,
            Words(Float(-2f), Float(1f), Float(float.NaN), Float(2f), Float(2.9f), Float(6f))));
        NativeVFXParticleSystem state = Read(device, 23);

        Assert.Equal(2, state.Info.aliveCount);
        Assert.Equal(Float(6f), Word(state.AttributeRecords, 0));
        Assert.Equal(Float(6f), Word(state.AttributeRecords, 1));
    }

    [Fact]
    public void SpawnCount_PositiveInfinitySaturatesAtParticleCapacity()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            3, new[] { Attribute("spawnIndex", VFXRuntimeValueType.UInt32, 99) },
            new[] { SystemValue(0, VFXRuntimeInitializeValueSource.SpawnIndex) },
            spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(24, 1, 4), kernel, Words(Float(float.PositiveInfinity))));

        Assert.Equal(3, Read(device, 24).Info.aliveCount);
    }

    [Fact]
    public void SpawnCount_AllZeroStillPublishesEmptyParticleSystem()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>(), spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(25, 1, 4, count: 2), kernel, Words(Float(0f), Float(-1f))));

        Assert.Equal(0, Read(device, 25).Info.aliveCount);
    }

    [Fact]
    public void SpawnCount_NewerDispatchUsesOnlyRemainingSequentialCapacity()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("particleId", VFXRuntimeValueType.UInt32, 99) },
            new[] { SystemValue(0, VFXRuntimeInitializeValueSource.ParticleId) },
            spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(26, 1, 4), kernel, Words(Float(3f))));
        Assert.True(Submit(device, Desc(26, 2, 4), kernel, Words(Float(3f))));
        NativeVFXParticleSystem state = Read(device, 26);

        Assert.Equal(4, state.Info.aliveCount);
        Assert.Equal(new uint[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4)
            .Select(index => Word(state.AttributeRecords, index)));
    }

    [Fact]
    public void SpawnCount_DeadListLimitsMultiSpawnAndConsumesPhysicalSlots()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            3, new[] { Attribute("alive", VFXRuntimeValueType.Boolean, 1) },
            Array.Empty<VFXRuntimeInitializeOperationData>(), usesDeadList: true,
            spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(27, 1, 4), kernel, Words(Float(8f))));
        NativeVFXParticleSystem state = Read(device, 27);

        Assert.Equal(3, state.Info.aliveCount);
        Assert.Equal(0, state.Info.deadCount);
        Assert.Empty(state.DeadList);
    }

    [Fact]
    public void SpawnCount_AliveSourceGateUsesPrefixMappedRecord()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("alive", VFXRuntimeValueType.Boolean, 1) },
            new[] { Source(0, 1, VFXRuntimeValueType.Boolean) }, usesDeadList: true,
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(28, 1, 8, count: 2), kernel,
            Words(Float(2f), 0, Float(2f), 1)));
        NativeVFXParticleSystem state = Read(device, 28);

        Assert.Equal(2, state.Info.aliveCount);
        Assert.Equal(2, state.Info.deadCount);
    }

    [Fact]
    public void SpawnCount_IdenticalRetryDoesNotRunExpandedSpawnTwice()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>(), spawnCountSourceOffsetWords: 0);
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(29, 1, 4);
        byte[] source = Words(Float(3f));

        Assert.True(Submit(device, desc, kernel, source));
        ulong generation = Read(device, 29).Info.generation;
        Assert.True(Submit(device, desc, kernel, source));
        NativeVFXParticleSystem state = Read(device, 29);

        Assert.Equal(3, state.Info.aliveCount);
        Assert.Equal(generation, state.Info.generation);
    }

    [Fact]
    public void RuntimeAssetV5_RoundTripsExecutableKernelAndSpawnCountOffset()
    {
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(0.1f)) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        var data = new VFXRuntimeAssetData(
            new[]
            {
                new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1),
                new VFXRuntimeAttributeData("size", VFXRuntimeValueType.Float, 1, 1)
            },
            new[] { "Spawn" }, new[] { input },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 8) },
            Array.Empty<VFXRuntimeOutputEventData>());

        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(data.Serialize());

        VFXRuntimeInitializeKernelData restoredKernel =
            Assert.Single(Assert.Single(restored.InputEventDispatches).Targets).InitializeKernel!;
        Assert.Equal(kernel.ContextId, restoredKernel.ContextId);
        Assert.Equal(kernel.ParticleCapacity, restoredKernel.ParticleCapacity);
        Assert.Equal(kernel.AttributeStrideWords, restoredKernel.AttributeStrideWords);
        Assert.Equal(kernel.Attributes.Select(attribute => attribute.Layout),
            restoredKernel.Attributes.Select(attribute => attribute.Layout));
        Assert.Equal(kernel.Operations.Select(operation => operation.ValueSource),
            restoredKernel.Operations.Select(operation => operation.ValueSource));
        Assert.Equal(0, restoredKernel.SpawnCountSourceOffsetWords);
        Assert.Equal(5u, VisualEffectAsset.currentRuntimeDataVersion);
    }

    [Fact]
    public void RuntimeAssetV3_MigratesInitializeKernelToLegacyOneRecordMode()
    {
        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(LegacyV3RuntimeAsset());

        VFXRuntimeInitializeKernelData kernel =
            Assert.Single(Assert.Single(restored.InputEventDispatches).Targets).InitializeKernel!;
        Assert.Equal(40, kernel.ContextId);
        Assert.Equal(4u, kernel.ParticleCapacity);
        Assert.Equal(1, kernel.SourceStrideWords);
        Assert.Equal(-1, kernel.SpawnCountSourceOffsetWords);
        Assert.Single(kernel.Attributes);
    }

    [Fact]
    public void RuntimeAssetV4_RejectsKernelCapacityMismatch()
    {
        VFXRuntimeInitializeKernelData kernel = Kernel(
            7, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>());
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(), new[] { "Spawn" }, new[] { input },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 8) },
            Array.Empty<VFXRuntimeOutputEventData>());

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    [Fact]
    public void RuntimeAssetV4_RejectsZeroSourceStrideBeforeNativeDispatch()
    {
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>()) with { SourceStrideWords = 0 };
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(), new[] { "Spawn" }, new[] { input },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 8) },
            Array.Empty<VFXRuntimeOutputEventData>());

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    [Fact]
    public void RuntimeAssetV4_RejectsSpawnCountOffsetWithoutMatchingEventField()
    {
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>(),
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 1);
        var data = new VFXRuntimeAssetData(
            new[] { new VFXRuntimeAttributeData("size", VFXRuntimeValueType.Float, 0, 1) },
            new[] { "Spawn" },
            new[] { new VFXRuntimeInputEventData("Spawn", new[]
            {
                new VFXRuntimeInputEventTargetData(
                    40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
            }) },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 8) },
            Array.Empty<VFXRuntimeOutputEventData>());

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    [Fact]
    public void Metal_ExecutesSameKernelAndPublishesGpuBackend()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4,
            new[] { Attribute("size", VFXRuntimeValueType.Float, Float(1f)) },
            new[] { Source(0, 0, VFXRuntimeValueType.Float),
                Constant(0, VFXRuntimeValueType.Float, Float(2f), VFXRuntimeInitializeComposition.Multiply) });

        Assert.True(Submit(device, Desc(11, 1, 4), kernel, Words(Float(3f))));
        NativeVFXParticleSystem state = Read(device, 11);

        Assert.Equal(2, state.Info.backendKind);
        Assert.Equal(Float(6f), Word(state.AttributeRecords, 0));
    }

    [Fact]
    public void Metal_ExecutesSpawnCountPrefixAndSourceMappingBitExactly()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, 0) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);

        Assert.True(Submit(device, Desc(30, 1, 8, count: 2), kernel,
            Words(Float(2f), Float(4f), Float(3f), Float(7f))));
        NativeVFXParticleSystem state = Read(device, 30);

        Assert.Equal(2, state.Info.backendKind);
        Assert.Equal(5, state.Info.aliveCount);
        Assert.Equal(new[] { 4f, 4f, 7f, 7f, 7f }, Enumerable.Range(0, 5).Select(index =>
            BitConverter.UInt32BitsToSingle(Word(state.AttributeRecords, index))));
    }

    [Fact]
    public void VisualEffect_TwoCpuEventsExpandThroughPrefixPlanInOneTransaction()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            8, new[] { Attribute("size", VFXRuntimeValueType.Float, 0) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);
        var data = new VFXRuntimeAssetData(
            new[]
            {
                new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1),
                new VFXRuntimeAttributeData("size", VFXRuntimeValueType.Float, 1, 1)
            },
            new[] { "Spawn" },
            new[] { new VFXRuntimeInputEventData("Spawn", new[]
            {
                new VFXRuntimeInputEventTargetData(
                    40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
            }) },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 8) },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        using VFXEventAttribute first = effect.CreateVFXEventAttribute()!;
        first.SetFloat("spawnCount", 2f);
        first.SetFloat("size", 4f);
        using VFXEventAttribute second = effect.CreateVFXEventAttribute()!;
        second.SetFloat("spawnCount", 1f);
        second.SetFloat("size", 9f);

        effect.SendEvent("Spawn", first);
        effect.SendEvent("Spawn", second);
        Assert.Equal(2, effect.ProcessInputEvents(device));

        Assert.True(device.TryGetVFXParticleSystem(
            unchecked((ulong)(uint)effect.GetInstanceID()), Shader.PropertyToID("Particles"),
            out NativeVFXParticleSystem? state));
        Assert.Equal(3, state!.Info.aliveCount);
        Assert.Equal(3, effect.aliveParticleCount);
        Assert.Equal(new[] { 4f, 4f, 9f }, Enumerable.Range(0, 3).Select(index =>
            BitConverter.UInt32BitsToSingle(Word(state.AttributeRecords, index))));
    }

    [Fact]
    public void EmptySchemaCpuEvent_StillExecutesOneInitializeRecord()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(0.1f)) },
            new[] { Constant(0, VFXRuntimeValueType.Float, Float(4f)) });
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(), new[] { "Spawn" }, new[] { input },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 4) },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };

        effect.SendEvent("Spawn");
        Assert.Equal(1, effect.ProcessInputEvents(device));

        Assert.True(device.TryGetVFXParticleSystem(
            unchecked((ulong)(uint)effect.GetInstanceID()), Shader.PropertyToID("Particles"),
            out NativeVFXParticleSystem? state));
        Assert.Equal(1, state!.Info.aliveCount);
        Assert.Equal(Float(4f), Word(state.AttributeRecords, 0));
    }

    [Fact]
    public void RuntimeImportedSpawnCount_DefaultsToOneForAttributeAndNullEvent()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4, new[] { Attribute("size", VFXRuntimeValueType.Float, Float(0f)) },
            new[] { Source(0, 1, VFXRuntimeValueType.Float) },
            sourceStrideWords: 2, spawnCountSourceOffsetWords: 0);
        var data = new VFXRuntimeAssetData(
            new[]
            {
                new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1),
                new VFXRuntimeAttributeData("size", VFXRuntimeValueType.Float, 1, 1)
            },
            new[] { "Spawn" },
            new[] { new VFXRuntimeInputEventData("Spawn", new[]
            {
                new VFXRuntimeInputEventTargetData(
                    40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
            }) },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 4) },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        using VFXEventAttribute defaults = effect.CreateVFXEventAttribute()!;

        Assert.Equal(1f, defaults.GetFloat("spawnCount"));
        Assert.Equal(0.1f, defaults.GetFloat("size"));
        effect.SendEvent("Spawn");
        Assert.Equal(1, effect.ProcessInputEvents(device));

        Assert.True(device.TryGetVFXParticleSystem(
            unchecked((ulong)(uint)effect.GetInstanceID()), Shader.PropertyToID("Particles"),
            out NativeVFXParticleSystem? state));
        Assert.Equal(1, state!.Info.aliveCount);
        Assert.Equal(Float(0.1f), Word(state.AttributeRecords, 0));
    }

    [Theory]
    [InlineData(-100f)]
    [InlineData(-10f)]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(0.125f)]
    [InlineData(1f)]
    [InlineData(4f)]
    [InlineData(17f)]
    [InlineData(100f)]
    [InlineData(1000f)]
    public void VisualEffect_DeferredInitializePublishesAtUpdateDependency(float value)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4,
            new[] { Attribute("size", VFXRuntimeValueType.Float, Float(value)) },
            Array.Empty<VFXRuntimeInitializeOperationData>());
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(),
            new[] { "Spawn" },
            new[] { input },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 4) },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        int particleSystemId = Shader.PropertyToID("Particles");

        effect.SendEvent("Spawn");
        Assert.Equal(1, effect.ProcessInputEvents(device, deferInitializeCompletion: true));
        Assert.False(device.TryGetVFXParticleSystemInfo(
            effectId, particleSystemId, out _));

        Assert.Equal(0, effect.UpdateParticleSystems(0f, device));

        Assert.True(device.TryGetVFXParticleSystem(
            effectId, particleSystemId, out NativeVFXParticleSystem? state));
        Assert.Equal(1, state!.Info.aliveCount);
        Assert.Equal(Float(value), Word(state.AttributeRecords, 0));
        Assert.Equal(1, effect.aliveParticleCount);
    }

    [Fact]
    public void VisualEffect_DeferredInitializeQueueDoesNotBlockAnotherDevice()
    {
        using NativeGraphicsDevice firstDevice = CreateDevice(GraphicsDeviceType.Null);
        using NativeGraphicsDevice secondDevice = CreateDevice(GraphicsDeviceType.Null);
        if (firstDevice.Handle == IntPtr.Zero || secondDevice.Handle == IntPtr.Zero) return;
        VFXRuntimeInitializeKernelData kernel = Kernel(
            4,
            new[] { Attribute("size", VFXRuntimeValueType.Float, Float(7f)) },
            Array.Empty<VFXRuntimeInitializeOperationData>());
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", Array.Empty<long>(), Array.Empty<string>(), kernel)
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(),
            new[] { "Spawn" },
            new[] { input },
            new[] { new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 4) },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        int particleSystemId = Shader.PropertyToID("Particles");

        effect.SendEvent("Spawn");
        Assert.Equal(1, effect.ProcessInputEvents(
            firstDevice, deferInitializeCompletion: true));
        Assert.Equal(1, effect.ProcessInputEvents(
            secondDevice, deferInitializeCompletion: true));

        Assert.Equal(0, effect.UpdateParticleSystems(0f, secondDevice));
        Assert.True(secondDevice.TryGetVFXParticleSystem(
            effectId, particleSystemId, out NativeVFXParticleSystem? secondState));
        Assert.Equal(Float(7f), Word(secondState!.AttributeRecords, 0));
        Assert.False(firstDevice.TryGetVFXParticleSystemInfo(
            effectId, particleSystemId, out _));

        Assert.Equal(0, effect.UpdateParticleSystems(0f, firstDevice));
        Assert.True(firstDevice.TryGetVFXParticleSystem(
            effectId, particleSystemId, out NativeVFXParticleSystem? firstState));
        Assert.Equal(Float(7f), Word(firstState!.AttributeRecords, 0));
    }

    private static bool Submit(
        NativeGraphicsDevice device,
        AnityNative.GraphicsVFXInitializeDispatchDesc desc,
        VFXRuntimeInitializeKernelData kernel,
        byte[] source,
        uint seed = 0)
        => device.SubmitVFXInitializeKernels(
            new[] { desc }, new VFXRuntimeInitializeKernelData?[] { kernel }, source, seed);

    private static NativeVFXParticleSystem Read(NativeGraphicsDevice device, ulong effectId)
    {
        Assert.True(device.TryGetVFXParticleSystem(effectId, ParticleSystemId, out NativeVFXParticleSystem? state));
        return state!;
    }

    private static VFXRuntimeInitializeKernelData Kernel(
        uint capacity,
        IReadOnlyList<VFXRuntimeInitializeAttributeData> attributes,
        IReadOnlyList<VFXRuntimeInitializeOperationData> operations,
        bool usesDeadList = false,
        int sourceStrideWords = 1,
        int spawnCountSourceOffsetWords = -1)
    {
        var packedAttributes = new List<VFXRuntimeInitializeAttributeData>(attributes.Count);
        int stride = 0;
        foreach (VFXRuntimeInitializeAttributeData attribute in attributes)
        {
            packedAttributes.Add(attribute with
            {
                Layout = attribute.Layout with { OffsetWords = stride }
            });
            stride += attribute.Layout.SizeWords;
        }
        return new VFXRuntimeInitializeKernelData(
            40, capacity, stride, sourceStrideWords, usesDeadList,
            packedAttributes, operations, spawnCountSourceOffsetWords);
    }

    private static VFXRuntimeInitializeAttributeData Attribute(
        string name, VFXRuntimeValueType type, params uint[] defaults)
    {
        int size = VFXRuntimeAssetData.WordCount(type);
        Assert.Equal(size, defaults.Length);
        return new VFXRuntimeInitializeAttributeData(
            new VFXRuntimeAttributeData(name, type, 0, size), defaults);
    }

    private static VFXRuntimeInitializeOperationData Constant(
        int targetOffset,
        VFXRuntimeValueType type,
        uint value,
        VFXRuntimeInitializeComposition composition = VFXRuntimeInitializeComposition.Overwrite,
        float blend = 1f)
        => new(targetOffset, -1, type,
            VFXRuntimeInitializeValueSource.Constant, composition,
            VFXRuntimeInitializeRandomMode.Off,
            new[] { value }, Array.Empty<uint>(), Float(blend));

    private static VFXRuntimeInitializeOperationData Source(
        int targetOffset, int sourceOffset, VFXRuntimeValueType type)
        => new(targetOffset, sourceOffset, type,
            VFXRuntimeInitializeValueSource.Source,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            Array.Empty<uint>(), Array.Empty<uint>(), Float(1f));

    private static VFXRuntimeInitializeOperationData SystemValue(
        int targetOffset, VFXRuntimeInitializeValueSource source)
        => new(targetOffset, -1, VFXRuntimeValueType.UInt32, source,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            Array.Empty<uint>(), Array.Empty<uint>(), Float(1f));

    private static AnityNative.GraphicsVFXInitializeDispatchDesc Desc(
        ulong effectId, ulong sequence, int strideBytes,
        int start = 0, int count = 1)
        => new()
        {
            effectId = effectId,
            sequence = sequence,
            initializeContextId = 40,
            sourceSpawnerContextId = 30,
            eventNameId = 12,
            particleSystemId = ParticleSystemId,
            spawnSystemId = 13,
            startEventIndex = start,
            recordCount = count,
            strideBytes = strideBytes
        };

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

    private static byte[] LegacyV3RuntimeAsset()
    {
        using var payloadStream = new MemoryStream();
        using (var writer = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0); // Event attributes.
            writer.Write(1); WriteString(writer, "Spawn");
            writer.Write(1); WriteString(writer, "Spawn");
            writer.Write(1);
            writer.Write(40L); WriteString(writer, "Particles");
            writer.Write(0); // Spawner context ids.
            writer.Write(0); // Spawn system names.
            writer.Write(true);
            writer.Write(40L);
            writer.Write(4u);
            writer.Write(1); // Stored attribute stride.
            writer.Write(1); // Source stride; v3 has no spawnCount offset field.
            writer.Write(false);
            writer.Write(1); WriteString(writer, "size");
            writer.Write((byte)VFXRuntimeValueType.Float);
            writer.Write(0); writer.Write(1);
            writer.Write(1); writer.Write(Float(0f));
            writer.Write(0); // Operations.
            writer.Write(1); WriteString(writer, "Particles");
            writer.Write((byte)VFXRuntimeSystemKind.Particle);
            writer.Write(4u);
            writer.Write(0); // Output events.
        }
        byte[] payload = payloadStream.ToArray();
        byte[] hash = SHA256.HashData(payload);
        using var envelopeStream = new MemoryStream();
        using (var envelope = new BinaryWriter(envelopeStream, Encoding.UTF8, leaveOpen: true))
        {
            envelope.Write(0x58564641u);
            envelope.Write(3u);
            envelope.Write(payload.Length);
            envelope.Write(payload);
            envelope.Write(hash);
        }
        return envelopeStream.ToArray();
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static uint Word(byte[] bytes, int wordIndex)
        => BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.AsSpan(wordIndex * sizeof(uint), sizeof(uint)));

    private static uint Float(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private const int ParticleSystemId = 77;
}
