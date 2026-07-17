using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXInputDispatchTests
{
    [Fact]
    public void SingleBatch_ProducesCompleteDispatchPlan()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        byte[] payload = Words(0x3f800000u, 0x40000000u);

        Assert.True(device.UploadVFXEvent(101, SpawnId, 1, payload, 2, 1));
        Assert.True(device.TryGetVFXEventDispatchPlan(101, out NativeVFXEventDispatchPlan? plan));

        Assert.NotNull(plan);
        Assert.Equal(101ul, plan.Info.effectId);
        Assert.Equal(1ul, plan.Info.firstSequence);
        Assert.Equal(1ul, plan.Info.lastSequence);
        Assert.Equal(1, plan.Info.batchCount);
        Assert.Equal(1, plan.Info.recordCount);
        Assert.Equal(8, plan.Info.strideBytes);
        Assert.Equal(payload, plan.Records);
    }

    [Fact]
    public void MultipleBatches_ComputeStableRecordPrefixSum()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(102, SpawnId, 1, Words(1, 2), 1, 2));
        Assert.True(device.UploadVFXEvent(102, SpawnId, 2, Words(3), 1, 1));
        Assert.True(device.UploadVFXEvent(102, SpawnId, 3, Words(4, 5, 6), 1, 3));

        Assert.True(device.TryGetVFXEventDispatchPlan(102, out NativeVFXEventDispatchPlan? plan));

        Assert.Equal(new[] { 0, 2, 3 }, plan!.Batches.Select(batch => batch.startEventIndex));
        Assert.Equal(new[] { 2, 1, 3 }, plan.Batches.Select(batch => batch.recordCount));
        Assert.Equal(6, plan.Info.recordCount);
        Assert.Equal(Words(1, 2, 3, 4, 5, 6), plan.Records);
    }

    [Fact]
    public void ZeroRecordEvent_RemainsAnOrderedDispatchBatch()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(103, PlayId, 1, Array.Empty<byte>(), 0, 0));
        Assert.True(device.UploadVFXEvent(103, SpawnId, 2, Words(7), 1, 1));

        Assert.True(device.TryGetVFXEventDispatchPlan(103, out NativeVFXEventDispatchPlan? plan));

        Assert.Equal(2, plan!.Info.batchCount);
        Assert.Equal(0, plan.Batches[0].recordCount);
        Assert.Equal(0, plan.Batches[0].strideBytes);
        Assert.Equal(0, plan.Batches[1].startEventIndex);
        Assert.Equal(Words(7), plan.Records);
    }

    [Fact]
    public void DifferentEventNames_PreserveSequenceOrder()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(104, SpawnId, 1, Words(1), 1, 1));
        Assert.True(device.UploadVFXEvent(104, StopId, 2, Words(2), 1, 1));
        Assert.True(device.UploadVFXEvent(104, PlayId, 3, Words(3), 1, 1));

        Assert.True(device.TryGetVFXEventDispatchPlan(104, out NativeVFXEventDispatchPlan? plan));

        Assert.Equal(new[] { SpawnId, StopId, PlayId },
            plan!.Batches.Select(batch => batch.eventNameId));
        Assert.Equal(new ulong[] { 1, 2, 3 }, plan.Batches.Select(batch => batch.sequence));
    }

    [Fact]
    public void LegacyReadback_StillReturnsLatestUpload()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(105, SpawnId, 1, Words(10), 1, 1));
        Assert.True(device.UploadVFXEvent(105, SpawnId, 2, Words(20), 1, 1));

        Assert.True(device.TryGetVFXEventUploadInfo(105, out AnityNative.GraphicsVFXEventUploadInfo info));
        Assert.Equal(2ul, info.desc.sequence);
        Assert.True(device.TryReadbackVFXEventRecords(105, out byte[] records));
        Assert.Equal(Words(20), records);
    }

    [Fact]
    public void DuplicateAndOlderSequences_AreRejectedWithoutChangingPlan()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(106, SpawnId, 4, Words(1), 1, 1));

        Assert.False(device.UploadVFXEvent(106, SpawnId, 4, Words(2), 1, 1));
        Assert.False(device.UploadVFXEvent(106, SpawnId, 3, Words(3), 1, 1));
        Assert.True(device.TryGetVFXEventDispatchPlan(106, out NativeVFXEventDispatchPlan? plan));
        Assert.Single(plan!.Batches);
        Assert.Equal(Words(1), plan.Records);
    }

    [Fact]
    public void InconsistentCompiledStride_IsRejectedAtEnqueue()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(107, SpawnId, 1, Words(1), 1, 1));

        Assert.False(device.UploadVFXEvent(107, SpawnId, 2, Words(2, 3), 2, 1));
        Assert.True(device.TryGetVFXEventDispatchPlan(107, out NativeVFXEventDispatchPlan? plan));
        Assert.Single(plan!.Batches);
    }

    [Fact]
    public void ConsumeThroughSequence_LeavesLaterBatchesPending()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        for (ulong sequence = 1; sequence <= 3; sequence++)
            Assert.True(device.UploadVFXEvent(108, SpawnId, sequence, Words((uint)sequence), 1, 1));

        Assert.True(device.ConsumeVFXEventDispatchPlan(108, 2));
        Assert.True(device.TryGetVFXEventDispatchPlan(108, out NativeVFXEventDispatchPlan? plan));

        Assert.Single(plan!.Batches);
        Assert.Equal(3ul, plan.Info.firstSequence);
        Assert.Equal(Words(3), plan.Records);
    }

    [Fact]
    public void ConsumeUnknownSequence_DoesNotMutateQueue()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(109, SpawnId, 1, Words(1), 1, 1));

        Assert.False(device.ConsumeVFXEventDispatchPlan(109, 2));
        Assert.True(device.TryGetVFXEventDispatchPlan(109, out NativeVFXEventDispatchPlan? plan));
        Assert.Single(plan!.Batches);
    }

    [Fact]
    public void InsufficientNativeCopyCapacity_DoesNotConsumePlan()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(110, SpawnId, 1, Words(1, 2), 1, 2));
        var tooSmall = new byte[4];

        Assert.NotEqual(AnityNative.Result.Ok, AnityNative.Graphics_CopyVFXEventDispatchRecords(
            device.Handle, 110, 1, tooSmall, tooSmall.Length, out int written));
        Assert.Equal(0, written);
        Assert.True(device.TryGetVFXEventDispatchPlan(110, out NativeVFXEventDispatchPlan? plan));
        Assert.Equal(8, plan!.Records.Length);
    }

    [Fact]
    public void ProcessInputEvents_ConsumesNativeAndManagedQueuesTogether()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("spawnCount", 5f);
        effect.SendEvent("Spawn", attribute);

        Assert.Equal(1, effect.pendingEventCount);
        Assert.Equal(1, effect.ProcessInputEvents(device));
        Assert.Equal(0, effect.pendingEventCount);
        Assert.False(device.TryGetVFXEventDispatchPlan(EffectId(effect), out _));
    }

    [Fact]
    public void ProcessInputEvents_EmptyQueueIsNoOp()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateEffect();

        Assert.Equal(0, effect.ProcessInputEvents(device));
    }

    [Fact]
    public void ConcurrentSendEvent_PreservesEveryNativeSequence()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateEffect();

        Parallel.For(0, 32, index =>
        {
            using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
            attribute.SetFloat("spawnCount", index + 1);
            effect.SendEvent("Spawn", attribute);
        });

        Assert.True(device.TryGetVFXEventDispatchPlan(EffectId(effect), out NativeVFXEventDispatchPlan? plan));
        Assert.Equal(32, plan!.Info.batchCount);
        Assert.Equal(32, plan.Info.recordCount);
        Assert.Equal(Enumerable.Range(1, 32).Select(value => (ulong)value),
            plan.Batches.Select(batch => batch.sequence));
        Assert.Equal(32, effect.ProcessInputEvents(device));
    }

    [Fact]
    public void SequenceRemainsMonotonicAfterPlanConsumption()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateEffect();
        effect.SendEvent("Spawn");
        Assert.Equal(1, effect.ProcessInputEvents(device));

        effect.SendEvent("Spawn");
        Assert.True(device.TryGetVFXEventDispatchPlan(EffectId(effect), out NativeVFXEventDispatchPlan? plan));
        Assert.Equal(2ul, plan!.Info.firstSequence);
    }

    [Fact]
    public void VFXManagerCameraProcess_ConsumesInputPlanBeforeAdvance()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (_, VisualEffectAsset asset) = CreateEffect();
        var effectObject = new GameObject("Input VFX");
        VisualEffect effect = effectObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        effect.SendEvent("Spawn");
        var cameraObject = new GameObject("Input Camera");
        Camera camera = cameraObject.AddComponent<Camera>();

