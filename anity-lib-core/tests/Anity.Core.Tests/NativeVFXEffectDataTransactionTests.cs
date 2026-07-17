using System.Buffers.Binary;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXEffectDataTransactionTests
{
    private const int ParticleSystemId = 71;
    private const long InitializeContextId = 40;
    private static readonly int ManagedParticleSystemId = Shader.PropertyToID("Particles");
    private static readonly int SpawnId = Shader.PropertyToID("Spawn");
    private static readonly int HitId = Shader.PropertyToID("Hit");

    [Fact]
    public void Abort_RestoresConsumedInputPrefixAndPreservesNewTail()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2001;
        Assert.True(device.UploadVFXEvent(effectId, SpawnId, 1, Words(1), 1, 1));
        Assert.True(device.UploadVFXEvent(effectId, SpawnId, 2, Words(2), 1, 1));
        uint frame = Prepare(device, effectId);

        Assert.True(device.ConsumeVFXEventDispatchPlan(effectId, 1));
        Assert.True(device.UploadVFXEvent(effectId, SpawnId, 3, Words(3), 1, 1));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        Assert.True(device.TryGetVFXEventDispatchPlan(effectId, out NativeVFXEventDispatchPlan? plan));
        Assert.Equal(new ulong[] { 1, 2, 3 }, plan!.Batches.Select(batch => batch.sequence));
        Assert.Equal(Words(1, 2, 3), plan.Records);
    }

    [Fact]
    public void Commit_DiscardsConsumedInputPrefixAndKeepsTail()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2002;
        Assert.True(device.UploadVFXEvent(effectId, SpawnId, 1, Words(1), 1, 1));
        Assert.True(device.UploadVFXEvent(effectId, SpawnId, 2, Words(2), 1, 1));
        uint frame = Prepare(device, effectId);

        Assert.True(device.ConsumeVFXEventDispatchPlan(effectId, 1));
        Assert.True(device.CommitVFXEffectFrame(effectId, frame, out _));

        Assert.True(device.TryGetVFXEventDispatchPlan(effectId, out NativeVFXEventDispatchPlan? plan));
        Assert.Single(plan!.Batches);
        Assert.Equal(2ul, plan.Info.firstSequence);
    }

    [Fact]
    public void Abort_RemovesInitializeDispatchCreatedInsideFrame()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2003;
        uint frame = Prepare(device, effectId);

        Assert.True(device.SubmitVFXInitializeDispatch(Desc(effectId, 1), Words(7)));
        Assert.True(device.TryGetVFXInitializeDispatch(effectId, InitializeContextId, out _));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        Assert.False(device.TryGetVFXInitializeDispatch(effectId, InitializeContextId, out _));
    }

    [Fact]
    public void Abort_RestoresPreviousInitializeDispatchPayload()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2004;
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(effectId, 1), Words(7)));
        uint frame = Prepare(device, effectId);

        Assert.True(device.SubmitVFXInitializeDispatch(Desc(effectId, 2), Words(9)));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        Assert.True(device.TryGetVFXInitializeDispatch(
            effectId, InitializeContextId, out NativeVFXInitializeDispatch? restored));
        Assert.Equal(1ul, restored!.Info.desc.sequence);
        Assert.Equal(Words(7), restored.Records);
    }

    [Fact]
    public void Commit_PersistsInitializeDispatchPayload()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2005;
        uint frame = Prepare(device, effectId);

        Assert.True(device.SubmitVFXInitializeDispatch(Desc(effectId, 1), Words(11)));
        Assert.True(device.CommitVFXEffectFrame(effectId, frame, out _));

        Assert.True(device.TryGetVFXInitializeDispatch(
            effectId, InitializeContextId, out NativeVFXInitializeDispatch? committed));
        Assert.Equal(Words(11), committed!.Records);
    }

    [Fact]
    public void Abort_RemovesParticleBufferAndDeadListCreatedInsideFrame()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2006;
        uint frame = Prepare(device, effectId);

        Assert.True(SubmitKernel(device, effectId, 1, 3f));
        Assert.True(device.TryGetVFXParticleSystem(effectId, ParticleSystemId, out _));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        Assert.False(device.TryGetVFXParticleSystem(effectId, ManagedParticleSystemId, out _));
    }

    [Fact]
    public void Abort_RestoresParticleAttributesAliveCountAndDeadList()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2007;
        Assert.True(SubmitKernel(device, effectId, 1, 3f));
        NativeVFXParticleSystem baseline = ReadParticles(device, effectId);
        uint frame = Prepare(device, effectId);

        Assert.True(SubmitKernel(device, effectId, 2, 9f));
        Assert.Equal(2, ReadParticles(device, effectId).Info.aliveCount);
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        NativeVFXParticleSystem restored = ReadParticles(device, effectId);
        Assert.Equal(baseline.Info.aliveCount, restored.Info.aliveCount);
        Assert.Equal(baseline.Info.deadCount, restored.Info.deadCount);
        Assert.Equal(baseline.AttributeRecords, restored.AttributeRecords);
        Assert.Equal(baseline.DeadList, restored.DeadList);
    }

    [Fact]
    public void Commit_PersistsParticleAttributesAliveCountAndDeadList()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2008;
        Assert.True(SubmitKernel(device, effectId, 1, 3f));
        uint frame = Prepare(device, effectId);

        Assert.True(SubmitKernel(device, effectId, 2, 9f));
        Assert.True(device.CommitVFXEffectFrame(effectId, frame, out _));

        NativeVFXParticleSystem committed = ReadParticles(device, effectId);
        Assert.Equal(2, committed.Info.aliveCount);
        Assert.Equal(2, committed.Info.deadCount);
        Assert.Contains(Float(3f), ToWords(committed.AttributeRecords));
        Assert.Contains(Float(9f), ToWords(committed.AttributeRecords));
    }

    [Fact]
    public void MetalAbort_RestoresParticleAttributesAndDeadList()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        const ulong effectId = 2012;
        Assert.True(SubmitKernel(device, effectId, 1, 2f));
        NativeVFXParticleSystem baseline = ReadParticles(device, effectId);
        uint frame = Prepare(device, effectId);

        Assert.True(SubmitKernel(device, effectId, 2, 6f));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        NativeVFXParticleSystem restored = ReadParticles(device, effectId);
        Assert.Equal(2, restored.Info.backendKind);
        Assert.Equal(baseline.AttributeRecords, restored.AttributeRecords);
        Assert.Equal(baseline.DeadList, restored.DeadList);
    }

    [Fact]
    public void Abort_RestoresOutputQueueDequeuedInsideFrame()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2009;
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, Words(Float(2f)), 1, 1));
        uint frame = Prepare(device, effectId);

        Assert.True(device.TryDequeueVFXOutputEvent(effectId, 1, 4, out _));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        Assert.True(device.TryPeekVFXOutputEvent(effectId, out var restored));
        Assert.Equal(1ul, restored.desc.sequence);
    }

    [Fact]
    public void Abort_DiscardsNewOutputAndRestoresSequenceWatermark()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2010;
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 5, Words(Float(1f)), 1, 1));
        Assert.True(device.TryDequeueVFXOutputEvent(effectId, 5, 4, out _));
        uint frame = Prepare(device, effectId);

        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 6, Words(Float(2f)), 1, 1));
        Assert.True(device.AbortVFXEffectFrame(effectId, frame));

        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(0, count);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 6, Words(Float(3f)), 1, 1));
    }

    [Fact]
    public void Commit_KeepsOutputDequeueAndLatestSequence()
    {
        using NativeGraphicsDevice device = CreateDevice();
        const ulong effectId = 2011;
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 5, Words(Float(1f)), 1, 1));
        uint frame = Prepare(device, effectId);

        Assert.True(device.TryDequeueVFXOutputEvent(effectId, 5, 4, out _));
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 6, Words(Float(2f)), 1, 1));
        Assert.True(device.CommitVFXEffectFrame(effectId, frame, out _));

        Assert.True(device.TryPeekVFXOutputEvent(effectId, out var remaining));
        Assert.Equal(6ul, remaining.desc.sequence);
        Assert.False(device.EnqueueVFXOutputEvent(effectId, HitId, 5, Words(Float(4f)), 1, 1));
    }

    [Fact]
    public void StagedOutput_IsInvisibleUntilCommitAndExplicitDelivery()
    {
        using NativeGraphicsDevice device = CreateDevice();
        (VisualEffect effect, _) = CreateOutputEffect();
        var values = new List<float>();
        effect.outputEventReceived += args => values.Add(args.eventAttribute.GetFloat("value"));
        Assert.True(device.EnqueueVFXOutputEvent(
            EffectId(effect), HitId, 1, Words(Float(4f)), 1, 1));
        Assert.True(device.BeginVFXFrame(out uint frame));
        effect.PrepareManualVfxFrame(0.1f, frame, device);

        Assert.Equal(1, effect.StageOutputEventsForCommit(device));
        Assert.Empty(values);
        effect.CompleteVfxFrame(device);
        Assert.Empty(values);
        Assert.Equal(1, effect.DeliverCommittedOutputEvents());
        Assert.Equal(new[] { 4f }, values);
    }

    [Fact]
    public void ManagedAbort_ClearsStagingAndRestoresNativeOutputForRetry()
    {
        using NativeGraphicsDevice device = CreateDevice();
        (VisualEffect effect, _) = CreateOutputEffect();
        var values = new List<float>();
        effect.outputEventReceived += args => values.Add(args.eventAttribute.GetFloat("value"));
        Assert.True(device.EnqueueVFXOutputEvent(
            EffectId(effect), HitId, 1, Words(Float(5f)), 1, 1));
        Assert.True(device.BeginVFXFrame(out uint frame));
        effect.PrepareManualVfxFrame(0.1f, frame, device);

        Assert.Equal(1, effect.StageOutputEventsForCommit(device));
        effect.AbortVfxFrame(device);

        Assert.Equal(1, effect.ProcessOutputEvents(device));
        Assert.Equal(new[] { 5f }, values);
        Assert.Equal(0, effect.DeliverCommittedOutputEvents());
    }

    [Fact]
    public void OutputCallbackException_HappensAfterNativeCommit()
    {
        using NativeGraphicsDevice device = CreateDevice();
        (VisualEffect effect, _) = CreateOutputEffect();
        effect.outputEventReceived += _ => throw new InvalidOperationException("user output failure");
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, Words(Float(6f)), 1, 1));
        Assert.True(device.BeginVFXFrame(out uint frame));
        effect.PrepareManualVfxFrame(0.1f, frame, device);
        effect.StageOutputEventsForCommit(device);
        effect.CompleteVfxFrame(device);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => effect.DeliverCommittedOutputEvents());

        Assert.Equal("user output failure", error.Message);
        Assert.True(device.TryGetVFXEffectFrameState(effectId, out var committed));
        Assert.Equal(0u, committed.prepared);
        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void OutputCallbackException_PreservesLaterCommittedBatchesForRetry()
    {
        using NativeGraphicsDevice device = CreateDevice();
        (VisualEffect effect, _) = CreateOutputEffect();
        int calls = 0;
        var values = new List<float>();
        effect.outputEventReceived += args =>
        {
            calls++;
            if (calls == 1) throw new InvalidOperationException("first batch failure");
            values.Add(args.eventAttribute.GetFloat("value"));
        };
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, Words(Float(1f)), 1, 1));
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 2, Words(Float(2f)), 1, 1));
        Assert.True(device.BeginVFXFrame(out uint frame));
        effect.PrepareManualVfxFrame(0.1f, frame, device);
        Assert.Equal(2, effect.StageOutputEventsForCommit(device));
        effect.CompleteVfxFrame(device);

        Assert.Throws<InvalidOperationException>(() => effect.DeliverCommittedOutputEvents());
        Assert.Equal(1, effect.DeliverCommittedOutputEvents());

        Assert.Equal(new[] { 2f }, values);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void PlayerLoop_OutputFailureCommitsFrameAndDeliversLaterBatchNextUpdate()
    {
        DestroyLiveEffects();
        using NativeGraphicsDevice device = CreateDevice();
        (VisualEffect effect, _) = CreateOutputEffect();
        int calls = 0;
        var values = new List<float>();
        effect.outputEventReceived += args =>
        {
            calls++;
            if (calls == 1) throw new InvalidOperationException("player output failure");
            values.Add(args.eventAttribute.GetFloat("value"));
        };
        ulong effectId = EffectId(effect);
        try
        {
            Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, Words(Float(3f)), 1, 1));
            Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 2, Words(Float(4f)), 1, 1));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => UnityRuntime.Tick(0.05f));

            Assert.Equal("player output failure", error.Message);
            Assert.True(device.TryGetVFXEffectFrameState(effectId, out var committed));
            Assert.Equal(0u, committed.prepared);
            UnityRuntime.Tick(0.05f);
            Assert.Equal(new[] { 4f }, values);
            Assert.Equal(2, calls);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void ManagedAbort_RestoresAliveCountAndInputPlan()
    {
        using NativeGraphicsDevice device = CreateDevice();
        VisualEffect effect = CreateInputParticleEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("value", 7f);
        effect.SendEvent("Spawn", attribute);
        ulong effectId = EffectId(effect);
        Assert.True(device.BeginVFXFrame(out uint frame));
        effect.PrepareManualVfxFrame(0.1f, frame, device);

        Assert.Equal(1, effect.ProcessInputEvents(device));
        Assert.Equal(1, effect.aliveParticleCount);
        effect.AbortVfxFrame(device);

        Assert.Equal(0, effect.aliveParticleCount);
        Assert.False(device.TryGetVFXParticleSystem(effectId, ParticleSystemId, out _));
        Assert.True(device.TryGetVFXEventDispatchPlan(effectId, out var restored));
        Assert.Single(restored!.Batches);
    }

    [Fact]
    public void ManagedRetry_AfterAbortConsumesRestoredInputExactlyOnce()
    {
        using NativeGraphicsDevice device = CreateDevice();
        VisualEffect effect = CreateInputParticleEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("value", 8f);
        effect.SendEvent("Spawn", attribute);
        ulong effectId = EffectId(effect);
        Assert.True(device.BeginVFXFrame(out uint firstFrame));
        effect.PrepareManualVfxFrame(0.1f, firstFrame, device);
        effect.ProcessInputEvents(device);
        effect.AbortVfxFrame(device);
        Assert.True(device.BeginVFXFrame(out uint retryFrame));
        effect.PrepareManualVfxFrame(0.1f, retryFrame, device);

        Assert.Equal(1, effect.ProcessInputEvents(device));
        effect.CompleteVfxFrame(device);

        Assert.Equal(1, effect.aliveParticleCount);
        Assert.True(device.TryGetVFXParticleSystem(
            effectId, ManagedParticleSystemId, out NativeVFXParticleSystem? particles));
        Assert.Equal(1, particles!.Info.aliveCount);
        Assert.False(device.TryGetVFXEventDispatchPlan(effectId, out _));
    }

    private static uint Prepare(NativeGraphicsDevice device, ulong effectId)
    {
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(effectId, frame, 0.1f, out _));
        return frame;
    }

    private static bool SubmitKernel(
        NativeGraphicsDevice device,
        ulong effectId,
        ulong sequence,
        float value)
        => device.SubmitVFXInitializeKernels(
            new[] { Desc(effectId, sequence) },
            new VFXRuntimeInitializeKernelData?[] { ParticleKernel() },
            Words(Float(value)), 17);

    private static NativeVFXParticleSystem ReadParticles(
        NativeGraphicsDevice device,
        ulong effectId)
    {
        Assert.True(device.TryGetVFXParticleSystem(
            effectId, ParticleSystemId, out NativeVFXParticleSystem? state));
        return state!;
    }

    private static VFXRuntimeInitializeKernelData ParticleKernel()
        => new(
            InitializeContextId, 4, 2, 1, true,
            new[]
            {
                new VFXRuntimeInitializeAttributeData(
                    new VFXRuntimeAttributeData("alive", VFXRuntimeValueType.Boolean, 0, 1),
                    new uint[] { 1 }),
                new VFXRuntimeInitializeAttributeData(
                    new VFXRuntimeAttributeData("value", VFXRuntimeValueType.Float, 1, 1),
                    new uint[] { 0 })
            },
            new[]
            {
                new VFXRuntimeInitializeOperationData(
                    1, 0, VFXRuntimeValueType.Float,
                    VFXRuntimeInitializeValueSource.Source,
                    VFXRuntimeInitializeComposition.Overwrite,
                    VFXRuntimeInitializeRandomMode.Off,
                    Array.Empty<uint>(), Array.Empty<uint>(), Float(1f))
            });

    private static AnityNative.GraphicsVFXInitializeDispatchDesc Desc(
        ulong effectId,
        ulong sequence)
        => new()
        {
            effectId = effectId,
            sequence = sequence,
            initializeContextId = InitializeContextId,
            sourceSpawnerContextId = 30,
            eventNameId = SpawnId,
            particleSystemId = ParticleSystemId,
            spawnSystemId = 13,
            startEventIndex = 0,
            recordCount = 1,
            strideBytes = sizeof(uint)
        };

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateOutputEffect()
    {
        var field = new VFXRuntimeAttributeData("value", VFXRuntimeValueType.Float, 0, 1);
        var data = new VFXRuntimeAssetData(
            new[] { field }, Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            Array.Empty<VFXRuntimeSystemData>(),
            new[]
            {
                new VFXRuntimeOutputEventData(
                    "Hit", new long[] { 30 },
                    Array.Empty<VFXRuntimeOutputEventMapping>(),
                    new[] { field }, 1)
            });
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return (new VisualEffect { visualEffectAsset = asset }, asset);
    }

    private static VisualEffect CreateInputParticleEffect()
    {
        VFXRuntimeInitializeKernelData kernel = ParticleKernel();
        var data = new VFXRuntimeAssetData(
            new[] { new VFXRuntimeAttributeData("value", VFXRuntimeValueType.Float, 0, 1) },
            new[] { "Spawn" },
            new[]
            {
                new VFXRuntimeInputEventData("Spawn", new[]
                {
                    new VFXRuntimeInputEventTargetData(
                        InitializeContextId, "Particles", Array.Empty<long>(),
                        Array.Empty<string>(), kernel)
                })
            },
            new[]
            {
                new VFXRuntimeSystemData(
                    "Particles", VFXRuntimeSystemKind.Particle, 4)
            },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return new VisualEffect { visualEffectAsset = asset };
    }

    private static NativeGraphicsDevice CreateDevice(
        GraphicsDeviceType type = GraphicsDeviceType.Null)
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            type, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static ulong EffectId(VisualEffect effect)
        => unchecked((ulong)(uint)effect.GetInstanceID());

    private static void DestroyLiveEffects()
    {
        foreach (VisualEffect effect in UnityEngine.Object.FindObjectsOfType<VisualEffect>())
            UnityEngine.Object.DestroyImmediate(effect);
    }

    private static byte[] Words(params uint[] words)
    {
        var bytes = new byte[words.Length * sizeof(uint)];
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(index * sizeof(uint), sizeof(uint)), words[index]);
        return bytes;
    }

    private static IEnumerable<uint> ToWords(byte[] bytes)
    {
        for (int offset = 0; offset < bytes.Length; offset += sizeof(uint))
            yield return BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(offset, sizeof(uint)));
    }

    private static uint Float(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));
}
