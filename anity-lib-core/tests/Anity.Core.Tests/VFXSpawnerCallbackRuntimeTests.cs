using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class VFXSpawnerCallbackRuntimeTests
{
    [Fact]
    public void RuntimeAssetV9_RoundTripsCallbackTypeAndTypedValues()
    {
        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(
            Data(Program(Callback(10, 2f, 100f))).Serialize());

        VFXRuntimeSpawnerBlockData callback = Assert.Single(
            Assert.Single(restored.SpawnerPrograms).Blocks);
        Assert.Equal(VFXRuntimeSpawnerBlockKind.CustomCallback, callback.Kind);
        Assert.Equal(typeof(ProbeCallback).AssemblyQualifiedName, callback.CallbackTypeName);
        Assert.Equal(new[] { "SpawnDelta", "Marker" },
            callback.CallbackValues.Select(value => value.Name));
        Assert.Equal(2f, Float(callback.CallbackValues[0].Words[0]));
        Assert.Equal(100f, Float(callback.CallbackValues[1].Words[0]));
    }

    [Fact]
    public void RuntimeAssetV9_RejectsCallbackWithoutType()
    {
        VFXRuntimeSpawnerBlockData callback = Callback(10, 2f, 100f) with
        {
            CallbackTypeName = null
        };
        Assert.Throws<InvalidDataException>(() => Data(Program(callback)).Serialize());
    }

    [Fact]
    public void RuntimeAssetV9_RejectsDuplicateCallbackInputNames()
    {
        VFXRuntimeSpawnerBlockData callback = Callback(10, 2f, 100f);
        callback = callback with
        {
            CallbackValues = callback.CallbackValues.Concat(new[]
            {
                new VFXRuntimeSpawnerExpressionValueData(
                    "Marker", VFXRuntimeValueType.Float, new[] { Word(1f) })
            }).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => Data(Program(callback)).Serialize());
    }

    [Fact]
    public void CallbackAfterRate_SeesRateValueAndWritesFinalRecord()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(
            Rate(1, 16f), SetSize(2, -1f), Callback(3, 2f, 100f)));
        Play(effect, device);

        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.05f, device));

        CallbackRecord update = Assert.Single(ProbeCallback.Records.Where(record => record.Method == "OnUpdate"));
        AssertClose(0.8f, update.Before);
        AssertClose(2.8f, update.After);
        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        AssertClose(2.8f, state.spawnCount);
        Assert.Equal(100f, state.vfxEventAttribute.GetFloat("size"));
    }

    [Fact]
    public void CallbackBeforeRate_SeesZeroAndLaterSetOverwritesRecord()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(
            Callback(1, 2f, 200f), Rate(2, 16f), SetSize(3, -1f)));
        Play(effect, device);

        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.05f, device));

        CallbackRecord update = Assert.Single(ProbeCallback.Records.Where(record => record.Method == "OnUpdate"));
        Assert.Equal(0f, update.Before);
        Assert.Equal(2f, update.After);
        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        AssertClose(2.8f, state.spawnCount);
        Assert.Equal(-1f, state.vfxEventAttribute.GetFloat("size"));
    }

    [Fact]
    public void PlayCallback_ReceivesUnityControlSentinelAndLoopingState()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(Callback(1, 2f, 100f)));

        Play(effect, device);

        CallbackRecord play = Assert.Single(ProbeCallback.Records);
        Assert.Equal("OnPlay", play.Method);
        Assert.Equal(1f, play.Before);
        Assert.Equal(VFXSpawnerLoopState.Looping, play.LoopState);
        Assert.True(play.Playing);
        Assert.Equal(0f, play.TotalTime);
    }

    [Fact]
    public void StopCallback_ReceivesSentinelFinishedStateAndPreviousDeltaTime()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(Callback(1, 2f, 100f)));
        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        ProbeCallback.Reset();

        effect.Stop();
        effect.ProcessInputEvents(device);

        CallbackRecord stop = Assert.Single(ProbeCallback.Records);
        Assert.Equal("OnStop", stop.Method);
        Assert.Equal(1f, stop.Before);
        Assert.Equal(VFXSpawnerLoopState.Finished, stop.LoopState);
        Assert.False(stop.Playing);
        AssertClose(0.05f, stop.DeltaTime);
        Assert.Equal(0f, stop.TotalTime);
    }

    [Fact]
    public void CallbackLifecycle_ReceivesUnityEventAttributeDefaultsOnPlayUpdateAndStop()
    {
        DefaultObservingCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(Callback(
            1, 0f, 0f, typeof(DefaultObservingCallback))));

        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.Stop();
        effect.ProcessInputEvents(device);

        Assert.Equal(0.1f, DefaultObservingCallback.PlaySize);
        Assert.Equal(0.1f, DefaultObservingCallback.UpdateSize);
        Assert.Equal(0.1f, DefaultObservingCallback.StopSize);
    }

    [Fact]
    public void FinishedSpawner_StillInvokesUpdateCallbackWithoutRunningRate()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(
            Callback(1, 2f, 100f), Rate(2, 16f), SetSize(3, -1f)));
        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.Stop();
        effect.ProcessInputEvents(device);
        ProbeCallback.Reset();

        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.05f, device));

        CallbackRecord update = Assert.Single(ProbeCallback.Records);
        Assert.Equal("OnUpdate", update.Method);
        Assert.Equal(0f, update.Before);
        Assert.Equal(2f, update.After);
        Assert.False(update.Playing);
        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        Assert.Equal(2f, state.spawnCount);
        Assert.Equal(-1f, state.vfxEventAttribute.GetFloat("size"));
    }

    [Fact]
    public void Replay_ResetsCallbackInstanceLocalState()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(Callback(1, 2f, 100f)));
        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.Stop();
        effect.ProcessInputEvents(device);
        Play(effect, device);
        ProbeCallback.Reset();

        effect.AdvanceSpawnerSystems(0.05f, device);

        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        Assert.Equal(100f, state.vfxEventAttribute.GetFloat("size"));
    }

    [Fact]
    public void EffectsSharingAsset_HaveIndependentCallbackInstances()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect first = Effect(Program(Callback(1, 2f, 100f)), out VisualEffectAsset asset);
        var second = new VisualEffect { visualEffectAsset = asset };
        Play(first, device);
        first.AdvanceSpawnerSystems(0.05f, device);
        first.AdvanceSpawnerSystems(0.05f, device);
        Play(second, device);

        second.AdvanceSpawnerSystems(0.05f, device);

        using VFXSpawnerState firstState = first.GetSpawnSystemInfo("Spawn");
        using VFXSpawnerState secondState = second.GetSpawnSystemInfo("Spawn");
        Assert.Equal(101f, firstState.vfxEventAttribute.GetFloat("size"));
        Assert.Equal(100f, secondState.vfxEventAttribute.GetFloat("size"));
    }

    [Fact]
    public void CallbackException_IsRethrownAsOriginalManagedException()
    {
        ThrowingCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(Callback(
            1, 2f, 100f, typeof(ThrowingCallback))));
        Play(effect, device);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            effect.AdvanceSpawnerSystems(0.05f, device));
        Assert.Equal("callback failure", exception.Message);
    }

    [Fact]
    public void PlayerLoopCallbackException_AbortsFrameAndNextUpdateRecovers()
    {
        DestroyLiveEffects();
        OneShotThrowingCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(Callback(
            1, 2f, 100f, typeof(OneShotThrowingCallback))));
        try
        {
            Play(effect, device);
            ulong effectId = unchecked((ulong)(uint)effect.GetInstanceID());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => UnityRuntime.Tick(0.05f));

            Assert.Equal("one-shot callback failure", exception.Message);
            Assert.Equal(0f, effect.currentTime);
            Assert.False(device.TryGetVFXEffectFrameState(effectId, out _));

            UnityRuntime.Tick(0.05f);

            AssertClose(0.05f, effect.currentTime);
            Assert.True(device.TryGetVFXEffectFrameState(effectId, out var recovered));
            Assert.Equal(0u, recovered.prepared);
            Assert.Equal(2, OneShotThrowingCallback.UpdateCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void ExpressionValues_DecodeAllBlittableUnityCallbackTypes()
    {
        TypedCallback.Reset();
        var matrix = Matrix4x4.identity;
        matrix.m03 = 9f;
        VFXRuntimeSpawnerBlockData callback = Callback(
            1, 0f, 0f, typeof(TypedCallback)) with
        {
            CallbackValues = new VFXRuntimeSpawnerExpressionValueData[]
            {
                Value("Bool", VFXRuntimeValueType.Boolean, 1u),
                Value("Int", VFXRuntimeValueType.Int32, unchecked((uint)-7)),
                Value("UInt", VFXRuntimeValueType.UInt32, 42u),
                Value("Float", VFXRuntimeValueType.Float, Word(1.25f)),
                Value("Float2", VFXRuntimeValueType.Float2, Word(2f), Word(3f)),
                Value("Float3", VFXRuntimeValueType.Float3, Word(4f), Word(5f), Word(6f)),
                Value("Float4", VFXRuntimeValueType.Float4, Word(7f), Word(8f), Word(9f), Word(10f)),
                new("Matrix", VFXRuntimeValueType.Matrix4x4,
                    Enumerable.Range(0, 16).Select(index => Word(matrix[index])).ToArray())
            }
        };
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = Effect(Program(callback));

        Play(effect, device);

        Assert.True(TypedCallback.Bool);
        Assert.Equal(-7, TypedCallback.Int);
        Assert.Equal(42u, TypedCallback.UInt);
        Assert.Equal(1.25f, TypedCallback.Float);
        Assert.Equal(new Vector2(2f, 3f), TypedCallback.Float2);
        Assert.Equal(new Vector3(4f, 5f, 6f), TypedCallback.Float3);
        Assert.Equal(new Vector4(7f, 8f, 9f, 10f), TypedCallback.Float4);
        Assert.Equal(9f, TypedCallback.Matrix.m03);
    }

    [Fact]
    public void RuntimeAssetV9_RoundTripsExposedPropertyCallbackSource()
    {
        VFXRuntimeSpawnerBlockData callback = Callback(1, 2f, 100f);
        callback = callback with
        {
            CallbackValues = callback.CallbackValues.Select(value => value.Name == "SpawnDelta"
                ? value with { SourcePropertyName = "Dynamic Delta" }
                : value).ToArray()
        };
        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(Data(
            Program(callback), Property("Dynamic Delta", VFXRuntimeValueType.Float, Word(3f))).Serialize());

        Assert.Equal("Dynamic Delta", restored.SpawnerPrograms[0].Blocks[0]
            .CallbackValues[0].SourcePropertyName);
        Assert.Equal(3f, Float(Assert.Single(restored.ExposedProperties).DefaultWords[0]));
    }

    [Fact]
    public void RuntimeAssetV11_ReadsRealV9CallbackPayloadWithoutExpressionField()
    {
        VFXRuntimeSpawnerBlockData callback = Callback(1, 2f, 100f) with
        {
            CallbackValues = new[]
            {
                new VFXRuntimeSpawnerExpressionValueData(
                    "SpawnDelta", VFXRuntimeValueType.Float, new[] { Word(2f) })
                {
                    SourcePropertyName = "Dynamic Delta"
                }
            }
        };
        byte[] v10 = Data(
            Program(callback),
            Property("Dynamic Delta", VFXRuntimeValueType.Float, Word(3f))).Serialize();

        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(
            ConvertSingleCallbackV11PayloadToV9(v10));

        VFXRuntimeSpawnerExpressionValueData value = Assert.Single(
            restored.SpawnerPrograms[0].Blocks[0].CallbackValues);
        Assert.Equal("Dynamic Delta", value.SourcePropertyName);
        Assert.Null(value.Expression);
    }

    [Fact]
    public void RuntimeAssetV9_RejectsMissingExposedPropertySource()
    {
        VFXRuntimeSpawnerBlockData callback = DynamicDeltaCallback("Missing");
        Assert.Throws<InvalidDataException>(() => Data(Program(callback)).Serialize());
    }

    [Fact]
    public void RuntimeAssetV9_RejectsMismatchedExposedPropertySourceType()
    {
        VFXRuntimeSpawnerBlockData callback = DynamicDeltaCallback("Dynamic Delta");
        Assert.Throws<InvalidDataException>(() => Data(
            Program(callback), Property("Dynamic Delta", VFXRuntimeValueType.Int32, 3u)).Serialize());
    }

    [Fact]
    public void RuntimeAssetV9_RejectsDuplicateExposedPropertyNames()
    {
        Assert.Throws<InvalidDataException>(() => Data(
            Program(Callback(1, 2f, 100f)),
            Property("Value", VFXRuntimeValueType.Float, Word(1f)),
            Property("Value", VFXRuntimeValueType.Float, Word(2f))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV9_RejectsNonFiniteExposedPropertyDefault()
    {
        Assert.Throws<InvalidDataException>(() => Data(
            Program(Callback(1, 2f, 100f)),
            Property("Value", VFXRuntimeValueType.Float, Word(float.NaN))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV9_ImportsExposedPropertySurfaceAndDefault()
    {
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(Data(
            Program(Callback(1, 2f, 100f)),
            Property("Dynamic Delta", VFXRuntimeValueType.Float, Word(3.5f))).Serialize());
        var properties = new List<VFXExposedProperty>();
        asset.GetExposedProperties(properties);
        var effect = new VisualEffect { visualEffectAsset = asset };

        Assert.Equal("Dynamic Delta", Assert.Single(properties).name);
        Assert.Equal(typeof(float), properties[0].type);
        Assert.True(effect.HasFloat("Dynamic Delta"));
        Assert.Equal(3.5f, effect.GetFloat("Dynamic Delta"));
    }

    [Fact]
    public void DirectExposedPropertyLink_UsesComponentOverride()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = DynamicEffect(3f);
        effect.SetFloat("Dynamic Delta", 7f);
        Play(effect, device);

        effect.AdvanceSpawnerSystems(0.05f, device);

        CallbackRecord update = Assert.Single(ProbeCallback.Records.Where(record => record.Method == "OnUpdate"));
        Assert.Equal(7f, update.After);
    }

    [Fact]
    public void DirectExposedPropertyLink_RefreshesBetweenTicks()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = DynamicEffect(3f);
        Play(effect, device);
        effect.SetFloat("Dynamic Delta", 4f);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.SetFloat("Dynamic Delta", 9f);

        effect.AdvanceSpawnerSystems(0.05f, device);

        Assert.Equal(new[] { 4f, 9f }, ProbeCallback.Records
            .Where(record => record.Method == "OnUpdate").Select(record => record.After));
    }

    [Fact]
    public void DirectExposedPropertyLink_ResetOverrideRestoresAssetDefault()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = DynamicEffect(3f);
        effect.SetFloat("Dynamic Delta", 8f);
        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.ResetOverride("Dynamic Delta");

        effect.AdvanceSpawnerSystems(0.05f, device);

        Assert.Equal(new[] { 8f, 3f }, ProbeCallback.Records
            .Where(record => record.Method == "OnUpdate").Select(record => record.After));
    }

    [Fact]
    public void DirectExposedPropertyLink_IsIsolatedBetweenComponents()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect first = DynamicEffect(3f, out VisualEffectAsset asset);
        var second = new VisualEffect { visualEffectAsset = asset };
        first.SetFloat("Dynamic Delta", 5f);
        second.SetFloat("Dynamic Delta", 11f);
        Play(first, device);
        Play(second, device);

        first.AdvanceSpawnerSystems(0.05f, device);
        second.AdvanceSpawnerSystems(0.05f, device);

        Assert.Contains(ProbeCallback.Records, record => record.Method == "OnUpdate" && record.After == 5f);
        Assert.Contains(ProbeCallback.Records, record => record.Method == "OnUpdate" && record.After == 11f);
    }

    [Fact]
    public void RuntimeAssetV11_RoundTripsTypedExpressionProgram()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Dynamic", VFXRuntimeValueType.Float),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f)),
            BinaryInstruction(VFXRuntimeExpressionOperation.Add, VFXRuntimeValueType.Float, 0, 1));
        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(Data(
            Program(ExpressionCallback(expression)),
            Property("Dynamic", VFXRuntimeValueType.Float, Word(3f))).Serialize());

        VFXRuntimeExpressionProgramData program = Assert.IsType<VFXRuntimeExpressionProgramData>(
            restored.SpawnerPrograms[0].Blocks[0].CallbackValues[0].Expression);
        Assert.Equal(VFXRuntimeExpressionOperation.Add, program.Instructions[2].Operation);
        Assert.Equal(2, program.ResultIndex);
    }

    [Fact]
    public void RuntimeAssetV11_RejectsEmptyExpressionProgram()
    {
        var expression = new VFXRuntimeExpressionProgramData(
            VFXRuntimeValueType.Float, 0, Array.Empty<VFXRuntimeExpressionInstructionData>());
        Assert.Throws<InvalidDataException>(() => Data(
            Program(ExpressionCallback(expression))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV11_RejectsForwardExpressionOperand()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            BinaryInstruction(VFXRuntimeExpressionOperation.Add, VFXRuntimeValueType.Float, 1, 1),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f)));
        Assert.Throws<InvalidDataException>(() => Data(
            Program(ExpressionCallback(expression))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV11_RejectsExpressionResultTypeMismatch()
    {
        var expression = new VFXRuntimeExpressionProgramData(
            VFXRuntimeValueType.Int32, 0,
            new[] { ConstantInstruction(VFXRuntimeValueType.Int32, 2u) });
        Assert.Throws<InvalidDataException>(() => Data(
            Program(ExpressionCallback(expression))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV11_RejectsMissingExpressionProperty()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Missing", VFXRuntimeValueType.Float));
        Assert.Throws<InvalidDataException>(() => Data(
            Program(ExpressionCallback(expression))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV11_RejectsMultipleCallbackRuntimeSources()
    {
        VFXRuntimeSpawnerBlockData callback = ExpressionCallback(FloatExpression(
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f))));
        VFXRuntimeSpawnerExpressionValueData first = callback.CallbackValues[0] with
        {
            SourcePropertyName = "Dynamic"
        };
        callback = callback with { CallbackValues = new[] { first, callback.CallbackValues[1] } };
        Assert.Throws<InvalidDataException>(() => Data(
            Program(callback), Property("Dynamic", VFXRuntimeValueType.Float, Word(3f))).Serialize());
    }

    [Fact]
    public void RuntimeExpression_AddsDynamicPropertyAndConstant()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Dynamic", VFXRuntimeValueType.Float),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f)),
            BinaryInstruction(VFXRuntimeExpressionOperation.Add, VFXRuntimeValueType.Float, 0, 1));
        AssertExpressionResult(expression, 7f, 5f);
    }

    [Fact]
    public void RuntimeExpression_SubtractsInSerializedOperandOrder()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Dynamic", VFXRuntimeValueType.Float),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f)),
            BinaryInstruction(VFXRuntimeExpressionOperation.Subtract, VFXRuntimeValueType.Float, 0, 1));
        AssertExpressionResult(expression, 3f, 5f);
    }

    [Fact]
    public void RuntimeExpression_MultipliesAndRefreshesPropertyBetweenTicks()
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Dynamic", VFXRuntimeValueType.Float),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(3f)),
            BinaryInstruction(VFXRuntimeExpressionOperation.Multiply, VFXRuntimeValueType.Float, 0, 1));
        VisualEffect effect = ExpressionEffect(expression, 2f);
        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        effect.SetFloat("Dynamic", 4f);
        effect.AdvanceSpawnerSystems(0.05f, device);

        Assert.Equal(new[] { 6f, 12f }, ProbeCallback.Records
            .Where(record => record.Method == "OnUpdate").Select(record => record.After));
    }

    [Fact]
    public void RuntimeExpression_OneMinusUsesUnityComponentSemantics()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Dynamic", VFXRuntimeValueType.Float),
            UnaryInstruction(VFXRuntimeExpressionOperation.OneMinus, VFXRuntimeValueType.Float, 0));
        AssertExpressionResult(expression, 0.75f, 0.25f);
    }

    [Fact]
    public void RuntimeExpression_EvaluatesNestedSsaInstructionsInOrder()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            PropertyInstruction("Dynamic", VFXRuntimeValueType.Float),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f)),
            BinaryInstruction(VFXRuntimeExpressionOperation.Add, VFXRuntimeValueType.Float, 0, 1),
            ConstantInstruction(VFXRuntimeValueType.Float, Word(3f)),
            BinaryInstruction(VFXRuntimeExpressionOperation.Multiply, VFXRuntimeValueType.Float, 2, 3));
        AssertExpressionResult(expression, 21f, 5f);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    public void RuntimeBuiltInFloat_UsesLiveUnityAndVfxManagerValues(
        int operationValue)
    {
        var operation = (VFXRuntimeExpressionOperation)operationValue;
        float previousFixed = VFXManager.fixedTimeStep;
        float previousMax = VFXManager.maxDeltaTime;
        try
        {
            VFXManager.fixedTimeStep = 0.03125f;
            VFXManager.maxDeltaTime = 0.125f;
            FloatValueCallback.Reset();
            using NativeGraphicsDevice device = Device();
            if (device.Handle == IntPtr.Zero) return;
            VisualEffect effect = TypedExpressionEffect(
                BuiltInExpression(operation, VFXRuntimeValueType.Float),
                typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
            effect.playRate = 1.75f;
            float expected = ExpectedFloatBuiltIn(operation, effect);
            Play(effect, device);
            FloatValueCallback.Reset();

            effect.AdvanceSpawnerSystems(0.05f, device);

            AssertClose(expected, Assert.Single(FloatValueCallback.Values));
        }
        finally
        {
            VFXManager.fixedTimeStep = previousFixed;
            VFXManager.maxDeltaTime = previousMax;
        }
    }

    [Fact]
    public void RuntimeBuiltInVfxDeltaTime_UsesCallbackSchedulerDelta()
    {
        AssertFloatBuiltIn(VFXRuntimeExpressionOperation.VfxDeltaTime, 0.08f, 2f, 0.08f);
    }

    [Fact]
    public void RuntimeBuiltInUnscaledDeltaTime_RemovesPlayRateScale()
    {
        AssertFloatBuiltIn(VFXRuntimeExpressionOperation.VfxUnscaledDeltaTime, 0.04f, 2f, 0.08f);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 2)]
    public void RuntimeBuiltInVfxDeltaTime_UsesOfficialFixedStepAccumulator(
        int frameIndex,
        int expectedStepCount)
    {
        float previousFixed = VFXManager.fixedTimeStep;
        float previousMax = VFXManager.maxDeltaTime;
        try
        {
            VFXManager.fixedTimeStep = 1f / 60f;
            VFXManager.maxDeltaTime = 0.05f;
            var effect = new VisualEffect { playRate = 1.75f };
            float actual = 0f;
            for (int index = 0; index <= frameIndex; index++)
                actual = effect.PrepareVfxFrame(index == 0 ? 0.0625f : 0.03125f,
                    unchecked((uint)(100 + index)));

            AssertClose(expectedStepCount * VFXManager.fixedTimeStep * 1.75f, actual);
        }
        finally
        {
            VFXManager.fixedTimeStep = previousFixed;
            VFXManager.maxDeltaTime = previousMax;
        }
    }

    [Fact]
    public void RuntimeBuiltInVfxDeltaTime_OnPlayUsesPreparedFrameInsteadOfZeroStateDelta()
    {
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxDeltaTime, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        effect.playRate = 1.75f;
        float expected = effect.PrepareVfxFrame(0.0625f, 545u);

        Play(effect, device);

        AssertClose(expected, Assert.Single(FloatValueCallback.Values));
        AssertClose(0.0875f, expected);
    }

    [Fact]
    public void RuntimeBuiltInVfxDeltaTime_OnStopUsesCurrentPreparedFrame()
    {
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxDeltaTime, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        effect.playRate = 1.75f;
        effect.PrepareVfxFrame(0.0625f, 545u);
        Play(effect, device);
        FloatValueCallback.Reset();
        float expected = effect.PrepareVfxFrame(0.03125f, 546u);

        effect.Stop();
        effect.ProcessInputEvents(device);

        AssertClose(expected, Assert.Single(FloatValueCallback.Values));
        AssertClose(2f * VFXManager.fixedTimeStep * 1.75f, expected);
    }

    [Fact]
    public void RuntimeBuiltInTotalTime_AccumulatesPreparedVfxDelta()
    {
        var effect = new VisualEffect { playRate = 1.75f };
        float first = effect.PrepareVfxFrame(0.0625f, 545u);
        effect.CompleteVfxFrame();
        float second = effect.PrepareVfxFrame(0.03125f, 546u);
        effect.CompleteVfxFrame();

        AssertClose(first + second, effect.currentTime);
        AssertClose(0.14583334f, effect.currentTime);
    }

    [Fact]
    public void RuntimeBuiltInTotalTime_UsesVisualEffectComponentClock()
    {
        DestroyLiveEffects();
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxTotalTime, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        try
        {
            effect.pause = true;
            effect.Simulate(1.25f, 2);
            Play(effect, device);
            UnityRuntime.Tick(0.05f);
            FloatValueCallback.Reset();

            effect.AdvanceSpawnerSystems(0.05f, device);

            AssertClose(2.5f, Assert.Single(FloatValueCallback.Values));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void RuntimeBuiltInFrameIndex_UsesIndependentVfxManagerFrameIndex()
    {
        UIntValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxFrameIndex, VFXRuntimeValueType.UInt32),
            typeof(UIntValueCallback), "Value", VFXRuntimeValueType.UInt32, 0u);
        Play(effect, device);
        UIntValueCallback.Reset();

        effect.AdvanceSpawnerSystems(0.05f, device);

        Assert.Equal(effect.currentVfxFrameIndex, Assert.Single(UIntValueCallback.Values));
    }

    [Fact]
    public void RuntimeBuiltInVfxDelta_OnPlayReadsNativePreparedFrame()
    {
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxDeltaTime, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        effect.playRate = 1.75f;

        Assert.True(device.BeginVFXFrame(out uint frame));
        AssertClose(0.0875f, effect.PrepareVfxFrame(0.0625f, frame, device));
        Play(effect, device);

        AssertClose(0.0875f, Assert.Single(FloatValueCallback.Values));
        effect.CompleteVfxFrame(device);
    }

    [Fact]
    public void RuntimePlayerLoop_PausedSpawnerStillRunsOnUpdateWithZeroDelta()
    {
        DestroyLiveEffects();
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxDeltaTime, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        Play(effect, device);
        FloatValueCallback.Reset();
        float oldFixed = VFXManager.fixedTimeStep;
        float oldMaximum = VFXManager.maxDeltaTime;
        float oldScale = Time.timeScale;
        try
        {
            VFXManager.fixedTimeStep = 1f / 60f;
            VFXManager.maxDeltaTime = 0.05f;
            Time.timeScale = 1f;
            effect.pause = true;

            UnityRuntime.Tick(0.05f);

            AssertClose(0f, Assert.Single(FloatValueCallback.Values));
            AssertClose(0f, effect.currentVfxDeltaTime);
            AssertClose(0f, effect.currentTime);
        }
        finally
        {
            VFXManager.fixedTimeStep = oldFixed;
            VFXManager.maxDeltaTime = oldMaximum;
            Time.timeScale = oldScale;
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void RuntimePlayerLoop_CulledSpawnerFreezesUntilVisibleAgain()
    {
        DestroyLiveEffects();
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.VfxDeltaTime, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        effect.visualEffectAsset!.DefineParticleSystem(
            "Particles",
            new VFXParticleSystemInfo(0, 64, false,
                new Bounds(new Vector3(4f, 0f, 0f), Vector3.one)));
        var camera = new Camera
        {
            projectionMatrix = Matrix4x4.identity,
            worldToCameraMatrix = Matrix4x4.identity
        };
        RenderPipeline? previousPipeline = RenderPipelineManager.currentPipeline;
        RenderPipelineManager.SetCurrentPipeline(new CallbackCullingRenderPipeline());
        Play(effect, device);
        FloatValueCallback.Reset();
        float oldFixed = VFXManager.fixedTimeStep;
        float oldMaximum = VFXManager.maxDeltaTime;
        float oldScale = Time.timeScale;
        try
        {
            VFXManager.fixedTimeStep = 1f / 60f;
            VFXManager.maxDeltaTime = 0.05f;
            Time.timeScale = 1f;
            UnityRuntime.Tick(0.05f);
            AssertClose(0.05f, Assert.Single(FloatValueCallback.Values));
            Assert.True(effect.culled);
            FloatValueCallback.Reset();
            uint beforeFrame = effect.currentVfxFrameIndex;

            UnityRuntime.Tick(0.05f);

            Assert.Empty(FloatValueCallback.Values);
            Assert.Equal(beforeFrame, effect.currentVfxFrameIndex);
            AssertClose(0.05f, effect.currentTime);

            effect.visualEffectAsset.DefineParticleSystem(
                "Particles",
                new VFXParticleSystemInfo(0, 64, false,
                    new Bounds(Vector3.zero, Vector3.one)));
            UnityRuntime.Tick(0.05f);
            Assert.Empty(FloatValueCallback.Values);
            Assert.False(effect.culled);

            UnityRuntime.Tick(0.05f);
            AssertClose(0.05f, Assert.Single(FloatValueCallback.Values));
            AssertClose(0.1f, effect.currentTime);
        }
        finally
        {
            VFXManager.fixedTimeStep = oldFixed;
            VFXManager.maxDeltaTime = oldMaximum;
            Time.timeScale = oldScale;
            UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(camera);
            RenderPipelineManager.SetCurrentPipeline(previousPipeline);
        }
    }

    [Fact]
    public void RuntimeBuiltInSystemSeed_IsVisibleDuringSynchronousOnPlayCallback()
    {
        UIntValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(VFXRuntimeExpressionOperation.SystemSeed, VFXRuntimeValueType.UInt32),
            typeof(UIntValueCallback), "Value", VFXRuntimeValueType.UInt32, 0u);
        effect.startSeed = 0xf1234567u;
        effect.resetSeedOnPlay = false;

        Play(effect, device);

        Assert.Equal(0xf1234567u, Assert.Single(UIntValueCallback.Values));
    }

    [Theory]
    [InlineData(21)]
    [InlineData(22)]
    public void RuntimeBuiltInTransformMatrices_UseComponentTransform(
        int operationValue)
    {
        var operation = (VFXRuntimeExpressionOperation)operationValue;
        MatrixValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(operation, VFXRuntimeValueType.Matrix4x4),
            typeof(MatrixValueCallback), "Value", VFXRuntimeValueType.Matrix4x4,
            Enumerable.Range(0, 16).Select(index => Word(Matrix4x4.identity[index])).ToArray());
        effect.transform.position = new Vector3(3f, -2f, 5f);
        effect.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
        effect.transform.localScale = new Vector3(2f, 3f, 4f);
        Matrix4x4 expected = operation == VFXRuntimeExpressionOperation.LocalToWorld
            ? effect.transform.localToWorldMatrix
            : effect.transform.worldToLocalMatrix;
        Play(effect, device);
        MatrixValueCallback.Reset();

        effect.AdvanceSpawnerSystems(0.05f, device);

        AssertMatrixClose(expected, Assert.Single(MatrixValueCallback.Values));
    }

    [Theory]
    [InlineData(7, 2)]
    [InlineData(23, 4)]
    [InlineData(21, 4)]
    public void RuntimeAssetV11_RejectsBuiltInWithWrongType(
        int operationValue,
        int typeValue)
    {
        var operation = (VFXRuntimeExpressionOperation)operationValue;
        var type = (VFXRuntimeValueType)typeValue;
        var expression = new VFXRuntimeExpressionProgramData(type, 0, new[]
        {
            BuiltInInstruction(operation, type)
        });
        Assert.Throws<InvalidDataException>(() => Data(
            Program(TypedExpressionCallback(expression, typeof(FloatValueCallback),
                "Value", type, type == VFXRuntimeValueType.Float ? new[] { Word(0f) } : new[] { 0u }))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV11_RejectsBuiltInWithOperand()
    {
        var expression = new VFXRuntimeExpressionProgramData(
            VFXRuntimeValueType.Float, 1, new[]
            {
                ConstantInstruction(VFXRuntimeValueType.Float, Word(1f)),
                new VFXRuntimeExpressionInstructionData(
                    VFXRuntimeExpressionOperation.GameDeltaTime,
                    VFXRuntimeValueType.Float, 0, -1, Array.Empty<uint>(), null)
            });
        Assert.Throws<InvalidDataException>(() => Data(
            Program(ExpressionCallback(expression))).Serialize());
    }

    [Fact]
    public void RuntimeAssetV11_ReadsV10ExpressionPayload()
    {
        VFXRuntimeExpressionProgramData expression = FloatExpression(
            ConstantInstruction(VFXRuntimeValueType.Float, Word(2f)));
        byte[] v11 = ConvertV14SystemLayout(
            Data(Program(ExpressionCallback(expression))).Serialize(), 10u);

        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(v11);

        Assert.Equal(VFXRuntimeExpressionOperation.Constant,
            restored.SpawnerPrograms[0].Blocks[0].CallbackValues[0]
                .Expression!.Instructions[0].Operation);
    }

    private static VFXRuntimeSpawnerExpressionValueData Value(
        string name, VFXRuntimeValueType type, params uint[] words)
        => new(name, type, words);

    private static VFXRuntimeExposedPropertyData Property(
        string name, VFXRuntimeValueType type, params uint[] words)
        => new(name, type, words);

    private static VFXRuntimeSpawnerBlockData DynamicDeltaCallback(string propertyName)
    {
        VFXRuntimeSpawnerBlockData callback = Callback(1, 2f, 100f);
        return callback with
        {
            CallbackValues = callback.CallbackValues.Select(value => value.Name == "SpawnDelta"
                ? value with { SourcePropertyName = propertyName }
                : value).ToArray()
        };
    }

    private static VFXRuntimeSpawnerBlockData ExpressionCallback(
        VFXRuntimeExpressionProgramData expression)
    {
        VFXRuntimeSpawnerBlockData callback = Callback(1, 0f, 100f);
        return callback with
        {
            CallbackValues = new[]
            {
                callback.CallbackValues[0] with { Expression = expression },
                callback.CallbackValues[1]
            }
        };
    }

    private static VFXRuntimeSpawnerBlockData TypedExpressionCallback(
        VFXRuntimeExpressionProgramData expression,
        Type callbackType,
        string name,
        VFXRuntimeValueType type,
        IReadOnlyList<uint> words)
        => new(1, VFXRuntimeSpawnerBlockKind.CustomCallback, 0f, 0f, 0f, 0f, false)
        {
            CallbackTypeName = callbackType.AssemblyQualifiedName,
            CallbackValues = new[]
            {
                new VFXRuntimeSpawnerExpressionValueData(name, type, words)
                {
                    Expression = expression
                }
            }
        };

    private static VFXRuntimeExpressionProgramData FloatExpression(
        params VFXRuntimeExpressionInstructionData[] instructions)
        => new(VFXRuntimeValueType.Float, instructions.Length - 1, instructions);

    private static VFXRuntimeExpressionInstructionData ConstantInstruction(
        VFXRuntimeValueType type,
        params uint[] words)
        => new(VFXRuntimeExpressionOperation.Constant, type, -1, -1, words, null);

    private static VFXRuntimeExpressionInstructionData PropertyInstruction(
        string name,
        VFXRuntimeValueType type)
        => new(VFXRuntimeExpressionOperation.ExposedProperty, type, -1, -1, Array.Empty<uint>(), name);

    private static VFXRuntimeExpressionInstructionData BinaryInstruction(
        VFXRuntimeExpressionOperation operation,
        VFXRuntimeValueType type,
        int inputA,
        int inputB)
        => new(operation, type, inputA, inputB, Array.Empty<uint>(), null);

    private static VFXRuntimeExpressionInstructionData UnaryInstruction(
        VFXRuntimeExpressionOperation operation,
        VFXRuntimeValueType type,
        int input)
        => new(operation, type, input, -1, Array.Empty<uint>(), null);

    private static VFXRuntimeExpressionInstructionData BuiltInInstruction(
        VFXRuntimeExpressionOperation operation,
        VFXRuntimeValueType type)
        => new(operation, type, -1, -1, Array.Empty<uint>(), null);

    private static VFXRuntimeExpressionProgramData BuiltInExpression(
        VFXRuntimeExpressionOperation operation,
        VFXRuntimeValueType type)
        => new(type, 0, new[] { BuiltInInstruction(operation, type) });

    private static VisualEffect ExpressionEffect(
        VFXRuntimeExpressionProgramData expression,
        float defaultValue)
    {
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(Data(
            Program(ExpressionCallback(expression)),
            Property("Dynamic", VFXRuntimeValueType.Float, Word(defaultValue))).Serialize());
        return new VisualEffect { visualEffectAsset = asset };
    }

    private static VisualEffect TypedExpressionEffect(
        VFXRuntimeExpressionProgramData expression,
        Type callbackType,
        string name,
        VFXRuntimeValueType type,
        params uint[] words)
    {
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(Data(Program(TypedExpressionCallback(
            expression, callbackType, name, type, words))).Serialize());
        var gameObject = new GameObject("VFX Built-In Expression Test");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        return effect;
    }

    private static void AssertFloatBuiltIn(
        VFXRuntimeExpressionOperation operation,
        float expected,
        float playRate,
        float schedulerDelta)
    {
        FloatValueCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = TypedExpressionEffect(
            BuiltInExpression(operation, VFXRuntimeValueType.Float),
            typeof(FloatValueCallback), "Value", VFXRuntimeValueType.Float, Word(0f));
        effect.playRate = playRate;
        Play(effect, device);
        FloatValueCallback.Reset();
        effect.AdvanceSpawnerSystems(schedulerDelta, device);
        AssertClose(expected, Assert.Single(FloatValueCallback.Values));
    }

    private static float ExpectedFloatBuiltIn(
        VFXRuntimeExpressionOperation operation,
        VisualEffect effect)
        => operation switch
        {
            VFXRuntimeExpressionOperation.VfxPlayRate => effect.playRate,
            VFXRuntimeExpressionOperation.VfxManagerFixedTimeStep => VFXManager.fixedTimeStep,
            VFXRuntimeExpressionOperation.VfxManagerMaxDeltaTime => VFXManager.maxDeltaTime,
            VFXRuntimeExpressionOperation.GameDeltaTime => Time.deltaTime,
            VFXRuntimeExpressionOperation.GameUnscaledDeltaTime => Time.unscaledDeltaTime,
            VFXRuntimeExpressionOperation.GameSmoothDeltaTime => Time.smoothDeltaTime,
            VFXRuntimeExpressionOperation.GameTotalTime => Time.time,
            VFXRuntimeExpressionOperation.GameUnscaledTotalTime => Time.unscaledTime,
            VFXRuntimeExpressionOperation.GameTotalTimeSinceSceneLoad => Time.timeSinceLevelLoad,
            VFXRuntimeExpressionOperation.GameTimeScale => Time.timeScale,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static void AssertExpressionResult(
        VFXRuntimeExpressionProgramData expression,
        float expected,
        float propertyValue)
    {
        ProbeCallback.Reset();
        using NativeGraphicsDevice device = Device();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = ExpressionEffect(expression, propertyValue);
        Play(effect, device);
        effect.AdvanceSpawnerSystems(0.05f, device);
        CallbackRecord update = Assert.Single(ProbeCallback.Records.Where(record => record.Method == "OnUpdate"));
        AssertClose(expected, update.After);
    }

    private static VFXRuntimeSpawnerBlockData Callback(
        long id, float delta, float marker, Type? type = null)
        => new(id, VFXRuntimeSpawnerBlockKind.CustomCallback, 0f, 0f, 0f, 0f, false)
        {
            CallbackTypeName = (type ?? typeof(ProbeCallback)).AssemblyQualifiedName,
            CallbackValues = new[]
            {
                Value("SpawnDelta", VFXRuntimeValueType.Float, Word(delta)),
                Value("Marker", VFXRuntimeValueType.Float, Word(marker))
            }
        };

    private static VFXRuntimeSpawnerBlockData Rate(long id, float value)
        => new(id, VFXRuntimeSpawnerBlockKind.ConstantRate, value, value, 0f, 0f, false);

    private static VFXRuntimeSpawnerBlockData SetSize(long id, float value)
        => new(id, VFXRuntimeSpawnerBlockKind.SetAttribute, 0f, 0f, 0f, 0f, false)
        {
            TargetOffsetWords = 1,
            TargetValueType = VFXRuntimeValueType.Float,
            ValueA = new[] { Word(value) },
            ValueB = new[] { Word(value) }
        };

    private static VFXRuntimeSpawnerProgramData Program(
        params VFXRuntimeSpawnerBlockData[] blocks)
        => new(
            100, "Spawn",
            VFXRuntimeSpawnerValueMode.Infinite,
            VFXRuntimeSpawnerValueMode.Infinite,
            VFXRuntimeSpawnerValueMode.Disabled,
            VFXRuntimeSpawnerValueMode.Disabled,
            0f, 0f, 0d, 0d, 0f, 0f, 0f, 0f,
            new[]
            {
                new VFXRuntimeSpawnerControlData(VisualEffectAsset.PlayEventName, 0),
                new VFXRuntimeSpawnerControlData(VisualEffectAsset.StopEventName, 1)
            },
            new[] { new VFXRuntimeSpawnerOutputData(200, "Particles", null) },
            blocks)
        {
            EventStrideWords = 2
        };

    private static VFXRuntimeAssetData Data(
        VFXRuntimeSpawnerProgramData program,
        params VFXRuntimeExposedPropertyData[] exposedProperties)
        => new(
            new[]
            {
                new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1),
                new VFXRuntimeAttributeData("size", VFXRuntimeValueType.Float, 1, 1)
            },
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            new[]
            {
                new VFXRuntimeSystemData("Spawn", VFXRuntimeSystemKind.Spawn, 0),
                new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 64)
            },
            Array.Empty<VFXRuntimeOutputEventData>())
        {
            SpawnerPrograms = new[] { program },
            ExposedProperties = exposedProperties
        };

    private static VisualEffect DynamicEffect(float defaultValue)
        => DynamicEffect(defaultValue, out _);

    private static VisualEffect DynamicEffect(float defaultValue, out VisualEffectAsset asset)
    {
        asset = new VisualEffectAsset();
        asset.ImportRuntimeData(Data(
            Program(DynamicDeltaCallback("Dynamic Delta")),
            Property("Dynamic Delta", VFXRuntimeValueType.Float, Word(defaultValue))).Serialize());
        return new VisualEffect { visualEffectAsset = asset };
    }

    private static VisualEffect Effect(VFXRuntimeSpawnerProgramData program)
        => Effect(program, out _);

    private static VisualEffect Effect(
        VFXRuntimeSpawnerProgramData program,
        out VisualEffectAsset asset)
    {
        asset = new VisualEffectAsset();
        asset.ImportRuntimeData(Data(program).Serialize());
        return new VisualEffect { visualEffectAsset = asset };
    }

    private static void DestroyLiveEffects()
    {
        foreach (VisualEffect effect in VFXManager.GetComponents())
            UnityEngine.Object.DestroyImmediate(effect);
    }

    private static NativeGraphicsDevice Device()
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static void Play(VisualEffect effect, NativeGraphicsDevice device)
    {
        effect.Play();
        Assert.Equal(1, effect.ProcessInputEvents(device));
    }

    private static byte[] ConvertSingleCallbackV11PayloadToV9(byte[] v10)
    {
        v10 = ConvertV14SystemLayout(v10, 11u);
        Assert.Equal(11u, BinaryPrimitives.ReadUInt32LittleEndian(v10.AsSpan(4, 4)));
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(v10.AsSpan(8, 4));
        byte[] payload = v10.AsSpan(12, payloadLength).ToArray();
        Assert.Equal(0, payload[^1]);
        Array.Resize(ref payload, payload.Length - 1);
        byte[] hash = SHA256.HashData(payload);
        using var stream = new MemoryStream(12 + payload.Length + hash.Length);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(BinaryPrimitives.ReadUInt32LittleEndian(v10.AsSpan(0, 4)));
        writer.Write(9u);
        writer.Write(payload.Length);
        writer.Write(payload);
        writer.Write(hash);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] ConvertV14SystemLayout(byte[] source, uint targetVersion)
    {
        Assert.Equal(15u, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(4, 4)));
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(8, 4));
        byte[] payload = source.AsSpan(12, payloadLength).ToArray();
        using var readerStream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(readerStream, System.Text.Encoding.UTF8, leaveOpen: true);
        int attributeCount = reader.ReadInt32();
        for (int index = 0; index < attributeCount; index++)
        {
            ReadPayloadString(reader);
            reader.ReadByte();
            reader.ReadInt32();
            reader.ReadInt32();
        }
        int eventCount = reader.ReadInt32();
        for (int index = 0; index < eventCount; index++) ReadPayloadString(reader);
        Assert.Equal(0, reader.ReadInt32());
        int systemCountOffset = checked((int)readerStream.Position);
        int systemCount = reader.ReadInt32();
        const int v13BoundsMetadataByteCount = 63;
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(
            payload.AsSpan(payload.Length - 2 * sizeof(int), sizeof(int))));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(
            payload.AsSpan(payload.Length - sizeof(int), sizeof(int))));
        using var downgraded = new MemoryStream(
            payload.Length - systemCount * v13BoundsMetadataByteCount - 2 * sizeof(int));
        downgraded.Write(payload, 0, systemCountOffset + sizeof(int));
        for (int index = 0; index < systemCount; index++)
        {
            int start = checked((int)readerStream.Position);
            ReadPayloadString(reader);
            reader.ReadByte();
            reader.ReadUInt32();
            int end = checked((int)readerStream.Position);
            downgraded.Write(payload, start, end - start);
            readerStream.Position += v13BoundsMetadataByteCount;
        }
        int remainder = checked((int)readerStream.Position);
        downgraded.Write(payload, remainder, payload.Length - remainder - 2 * sizeof(int));
        return RebuildRuntimeEnvelope(source, downgraded.ToArray(), targetVersion);
    }

    private static byte[] RebuildRuntimeEnvelope(byte[] source, byte[] payload, uint version)
    {
        byte[] hash = SHA256.HashData(payload);
        using var stream = new MemoryStream(12 + payload.Length + hash.Length);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0, 4)));
        writer.Write(version);
        writer.Write(payload.Length);
        writer.Write(payload);
        writer.Write(hash);
        writer.Flush();
        return stream.ToArray();
    }

    private static string ReadPayloadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static uint Word(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static float Float(uint word)
        => BitConverter.Int32BitsToSingle(unchecked((int)word));

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(Math.Abs(expected - actual), 0f, 0.00001f);

    private static void AssertMatrixClose(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (int index = 0; index < 16; index++)
            Assert.InRange(Math.Abs(expected[index] - actual[index]), 0f, 0.0001f);
    }

    public sealed class ProbeCallback : VFXSpawnerCallbacks
    {
        private static readonly ConcurrentQueue<CallbackRecord> Log = new();
        private int _updateIndex;

        internal static IReadOnlyList<CallbackRecord> Records => Log.ToArray();

        internal static void Reset()
        {
            while (Log.TryDequeue(out _))
            {
            }
        }

        public override void OnPlay(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
            _updateIndex = 0;
            Record("OnPlay", state, state.spawnCount);
        }

        public override void OnUpdate(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
            float before = state.spawnCount;
            state.spawnCount += values.GetFloat("SpawnDelta");
            state.vfxEventAttribute.SetFloat("size", values.GetFloat("Marker") + _updateIndex++);
            Record("OnUpdate", state, before);
        }

        public override void OnStop(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Record("OnStop", state, state.spawnCount);

        private static void Record(string method, VFXSpawnerState state, float before)
            => Log.Enqueue(new CallbackRecord(
                method, before, state.spawnCount, state.playing, state.loopState,
                state.deltaTime, state.totalTime));
    }

    public sealed class FloatValueCallback : VFXSpawnerCallbacks
    {
        private static readonly ConcurrentQueue<float> Log = new();
        internal static IReadOnlyList<float> Values => Log.ToArray();
        internal static void Reset()
        {
            while (Log.TryDequeue(out _))
            {
            }
        }
        public override void OnPlay(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetFloat("Value"));
        public override void OnUpdate(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetFloat("Value"));
        public override void OnStop(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetFloat("Value"));
    }

    private sealed class CallbackCullingRenderPipeline : RenderPipeline
    {
        protected override void ExecuteRender(ScriptableRenderContext context, Camera camera)
        {
        }
    }

    public sealed class UIntValueCallback : VFXSpawnerCallbacks
    {
        private static readonly ConcurrentQueue<uint> Log = new();
        internal static IReadOnlyList<uint> Values => Log.ToArray();
        internal static void Reset()
        {
            while (Log.TryDequeue(out _))
            {
            }
        }
        public override void OnPlay(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetUInt("Value"));
        public override void OnUpdate(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetUInt("Value"));
        public override void OnStop(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetUInt("Value"));
    }

    public sealed class MatrixValueCallback : VFXSpawnerCallbacks
    {
        private static readonly ConcurrentQueue<Matrix4x4> Log = new();
        internal static IReadOnlyList<Matrix4x4> Values => Log.ToArray();
        internal static void Reset()
        {
            while (Log.TryDequeue(out _))
            {
            }
        }
        public override void OnPlay(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetMatrix4x4("Value"));
        public override void OnUpdate(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetMatrix4x4("Value"));
        public override void OnStop(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => Log.Enqueue(values.GetMatrix4x4("Value"));
    }

    public sealed class ThrowingCallback : VFXSpawnerCallbacks
    {
        internal static void Reset()
        {
        }

        public override void OnPlay(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
        }

        public override void OnUpdate(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => throw new InvalidOperationException("callback failure");

        public override void OnStop(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
        }
    }

    public sealed class OneShotThrowingCallback : VFXSpawnerCallbacks
    {
        private static int _updateCount;
        internal static int UpdateCount => Volatile.Read(ref _updateCount);
        internal static void Reset() => Volatile.Write(ref _updateCount, 0);

        public override void OnPlay(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
        }

        public override void OnUpdate(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
            if (Interlocked.Increment(ref _updateCount) == 1)
                throw new InvalidOperationException("one-shot callback failure");
        }

        public override void OnStop(
            VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
        }
    }

    public sealed class TypedCallback : VFXSpawnerCallbacks
    {
        internal static bool Bool;
        internal static int Int;
        internal static uint UInt;
        internal static float Float;
        internal static Vector2 Float2;
        internal static Vector3 Float3;
        internal static Vector4 Float4;
        internal static Matrix4x4 Matrix;

        internal static void Reset()
        {
            Bool = false;
            Int = 0;
            UInt = 0;
            Float = 0f;
            Float2 = default;
            Float3 = default;
            Float4 = default;
            Matrix = default;
        }

        public override void OnPlay(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
            Bool = values.GetBool("Bool");
            Int = values.GetInt("Int");
            UInt = values.GetUInt("UInt");
            Float = values.GetFloat("Float");
            Float2 = values.GetVector2("Float2");
            Float3 = values.GetVector3("Float3");
            Float4 = values.GetVector4("Float4");
            Matrix = values.GetMatrix4x4("Matrix");
        }

        public override void OnUpdate(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
        }

        public override void OnStop(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
        {
        }
    }

    public sealed class DefaultObservingCallback : VFXSpawnerCallbacks
    {
        internal static float PlaySize;
        internal static float UpdateSize;
        internal static float StopSize;

        internal static void Reset()
        {
            PlaySize = float.NaN;
            UpdateSize = float.NaN;
            StopSize = float.NaN;
        }

        public override void OnPlay(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => PlaySize = state.vfxEventAttribute.GetFloat("size");

        public override void OnUpdate(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => UpdateSize = state.vfxEventAttribute.GetFloat("size");

        public override void OnStop(VFXSpawnerState state, VFXExpressionValues values, VisualEffect component)
            => StopSize = state.vfxEventAttribute.GetFloat("size");
    }

    internal readonly record struct CallbackRecord(
        string Method,
        float Before,
        float After,
        bool Playing,
        VFXSpawnerLoopState LoopState,
        float DeltaTime,
        float TotalTime);
}
