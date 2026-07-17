using Anity.Core.Runtime.Native;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace UnityEngine.VFX;

public struct VFXOutputEventArgs
{
    public int nameId { get; }
    public VFXEventAttribute eventAttribute { get; }

    public VFXOutputEventArgs(int nameId, VFXEventAttribute eventAttribute)
    {
        this.nameId = nameId;
        this.eventAttribute = eventAttribute;
    }
}

[Bindings.NativeHeader("Modules/VFX/Public/ScriptBindings/VisualEffectBindings.h")]
[Bindings.NativeHeader("Modules/VFX/Public/VisualEffect.h")]
[RequireComponent(typeof(Transform))]
public class VisualEffect : Behaviour
{
    private static readonly AnityNative.GraphicsVFXSpawnerCallback NativeSpawnerCallback =
        InvokeNativeSpawnerCallback;
    private readonly object _eventLock = new();
    private readonly object _manualUpdateLock = new();
    private readonly Queue<VFXPendingEvent> _pendingEvents = new();
    private readonly Queue<VFXManualUpdate> _pendingManualUpdates = new();
    private readonly List<VFXStagedOutputBatch> _stagedOutputBatches = new();
    private readonly Dictionary<int, object?> _propertyOverrides = new();
    private readonly Dictionary<int, VFXRuntimeSpawnerInstance> _spawnerInstances = new();
    private readonly HashSet<NativeGraphicsDevice> _spawnerRegisteredDevices = new();
    private readonly Dictionary<NativeGraphicsDevice, uint> _planarRegisteredVersions = new();
    private VisualEffectAsset? _visualEffectAsset;
    private VFXEventAttribute? _cachedEventAttribute;
    private VFXBoundInputEventDispatchPlan? _lastInputEventDispatch;
    private bool _pause;
    private float _playRate = 1f;
    private uint _startSeed;
    private bool _resetSeedOnPlay = true;
    private int _initialEventID = VisualEffectAsset.PlayEventID;
    private float _time;
    private float _vfxDeltaTime;
    private float _vfxUpdateAccumulator;
    private uint _vfxFrameIndex;
    private long _eventSequence;
    private VFXManagedFrameSnapshot _preparedFrameSnapshot;
    private bool _hasPreparedFrameSnapshot;
    private bool _preparedManualFrame;
    private ulong _pendingUpdateTicket;
    private int[] _pendingUpdateParticleSystemIds = Array.Empty<int>();
    private readonly Queue<VFXCommittedUpdateTicket> _committedUpdateTickets = new();
    private readonly Queue<VFXCommittedInitializeTicket> _committedInitializeTickets = new();

    public Action<VFXOutputEventArgs>? outputEventReceived;

    public bool pause
    {
        get => _pause;
        set => _pause = value;
    }

    public float playRate
    {
        get => _playRate;
        set => _playRate = value;
    }

    public uint startSeed
    {
        get => _startSeed;
        set => _startSeed = value;
    }

    public bool resetSeedOnPlay
    {
        get => _resetSeedOnPlay;
        set => _resetSeedOnPlay = value;
    }

    public int initialEventID
    {
        get => _initialEventID;
        set => _initialEventID = value;
    }

    public string initialEventName
    {
        get => Shader.GetPropertyName(_initialEventID);
        set => _initialEventID = Shader.PropertyToID(value);
    }

    public bool culled { get; internal set; }

    public VisualEffectAsset? visualEffectAsset
    {
        get => _visualEffectAsset;
        set
        {
            if (ReferenceEquals(_visualEffectAsset, value)) return;
            NativeGraphicsDevice.ClearVFXEffectStateFromAll(
                unchecked((ulong)(uint)GetInstanceID()));
            ClearPendingUpdateTicket();
            _committedInitializeTickets.Clear();
            _cachedEventAttribute?.Dispose();
            _cachedEventAttribute = null;
            _visualEffectAsset = value;
            _propertyOverrides.Clear();
            _lastInputEventDispatch = null;
            DisposeSpawnerInstances();
            lock (_eventLock)
            {
                while (_pendingEvents.Count > 0)
                    _pendingEvents.Dequeue().EventAttribute?.Dispose();
            }
            ClearPendingManualUpdates();
        }
    }

    public int aliveParticleCount { get; internal set; }
    internal float currentTime => _time;
    internal float currentVfxDeltaTime => _vfxDeltaTime;
    internal uint currentVfxFrameIndex => _vfxFrameIndex;
    internal AnityNative.GraphicsVFXPlanarDrawInfo lastPlanarDrawInfo { get; private set; }
    internal AnityNative.GraphicsVFXPlanarCameraDrawInfo lastPlanarCameraDrawInfo { get; private set; }
    internal bool hasPlanarOutputs => _visualEffectAsset?.GetPlanarOutputs().Count > 0;

    internal bool TryGetWorldCullingBounds(out Bounds bounds)
        => TryResolveWorldCullingBounds(null, out bounds);

    internal bool TryGetWorldCullingBounds(
        NativeGraphicsDevice device,
        out Bounds bounds)
        => TryResolveWorldCullingBounds(device, out bounds);

    private bool TryResolveWorldCullingBounds(
        NativeGraphicsDevice? device,
        out Bounds bounds)
    {
        bounds = default;
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null) return false;
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        var particleSystems = new List<string>();
        asset.GetParticleSystemNames(particleSystems);
        if (particleSystems.Count == 0) return false;

