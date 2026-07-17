using System.Buffers.Binary;
using System.Reflection;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class VFXEventAttributeTests
{
    [Fact]
    public void PublicSurfaceMatchesUnityEventAttributeShape()
    {
        Type type = typeof(VFXEventAttribute);
        Assert.True(type.IsSealed);
        Assert.Contains(typeof(IDisposable), type.GetInterfaces());
        Assert.Single(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(50, methods.Length);
        Assert.Equal(16, methods.Count(method => method.Name.StartsWith("Has", StringComparison.Ordinal)));
        Assert.Equal(16, methods.Count(method => method.Name.StartsWith("Set", StringComparison.Ordinal)));
        Assert.Equal(16, methods.Count(method => method.Name.StartsWith("Get", StringComparison.Ordinal)));
    }

    [Fact]
    public void AssetSchemaMakesHasAvailableBeforeAnySet()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        Assert.True(attribute.HasBool("alive"));
        Assert.True(attribute.HasInt("index"));
        Assert.True(attribute.HasUint("flags"));
        Assert.True(attribute.HasFloat("spawnCount"));
        Assert.True(attribute.HasVector2("uv"));
        Assert.True(attribute.HasVector3("position"));
        Assert.True(attribute.HasVector4("color"));
        Assert.True(attribute.HasMatrix4x4("transform"));
    }

    [Fact]
    public void StringAndPropertyIdOverloadsAddressSameValue()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        int id = Shader.PropertyToID("spawnCount");
        attribute.SetFloat("spawnCount", 12.5f);
        Assert.Equal(12.5f, attribute.GetFloat(id));
        attribute.SetFloat(id, 7.25f);
        Assert.Equal(7.25f, attribute.GetFloat("spawnCount"));
    }

    [Fact]
    public void ScalarTypesRoundTripWithoutNumericCoercion()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetBool("alive", true);
        attribute.SetInt("index", -17);
        attribute.SetUint("flags", 0xfedcba98u);
        attribute.SetFloat("spawnCount", -0.0f);
        Assert.True(attribute.GetBool("alive"));
        Assert.Equal(-17, attribute.GetInt("index"));
        Assert.Equal(0xfedcba98u, attribute.GetUint("flags"));
        Assert.Equal(unchecked((uint)BitConverter.SingleToInt32Bits(-0.0f)),
            unchecked((uint)BitConverter.SingleToInt32Bits(attribute.GetFloat("spawnCount"))));
        Assert.False(attribute.HasFloat("index"));
        Assert.Equal(0f, attribute.GetFloat("index"));
    }

    [Fact]
    public void VectorAndMatrixTypesRoundTripExactly()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        var uv = new Vector2(1.25f, -2.5f);
        var position = new Vector3(3f, 4f, 5f);
        var color = new Vector4(6f, 7f, 8f, 9f);
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.m03 = 10f;
        matrix.m21 = -11f;
        attribute.SetVector2("uv", uv);
        attribute.SetVector3("position", position);
        attribute.SetVector4("color", color);
        attribute.SetMatrix4x4("transform", matrix);
        Assert.Equal(uv, attribute.GetVector2("uv"));
        Assert.Equal(position, attribute.GetVector3("position"));
        Assert.Equal(color, attribute.GetVector4("color"));
        Assert.Equal(matrix, attribute.GetMatrix4x4("transform"));
    }

    [Fact]
    public void UnknownAndWrongTypedSetDoNotMutateCompiledLayout()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("unknown", 42f);
        attribute.SetInt("spawnCount", 9);
        Assert.False(attribute.HasFloat("unknown"));
        Assert.False(attribute.HasInt("spawnCount"));
        Assert.Equal(0f, attribute.GetFloat("unknown"));
        Assert.Equal(0, attribute.GetInt("spawnCount"));
    }

    [Fact]
    public void ManuallyDefinedSpawnCountRetainsZeroDefault()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;

        Assert.Equal(0f, attribute.GetFloat("spawnCount"));
    }

    [Fact]
    public void CopyConstructorCreatesIndependentValueSnapshot()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute original = effect.CreateVFXEventAttribute()!;
        original.SetFloat("spawnCount", 3f);
        using var copy = new VFXEventAttribute(original);
        original.SetFloat("spawnCount", 99f);
        Assert.Equal(3f, copy.GetFloat("spawnCount"));
        Assert.Same(original.vfxAsset, copy.vfxAsset);
    }

    [Fact]
    public void NullCopyConstructorUsesUnityExceptionParameterText()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new VFXEventAttribute((VFXEventAttribute)null!));
        Assert.Equal("VFXEventAttribute expect a non null attribute", exception.ParamName);
    }

    [Fact]
    public void CopyValuesFromClearsOldValuesAndFiltersToDestinationSchema()
    {
        (VisualEffect sourceEffect, _) = CreateEffect();
        using VFXEventAttribute source = sourceEffect.CreateVFXEventAttribute()!;
        source.SetFloat("spawnCount", 8f);
        source.SetInt("index", -4);

        var destinationAsset = new VisualEffectAsset();
        destinationAsset.DefineEventAttribute("spawnCount", typeof(float));
        var destinationEffect = new VisualEffect { visualEffectAsset = destinationAsset };
        using VFXEventAttribute destination = destinationEffect.CreateVFXEventAttribute()!;
        destination.SetFloat("spawnCount", 2f);
        destination.CopyValuesFrom(source);
        Assert.Equal(8f, destination.GetFloat("spawnCount"));
        Assert.False(destination.HasInt("index"));
        Assert.Equal(0, destination.GetInt("index"));
        Assert.Throws<ArgumentNullException>(() => destination.CopyValuesFrom(null!));
    }

    [Fact]
    public void DisposeIsIdempotentAndRejectsFurtherNativeStyleAccess()
    {
        (VisualEffect effect, _) = CreateEffect();
        VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.Dispose();
        attribute.Dispose();
        Assert.Throws<ObjectDisposedException>(() => attribute.HasFloat("spawnCount"));
        Assert.Throws<ObjectDisposedException>(() => attribute.SetFloat("spawnCount", 1f));
        Assert.Throws<ObjectDisposedException>(() => attribute.GetFloat("spawnCount"));
    }

    [Fact]
    public void PackedRecordUsesLittleEndianCompiledWordOffsets()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetBool("alive", true);
        attribute.SetInt("index", -2);
        attribute.SetUint("flags", 0x89abcdefu);
        attribute.SetFloat("spawnCount", 2.5f);
        attribute.SetVector2("uv", new Vector2(3.5f, -4.5f));
        byte[] bytes = attribute.PackValues(out int strideWords);
        Assert.Equal(29, strideWords);
        Assert.Equal(116, bytes.Length);
        Assert.Equal(1u, Word(bytes, 0));
        Assert.Equal(unchecked((uint)-2), Word(bytes, 1));
        Assert.Equal(0x89abcdefu, Word(bytes, 2));
        Assert.Equal(FloatWord(2.5f), Word(bytes, 3));
        Assert.Equal(FloatWord(3.5f), Word(bytes, 4));
        Assert.Equal(FloatWord(-4.5f), Word(bytes, 5));
    }

    [Fact]
    public void SendEventRejectsAttributeCreatedForAnotherAsset()
    {
        (VisualEffect first, _) = CreateEffect();
        (VisualEffect second, _) = CreateEffect();
        using VFXEventAttribute attribute = first.CreateVFXEventAttribute()!;
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => second.SendEvent("Spawn", attribute));
        Assert.Equal(
            "Invalid VFXEventAttribute provided to VisualEffect. It has been created with another VisualEffectAsset. Use CreateVFXEventAttribute.",
            exception.Message);
    }

    [Fact]
    public void SendEventQueuesImmutableSnapshotAndPackedPayload()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("spawnCount", 4f);
        effect.SendEvent("Spawn", attribute);
        attribute.SetFloat("spawnCount", 100f);
        Assert.True(effect.TryDequeueEvent(out VFXPendingEvent pending));
        Assert.Equal(Shader.PropertyToID("Spawn"), pending.NameId);
        Assert.Equal(1ul, pending.Sequence);
        Assert.Equal(4f, pending.EventAttribute!.GetFloat("spawnCount"));
        Assert.Equal(FloatWord(4f), Word(pending.Payload, 3));
    }

    [Fact]
    public void ConcurrentSendMaintainsUniqueOrderedQueueSequence()
    {
        (VisualEffect effect, _) = CreateEffect();
        Parallel.For(0, 32, index => effect.SendEvent(index));
        var sequences = new List<ulong>();
        while (effect.TryDequeueEvent(out VFXPendingEvent pending)) sequences.Add(pending.Sequence);
        Assert.Equal(32, sequences.Count);
        Assert.Equal(Enumerable.Range(1, 32).Select(value => (ulong)value), sequences);
    }

    [Fact]
    public void PlayStopAndReinitUseOfficialEventIds()
    {
        (VisualEffect effect, _) = CreateEffect();
        effect.Play();
        effect.Stop();
        Assert.True(effect.TryDequeueEvent(out VFXPendingEvent play));
        Assert.Equal(VisualEffectAsset.PlayEventID, play.NameId);
        Assert.True(effect.TryDequeueEvent(out VFXPendingEvent stop));
        Assert.Equal(VisualEffectAsset.StopEventID, stop.NameId);
        effect.initialEventName = "Boot";
        effect.Reinit();
        Assert.True(effect.TryDequeueEvent(out VFXPendingEvent boot));
        Assert.Equal(Shader.PropertyToID("Boot"), boot.NameId);
    }

    [Fact]
    public void OutputEventInvokesCallbackWithCachedAttribute()
    {
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("spawnCount", 6f);
        VFXOutputEventArgs received = default;
        effect.outputEventReceived += args => received = args;
        effect.InvokeOutputEvent(77, attribute);
        Assert.Equal(77, received.nameId);
        Assert.Equal(6f, received.eventAttribute.GetFloat("spawnCount"));
        Assert.NotSame(attribute, received.eventAttribute);
    }

    [Fact]
    public void AssetPublicListsReturnCompiledDefinitionsWithoutAliasing()
    {
        (_, VisualEffectAsset asset) = CreateEffect();
        asset.DefineEvent("Spawn");
        asset.DefineExposedProperty("MainTex", typeof(Texture2D), UnityEngine.Rendering.TextureDimension.Tex2D);
        var events = new List<string> { "stale" };
        var properties = new List<VFXExposedProperty>();
        asset.GetEvents(events);
        asset.GetExposedProperties(properties);
        Assert.Equal(new[] { "Spawn" }, events);
        Assert.Equal("MainTex", Assert.Single(properties).name);
        Assert.Equal(UnityEngine.Rendering.TextureDimension.Tex2D, asset.GetTextureDimension("MainTex"));
        events.Clear();
        asset.GetEvents(events);
        Assert.Equal(new[] { "Spawn" }, events);
    }

    [Fact]
    public void NativeUploadPreservesEventDescriptorAndRecordBytes()
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            UnityEngine.Rendering.GraphicsDeviceType.Null, 16, 16, false);
        if (device.Handle == IntPtr.Zero) return;
        (VisualEffect effect, _) = CreateEffect();
        using VFXEventAttribute attribute = effect.CreateVFXEventAttribute()!;
        attribute.SetFloat("spawnCount", 13f);
        effect.SendEvent("Spawn", attribute);
        ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
        Assert.True(device.TryGetVFXEventUploadInfo(effectId, out AnityNative.GraphicsVFXEventUploadInfo info));
        Assert.Equal(effectId, info.desc.effectId);
        Assert.Equal(Shader.PropertyToID("Spawn"), info.desc.eventNameId);
        Assert.Equal(1, info.desc.recordCount);
        Assert.Equal(116, info.desc.strideBytes);
        Assert.True(device.TryReadbackVFXEventRecords(effectId, out byte[] bytes));
        Assert.Equal(FloatWord(13f), Word(bytes, 3));
    }

    [Fact]
    public void CreateAttributeWithoutAssignedAssetReturnsNull()
    {
        Assert.Null(new VisualEffect().CreateVFXEventAttribute());
    }

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateEffect()
    {
        var asset = new VisualEffectAsset();
        asset.DefineEventAttribute("alive", typeof(bool));
        asset.DefineEventAttribute("index", typeof(int));
        asset.DefineEventAttribute("flags", typeof(uint));
        asset.DefineEventAttribute("spawnCount", typeof(float));
        asset.DefineEventAttribute("uv", typeof(Vector2));
        asset.DefineEventAttribute("position", typeof(Vector3));
        asset.DefineEventAttribute("color", typeof(Vector4));
        asset.DefineEventAttribute("transform", typeof(Matrix4x4));
        var effect = new VisualEffect { visualEffectAsset = asset };
        return (effect, asset);
    }

    private static uint Word(byte[] bytes, int wordIndex)
        => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(wordIndex * sizeof(uint), sizeof(uint)));

    private static uint FloatWord(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));
}