#pragma warning disable CS0618
        VFXManager.ProcessCamera(camera);
#pragma warning restore CS0618

        Assert.False(device.TryGetVFXEventDispatchPlan(EffectId(effect), out _));
        Assert.Equal(0, effect.pendingEventCount);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(effectObject);
    }

    [Fact]
    public void ProcessInputEvents_BindsNativeBatchToCompiledRuntimeTargets()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateMappedEffect();
        effect.SendEvent("Spawn");

        Assert.Equal(1, effect.ProcessInputEvents(device));

        VFXBoundInputEventDispatchBatch bound = Assert.Single(effect.lastInputEventDispatch!.Batches);
        Assert.Equal(SpawnId, bound.NativeBatch.eventNameId);
        VFXRuntimeInputEventTargetData target = Assert.Single(bound.InputEvent!.Targets);
        Assert.Equal(40, target.InitializeContextId);
        Assert.Equal("Particles", target.ParticleSystemName);
        Assert.Equal(new long[] { 30 }, target.SpawnerContextIds);
        Assert.Equal(new[] { "Rate" }, target.SpawnSystemNames);
    }

    [Fact]
    public void ProcessInputEvents_UnknownEventHasNoCompiledTargetBinding()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateMappedEffect();
        effect.SendEvent("Unknown");

        Assert.Equal(1, effect.ProcessInputEvents(device));

        Assert.Null(Assert.Single(effect.lastInputEventDispatch!.Batches).InputEvent);
    }

    [Fact]
    public void ChangingAssetClearsPreviousBoundDispatchPlan()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateMappedEffect();
        effect.SendEvent("Spawn");
        effect.ProcessInputEvents(device);
        Assert.NotNull(effect.lastInputEventDispatch);

        effect.visualEffectAsset = new VisualEffectAsset();

        Assert.Null(effect.lastInputEventDispatch);
    }

    [Fact]
    public void RuntimeDataVersionOne_RemainsReadableWithEmptyDispatchTargets()
    {
        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(VersionOnePayload("Legacy"));

        Assert.Equal(new[] { "Legacy" }, restored.InputEvents);
        VFXRuntimeInputEventData input = Assert.Single(restored.InputEventDispatches);
        Assert.Equal("Legacy", input.Name);
        Assert.Empty(input.Targets);
    }

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateEffect()
    {
        var asset = new VisualEffectAsset();
        asset.DefineEvent("Spawn");
        asset.DefineEventAttribute("spawnCount", typeof(float));
        return (new VisualEffect { visualEffectAsset = asset }, asset);
    }

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateMappedEffect()
    {
        var input = new VFXRuntimeInputEventData("Spawn", new[]
        {
            new VFXRuntimeInputEventTargetData(
                40, "Particles", new long[] { 30 }, new[] { "Rate" })
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(),
            new[] { "Spawn" },
            new[] { input },
            new[]
            {
                new VFXRuntimeSystemData("Rate", VFXRuntimeSystemKind.Spawn, 0),
                new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 1024)
            },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return (new VisualEffect { visualEffectAsset = asset }, asset);
    }

    private static NativeGraphicsDevice CreateDevice()
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(GraphicsDeviceType.Null, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static ulong EffectId(VisualEffect effect)
        => unchecked((ulong)(uint)effect.GetInstanceID());

    private static byte[] Words(params uint[] words)
    {
        var bytes = new byte[words.Length * sizeof(uint)];
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(index * sizeof(uint), sizeof(uint)), words[index]);
        return bytes;
    }

    private static byte[] VersionOnePayload(string eventName)
    {
        byte[] payload;
        using (var payloadStream = new MemoryStream())
        using (var writer = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0); // attributes
            writer.Write(1); // input events
            byte[] nameBytes = Encoding.UTF8.GetBytes(eventName);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write(0); // systems
            writer.Write(0); // output events
            writer.Flush();
            payload = payloadStream.ToArray();
        }
        byte[] hash;
        using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(payload);
        using var result = new MemoryStream();
        using var envelope = new BinaryWriter(result, Encoding.UTF8, leaveOpen: true);
        envelope.Write(0x58564641u);
        envelope.Write(1u);
        envelope.Write(payload.Length);
        envelope.Write(payload);
        envelope.Write(hash);
        envelope.Flush();
        return result.ToArray();
    }

    private static readonly int SpawnId = Shader.PropertyToID("Spawn");
    private static readonly int PlayId = Shader.PropertyToID("Play");
    private static readonly int StopId = Shader.PropertyToID("Stop");
}
