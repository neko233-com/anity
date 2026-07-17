using Anity.Core.Runtime.Native;

namespace UnityEngine.VFX;

[Flags]
public enum VFXCameraBufferTypes
{
    None = 0,
    Depth = 1,
    Color = 2,
    Normal = 4
}

[Scripting.RequiredByNativeCode]
public struct VFXCameraXRSettings
{
    public uint viewTotal;
    public uint viewCount;
    public uint viewOffset;
}

[Scripting.RequiredByNativeCode]
public struct VFXBatchedEffectInfo
{
    public VisualEffectAsset vfxAsset;
    public uint activeBatchCount;
    public uint inactiveBatchCount;
    public uint activeInstanceCount;
    public uint unbatchedInstanceCount;
    public uint totalInstanceCapacity;
    public uint maxInstancePerBatchCapacity;
    public ulong totalGPUSizeInBytes;
    public ulong totalCPUSizeInBytes;
}

[Bindings.NativeType(Header = "Modules/VFX/Public/VFXExpressionValues.h")]
[Scripting.RequiredByNativeCode]
public class VFXExpressionValues
{
    private readonly Dictionary<int, object?> _values = new();

    private VFXExpressionValues()
    {
    }

    internal static VFXExpressionValues Create() => new();

    internal void SetValue<T>(int nameID, T value) => _values[nameID] = value;
    internal void SetValue<T>(string name, T value) => SetValue(Shader.PropertyToID(name), value);

    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<bool>")]
    public bool GetBool(int nameID) => Get<bool>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<int>")]
    public int GetInt(int nameID) => Get<int>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<UInt32>")]
    public uint GetUInt(int nameID) => Get<uint>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<float>")]
    public float GetFloat(int nameID) => Get<float>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<Vector2f>")]
    public Vector2 GetVector2(int nameID) => Get<Vector2>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<Vector3f>")]
    public Vector3 GetVector3(int nameID) => Get<Vector3>(nameID);
    [Bindings.NativeName("GetValueFromScript<Vector4f>"), Bindings.NativeThrows]
    public Vector4 GetVector4(int nameID) => Get<Vector4>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<Matrix4x4f>")]
    public Matrix4x4 GetMatrix4x4(int nameID) => Get<Matrix4x4>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<Texture*>")]
    public Texture GetTexture(int nameID) => Get<Texture>(nameID);
    [Bindings.NativeThrows, Bindings.NativeName("GetValueFromScript<Mesh*>")]
    public Mesh GetMesh(int nameID) => Get<Mesh>(nameID);

    public AnimationCurve GetAnimationCurve(int nameID)
    {
        AnimationCurve source = Get<AnimationCurve>(nameID);
        return new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
    }

    public Gradient GetGradient(int nameID)
    {
        Gradient source = Get<Gradient>(nameID);
        return new Gradient
        {
            colorKeys = source.colorKeys.ToArray(),
            alphaKeys = source.alphaKeys.ToArray(),
            mode = source.mode
        };
    }

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

    private T Get<T>(int nameID)
    {
        if (_values.TryGetValue(nameID, out object? value) && value is T typed) return typed;
        throw new ArgumentException(
            $"VFX expression '{Shader.GetPropertyName(nameID)}' is missing or is not a {typeof(T).Name}.",
            nameof(nameID));
    }

    private static int PropertyId(string name) => Shader.PropertyToID(name);
}

[Bindings.NativeHeader("Modules/VFX/Public/VFXManager.h")]
[Bindings.NativeHeader("Modules/VFX/Public/ScriptBindings/VFXManagerBindings.h")]
[Bindings.StaticAccessor("GetVFXManager()", Bindings.StaticAccessorType.Dot)]
[Scripting.RequiredByNativeCode]
public static class VFXManager
{
    private static readonly object Sync = new();
    private static readonly Dictionary<(int CameraId, VFXCameraBufferTypes Type), CameraBufferBinding> CameraBuffers = new();
    private static readonly Dictionary<int, VFXCameraXRSettings> PreparedCameras = new();
    private static float _fixedTimeStep = 1f / 60f;
    private static float _maxDeltaTime = 1f / 20f;
    private static uint _frameIndex;
    private static ulong _playerLoopToken;
    private static int _lastPlayerLoopTimeFrame = int.MinValue;
    private static NativeGraphicsDevice? _lastPlayerLoopDevice;
    private static ulong _activeCullingToken;
    private static NativeGraphicsDevice? _activeCullingDevice;
    private static VisualEffect[] _activeCullingEffects = Array.Empty<VisualEffect>();
    private static readonly HashSet<ulong> SubmittedCullingCameras = new();

