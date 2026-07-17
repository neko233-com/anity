using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXInitializeDispatchTests
{
    [Fact]
    public void NativeLayouts_MatchCAbi()
    {
        Assert.Equal(56, Marshal.SizeOf<AnityNative.GraphicsVFXInitializeDispatchDesc>());
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXInitializeDispatchInfo>());
    }

    [Fact]
    public void CpuReference_CopiesSelectedRecordRangeAndMetadata()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(101, 3, 40, 1, 2, 8);

        Assert.True(device.SubmitVFXInitializeDispatch(desc, Words(10, 11, 20, 21, 30, 31, 40, 41)));
        Assert.True(device.TryGetVFXInitializeDispatch(101, 40, out NativeVFXInitializeDispatch? dispatch));

        Assert.Equal(0, dispatch!.Info.backendKind);
        Assert.Equal(32, dispatch.Info.sourceByteCount);
        Assert.Equal(16, dispatch.Info.outputByteCount);
        Assert.Equal(30, dispatch.Info.desc.sourceSpawnerContextId);
        Assert.Equal(Words(20, 21, 30, 31), dispatch.Records);
    }

    [Fact]
    public void DifferentInitializeContexts_HaveIndependentLatestResults()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.SubmitVFXInitializeDispatch(Desc(102, 1, 40, 0, 1, 4), Words(1)));
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(102, 1, 50, 1, 1, 4), Words(1, 2)));

        Assert.True(device.TryGetVFXInitializeDispatch(102, 40, out NativeVFXInitializeDispatch? first));
        Assert.True(device.TryGetVFXInitializeDispatch(102, 50, out NativeVFXInitializeDispatch? second));
        Assert.Equal(Words(1), first!.Records);
        Assert.Equal(Words(2), second!.Records);
    }

    [Fact]
    public void NewerSequence_ReplacesTargetResult()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(103, 1, 40, 0, 1, 4), Words(1)));
        Assert.True(device.TryGetVFXInitializeDispatch(103, 40, out NativeVFXInitializeDispatch? first));

        Assert.True(device.SubmitVFXInitializeDispatch(Desc(103, 2, 40, 0, 1, 4), Words(2)));
        Assert.True(device.TryGetVFXInitializeDispatch(103, 40, out NativeVFXInitializeDispatch? second));

        Assert.Equal(2ul, second!.Info.desc.sequence);
        Assert.True(second.Info.dispatchGeneration > first!.Info.dispatchGeneration);
        Assert.Equal(Words(2), second.Records);
    }

    [Fact]
    public void OlderSequence_IsRejectedWithoutMutation()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(104, 2, 40, 0, 1, 4), Words(2)));

        Assert.False(device.SubmitVFXInitializeDispatch(Desc(104, 1, 40, 0, 1, 4), Words(1)));
        Assert.True(device.TryGetVFXInitializeDispatch(104, 40, out NativeVFXInitializeDispatch? dispatch));
        Assert.Equal(2ul, dispatch!.Info.desc.sequence);
        Assert.Equal(Words(2), dispatch.Records);
    }

    [Fact]
    public void IdenticalRetry_IsIdempotent()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(105, 1, 40, 1, 1, 4);
        byte[] largerSource = Words(1, 2, 3);
        Assert.True(device.SubmitVFXInitializeDispatch(desc, Words(1, 2)));
        Assert.True(device.TryGetVFXInitializeDispatch(105, 40, out NativeVFXInitializeDispatch? first));

        Assert.True(device.SubmitVFXInitializeDispatch(desc, largerSource));
        Assert.True(device.TryGetVFXInitializeDispatch(105, 40, out NativeVFXInitializeDispatch? second));

        Assert.Equal(first!.Info.dispatchGeneration, second!.Info.dispatchGeneration);
        Assert.Equal(Words(2), second.Records);
    }

    [Fact]
    public void SameSequenceWithDifferentContent_IsRejected()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(106, 1, 40, 0, 1, 4);
        Assert.True(device.SubmitVFXInitializeDispatch(desc, Words(1)));

        Assert.False(device.SubmitVFXInitializeDispatch(desc, Words(2)));
        Assert.True(device.TryGetVFXInitializeDispatch(106, 40, out NativeVFXInitializeDispatch? dispatch));
        Assert.Equal(Words(1), dispatch!.Records);
    }

    [Fact]
    public void SourceRangePastEnd_IsRejected()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;

        Assert.False(device.SubmitVFXInitializeDispatch(Desc(107, 1, 40, 2, 2, 4), Words(1, 2, 3)));
        Assert.False(device.TryGetVFXInitializeDispatch(107, 40, out _));
    }

    [Fact]
    public void MisalignedStride_IsRejectedByNativeAbi()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(108, 1, 40, 0, 1, 2);

        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.Graphics_SubmitVFXInitializeDispatch(
                device.Handle, ref desc, new byte[2], 2));
    }

    [Fact]
    public void InsufficientReadbackCapacity_DoesNotMutateResult()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(109, 1, 40, 0, 2, 4), Words(1, 2)));

        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.Graphics_ReadbackVFXInitializeDispatch(
                device.Handle, 109, 40, new byte[4], 4, out int written));
        Assert.Equal(0, written);
        Assert.True(device.TryGetVFXInitializeDispatch(109, 40, out NativeVFXInitializeDispatch? dispatch));
        Assert.Equal(Words(1, 2), dispatch!.Records);
    }

    [Fact]
    public void ProcessInputEvents_SubmitsCompiledInitializeTarget()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = CreateMappedEffect(40);
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("spawnCount", 7.5f);
        effect.SendEvent("Spawn", attribute);

        Assert.Equal(1, effect.ProcessInputEvents(device));
        Assert.True(device.TryGetVFXInitializeDispatch(
            EffectId(effect), 40, out NativeVFXInitializeDispatch? dispatch));

        Assert.Equal(SpawnId, dispatch!.Info.desc.eventNameId);
        Assert.Equal(Shader.PropertyToID("Particles40"), dispatch.Info.desc.particleSystemId);
        Assert.Equal(Shader.PropertyToID("Rate40"), dispatch.Info.desc.spawnSystemId);
        Assert.Equal(30, dispatch.Info.desc.sourceSpawnerContextId);
        Assert.Equal(Words(unchecked((uint)BitConverter.SingleToInt32Bits(7.5f))), dispatch.Records);
    }

    [Fact]
    public void ProcessInputEvents_BranchingTargetsSubmitIndependently()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = CreateMappedEffect(40, 50);
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("spawnCount", 2f);
        effect.SendEvent("Spawn", attribute);

        Assert.Equal(1, effect.ProcessInputEvents(device));

        Assert.True(device.TryGetVFXInitializeDispatch(EffectId(effect), 40, out _));
        Assert.True(device.TryGetVFXInitializeDispatch(EffectId(effect), 50, out _));
    }

    [Fact]
    public void UnknownInputEvent_DoesNotCreateInitializeDispatch()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = CreateMappedEffect(40);
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        effect.SendEvent("Unknown", attribute);

        Assert.Equal(1, effect.ProcessInputEvents(device));
        Assert.False(device.TryGetVFXInitializeDispatch(EffectId(effect), 40, out _));
    }

    [Fact]
    public void BulkTransaction_PublishesEveryTargetTogether()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var descs = new[]
        {
            Desc(201, 1, 40, 0, 1, 4),
            Desc(201, 1, 50, 1, 1, 4)
        };

        Assert.True(device.SubmitVFXInitializeDispatches(descs, Words(10, 20)));
        Assert.True(device.TryGetVFXInitializeDispatch(201, 40, out NativeVFXInitializeDispatch? first));
        Assert.True(device.TryGetVFXInitializeDispatch(201, 50, out NativeVFXInitializeDispatch? second));
        Assert.Equal(Words(10), first!.Records);
        Assert.Equal(Words(20), second!.Records);
    }

    [Fact]
    public void BulkTransaction_InvalidLaterDescriptorPublishesNothing()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var descs = new[]
        {
            Desc(202, 1, 40, 0, 1, 4),
            Desc(202, 1, 50, 3, 2, 4)
        };
        byte[] source = Words(1, 2, 3, 4);

        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.Graphics_SubmitVFXInitializeDispatches(
                device.Handle, descs, descs.Length, source, source.Length));
        Assert.False(device.TryGetVFXInitializeDispatch(202, 40, out _));
        Assert.False(device.TryGetVFXInitializeDispatch(202, 50, out _));
    }

    [Fact]
    public void BulkTransaction_ConflictRollsBackUnrelatedNewTarget()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(203, 1, 40, 0, 1, 4), Words(1)));
        var descs = new[]
        {
            Desc(203, 2, 50, 0, 1, 4),
            Desc(203, 1, 40, 1, 1, 4)
        };

        Assert.False(device.SubmitVFXInitializeDispatches(descs, Words(2, 9)));
        Assert.False(device.TryGetVFXInitializeDispatch(203, 50, out _));
        Assert.True(device.TryGetVFXInitializeDispatch(203, 40, out NativeVFXInitializeDispatch? existing));
        Assert.Equal(Words(1), existing!.Records);
    }

    [Fact]
    public void BulkTransaction_AscendingSameTargetPublishesLatestBatch()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var descs = new[]
        {
            Desc(204, 1, 40, 0, 1, 4),
            Desc(204, 2, 40, 1, 1, 4)
        };

        Assert.True(device.SubmitVFXInitializeDispatches(descs, Words(1, 2)));
        Assert.True(device.TryGetVFXInitializeDispatch(204, 40, out NativeVFXInitializeDispatch? latest));
        Assert.Equal(2ul, latest!.Info.desc.sequence);
        Assert.Equal(Words(2), latest.Records);
    }

    [Fact]
    public void BulkTransaction_NonMonotonicSameTargetRollsBack()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var descs = new[]
        {
            Desc(205, 2, 40, 1, 1, 4),
            Desc(205, 1, 40, 0, 1, 4)
        };

        Assert.False(device.SubmitVFXInitializeDispatches(descs, Words(1, 2)));
        Assert.False(device.TryGetVFXInitializeDispatch(205, 40, out _));
    }

    [Fact]
    public void BulkTransaction_IdenticalRetryDoesNotAdvanceGenerations()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var descs = new[]
        {
            Desc(206, 1, 40, 0, 1, 4),
            Desc(206, 1, 50, 1, 1, 4)
        };
        Assert.True(device.SubmitVFXInitializeDispatches(descs, Words(1, 2)));
        Assert.True(device.TryGetVFXInitializeDispatch(206, 40, out NativeVFXInitializeDispatch? firstBefore));
        Assert.True(device.TryGetVFXInitializeDispatch(206, 50, out NativeVFXInitializeDispatch? secondBefore));

        Assert.True(device.SubmitVFXInitializeDispatches(descs, Words(1, 2, 3)));
        Assert.True(device.TryGetVFXInitializeDispatch(206, 40, out NativeVFXInitializeDispatch? firstAfter));
        Assert.True(device.TryGetVFXInitializeDispatch(206, 50, out NativeVFXInitializeDispatch? secondAfter));
        Assert.Equal(firstBefore!.Info.dispatchGeneration, firstAfter!.Info.dispatchGeneration);
        Assert.Equal(secondBefore!.Info.dispatchGeneration, secondAfter!.Info.dispatchGeneration);
    }

    [Fact]
    public void ClearEffectState_RemovesUploadAndInputPlan()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.UploadVFXEvent(207, SpawnId, 1, Words(1), 1, 1));
        Assert.True(device.TryGetVFXEventDispatchPlan(207, out _));

        Assert.True(device.ClearVFXEffectState(207));

        Assert.False(device.TryGetVFXEventUploadInfo(207, out _));
        Assert.False(device.TryGetVFXEventDispatchPlan(207, out _));
    }

    [Fact]
    public void ClearEffectState_RemovesOutputQueueAndSequence()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.EnqueueVFXOutputEvent(208, SpawnId, 4, Words(1), 1, 1));
        Assert.True(device.TryGetVFXOutputEventCount(208, out int before));
        Assert.Equal(1, before);

        Assert.True(device.ClearVFXEffectState(208));

        Assert.True(device.TryGetVFXOutputEventCount(208, out int after));
        Assert.Equal(0, after);
        Assert.True(device.EnqueueVFXOutputEvent(208, SpawnId, 1, Words(2), 1, 1));
    }

    [Fact]
    public void ClearEffectState_RemovesAllTargetsWithoutTouchingOtherEffect()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.SubmitVFXInitializeDispatches(new[]
        {
            Desc(209, 1, 40, 0, 1, 4),
            Desc(209, 1, 50, 1, 1, 4)
        }, Words(1, 2)));
        Assert.True(device.SubmitVFXInitializeDispatch(Desc(210, 1, 40, 0, 1, 4), Words(3)));

        Assert.True(device.ClearVFXEffectState(209));

        Assert.False(device.TryGetVFXInitializeDispatch(209, 40, out _));
        Assert.False(device.TryGetVFXInitializeDispatch(209, 50, out _));
        Assert.True(device.TryGetVFXInitializeDispatch(210, 40, out _));
    }

    [Fact]
    public void AssetSwitch_ClearsDeviceOwnedEffectState()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = CreateMappedEffect(40);
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        effect.SendEvent("Spawn", attribute);
        effect.ProcessInputEvents(device);
        Assert.True(device.TryGetVFXInitializeDispatch(EffectId(effect), 40, out _));

        effect.visualEffectAsset = new VisualEffectAsset();

        Assert.False(device.TryGetVFXInitializeDispatch(EffectId(effect), 40, out _));
    }

    [Fact]
    public void DestroyImmediate_ClearsDeviceOwnedEffectState()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;
        var gameObject = new GameObject("VFX teardown");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = CreateMappedEffect(40).visualEffectAsset;
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        effect.SendEvent("Spawn", attribute);
        effect.ProcessInputEvents(device);
        ulong effectId = EffectId(effect);
        Assert.True(device.TryGetVFXInitializeDispatch(effectId, 40, out _));

        UnityEngine.Object.DestroyImmediate(gameObject);

        Assert.False(device.TryGetVFXInitializeDispatch(effectId, 40, out _));
        Assert.False(device.TryGetVFXEventDispatchPlan(effectId, out _));
    }

    [Fact]
    public void ClearUnknownEffect_IsIdempotentSuccess()
    {
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Null);
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.ClearVFXEffectState(999_999));
        Assert.True(device.ClearVFXEffectState(999_999));
    }

    [Fact]
    public void MetalBackend_UsesRealComputeEncoderAndReadback()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        using NativeGraphicsDevice device = CreateDevice(GraphicsDeviceType.Metal);
        if (device.Handle == IntPtr.Zero) return;
        Assert.Equal(GraphicsDeviceType.Metal, device.DeviceType);
        AnityNative.GraphicsVFXInitializeDispatchDesc desc = Desc(110, 1, 40, 1, 2, 8);

        Assert.True(device.SubmitVFXInitializeDispatch(
            desc, Words(10, 11, 20, 21, 30, 31, 40, 41)));
        Assert.True(device.TryGetVFXInitializeDispatch(110, 40, out NativeVFXInitializeDispatch? dispatch));

        Assert.Equal(2, dispatch!.Info.backendKind);
        Assert.Equal(Words(20, 21, 30, 31), dispatch.Records);
    }

    private static AnityNative.GraphicsVFXInitializeDispatchDesc Desc(
        ulong effectId, ulong sequence, long initializeContextId,
        int startEventIndex, int recordCount, int strideBytes)
        => new()
        {
            effectId = effectId,
            sequence = sequence,
            initializeContextId = initializeContextId,
            sourceSpawnerContextId = 30,
            eventNameId = SpawnId,
            particleSystemId = Shader.PropertyToID("Particles"),
            spawnSystemId = Shader.PropertyToID("Rate"),
            startEventIndex = startEventIndex,
            recordCount = recordCount,
            strideBytes = strideBytes
        };

    private static VisualEffect CreateMappedEffect(params long[] initializeContextIds)
    {
        var targets = initializeContextIds.Select(id =>
            new VFXRuntimeInputEventTargetData(
                id, $"Particles{id}", new long[] { 30 }, new[] { $"Rate{id}" }))
            .ToArray();
        var systems = initializeContextIds.SelectMany(id => new[]
        {
            new VFXRuntimeSystemData($"Rate{id}", VFXRuntimeSystemKind.Spawn, 0),
            new VFXRuntimeSystemData($"Particles{id}", VFXRuntimeSystemKind.Particle, 1024)
        }).ToArray();
        var data = new VFXRuntimeAssetData(
            new[]
            {
                new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1)
            },
            new[] { "Spawn" },
            new[] { new VFXRuntimeInputEventData("Spawn", targets) },
            systems,
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return new VisualEffect { visualEffectAsset = asset };
    }

    private static NativeGraphicsDevice CreateDevice(GraphicsDeviceType type)
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(type, 16, 16, false);
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

    private static readonly int SpawnId = Shader.PropertyToID("Spawn");
}
