using Anity.Core.Runtime.Native;
using System.Runtime.InteropServices;

namespace UnityEngine.VFX;

/// <summary>
/// Managed Unity API snapshot for one native VFX Spawner program. Scheduling,
/// clocks, random streams and task accumulators are owned by anity-native.
/// </summary>
internal sealed class VFXRuntimeSpawnerInstance : IDisposable
{
    private bool _disposed;
    private readonly VisualEffectAsset _asset;
    private readonly Dictionary<long, CallbackBinding> _callbacks = new();
    private uint _systemSeed;

    internal VFXRuntimeSpawnerInstance(
        VFXRuntimeSpawnerProgramData program,
        VisualEffectAsset asset,
        uint systemSeed)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _systemSeed = systemSeed;
        State = new VFXSpawnerState(asset)
        {
            loopState = VFXSpawnerLoopState.Finished,
            loopCount = 0,
            loopDuration = 0f
        };
        foreach (VFXRuntimeSpawnerBlockData block in program.Blocks.Where(candidate =>
                     candidate.Kind == VFXRuntimeSpawnerBlockKind.CustomCallback))
            _callbacks.Add(block.BlockId, CallbackBinding.Create(block));
    }

    internal VFXRuntimeSpawnerProgramData Program { get; }

    internal VFXSpawnerState State { get; }

    internal bool HasCallbacks => _callbacks.Count != 0;

    internal uint SystemSeed
    {
        get => _systemSeed;
        set => _systemSeed = value;
    }

    internal void ApplyNativeState(
        ulong effectId,
        AnityNative.GraphicsVFXSpawnerState native,
        bool allowUnsafeTime = false)
    {
        ThrowIfDisposed();
        int expectedSystemId = Shader.PropertyToID(Program.SystemName);
        if (native.effectId != effectId || native.contextId != Program.ContextId ||
            native.systemId != expectedSystemId || native.loopState is < 0 or > 3 ||
            native.playing is < 0 or > 1 || native.newLoop is < 0 or > 1 ||
            (native.playing == 1) != (native.loopState == (int)VFXSpawnerLoopState.Looping) ||
            !float.IsFinite(native.spawnCount) ||
            (!allowUnsafeTime &&
             (!float.IsFinite(native.deltaTime) || native.deltaTime < 0f ||
              !float.IsFinite(native.totalTime) || native.totalTime < 0f)) ||
            !float.IsFinite(native.delayBeforeLoop) || native.delayBeforeLoop < 0f ||
            !float.IsFinite(native.loopDuration) ||
            (native.loopDuration < 0f && native.loopDuration != -1f) ||
            !float.IsFinite(native.delayAfterLoop) || native.delayAfterLoop < 0f ||
            native.loopIndex < 0 || native.loopCount < -1)
            throw new InvalidDataException("Native VFX Spawner state is invalid.");
        ApplyNativeState(State, native);
        // Unity exposes the raw task-chain value through both the dedicated
        // state property and the Spawner event attribute snapshot. This is
        // deliberately not the accumulated Initialize dispatch count.
        State.vfxEventAttribute.SetFloat("spawnCount", native.spawnCount);
        State.SetNewLoop(native.newLoop != 0);
    }

    internal void LoadEventRecord(ReadOnlySpan<byte> record)
    {
        ThrowIfDisposed();
        State.vfxEventAttribute.LoadRuntimeRecord(record);
    }

    internal void InvokeCallback(
        long blockId,
        int phase,
        ref AnityNative.GraphicsVFXSpawnerState native,
        IntPtr eventRecord,
        int eventRecordByteCount,
        VisualEffect component)
    {
        ThrowIfDisposed();
        if (!_callbacks.TryGetValue(blockId, out CallbackBinding? binding))
            throw new InvalidDataException($"Native VFX Spawner requested unknown callback block '{blockId}'.");
        if (phase is < 1 or > 3)
            throw new InvalidDataException($"Native VFX Spawner callback phase '{phase}' is invalid.");
        bool update = phase == 2;
        VFXSpawnerState callbackState = update ? State : new VFXSpawnerState(_asset);
        try
        {
            ApplyNativeState(callbackState, native);
            callbackState.SetNewLoop(native.newLoop != 0);
            binding.UpdateValues(component, callbackState, _systemSeed);
            if (update && eventRecordByteCount > 0)
            {
                if (eventRecord == IntPtr.Zero)
                    throw new InvalidDataException("Native VFX Spawner callback record pointer is null.");
                var record = new byte[eventRecordByteCount];
                Marshal.Copy(eventRecord, record, 0, record.Length);
                callbackState.vfxEventAttribute.LoadRuntimeRecord(record);
            }
            switch (phase)
            {
                case 1:
                    binding.Callback.OnPlay(callbackState, binding.Values, component);
                    break;
                case 2:
                    binding.Callback.OnUpdate(callbackState, binding.Values, component);
                    break;
                case 3:
                    binding.Callback.OnStop(callbackState, binding.Values, component);
                    break;
            }
            CopyToNative(callbackState, ref native);
            if (update && eventRecordByteCount > 0)
            {
                byte[] record = callbackState.vfxEventAttribute.PackValues(out int strideWords);
                if (record.Length != eventRecordByteCount ||
                    strideWords != Program.EventStrideWords)
                    throw new InvalidDataException("Managed VFX Spawner callback changed the Event record layout.");
                Marshal.Copy(record, 0, eventRecord, record.Length);
            }
        }
        finally
        {
            if (!update) callbackState.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (CallbackBinding callback in _callbacks.Values)
            Object.DestroyImmediate(callback.Callback);
        _callbacks.Clear();
        State.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VFXRuntimeSpawnerInstance));
    }

    private static void ApplyNativeState(
        VFXSpawnerState state,
        AnityNative.GraphicsVFXSpawnerState native)
    {
        state.loopState = (VFXSpawnerLoopState)native.loopState;
        state.spawnCount = native.spawnCount;
        state.deltaTime = native.deltaTime;
        state.totalTime = native.totalTime;
        state.delayBeforeLoop = native.delayBeforeLoop;
        state.loopDuration = native.loopDuration;
        state.delayAfterLoop = native.delayAfterLoop;
        state.loopIndex = native.loopIndex;
        state.loopCount = native.loopCount;
    }

    private static void CopyToNative(
        VFXSpawnerState state,
        ref AnityNative.GraphicsVFXSpawnerState native)
    {
        native.loopState = (int)state.loopState;
        native.playing = state.playing ? 1 : 0;
        native.spawnCount = state.spawnCount;
        native.deltaTime = state.deltaTime;
        native.totalTime = state.totalTime;
        native.delayBeforeLoop = state.delayBeforeLoop;
        native.loopDuration = state.loopDuration;
        native.delayAfterLoop = state.delayAfterLoop;
        native.loopIndex = state.loopIndex;
        native.loopCount = state.loopCount;
    }

    private sealed class CallbackBinding
    {
        private readonly IReadOnlyList<VFXRuntimeSpawnerExpressionValueData> _inputs;

        private CallbackBinding(
            VFXSpawnerCallbacks callback,
            VFXExpressionValues values,
            IReadOnlyList<VFXRuntimeSpawnerExpressionValueData> inputs)
        {
            Callback = callback;
            Values = values;
            _inputs = inputs;
        }

        internal VFXSpawnerCallbacks Callback { get; }
        internal VFXExpressionValues Values { get; }

        internal static CallbackBinding Create(VFXRuntimeSpawnerBlockData block)
        {
            string assemblyQualifiedName = block.CallbackTypeName
                ?? throw new InvalidDataException(
                    $"VFX callback block '{block.BlockId}' has no callback type.");
            Type? type = Type.GetType(assemblyQualifiedName, throwOnError: false);
            if (type is null)
            {
                string typeName = assemblyQualifiedName.Split(',')[0].Trim();
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                    .FirstOrDefault(candidate => candidate is not null);
            }
            if (type is null || type.IsAbstract ||
                !typeof(VFXSpawnerCallbacks).IsAssignableFrom(type))
                throw new InvalidDataException(
                    $"VFX callback block '{block.BlockId}' type '{assemblyQualifiedName}' is unavailable or invalid.");
            var callback = (VFXSpawnerCallbacks)ScriptableObject.CreateInstance(type);
            VFXExpressionValues values = VFXExpressionValues.Create();
            foreach (VFXRuntimeSpawnerExpressionValueData value in block.CallbackValues)
                SetExpressionValue(values, value, component: null);
            return new CallbackBinding(callback, values, block.CallbackValues);
        }

        internal void UpdateValues(
            VisualEffect component,
            VFXSpawnerState callbackState,
            uint systemSeed)
        {
            foreach (VFXRuntimeSpawnerExpressionValueData value in _inputs)
                if (value.SourcePropertyName is not null || value.Expression is not null)
                    SetExpressionValue(Values, value, component, callbackState, systemSeed);
        }

        private static void SetExpressionValue(
            VFXExpressionValues values,
            VFXRuntimeSpawnerExpressionValueData value,
            VisualEffect? component,
            VFXSpawnerState? callbackState = null,
            uint systemSeed = 0)
        {
            object operand = component is not null && value.Expression is not null
                ? EvaluateExpression(value.Expression, component, callbackState!, systemSeed)
                : component is not null && value.SourcePropertyName is not null
                    ? PropertyValue(component, value.SourcePropertyName, value.ValueType)
                    : Decode(value.ValueType, value.Words);
            values.SetValue(value.Name, operand);
        }

        private static object EvaluateExpression(
            VFXRuntimeExpressionProgramData program,
            VisualEffect component,
            VFXSpawnerState callbackState,
            uint systemSeed)
        {
            var values = new object[program.Instructions.Count];
            for (int index = 0; index < program.Instructions.Count; index++)
            {
                VFXRuntimeExpressionInstructionData instruction = program.Instructions[index];
                values[index] = instruction.Operation switch
                {
                    VFXRuntimeExpressionOperation.Constant =>
                        Decode(instruction.ValueType, instruction.ConstantWords),
                    VFXRuntimeExpressionOperation.ExposedProperty =>
                        PropertyValue(component, instruction.PropertyName!, instruction.ValueType),
                    VFXRuntimeExpressionOperation.Add =>
                        Binary(instruction.ValueType, values[instruction.InputA], values[instruction.InputB], 0),
                    VFXRuntimeExpressionOperation.Subtract =>
                        Binary(instruction.ValueType, values[instruction.InputA], values[instruction.InputB], 1),
                    VFXRuntimeExpressionOperation.Multiply =>
                        Binary(instruction.ValueType, values[instruction.InputA], values[instruction.InputB], 2),
                    VFXRuntimeExpressionOperation.OneMinus =>
                        OneMinus(instruction.ValueType, values[instruction.InputA]),
                    VFXRuntimeExpressionOperation.VfxDeltaTime => component.currentVfxDeltaTime,
                    VFXRuntimeExpressionOperation.VfxUnscaledDeltaTime =>
                        component.playRate == 0f ? 0f : component.currentVfxDeltaTime / component.playRate,
                    VFXRuntimeExpressionOperation.VfxTotalTime => component.currentTime,
                    VFXRuntimeExpressionOperation.VfxFrameIndex => component.currentVfxFrameIndex,
                    VFXRuntimeExpressionOperation.VfxPlayRate => component.playRate,
                    VFXRuntimeExpressionOperation.VfxManagerFixedTimeStep => VFXManager.fixedTimeStep,
                    VFXRuntimeExpressionOperation.VfxManagerMaxDeltaTime => VFXManager.maxDeltaTime,
                    VFXRuntimeExpressionOperation.GameDeltaTime => Time.deltaTime,
                    VFXRuntimeExpressionOperation.GameUnscaledDeltaTime => Time.unscaledDeltaTime,
                    VFXRuntimeExpressionOperation.GameSmoothDeltaTime => Time.smoothDeltaTime,
                    VFXRuntimeExpressionOperation.GameTotalTime => Time.time,
                    VFXRuntimeExpressionOperation.GameUnscaledTotalTime => Time.unscaledTime,
                    VFXRuntimeExpressionOperation.GameTotalTimeSinceSceneLoad => Time.timeSinceLevelLoad,
                    VFXRuntimeExpressionOperation.GameTimeScale => Time.timeScale,
                    VFXRuntimeExpressionOperation.LocalToWorld => component.transform.localToWorldMatrix,
                    VFXRuntimeExpressionOperation.WorldToLocal => component.transform.worldToLocalMatrix,
                    VFXRuntimeExpressionOperation.SystemSeed => systemSeed,
                    _ => throw new InvalidDataException(
                        $"VFX runtime expression opcode '{instruction.Operation}' is unsupported.")
                };
            }
            return values[program.ResultIndex];
        }

        private static object Binary(
            VFXRuntimeValueType type,
            object left,
            object right,
            int operation)
            => type switch
            {
                VFXRuntimeValueType.UInt32 => operation switch
                {
                    0 => unchecked((uint)left + (uint)right),
                    1 => unchecked((uint)left - (uint)right),
                    _ => unchecked((uint)left * (uint)right)
                },
                VFXRuntimeValueType.Int32 => operation switch
                {
                    0 => unchecked((int)left + (int)right),
                    1 => unchecked((int)left - (int)right),
                    _ => unchecked((int)left * (int)right)
                },
                VFXRuntimeValueType.Float => FloatBinary((float)left, (float)right, operation),
                VFXRuntimeValueType.Float2 => Vector2Binary((Vector2)left, (Vector2)right, operation),
                VFXRuntimeValueType.Float3 => Vector3Binary((Vector3)left, (Vector3)right, operation),
                VFXRuntimeValueType.Float4 => Vector4Binary((Vector4)left, (Vector4)right, operation),
                _ => throw new InvalidDataException(
                    $"VFX runtime expression type '{type}' does not support arithmetic.")
            };

        private static float FloatBinary(float left, float right, int operation)
            => operation == 0 ? left + right : operation == 1 ? left - right : left * right;

        private static Vector2 Vector2Binary(Vector2 left, Vector2 right, int operation)
            => new(FloatBinary(left.x, right.x, operation), FloatBinary(left.y, right.y, operation));

        private static Vector3 Vector3Binary(Vector3 left, Vector3 right, int operation)
            => new(
                FloatBinary(left.x, right.x, operation),
                FloatBinary(left.y, right.y, operation),
                FloatBinary(left.z, right.z, operation));

        private static Vector4 Vector4Binary(Vector4 left, Vector4 right, int operation)
            => new(
                FloatBinary(left.x, right.x, operation),
                FloatBinary(left.y, right.y, operation),
                FloatBinary(left.z, right.z, operation),
                FloatBinary(left.w, right.w, operation));

        private static object OneMinus(VFXRuntimeValueType type, object value)
            => type switch
            {
                VFXRuntimeValueType.Float => 1f - (float)value,
                VFXRuntimeValueType.Float2 => Vector2.one - (Vector2)value,
                VFXRuntimeValueType.Float3 => Vector3.one - (Vector3)value,
                VFXRuntimeValueType.Float4 => Vector4.one - (Vector4)value,
                _ => throw new InvalidDataException(
                    $"VFX runtime expression type '{type}' does not support OneMinus.")
            };

        private static object Decode(
            VFXRuntimeValueType type,
            IReadOnlyList<uint> words)
        {
            float Float(int index) => BitConverter.Int32BitsToSingle(unchecked((int)words[index]));
            return type switch
            {
                VFXRuntimeValueType.Boolean => words[0] != 0,
                VFXRuntimeValueType.UInt32 => words[0],
                VFXRuntimeValueType.Int32 => unchecked((int)words[0]),
                VFXRuntimeValueType.Float => Float(0),
                VFXRuntimeValueType.Float2 => new Vector2(Float(0), Float(1)),
                VFXRuntimeValueType.Float3 => new Vector3(Float(0), Float(1), Float(2)),
                VFXRuntimeValueType.Float4 => new Vector4(Float(0), Float(1), Float(2), Float(3)),
                VFXRuntimeValueType.Matrix4x4 => Matrix(words),
                _ => throw new InvalidDataException($"VFX runtime value type '{type}' is unsupported.")
            };
        }

        private static object PropertyValue(
            VisualEffect component,
            string name,
            VFXRuntimeValueType type)
            => type switch
            {
                VFXRuntimeValueType.Boolean => component.GetBool(name),
                VFXRuntimeValueType.UInt32 => component.GetUInt(name),
                VFXRuntimeValueType.Int32 => component.GetInt(name),
                VFXRuntimeValueType.Float => component.GetFloat(name),
                VFXRuntimeValueType.Float2 => component.GetVector2(name),
                VFXRuntimeValueType.Float3 => component.GetVector3(name),
                VFXRuntimeValueType.Float4 => component.GetVector4(name),
                VFXRuntimeValueType.Matrix4x4 => component.GetMatrix4x4(name),
                _ => throw new InvalidDataException(
                    $"VFX exposed property '{name}' has unsupported type '{type}'.")
            };

        private static Matrix4x4 Matrix(IReadOnlyList<uint> words)
        {
            var result = new Matrix4x4();
            for (int index = 0; index < 16; index++)
                result[index] = BitConverter.Int32BitsToSingle(unchecked((int)words[index]));
            return result;
        }
    }
}