    public static VisualEffect[] GetComponents()
        => Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

    public static float fixedTimeStep
    {
        get => _fixedTimeStep;
        set
        {
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
            _fixedTimeStep = value;
        }
    }

    public static float maxDeltaTime
    {
        get => _maxDeltaTime;
        set
        {
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
            _maxDeltaTime = value;
        }
    }

    public static void FlushEmptyBatches()
    {
        lock (Sync)
        {
            int[] liveCameraIds = Camera.AllCameras.Select(camera => camera.GetInstanceID()).ToArray();
            foreach ((int cameraId, VFXCameraBufferTypes type) in CameraBuffers.Keys.ToArray())
                if (!liveCameraIds.Contains(cameraId)) CameraBuffers.Remove((cameraId, type));
            foreach (int cameraId in PreparedCameras.Keys.ToArray())
                if (!liveCameraIds.Contains(cameraId)) PreparedCameras.Remove(cameraId);
        }
    }

    public static VFXBatchedEffectInfo GetBatchedEffectInfo(
        [Bindings.NotNull("NullExceptionObject")] VisualEffectAsset vfx)
    {
        if (vfx is null) throw new ArgumentNullException(nameof(vfx));
        VisualEffect[] components = GetComponents().Where(effect => ReferenceEquals(effect.visualEffectAsset, vfx)).ToArray();
        uint active = checked((uint)components.Count(effect => effect.enabled));
        uint inactive = checked((uint)components.Length) - active;
        return new VFXBatchedEffectInfo
        {
            vfxAsset = vfx,
            activeBatchCount = active > 0 ? 1u : 0u,
            inactiveBatchCount = inactive > 0 ? 1u : 0u,
            activeInstanceCount = active,
            unbatchedInstanceCount = 0,
            totalInstanceCapacity = checked((uint)components.Length),
            maxInstancePerBatchCapacity = checked((uint)components.Length),
            totalGPUSizeInBytes = 0,
            totalCPUSizeInBytes = checked((ulong)components.Length * 64ul)
        };
    }

    [Bindings.FreeFunction(Name = "VFXManagerBindings::GetBatchedEffectInfos", HasExplicitThis = false)]
    public static void GetBatchedEffectInfos(
        [Bindings.NotNull("NullExceptionObject")] List<VFXBatchedEffectInfo> infos)
    {
        if (infos is null) throw new ArgumentNullException(nameof(infos));
        infos.Clear();
        foreach (VisualEffectAsset asset in GetComponents()
                     .Select(effect => effect.visualEffectAsset)
                     .Where(asset => asset is not null)
                     .Distinct()!)
            infos.Add(GetBatchedEffectInfo(asset));
    }

    [Obsolete("Use explicit PrepareCamera and ProcessCameraCommand instead")]
    public static void ProcessCamera(Camera cam)
    {
        PrepareCamera(cam);
        ProcessCameraCommand(cam, null!, DefaultCameraXRSettings);
    }

    public static void PrepareCamera(Camera cam) => PrepareCamera(cam, DefaultCameraXRSettings);

    public static void PrepareCamera(
        [Bindings.NotNull("NullExceptionObject")] Camera cam,
        VFXCameraXRSettings camXRSettings)
    {
        ValidateCamera(cam);
        ValidateXRSettings(camXRSettings);
        lock (Sync) PreparedCameras[cam.GetInstanceID()] = camXRSettings;
    }

    [Obsolete("Use ProcessCameraCommand with CullingResults to allow culling of VFX per camera")]
    public static void ProcessCameraCommand(Camera cam, Rendering.CommandBuffer cmd)
        => ProcessCameraCommand(cam, cmd, DefaultCameraXRSettings);

    [Obsolete("Use ProcessCameraCommand with CullingResults to allow culling of VFX per camera")]
    public static void ProcessCameraCommand(Camera cam, Rendering.CommandBuffer cmd, VFXCameraXRSettings camXRSettings)
        => ProcessCameraCommandInternal(cam, cmd, camXRSettings);

    public static void ProcessCameraCommand(
        Camera cam,
        Rendering.CommandBuffer cmd,
        VFXCameraXRSettings camXRSettings,
        Rendering.CullingResults results)
        => ProcessCameraCommandInternal(cam, cmd, camXRSettings);

