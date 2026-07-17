using System.Buffers.Binary;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXOutputEventTests
{
    [Fact]
    public void NativeQueue_PreservesDescriptorAndCount()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        byte[] payload = FloatRecords(3f, 5f);

        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, payload, 1, 2));
        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(1, count);
        Assert.True(device.TryPeekVFXOutputEvent(effectId, out AnityNative.GraphicsVFXEventUploadInfo info));
        Assert.Equal(HitId, info.desc.eventNameId);
        Assert.Equal(2, info.desc.recordCount);
        Assert.Equal(4, info.desc.strideBytes);
        Assert.Equal(8, info.byteCount);
    }

    [Fact]
    public void NativeQueue_DequeueReturnsBitExactPayload()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        byte[] payload = FloatRecords(-0f, 7.25f);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, payload, 1, 2));

        Assert.True(device.TryDequeueVFXOutputEvent(effectId, 1, payload.Length, out byte[] actual));

        Assert.Equal(payload, actual);
        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void NativeQueue_IsFifoAcrossBatches()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, FloatRecords(1f), 1, 1));
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 2, FloatRecords(2f), 1, 1));

        Assert.True(device.TryPeekVFXOutputEvent(effectId, out AnityNative.GraphicsVFXEventUploadInfo first));
        Assert.Equal(1ul, first.desc.sequence);
        Assert.True(device.TryDequeueVFXOutputEvent(effectId, 1, 4, out _));
        Assert.True(device.TryPeekVFXOutputEvent(effectId, out AnityNative.GraphicsVFXEventUploadInfo second));
        Assert.Equal(2ul, second.desc.sequence);
    }

    [Fact]
    public void NativeQueue_RejectsDuplicateSequence()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 4, FloatRecords(1f), 1, 1));

        Assert.False(device.EnqueueVFXOutputEvent(effectId, HitId, 4, FloatRecords(2f), 1, 1));
        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(1, count);
    }

    [Fact]
    public void NativeQueue_RejectsOlderSequenceAfterDequeue()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 7, FloatRecords(1f), 1, 1));
        Assert.True(device.TryDequeueVFXOutputEvent(effectId, 7, 4, out _));

        Assert.False(device.EnqueueVFXOutputEvent(effectId, HitId, 6, FloatRecords(2f), 1, 1));
    }

    [Fact]
    public void NativeQueue_WrongExpectedSequenceDoesNotPop()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 3, FloatRecords(1f), 1, 1));

        Assert.False(device.TryDequeueVFXOutputEvent(effectId, 2, 4, out _));
        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(1, count);
    }

    [Fact]
    public void ManagedDispatch_DecodesSingleFloatRecord()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        var values = new List<float>();
        effect.outputEventReceived += args => values.Add(args.eventAttribute.GetFloat("spawnCount"));
        Assert.True(device.EnqueueVFXOutputEvent(EffectId(effect), HitId, 1, FloatRecords(4.5f), 1, 1));

        int delivered = effect.ProcessOutputEvents(device);

        Assert.Equal(1, delivered);
        Assert.Equal(new[] { 4.5f }, values);
    }

    [Fact]
    public void ManagedDispatch_DecodesEveryRecordInOrder()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        var values = new List<float>();
        effect.outputEventReceived += args => values.Add(args.eventAttribute.GetFloat("spawnCount"));
        Assert.True(device.EnqueueVFXOutputEvent(
            EffectId(effect), HitId, 1, FloatRecords(1f, 2f, 3f), 1, 3));

        Assert.Equal(3, effect.ProcessOutputEvents(device));
        Assert.Equal(new[] { 1f, 2f, 3f }, values);
        Assert.True(device.TryGetVFXOutputEventCount(EffectId(effect), out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void ManagedDispatch_PreservesMultipleBatchOrder()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        var values = new List<float>();
        effect.outputEventReceived += args => values.Add(args.eventAttribute.GetFloat("spawnCount"));
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, FloatRecords(8f), 1, 1));
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 2, FloatRecords(9f), 1, 1));

        Assert.Equal(2, effect.ProcessOutputEvents(device));
        Assert.Equal(new[] { 8f, 9f }, values);
    }

    [Fact]
    public void ManagedDispatch_DecodesBoolIntUintAndVectorsBitExactly()
    {
        VFXRuntimeAttributeData[] fields =
        {
            A("alive", VFXRuntimeValueType.Boolean, 0),
            A("index", VFXRuntimeValueType.Int32, 1),
            A("flags", VFXRuntimeValueType.UInt32, 2),
            A("uv", VFXRuntimeValueType.Float2, 3),
            A("position", VFXRuntimeValueType.Float3, 5),
            A("color", VFXRuntimeValueType.Float4, 8)
        };
        (VisualEffect effect, _) = CreateEffect(fields, 12);
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        var payload = new byte[48];
        Write(payload, 0, 1u); Write(payload, 1, unchecked((uint)-7)); Write(payload, 2, 0xf0000001u);
        for (int i = 0; i < 9; i++) WriteFloat(payload, i + 3, i + 0.25f);
        VFXOutputEventArgs received = default;
        effect.outputEventReceived += args => received = args;
        Assert.True(device.EnqueueVFXOutputEvent(EffectId(effect), HitId, 1, payload, 12, 1));

        Assert.Equal(1, effect.ProcessOutputEvents(device));
        Assert.True(received.eventAttribute.GetBool("alive"));
        Assert.Equal(-7, received.eventAttribute.GetInt("index"));
        Assert.Equal(0xf0000001u, received.eventAttribute.GetUint("flags"));
        Assert.Equal(new Vector2(0.25f, 1.25f), received.eventAttribute.GetVector2("uv"));
        Assert.Equal(new Vector3(2.25f, 3.25f, 4.25f), received.eventAttribute.GetVector3("position"));
        Assert.Equal(new Vector4(5.25f, 6.25f, 7.25f, 8.25f), received.eventAttribute.GetVector4("color"));
    }

    [Fact]
    public void ManagedDispatch_UnknownOutputNameIsConsumedWithoutCallback()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        int callbacks = 0;
        effect.outputEventReceived += _ => callbacks++;
        Assert.True(device.EnqueueVFXOutputEvent(
            EffectId(effect), Shader.PropertyToID("Unknown"), 1, FloatRecords(1f), 1, 1));

        Assert.Equal(0, effect.ProcessOutputEvents(device));
        Assert.Equal(0, callbacks);
        Assert.True(device.TryGetVFXOutputEventCount(EffectId(effect), out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void ManagedDispatch_RejectsAndConsumesMismatchedStride()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        Assert.True(device.EnqueueVFXOutputEvent(EffectId(effect), HitId, 1, new byte[8], 2, 1));

        Assert.Throws<InvalidDataException>(() => effect.ProcessOutputEvents(device));
        Assert.True(device.TryGetVFXOutputEventCount(EffectId(effect), out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void ManagedDispatch_EmptyQueueIsNoOp()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();

        Assert.Equal(0, effect.ProcessOutputEvents(device));
    }

    [Fact]
    public void ManagedDispatch_RemovedAssetDrainsStaleNativeOutput()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        ulong effectId = EffectId(effect);
        Assert.True(device.EnqueueVFXOutputEvent(effectId, HitId, 1, FloatRecords(2f), 1, 1));
        effect.visualEffectAsset = null;

        Assert.Equal(0, effect.ProcessOutputEvents(device));
        Assert.True(device.TryGetVFXOutputEventCount(effectId, out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void VFXManagerCameraProcess_DeliversNativeOutputBeforeAdvancing()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateFloatEffect();
        var gameObject = new GameObject("VFX");
        VisualEffect component = gameObject.AddComponent<VisualEffect>();
        component.visualEffectAsset = effect.visualEffectAsset;
        float received = 0;
        component.outputEventReceived += args => received = args.eventAttribute.GetFloat("spawnCount");
        Assert.True(device.EnqueueVFXOutputEvent(
            EffectId(component), HitId, 1, FloatRecords(12f), 1, 1));
        var cameraObject = new GameObject("Camera");
        Camera camera = cameraObject.AddComponent<Camera>();

#pragma warning disable CS0618
        VFXManager.ProcessCamera(camera);
#pragma warning restore CS0618

        Assert.Equal(12f, received);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(gameObject);
    }

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateFloatEffect()
        => CreateEffect(new[] { A("spawnCount", VFXRuntimeValueType.Float, 0) }, 1);

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateEffect(
        IReadOnlyList<VFXRuntimeAttributeData> fields,
        int strideWords)
    {
        var data = new VFXRuntimeAssetData(
            fields,
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            Array.Empty<VFXRuntimeSystemData>(),
            new[]
            {
                new VFXRuntimeOutputEventData(
                    "Hit", new long[] { 30 }, Array.Empty<VFXRuntimeOutputEventMapping>(), fields, strideWords)
            });
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return (new VisualEffect { visualEffectAsset = asset }, asset);
    }

    private static VFXRuntimeAttributeData A(string name, VFXRuntimeValueType type, int offset)
        => new(name, type, offset, VFXRuntimeAssetData.WordCount(type));

    private static NativeGraphicsDevice CreateDevice()
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(GraphicsDeviceType.Null, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static ulong EffectId(VisualEffect effect)
        => unchecked((ulong)(uint)effect.GetInstanceID());

    private static byte[] FloatRecords(params float[] values)
    {
        var bytes = new byte[values.Length * sizeof(uint)];
        for (int index = 0; index < values.Length; index++) WriteFloat(bytes, index, values[index]);
        return bytes;
    }

    private static void WriteFloat(byte[] bytes, int word, float value)
        => Write(bytes, word, unchecked((uint)BitConverter.SingleToInt32Bits(value)));

    private static void Write(byte[] bytes, int word, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * sizeof(uint), sizeof(uint)), value);

    private static readonly int HitId = Shader.PropertyToID("Hit");
}