        bool initialized = false;
        foreach (string systemName in particleSystems)
        {
            int particleSystemId = Shader.PropertyToID(systemName);
            if (!asset.TryGetParticleCullingBounds(
                    particleSystemId, out VFXParticleCullingBounds metadata))
                return false;
            Bounds systemBounds;
            if (metadata.HasStaticBounds)
            {
                systemBounds = metadata.Bounds;
            }
            else if (metadata.HasAutomaticBounds && device is not null)
            {
                if (!device.TryReduceVFXParticleBounds(
                        effectId, particleSystemId, metadata,
                        out systemBounds, out _, out _))
                    return false;
            }
            else
            {
                return false;
            }
            bool worldSpace = metadata.WorldSpace;
            Bounds worldBounds = worldSpace ? systemBounds : TransformBounds(systemBounds);
            if (!HasFiniteNonNegativeBounds(worldBounds)) return false;
            if (!initialized)
            {
                bounds = worldBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(worldBounds);
            }
        }
        return initialized;
    }

    private Bounds TransformBounds(Bounds localBounds)
    {
        Transform? owner = transform;
        if (owner is null) return localBounds;
        Matrix4x4 matrix = owner.localToWorldMatrix;
        Vector3 localExtents = localBounds.extents;
        Vector3 worldExtents = new(
            MathF.Abs(matrix.m00) * localExtents.x +
            MathF.Abs(matrix.m01) * localExtents.y +
            MathF.Abs(matrix.m02) * localExtents.z,
            MathF.Abs(matrix.m10) * localExtents.x +
            MathF.Abs(matrix.m11) * localExtents.y +
            MathF.Abs(matrix.m12) * localExtents.z,
            MathF.Abs(matrix.m20) * localExtents.x +
            MathF.Abs(matrix.m21) * localExtents.y +
            MathF.Abs(matrix.m22) * localExtents.z);
        return new Bounds(owner.TransformPoint(localBounds.center), worldExtents * 2f);
    }

    private static bool HasFiniteNonNegativeBounds(Bounds bounds)
        => float.IsFinite(bounds.center.x) && float.IsFinite(bounds.center.y) &&
           float.IsFinite(bounds.center.z) && float.IsFinite(bounds.size.x) &&
           float.IsFinite(bounds.size.y) && float.IsFinite(bounds.size.z) &&
           bounds.size.x >= 0f && bounds.size.y >= 0f && bounds.size.z >= 0f;

    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<bool>", HasExplicitThis = true)]
    public bool HasBool(int nameID) => HasValue(nameID, typeof(bool));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<int>", HasExplicitThis = true)]
    public bool HasInt(int nameID) => HasValue(nameID, typeof(int));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<UInt32>", HasExplicitThis = true)]
    public bool HasUInt(int nameID) => HasValue(nameID, typeof(uint));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<float>", HasExplicitThis = true)]
    public bool HasFloat(int nameID) => HasValue(nameID, typeof(float));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Vector2f>", HasExplicitThis = true)]
    public bool HasVector2(int nameID) => HasValue(nameID, typeof(Vector2));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Vector3f>", HasExplicitThis = true)]
    public bool HasVector3(int nameID) => HasValue(nameID, typeof(Vector3));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Vector4f>", HasExplicitThis = true)]
    public bool HasVector4(int nameID) => HasValue(nameID, typeof(Vector4));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Matrix4x4f>", HasExplicitThis = true)]
    public bool HasMatrix4x4(int nameID) => HasValue(nameID, typeof(Matrix4x4));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Texture*>", HasExplicitThis = true)]
    public bool HasTexture(int nameID) => HasValue(nameID, typeof(Texture));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<AnimationCurve*>", HasExplicitThis = true)]
    public bool HasAnimationCurve(int nameID) => HasValue(nameID, typeof(AnimationCurve));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Gradient*>", HasExplicitThis = true)]
    public bool HasGradient(int nameID) => HasValue(nameID, typeof(Gradient));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<Mesh*>", HasExplicitThis = true)]
    public bool HasMesh(int nameID) => HasValue(nameID, typeof(Mesh));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<SkinnedMeshRenderer*>", HasExplicitThis = true)]
    public bool HasSkinnedMeshRenderer(int nameID) => HasValue(nameID, typeof(SkinnedMeshRenderer));
    [Bindings.FreeFunction(Name = "VisualEffectBindings::HasValueFromScript<GraphicsBuffer*>", HasExplicitThis = true)]
    public bool HasGraphicsBuffer(int nameID) => HasValue(nameID, typeof(GraphicsBuffer));

    public bool HasBool(string name) => HasBool(PropertyId(name));
    public bool HasInt(string name) => HasInt(PropertyId(name));
    public bool HasUInt(string name) => HasUInt(PropertyId(name));
    public bool HasFloat(string name) => HasFloat(PropertyId(name));
    public bool HasVector2(string name) => HasVector2(PropertyId(name));
    public bool HasVector3(string name) => HasVector3(PropertyId(name));
    public bool HasVector4(string name) => HasVector4(PropertyId(name));
    public bool HasMatrix4x4(string name) => HasMatrix4x4(PropertyId(name));
    public bool HasTexture(string name) => HasTexture(PropertyId(name));
    public bool HasAnimationCurve(string name) => HasAnimationCurve(PropertyId(name));
    public bool HasGradient(string name) => HasGradient(PropertyId(name));
    public bool HasMesh(string name) => HasMesh(PropertyId(name));
    public bool HasSkinnedMeshRenderer(string name) => HasSkinnedMeshRenderer(PropertyId(name));
    public bool HasGraphicsBuffer(string name) => HasGraphicsBuffer(PropertyId(name));

    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<bool>", HasExplicitThis = true)]
    public void SetBool(int nameID, bool b) => SetValue(nameID, typeof(bool), b);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<int>", HasExplicitThis = true)]
    public void SetInt(int nameID, int i) => SetValue(nameID, typeof(int), i);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<UInt32>", HasExplicitThis = true)]
    public void SetUInt(int nameID, uint i) => SetValue(nameID, typeof(uint), i);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<float>", HasExplicitThis = true)]
    public void SetFloat(int nameID, float f) => SetValue(nameID, typeof(float), f);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Vector2f>", HasExplicitThis = true)]
    public void SetVector2(int nameID, Vector2 v) => SetValue(nameID, typeof(Vector2), v);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Vector3f>", HasExplicitThis = true)]
    public void SetVector3(int nameID, Vector3 v) => SetValue(nameID, typeof(Vector3), v);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Vector4f>", HasExplicitThis = true)]
    public void SetVector4(int nameID, Vector4 v) => SetValue(nameID, typeof(Vector4), v);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Matrix4x4f>", HasExplicitThis = true)]
    public void SetMatrix4x4(int nameID, Matrix4x4 v) => SetValue(nameID, typeof(Matrix4x4), v);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Texture*>", HasExplicitThis = true)]
    public void SetTexture(int nameID, [Bindings.NotNull("ArgumentNullException")] Texture t)
    {
        if (t is null) throw new ArgumentNullException(nameof(t));
        SetValue(nameID, typeof(Texture), t);
    }
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<AnimationCurve*>", HasExplicitThis = true)]
    public void SetAnimationCurve(int nameID, [Bindings.NotNull("ArgumentNullException")] AnimationCurve c)
    {
        if (c is null) throw new ArgumentNullException(nameof(c));
        SetValue(nameID, typeof(AnimationCurve), c);
    }
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Gradient*>", HasExplicitThis = true)]
    public void SetGradient(int nameID, [Bindings.NotNull("ArgumentNullException")] Gradient g)
    {
        if (g is null) throw new ArgumentNullException(nameof(g));
        SetValue(nameID, typeof(Gradient), g);
    }
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<Mesh*>", HasExplicitThis = true)]
    public void SetMesh(int nameID, [Bindings.NotNull("ArgumentNullException")] Mesh m)
    {
        if (m is null) throw new ArgumentNullException(nameof(m));
        SetValue(nameID, typeof(Mesh), m);
    }
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<SkinnedMeshRenderer*>", HasExplicitThis = true)]
    public void SetSkinnedMeshRenderer(int nameID, SkinnedMeshRenderer m)
        => SetValue(nameID, typeof(SkinnedMeshRenderer), m);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::SetValueFromScript<GraphicsBuffer*>", HasExplicitThis = true)]
    public void SetGraphicsBuffer(int nameID, GraphicsBuffer g)
        => SetValue(nameID, typeof(GraphicsBuffer), g);

    public void SetBool(string name, bool b) => SetBool(PropertyId(name), b);
    public void SetInt(string name, int i) => SetInt(PropertyId(name), i);
    public void SetUInt(string name, uint i) => SetUInt(PropertyId(name), i);
    public void SetFloat(string name, float f) => SetFloat(PropertyId(name), f);
    public void SetVector2(string name, Vector2 v) => SetVector2(PropertyId(name), v);
    public void SetVector3(string name, Vector3 v) => SetVector3(PropertyId(name), v);
    public void SetVector4(string name, Vector4 v) => SetVector4(PropertyId(name), v);
    public void SetMatrix4x4(string name, Matrix4x4 v) => SetMatrix4x4(PropertyId(name), v);
    public void SetTexture(string name, Texture t) => SetTexture(PropertyId(name), t);
    public void SetAnimationCurve(string name, AnimationCurve c) => SetAnimationCurve(PropertyId(name), c);
    public void SetGradient(string name, Gradient g) => SetGradient(PropertyId(name), g);
    public void SetMesh(string name, Mesh m) => SetMesh(PropertyId(name), m);
    public void SetSkinnedMeshRenderer(string name, SkinnedMeshRenderer m) => SetSkinnedMeshRenderer(PropertyId(name), m);
    public void SetGraphicsBuffer(string name, GraphicsBuffer g) => SetGraphicsBuffer(PropertyId(name), g);

    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<bool>", HasExplicitThis = true)]
    public bool GetBool(int nameID) => GetValue(nameID, typeof(bool), false);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<int>", HasExplicitThis = true)]
    public int GetInt(int nameID) => GetValue(nameID, typeof(int), 0);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<UInt32>", HasExplicitThis = true)]
    public uint GetUInt(int nameID) => GetValue(nameID, typeof(uint), 0u);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<float>", HasExplicitThis = true)]
    public float GetFloat(int nameID) => GetValue(nameID, typeof(float), 0f);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<Vector2f>", HasExplicitThis = true)]
    public Vector2 GetVector2(int nameID) => GetValue(nameID, typeof(Vector2), Vector2.zero);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<Vector3f>", HasExplicitThis = true)]
    public Vector3 GetVector3(int nameID) => GetValue(nameID, typeof(Vector3), Vector3.zero);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<Vector4f>", HasExplicitThis = true)]
    public Vector4 GetVector4(int nameID) => GetValue(nameID, typeof(Vector4), Vector4.zero);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<Matrix4x4f>", HasExplicitThis = true)]
    public Matrix4x4 GetMatrix4x4(int nameID) => GetValue(nameID, typeof(Matrix4x4), Matrix4x4.zero);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<Texture*>", HasExplicitThis = true)]
    public Texture GetTexture(int nameID) => GetValue<Texture>(nameID, typeof(Texture), null!);
    public AnimationCurve GetAnimationCurve(int nameID) => GetValue<AnimationCurve>(nameID, typeof(AnimationCurve), null!);
    public Gradient GetGradient(int nameID) => GetValue<Gradient>(nameID, typeof(Gradient), null!);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<Mesh*>", HasExplicitThis = true)]
    public Mesh GetMesh(int nameID) => GetValue<Mesh>(nameID, typeof(Mesh), null!);
    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetValueFromScript<SkinnedMeshRenderer*>", HasExplicitThis = true)]
    public SkinnedMeshRenderer GetSkinnedMeshRenderer(int nameID)
        => GetValue<SkinnedMeshRenderer>(nameID, typeof(SkinnedMeshRenderer), null!);

    public bool GetBool(string name) => GetBool(PropertyId(name));
    public int GetInt(string name) => GetInt(PropertyId(name));
    public uint GetUInt(string name) => GetUInt(PropertyId(name));
    public float GetFloat(string name) => GetFloat(PropertyId(name));
    public Vector2 GetVector2(string name) => GetVector2(PropertyId(name));
    public Vector3 GetVector3(string name) => GetVector3(PropertyId(name));
    public Vector4 GetVector4(string name) => GetVector4(PropertyId(name));
    public Matrix4x4 GetMatrix4x4(string name) => GetMatrix4x4(PropertyId(name));
    public Texture GetTexture(string name) => GetTexture(PropertyId(name));
    public AnimationCurve GetAnimationCurve(string name) => GetAnimationCurve(PropertyId(name));
    public Gradient GetGradient(string name) => GetGradient(PropertyId(name));
    public Mesh GetMesh(string name) => GetMesh(PropertyId(name));
    public SkinnedMeshRenderer GetSkinnedMeshRenderer(string name) => GetSkinnedMeshRenderer(PropertyId(name));

    [Bindings.FreeFunction(Name = "VisualEffectBindings::ResetOverrideFromScript", HasExplicitThis = true)]
    public void ResetOverride(int nameID) => _propertyOverrides.Remove(nameID);
    public void ResetOverride(string name) => ResetOverride(PropertyId(name));

    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetTextureDimensionFromScript", HasExplicitThis = true)]
    public Rendering.TextureDimension GetTextureDimension(int nameID)
        => _visualEffectAsset?.GetTextureDimension(nameID) ?? Rendering.TextureDimension.Unknown;
    public Rendering.TextureDimension GetTextureDimension(string name) => GetTextureDimension(PropertyId(name));

    public bool HasSystem(int nameID) => _visualEffectAsset?.HasSystem(nameID) == true;
    public bool HasSystem(string name) => HasSystem(PropertyId(name));
    public bool HasAnySystemAwake()
    {
        EnsureSpawnerInstances();
        return _spawnerInstances.Values.Any(instance =>
                   instance.State.loopState != VFXSpawnerLoopState.Finished) ||
               _visualEffectAsset?.HasAnySystemAwake == true;
    }

    public void GetSystemNames(List<string> names)
        => FillNames(names, _visualEffectAsset is null ? null : list => _visualEffectAsset.GetSystemNames(list));
    public void GetParticleSystemNames(List<string> names)
        => FillNames(names, _visualEffectAsset is null ? null : list => _visualEffectAsset.GetParticleSystemNames(list));
    public void GetSpawnSystemNames(List<string> names)
        => FillNames(names, _visualEffectAsset is null ? null : list => _visualEffectAsset.GetSpawnSystemNames(list));
    public void GetOutputEventNames(List<string> names)
        => FillNames(names, _visualEffectAsset is null ? null : list => _visualEffectAsset.GetOutputEventNames(list));

    [Bindings.FreeFunction(Name = "VisualEffectBindings::GetParticleSystemInfo", HasExplicitThis = true, ThrowsException = true)]
    public VFXParticleSystemInfo GetParticleSystemInfo(int nameID)
    {
        if (_visualEffectAsset?.TryGetParticleSystemInfo(nameID, out VFXParticleSystemInfo info) == true) return info;
        throw new ArgumentException($"VFX particle system '{Shader.GetPropertyName(nameID)}' does not exist.", nameof(nameID));
    }
    public VFXParticleSystemInfo GetParticleSystemInfo(string name) => GetParticleSystemInfo(PropertyId(name));

    public VFXSpawnerState GetSpawnSystemInfo(int nameID)
    {
        EnsureSpawnerInstances();
        if (_spawnerInstances.TryGetValue(nameID, out VFXRuntimeSpawnerInstance? instance))
            return new VFXSpawnerState(instance.State);
        if (_visualEffectAsset?.TryGetSpawnSystemInfo(nameID, out VFXSpawnerState state) == true) return state;
        throw new ArgumentException($"VFX spawn system '{Shader.GetPropertyName(nameID)}' does not exist.", nameof(nameID));
    }
    public VFXSpawnerState GetSpawnSystemInfo(string name) => GetSpawnSystemInfo(PropertyId(name));
    public void GetSpawnSystemInfo(int nameID, VFXSpawnerState spawnState)
    {
        if (spawnState is null) throw new ArgumentNullException(nameof(spawnState));
        using VFXSpawnerState source = GetSpawnSystemInfo(nameID);
        spawnState.CopyFrom(source);
    }

    public VFXEventAttribute? CreateVFXEventAttribute()
        => _visualEffectAsset is null ? null : new VFXEventAttribute(_visualEffectAsset);

    public void SendEvent(int eventNameID, VFXEventAttribute? eventAttribute)
    {
        CheckValidVFXEventAttribute(eventAttribute);
        VFXEventAttribute? snapshot = eventAttribute is null ? null : new VFXEventAttribute(eventAttribute);
        byte[] payload;
        int strideWords;
        if (snapshot is not null)
        {
            payload = snapshot.PackValues(out strideWords);
        }
        else if (_visualEffectAsset is not null)
        {
            using var defaults = new VFXEventAttribute(_visualEffectAsset);
            payload = defaults.PackValues(out strideWords);
        }
        else
        {
            payload = EmptyPayload(out strideWords);
        }
        // CPU Event is still one event record when its public attribute schema is empty.
        // A private transport word keeps prefix-sum routing and native Initialize dispatch
        // unambiguous without exposing a synthetic VFXEventAttribute to scripts.
        if (strideWords == 0)
        {
            strideWords = 1;
            payload = new byte[sizeof(uint)];
        }
        ulong sequence;
        lock (_eventLock)
        {
            sequence = unchecked((ulong)++_eventSequence);
            _pendingEvents.Enqueue(new VFXPendingEvent(eventNameID, sequence, snapshot, payload, strideWords));
            // Preserve sequence order across concurrent SendEvent callers. Native input
            // dispatch plans require a strictly increasing per-effect stream.
            NativeGraphicsDevice.UploadVFXEventFromAll(
                unchecked((ulong)(uint)GetInstanceID()), eventNameID, sequence,
                payload, strideWords, 1);
        }
    }

    public void SendEvent(string eventName, VFXEventAttribute? eventAttribute)
        => SendEvent(Shader.PropertyToID(eventName), eventAttribute);

    public void SendEvent(int eventNameID) => SendEvent(eventNameID, null);
    public void SendEvent(string eventName) => SendEvent(Shader.PropertyToID(eventName), null);

    public void Play(VFXEventAttribute? eventAttribute) => SendEvent(VisualEffectAsset.PlayEventID, eventAttribute);
    public void Play() => SendEvent(VisualEffectAsset.PlayEventID);
    public void Stop(VFXEventAttribute? eventAttribute) => SendEvent(VisualEffectAsset.StopEventID, eventAttribute);
    public void Stop() => SendEvent(VisualEffectAsset.StopEventID);

    public void Reinit()
    {
        _time = 0f;
        _vfxDeltaTime = 0f;
        _vfxUpdateAccumulator = 0f;
        aliveParticleCount = 0;
        lock (_eventLock)
        {
            while (_pendingEvents.Count > 0)
                _pendingEvents.Dequeue().EventAttribute?.Dispose();
        }
        ClearPendingManualUpdates();
        NativeGraphicsDevice.ClearVFXEffectStateFromAll(
            unchecked((ulong)(uint)GetInstanceID()));
        ClearPendingUpdateTicket();
        _committedInitializeTickets.Clear();
        DisposeSpawnerInstances();
        EnsureSpawnerInstances();
        SendEvent(_initialEventID);
    }

    public void AdvanceOneFrame()
    {
        if (!_pause) return;
        lock (_manualUpdateLock)
            _pendingManualUpdates.Enqueue(new VFXManualUpdate(
                VFXManualUpdateKind.AdvanceOneFrame, 0f, 1));
    }

    public void Simulate(float stepDeltaTime, uint stepCount = 1)
    {
        if (stepCount == 0) return;
        lock (_manualUpdateLock)
            _pendingManualUpdates.Enqueue(new VFXManualUpdate(
                VFXManualUpdateKind.Simulate, stepDeltaTime, stepCount));
    }

    internal bool TryDequeueManualUpdate(out VFXManualUpdate update)
    {
        lock (_manualUpdateLock)
        {
            if (_pendingManualUpdates.Count == 0)
            {
                update = default;
                return false;
            }
            update = _pendingManualUpdates.Dequeue();
            return true;
        }
    }

    internal void ClearPendingManualUpdates()
    {
        lock (_manualUpdateLock) _pendingManualUpdates.Clear();
    }

    internal int pendingEventCount
    {
        get { lock (_eventLock) return _pendingEvents.Count; }
    }

    internal bool TryDequeueEvent(out VFXPendingEvent pendingEvent)
    {
        lock (_eventLock)
        {
            if (_pendingEvents.Count == 0)
            {
                pendingEvent = default;
                return false;
            }
            pendingEvent = _pendingEvents.Dequeue();
            return true;
        }
    }

    internal void InvokeOutputEvent(int eventNameId, VFXEventAttribute eventAttribute)
    {
        if (eventAttribute is null) throw new ArgumentNullException(nameof(eventAttribute));
        CheckValidVFXEventAttribute(eventAttribute);
        _cachedEventAttribute ??= new VFXEventAttribute(eventAttribute);
        _cachedEventAttribute.CopyValuesFrom(eventAttribute);
        outputEventReceived?.Invoke(new VFXOutputEventArgs(eventNameId, _cachedEventAttribute));
    }

    internal int ProcessInputEvents(
        NativeGraphicsDevice device,
        bool deferInitializeCompletion = false)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (!device.TryGetVFXEventDispatchPlan(effectId, out NativeVFXEventDispatchPlan? plan))
        {
            _lastInputEventDispatch = null;
            return 0;
        }
        if (plan is null)
            throw new InvalidOperationException("Native VFX input event dispatch plan is missing.");
        var boundBatches = new VFXBoundInputEventDispatchBatch[plan.Batches.Length];
        var initializeDispatches = new List<AnityNative.GraphicsVFXInitializeDispatchDesc>();
        var initializeKernels = new List<VFXRuntimeInitializeKernelData?>();
        for (int index = 0; index < plan.Batches.Length; index++)
        {
            AnityNative.GraphicsVFXEventDispatchBatch batch = plan.Batches[index];
            ApplySpawnerControl(batch.eventNameId, device);
            VFXRuntimeInputEventData? inputEvent = null;
            if (_visualEffectAsset?.TryGetInputEventRuntimeData(
                    batch.eventNameId, out VFXRuntimeInputEventData resolved) == true)
                inputEvent = resolved;
            boundBatches[index] = new VFXBoundInputEventDispatchBatch(batch, inputEvent);
            if (batch.recordCount <= 0 || inputEvent is null) continue;
            foreach (VFXRuntimeInputEventTargetData target in inputEvent.Targets)
            {
                if (_visualEffectAsset is not null && target.SpawnerContextIds.Any(contextId =>
                        _visualEffectAsset.TryGetSpawnerProgramByContext(contextId, out _)))
                    continue;
                long sourceSpawnerContextId = target.SpawnerContextIds.Count == 0
                    ? 0 : target.SpawnerContextIds[^1];
                int spawnSystemId = target.SpawnSystemNames.Count == 0
                    ? 0 : Shader.PropertyToID(target.SpawnSystemNames[^1]);
                initializeDispatches.Add(new AnityNative.GraphicsVFXInitializeDispatchDesc
                {
                    effectId = effectId,
                    sequence = batch.sequence,
                    initializeContextId = target.InitializeContextId,
                    sourceSpawnerContextId = sourceSpawnerContextId,
                    eventNameId = batch.eventNameId,
                    particleSystemId = Shader.PropertyToID(target.ParticleSystemName),
                    spawnSystemId = spawnSystemId,
                    startEventIndex = batch.startEventIndex,
                    recordCount = batch.recordCount,
                    strideBytes = batch.strideBytes
                });
                initializeKernels.Add(target.InitializeKernel);
            }
        }
        ulong initializeTicketId = 0;
        if (initializeDispatches.Count > 0)
        {
            CompletePendingInitializeTickets(device, effectId, waitForCompletion: true);
            if (!device.BeginVFXInitializeKernels(
                    initializeDispatches.ToArray(), initializeKernels.ToArray(),
                    plan.Records, _startSeed, out initializeTicketId))
                throw new InvalidOperationException(
                    "Native VFX Initialize dispatch transaction failed.");
        }
        if (!device.ConsumeVFXEventDispatchPlan(effectId, plan.Info.lastSequence))
        {
            if (initializeTicketId != 0 &&
                !device.CancelVFXInitializeKernels(initializeTicketId))
                throw new InvalidOperationException(
                    "Native VFX input event dispatch plan changed and its Initialize cancellation failed.");
            throw new InvalidOperationException("Native VFX input event dispatch plan changed while being consumed.");
        }
        if (initializeTicketId != 0)
            _committedInitializeTickets.Enqueue(new VFXCommittedInitializeTicket(
                device, initializeTicketId));
        lock (_eventLock)
        {
            while (_pendingEvents.Count > 0 &&
                   _pendingEvents.Peek().Sequence <= plan.Info.lastSequence)
                _pendingEvents.Dequeue().EventAttribute?.Dispose();
        }
        _lastInputEventDispatch = new VFXBoundInputEventDispatchPlan(plan, boundBatches);
        if (!deferInitializeCompletion)
            CompletePendingInitializeTickets(
                device, effectId, waitForCompletion: true);
        return plan.Batches.Length;
    }

    internal int AdvanceSpawnerSystems(
        float deltaTime,
        NativeGraphicsDevice device,
        bool advanceFrameIndex = true,
        bool allowUnsafeDeltaTime = false,
        bool deferInitializeCompletion = false)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        if (!allowUnsafeDeltaTime && (!float.IsFinite(deltaTime) || deltaTime < 0f))
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null) return 0;
        _vfxDeltaTime = deltaTime;
        if (advanceFrameIndex)
            _vfxFrameIndex = VFXManager.AdvanceManualFrameIndex(device);
        EnsureSpawnerProgramsRegistered(device);
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        bool hasCallbacks = _spawnerInstances.Values.Any(instance => instance.HasCallbacks);
        AnityNative.GraphicsVFXSpawnerState[] nativeStates;
        bool ticked;
        if (hasCallbacks)
        {
            var bridge = new SpawnerCallbackBridge(this);
            GCHandle handle = GCHandle.Alloc(bridge);
            try
            {
                ticked = device.TickVFXSpawners(
                    effectId, deltaTime, _spawnerInstances.Count,
                    NativeSpawnerCallback, GCHandle.ToIntPtr(handle), out nativeStates,
                    allowUnsafeDeltaTime);
                bridge.ThrowIfCaptured();
            }
            finally
            {
                handle.Free();
            }
        }
        else
        {
            ticked = device.TickVFXSpawners(
                effectId, deltaTime, _spawnerInstances.Count, out nativeStates,
                allowUnsafeDeltaTime);
        }
        if (!ticked ||
            nativeStates.Length != _spawnerInstances.Count)
            throw new InvalidOperationException("Native VFX Spawner scheduler tick failed.");
        int dispatched = 0;
        foreach (AnityNative.GraphicsVFXSpawnerState nativeState in nativeStates)
        {
            if (!asset.TryGetSpawnerProgramByContext(
                    nativeState.contextId, out VFXRuntimeSpawnerProgramData program) ||
                !_spawnerInstances.TryGetValue(
                    Shader.PropertyToID(program.SystemName), out VFXRuntimeSpawnerInstance? instance))
                throw new InvalidOperationException("Native VFX Spawner returned an unknown context.");
            instance.ApplyNativeState(effectId, nativeState, allowUnsafeDeltaTime);
            byte[]? currentEventRecord = null;
            if (program.EventStrideWords > 0)
            {
                if (!device.TryGetVFXSpawnerEventRecord(
                        effectId, program.ContextId, program.EventStrideWords,
                        out currentEventRecord))
                    throw new InvalidOperationException("Native VFX Spawner Event record readback failed.");
                instance.LoadEventRecord(currentEventRecord);
            }
            float eventSpawnCount = nativeState.eventSpawnCount;
            if (eventSpawnCount < 1f) continue;
            byte[] records;
            int strideWords;
            if (program.EventStrideWords > 0)
            {
                strideWords = program.EventStrideWords;
                records = currentEventRecord!;
            }
            else
            {
                using var source = new VFXEventAttribute(asset);
                source.SetFloat("spawnCount", eventSpawnCount);
                records = source.PackValues(out strideWords);
            }
            if (strideWords <= 0)
                throw new InvalidDataException("VFX Spawner output requires the spawnCount Event attribute.");
            ulong sequence = unchecked((ulong)Interlocked.Increment(ref _eventSequence));
            var dispatches = new AnityNative.GraphicsVFXInitializeDispatchDesc[program.Outputs.Count];
            var kernels = new VFXRuntimeInitializeKernelData?[program.Outputs.Count];
            for (int outputIndex = 0; outputIndex < program.Outputs.Count; outputIndex++)
            {
                VFXRuntimeSpawnerOutputData output = program.Outputs[outputIndex];
                int particleSystemId = Shader.PropertyToID(output.ParticleSystemName);
                dispatches[outputIndex] = new AnityNative.GraphicsVFXInitializeDispatchDesc
                {
                    effectId = effectId,
                    sequence = sequence,
                    initializeContextId = output.InitializeContextId,
                    sourceSpawnerContextId = program.ContextId,
                    eventNameId = 0,
                    particleSystemId = particleSystemId,
                    spawnSystemId = Shader.PropertyToID(program.SystemName),
                    startEventIndex = 0,
                    recordCount = 1,
                    strideBytes = checked(strideWords * sizeof(uint))
                };
                kernels[outputIndex] = output.InitializeKernel;
            }
            if (dispatches.Length > 0)
            {
                CompletePendingInitializeTickets(device, effectId, waitForCompletion: true);
                if (!device.BeginVFXInitializeKernels(
                        dispatches, kernels, records, _startSeed,
                        out ulong ticketId))
                    throw new InvalidOperationException(
                        "Native VFX Spawner Initialize dispatch transaction failed.");
                _committedInitializeTickets.Enqueue(new VFXCommittedInitializeTicket(
                    device, ticketId));
            }
            dispatched += dispatches.Length;
            foreach (KeyValuePair<int, VFXRuntimeOutputEventData> outputEvent in
                     asset.GetOutputEventsForSpawner(program.ContextId))
            {
                byte[] outputRecord = PackSpawnerOutputEventRecord(
                    asset, outputEvent.Value, records, strideWords);
                ulong outputSequence = unchecked((ulong)Interlocked.Increment(ref _eventSequence));
                if (!device.EnqueueVFXOutputEvent(
                        effectId, outputEvent.Key, outputSequence, outputRecord,
                        outputEvent.Value.StrideWords, 1))
                    throw new InvalidOperationException(
                        $"Native VFX Spawner output event '{outputEvent.Value.Name}' enqueue failed.");
            }
        }
        if (!deferInitializeCompletion)
            CompletePendingInitializeTickets(
                device, effectId, waitForCompletion: true);
        return dispatched;
    }

    internal int UpdateParticleSystems(float deltaTime, NativeGraphicsDevice device)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null) return 0;
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (_pendingUpdateTicket != 0)
            throw new InvalidOperationException(
                "A native VFX Update/Reap transaction is already pending.");
        RefreshAliveParticleCountAfterUpdate(
            device, effectId, waitForCompletion: false);
        VFXRuntimeUpdateKernelData[] ResolveActiveKernels()
            => asset.GetUpdateKernels()
                .Where(kernel => device.TryGetVFXParticleSystemInfo(
                    effectId, Shader.PropertyToID(kernel.ParticleSystemName), out _))
                .ToArray();
        bool hasPendingInitialize = _committedInitializeTickets.Any(
            pending => ReferenceEquals(pending.Device, device));
        VFXRuntimeUpdateKernelData[] activeKernels = ResolveActiveKernels();
        if (activeKernels.Length == 0 && hasPendingInitialize)
        {
            CompletePendingInitializeTickets(
                device, effectId, waitForCompletion: true);
            activeKernels = ResolveActiveKernels();
            hasPendingInitialize = false;
        }
        if (activeKernels.Length == 0) return 0;
        int[] particleSystemIds = activeKernels
            .Select(kernel => Shader.PropertyToID(kernel.ParticleSystemName))
            .Distinct()
            .ToArray();
        VFXParticleCullingBounds?[] automaticBounds = activeKernels
            .Select(kernel =>
            {
                int particleSystemId = Shader.PropertyToID(kernel.ParticleSystemName);
                return asset.TryGetParticleCullingBounds(
                           particleSystemId, out VFXParticleCullingBounds metadata) &&
                       metadata.HasAutomaticBounds && !metadata.HasStaticBounds &&
                       metadata.AliveOffsetWords >= 0
                    ? metadata
                    : (VFXParticleCullingBounds?)null;
            })
            .ToArray();
        bool submitted = device.BeginVFXUpdateKernels(
            effectId, activeKernels, automaticBounds,
            deltaTime, _startSeed, out ulong ticketId);
        if (!submitted && hasPendingInitialize)
        {
            CompletePendingInitializeTickets(
                device, effectId, waitForCompletion: true);
            activeKernels = ResolveActiveKernels();
            particleSystemIds = activeKernels
                .Select(kernel => Shader.PropertyToID(kernel.ParticleSystemName))
                .Distinct()
                .ToArray();
            automaticBounds = activeKernels
                .Select(kernel =>
                {
                    int particleSystemId = Shader.PropertyToID(kernel.ParticleSystemName);
                    return asset.TryGetParticleCullingBounds(
                               particleSystemId, out VFXParticleCullingBounds metadata) &&
                           metadata.HasAutomaticBounds && !metadata.HasStaticBounds &&
                           metadata.AliveOffsetWords >= 0
                        ? metadata
                        : (VFXParticleCullingBounds?)null;
                })
                .ToArray();
            submitted = activeKernels.Length > 0 && device.BeginVFXUpdateKernels(
                effectId, activeKernels, automaticBounds,
                deltaTime, _startSeed, out ticketId);
        }
        if (!submitted)
            throw new InvalidOperationException("Native VFX Update/Reap submission failed.");
        _pendingUpdateTicket = ticketId;
        _pendingUpdateParticleSystemIds = particleSystemIds;
        return activeKernels.Length;
    }

    internal float PrepareVfxFrame(float gameDeltaTime, uint frameIndex)
    {
        if (!float.IsFinite(gameDeltaTime) || gameDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(gameDeltaTime));
        _vfxFrameIndex = frameIndex;
        if (_pause)
        {
            _vfxDeltaTime = 0f;
            return 0f;
        }

        _vfxUpdateAccumulator += gameDeltaTime;
        float fixedStep = VFXManager.fixedTimeStep;
        int availableSteps = (int)MathF.Floor(_vfxUpdateAccumulator / fixedStep);
        int maximumSteps = Math.Max(1, (int)MathF.Round(
            VFXManager.maxDeltaTime / fixedStep));
        int consumedSteps = Math.Min(availableSteps, maximumSteps);
        if (consumedSteps <= 0)
        {
            _vfxDeltaTime = 0f;
            return 0f;
        }

        float unscaledVfxDelta = consumedSteps * fixedStep;
        _vfxUpdateAccumulator = MathF.Max(0f, _vfxUpdateAccumulator - unscaledVfxDelta);
        _vfxDeltaTime = unscaledVfxDelta * _playRate;
        return _vfxDeltaTime;
    }

    internal float PrepareVfxFrame(
        float gameDeltaTime,
        uint frameIndex,
        NativeGraphicsDevice device,
        bool? pausedOverride = null)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        if (!float.IsFinite(gameDeltaTime) || gameDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(gameDeltaTime));
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        CapturePreparedFrameSnapshot(manual: false);
        if (!device.PrepareVFXEffectFrame(
                effectId, frameIndex, gameDeltaTime, _playRate,
                VFXManager.fixedTimeStep, VFXManager.maxDeltaTime,
                pausedOverride ?? _pause,
                out AnityNative.GraphicsVFXFrameState state))
        {
            _hasPreparedFrameSnapshot = false;
            throw new InvalidOperationException("Native VFX effect frame preparation failed.");
        }
        ApplyNativeVfxFrameState(effectId, frameIndex, state, prepared: true);
        return _vfxDeltaTime;
    }

    internal float PrepareManualVfxFrame(
        float stepDeltaTime,
        uint frameIndex,
        NativeGraphicsDevice device)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        CapturePreparedFrameSnapshot(manual: true);
        if (!device.PrepareVFXEffectManualFrame(
                effectId, frameIndex, stepDeltaTime,
                out AnityNative.GraphicsVFXFrameState state))
        {
            _hasPreparedFrameSnapshot = false;
            throw new InvalidOperationException(
                "Native VFX manual effect frame preparation failed.");
        }
        ApplyNativeManualVfxFrameState(effectId, frameIndex, state, prepared: true);
        return _vfxDeltaTime;
    }

    internal void CompleteVfxFrame()
    {
        if (!_pause) _time += _vfxDeltaTime;
    }

    internal void CompleteVfxFrame(NativeGraphicsDevice device)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (!device.CommitVFXEffectFrame(
                effectId, _vfxFrameIndex,
                out AnityNative.GraphicsVFXFrameState state))
            throw new InvalidOperationException("Native VFX effect frame commit failed.");
        if (_pendingUpdateTicket != 0)
        {
            _committedUpdateTickets.Enqueue(new VFXCommittedUpdateTicket(
                _pendingUpdateTicket, _pendingUpdateParticleSystemIds));
            ClearCurrentPendingUpdateTicket();
        }
        if (_preparedManualFrame)
            ApplyNativeManualVfxFrameState(
                effectId, _vfxFrameIndex, state, prepared: false);
        else
            ApplyNativeVfxFrameState(effectId, _vfxFrameIndex, state, prepared: false);
        RefreshAliveParticleCountAfterUpdate(
            device, effectId, waitForCompletion: false);
        _hasPreparedFrameSnapshot = false;
        _preparedManualFrame = false;
    }

    internal void AbortVfxFrame(NativeGraphicsDevice device)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        if (!_hasPreparedFrameSnapshot)
            throw new InvalidOperationException("No native VFX frame is prepared.");
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (!device.AbortVFXEffectFrame(effectId, _vfxFrameIndex))
            throw new InvalidOperationException("Native VFX effect frame abort failed.");
        CancelPendingInitializeTickets(device);
        ClearCurrentPendingUpdateTicket();
        RestoreSpawnerInstancesFromNative(device, effectId);
        _time = _preparedFrameSnapshot.Time;
        _vfxDeltaTime = _preparedFrameSnapshot.DeltaTime;
        _vfxUpdateAccumulator = _preparedFrameSnapshot.Accumulator;
        _vfxFrameIndex = _preparedFrameSnapshot.FrameIndex;
        aliveParticleCount = _preparedFrameSnapshot.AliveParticleCount;
        _lastInputEventDispatch = _preparedFrameSnapshot.LastInputEventDispatch;
        _stagedOutputBatches.Clear();
        _hasPreparedFrameSnapshot = false;
        _preparedManualFrame = false;
    }

    private void RestoreSpawnerInstancesFromNative(
        NativeGraphicsDevice device,
        ulong effectId)
    {
        foreach (VFXRuntimeSpawnerInstance instance in _spawnerInstances.Values)
        {
            if (!device.TryGetVFXSpawnerState(
                    effectId, instance.Program.ContextId,
                    out AnityNative.GraphicsVFXSpawnerState state))
                throw new InvalidOperationException(
                    "Native VFX Spawner rollback state is unavailable.");
            instance.ApplyNativeState(effectId, state, allowUnsafeTime: true);
        }
    }

    private void RefreshAliveParticleCountAfterUpdate(
        NativeGraphicsDevice device,
        ulong effectId,
        bool waitForCompletion)
    {
        int[] particleSystemIds = Array.Empty<int>();
        bool retiredAny = false;
        while (_committedUpdateTickets.Count > 0)
        {
            VFXCommittedUpdateTicket pending = _committedUpdateTickets.Peek();
            bool ticketExists = device.TryGetVFXUpdateTicketInfo(
                pending.TicketId, out AnityNative.GraphicsVFXUpdateTicketInfo ticket);
            if (ticketExists && !waitForCompletion && ticket.state == 0)
            {
                particleSystemIds = pending.ParticleSystemIds;
                break;
            }
            if (ticketExists && (ticket.state == 2 ||
                !device.CompleteVFXUpdateKernels(pending.TicketId)))
                throw new InvalidOperationException(
                    "Native VFX Update/Reap completion failed.");
            _committedUpdateTickets.Dequeue();
            particleSystemIds = pending.ParticleSystemIds;
            retiredAny = true;
        }
        if (particleSystemIds.Length == 0 && !retiredAny) return;
        int totalAlive = 0;
        foreach (int particleSystemId in particleSystemIds)
        {
            if (!device.TryGetVFXParticleSystemInfo(
                    effectId, particleSystemId,
                    out AnityNative.GraphicsVFXParticleSystemInfo info))
                throw new InvalidOperationException(
                    "Native VFX particle system state is missing after Update/Reap commit.");
            totalAlive = checked(totalAlive + info.aliveCount);
        }
        aliveParticleCount = totalAlive;
    }

    private void CompletePendingInitializeTickets(
        NativeGraphicsDevice device,
        ulong effectId,
        bool waitForCompletion)
    {
        bool completedAny = false;
        bool deviceBlocked = false;
        int count = _committedInitializeTickets.Count;
        for (int index = 0; index < count; index++)
        {
            VFXCommittedInitializeTicket pending =
                _committedInitializeTickets.Dequeue();
            if (!ReferenceEquals(pending.Device, device) || deviceBlocked)
            {
                _committedInitializeTickets.Enqueue(pending);
                continue;
            }
            bool exists = device.TryGetVFXInitializeTicketInfo(
                pending.TicketId, out AnityNative.GraphicsVFXInitializeTicketInfo info);
            if (exists && !waitForCompletion && info.state == 0)
            {
                _committedInitializeTickets.Enqueue(pending);
                deviceBlocked = true;
                continue;
            }
            if (exists && (info.state == 2 ||
                !device.CompleteVFXInitializeKernels(pending.TicketId)))
                throw new InvalidOperationException(
                    "Native VFX Initialize completion failed.");
            completedAny |= exists;
        }
        if (!completedAny) return;
        int totalAlive = 0;
        var particleSystemNames = new List<string>();
        _visualEffectAsset?.GetParticleSystemNames(particleSystemNames);
        foreach (string particleSystemName in particleSystemNames)
        {
            if (device.TryGetVFXParticleSystemInfo(
                    effectId, Shader.PropertyToID(particleSystemName),
                    out AnityNative.GraphicsVFXParticleSystemInfo info))
                totalAlive = checked(totalAlive + info.aliveCount);
        }
        aliveParticleCount = totalAlive;
    }

    private void CancelPendingInitializeTickets(NativeGraphicsDevice device)
    {
        int count = _committedInitializeTickets.Count;
        for (int index = 0; index < count; index++)
        {
            VFXCommittedInitializeTicket pending =
                _committedInitializeTickets.Dequeue();
            if (ReferenceEquals(pending.Device, device))
            {
                if (device.TryGetVFXInitializeTicketInfo(pending.TicketId, out _) &&
                    !device.CancelVFXInitializeKernels(pending.TicketId))
                    throw new InvalidOperationException(
                        "Native VFX Initialize cancellation failed.");
            }
            else
            {
                _committedInitializeTickets.Enqueue(pending);
            }
        }
    }

    private void ClearPendingUpdateTicket()
    {
        ClearCurrentPendingUpdateTicket();
        _committedUpdateTickets.Clear();
    }

    private void ClearCurrentPendingUpdateTicket()
    {
        _pendingUpdateTicket = 0;
        _pendingUpdateParticleSystemIds = Array.Empty<int>();
    }

    private void CapturePreparedFrameSnapshot(bool manual)
    {
        if (_hasPreparedFrameSnapshot)
            throw new InvalidOperationException("A native VFX frame is already prepared.");
        if (_stagedOutputBatches.Count != 0)
            throw new InvalidOperationException("A committed VFX output batch is still pending delivery.");
        _preparedFrameSnapshot = new VFXManagedFrameSnapshot(
            _time, _vfxDeltaTime, _vfxUpdateAccumulator, _vfxFrameIndex,
            aliveParticleCount, _lastInputEventDispatch);
        _hasPreparedFrameSnapshot = true;
        _preparedManualFrame = manual;
    }

    private void ApplyNativeVfxFrameState(
        ulong effectId,
        uint frameIndex,
        AnityNative.GraphicsVFXFrameState state,
        bool prepared)
    {
        if (state.effectId != effectId || state.frameIndex != frameIndex ||
            state.prepared != (prepared ? 1u : 0u) || state.generation == 0 ||
            !float.IsFinite(state.gameDeltaTime) || state.gameDeltaTime < 0f ||
            !float.IsFinite(state.unscaledDeltaTime) || state.unscaledDeltaTime < 0f ||
            !float.IsFinite(state.deltaTime) || state.deltaTime < 0f ||
            !float.IsFinite(state.accumulator) || state.accumulator < 0f)
            throw new InvalidOperationException("Native VFX effect frame state is invalid.");
        float expectedDelta = state.unscaledDeltaTime * _playRate;
        float tolerance = MathF.Max(1e-6f, MathF.Abs(expectedDelta) * 1e-5f);
        if (MathF.Abs(state.deltaTime - expectedDelta) > tolerance)
            throw new InvalidOperationException("Native VFX effect frame scaling is invalid.");
        _vfxFrameIndex = state.frameIndex;
        _vfxDeltaTime = state.deltaTime;
        _vfxUpdateAccumulator = state.accumulator;
        _time = state.totalTime;
    }

    private void ApplyNativeManualVfxFrameState(
        ulong effectId,
        uint frameIndex,
        AnityNative.GraphicsVFXFrameState state,
        bool prepared)
    {
        if (state.effectId != effectId || state.frameIndex != frameIndex ||
            state.stepCount != 1 ||
            state.prepared != (prepared ? 1u : 0u) || state.generation == 0 ||
            !float.IsFinite(state.accumulator) || state.accumulator < 0f)
            throw new InvalidOperationException("Native VFX manual frame state is invalid.");
        _vfxFrameIndex = state.frameIndex;
        _vfxDeltaTime = state.deltaTime;
        _vfxUpdateAccumulator = state.accumulator;
        _time = state.totalTime;
    }

    private static byte[] PackSpawnerOutputEventRecord(
        VisualEffectAsset asset,
        VFXRuntimeOutputEventData outputEvent,
        byte[] sourceRecord,
        int sourceStrideWords)
    {
        if (sourceRecord.Length != checked(sourceStrideWords * sizeof(uint)))
            throw new InvalidDataException("Native VFX Spawner Event record has an invalid byte length.");
        var result = new byte[checked(outputEvent.StrideWords * sizeof(uint))];
        foreach (VFXRuntimeAttributeData field in outputEvent.Attributes)
        {
            if (!asset.TryGetEventAttribute(
                    Shader.PropertyToID(field.Name), out VFXEventAttributeSchemaEntry source) ||
                source.ValueType.SystemType != VFXRuntimeAssetData.SystemType(field.ValueType) ||
                source.OffsetWords < 0 || source.OffsetWords + field.SizeWords > sourceStrideWords)
                throw new InvalidDataException(
                    $"VFX output event '{outputEvent.Name}' attribute '{field.Name}' does not match the Spawner Event record.");
            sourceRecord.AsSpan(source.OffsetWords * sizeof(uint), field.SizeWords * sizeof(uint)).CopyTo(
                result.AsSpan(field.OffsetWords * sizeof(uint), field.SizeWords * sizeof(uint)));
        }
        return result;
    }

    internal VFXBoundInputEventDispatchPlan? lastInputEventDispatch => _lastInputEventDispatch;

    internal void ReleaseNativeState()
    {
        NativeGraphicsDevice.ClearVFXEffectStateFromAll(
            unchecked((ulong)(uint)GetInstanceID()));
        ClearPendingUpdateTicket();
        _committedInitializeTickets.Clear();
        _cachedEventAttribute?.Dispose();
        _cachedEventAttribute = null;
        _lastInputEventDispatch = null;
        DisposeSpawnerInstances();
        lock (_eventLock)
        {
            while (_pendingEvents.Count > 0)
                _pendingEvents.Dequeue().EventAttribute?.Dispose();
        }
    }

    internal int ProcessOutputEvents(NativeGraphicsDevice device)
        => DrainNativeOutputEvents(device, stageForCommit: false);

    internal int StageOutputEventsForCommit(NativeGraphicsDevice device)
    {
        if (_stagedOutputBatches.Count != 0)
            throw new InvalidOperationException("VFX output delivery is already staged.");
        return DrainNativeOutputEvents(device, stageForCommit: true);
    }

    internal int DeliverCommittedOutputEvents()
    {
        VFXStagedOutputBatch[] batches = _stagedOutputBatches.ToArray();
        _stagedOutputBatches.Clear();
        int delivered = 0;
        for (int batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            VFXStagedOutputBatch batch = batches[batchIndex];
            try
            {
                using var eventAttribute = new VFXEventAttribute(batch.Asset);
                for (int recordIndex = 0; recordIndex < batch.RecordCount; recordIndex++)
                {
                    eventAttribute.LoadRuntimeRecord(
                        batch.OutputEvent,
                        batch.Records.AsSpan(
                            recordIndex * batch.StrideBytes, batch.StrideBytes));
                    InvokeOutputEvent(batch.EventNameId, eventAttribute);
                    delivered++;
                }
            }
            catch
            {
                for (int pendingIndex = batchIndex + 1;
                     pendingIndex < batches.Length; pendingIndex++)
                    _stagedOutputBatches.Add(batches[pendingIndex]);
                throw;
            }
        }
        return delivered;
    }

    private int DrainNativeOutputEvents(
        NativeGraphicsDevice device,
        bool stageForCommit)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        VisualEffectAsset? asset = _visualEffectAsset;
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (asset is null)
        {
            while (device.TryPeekVFXOutputEvent(effectId, out AnityNative.GraphicsVFXEventUploadInfo stale))
                if (!device.TryDequeueVFXOutputEvent(effectId, stale.desc.sequence, stale.byteCount, out _)) break;
            return 0;
        }
        int eventCount = 0;
        while (device.TryPeekVFXOutputEvent(effectId, out AnityNative.GraphicsVFXEventUploadInfo info))
        {
            if (!device.TryDequeueVFXOutputEvent(effectId, info.desc.sequence, info.byteCount, out byte[] records))
                throw new InvalidOperationException("Native VFX output event queue changed while being consumed.");
            if (!asset.TryGetOutputEventRuntimeData(info.desc.eventNameId, out VFXRuntimeOutputEventData outputEvent))
                continue;
            int expectedStrideBytes = checked(outputEvent.StrideWords * sizeof(uint));
            int expectedByteCount = checked(expectedStrideBytes * info.desc.recordCount);
            if (info.desc.strideBytes != expectedStrideBytes || info.byteCount != expectedByteCount)
                throw new InvalidDataException(
                    $"Native VFX output event '{outputEvent.Name}' does not match its compiled record layout.");
            if (stageForCommit)
            {
                _stagedOutputBatches.Add(new VFXStagedOutputBatch(
                    asset, info.desc.eventNameId, outputEvent, records,
                    expectedStrideBytes, info.desc.recordCount));
                eventCount = checked(eventCount + info.desc.recordCount);
                continue;
            }
            using var eventAttribute = new VFXEventAttribute(asset);
            for (int recordIndex = 0; recordIndex < info.desc.recordCount; recordIndex++)
            {
                eventAttribute.LoadRuntimeRecord(
                    outputEvent,
                    records.AsSpan(recordIndex * expectedStrideBytes, expectedStrideBytes));
                InvokeOutputEvent(info.desc.eventNameId, eventAttribute);
                eventCount++;
            }
        }
        return eventCount;
    }

    private void CheckValidVFXEventAttribute(VFXEventAttribute? eventAttribute)
    {
        if (eventAttribute is not null && !ReferenceEquals(eventAttribute.vfxAsset, _visualEffectAsset))
            throw new InvalidOperationException(
                "Invalid VFXEventAttribute provided to VisualEffect. It has been created with another VisualEffectAsset. Use CreateVFXEventAttribute.");
    }

    private void ApplySpawnerControl(int eventNameId, NativeGraphicsDevice device)
    {
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null) return;
        EnsureSpawnerProgramsRegistered(device);
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        uint? generatedPlaySeed = null;
        foreach (VFXRuntimeSpawnerProgramData program in asset.GetSpawnerProgramsForControl(eventNameId))
        {
            int systemId = Shader.PropertyToID(program.SystemName);
            if (!_spawnerInstances.TryGetValue(systemId, out VFXRuntimeSpawnerInstance? instance)) continue;
            foreach (VFXRuntimeSpawnerControlData control in program.Controls.Where(control =>
                         Shader.PropertyToID(control.EventName) == eventNameId))
            {
                bool play = control.InputSlotIndex == 0;
                bool generateSeed = play && _resetSeedOnPlay;
                uint seed = generateSeed
                    ? generatedPlaySeed ??= GeneratePlaySeed()
                    : _startSeed;
                AnityNative.GraphicsVFXSpawnerState nativeState;
                bool controlled;
                uint previousSystemSeed = instance.SystemSeed;
                if (play) instance.SystemSeed = seed;
                try
                {
                    if (instance.HasCallbacks)
                    {
                        var bridge = new SpawnerCallbackBridge(this);
                        GCHandle handle = GCHandle.Alloc(bridge);
                        try
                        {
                            controlled = device.ControlVFXSpawner(
                                effectId, program.ContextId, play, seed, generateSeed,
                                NativeSpawnerCallback, GCHandle.ToIntPtr(handle), out nativeState);
                            bridge.ThrowIfCaptured();
                        }
                        finally
                        {
                            handle.Free();
                        }
                    }
                    else
                    {
                        controlled = device.ControlVFXSpawner(
                            effectId, program.ContextId, play, seed, generateSeed, out nativeState);
                    }
                }
                catch
                {
                    instance.SystemSeed = previousSystemSeed;
                    throw;
                }
                if (!controlled)
                {
                    instance.SystemSeed = previousSystemSeed;
                    throw new InvalidOperationException("Native VFX Spawner control failed.");
                }
                instance.ApplyNativeState(effectId, nativeState);
            }
        }
    }

    private static uint GeneratePlaySeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    private void EnsureSpawnerProgramsRegistered(NativeGraphicsDevice device)
    {
        EnsureSpawnerInstances();
        if (_spawnerRegisteredDevices.Contains(device)) return;
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null) return;
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (!device.SetVFXSpawnerPrograms(effectId, asset.SpawnerPrograms))
            throw new InvalidOperationException("Native VFX Spawner program installation failed.");
        using (var defaults = new VFXEventAttribute(asset))
        {
            byte[] record = defaults.PackValues(out int strideWords);
            foreach (VFXRuntimeSpawnerProgramData program in asset.SpawnerPrograms.Where(program =>
                         program.EventStrideWords > 0))
            {
                if (program.EventStrideWords != strideWords ||
                    !device.SetVFXSpawnerEventRecordDefaults(
                        effectId, program.ContextId, record))
                    throw new InvalidOperationException(
                        "Native VFX Spawner default Event record installation failed.");
            }
        }
        _spawnerRegisteredDevices.Add(device);
    }

    internal void EnsureNativeVfxStateRegistered(NativeGraphicsDevice device)
    {
        EnsureSpawnerProgramsRegistered(device);
        EnsurePlanarOutputsRegistered(device);
    }

    private void EnsurePlanarOutputsRegistered(NativeGraphicsDevice device)
    {
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null) return;
        uint version = asset.GetCompilationVersion();
        if (_planarRegisteredVersions.TryGetValue(device, out uint registeredVersion) &&
            registeredVersion == version)
            return;
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (!device.SetVFXPlanarOutputs(effectId, asset.GetPlanarOutputs(), asset))
            throw new InvalidOperationException("Native VFX Planar Output installation failed.");
        _planarRegisteredVersions[device] = version;
    }

    internal void DrawPlanarOutputs(
        NativeGraphicsDevice device,
        Camera camera,
        Matrix4x4 worldToClip,
        bool clear = false)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        if (camera is null) throw new ArgumentNullException(nameof(camera));
        EnsurePlanarOutputsRegistered(device);
        ulong effectId = unchecked((ulong)(uint)GetInstanceID());
        if (_visualEffectAsset?.GetPlanarOutputs().Count == 0)
        {
            lastPlanarDrawInfo = new AnityNative.GraphicsVFXPlanarDrawInfo
            {
                effectId = effectId,
                cameraId = unchecked((ulong)(uint)camera.GetInstanceID())
            };
            return;
        }
        Matrix4x4 localToWorld = transform?.localToWorldMatrix ?? Matrix4x4.identity;
        if (!device.DrawVFXPlanarOutputs(
                effectId, localToWorld, worldToClip, camera, clear,
                out AnityNative.GraphicsVFXPlanarDrawInfo info))
            throw new InvalidOperationException("Native VFX Planar Output draw failed.");
        lastPlanarDrawInfo = info;
    }

    internal void SetLastPlanarCameraDrawInfo(
        AnityNative.GraphicsVFXPlanarCameraDrawInfo info)
        => lastPlanarCameraDrawInfo = info;

    private void EnsureSpawnerInstances()
    {
        VisualEffectAsset? asset = _visualEffectAsset;
        if (asset is null || _spawnerInstances.Count != 0) return;
        foreach (VFXRuntimeSpawnerProgramData program in asset.SpawnerPrograms)
        {
            int systemId = Shader.PropertyToID(program.SystemName);
            _spawnerInstances.Add(systemId, new VFXRuntimeSpawnerInstance(program, asset, _startSeed));
        }
    }

    private void DisposeSpawnerInstances()
    {
        foreach (VFXRuntimeSpawnerInstance instance in _spawnerInstances.Values) instance.Dispose();
        _spawnerInstances.Clear();
        _spawnerRegisteredDevices.Clear();
        _planarRegisteredVersions.Clear();
        lastPlanarDrawInfo = default;
        lastPlanarCameraDrawInfo = default;
    }

    private static AnityNative.Result InvokeNativeSpawnerCallback(
        IntPtr userData,
        long blockId,
        int phase,
        ref AnityNative.GraphicsVFXSpawnerState state,
        IntPtr eventRecord,
        int eventRecordByteCount)
    {
        try
        {
            var bridge = (SpawnerCallbackBridge?)GCHandle.FromIntPtr(userData).Target
                ?? throw new InvalidOperationException("VFX Spawner callback bridge is unavailable.");
            if (!bridge.Effect._spawnerInstances.TryGetValue(
                    state.systemId, out VFXRuntimeSpawnerInstance? instance))
                throw new InvalidDataException(
                    $"Native VFX Spawner callback references unknown system '{state.systemId}'.");
            instance.InvokeCallback(
                blockId, phase, ref state, eventRecord, eventRecordByteCount, bridge.Effect);
            return AnityNative.Result.Ok;
        }
        catch (Exception exception)
        {
            try
            {
                ((SpawnerCallbackBridge?)GCHandle.FromIntPtr(userData).Target)?.Capture(exception);
            }
            catch
            {
            }
            return AnityNative.Result.Internal;
        }
    }

    private sealed class SpawnerCallbackBridge
    {
        private ExceptionDispatchInfo? _exception;

        internal SpawnerCallbackBridge(VisualEffect effect)
        {
            Effect = effect;
        }

        internal VisualEffect Effect { get; }

        internal void Capture(Exception exception)
            => _exception ??= ExceptionDispatchInfo.Capture(exception);

        internal void ThrowIfCaptured() => _exception?.Throw();
    }

    private static byte[] EmptyPayload(out int strideWords)
    {
        strideWords = 0;
        return Array.Empty<byte>();
    }

    private bool HasValue(int nameID, Type expectedType)
        => _visualEffectAsset?.TryGetExposedProperty(nameID, out VFXExposedPropertyDefinition definition) == true &&
           expectedType.IsAssignableFrom(definition.Type);

    private void SetValue(int nameID, Type expectedType, object? value)
    {
        if (HasValue(nameID, expectedType)) _propertyOverrides[nameID] = value;
    }

    private T GetValue<T>(int nameID, Type expectedType, T defaultValue)
    {
        if (!HasValue(nameID, expectedType)) return defaultValue;
        if (_propertyOverrides.TryGetValue(nameID, out object? value))
            return value is null ? defaultValue : (T)value;
        if (_visualEffectAsset!.TryGetExposedProperty(nameID, out VFXExposedPropertyDefinition definition) &&
            definition.DefaultValue is T typedDefault)
            return typedDefault;
        return defaultValue;
    }

    private static int PropertyId(string name) => Shader.PropertyToID(name);

    private static void FillNames(List<string> names, Action<List<string>>? fill)
    {
        if (names is null) throw new ArgumentNullException(nameof(names));
        if (fill is null) names.Clear();
        else fill(names);
    }
}

