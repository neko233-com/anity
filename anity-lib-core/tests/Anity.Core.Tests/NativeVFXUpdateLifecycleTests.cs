using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXUpdateLifecycleTests
{
    private const string SystemName = "Particles";
    private const int Alive = 0;
    private const int Position = 1;
    private const int Velocity = 4;
    private const int Age = 7;
    private const int Lifetime = 8;
    private const int Mass = 9;
    private const int Seed = 10;
    private const int Size = 11;
    private const int ScaleX = 12;
    private const int ScaleY = 13;
    private const int Stride = 15;

    [Fact]
    public void NativeLayouts_MatchUpdateKernelCAbi()
    {
        Assert.Equal(64, Marshal.SizeOf<AnityNative.GraphicsVFXUpdateKernelDesc>());
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXUpdateOperationDesc>());
        Assert.Equal(528, Marshal.SizeOf<AnityNative.GraphicsVFXUpdateBackendStats>());
        Assert.Equal(48, Marshal.SizeOf<AnityNative.GraphicsVFXUpdateTicketInfo>());
        Assert.Equal(48, Marshal.SizeOf<AnityNative.GraphicsVFXInitializeTicketInfo>());
    }

    [Theory]
    [InlineData(0, 10f)]
    [InlineData(1, 12f)]
    [InlineData(2, 20f)]
    [InlineData(3, 4f)]
    public void SetAttribute_AppliesCompositionInPersistentStore(
        int compositionValue,
        float expected)
    {
        using NativeGraphicsDevice device = Spawn(positionX: 2f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0.25f, Set(Position, VFXRuntimeValueType.Float,
            (VFXRuntimeInitializeComposition)compositionValue, Float(10f), blend: 0.25f)));

        AssertClose(expected, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void SourceSnapshot_ReadsEntryRecordDespiteEarlierWrites()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 2f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f)),
            SourceSet(Velocity, Position, VFXRuntimeValueType.Float)));

        ReadOnlySpan<byte> record = ActiveRecord(Read(device));
        AssertClose(9f, ReadFloat(record, Position));
        AssertClose(2f, ReadFloat(record, Velocity));
    }

    [Fact]
    public void GravityThenEulerIntegration_UsesProgramOrder()
    {
        using NativeGraphicsDevice device = Spawn();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0.5f,
            IntegrateConstant(Velocity, VFXRuntimeValueType.Float3, 0f, -9.81f, 0f),
            IntegrateSource(Position, Velocity, VFXRuntimeValueType.Float3)));

        ReadOnlySpan<byte> record = ActiveRecord(Read(device));
        AssertClose(-4.905f, ReadFloat(record, Velocity + 1));
        AssertClose(-2.4525f, ReadFloat(record, Position + 1));
    }

    [Fact]
    public void AbsoluteForce_DividesByMassAndIntegratesDeltaTime()
    {
        using NativeGraphicsDevice device = Spawn(mass: 2f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0.5f, Operation(
            VFXRuntimeUpdateOperationKind.Force, Velocity, Mass, -1,
            VFXRuntimeValueType.Float3, Values(4f, 0f, 0f))));

        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Velocity));
    }

    [Fact]
    public void RelativeForce_ClampsInterpolationFactorLikeUnityHelper()
    {
        using NativeGraphicsDevice device = Spawn(velocityX: 2f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0.25f, Operation(
            VFXRuntimeUpdateOperationKind.RelativeForce, Velocity, Mass, -1,
            VFXRuntimeValueType.Float3, Values(10f, 0f, 0f), Values(2f))));

        AssertClose(6f, ReadFloat(ActiveRecord(Read(device)), Velocity));
    }

    [Fact]
    public void Drag_WithoutParticleSizeAttenuatesVelocity()
    {
        using NativeGraphicsDevice device = Spawn(velocityX: 8f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0.25f, Operation(
            VFXRuntimeUpdateOperationKind.Drag, Velocity, Mass, -1,
            VFXRuntimeValueType.Float3, Values(2f))));

        AssertClose(4f, ReadFloat(ActiveRecord(Read(device)), Velocity));
    }

    [Fact]
    public void Drag_WithParticleSizeUsesProjectedArea()
    {
        using NativeGraphicsDevice device = Spawn(velocityX: 8f, size: 2f, scaleX: 1f, scaleY: 0.5f);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData drag = new(
            VFXRuntimeUpdateOperationKind.Drag, Velocity, Mass, Size, ScaleX, ScaleY,
            VFXRuntimeValueType.Float3, VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off, false, Values(0.5f), Array.Empty<uint>(), Float(1f));

        Assert.True(Dispatch(device, 0.5f, drag));

        AssertClose(4f, ReadFloat(ActiveRecord(Read(device)), Velocity));
    }

    [Fact]
    public void SkipZeroDelta_SkipsExplicitOperationsAndGenerationStillAdvancesOnce()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 3f);
        if (device.Handle == IntPtr.Zero) return;
        ulong generation = Read(device).Info.generation;

        Assert.True(Dispatch(device, 0f, true,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f))));

        NativeVFXParticleSystem state = Read(device);
        AssertClose(3f, ReadFloat(ActiveRecord(state), Position));
        Assert.True(state.Info.generation > generation);
    }

    [Fact]
    public void AgeThenReap_OnlyPersistsDeathAndRecyclesPhysicalIndex()
    {
        using NativeGraphicsDevice device = Spawn(age: 1f, lifetime: 0.5f);
        if (device.Handle == IntPtr.Zero) return;
        int physicalIndex = ActiveIndex(Read(device));

        Assert.True(Dispatch(device, 0.25f,
            IntegrateConstant(Age, VFXRuntimeValueType.Float, 1f),
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)));

        NativeVFXParticleSystem dead = Read(device);
        Assert.Equal(0, dead.Info.aliveCount);
        Assert.Equal(4, dead.Info.deadCount);
        Assert.Equal(0u, ReadWord(Record(dead, physicalIndex), Alive));
        AssertClose(1f, ReadFloat(Record(dead, physicalIndex), Age));
        Assert.True(SubmitSpawn(device, sequence: 2, age: 0f, lifetime: 2f));
        Assert.Equal(physicalIndex, ActiveIndex(Read(device)));
    }

    [Fact]
    public void AbortFrame_RestoresAttributesDeadListAndGeneration()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        NativeVFXParticleSystem before = Read(device);
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(7f))));
        AssertClose(7f, ReadFloat(ActiveRecord(Read(device)), Position));

        Assert.True(device.AbortVFXEffectFrame(1, frame));

        NativeVFXParticleSystem restored = Read(device);
        AssertClose(1f, ReadFloat(ActiveRecord(restored), Position));
        Assert.Equal(before.Info.generation, restored.Info.generation);
        Assert.Equal(before.DeadList, restored.DeadList);
    }

    [Fact]
    public void CommitFrame_PersistsUpdate()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(7f))));

        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        AssertClose(7f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void KernelBatch_IsAtomicWhenLaterKernelMismatchesStorage()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData set = Set(Position, VFXRuntimeValueType.Float,
            VFXRuntimeInitializeComposition.Overwrite, Float(7f));
        VFXRuntimeUpdateKernelData first = Kernel(new[] { set });
        VFXRuntimeUpdateKernelData invalidSecond = Kernel(new[] { set }) with
        {
            ContextId = 51,
            ParticleCapacity = 3
        };

        Assert.False(device.DispatchVFXUpdateKernels(
            1, new[] { first, invalidSecond }, 0.1f, 17));

        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void RuntimeV15_RoundTripsUpdateProgram()
    {
        VFXRuntimeAssetData source = RuntimeData(Kernel(new[]
        {
            IntegrateSource(Position, Velocity, VFXRuntimeValueType.Float3),
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)
        }));

        byte[] bytes = source.Serialize();
        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(bytes);

        Assert.Equal(15u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)));
        VFXRuntimeUpdateKernelData kernel = Assert.Single(restored.UpdateKernels);
        Assert.Equal(SystemName, kernel.ParticleSystemName);
        Assert.Equal(2, kernel.Operations.Count);
        Assert.Equal(VFXRuntimeUpdateOperationKind.Reap, kernel.Operations[1].Kind);
    }

    [Fact]
    public void RuntimeV15_RejectsUpdateLayoutThatDiffersFromInitialize()
    {
        VFXRuntimeUpdateKernelData mismatched = Kernel(new[]
        {
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(2f))
        }) with { AttributeStrideWords = Stride - 1 };

        Assert.Throws<InvalidDataException>(() => RuntimeData(mismatched).Serialize());
    }

    [Theory]
    [InlineData(0)]
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
    [InlineData(11)]
    [InlineData(12)]
    public void MetalCompute_MatchesCpuReferenceForOrderedProgram(int program)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice cpu = SpawnForProgram(GraphicsDeviceType.Null, program);
        using NativeGraphicsDevice metal = SpawnForProgram(GraphicsDeviceType.Metal, program);
        if (cpu.Handle == IntPtr.Zero || metal.Handle == IntPtr.Zero) return;

        Assert.True(RunProgram(cpu, program));
        Assert.True(RunProgram(metal, program));

        NativeVFXParticleSystem cpuState = Read(cpu);
        NativeVFXParticleSystem metalState = Read(metal);
        Assert.Equal(0, cpuState.Info.backendKind);
        Assert.Equal(2, metalState.Info.backendKind);
        AssertEquivalentParticleState(cpuState, metalState);
    }

    [Fact]
    public void MetalCompute_AbortRestoresCommittedParticleGenerationAndAttributes()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        NativeVFXParticleSystem before = Read(device);
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f))));
        Assert.Equal(2, Read(device).Info.backendKind);

        Assert.True(device.AbortVFXEffectFrame(1, frame));

        NativeVFXParticleSystem restored = Read(device);
        Assert.Equal(before.Info.generation, restored.Info.generation);
        Assert.Equal(before.AttributeRecords, restored.AttributeRecords);
        Assert.Equal(before.DeadList, restored.DeadList);
    }

    [Fact]
    public void MetalCompute_FailedBatchDoesNotLeakPersistentBufferMutation()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData set = Set(Position, VFXRuntimeValueType.Float,
            VFXRuntimeInitializeComposition.Overwrite, Float(7f));
        VFXRuntimeUpdateKernelData first = Kernel(new[] { set });
        VFXRuntimeUpdateKernelData invalidSecond = Kernel(new[] { set }) with
        {
            ContextId = 51,
            ParticleCapacity = 3
        };

        Assert.False(device.DispatchVFXUpdateKernels(
            1, new[] { first, invalidSecond }, 0.1f, 17));
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 2f)));
        AssertClose(1.2f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalCompute_ClearEffectReleasesCacheAndAllowsIdentityReuse()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 4f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(8f))));

        Assert.True(device.ClearVFXEffectState(1));
        Assert.False(device.TryGetVFXParticleSystem(1, Shader.PropertyToID(SystemName), out _));
        Assert.True(SubmitSpawn(device, 1, positionX: 2f));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        NativeVFXParticleSystem recreated = Read(device);
        Assert.Equal(2, recreated.Info.backendKind);
        AssertClose(2.1f, ReadFloat(ActiveRecord(recreated), Position));
    }

    [Fact]
    public void MetalCompute_MultipleDeathsAppendDeterministicPhysicalIndices()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(SubmitSpawn(device, 1, age: 2f, lifetime: 1f));
        Assert.True(SubmitSpawn(device, 2, age: 0f, lifetime: 1f));
        Assert.True(SubmitSpawn(device, 3, age: 3f, lifetime: 1f));

        Assert.True(Dispatch(device, 0f,
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)));

        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(2, state.Info.backendKind);
        Assert.Equal(1, state.Info.aliveCount);
        Assert.Equal(new uint[] { 0, 1, 3 }, state.DeadList);
        Assert.Equal(2, ActiveIndex(state));
    }

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
    public void MetalResidentRing_RotatesAndElidesParticleUploads(int dispatchCount)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;

        for (int index = 0; index < dispatchCount; index++)
            Assert.True(Dispatch(device, 0.1f,
                IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)),
                $"native result: {device.LastVFXUpdateSubmitResult}");

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal((ulong)dispatchCount, stats.dispatchCount);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal((ulong)dispatchCount, stats.operationUploadCount);
        Assert.Equal((ulong)dispatchCount, stats.gpuCopyCount);
        Assert.Equal((ulong)dispatchCount, stats.completionCount);
        Assert.Equal(0ul, stats.synchronousReadbackCount);
        Assert.Equal((ulong)dispatchCount, stats.deadCompactionDispatchCount);
        Assert.Equal((ulong)dispatchCount, stats.residentOnlyPublishCount);
        Assert.Equal(1ul, stats.allocationStateUploadCount);
        Assert.Equal((ulong)dispatchCount, stats.allocationStateGpuCopyCount);
        Assert.Equal((ulong)(dispatchCount - 1),
            stats.allocationStateResidentHitCount);
        Assert.Equal((dispatchCount - 1) % 3, stats.ringIndex);
        Assert.Equal(3, stats.ringSize);
        Assert.Equal(Read(device).Info.generation, stats.residentGeneration);
        Assert.Equal(stats.residentGeneration, stats.allocationStateGeneration);
        Assert.True(stats.particleBufferCapacityBytes >= 4ul * Stride * sizeof(uint));
        Assert.True(stats.operationBufferCapacityBytes >= 80ul);
    }

    [Fact]
    public void MetalResidentGeneration_AbortRestoresGpuSnapshotWithoutUpload()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.Equal(1ul, Stats(device).particleUploadCount);
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.Equal(1ul, Stats(device).particleUploadCount);

        Assert.True(device.AbortVFXEffectFrame(1, frame));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(1ul, stats.residentRestoreCount);
        Assert.Equal(Read(device).Info.generation, stats.residentGeneration);
        AssertClose(1.2f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalResidentGeneration_PrevalidatedFailureSubmitsNoGpuWork()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData set = Set(Position, VFXRuntimeValueType.Float,
            VFXRuntimeInitializeComposition.Overwrite, Float(7f));
        VFXRuntimeUpdateKernelData first = Kernel(new[] { set });
        VFXRuntimeUpdateKernelData invalidSecond = Kernel(new[] { set }) with
        {
            ContextId = 51,
            ParticleCapacity = 3
        };
        Assert.False(device.DispatchVFXUpdateKernels(
            1, new[] { first, invalidSecond }, 0.1f, 17));
        Assert.False(device.TryGetVFXUpdateBackendStats(
            1, Shader.PropertyToID(SystemName), out _));

        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 2f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(1ul, stats.dispatchCount);
        AssertClose(1.2f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalResidentGeneration_InitializeMutationKeepsResidentBuffers()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.Equal(1ul, Stats(device).particleUploadCount);

        Assert.True(SubmitSpawn(device, 2, positionX: 4f));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(1ul, stats.residentInitializeCount);
        Assert.Equal(1ul, stats.residentInitializeSpawnCount);
        Assert.Equal(Read(device).Info.generation, stats.residentGeneration);
    }

    [Fact]
    public void MetalResidentRing_GrowsOperationSlotWithoutParticleReupload()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        ulong initialCapacity = Stats(device).operationBufferCapacityBytes;
        VFXRuntimeUpdateOperationData[] operations = Enumerable.Range(0, 80)
            .Select(_ => Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Add, Float(0.01f)))
            .ToArray();

        Assert.True(Dispatch(device, 0.1f, operations));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.True(stats.operationBufferCapacityBytes > initialCapacity);
        Assert.True(stats.operationBufferCapacityBytes >= 80ul * 80ul);
    }

    [Fact]
    public void MetalResidentRing_ClearRemovesStatsAndRecreateStartsFresh()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.Equal(1ul, Stats(device).dispatchCount);

        Assert.True(device.ClearVFXEffectState(1));
        Assert.False(device.TryGetVFXUpdateBackendStats(
            1, Shader.PropertyToID(SystemName), out _));
        Assert.True(SubmitSpawn(device, 1, positionX: 3f));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.dispatchCount);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(0, stats.ringIndex);
    }

    [Fact]
    public async Task MetalResidentRing_ConcurrentDispatchesSerializeWithoutExtraUploads()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Task<bool>[] work = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => Dispatch(device, 0.1f,
                IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f))))
            .ToArray();

        bool[] results = await Task.WhenAll(work);

        Assert.All(results, Assert.True);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(12ul, stats.dispatchCount);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(12ul, stats.completionCount);
        AssertClose(2.2f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalResidentRing_ZeroDeltaStillCompletesWithoutReupload()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        Assert.True(Dispatch(device, 0f, true,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f))));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(2ul, stats.dispatchCount);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(2ul, stats.completionCount);
        AssertClose(1.1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalResidentOnly_CompleteDefersFullParticleReadback()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(0ul, stats.synchronousReadbackCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackBytes);
        Assert.Equal(1ul, stats.residentOnlyPublishCount);
    }

    [Fact]
    public void MetalResidentOnly_FirstExplicitReadbackMaterializesOnce()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        NativeVFXParticleSystem state = Read(device);

        AssertClose(1.1f, ReadFloat(ActiveRecord(state), Position));
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.deferredParticleReadbackCount);
        Assert.Equal((ulong)(4 * Stride * sizeof(uint)),
            stats.deferredParticleReadbackBytes);
    }

    [Fact]
    public void MetalResidentOnly_RepeatedExplicitReadUsesMaterializedCopy()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        _ = Read(device);
        _ = Read(device);
        _ = Read(device);

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.deferredParticleReadbackCount);
        Assert.Equal((ulong)(4 * Stride * sizeof(uint)),
            stats.deferredParticleReadbackBytes);
    }

    [Fact]
    public void MetalResidentOnly_ConsecutiveGpuGenerationsAvoidReadbackAndReupload()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;

        for (int frame = 0; frame < 6; frame++)
            Assert.True(Dispatch(device, 0.1f,
                IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(6ul, stats.dispatchCount);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        Assert.Equal(6ul, stats.residentOnlyPublishCount);
        Assert.Equal(stats.residentGeneration, Read(device).Info.generation);
    }

    [Fact]
    public void MetalResidentOnly_ReadbackThenNextUpdateStillUsesResidentGeneration()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        _ = Read(device);

        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.particleUploadCount);
        Assert.Equal(1ul, stats.deferredParticleReadbackCount);
        AssertClose(1.2f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.Equal(2ul, Stats(device).deferredParticleReadbackCount);
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
    public void MetalResidentOnly_InitializeMutatesGpuStateWithoutParticleReadback(
        float spawnPosition)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        Assert.True(SubmitSpawn(device, 2, positionX: spawnPosition));
        AnityNative.GraphicsVFXUpdateBackendStats initialized = Stats(device);
        Assert.Equal(0ul, initialized.deferredParticleReadbackCount);
        Assert.Equal(1ul, initialized.particleUploadCount);
        Assert.Equal(1ul, initialized.residentInitializeCount);
        Assert.Equal(1ul, initialized.residentInitializeSpawnCount);
        Assert.Equal((ulong)(4 * Stride * sizeof(uint)),
            initialized.residentInitializeReadbackAvoidedBytes);
        Assert.Equal(1ul,
            initialized.residentInitializeAllocationStateReadCount);
        Assert.Equal(1ul,
            initialized.residentInitializeIndirectDispatchCount);
        Assert.Equal(1ul,
            initialized.residentInitializeIndirectPreparationCount);
        Assert.Equal(1ul,
            initialized.residentInitializeSourceStateGpuCopyCount);
        Assert.Equal(0ul, initialized.initializeCpuDispatchSizingCount);
        Assert.Equal(1ul, initialized.residentInitializeTargetCopyCount);
        Assert.Equal((ulong)(4 * Stride * sizeof(uint) +
            4 * sizeof(uint) + 4 * sizeof(uint)),
            initialized.residentInitializeTargetCopyBytes);
        Assert.Equal(1ul,
            initialized.residentInitializeAtomicPublishCount);
        Assert.Equal(1ul, initialized.asynchronousInitializeBeginCount);
        Assert.Equal(0ul, initialized.asynchronousInitializePollCount);
        Assert.Equal(1ul,
            initialized.asynchronousInitializeCompletionCount);
        Assert.Equal(0ul, initialized.asynchronousInitializeCancelCount);
        Assert.Equal(initialized.residentGeneration,
            initialized.allocationStateGeneration);
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AnityNative.GraphicsVFXUpdateBackendStats updated = Stats(device);
        Assert.Equal(0ul, updated.deferredParticleReadbackCount);
        Assert.Equal(1ul, updated.particleUploadCount);
        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(2, state.Info.aliveCount);
        AssertClose(1.2f, ReadFloat(Record(state, 3), Position));
        AssertClose(spawnPosition + 0.1f,
            ReadFloat(Record(state, 2), Position));
        AnityNative.GraphicsVFXUpdateBackendStats readback = Stats(device);
        Assert.Equal(1ul, readback.allocationStateReadbackCount);
        Assert.Equal(1ul, readback.deadListReadbackCount);
        Assert.Equal(24ul, readback.metadataReadbackBytes);
        Assert.Equal(state.Info.generation,
            readback.metadataReadbackGeneration);
    }

    [Theory]
    [InlineData(true, -20f)]
    [InlineData(true, -1f)]
    [InlineData(true, 0f)]
    [InlineData(true, 2.5f)]
    [InlineData(true, 100f)]
    [InlineData(false, -20f)]
    [InlineData(false, -1f)]
    [InlineData(false, 0f)]
    [InlineData(false, 2.5f)]
    [InlineData(false, 100f)]
    public void MetalResidentInitialize_PreparedFrameUsesGpuSnapshot(
        bool abort, float spawnPosition)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(
            1, frame, 0.1f, out _));

        Assert.True(SubmitSpawn(device, 2, positionX: spawnPosition));
        AnityNative.GraphicsVFXUpdateBackendStats staged = Stats(device);
        Assert.Equal(1ul, staged.residentInitializeCount);
        Assert.Equal(1ul, staged.residentInitializeIndirectDispatchCount);
        Assert.Equal(1ul,
            staged.residentInitializeSourceStateGpuCopyCount);
        Assert.Equal(0ul, staged.initializeCpuDispatchSizingCount);
        Assert.Equal(1ul, staged.residentInitializeTargetCopyCount);
        Assert.Equal((ulong)(4 * Stride * sizeof(uint) +
            4 * sizeof(uint) + 4 * sizeof(uint)),
            staged.residentInitializeTargetCopyBytes);
        Assert.Equal(1ul, staged.residentInitializeAtomicPublishCount);
        Assert.Equal(1ul, staged.asynchronousInitializeBeginCount);
        Assert.Equal(0ul, staged.asynchronousInitializePollCount);
        Assert.Equal(1ul, staged.asynchronousInitializeCompletionCount);
        Assert.Equal(0ul, staged.asynchronousInitializeCancelCount);
        Assert.Equal(1ul, staged.residentSnapshotCount);
        Assert.Equal(0ul, staged.deferredParticleReadbackCount);

        if (abort)
            Assert.True(device.AbortVFXEffectFrame(1, frame));
        else
            Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        NativeVFXParticleSystem state = Read(device);
        AnityNative.GraphicsVFXUpdateBackendStats completed = Stats(device);
        Assert.Equal(1ul, completed.particleUploadCount);
        if (abort)
        {
            Assert.Equal(1, state.Info.aliveCount);
            Assert.Equal(3, state.Info.deadCount);
            AssertClose(1.2f, ReadFloat(Record(state, 3), Position));
            Assert.Equal(1ul, completed.residentRestoreCount);
        }
        else
        {
            Assert.Equal(2, state.Info.aliveCount);
            Assert.Equal(2, state.Info.deadCount);
            AssertClose(1.2f, ReadFloat(Record(state, 3), Position));
            AssertClose(spawnPosition + 0.1f,
                ReadFloat(Record(state, 2), Position));
            Assert.True(completed.residentSnapshotDiscardCount >= 1ul);
        }
    }

    [Theory]
    [InlineData(true, -20f)]
    [InlineData(true, -1f)]
    [InlineData(true, 0f)]
    [InlineData(true, 2.5f)]
    [InlineData(true, 100f)]
    [InlineData(false, -20f)]
    [InlineData(false, -1f)]
    [InlineData(false, 0f)]
    [InlineData(false, 2.5f)]
    [InlineData(false, 100f)]
    public void MetalInitializeTicket_PollThenCompleteOrCancelIsAtomic(
        bool cancel, float spawnPosition)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        AnityNative.GraphicsVFXUpdateBackendStats sourceStats = Stats(device);

        Assert.True(BeginSpawn(
            device, 2, spawnPosition, out ulong ticket));
        Assert.NotEqual(0ul, ticket);
        Assert.True(device.TryGetVFXInitializeTicketInfo(ticket, out var info));
        Assert.Equal(ticket, info.ticketId);
        Assert.Equal(1ul, info.effectId);
        Assert.Equal(1, info.dispatchCount);
        Assert.Equal(1, info.effectCount);
        Assert.Equal(2, info.backendKind);
        Assert.True(info.targetRegistryGeneration > info.sourceRegistryGeneration);
        Assert.InRange(info.state, 0, 1);
        AnityNative.GraphicsVFXUpdateBackendStats queued = Stats(device);
        Assert.True(queued.residentGeneration > sourceStats.residentGeneration);
        Assert.Equal(queued.residentGeneration,
            queued.allocationStateGeneration);
        Assert.Equal(1ul,
            queued.asynchronousInitializeResidentPublishCount);
        Assert.Equal(0ul,
            queued.asynchronousInitializeResidentCompletionCount);
        Assert.Equal(0ul,
            queued.asynchronousInitializeResidentRollbackCount);
        Assert.Equal(1ul, queued.pendingInitializeCount);
        Assert.Equal(0ul, queued.residentInitializeAtomicPublishCount);
        Assert.True(device.TryGetVFXParticleSystemInfo(
            1, Shader.PropertyToID(SystemName),
            out var committedBeforeRetirement));
        Assert.Equal(1, committedBeforeRetirement.aliveCount);
        Assert.Equal(sourceStats.residentGeneration,
            committedBeforeRetirement.generation);

        if (cancel)
            Assert.True(device.CancelVFXInitializeKernels(ticket));
        else
            Assert.True(device.CompleteVFXInitializeKernels(ticket));
        Assert.False(device.TryGetVFXInitializeTicketInfo(ticket, out _));

        NativeVFXParticleSystem state = Read(device);
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.asynchronousInitializeBeginCount);
        Assert.True(stats.asynchronousInitializePollCount >= 1ul);
        if (cancel)
        {
            Assert.Equal(1, state.Info.aliveCount);
            AssertClose(1.1f, ReadFloat(Record(state, 3), Position));
            Assert.Equal(0ul, stats.asynchronousInitializeCompletionCount);
            Assert.Equal(1ul, stats.asynchronousInitializeCancelCount);
            Assert.Equal(0ul, stats.residentInitializeAtomicPublishCount);
            Assert.Equal(sourceStats.residentGeneration,
                stats.residentGeneration);
            Assert.Equal(0ul,
                stats.asynchronousInitializeResidentCompletionCount);
            Assert.Equal(1ul,
                stats.asynchronousInitializeResidentRollbackCount);
        }
        else
        {
            Assert.Equal(2, state.Info.aliveCount);
            AssertClose(spawnPosition, ReadFloat(Record(state, 2), Position));
            Assert.Equal(1ul, stats.asynchronousInitializeCompletionCount);
            Assert.Equal(0ul, stats.asynchronousInitializeCancelCount);
            Assert.Equal(1ul, stats.residentInitializeAtomicPublishCount);
            Assert.Equal(queued.residentGeneration,
                stats.residentGeneration);
            Assert.Equal(1ul,
                stats.asynchronousInitializeResidentCompletionCount);
            Assert.Equal(0ul,
                stats.asynchronousInitializeResidentRollbackCount);
        }
        Assert.Equal(1ul,
            stats.asynchronousInitializeResidentPublishCount);
        Assert.Equal(0ul, stats.pendingInitializeCount);
    }

    [Fact]
    public void MetalInitializeTicket_BlocksSecondInitializeAndCancelsDependentUpdate()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.True(BeginSpawn(device, 2, 3f, out ulong ticket));

        Assert.False(BeginSpawn(device, 3, 4f, out ulong rejected));
        Assert.Equal(0ul, rejected);
        var kernel = Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)
        });
        Assert.True(device.BeginVFXUpdateKernels(
            1, new[] { kernel }, 0.1f, 17, out ulong updateTicket),
            $"native result: {device.LastVFXUpdateSubmitResult}");
        Assert.NotEqual(0ul, updateTicket);
        Assert.True(device.CancelVFXInitializeKernels(ticket));
        Assert.False(device.TryGetVFXUpdateTicketInfo(updateTicket, out _));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
    }

    [Theory]
    [InlineData(-100f)]
    [InlineData(-20f)]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(1f)]
    [InlineData(2.5f)]
    [InlineData(10f)]
    [InlineData(100f)]
    [InlineData(1000f)]
    public void MetalInitializeTicket_UpdateQueuesBeforeCpuRetirement(
        float spawnPosition)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(
            1, frame, 0.1f, out _));
        Assert.True(BeginSpawn(
            device, 2, spawnPosition, out ulong initializeTicket));
        var kernel = Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)
        });

        Assert.True(device.BeginVFXUpdateKernels(
            1, new[] { kernel }, 0.1f, 17, out ulong updateTicket));

        Assert.True(device.TryGetVFXInitializeTicketInfo(
            initializeTicket, out _));
        Assert.True(device.TryGetVFXUpdateTicketInfo(updateTicket, out _));
        Assert.Equal(1ul, Stats(device).pendingInitializeCount);
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));
        Assert.Equal(1ul, Stats(device).pendingUpdateCount);
        Assert.True(device.CompleteVFXUpdateKernels(updateTicket));
        Assert.False(device.TryGetVFXInitializeTicketInfo(
            initializeTicket, out _));
        Assert.False(device.TryGetVFXUpdateTicketInfo(updateTicket, out _));
        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(2, state.Info.aliveCount);
        AssertClose(spawnPosition + 0.1f,
            ReadFloat(Record(state, 2), Position));
        AssertClose(1.2f, ReadFloat(Record(state, 3), Position));
        Assert.Equal(0ul, Stats(device).pendingInitializeCount);
    }

    [Theory]
    [InlineData(-100f)]
    [InlineData(-20f)]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(1f)]
    [InlineData(2.5f)]
    [InlineData(10f)]
    [InlineData(100f)]
    [InlineData(1000f)]
    public void MetalFailureInjection_UpdateFailureRollsBackWholeInitializeChain(
        float spawnPosition)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        NativeVFXParticleSystem baseline = Read(device);
        Assert.Equal(baseline.Info.generation, Stats(device).residentGeneration);
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(
            1, frame, 0.1f, out _));
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand));
        Assert.True(BeginSpawn(
            device, 2, spawnPosition, out ulong initializeTicket));
        VFXRuntimeUpdateKernelData kernel = Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)
        });
        Assert.True(device.BeginVFXUpdateKernels(
            1, new[] { kernel }, 0.1f, 17, out ulong updateTicket),
            $"native result: {device.LastVFXUpdateSubmitResult}");
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));
        Assert.True(device.TryGetVFXUpdateTicketInfo(updateTicket, out var pending));
        Assert.Equal(2, pending.state);

        Assert.False(device.CompleteVFXUpdateKernels(updateTicket));

        Assert.False(device.TryGetVFXInitializeTicketInfo(initializeTicket, out _));
        Assert.False(device.TryGetVFXUpdateTicketInfo(updateTicket, out _));
        NativeVFXParticleSystem restored = Read(device);
        Assert.Equal(baseline.Info.generation, restored.Info.generation);
        Assert.Equal(baseline.Info.aliveCount, restored.Info.aliveCount);
        Assert.Equal(baseline.Info.deadCount, restored.Info.deadCount);
        Assert.Equal(baseline.AttributeRecords, restored.AttributeRecords);
        Assert.Equal(baseline.DeadList, restored.DeadList);
        AnityNative.GraphicsVFXUpdateBackendStats rolledBack = Stats(device);
        Assert.Equal(0ul, rolledBack.pendingInitializeCount);
        Assert.Equal(0ul, rolledBack.pendingUpdateCount);
        Assert.True(rolledBack.asynchronousResidentRollbackCount >= 1ul);
        Assert.True(rolledBack.residentSnapshotCount >= 1ul);

        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        NativeVFXParticleSystem recovered = Read(device);
        Assert.Equal(1, recovered.Info.aliveCount);
        AssertClose(1.2f, ReadFloat(ActiveRecord(recovered), Position));
    }

    [Fact]
    public void MetalFailureInjection_InitializeFailureIsAtomicAndRecoverable()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        NativeVFXParticleSystem baseline = Read(device);
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.InitializeCommand));
        Assert.True(BeginSpawn(device, 2, 9f, out ulong failedTicket));
        Assert.True(device.TryGetVFXInitializeTicketInfo(
            failedTicket, out var pending));
        Assert.Equal(2, pending.state);

        Assert.False(device.CompleteVFXInitializeKernels(failedTicket));

        Assert.False(device.TryGetVFXInitializeTicketInfo(failedTicket, out _));
        NativeVFXParticleSystem restored = Read(device);
        Assert.Equal(baseline.Info.generation, restored.Info.generation);
        Assert.Equal(baseline.AttributeRecords, restored.AttributeRecords);
        Assert.Equal(baseline.DeadList, restored.DeadList);
        Assert.Equal(0ul, Stats(device).pendingInitializeCount);
        Assert.True(BeginSpawn(device, 3, 7f, out ulong recoveryTicket));
        Assert.True(device.CompleteVFXInitializeKernels(recoveryTicket));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        NativeVFXParticleSystem recovered = Read(device);
        Assert.Equal(2, recovered.Info.aliveCount);
        AssertClose(1.2f, ReadFloat(Record(recovered, 3), Position));
        AssertClose(7.1f, ReadFloat(Record(recovered, 2), Position));
    }

    [Fact]
    public void MetalFailureInjection_CountIsConsumedBySuccessfulBegins()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand, 2));
        VFXRuntimeUpdateOperationData operation =
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f);

        ulong first = BeginUpdate(device, 0.1f, operation);
        Assert.False(device.CompleteVFXUpdateKernels(first));
        ulong second = BeginUpdate(device, 0.1f, operation);
        Assert.False(device.CompleteVFXUpdateKernels(second));
        ulong recovered = BeginUpdate(device, 0.1f, operation);
        Assert.True(device.CompleteVFXUpdateKernels(recovered));

        AssertClose(1.1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalFailureInjection_ZeroCountDisarmsFailure()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand));
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand, 0));

        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        AssertClose(1.1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalFailureInjection_ClearCancelsInjectedSubmissionAndAllowsReuse()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));

        Assert.True(device.ClearVFXEffectState(1));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.True(SubmitSpawn(device, 1, positionX: 4f));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        AssertClose(4.1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalFailureInjection_DisposeWaitsForInjectedSubmission()
    {
        if (!OperatingSystem.IsMacOS()) return;
        NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero)
        {
            device.Dispose();
            return;
        }
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand));
        _ = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));

        device.Dispose();
        device.Dispose();

        Assert.False(device.IsValid);
    }

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
    public void MetalDeviceRemoval_PoisonsDeviceAfterLongGenerationChain(
        int successfulGenerationCount)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.CreateSwapchain(32, 32, imageCount: 3, hdr: false));
        VFXRuntimeUpdateOperationData operation =
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f);
        for (int generation = 0; generation < successfulGenerationCount; ++generation)
            Assert.True(Dispatch(device, 0.01f, operation));
        Assert.True(device.TryGetVFXParticleSystemInfo(
            1, Shader.PropertyToID(SystemName), out var baseline));
        Assert.True(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.DeviceRemoval));

        Assert.False(Dispatch(device, 0.01f, operation));

        Assert.True(device.TryGetVFXParticleSystemInfo(
            1, Shader.PropertyToID(SystemName), out var restored));
        Assert.Equal(baseline.generation, restored.generation);
        Assert.True(device.TryGetVFXPlanarSubmissionStats(out var health));
        Assert.Equal(1, health.deviceLost);
        Assert.Equal(AnityNative.Result.DeviceLost,
            device.WaitForVFXPlanarSubmissions(0, 0));
        Assert.False(Dispatch(device, 0.01f, operation));
        Assert.False(device.InjectVFXFailure(
            AnityNative.GraphicsVFXFailurePoint.UpdateCommand));
        Assert.Equal(AnityNative.Result.DeviceLost,
            AnityNative.Graphics_AcquireNextImage(
                device.SwapchainHandle, out _));
        Assert.Equal(AnityNative.Result.DeviceLost,
            AnityNative.Graphics_PresentSwapchain(device.SwapchainHandle));
        Assert.Equal(AnityNative.Result.DeviceLost,
            AnityNative.Graphics_ReadbackSwapchainRGBA8(
                device.SwapchainHandle, new byte[32 * 32 * 4],
                32 * 32 * 4, out _));
        Assert.Equal(-1, device.AcquireNextImage());
        Assert.Equal(AnityNative.Result.DeviceLost, device.LastSwapchainResult);
        int presentCount = device.PresentCount;
        device.Present();
        Assert.Equal(presentCount, device.PresentCount);
        Assert.Equal(AnityNative.Result.DeviceLost, device.LastSwapchainResult);
        Assert.False(device.TryReadbackSwapchainRGBA8(out _));
        Assert.Equal(AnityNative.Result.DeviceLost, device.LastSwapchainResult);
    }

    [Fact]
    public void MetalInitializeTicket_ClearCancelsPendingGpuWork()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.True(BeginSpawn(device, 2, 3f, out ulong ticket));

        Assert.True(device.ClearVFXEffectState(1));
        Assert.False(device.TryGetVFXInitializeTicketInfo(ticket, out _));
        Assert.False(device.TryGetVFXParticleSystemInfo(
            1, Shader.PropertyToID(SystemName), out _));
    }

    [Fact]
    public void MetalInitializeTicket_DeviceDisposeCancelsPendingGpuWork()
    {
        if (!OperatingSystem.IsMacOS()) return;
        NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero)
        {
            device.Dispose();
            return;
        }
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.True(BeginSpawn(device, 2, 3f, out _));
        device.Dispose();
        Assert.False(device.IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CpuInitializeTicket_IsReadyAndPublishesOnlyOnComplete(bool cancel)
    {
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Null, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(BeginSpawn(device, 2, 8f, out ulong ticket));
        Assert.True(device.TryGetVFXInitializeTicketInfo(ticket, out var info));
        Assert.Equal(1, info.state);
        Assert.Equal(0, info.backendKind);

        if (cancel)
            Assert.True(device.CancelVFXInitializeKernels(ticket));
        else
            Assert.True(device.CompleteVFXInitializeKernels(ticket));
        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(cancel ? 1 : 2, state.Info.aliveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MetalInitializeTicket_MultiSystemBatchPublishesAtomically(bool cancel)
    {
        if (!OperatingSystem.IsMacOS()) return;
        const string secondSystem = "TicketSystemB";
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(SubmitSpawn(device, 1, positionX: 2f,
            systemName: secondSystem, initializeContextId: 41, spawnSystemId: 14));
        var operation = IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f);
        Assert.True(device.DispatchVFXUpdateKernels(1, new[]
        {
            Kernel(new[] { operation }),
            Kernel(new[] { operation }, secondSystem, 51)
        }, 0.1f, 17));

        Assert.True(BeginSpawnBatch(device, new[]
        {
            (SystemName, 40L, 10f),
            (secondSystem, 41L, 20f)
        }, out ulong ticket));
        Assert.True(device.TryGetVFXInitializeTicketInfo(ticket, out var info));
        Assert.Equal(2, info.dispatchCount);
        Assert.Equal(1, info.effectCount);
        if (cancel)
            Assert.True(device.CancelVFXInitializeKernels(ticket));
        else
            Assert.True(device.CompleteVFXInitializeKernels(ticket));

        NativeVFXParticleSystem first = Read(device);
        NativeVFXParticleSystem second = Read(device, secondSystem);
        Assert.Equal(cancel ? 1 : 2, first.Info.aliveCount);
        Assert.Equal(cancel ? 1 : 2, second.Info.aliveCount);
        if (!cancel)
        {
            AssertClose(10f, ReadFloat(Record(first, 2), Position));
            AssertClose(20f, ReadFloat(Record(second, 2), Position));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MetalInitializeTicket_SameSystemChainRollsBackNewestToOldest(bool cancel)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        Assert.True(BeginSpawnBatch(device, new[]
        {
            (SystemName, 40L, 10f),
            (SystemName, 41L, 20f)
        }, out ulong ticket));
        if (cancel)
            Assert.True(device.CancelVFXInitializeKernels(ticket));
        else
            Assert.True(device.CompleteVFXInitializeKernels(ticket));

        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(cancel ? 1 : 3, state.Info.aliveCount);
        if (cancel)
        {
            AssertClose(1.1f, ReadFloat(Record(state, 3), Position));
            Assert.True(Stats(device).residentRestoreCount >= 1ul);
        }
        else
        {
            AssertClose(10f, ReadFloat(Record(state, 2), Position));
            AssertClose(20f, ReadFloat(Record(state, 1), Position));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MetalInitializeTicket_IndependentEffectsMergeWithoutClobber(bool cancelFirst)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(SubmitSpawn(device, 1, positionX: 2f, effectId: 2));
        var operation = IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f);
        Assert.True(device.DispatchVFXUpdateKernels(
            1, new[] { Kernel(new[] { operation }) }, 0.1f, 17));
        Assert.True(device.DispatchVFXUpdateKernels(
            2, new[] { Kernel(new[] { operation }) }, 0.1f, 17));

        Assert.True(BeginSpawn(device, 2, 10f, out ulong firstTicket, 1));
        Assert.True(BeginSpawn(device, 2, 20f, out ulong secondTicket, 2));
        Assert.NotEqual(firstTicket, secondTicket);
        if (cancelFirst)
            Assert.True(device.CancelVFXInitializeKernels(firstTicket));
        else
            Assert.True(device.CompleteVFXInitializeKernels(firstTicket));
        Assert.True(device.CompleteVFXInitializeKernels(secondTicket));

        NativeVFXParticleSystem first = Read(device, effectId: 1);
        NativeVFXParticleSystem second = Read(device, effectId: 2);
        Assert.Equal(cancelFirst ? 1 : 2, first.Info.aliveCount);
        Assert.Equal(2, second.Info.aliveCount);
        AssertClose(20f, ReadFloat(Record(second, 2), Position));
    }

    [Fact]
    public void MetalDeathCompaction_NoDeathsStillRunsGpuCompactionWithoutReadback()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, age: 0f, lifetime: 1f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(Dispatch(device, 0f,
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(2ul, stats.deadPrefixPassCount);
        Assert.Equal(1ul, stats.deadCompactionDispatchCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        Assert.Equal(1, Read(device).Info.aliveCount);
    }

    [Fact]
    public void MetalDeathCompaction_CapacityOneNeedsNoPrefixPass()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnCapacity(1, _ => 2f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(DispatchCapacity(device, 1,
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(0ul, stats.deadPrefixPassCount);
        Assert.Equal(1ul, stats.deadCompactionDispatchCount);
        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(0, state.Info.aliveCount);
        Assert.Equal(new uint[] { 0 }, state.DeadList);
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    [InlineData(7, 3)]
    public void MetalDeathCompaction_NonPowerOfTwoCapacityPreservesPhysicalOrder(
        int capacity,
        int expectedPrefixPasses)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnCapacity(
            capacity, index => index % 2 == 0 ? 2f : 0f);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(DispatchCapacity(device, capacity,
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal((ulong)expectedPrefixPasses, stats.deadPrefixPassCount);
        Assert.Equal(1ul, stats.deadCompactionDispatchCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        NativeVFXParticleSystem state = Read(device);
        uint[] expected = Enumerable.Range(0, capacity)
            .Where(index => index % 2 == 0)
            .Select(index => (uint)index)
            .ToArray();
        Assert.Equal(expected, state.DeadList);
        Assert.Equal(capacity - expected.Length, state.Info.aliveCount);
    }

    [Fact]
    public void MetalResidentSnapshot_CommitDiscardsPreparedGeneration()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.Equal(1ul, Stats(device).residentSnapshotCount);

        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(0ul, stats.residentRestoreCount);
        Assert.Equal(1ul, stats.residentSnapshotDiscardCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        AssertClose(1.1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalResidentSnapshot_AbortRestoresWithoutParticleReadback()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        Assert.True(device.AbortVFXEffectFrame(1, frame));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.residentSnapshotCount);
        Assert.Equal(1ul, stats.residentRestoreCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        Assert.Equal(1ul, stats.particleUploadCount);
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.Equal(0ul, Stats(device).deferredParticleReadbackCount);
    }

    [Fact]
    public void MetalResidentSnapshot_AbortRestoresOldestAndDiscardsNewerSnapshot()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));
        Assert.Equal(2ul, Stats(device).residentSnapshotCount);

        Assert.True(device.AbortVFXEffectFrame(1, frame));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.residentRestoreCount);
        Assert.Equal(1ul, stats.residentSnapshotDiscardCount);
        Assert.Equal(0ul, stats.deferredParticleReadbackCount);
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalResidentOnly_BatchMaterializesSystemsIndependently()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(2);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(DispatchBatch(device, 2, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));
        Assert.Equal(0ul, Stats(device, BatchSystem(0)).deferredParticleReadbackCount);
        Assert.Equal(0ul, Stats(device, BatchSystem(1)).deferredParticleReadbackCount);

        _ = Read(device, BatchSystem(0));

        Assert.Equal(1ul, Stats(device, BatchSystem(0)).deferredParticleReadbackCount);
        Assert.Equal(0ul, Stats(device, BatchSystem(1)).deferredParticleReadbackCount);
        _ = Read(device, BatchSystem(1));
        Assert.Equal(1ul, Stats(device, BatchSystem(1)).deferredParticleReadbackCount);
    }

    [Fact]
    public void NullBackend_DoesNotExposeMetalResidentStats()
    {
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(Dispatch(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)));

        Assert.False(device.TryGetVFXUpdateBackendStats(
            1, Shader.PropertyToID(SystemName), out _));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void MetalAsyncBatch_SubmitsAllSystemsBeforeCompletion(int width)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(width);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(DispatchBatch(device, width, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));

        for (int index = 0; index < width; index++)
        {
            string name = BatchSystem(index);
            AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device, name);
            Assert.Equal(width, stats.lastBatchWidth);
            Assert.Equal(width, stats.peakBatchWidth);
            Assert.Equal(1ul, stats.asyncBatchCount);
            Assert.Equal(1ul, stats.dispatchCount);
            Assert.Equal(1ul, stats.completionCount);
            Assert.Equal(0, stats.ringIndex);
            AssertClose(index + 1.1f,
                ReadFloat(ActiveRecord(Read(device, name)), Position));
        }
    }

    [Fact]
    public void MetalAsyncBatch_ConsecutiveFramesRotateEverySystemRing()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(3);
        if (device.Handle == IntPtr.Zero) return;
        for (int frame = 0; frame < 5; frame++)
            Assert.True(DispatchBatch(device, 3, 0.1f,
                _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));

        for (int index = 0; index < 3; index++)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(5ul, stats.dispatchCount);
            Assert.Equal(1ul, stats.particleUploadCount);
            Assert.Equal(5ul, stats.asyncBatchCount);
            Assert.Equal(1, stats.ringIndex);
            AssertClose(index + 1.5f,
                ReadFloat(ActiveRecord(Read(device, BatchSystem(index))), Position));
        }
    }

    [Fact]
    public void MetalAsyncBatch_AbortRestoresAllGpuSnapshotsWithoutReupload()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(3);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(DispatchBatch(device, 3, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        Assert.True(DispatchBatch(device, 3, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));

        Assert.True(device.AbortVFXEffectFrame(1, frame));
        Assert.True(DispatchBatch(device, 3, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));

        for (int index = 0; index < 3; index++)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(1ul, stats.particleUploadCount);
            Assert.Equal(1ul, stats.residentRestoreCount);
            Assert.Equal(3ul, stats.dispatchCount);
            AssertClose(index + 1.2f,
                ReadFloat(ActiveRecord(Read(device, BatchSystem(index))), Position));
        }
    }

    [Fact]
    public void MetalAsyncBatch_DuplicateSystemIsRejectedBeforeSubmission()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData operation =
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f);
        VFXRuntimeUpdateKernelData first = Kernel(new[] { operation });
        VFXRuntimeUpdateKernelData duplicate = Kernel(
            new[] { operation }, contextId: 51);

        Assert.False(device.DispatchVFXUpdateKernels(
            1, new[] { first, duplicate }, 0.1f, 17));

        Assert.False(device.TryGetVFXUpdateBackendStats(
            1, Shader.PropertyToID(SystemName), out _));
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalAsyncBatch_ClearRemovesEverySystemResource()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(4);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(DispatchBatch(device, 4, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));

        Assert.True(device.ClearVFXEffectState(1));

        for (int index = 0; index < 4; index++)
            Assert.False(device.TryGetVFXUpdateBackendStats(
                1, Shader.PropertyToID(BatchSystem(index)), out _));
    }

    [Fact]
    public void MetalAsyncBatch_MixedDeathsRemainIsolatedPerSystem()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(SubmitSpawn(device, 1, age: 2f, lifetime: 1f,
            systemName: BatchSystem(0), initializeContextId: 100));
        Assert.True(SubmitSpawn(device, 2, age: 0f, lifetime: 1f,
            systemName: BatchSystem(1), initializeContextId: 101));
        Assert.True(SubmitSpawn(device, 3, age: 3f, lifetime: 1f,
            systemName: BatchSystem(2), initializeContextId: 102));

        Assert.True(DispatchBatch(device, 3, 0f, _ => new[]
        {
            Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                VFXRuntimeValueType.Boolean)
        }));

        Assert.Equal(0, Read(device, BatchSystem(0)).Info.aliveCount);
        Assert.Equal(1, Read(device, BatchSystem(1)).Info.aliveCount);
        Assert.Equal(0, Read(device, BatchSystem(2)).Info.aliveCount);
        Assert.Equal(4, Read(device, BatchSystem(0)).Info.deadCount);
        Assert.Equal(3, Read(device, BatchSystem(1)).Info.deadCount);
        Assert.Equal(4, Read(device, BatchSystem(2)).Info.deadCount);
    }

    [Fact]
    public void MetalAsyncBatch_OperationGrowthIsIsolatedPerRing()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(2);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(DispatchBatch(device, 2, 0.1f,
            _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));
        VFXRuntimeUpdateOperationData[] largeProgram = Enumerable.Range(0, 80)
            .Select(_ => Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Add, Float(0.01f)))
            .ToArray();

        Assert.True(DispatchBatch(device, 2, 0.1f, index => index == 0
            ? largeProgram
            : new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) }));

        AnityNative.GraphicsVFXUpdateBackendStats large = Stats(device, BatchSystem(0));
        AnityNative.GraphicsVFXUpdateBackendStats small = Stats(device, BatchSystem(1));
        Assert.True(large.operationBufferCapacityBytes >= 80ul * 80ul);
        Assert.True(large.operationBufferCapacityBytes > small.operationBufferCapacityBytes);
        Assert.Equal(1ul, large.particleUploadCount);
        Assert.Equal(1ul, small.particleUploadCount);
    }

    [Fact]
    public async Task MetalAsyncBatch_ConcurrentBatchesSerializeWithoutCacheRaces()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(2);
        if (device.Handle == IntPtr.Zero) return;
        Task<bool>[] work = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => DispatchBatch(device, 2, 0.1f,
                _ => new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) })))
            .ToArray();

        bool[] results = await Task.WhenAll(work);

        Assert.All(results, Assert.True);
        for (int index = 0; index < 2; index++)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(8ul, stats.dispatchCount);
            Assert.Equal(1ul, stats.particleUploadCount);
            Assert.Equal(8ul, stats.asyncBatchCount);
            AssertClose(index + 1.8f,
                ReadFloat(ActiveRecord(Read(device, BatchSystem(index))), Position));
        }
    }

    [Fact]
    public void MetalAsyncBatch_ZeroDeltaCompletesEverySubmittedSystem()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(2);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateKernelData[] kernels = Enumerable.Range(0, 2)
            .Select(index => Kernel(new[]
            {
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Overwrite, Float(9f))
            }, BatchSystem(index), 500 + index) with { SkipZeroDeltaUpdate = true })
            .ToArray();

        Assert.True(device.DispatchVFXUpdateKernels(1, kernels, 0f, 17));

        for (int index = 0; index < 2; index++)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(2, stats.lastBatchWidth);
            Assert.Equal(1ul, stats.completionCount);
            AssertClose(index + 1f,
                ReadFloat(ActiveRecord(Read(device, BatchSystem(index))), Position));
        }
    }

    [Fact]
    public void AsyncTicket_BeginReportsStableIdentityWithoutPublishing()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;

        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(7f)));

        Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out var info));
        Assert.Equal(ticket, info.ticketId);
        Assert.Equal(1ul, info.effectId);
        Assert.Equal(1, info.kernelCount);
        Assert.Equal(0u, info.frameIndex);
        Assert.Equal(0ul, info.preparedFrameGeneration);
        Assert.True(info.submitGeneration > 0);
        Assert.Equal(1f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.True(device.CancelVFXUpdateKernels(ticket));
    }

    [Fact]
    public void AsyncTicket_CompletePublishesAndRemovesTicket()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(7f)));

        Assert.True(device.CompleteVFXUpdateKernels(ticket));

        AssertClose(7f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.False(device.CompleteVFXUpdateKernels(ticket));
    }

    [Fact]
    public void AsyncTicket_CancelPreservesAuthoritativeStateAndIsSingleUse()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 2f);
        if (device.Handle == IntPtr.Zero) return;
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f)));

        Assert.True(device.CancelVFXUpdateKernels(ticket));

        AssertClose(2f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.False(device.CancelVFXUpdateKernels(ticket));
    }

    [Fact]
    public void AsyncTicket_RejectsSecondPendingUpdateForSameEffect()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateKernelData kernel = Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)
        });
        Assert.True(device.BeginVFXUpdateKernels(1, new[] { kernel }, 0.1f, 17, out ulong ticket));

        Assert.False(device.BeginVFXUpdateKernels(1, new[] { kernel }, 0.1f, 17, out ulong rejected));

        Assert.Equal(0ul, rejected);
        Assert.True(device.CancelVFXUpdateKernels(ticket));
    }

    [Fact]
    public void AsyncTicket_GenerationCasRejectsConcurrentParticleMutation()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(8f)));
        Assert.True(SubmitSpawn(device, 2, positionX: 3f));

        Assert.False(device.CompleteVFXUpdateKernels(ticket));

        NativeVFXParticleSystem state = Read(device);
        Assert.Equal(2, state.Info.aliveCount);
        Assert.DoesNotContain(Float(8f), Enumerable.Range(0, state.Info.capacity)
            .Select(index => ReadWord(Record(state, index), Position)));
        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
    }

    [Fact]
    public void AsyncTicket_PreparedFrameIdentityIsCaptured()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out var prepared));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));

        Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out var info));
        Assert.Equal(frame, info.frameIndex);
        Assert.Equal(prepared.generation, info.preparedFrameGeneration);
        Assert.True(device.AbortVFXEffectFrame(1, frame));
    }

    [Fact]
    public void AsyncTicket_FrameCommitCompletesAndPublishesAtomically()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(6f)));
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));

        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        AssertClose(6f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
    }

    [Fact]
    public void AsyncTicket_FrameAbortCancelsWithoutPublishing()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        NativeVFXParticleSystem before = Read(device);
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(6f)));

        Assert.True(device.AbortVFXEffectFrame(1, frame));

        NativeVFXParticleSystem restored = Read(device);
        Assert.Equal(before.Info.generation, restored.Info.generation);
        Assert.Equal(before.AttributeRecords, restored.AttributeRecords);
        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
    }

    [Fact]
    public void AsyncTicket_ResetFrameStateCancelsPendingWork()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(5f)));

        Assert.True(device.ResetVFXEffectFrameState(1));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void AsyncTicket_ClearEffectCancelsAndRemovesParticleState()
    {
        using NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(5f)));

        Assert.True(device.ClearVFXEffectState(1));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.False(device.TryGetVFXParticleSystem(
            1, Shader.PropertyToID(SystemName), out _));
    }

    [Fact]
    public void AsyncTicket_MultiSystemBatchPublishesAllAtCompletion()
    {
        using NativeGraphicsDevice device = SpawnBatch(3);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateKernelData[] kernels = Enumerable.Range(0, 3)
            .Select(index => Kernel(new[]
            {
                IntegrateConstant(Position, VFXRuntimeValueType.Float, 2f)
            }, BatchSystem(index), 500 + index))
            .ToArray();
        Assert.True(device.BeginVFXUpdateKernels(1, kernels, 0.1f, 17, out ulong ticket));
        for (int index = 0; index < 3; index++)
            AssertClose(index + 1f,
                ReadFloat(ActiveRecord(Read(device, BatchSystem(index))), Position));

        Assert.True(device.CompleteVFXUpdateKernels(ticket));

        for (int index = 0; index < 3; index++)
            AssertClose(index + 1.2f,
                ReadFloat(ActiveRecord(Read(device, BatchSystem(index))), Position));
    }

    [Fact]
    public void MetalAsyncTicket_PollReportsSubmittedBackendBeforePublication()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 2f));

        Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out var info));
        Assert.Equal(2, info.backendKind);
        Assert.InRange(info.state, 0, 1);
        AssertClose(1f, ReadFloat(ActiveRecord(Read(device)), Position));

        Assert.True(device.CompleteVFXUpdateKernels(ticket));
        AssertClose(1.2f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void AsyncTicket_InvalidBatchReturnsNoTicket()
    {
        using NativeGraphicsDevice device = Spawn();
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateKernelData invalid = Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)
        }) with { ParticleCapacity = 3 };

        Assert.False(device.BeginVFXUpdateKernels(
            1, new[] { invalid }, 0.1f, 17, out ulong ticket));

        Assert.Equal(0ul, ticket);
        AssertClose(0f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void AsyncTicket_DeviceDisposeWithPendingWorkIsSafe()
    {
        NativeGraphicsDevice device = Spawn(positionX: 1f);
        if (device.Handle == IntPtr.Zero)
        {
            device.Dispose();
            return;
        }
        _ = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));

        device.Dispose();
        device.Dispose();
    }

    [Fact]
    public void MetalFrameCommit_PublishesResidentGenerationWithoutCompletionWait()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(8f)));
        Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out var submitted));

        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(submitted.submitGeneration, stats.residentGeneration);
        Assert.Equal(1ul, stats.asynchronousResidentPublishCount);
        Assert.Equal(0ul, stats.asynchronousResidentCompletionCount);
        Assert.Equal(0ul, stats.completionCount);
        Assert.Equal(1ul, stats.pendingUpdateCount);
        Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.True(device.CompleteVFXUpdateKernels(ticket));
    }

    [Fact]
    public void MetalFrameCommit_ExplicitCompletionPublishesCpuMetadata()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(
            GraphicsDeviceType.Metal, positionX: 1f, age: 0.9f, lifetime: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.2f, out _));
        ulong ticket = BeginUpdate(device, 0.2f,
            IntegrateConstant(Age, VFXRuntimeValueType.Float, 1f),
            Operation(VFXRuntimeUpdateOperationKind.Reap,
                Alive, Age, Lifetime, VFXRuntimeValueType.Boolean));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.True(device.CompleteVFXUpdateKernels(ticket));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.asynchronousResidentCompletionCount);
        Assert.Equal(0ul, stats.pendingUpdateCount);
        Assert.Equal(1ul, stats.completionCount);
        Assert.Equal(0, Read(device).Info.aliveCount);
    }

    [Fact]
    public void MetalFrameCommit_RejectsCancellationAfterTransactionCommit()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.False(device.CancelVFXUpdateKernels(ticket));
        Assert.True(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.True(device.CompleteVFXUpdateKernels(ticket));
    }

    [Fact]
    public void MetalFrameCommit_NextPreparePollsWithoutCompletionWait()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));

        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.preparationPollCount);
        Assert.Equal(1ul,
            stats.preparationDeferredCount + stats.preparationRetiredCount);
        Assert.Equal(0ul, stats.completionWaitCount);
        Assert.True(device.AbortVFXEffectFrame(1, second));
        if (device.TryGetVFXUpdateTicketInfo(ticket, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket));
        Assert.Equal(1ul, Stats(device).asynchronousResidentCompletionCount);
    }

    [Fact]
    public void MetalFrameCommit_AutomaticPrepareUsesTheSameNonBlockingPollContract()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectFrame(
            1, first, 0.1f, 1f, 0.05f, 0.2f, false, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));

        Assert.True(device.PrepareVFXEffectFrame(
            1, second, 0.1f, 1f, 0.05f, 0.2f, false, out _));

        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.preparationPollCount);
        Assert.Equal(1ul,
            stats.preparationDeferredCount + stats.preparationRetiredCount);
        Assert.Equal(0ul, stats.completionWaitCount);
        Assert.True(device.AbortVFXEffectFrame(1, second));
        if (device.TryGetVFXUpdateTicketInfo(ticket, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket));
    }

    [Fact]
    public void MetalFrameCommit_AbortingOverlappedFramePreservesCommittedResident()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 2f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(7f)));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));
        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        Assert.True(device.AbortVFXEffectFrame(1, second));

        if (device.TryGetVFXUpdateTicketInfo(ticket, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket));
        AssertClose(7f, ReadFloat(ActiveRecord(Read(device)), Position));
        Assert.Equal(0ul, Stats(device).asynchronousResidentRollbackCount);
    }

    [Fact]
    public void MetalFrameCommit_EmptyOverlappedFrameCanCommitBeforeMetadataRetirement()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 2f));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));
        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        Assert.True(device.CommitVFXEffectFrame(1, second, out var state));

        Assert.Equal(second, state.frameIndex);
        if (device.TryGetVFXUpdateTicketInfo(ticket, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket));
        AssertClose(1.2f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalFrameCommit_InitializeIsARealDeadListDependencyBoundary()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(8f)));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));
        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        Assert.True(SubmitSpawn(device, 2, positionX: 3f));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.Equal(2, Read(device).Info.aliveCount);
        Assert.Equal(1ul, Stats(device).asynchronousResidentCompletionCount);
        Assert.True(device.AbortVFXEffectFrame(1, second));
    }

    [Fact]
    public void MetalFrameCommit_BatchPreparationPollIsRecordedForEverySystem()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(3);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        VFXRuntimeUpdateKernelData[] kernels = Enumerable.Range(0, 3)
            .Select(index => Kernel(
                new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) },
                BatchSystem(index), 700 + index))
            .ToArray();
        Assert.True(device.BeginVFXUpdateKernels(
            1, kernels, 0.1f, 17, out ulong ticket));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));

        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        for (int index = 0; index < 3; ++index)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(1ul, stats.preparationPollCount);
            Assert.Equal(1ul,
                stats.preparationDeferredCount + stats.preparationRetiredCount);
            Assert.Equal(0ul, stats.completionWaitCount);
        }
        Assert.True(device.AbortVFXEffectFrame(1, second));
        if (device.TryGetVFXUpdateTicketInfo(ticket, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket));
    }

    [Fact]
    public void VisualEffectMetal_NextUpdateCanDispatchBeforeItsPredecessorRetires()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float3, 1f, 0f, 0f)
        })).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(device, 1, positionX: 1f, effectId: effectId));
            Assert.True(device.BeginVFXFrame(out uint first));
            effect.PrepareManualVfxFrame(0.1f, first, device);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));
            effect.CompleteVfxFrame(device);
            Assert.True(device.BeginVFXFrame(out uint second));

            effect.PrepareManualVfxFrame(0.1f, second, device);
            AnityNative.GraphicsVFXUpdateBackendStats prepared =
                Stats(device, SystemName, effectId);
            Assert.Equal(1ul, prepared.preparationPollCount);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));

            AnityNative.GraphicsVFXUpdateBackendStats updated =
                Stats(device, SystemName, effectId);
            Assert.Equal(2ul, updated.dispatchCount);
            Assert.InRange(updated.asynchronousResidentCompletionCount, 0ul, 1ul);
            Assert.InRange(updated.pendingUpdateCount, 1ul, 2ul);
            effect.AbortVfxFrame(device);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void VisualEffectMetal_DeferredInitializeFlowsIntoFrameUpdateWithoutCpuRetirement()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData operation =
            IntegrateConstant(Position, VFXRuntimeValueType.Float3, 1f, 0f, 0f);
        VFXRuntimeUpdateKernelData updateKernel = Kernel(new[] { operation });
        VFXRuntimeAssetData runtimeData =
            DeferredInitializeRuntimeData(updateKernel);
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(runtimeData.Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(
                device, 1, positionX: 1f, effectId: effectId,
                initializeContextId: 41, spawnSystemId: 14));
            Assert.True(device.DispatchVFXUpdateKernels(
                effectId, new[] { Kernel(new[] { operation }) }, 0.1f, 17));
            ulong baselineCompletionWaitCount =
                Stats(device, SystemName, effectId).completionWaitCount;
            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(0.1f, frame, device);
            effect.SendEvent("Spawn");
            Assert.Equal(1, effect.ProcessInputEvents(
                device, deferInitializeCompletion: true));
            Assert.Equal(1ul, Stats(device, SystemName, effectId)
                .pendingInitializeCount);

            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));

            AnityNative.GraphicsVFXUpdateBackendStats queued =
                Stats(device, SystemName, effectId);
            Assert.Equal(1ul, queued.pendingInitializeCount);
            Assert.Equal(0ul, queued.pendingUpdateCount);
            Assert.Equal(0ul, queued.residentInitializeAtomicPublishCount);
            effect.CompleteVfxFrame(device);
            AnityNative.GraphicsVFXUpdateBackendStats published =
                Stats(device, SystemName, effectId);
            Assert.Equal(published.pendingInitializeCount,
                published.pendingUpdateCount);
            Assert.InRange(published.pendingInitializeCount, 0ul, 1ul);
            Assert.Equal(baselineCompletionWaitCount,
                published.completionWaitCount);

            NativeVFXParticleSystem state = Read(device, SystemName, effectId);
            Assert.Equal(2, state.Info.aliveCount);
            AssertClose(0.1f, ReadFloat(Record(state, 2), Position));
            AssertClose(1.2f, ReadFloat(Record(state, 3), Position));
            Assert.Equal(0ul, Stats(device, SystemName, effectId)
                .pendingInitializeCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.001f)]
    [InlineData(0.01f)]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(10f)]
    [InlineData(100f)]
    [InlineData(1000f)]
    public void VisualEffectMetal_PrepareKeepsInitializeQueuedForFrameUpdate(
        float deltaTime)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeUpdateOperationData operation =
            IntegrateConstant(Position, VFXRuntimeValueType.Float3, 1f, 0f, 0f);
        VFXRuntimeUpdateKernelData updateKernel = Kernel(new[] { operation });
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(
            DeferredInitializeRuntimeData(updateKernel).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(
                device, 1, positionX: 1f, effectId: effectId,
                initializeContextId: 41, spawnSystemId: 14));
            Assert.True(device.DispatchVFXUpdateKernels(
                effectId, new[] { updateKernel }, 0f, 17));
            effect.SendEvent("Spawn");
            Assert.Equal(1, effect.ProcessInputEvents(
                device, deferInitializeCompletion: true));
            AnityNative.GraphicsVFXUpdateBackendStats queued =
                Stats(device, SystemName, effectId);
            Assert.Equal(1ul, queued.pendingInitializeCount);
            Assert.Equal(0ul,
                queued.asynchronousInitializeResidentCompletionCount);

            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(deltaTime, frame, device);

            AnityNative.GraphicsVFXUpdateBackendStats prepared =
                Stats(device, SystemName, effectId);
            Assert.Equal(1ul, prepared.pendingInitializeCount);
            Assert.Equal(0ul,
                prepared.asynchronousInitializeResidentCompletionCount);
            Assert.Equal(queued.completionWaitCount,
                prepared.completionWaitCount);
            Assert.Equal(1, effect.UpdateParticleSystems(deltaTime, device));
            effect.CompleteVfxFrame(device);
            NativeVFXParticleSystem state = Read(device, SystemName, effectId);
            float[] positions = Enumerable.Range(0, state.Info.capacity)
                .Where(index => ReadWord(Record(state, index), Alive) != 0)
                .Select(index => ReadFloat(Record(state, index), Position))
                .OrderBy(value => value)
                .ToArray();
            Assert.Equal(2, positions.Length);
            AssertClose(deltaTime, positions[0]);
            AssertClose(1f + deltaTime, positions[1]);
            Assert.Equal(0ul, Stats(device, SystemName, effectId)
                .pendingInitializeCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void VisualEffectMetal_OrderedUpdateQueuePreservesEveryGeneration(
        int frameCount)
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float3, 1f, 0f, 0f)
        })).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(device, 1, positionX: 1f, effectId: effectId));
            for (int index = 0; index < frameCount; ++index)
            {
                Assert.True(device.BeginVFXFrame(out uint frame));
                effect.PrepareManualVfxFrame(0.1f, frame, device);
                Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));
                effect.CompleteVfxFrame(device);
            }

            NativeVFXParticleSystem state = Read(device, SystemName, effectId);
            AssertClose(1f + frameCount * 0.1f,
                ReadFloat(ActiveRecord(state), Position));
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, SystemName, effectId);
            Assert.Equal((ulong)frameCount, stats.dispatchCount);
            Assert.Equal((ulong)frameCount, stats.completionCount);
            Assert.Equal((ulong)frameCount,
                stats.asynchronousResidentCompletionCount);
            Assert.Equal(0ul, stats.pendingUpdateCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void VisualEffectMetal_PrepareOverlapDoesNotMaterializeParticleRecords()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(Kernel(new[]
        {
            IntegrateConstant(Position, VFXRuntimeValueType.Float3, 1f, 0f, 0f)
        })).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(device, 1, effectId: effectId));
            Assert.True(device.BeginVFXFrame(out uint first));
            effect.PrepareManualVfxFrame(0.1f, first, device);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));
            effect.CompleteVfxFrame(device);
            Assert.True(device.BeginVFXFrame(out uint second));

            effect.PrepareManualVfxFrame(0.1f, second, device);

            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, SystemName, effectId);
            Assert.Equal(1ul, stats.preparationPollCount);
            Assert.Equal(0ul, stats.deferredParticleReadbackCount);
            Assert.Equal(0ul, stats.deferredParticleReadbackBytes);
            effect.AbortVfxFrame(device);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void MetalFrameCommit_ReadbackAfterOverlappedPrepareStillRetiresTicket()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 2f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f)));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));
        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        AssertClose(9f, ReadFloat(ActiveRecord(Read(device)), Position));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.Equal(1ul, Stats(device).preparationPollCount);
        Assert.True(device.AbortVFXEffectFrame(1, second));
    }

    [Fact]
    public void MetalFrameCommit_ClearDuringOverlappedPrepareReleasesAllState()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));
        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));

        Assert.True(device.ClearVFXEffectState(1));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.False(device.TryGetVFXParticleSystemInfo(
            1, Shader.PropertyToID(SystemName), out _));
    }

    [Fact]
    public void MetalFrameCommit_PreparationPollsAreEffectScoped()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(SubmitSpawn(device, 1, positionX: 1f, effectId: 1));
        Assert.True(SubmitSpawn(device, 1, positionX: 2f, effectId: 2));
        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.PrepareVFXEffectManualFrame(1, first, 0.1f, out _));
        Assert.True(device.PrepareVFXEffectManualFrame(2, first, 0.1f, out _));
        ulong ticket1 = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.BeginVFXUpdateKernels(
            2, new[] { Kernel(new[]
            {
                IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f)
            }) }, 0.1f, 17, out ulong ticket2));
        Assert.True(device.CommitVFXEffectFrame(1, first, out _));
        Assert.True(device.CommitVFXEffectFrame(2, first, out _));
        Assert.True(device.BeginVFXFrame(out uint second));

        Assert.True(device.PrepareVFXEffectManualFrame(1, second, 0.1f, out _));
        Assert.Equal(1ul, Stats(device, SystemName, 1).preparationPollCount);
        Assert.Equal(0ul, Stats(device, SystemName, 2).preparationPollCount);
        Assert.True(device.PrepareVFXEffectManualFrame(2, second, 0.1f, out _));
        Assert.Equal(1ul, Stats(device, SystemName, 2).preparationPollCount);

        Assert.True(device.AbortVFXEffectFrame(1, second));
        Assert.True(device.AbortVFXEffectFrame(2, second));
        if (device.TryGetVFXUpdateTicketInfo(ticket1, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket1));
        if (device.TryGetVFXUpdateTicketInfo(ticket2, out _))
            Assert.True(device.CompleteVFXUpdateKernels(ticket2));
    }

    [Fact]
    public void MetalFrameCommit_ParticleReadbackIsAnExplicitCompletionBoundary()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 2f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(9f)));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        AssertClose(9f, ReadFloat(ActiveRecord(Read(device)), Position));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(device);
        Assert.Equal(1ul, stats.asynchronousResidentCompletionCount);
        Assert.Equal(1ul, stats.deferredParticleReadbackCount);
    }

    [Fact]
    public void MetalFrameCommit_ClearEffectWaitsAndReleasesPublishedSubmission()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.True(device.ClearVFXEffectState(1));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        Assert.False(device.TryGetVFXParticleSystemInfo(
            1, Shader.PropertyToID(SystemName), out _));
    }

    [Fact]
    public void MetalFrameCommit_ResetFrameRollsBackPublishedSubmission()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 3f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(7f)));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.True(device.ResetVFXEffectFrameState(1));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        AssertClose(3f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void MetalFrameCommit_DeviceDisposeWaitsForPublishedSubmission()
    {
        if (!OperatingSystem.IsMacOS()) return;
        NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Metal, positionX: 1f);
        if (device.Handle == IntPtr.Zero)
        {
            device.Dispose();
            return;
        }
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        _ = BeginUpdate(device, 0.1f,
            IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));
        Assert.Equal(1ul, Stats(device).pendingUpdateCount);

        device.Dispose();
        device.Dispose();
    }

    [Fact]
    public void MetalFrameCommit_BatchPublishesEverySystemBeforeCompletion()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(3);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        VFXRuntimeUpdateKernelData[] kernels = Enumerable.Range(0, 3)
            .Select(index => Kernel(
                new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) },
                BatchSystem(index), 500 + index))
            .ToArray();
        Assert.True(device.BeginVFXUpdateKernels(
            1, kernels, 0.1f, 17, out ulong ticket));

        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        for (int index = 0; index < 3; ++index)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(1ul, stats.asynchronousResidentPublishCount);
            Assert.Equal(1ul, stats.pendingUpdateCount);
            Assert.Equal(0ul, stats.completionCount);
        }
        Assert.True(device.CompleteVFXUpdateKernels(ticket));
    }

    [Fact]
    public void MetalFrameCommit_BatchCompletionReleasesEveryRingSlot()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = SpawnBatch(3);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        VFXRuntimeUpdateKernelData[] kernels = Enumerable.Range(0, 3)
            .Select(index => Kernel(
                new[] { IntegrateConstant(Position, VFXRuntimeValueType.Float, 1f) },
                BatchSystem(index), 600 + index))
            .ToArray();
        Assert.True(device.BeginVFXUpdateKernels(
            1, kernels, 0.1f, 17, out ulong ticket));
        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.True(device.CompleteVFXUpdateKernels(ticket));

        for (int index = 0; index < 3; ++index)
        {
            AnityNative.GraphicsVFXUpdateBackendStats stats =
                Stats(device, BatchSystem(index));
            Assert.Equal(1ul, stats.asynchronousResidentCompletionCount);
            Assert.Equal(0ul, stats.pendingUpdateCount);
            Assert.Equal(1ul, stats.completionCount);
        }
    }

    [Fact]
    public void NullFrameCommitCompletesSynchronouslyWithoutAsyncPublication()
    {
        using NativeGraphicsDevice device = Spawn(GraphicsDeviceType.Null, positionX: 1f);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1, frame, 0.1f, out _));
        ulong ticket = BeginUpdate(device, 0.1f,
            Set(Position, VFXRuntimeValueType.Float,
                VFXRuntimeInitializeComposition.Overwrite, Float(5f)));

        Assert.True(device.CommitVFXEffectFrame(1, frame, out _));

        Assert.False(device.TryGetVFXUpdateTicketInfo(ticket, out _));
        AssertClose(5f, ReadFloat(ActiveRecord(Read(device)), Position));
    }

    [Fact]
    public void VisualEffectMetalFrameCommitAvoidsFullParticleReadback()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(Kernel(new[]
        {
            Operation(VFXRuntimeUpdateOperationKind.SetAttribute,
                Position, -1, -1, VFXRuntimeValueType.Float3,
                Values(9f, 0f, 0f))
        })).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(device, 1, positionX: 2f, effectId: effectId));
            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(0.1f, frame, device);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));

            effect.CompleteVfxFrame(device);

            AnityNative.GraphicsVFXUpdateBackendStats stats = Stats(
                device, SystemName, effectId);
            Assert.Equal(1ul, stats.asynchronousResidentPublishCount);
            Assert.Equal(0ul, stats.deferredParticleReadbackCount);
            Assert.Equal(0ul, stats.deferredParticleReadbackBytes);
            Assert.True(device.BeginVFXFrame(out uint next));
            effect.PrepareManualVfxFrame(0.1f, next, device);
            Assert.Equal(0ul, Stats(device, SystemName, effectId)
                .deferredParticleReadbackCount);
            effect.AbortVfxFrame(device);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Theory]
    [InlineData(GraphicsDeviceType.Null)]
    [InlineData(GraphicsDeviceType.Metal)]
    public void VisualEffectFrame_UpdatePublishesOnlyDuringCommit(
        GraphicsDeviceType deviceType)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            deviceType, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(Kernel(new[]
        {
            Operation(VFXRuntimeUpdateOperationKind.SetAttribute,
                Position, -1, -1, VFXRuntimeValueType.Float3,
                Values(9f, 0f, 0f))
        })).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(device, 1, positionX: 2f, effectId: effectId));
            effect.aliveParticleCount = 77;
            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(0.1f, frame, device);

            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));

            Assert.Equal(77, effect.aliveParticleCount);
            AssertClose(2f, ReadFloat(ActiveRecord(Read(device, SystemName, effectId)), Position));
            effect.CompleteVfxFrame(device);
            Assert.Equal(1, effect.aliveParticleCount);
            AssertClose(9f, ReadFloat(ActiveRecord(Read(device, SystemName, effectId)), Position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Theory]
    [InlineData(GraphicsDeviceType.Null)]
    [InlineData(GraphicsDeviceType.Metal)]
    public void VisualEffectFrame_AbortCancelsPendingUpdateAndRestoresAliveCount(
        GraphicsDeviceType deviceType)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            deviceType, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(RuntimeData(Kernel(new[]
        {
            Operation(VFXRuntimeUpdateOperationKind.SetAttribute,
                Position, -1, -1, VFXRuntimeValueType.Float3,
                Values(9f, 0f, 0f))
        })).Serialize());
        var effect = new VisualEffect { visualEffectAsset = asset };
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        try
        {
            Assert.True(SubmitSpawn(device, 1, positionX: 2f, effectId: effectId));
            effect.aliveParticleCount = 17;
            Assert.True(device.BeginVFXFrame(out uint frame));
            effect.PrepareManualVfxFrame(0.1f, frame, device);
            Assert.Equal(1, effect.UpdateParticleSystems(0.1f, device));

            effect.AbortVfxFrame(device);

            Assert.Equal(17, effect.aliveParticleCount);
            AssertClose(2f, ReadFloat(ActiveRecord(Read(device, SystemName, effectId)), Position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    private static string BatchSystem(int index) => $"BatchParticles{index}";

    private static ulong BeginUpdate(
        NativeGraphicsDevice device,
        float deltaTime,
        params VFXRuntimeUpdateOperationData[] operations)
    {
        Assert.True(device.BeginVFXUpdateKernels(
            1, new[] { Kernel(operations) }, deltaTime, 17, out ulong ticket));
        Assert.NotEqual(0ul, ticket);
        return ticket;
    }

    private static NativeGraphicsDevice SpawnBatch(int width)
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        if (device.Handle == IntPtr.Zero) return device;
        for (int index = 0; index < width; index++)
            Assert.True(SubmitSpawn(
                device, (ulong)(index + 1), positionX: index + 1f,
                systemName: BatchSystem(index),
                initializeContextId: 100 + index,
                spawnSystemId: 200 + index));
        return device;
    }

    private static bool DispatchBatch(
        NativeGraphicsDevice device,
        int width,
        float deltaTime,
        Func<int, IReadOnlyList<VFXRuntimeUpdateOperationData>> operations)
    {
        VFXRuntimeUpdateKernelData[] kernels = Enumerable.Range(0, width)
            .Select(index => Kernel(
                operations(index), BatchSystem(index), 500 + index))
            .ToArray();
        return device.DispatchVFXUpdateKernels(1, kernels, deltaTime, 17);
    }

    private static NativeGraphicsDevice Spawn(
        GraphicsDeviceType type = GraphicsDeviceType.Null,
        float positionX = 0f,
        float velocityX = 0f,
        float age = 0f,
        float lifetime = 10f,
        float mass = 1f,
        float size = 1f,
        float scaleX = 1f,
        float scaleY = 1f)
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(type, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        if (device.Handle != IntPtr.Zero)
            Assert.True(SubmitSpawn(device, 1, positionX, velocityX, age, lifetime, mass, size, scaleX, scaleY));
        return device;
    }

    private static NativeGraphicsDevice SpawnForProgram(GraphicsDeviceType type, int program)
        => program switch
        {
            0 or 1 or 2 => Spawn(type, positionX: 2f),
            4 => Spawn(type, mass: 2f),
            5 => Spawn(type, velocityX: 2f),
            6 => Spawn(type, velocityX: 8f),
            7 => Spawn(type, velocityX: 8f, size: 2f, scaleX: 1f, scaleY: 0.5f),
            8 => Spawn(type, age: 1f, lifetime: 0.5f),
            9 => Spawn(type, positionX: 3f),
            12 => Spawn(type, velocityX: 2f),
            _ => Spawn(type)
        };

    private static NativeGraphicsDevice SpawnCapacity(
        int capacity,
        Func<int, float> age)
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        if (device.Handle == IntPtr.Zero) return device;
        for (int index = 0; index < capacity; index++)
            Assert.True(SubmitSpawn(
                device, (ulong)(index + 1), age: age(index), lifetime: 1f,
                initializeContextId: 740, spawnSystemId: 741,
                particleCapacity: capacity));
        return device;
    }

    private static bool DispatchCapacity(
        NativeGraphicsDevice device,
        int capacity,
        params VFXRuntimeUpdateOperationData[] operations)
        => device.DispatchVFXUpdateKernels(
            1, new[] { Kernel(operations, particleCapacity: capacity) }, 0f, 17);

    private static bool RunProgram(NativeGraphicsDevice device, int program)
        => program switch
        {
            0 => Dispatch(device, 0.25f,
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Overwrite, Float(10f))),
            1 => Dispatch(device, 0.25f,
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Add, Float(3f)),
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Multiply, Float(2f)),
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Blend, Float(14f), blend: 0.25f)),
            2 => Dispatch(device, 1f,
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Overwrite, Float(9f)),
                SourceSet(Velocity, Position, VFXRuntimeValueType.Float)),
            3 => Dispatch(device, 0.5f,
                IntegrateConstant(Velocity, VFXRuntimeValueType.Float3, 0f, -9.81f, 0f),
                IntegrateSource(Position, Velocity, VFXRuntimeValueType.Float3)),
            4 => Dispatch(device, 0.5f, Operation(
                VFXRuntimeUpdateOperationKind.Force, Velocity, Mass, -1,
                VFXRuntimeValueType.Float3, Values(4f, 0f, 0f))),
            5 => Dispatch(device, 0.25f, Operation(
                VFXRuntimeUpdateOperationKind.RelativeForce, Velocity, Mass, -1,
                VFXRuntimeValueType.Float3, Values(10f, 0f, 0f), Values(2f))),
            6 => Dispatch(device, 0.25f, Operation(
                VFXRuntimeUpdateOperationKind.Drag, Velocity, Mass, -1,
                VFXRuntimeValueType.Float3, Values(2f))),
            7 => Dispatch(device, 0.5f, new VFXRuntimeUpdateOperationData(
                VFXRuntimeUpdateOperationKind.Drag, Velocity, Mass, Size, ScaleX, ScaleY,
                VFXRuntimeValueType.Float3, VFXRuntimeInitializeComposition.Overwrite,
                VFXRuntimeInitializeRandomMode.Off, false,
                Values(0.5f), Array.Empty<uint>(), Float(1f))),
            8 => Dispatch(device, 0.25f,
                IntegrateConstant(Age, VFXRuntimeValueType.Float, 1f),
                Operation(VFXRuntimeUpdateOperationKind.Reap, Alive, Age, Lifetime,
                    VFXRuntimeValueType.Boolean)),
            9 => Dispatch(device, 0f, true,
                Set(Position, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeComposition.Overwrite, Float(9f))),
            10 => Dispatch(device, 0.1f, RandomSet(
                VFXRuntimeInitializeRandomMode.PerComponent)),
            11 => Dispatch(device, 0.1f, RandomSet(
                VFXRuntimeInitializeRandomMode.Uniform)),
            12 => Dispatch(device, 0.1f, Operation(
                VFXRuntimeUpdateOperationKind.CopyAttribute,
                Position, Velocity, -1, VFXRuntimeValueType.Float3)),
            _ => false
        };

    private static bool SubmitSpawn(
        NativeGraphicsDevice device,
        ulong sequence,
        float positionX = 0f,
        float velocityX = 0f,
        float age = 0f,
        float lifetime = 10f,
        float mass = 1f,
        float size = 1f,
        float scaleX = 1f,
        float scaleY = 1f,
        string systemName = SystemName,
        long initializeContextId = 40,
        int spawnSystemId = 13,
        ulong effectId = 1,
        int particleCapacity = 4)
    {
        VFXRuntimeInitializeKernelData kernel = InitializeKernel(
            positionX, velocityX, age, lifetime, mass, size, scaleX, scaleY,
            initializeContextId, particleCapacity);
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = effectId,
            sequence = sequence,
            initializeContextId = initializeContextId,
            sourceSpawnerContextId = 30,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(systemName),
            spawnSystemId = spawnSystemId,
            startEventIndex = 0,
            recordCount = 1,
            strideBytes = sizeof(uint)
        };
        return device.SubmitVFXInitializeKernels(
            new[] { dispatch }, new VFXRuntimeInitializeKernelData?[] { kernel },
            new byte[sizeof(uint)], 17);
    }

    private static bool BeginSpawn(
        NativeGraphicsDevice device,
        ulong sequence,
        float positionX,
        out ulong ticketId,
        ulong effectId = 1)
    {
        VFXRuntimeInitializeKernelData kernel = InitializeKernel(positionX);
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = effectId,
            sequence = sequence,
            initializeContextId = 40,
            sourceSpawnerContextId = 30,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(SystemName),
            spawnSystemId = 13,
            startEventIndex = 0,
            recordCount = 1,
            strideBytes = sizeof(uint)
        };
        return device.BeginVFXInitializeKernels(
            new[] { dispatch }, new VFXRuntimeInitializeKernelData?[] { kernel },
            new byte[sizeof(uint)], 17, out ticketId);
    }

    private static bool BeginSpawnBatch(
        NativeGraphicsDevice device,
        IReadOnlyList<(string SystemName, long ContextId, float PositionX)> targets,
        out ulong ticketId)
    {
        var dispatches = new AnityNative.GraphicsVFXInitializeDispatchDesc[targets.Count];
        var kernels = new VFXRuntimeInitializeKernelData?[targets.Count];
        for (int index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            dispatches[index] = new AnityNative.GraphicsVFXInitializeDispatchDesc
            {
                effectId = 1,
                sequence = 2,
                initializeContextId = target.ContextId,
                sourceSpawnerContextId = 30,
                eventNameId = 12,
                particleSystemId = Shader.PropertyToID(target.SystemName),
                spawnSystemId = 100 + index,
                startEventIndex = index,
                recordCount = 1,
                strideBytes = sizeof(uint)
            };
            kernels[index] = InitializeKernel(
                positionX: target.PositionX,
                initializeContextId: target.ContextId);
        }
        return device.BeginVFXInitializeKernels(
            dispatches, kernels, new byte[targets.Count * sizeof(uint)],
            17, out ticketId);
    }

    private static VFXRuntimeInitializeKernelData InitializeKernel(
        float positionX = 0f,
        float velocityX = 0f,
        float age = 0f,
        float lifetime = 10f,
        float mass = 1f,
        float size = 1f,
        float scaleX = 1f,
        float scaleY = 1f,
        long initializeContextId = 40,
        int particleCapacity = 4)
        => new(initializeContextId, checked((uint)particleCapacity), Stride, 1, true, new[]
        {
            Attribute("alive", VFXRuntimeValueType.Boolean, Alive, 1),
            Attribute("position", VFXRuntimeValueType.Float3, Position, Float(positionX), Float(0), Float(0)),
            Attribute("velocity", VFXRuntimeValueType.Float3, Velocity, Float(velocityX), Float(0), Float(0)),
            Attribute("age", VFXRuntimeValueType.Float, Age, Float(age)),
            Attribute("lifetime", VFXRuntimeValueType.Float, Lifetime, Float(lifetime)),
            Attribute("mass", VFXRuntimeValueType.Float, Mass, Float(mass)),
            Attribute("seed", VFXRuntimeValueType.UInt32, Seed, 123),
            Attribute("size", VFXRuntimeValueType.Float, Size, Float(size)),
            Attribute("scaleX", VFXRuntimeValueType.Float, ScaleX, Float(scaleX)),
            Attribute("scaleY", VFXRuntimeValueType.Float, ScaleY, Float(scaleY)),
            Attribute("scaleZ", VFXRuntimeValueType.Float, 14, Float(1))
        }, Array.Empty<VFXRuntimeInitializeOperationData>());

    private static VFXRuntimeInitializeAttributeData Attribute(
        string name,
        VFXRuntimeValueType type,
        int offset,
        params uint[] defaults)
        => new(new VFXRuntimeAttributeData(name, type, offset, defaults.Length), defaults);

    private static bool Dispatch(
        NativeGraphicsDevice device,
        float deltaTime,
        params VFXRuntimeUpdateOperationData[] operations)
        => Dispatch(device, deltaTime, false, operations);

    private static bool Dispatch(
        NativeGraphicsDevice device,
        float deltaTime,
        bool skipZeroDelta,
        params VFXRuntimeUpdateOperationData[] operations)
        => device.DispatchVFXUpdateKernels(
            1, new[] { Kernel(operations) with { SkipZeroDeltaUpdate = skipZeroDelta } }, deltaTime, 17);

    private static VFXRuntimeUpdateKernelData Kernel(
        IReadOnlyList<VFXRuntimeUpdateOperationData> operations,
        string systemName = SystemName,
        long contextId = 50,
        int particleCapacity = 4)
        => new(contextId, systemName, checked((uint)particleCapacity), Stride,
            true, false, Alive, Seed, operations);

    private static VFXRuntimeUpdateOperationData Set(
        int target,
        VFXRuntimeValueType type,
        VFXRuntimeInitializeComposition composition,
        uint value,
        float blend = 1f)
        => new(VFXRuntimeUpdateOperationKind.SetAttribute, target, -1, -1, -1, -1,
            type, composition, VFXRuntimeInitializeRandomMode.Off, false,
            new[] { value }, Array.Empty<uint>(), Float(blend));

    private static VFXRuntimeUpdateOperationData SourceSet(
        int target,
        int source,
        VFXRuntimeValueType type)
        => new(VFXRuntimeUpdateOperationKind.SetAttribute, target, source, -1, -1, -1,
            type, VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off, true,
            Array.Empty<uint>(), Array.Empty<uint>(), Float(1));

    private static VFXRuntimeUpdateOperationData RandomSet(
        VFXRuntimeInitializeRandomMode mode)
        => new(VFXRuntimeUpdateOperationKind.SetAttribute, Position, -1, -1, -1, -1,
            VFXRuntimeValueType.Float3, VFXRuntimeInitializeComposition.Overwrite,
            mode, false, Values(0f, 0f, 0f), Values(1f, 2f, 3f), Float(1));

    private static VFXRuntimeUpdateOperationData IntegrateSource(
        int target,
        int source,
        VFXRuntimeValueType type)
        => Operation(VFXRuntimeUpdateOperationKind.Integrate, target, source, -1, type);

    private static VFXRuntimeUpdateOperationData IntegrateConstant(
        int target,
        VFXRuntimeValueType type,
        params float[] values)
        => Operation(VFXRuntimeUpdateOperationKind.Integrate, target, -1, -1, type, Values(values));

    private static VFXRuntimeUpdateOperationData Operation(
        VFXRuntimeUpdateOperationKind kind,
        int target,
        int sourceA,
        int sourceB,
        VFXRuntimeValueType type,
        IReadOnlyList<uint>? valueA = null,
        IReadOnlyList<uint>? valueB = null)
        => new(kind, target, sourceA, sourceB, -1, -1, type,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off, false,
            valueA ?? Array.Empty<uint>(), valueB ?? Array.Empty<uint>(), Float(1));

    private static VFXRuntimeAssetData RuntimeData(VFXRuntimeUpdateKernelData update)
    {
        VFXRuntimeInitializeKernelData initialize = InitializeKernel();
        return new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(),
            new[] { "Spawn" },
            new[] { new VFXRuntimeInputEventData("Spawn", new[]
            {
                new VFXRuntimeInputEventTargetData(
                    40, SystemName, Array.Empty<long>(), Array.Empty<string>(), initialize)
            }) },
            new[] { new VFXRuntimeSystemData(SystemName, VFXRuntimeSystemKind.Particle, 4) },
            Array.Empty<VFXRuntimeOutputEventData>())
        {
            UpdateKernels = new[] { update }
        };
    }

    private static VFXRuntimeAssetData DeferredInitializeRuntimeData(
        VFXRuntimeUpdateKernelData update)
        => new(
            new[]
            {
                new VFXRuntimeAttributeData(
                    "spawnCount", VFXRuntimeValueType.Float, 0, 1)
            },
            new[] { "Spawn" },
            new[]
            {
                new VFXRuntimeInputEventData("Spawn", new[]
                {
                    new VFXRuntimeInputEventTargetData(
                        40, SystemName, Array.Empty<long>(),
                        Array.Empty<string>(), InitializeKernel())
                })
            },
            new[]
            {
                new VFXRuntimeSystemData(
                    SystemName, VFXRuntimeSystemKind.Particle, 4)
            },
            Array.Empty<VFXRuntimeOutputEventData>())
        {
            UpdateKernels = new[] { update }
        };

    private static NativeVFXParticleSystem Read(
        NativeGraphicsDevice device,
        string systemName = SystemName,
        ulong effectId = 1)
    {
        Assert.True(device.TryGetVFXParticleSystem(
            effectId, Shader.PropertyToID(systemName), out NativeVFXParticleSystem? state));
        return state!;
    }

    private static AnityNative.GraphicsVFXUpdateBackendStats Stats(
        NativeGraphicsDevice device,
        string systemName = SystemName,
        ulong effectId = 1)
    {
        Assert.True(device.TryGetVFXUpdateBackendStats(
            effectId, Shader.PropertyToID(systemName), out var stats));
        Assert.Equal(effectId, stats.effectId);
        Assert.Equal(Shader.PropertyToID(systemName), stats.particleSystemId);
        Assert.Equal(2, stats.backendKind);
        return stats;
    }

    private static int ActiveIndex(NativeVFXParticleSystem state)
    {
        for (int index = 0; index < state.Info.capacity; index++)
            if (ReadWord(Record(state, index), Alive) != 0) return index;
        throw new Xunit.Sdk.XunitException("No active VFX particle record was found.");
    }

    private static ReadOnlySpan<byte> ActiveRecord(NativeVFXParticleSystem state)
        => Record(state, ActiveIndex(state));

    private static ReadOnlySpan<byte> Record(NativeVFXParticleSystem state, int index)
        => state.AttributeRecords.AsSpan(
            index * state.Info.attributeStrideBytes,
            state.Info.attributeStrideBytes);

    private static uint ReadWord(ReadOnlySpan<byte> record, int wordOffset)
        => BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(wordOffset * sizeof(uint), sizeof(uint)));

    private static float ReadFloat(ReadOnlySpan<byte> record, int wordOffset)
        => BitConverter.Int32BitsToSingle(unchecked((int)ReadWord(record, wordOffset)));

    private static uint Float(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static uint[] Values(params float[] values)
        => values.Select(Float).ToArray();

    private static void AssertEquivalentParticleState(
        NativeVFXParticleSystem expected,
        NativeVFXParticleSystem actual)
    {
        Assert.Equal(expected.Info.capacity, actual.Info.capacity);
        Assert.Equal(expected.Info.attributeStrideBytes, actual.Info.attributeStrideBytes);
        Assert.Equal(expected.Info.aliveCount, actual.Info.aliveCount);
        Assert.Equal(expected.Info.deadCount, actual.Info.deadCount);
        Assert.Equal(expected.DeadList, actual.DeadList);
        for (int particle = 0; particle < expected.Info.capacity; particle++)
        {
            ReadOnlySpan<byte> expectedRecord = Record(expected, particle);
            ReadOnlySpan<byte> actualRecord = Record(actual, particle);
            for (int word = 0; word < Stride; word++)
            {
                if (word is Alive or Seed)
                    Assert.Equal(ReadWord(expectedRecord, word), ReadWord(actualRecord, word));
                else
                    AssertClose(ReadFloat(expectedRecord, word), ReadFloat(actualRecord, word));
            }
        }
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(actual, expected - 0.0001f, expected + 0.0001f);
}