    public static VFXCameraBufferTypes IsCameraBufferNeeded(
        [Bindings.NotNull("NullExceptionObject")] Camera cam)
    {
        ValidateCamera(cam);
        VFXCameraBufferTypes result = VFXCameraBufferTypes.None;
        foreach (VisualEffect effect in GetComponents())
            if (effect.enabled && effect.visualEffectAsset is not null)
                result |= effect.visualEffectAsset.CameraBufferRequirements;
        return result;
    }

    public static void SetCameraBuffer(
        [Bindings.NotNull("NullExceptionObject")] Camera cam,
        VFXCameraBufferTypes type,
        Texture buffer,
        int x,
        int y,
        int width,
        int height)
    {
        ValidateCamera(cam);
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (x < 0 || y < 0 || width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width));
        lock (Sync)
            CameraBuffers[(cam.GetInstanceID(), type)] = new CameraBufferBinding(buffer, x, y, width, height);
    }

    internal static bool TryGetCameraBuffer(
        Camera camera,
        VFXCameraBufferTypes type,
        out Texture? texture,
        out RectInt viewport)
    {
        lock (Sync)
        {
            if (CameraBuffers.TryGetValue((camera.GetInstanceID(), type), out CameraBufferBinding binding))
            {
                texture = binding.Texture;
                viewport = new RectInt(binding.X, binding.Y, binding.Width, binding.Height);
                return true;
            }
        }
        texture = null;
        viewport = default;
        return false;
    }

    private static VFXCameraXRSettings DefaultCameraXRSettings
        => new() { viewTotal = 1, viewCount = 1, viewOffset = 0 };

    private static void ProcessCameraCommandInternal(
        Camera cam,
        Rendering.CommandBuffer? cmd,
        VFXCameraXRSettings camXRSettings)
    {
        PrepareCamera(cam, camXRSettings);
        NativeGraphicsDevice? device = NativeGraphicsDevice.Current;
        if (device is null) return;
        device.ExecuteWhileAlive(() =>
        {
            lock (Sync)
            {
                if (_lastPlayerLoopTimeFrame == Time.frameCount &&
                    ReferenceEquals(_lastPlayerLoopDevice, device))
                    return;
            }
            uint frameIndex = BeginNativeFrame(device);
            ProcessEffects(device, frameIndex);
        });
    }

    internal static void ProcessPlayerLoopUpdate()
    {
        CompletePlayerLoopCulling();
        NativeGraphicsDevice? device = NativeGraphicsDevice.Current;
        if (device is null) return;
        device.ExecuteWhileAlive(() => ProcessPlayerLoopUpdate(device));
    }

    private static void ProcessPlayerLoopUpdate(NativeGraphicsDevice device)
    {
        ulong token;
        lock (Sync)
        {
            _playerLoopToken = unchecked(_playerLoopToken + 1ul);
            if (_playerLoopToken == 0) _playerLoopToken = 1;
            token = _playerLoopToken;
        }
        if (!device.BeginVFXPlayerLoopFrame(
                token, out uint frameIndex, out bool beganFrame) ||
            !beganFrame)
            throw new InvalidOperationException("Native VFX PlayerLoop frame clock failed.");
        VisualEffect[] effects = GetComponents();
        AnityNative.GraphicsVFXCullingBounds[] bounds = effects
            .Select(effect => CreateCullingBounds(effect, device))
            .ToArray();
        if (!device.BeginVFXCullingFrame(token, bounds))
            throw new InvalidOperationException("Native VFX culling frame failed to begin.");
        lock (Sync)
        {
            _frameIndex = frameIndex;
            _activeCullingToken = token;
            _activeCullingDevice = device;
            _activeCullingEffects = effects;
            SubmittedCullingCameras.Clear();
        }
        UpdateCullingFlags(device, effects);
        try
        {
            ProcessEffects(device, frameIndex);
        }
        catch (Exception frameException)
        {
            try
            {
                CompletePlayerLoopCulling();
            }
            catch (Exception cullingException)
            {
                throw new AggregateException(
                    "VFX PlayerLoop frame and culling recovery both failed.",
                    frameException, cullingException);
            }
            throw;
        }
        lock (Sync)
        {
            _lastPlayerLoopTimeFrame = Time.frameCount;
            _lastPlayerLoopDevice = device;
        }
    }

    internal static void ProcessCameraCommandFromRenderLoop(
        Camera camera,
        Rendering.CommandBuffer commandBuffer)
    {
        PrepareCamera(camera, DefaultCameraXRSettings);
        NativeGraphicsDevice? device;
        VisualEffect[] effects;
        ulong token;
        ulong cameraId = unchecked((ulong)(uint)camera.GetInstanceID());
        lock (Sync)
        {
            device = _activeCullingDevice;
            effects = _activeCullingEffects;
            token = _activeCullingToken;
            if (device is null || token == 0 || !SubmittedCullingCameras.Add(cameraId)) return;
        }
        Matrix4x4 matrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        var descriptor = new AnityNative.GraphicsVFXCullingCamera
        {
            cameraId = cameraId,
            m00 = matrix.m00, m01 = matrix.m01, m02 = matrix.m02, m03 = matrix.m03,
            m10 = matrix.m10, m11 = matrix.m11, m12 = matrix.m12, m13 = matrix.m13,
            m20 = matrix.m20, m21 = matrix.m21, m22 = matrix.m22, m23 = matrix.m23,
            m30 = matrix.m30, m31 = matrix.m31, m32 = matrix.m32, m33 = matrix.m33,
            cullingMask = camera.cullingMask,
            cameraType = (int)camera.cameraType
        };
        device.ExecuteWhileAlive(() =>
        {
            if (!device.SubmitVFXCullingCamera(token, descriptor))
                throw new InvalidOperationException("Native VFX camera culling submission failed.");
            var planarEffects = new List<VisualEffect>();
            foreach (VisualEffect effect in effects)
            {
                GameObject? owner = effect.gameObject;
                int layer = owner?.layer ?? 0;
                if (!effect.enabled || owner?.activeInHierarchy == false ||
                    effect.visualEffectAsset is null ||
                    (unchecked((uint)camera.cullingMask) & (1u << layer)) == 0)
                    continue;
                effect.EnsureNativeVfxStateRegistered(device);
                if (effect.hasPlanarOutputs) planarEffects.Add(effect);
            }
            if (planarEffects.Count == 0) return;
            if (!device.DrawVFXPlanarCamera(
                    camera, matrix, planarEffects, clear: false,
                    out AnityNative.GraphicsVFXPlanarCameraDrawInfo info))
                throw new InvalidOperationException("Native VFX Planar camera batch draw failed.");
            foreach (VisualEffect effect in planarEffects)
                effect.SetLastPlanarCameraDrawInfo(info);
        });
    }

    internal static void CompletePlayerLoopCulling()
    {
        NativeGraphicsDevice? device;
        VisualEffect[] effects;
        ulong token;
        lock (Sync)
        {
            device = _activeCullingDevice;
            effects = _activeCullingEffects;
            token = _activeCullingToken;
            if (device is null || token == 0) return;
            _activeCullingDevice = null;
            _activeCullingEffects = Array.Empty<VisualEffect>();
            _activeCullingToken = 0;
            SubmittedCullingCameras.Clear();
        }
        device.ExecuteWhileAlive(() =>
        {
            if (!device.CompleteVFXCullingFrame(token))
                throw new InvalidOperationException("Native VFX culling frame failed to complete.");
            UpdateCullingFlags(device, effects);
        });
    }

    private static AnityNative.GraphicsVFXCullingBounds CreateCullingBounds(
        VisualEffect effect,
        NativeGraphicsDevice device)
    {
        Bounds bounds = default;
        bool valid = effect.enabled && effect.visualEffectAsset is not null &&
                     effect.TryGetWorldCullingBounds(device, out bounds);
        Vector3 center = valid ? bounds.center : Vector3.zero;
        Vector3 extents = valid ? bounds.extents : Vector3.zero;
        return new AnityNative.GraphicsVFXCullingBounds
        {
            effectId = unchecked((ulong)(uint)effect.GetInstanceID()),
            centerX = center.x,
            centerY = center.y,
            centerZ = center.z,
            extentsX = extents.x,
            extentsY = extents.y,
            extentsZ = extents.z,
            layer = effect.gameObject?.layer ?? 0,
            valid = valid ? 1 : 0
        };
    }

    private static void UpdateCullingFlags(
        NativeGraphicsDevice device,
        IReadOnlyList<VisualEffect> effects)
    {
        foreach (VisualEffect effect in effects)
        {
            if (effect == null || effect.IsDestroyed) continue;
            ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());
            if (!device.TryGetVFXCullingState(effectId, out AnityNative.GraphicsVFXCullingState state))
                throw new InvalidOperationException("Native VFX culling state is unavailable.");
            effect.culled = state.culled != 0;
        }
    }

    private static void ProcessEffects(
        NativeGraphicsDevice device,
        uint frameIndex)
    {
        foreach (VisualEffect effect in GetComponents())
        {
            if (!effect.enabled) continue;
            effect.DeliverCommittedOutputEvents();
            ProcessManualUpdates(effect, device, frameIndex);
            if (effect.culled) continue;
            ProcessEffectFrame(
                effect, device, frameIndex, Time.deltaTime,
                manual: false, forceUnpaused: false);
        }
    }

    private static void ProcessManualUpdates(
        VisualEffect effect,
        NativeGraphicsDevice device,
        uint frameIndex)
    {
        while (effect.TryDequeueManualUpdate(out VFXManualUpdate update))
        {
            switch (update.Kind)
            {
                case VFXManualUpdateKind.AdvanceOneFrame:
                    ProcessEffectFrame(
                        effect, device, frameIndex, Time.deltaTime,
                        manual: false, forceUnpaused: true);
                    break;
                case VFXManualUpdateKind.Simulate:
                    for (uint step = 0; step < update.StepCount; step++)
                        ProcessEffectFrame(
                            effect, device, frameIndex, update.StepDeltaTime,
                            manual: true, forceUnpaused: true);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown VFX manual update kind '{update.Kind}'.");
            }
        }
    }

    private static void ProcessEffectFrame(
        VisualEffect effect,
        NativeGraphicsDevice device,
        uint frameIndex,
        float deltaTime,
        bool manual,
        bool forceUnpaused)
    {
        bool prepared = false;
        try
        {
            effect.EnsureNativeVfxStateRegistered(device);
            float vfxDeltaTime = manual
                ? effect.PrepareManualVfxFrame(deltaTime, frameIndex, device)
                : effect.PrepareVfxFrame(
                    deltaTime, frameIndex, device,
                    forceUnpaused ? false : null);
            prepared = true;
            effect.ProcessInputEvents(
                device, deferInitializeCompletion: true);
            effect.AdvanceSpawnerSystems(
                vfxDeltaTime, device, advanceFrameIndex: false,
                allowUnsafeDeltaTime: manual,
                deferInitializeCompletion: true);
            effect.UpdateParticleSystems(vfxDeltaTime, device);
            effect.StageOutputEventsForCommit(device);
            effect.CompleteVfxFrame(device);
            prepared = false;
            effect.DeliverCommittedOutputEvents();
        }
        catch (Exception frameException)
        {
            effect.ClearPendingManualUpdates();
            if (prepared)
            {
                try
                {
                    effect.AbortVfxFrame(device);
                }
                catch (Exception abortException)
                {
                    throw new AggregateException(
                        "VFX frame execution and native rollback both failed.",
                        frameException, abortException);
                }
            }
            throw;
        }
    }

    internal static uint AdvanceManualFrameIndex(NativeGraphicsDevice? device = null)
        => device is not null ? BeginNativeFrame(device) : AdvanceManagedFrameIndex();

    private static uint BeginNativeFrame(NativeGraphicsDevice device)
    {
        if (!device.BeginVFXFrame(out uint frameIndex))
            throw new InvalidOperationException("Native VFX manager frame clock failed.");
        lock (Sync) _frameIndex = frameIndex;
        return frameIndex;
    }

    private static uint AdvanceManagedFrameIndex()
    {
        lock (Sync)
        {
            _frameIndex = unchecked(_frameIndex + 1u);
            return _frameIndex;
        }
    }

    private static void ValidateCamera(Camera cam)
    {
        if (cam is null) throw new ArgumentNullException(nameof(cam));
    }

    private static void ValidateXRSettings(VFXCameraXRSettings settings)
    {
        if (settings.viewTotal == 0 || settings.viewCount == 0 || settings.viewOffset + settings.viewCount > settings.viewTotal)
            throw new ArgumentOutOfRangeException(nameof(settings));
    }

    private readonly record struct CameraBufferBinding(Texture Texture, int X, int Y, int Width, int Height);
}

[Serializable]
[Scripting.RequiredByNativeCode]
public abstract class VFXSpawnerCallbacks : ScriptableObject
{
    public abstract void OnPlay(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent);
    public abstract void OnUpdate(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent);
    public abstract void OnStop(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent);
}