internal readonly record struct VFXPendingEvent(
    int NameId,
    ulong Sequence,
    VFXEventAttribute? EventAttribute,
    byte[] Payload,
    int StrideWords);

internal readonly record struct VFXCommittedUpdateTicket(
    ulong TicketId,
    int[] ParticleSystemIds);

internal readonly record struct VFXCommittedInitializeTicket(
    NativeGraphicsDevice Device,
    ulong TicketId);

internal enum VFXManualUpdateKind
{
    AdvanceOneFrame,
    Simulate
}

internal readonly record struct VFXManualUpdate(
    VFXManualUpdateKind Kind,
    float StepDeltaTime,
    uint StepCount);

internal readonly record struct VFXManagedFrameSnapshot(
    float Time,
    float DeltaTime,
    float Accumulator,
    uint FrameIndex,
    int AliveParticleCount,
    VFXBoundInputEventDispatchPlan? LastInputEventDispatch);

internal sealed record VFXStagedOutputBatch(
    VisualEffectAsset Asset,
    int EventNameId,
    VFXRuntimeOutputEventData OutputEvent,
    byte[] Records,
    int StrideBytes,
    int RecordCount);

internal sealed record VFXBoundInputEventDispatchPlan(
    NativeVFXEventDispatchPlan NativePlan,
    IReadOnlyList<VFXBoundInputEventDispatchBatch> Batches);

internal readonly record struct VFXBoundInputEventDispatchBatch(
    AnityNative.GraphicsVFXEventDispatchBatch NativeBatch,
    VFXRuntimeInputEventData? InputEvent);
