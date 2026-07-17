using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class VFXSpawnerRuntimeTests
{
    private static long s_nextNativeEffectId = 10_000;

    public static TheoryData<int, VFXSpawnerLoopState, int, bool, bool, float>
        UnityConstantFiniteTimeline => new()
        {
            { 0, VFXSpawnerLoopState.DelayingBeforeLoop, 0, true, false, 0.05f },
            { 1, VFXSpawnerLoopState.DelayingBeforeLoop, 0, false, false, 0.10f },
            { 2, VFXSpawnerLoopState.Looping, 0, false, true, 0f },
            { 3, VFXSpawnerLoopState.Looping, 0, false, true, 0.05f },
            { 4, VFXSpawnerLoopState.Looping, 0, false, true, 0.10f },
            { 5, VFXSpawnerLoopState.Looping, 0, false, true, 0.15f },
            { 6, VFXSpawnerLoopState.Looping, 0, false, true, 0.20f },
            { 7, VFXSpawnerLoopState.DelayingAfterLoop, 0, false, false, 0f },
            { 8, VFXSpawnerLoopState.DelayingAfterLoop, 0, false, false, 0.05f },
            { 9, VFXSpawnerLoopState.DelayingAfterLoop, 0, false, false, 0.10f },
            { 10, VFXSpawnerLoopState.DelayingBeforeLoop, 1, false, false, 0f },
            { 11, VFXSpawnerLoopState.DelayingBeforeLoop, 1, true, false, 0.05f },
            { 12, VFXSpawnerLoopState.DelayingBeforeLoop, 1, false, false, 0.10f },
            { 13, VFXSpawnerLoopState.Looping, 1, false, true, 0f },
            { 14, VFXSpawnerLoopState.Looping, 1, false, true, 0.05f },
            { 15, VFXSpawnerLoopState.Looping, 1, false, true, 0.10f },
            { 16, VFXSpawnerLoopState.Looping, 1, false, true, 0.15f },
            { 17, VFXSpawnerLoopState.Looping, 1, false, true, 0.20f },
            { 18, VFXSpawnerLoopState.DelayingAfterLoop, 1, false, false, 0f },
            { 19, VFXSpawnerLoopState.DelayingAfterLoop, 1, false, false, 0.05f },
            { 20, VFXSpawnerLoopState.DelayingAfterLoop, 1, false, false, 0.10f },
            { 21, VFXSpawnerLoopState.Finished, 2, false, false, 0f },
            { 22, VFXSpawnerLoopState.Finished, 2, false, false, 0.05f },
            { 23, VFXSpawnerLoopState.Finished, 2, false, false, 0.10f },
            { 24, VFXSpawnerLoopState.Finished, 2, false, false, 0.15f }
        };

    [Fact]
    public void NativeSpawnerAbi_HasStableLayout()
    {
        Assert.Equal(96, Marshal.SizeOf<AnityNative.GraphicsVFXSpawnerProgramDesc>());
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXSpawnerBlockDesc>());
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXSpawnerState>());
    }

    [Fact]
    public void VFXManager_DefaultTimingMatchesUnity2022PlayerEvidence()
    {
        Assert.InRange(Math.Abs(VFXManager.fixedTimeStep - 1f / 60f), 0f, 0.000001f);
        Assert.InRange(Math.Abs(VFXManager.maxDeltaTime - 0.05f), 0f, 0.000001f);
    }

    [Fact]
    public void NativeProgram_InstallsFinished()
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(10f) }));
        if (native is null) return;
        Assert.Equal(VFXSpawnerLoopState.Finished, (VFXSpawnerLoopState)native.State.loopState);
        Assert.Equal(0, native.State.loopCount);
        Assert.Equal(0f, native.State.loopDuration);
    }

    [Fact]
    public void NativeStart_EntersInfiniteLoop()
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(10f) }));
        if (native is null) return;
        native.Play(12);
        Assert.Equal(VFXSpawnerLoopState.Looping, (VFXSpawnerLoopState)native.State.loopState);
        Assert.Equal(0, native.State.loopIndex);
        Assert.Equal(-1, native.State.loopCount);
        Assert.Equal(-1f, native.State.loopDuration);
        Assert.True(native.State.newLoop != 0);
        Assert.True(native.Tick(0.1f).newLoop != 0);
        Assert.Equal(0, native.Tick(0.1f).newLoop);
    }

    [Fact]
    public void NativeConstantRate_PreservesFractionalDebt()
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(2.5f) }));
        if (native is null) return;
        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState first = native.Tick(0.2f);
        AnityNative.GraphicsVFXSpawnerState second = native.Tick(0.2f);
        AnityNative.GraphicsVFXSpawnerState third = native.Tick(0.2f);
        AnityNative.GraphicsVFXSpawnerState fourth = native.Tick(0.2f);
        Assert.Equal(new[] { 0.5f, 0.5f, 0.5f, 0.5f },
            new[] { first.spawnCount, second.spawnCount, third.spawnCount, fourth.spawnCount });
        Assert.Equal(new[] { 0f, 1f, 0f, 1f },
            new[] { first.eventSpawnCount, second.eventSpawnCount, third.eventSpawnCount, fourth.eventSpawnCount });
    }

    [Fact]
    public void NativeStop_SuppressesFurtherSpawns()
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(100f) }));
        if (native is null) return;
        native.Play(1);
        native.Stop();
        Assert.Equal(0f, native.Tick(1f).spawnCount);
        Assert.Equal(VFXSpawnerLoopState.Finished, (VFXSpawnerLoopState)native.State.loopState);
    }

    [Fact]
    public void NativeRestart_ClearsFractionalAccumulators()
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(0.75f) }));
        if (native is null) return;
        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState beforeRestart = native.Tick(1f);
        Assert.Equal(0.75f, beforeRestart.spawnCount);
        Assert.Equal(0f, beforeRestart.eventSpawnCount);
        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState afterRestart = native.Tick(0.5f);
        Assert.Equal(0.375f, afterRestart.spawnCount);
        Assert.Equal(0f, afterRestart.eventSpawnCount);
    }

    [Fact]
    public void NativeSingleBurst_FiresOnceAtDelayBoundary()
    {
        using NativeSpawner? native = Native(Program(new[] { Burst(8f, 1f) }));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(0f, native.Tick(0.5f).spawnCount);
        Assert.Equal(8f, native.Tick(0.5f).spawnCount);
        Assert.Equal(0f, native.Tick(10f).spawnCount);
    }

    [Fact]
    public void NativePeriodicBurst_CatchesUpLargeDelta()
    {
        using NativeSpawner? native = Native(Program(new[] { Burst(2f, 0.25f, true) }));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(8f, native.Tick(1f).spawnCount);
    }

    [Fact]
    public void NativeZeroPeriodBurst_IsLimitedToOnePerTick()
    {
        using NativeSpawner? native = Native(Program(new[] { Burst(3f, 0f, true) }));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(3f, native.Tick(1f).spawnCount);
        Assert.Equal(3f, native.Tick(1f).spawnCount);
    }

    [Fact]
    public void NativeVariableRate_IsSeedDeterministic()
    {
        using NativeSpawner? first = Native(Program(new[] { VariableRate(2f, 20f, 0.1f, 0.4f) }));
        using NativeSpawner? second = Native(Program(new[] { VariableRate(2f, 20f, 0.1f, 0.4f) }));
        if (first is null || second is null) return;
        first.Play(77);
        second.Play(77);
        float[] a = Enumerable.Range(0, 20).Select(_ => first.Tick(0.05f).spawnCount).ToArray();
        float[] b = Enumerable.Range(0, 20).Select(_ => second.Tick(0.05f).spawnCount).ToArray();
        Assert.Equal(a, b);
        Assert.True(a.Sum() > 0f);
    }

    [Fact]
    public void NativeRandomLoopOperands_AreBoundedAndSeedDeterministic()
    {
        VFXRuntimeSpawnerProgramData program = Program(
            new[] { ConstantRate(1f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Random,
            loopCountMode: VFXRuntimeSpawnerValueMode.Random,
            delayBeforeMode: VFXRuntimeSpawnerValueMode.Random,
            delayAfterMode: VFXRuntimeSpawnerValueMode.Random) with
        {
            LoopDurationMin = 0.25f,
            LoopDurationMax = 0.75f,
            LoopCountMin = 2.25,
            LoopCountMax = 4.75,
            DelayBeforeLoopMin = 0.1f,
            DelayBeforeLoopMax = 0.2f,
            DelayAfterLoopMin = 0.3f,
            DelayAfterLoopMax = 0.4f
        };
        using NativeSpawner? first = Native(program);
        using NativeSpawner? second = Native(program);
        if (first is null || second is null) return;
        first.Play(991);
        second.Play(991);
        AnityNative.GraphicsVFXSpawnerState a = first.State;
        AnityNative.GraphicsVFXSpawnerState b = second.State;
        Assert.Equal(a.loopDuration, b.loopDuration);
        Assert.Equal(a.loopCount, b.loopCount);
        Assert.Equal(a.delayBeforeLoop, b.delayBeforeLoop);
        Assert.Equal(a.delayAfterLoop, b.delayAfterLoop);
        Assert.InRange(a.loopDuration, 0.25f, 0.75f);
        Assert.InRange(a.loopCount, 2, 4);
        Assert.InRange(a.delayBeforeLoop, 0.1f, 0.2f);
        Assert.InRange(a.delayAfterLoop, 0.3f, 0.4f);
        uint[] random = UnityRandomState(991u);
        float unit = NextUnityRandom(random); // Loop Count is evaluated first.
        Assert.Equal((int)(2.25f + (4.75f - 2.25f) * unit), a.loopCount);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(17u)]
    [InlineData(991u)]
    [InlineData(uint.MaxValue)]
    public void NativeRandomLoopCount_MatchesOfficialLerpThenFloatToIntCast(uint seed)
    {
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f) }) with
        {
            LoopCountMode = VFXRuntimeSpawnerValueMode.Random,
            LoopCountMin = 1.25,
            LoopCountMax = 3.75
        };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        native.Play(seed);
        uint[] random = UnityRandomState(seed);
        float unit = NextUnityRandom(random);
        int expected = (int)(1.25f + (3.75f - 1.25f) * unit);
        Assert.Equal(expected, native.State.loopCount);
    }

    [Theory]
    [InlineData(1u, 3, 0.3185656965f, 0.1476229727f, 0.1200570241f)]
    [InlineData(2u, 2, 0.1499169469f, 0.1689107269f, 0.0981686115f)]
    [InlineData(17u, 2, 0.1269086599f, 0.0725367516f, 0.1452650726f)]
    [InlineData(991u, 3, 0.3333427012f, 0.1506726146f, 0.1201523170f)]
    [InlineData(uint.MaxValue, 1, 0.1434502602f, 0.1149259135f, 0.1786898524f)]
    public void NativeRandomOperands_MatchUnity2022FixedSeedPlayerEvidence(
        uint seed,
        int loopCount,
        float loopDuration,
        float delayBefore,
        float delayAfter)
    {
        VFXRuntimeSpawnerProgramData program = Program(
            new[] { ConstantRate(16f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Random,
            loopCountMode: VFXRuntimeSpawnerValueMode.Random,
            delayBeforeMode: VFXRuntimeSpawnerValueMode.Random,
            delayAfterMode: VFXRuntimeSpawnerValueMode.Random) with
        {
            LoopDurationMin = 0.125f,
            LoopDurationMax = 0.375f,
            LoopCountMin = 1.25,
            LoopCountMax = 3.75,
            DelayBeforeLoopMin = 0.0625f,
            DelayBeforeLoopMax = 0.1875f,
            DelayAfterLoopMin = 0.0625f,
            DelayAfterLoopMax = 0.1875f
        };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        native.Play(seed);
        AnityNative.GraphicsVFXSpawnerState state = native.State;

        Assert.Equal(loopCount, state.loopCount);
        Assert.InRange(Math.Abs(state.loopDuration - loopDuration), 0f, 0.000001f);
        Assert.InRange(Math.Abs(state.delayBeforeLoop - delayBefore), 0f, 0.000001f);
        Assert.InRange(Math.Abs(state.delayAfterLoop - delayAfter), 0f, 0.000001f);
    }

    [Fact]
    public void NativeZeroLoopCount_RunsOneLoopBeforeFinishing()
    {
        using NativeSpawner? native = Native(Program(
            new[] { Burst(10f, 0f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            loopDuration: 0.25f,
            loopCount: 0));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(VFXSpawnerLoopState.Looping, (VFXSpawnerLoopState)native.State.loopState);
        Assert.Equal(0, native.State.loopCount);
        AnityNative.GraphicsVFXSpawnerState state = native.Tick(0.25f);
        Assert.Equal(10f, state.spawnCount);
        Assert.Equal(1, state.loopIndex);
        Assert.Equal(VFXSpawnerLoopState.Finished, (VFXSpawnerLoopState)state.loopState);
        Assert.Equal(0f, state.totalTime);
    }

    [Fact]
    public void NativeConstantLoopCount_PreservesInt32MaximumExactly()
    {
        using NativeSpawner? native = Native(Program(
            new[] { ConstantRate(1f) },
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCount: int.MaxValue));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(int.MaxValue, native.State.loopCount);
    }

    [Fact]
    public void NativeFiniteDurationAndCount_FinishesAtBoundary()
    {
        using NativeSpawner? native = Native(Program(
            new[] { ConstantRate(10f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            loopDuration: 0.5f,
            loopCount: 1));
        if (native is null) return;
        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState state = native.Tick(1f);
        Assert.Equal(10f, state.spawnCount);
        Assert.Equal(VFXSpawnerLoopState.Finished, (VFXSpawnerLoopState)state.loopState);
        Assert.Equal(1, state.loopIndex);
        Assert.Equal(0f, state.totalTime);
    }

    [Theory]
    [MemberData(nameof(UnityConstantFiniteTimeline))]
    public void NativeConstantFiniteTimeline_MatchesUnity2022Player(
        int frame,
        VFXSpawnerLoopState loopState,
        int loopIndex,
        bool newLoop,
        bool playing,
        float totalTime)
    {
        using NativeSpawner? native = Native(Program(
            new[] { ConstantRate(16f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            delayBeforeMode: VFXRuntimeSpawnerValueMode.Constant,
            delayAfterMode: VFXRuntimeSpawnerValueMode.Constant,
            loopDuration: 0.25f,
            loopCount: 2,
            delayBefore: 0.125f,
            delayAfter: 0.125f));
        if (native is null) return;
        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState state = default;
        for (int tick = 0; tick <= frame; tick++) state = native.Tick(0.05f);

        Assert.Equal(loopState, (VFXSpawnerLoopState)state.loopState);
        Assert.Equal(loopIndex, state.loopIndex);
        Assert.Equal(newLoop, state.newLoop != 0);
        Assert.Equal(playing, state.playing != 0);
        Assert.InRange(Math.Abs(state.totalTime - totalTime), 0f, 0.00001f);
        Assert.InRange(Math.Abs(state.deltaTime - 0.05f), 0f, 0.00001f);
        Assert.Equal(2, state.loopCount);
        Assert.Equal(0.25f, state.loopDuration);
        Assert.Equal(0.125f, state.delayBeforeLoop);
        Assert.Equal(0.125f, state.delayAfterLoop);
    }

    [Fact]
    public void NativeDelayBefore_ConsumesOnlyActiveLoopTime()
    {
        using NativeSpawner? native = Native(Program(
            new[] { ConstantRate(8f) },
            delayBeforeMode: VFXRuntimeSpawnerValueMode.Constant,
            delayBefore: 0.5f));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(VFXSpawnerLoopState.DelayingBeforeLoop,
            (VFXSpawnerLoopState)native.Tick(0.25f).loopState);
        AnityNative.GraphicsVFXSpawnerState state = native.Tick(0.5f);
        Assert.Equal(0f, state.spawnCount);
        Assert.Equal(VFXSpawnerLoopState.Looping, (VFXSpawnerLoopState)state.loopState);
        Assert.Equal(0f, state.totalTime);
        Assert.Equal(2f, native.Tick(0.25f).spawnCount);
    }

    [Fact]
    public void NativeDelayAfter_TransitionsToNextLoop()
    {
        using NativeSpawner? native = Native(Program(
            new[] { ConstantRate(4f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            delayAfterMode: VFXRuntimeSpawnerValueMode.Constant,
            loopDuration: 0.25f,
            loopCount: 2,
            delayAfter: 0.5f));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(VFXSpawnerLoopState.DelayingAfterLoop,
            (VFXSpawnerLoopState)native.Tick(0.25f).loopState);
        native.Tick(0.5f);
        Assert.Equal(1, native.State.loopIndex);
        Assert.Equal(VFXSpawnerLoopState.Looping, (VFXSpawnerLoopState)native.State.loopState);
    }

    [Fact]
    public void NativeLargeDelta_DoesNotCarryAcrossPhaseBoundary()
    {
        using NativeSpawner? native = Native(Program(
            new[] { ConstantRate(4f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            delayBeforeMode: VFXRuntimeSpawnerValueMode.Constant,
            delayAfterMode: VFXRuntimeSpawnerValueMode.Constant,
            loopDuration: 0.25f,
            loopCount: 2,
            delayBefore: 0.25f,
            delayAfter: 0.25f));
        if (native is null) return;
        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState state = native.Tick(2f);
        Assert.Equal(0f, state.spawnCount);
        Assert.Equal(0f, state.totalTime);
        Assert.Equal(VFXSpawnerLoopState.Looping, (VFXSpawnerLoopState)state.loopState);
        Assert.Equal(0, state.loopIndex);
    }

    [Fact]
    public void NativeBurst_ResetsForEveryLoop()
    {
        using NativeSpawner? native = Native(Program(
            new[] { Burst(3f, 0f) },
            loopDurationMode: VFXRuntimeSpawnerValueMode.Constant,
            loopCountMode: VFXRuntimeSpawnerValueMode.Constant,
            loopDuration: 0.5f,
            loopCount: 2));
        if (native is null) return;
        native.Play(1);
        Assert.Equal(3f, native.Tick(1f).spawnCount);
        Assert.Equal(3f, native.Tick(1f).spawnCount);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NativeTick_RejectsInvalidDeltaTime(float deltaTime)
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(1f) }));
        if (native is null) return;
        native.Play(1);
        Assert.False(native.Device.TickVFXSpawners(native.EffectId, deltaTime, 1, out _));
    }

    [Fact]
    public void NativeClearEffect_RemovesSpawnerState()
    {
        using NativeSpawner? native = Native(Program(new[] { ConstantRate(1f) }));
        if (native is null) return;
        Assert.True(native.Device.ClearVFXEffectState(native.EffectId));
        Assert.False(native.Device.TryGetVFXSpawnerState(
            native.EffectId, native.Program.ContextId, out _));
    }

    [Fact]
    public void RuntimeAssetV7_RoundTripsSpawnerProgramExactly()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(3f),
            VariableRate(1f, 4f, 0.2f, 0.8f),
            Burst(9f, 0.5f, periodic: true)
        });
        var source = new VFXRuntimeAssetData(
            new[] { new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1) },
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            new[]
            {
                new VFXRuntimeSystemData("Spawn", VFXRuntimeSystemKind.Spawn, 0),
                new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 64)
            },
            Array.Empty<VFXRuntimeOutputEventData>())
        {
            SpawnerPrograms = new[] { program }
        };

        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(source.Serialize());

        VFXRuntimeSpawnerProgramData actual = Assert.Single(restored.SpawnerPrograms);
        Assert.Equal(program.ContextId, actual.ContextId);
        Assert.Equal(program.SystemName, actual.SystemName);
        Assert.Equal(program.Controls, actual.Controls);
        Assert.Equal(program.Blocks, actual.Blocks);
        Assert.Equal(program.Outputs.Single().InitializeContextId, actual.Outputs.Single().InitializeContextId);
    }

    [Fact]
    public void RuntimeAssetV7_RoundTripsFractionalRandomLoopCountEndpoints()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f) }) with
        {
            LoopCountMode = VFXRuntimeSpawnerValueMode.Random,
            LoopCountMin = 1.125,
            LoopCountMax = 9.875
        };

        VFXRuntimeSpawnerProgramData actual = Assert.Single(
            VFXRuntimeAssetData.Deserialize(RuntimeData(program).Serialize()).SpawnerPrograms);

        Assert.Equal(1.125, actual.LoopCountMin);
        Assert.Equal(9.875, actual.LoopCountMax);
    }

    [Fact]
    public void RuntimeAssetV5_MigratesIntegerLoopCountEndpointsToV6Doubles()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f) }) with
        {
            LoopCountMode = VFXRuntimeSpawnerValueMode.Constant,
            LoopCountMin = 7,
            LoopCountMax = 7
        };
        byte[] v5 = ConvertV11SpawnerPayloadToV5(RuntimeData(program).Serialize(), 7, 7);

        VFXRuntimeSpawnerProgramData actual = Assert.Single(
            VFXRuntimeAssetData.Deserialize(v5).SpawnerPrograms);

        Assert.Equal(7d, actual.LoopCountMin);
        Assert.Equal(7d, actual.LoopCountMax);
    }

    [Theory]
    [InlineData((int)VFXRuntimeSpawnerValueMode.Constant, 1.5, 1.5)]
    [InlineData((int)VFXRuntimeSpawnerValueMode.Constant, 1.0, 2.0)]
    [InlineData((int)VFXRuntimeSpawnerValueMode.Random, 0.0, 2147483648.0)]
    [InlineData((int)VFXRuntimeSpawnerValueMode.Infinite, 0.0, 1.0)]
    [InlineData((int)VFXRuntimeSpawnerValueMode.Random, -1.0, 2.0)]
    [InlineData((int)VFXRuntimeSpawnerValueMode.Random, double.NaN, 2.0)]
    public void RuntimeAssetV7_RejectsInvalidLoopCountOperands(
        int mode,
        double minimum,
        double maximum)
    {
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f) }) with
        {
            LoopCountMode = (VFXRuntimeSpawnerValueMode)mode,
            LoopCountMin = minimum,
            LoopCountMax = maximum
        };

        Assert.Throws<InvalidDataException>(() => RuntimeData(program).Serialize());
    }

    [Fact]
    public void NativeProgram_RejectsUnsafeRandomFloatToIntRange()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f) }) with
        {
            LoopCountMode = VFXRuntimeSpawnerValueMode.Random,
            LoopCountMin = 1,
            LoopCountMax = 2147483647
        };

        Assert.False(device.SetVFXSpawnerPrograms(777, new[] { program }));
    }

    [Theory]
    [InlineData((int)VFXRuntimeValueType.Boolean, 1u)]
    [InlineData((int)VFXRuntimeValueType.UInt32, 0xf1234567u)]
    [InlineData((int)VFXRuntimeValueType.Int32, 0xffffffd6u)]
    [InlineData((int)VFXRuntimeValueType.Float, 0xc0600000u)]
    public void NativeSetAttribute_WritesConstantScalarWords(int type, uint expected)
    {
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(2f),
            SetAttribute(1, (VFXRuntimeValueType)type, VFXRuntimeInitializeRandomMode.Off,
                new[] { expected }, new[] { expected })
        }) with { EventStrideWords = 2 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;

        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState state = native.Tick(0.5f);

        Assert.Equal(1f, state.spawnCount);
        Assert.True(native.Device.TryGetVFXSpawnerEventRecord(
            native.EffectId, program.ContextId, 2, out byte[] record));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(4, 4)));
    }

    [Theory]
    [InlineData((int)VFXRuntimeValueType.Float2, 2)]
    [InlineData((int)VFXRuntimeValueType.Float3, 3)]
    [InlineData((int)VFXRuntimeValueType.Float4, 4)]
    public void NativeSetAttribute_WritesEveryFloatingVectorWidth(int type, int wordCount)
    {
        uint[] words = Enumerable.Range(1, wordCount)
            .Select(value => FloatWord(value * -1.25f))
            .ToArray();
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(1f),
            SetAttribute(1, (VFXRuntimeValueType)type, VFXRuntimeInitializeRandomMode.Off,
                words, words)
        }) with { EventStrideWords = 1 + wordCount };
        using NativeSpawner? native = Native(program);
        if (native is null) return;

        native.Play(2);
        native.Tick(1f);

        Assert.True(native.Device.TryGetVFXSpawnerEventRecord(
            native.EffectId, program.ContextId, 1 + wordCount, out byte[] record));
        Assert.Equal(words, Enumerable.Range(0, wordCount)
            .Select(index => BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(4 + index * 4, 4))));
    }

    [Theory]
    [InlineData((int)VFXRuntimeInitializeRandomMode.PerComponent)]
    [InlineData((int)VFXRuntimeInitializeRandomMode.Uniform)]
    public void NativeSetAttribute_MatchesUnityRandomForBothVectorModes(int modeValue)
    {
        var mode = (VFXRuntimeInitializeRandomMode)modeValue;
        float[] minimum = { 1f, 10f, 100f };
        float[] maximum = { 3f, 20f, 200f };
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(1f),
            SetAttribute(1, VFXRuntimeValueType.Float3, mode,
                minimum.Select(FloatWord).ToArray(), maximum.Select(FloatWord).ToArray())
        }) with { EventStrideWords = 4 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        uint[] random = UnityRandomState(17);

        native.Play(17);
        native.Tick(1f);

        float uniform = mode == VFXRuntimeInitializeRandomMode.Uniform
            ? NextUnityRandom(random)
            : 0f;
        float[] expected = Enumerable.Range(0, 3).Select(index =>
        {
            float unit = mode == VFXRuntimeInitializeRandomMode.Uniform
                ? uniform
                : NextUnityRandom(random);
            return minimum[index] + (maximum[index] - minimum[index]) * unit;
        }).ToArray();
        Assert.True(native.Device.TryGetVFXSpawnerEventRecord(
            native.EffectId, program.ContextId, 4, out byte[] record));
        Assert.Equal(expected, Enumerable.Range(0, 3).Select(index =>
            BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(
                record.AsSpan(4 + index * 4, 4)))));
    }

    [Fact]
    public void RuntimeAssetV7_RoundTripsSetAttributeOpcode()
    {
        VFXRuntimeSpawnerBlockData set = SetAttribute(
            1, VFXRuntimeValueType.Float3, VFXRuntimeInitializeRandomMode.PerComponent,
            new[] { FloatWord(-1f), FloatWord(2f), FloatWord(3f) },
            new[] { FloatWord(4f), FloatWord(5f), FloatWord(6f) });
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f), set }) with
        {
            EventStrideWords = 4
        };
        VFXRuntimeAttributeData[] attributes =
        {
            new("spawnCount", VFXRuntimeValueType.Float, 0, 1),
            new("position", VFXRuntimeValueType.Float3, 1, 3)
        };

        VFXRuntimeSpawnerProgramData restored = Assert.Single(
            VFXRuntimeAssetData.Deserialize(RuntimeData(program, attributes).Serialize()).SpawnerPrograms);
        VFXRuntimeSpawnerBlockData block = Assert.Single(restored.Blocks.Where(candidate =>
            candidate.Kind == VFXRuntimeSpawnerBlockKind.SetAttribute));

        Assert.Equal(4, restored.EventStrideWords);
        Assert.Equal(set.TargetOffsetWords, block.TargetOffsetWords);
        Assert.Equal(set.TargetValueType, block.TargetValueType);
        Assert.Equal(set.RandomMode, block.RandomMode);
        Assert.Equal(set.ValueA, block.ValueA);
        Assert.Equal(set.ValueB, block.ValueB);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RuntimeAssetV7_RejectsInvalidSetAttributeContracts(int invalidCase)
    {
        VFXRuntimeSpawnerBlockData set = SetAttribute(
            2, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
            new[] { FloatWord(1f) }, new[] { FloatWord(1f) });
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(1f), set }) with
        {
            EventStrideWords = 2
        };
        if (invalidCase == 0) set = set with { TargetOffsetWords = -1 };
        if (invalidCase == 1) program = program with { EventStrideWords = 3 };
        if (invalidCase == 2) set = set with
        {
            TargetValueType = VFXRuntimeValueType.Boolean,
            RandomMode = VFXRuntimeInitializeRandomMode.Uniform
        };
        if (invalidCase == 3) set = set with
        {
            ValueA = new[] { 0x7fc00000u },
            ValueB = new[] { 0x7fc00000u }
        };
        program = program with { Blocks = new[] { ConstantRate(1f), set } };
        VFXRuntimeAttributeData[] attributes =
        {
            new("spawnCount", VFXRuntimeValueType.Float, 0, 1),
            new("size", VFXRuntimeValueType.Float, 1, 1)
        };

        Assert.Throws<InvalidDataException>(() => RuntimeData(program, attributes).Serialize());
    }

    [Fact]
    public void NativeSetSpawnCount_AfterRateOverwritesTaskChainValue()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(16f),
            SetAttribute(0, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(3f) }, new[] { FloatWord(3f) })
        }) with { EventStrideWords = 1 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;

        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState state = native.Tick(0.05f);

        Assert.Equal(3f, state.spawnCount);
        Assert.Equal(3f, state.eventSpawnCount);
    }

    [Fact]
    public void NativeSetSpawnCount_BeforeRatePreservesOrderedAdditionAndResidue()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            SetAttribute(0, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(3f) }, new[] { FloatWord(3f) }),
            ConstantRate(16f)
        }) with { EventStrideWords = 1 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;

        native.Play(1);
        AnityNative.GraphicsVFXSpawnerState first = native.Tick(0.05f);
        AnityNative.GraphicsVFXSpawnerState second = native.Tick(0.05f);

        Assert.InRange(Math.Abs(first.spawnCount - 3.8f), 0f, 0.000001f);
        Assert.InRange(Math.Abs(first.eventSpawnCount - 3.8f), 0f, 0.000001f);
        Assert.InRange(Math.Abs(second.spawnCount - 3.8f), 0f, 0.000001f);
        Assert.InRange(Math.Abs(second.eventSpawnCount - 4.6f), 0f, 0.000001f);
    }

    [Fact]
    public void NativeFractionalRate_EventRecordContainsAccumulatedDispatchCount()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[] { ConstantRate(16f) }) with
        {
            EventStrideWords = 1
        };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        native.Play(1);

        AnityNative.GraphicsVFXSpawnerState first = native.Tick(0.05f);
        AnityNative.GraphicsVFXSpawnerState second = native.Tick(0.05f);

        Assert.InRange(Math.Abs(first.spawnCount - 0.8f), 0f, 0.000001f);
        Assert.Equal(0f, first.eventSpawnCount);
        Assert.InRange(Math.Abs(second.eventSpawnCount - 1.6f), 0f, 0.000001f);
        Assert.True(native.Device.TryGetVFXSpawnerEventRecord(
            native.EffectId, program.ContextId, 1, out byte[] record));
        Assert.InRange(Math.Abs(ReadFloat(record, 0) - 1.6f), 0f, 0.000001f);
    }

    [Fact]
    public void NativeStop_EvaluatesSetSpawnCountAfterReset()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(16f),
            SetAttribute(0, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(3f) }, new[] { FloatWord(3f) })
        }) with { EventStrideWords = 1 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        native.Play(1);
        native.Tick(0.05f);

        native.Stop();
        AnityNative.GraphicsVFXSpawnerState stopped = native.State;
        AnityNative.GraphicsVFXSpawnerState pending = native.Tick(0.05f);
        AnityNative.GraphicsVFXSpawnerState consumed = native.Tick(0.05f);

        Assert.Equal(VFXSpawnerLoopState.Finished, (VFXSpawnerLoopState)stopped.loopState);
        Assert.Equal(3f, stopped.spawnCount);
        Assert.Equal(3f, stopped.eventSpawnCount);
        Assert.Equal(3f, pending.eventSpawnCount);
        Assert.Equal(0f, consumed.eventSpawnCount);
    }

    [Fact]
    public void NativeSetAttribute_LastSetterWinsForScalarEventField()
    {
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(1f),
            SetAttribute(1, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(-4.5f) }, new[] { FloatWord(-4.5f) }),
            SetAttribute(1, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(7.25f) }, new[] { FloatWord(7.25f) })
        }) with { EventStrideWords = 2 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        native.Play(1);

        native.Tick(1f);

        Assert.True(native.Device.TryGetVFXSpawnerEventRecord(
            native.EffectId, program.ContextId, 2, out byte[] record));
        Assert.Equal(7.25f, ReadFloat(record, 1));
    }

    [Theory]
    [InlineData((int)VFXRuntimeInitializeRandomMode.PerComponent, 3)]
    [InlineData((int)VFXRuntimeInitializeRandomMode.Uniform, 1)]
    public void NativeRandomSetAttribute_EvaluatesOnFramesWithoutDispatch(
        int modeValue,
        int firstExpectedRandomIndex)
    {
        var mode = (VFXRuntimeInitializeRandomMode)modeValue;
        float[] minimum = { -1f, 0.25f, 0.5f };
        float[] maximum = { 2f, 0.75f, 1.5f };
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(16f),
            SetAttribute(1, VFXRuntimeValueType.Float3, mode,
                minimum.Select(FloatWord).ToArray(), maximum.Select(FloatWord).ToArray())
        }) with { EventStrideWords = 4 };
        using NativeSpawner? native = Native(program);
        if (native is null) return;
        native.Play(1);
        uint[] random = UnityRandomState(1);
        float[] units = Enumerable.Range(0, 6).Select(_ => NextUnityRandom(random)).ToArray();

        Assert.Equal(0f, native.Tick(0.05f).eventSpawnCount);
        Assert.InRange(Math.Abs(native.Tick(0.05f).eventSpawnCount - 1.6f), 0f, 0.000001f);

        Assert.True(native.Device.TryGetVFXSpawnerEventRecord(
            native.EffectId, program.ContextId, 4, out byte[] record));
        float uniform = units[firstExpectedRandomIndex];
        for (int component = 0; component < 3; component++)
        {
            float unit = mode == VFXRuntimeInitializeRandomMode.Uniform
                ? uniform
                : units[firstExpectedRandomIndex + component];
            float expected = minimum[component] + (maximum[component] - minimum[component]) * unit;
            Assert.InRange(Math.Abs(ReadFloat(record, 1 + component) - expected), 0f, 0.000001f);
        }
    }

    [Fact]
    public void FrameTick_PublicSpawnerStateKeepsRawFractionalSpawnCount()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = RuntimeEffect(16f);
        effect.Play();
        effect.ProcessInputEvents(device);

        Assert.Equal(0, effect.AdvanceSpawnerSystems(0.05f, device));

        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        Assert.InRange(Math.Abs(state.vfxEventAttribute.GetFloat("spawnCount") - 0.8f), 0f, 0.000001f);
    }

    [Fact]
    public void FrameTick_RoutesSpawnerRecordToMappedOutputEvent()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        const string outputName = "Anity SetAttribute Probe";
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(16f),
            SetAttribute(1, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(7.25f) }, new[] { FloatWord(7.25f) })
        }) with { EventStrideWords = 2 };
        VFXRuntimeAttributeData[] attributes =
        {
            new("spawnCount", VFXRuntimeValueType.Float, 0, 1),
            new("size", VFXRuntimeValueType.Float, 1, 1)
        };
        VFXRuntimeOutputEventData[] outputEvents =
        {
            new(outputName, new long[] { 300 },
                new[] { new VFXRuntimeOutputEventMapping("spawner_input", program.ContextId) },
                new[]
                {
                    new VFXRuntimeAttributeData("size", VFXRuntimeValueType.Float, 0, 1),
                    new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 1, 1)
                }, 2)
        };
        VisualEffect effect = RuntimeEffect(program, attributes, outputEvents);
        VFXOutputEventArgs received = default;
        effect.outputEventReceived += args => received = args;
        effect.Play();
        effect.ProcessInputEvents(device);

        Assert.Equal(0, effect.AdvanceSpawnerSystems(0.05f, device));
        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.05f, device));
        Assert.Equal(1, effect.ProcessOutputEvents(device));

        Assert.Equal(Shader.PropertyToID(outputName), received.nameId);
        Assert.Equal(7.25f, received.eventAttribute.GetFloat("size"));
        Assert.InRange(Math.Abs(received.eventAttribute.GetFloat("spawnCount") - 1.6f), 0f, 0.000001f);
    }

    [Fact]
    public void FrameTick_SetSpawnCountOnlyCanDriveOutputEventWithoutInitialize()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        const string outputName = "Set Only";
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            SetAttribute(0, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(3f) }, new[] { FloatWord(3f) })
        }) with
        {
            EventStrideWords = 1,
            Outputs = Array.Empty<VFXRuntimeSpawnerOutputData>()
        };
        VFXRuntimeAttributeData[] attributes =
        {
            new("spawnCount", VFXRuntimeValueType.Float, 0, 1)
        };
        VFXRuntimeOutputEventData[] outputEvents =
        {
            new(outputName, new long[] { 301 },
                new[] { new VFXRuntimeOutputEventMapping("spawner_input", program.ContextId) },
                new[] { new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1) }, 1)
        };
        VisualEffect effect = RuntimeEffect(program, attributes, outputEvents);
        float received = 0f;
        effect.outputEventReceived += args => received = args.eventAttribute.GetFloat("spawnCount");
        effect.Play();
        effect.ProcessInputEvents(device);

        Assert.Equal(0, effect.AdvanceSpawnerSystems(0.05f, device));
        Assert.Equal(1, effect.ProcessOutputEvents(device));
        Assert.Equal(3f, received);
    }

    [Fact]
    public void FrameTick_SubmitsNativeSetAttributeEventRecord()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VFXRuntimeSpawnerProgramData program = Program(new[]
        {
            ConstantRate(2f),
            SetAttribute(1, VFXRuntimeValueType.Float, VFXRuntimeInitializeRandomMode.Off,
                new[] { FloatWord(-4.5f) }, new[] { FloatWord(-4.5f) })
        }) with { EventStrideWords = 2 };
        VFXRuntimeAttributeData[] attributes =
        {
            new("spawnCount", VFXRuntimeValueType.Float, 0, 1),
            new("size", VFXRuntimeValueType.Float, 1, 1)
        };
        VisualEffect effect = RuntimeEffect(program, attributes);
        effect.Play();
        effect.ProcessInputEvents(device);

        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.5f, device));
        Assert.True(device.TryGetVFXInitializeDispatch(
            EffectId(effect), 200, out NativeVFXInitializeDispatch? dispatch));
        Assert.Equal(1f, BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(
            dispatch!.Records.AsSpan(0, 4))));
        Assert.Equal(-4.5f, BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(
            dispatch.Records.AsSpan(4, 4))));
    }

    [Fact]
    public void InputPlay_StartsInstanceWithoutDirectInitializePassThrough()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = RuntimeEffect(4f);
        effect.Play();

        Assert.Equal(1, effect.ProcessInputEvents(device));

        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        Assert.True(state.playing);
        Assert.False(device.TryGetVFXInitializeDispatch(EffectId(effect), 200, out _));
    }

    [Fact]
    public void FrameTick_SubmitsSpawnerGeneratedSpawnCountRecord()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = RuntimeEffect(4f);
        effect.Play();
        effect.ProcessInputEvents(device);

        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.5f, device));
        Assert.True(device.TryGetVFXInitializeDispatch(
            EffectId(effect), 200, out NativeVFXInitializeDispatch? dispatch));
        float spawnCount = BitConverter.Int32BitsToSingle(unchecked((int)
            BinaryPrimitives.ReadUInt32LittleEndian(dispatch!.Records.AsSpan(0, sizeof(uint)))));
        Assert.Equal(2f, spawnCount);
        Assert.Equal(100, dispatch.Info.desc.sourceSpawnerContextId);
        Assert.Equal(Shader.PropertyToID("Spawn"), dispatch.Info.desc.spawnSystemId);
    }

    [Fact]
    public void InputStop_StopsSubsequentFrameExecution()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect effect = RuntimeEffect(10f);
        effect.Play();
        effect.ProcessInputEvents(device);
        Assert.Equal(1, effect.AdvanceSpawnerSystems(0.1f, device));
        effect.Stop();
        effect.ProcessInputEvents(device);

        Assert.Equal(0, effect.AdvanceSpawnerSystems(1f, device));
        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Spawn");
        Assert.False(state.playing);
    }

    [Fact]
    public void VisualEffects_SharingAssetKeepIndependentSpawnerState()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        VisualEffect first = RuntimeEffect(2f, out VisualEffectAsset asset);
        var second = new VisualEffect { visualEffectAsset = asset };
        first.Play();
        first.ProcessInputEvents(device);
        first.AdvanceSpawnerSystems(0.5f, device);

        using VFXSpawnerState firstState = first.GetSpawnSystemInfo("Spawn");
        using VFXSpawnerState secondState = second.GetSpawnSystemInfo("Spawn");
        Assert.True(firstState.playing);
        Assert.False(secondState.playing);
        Assert.Equal(0.5f, firstState.totalTime);
        Assert.Equal(0f, secondState.totalTime);
    }

    private static NativeSpawner? Native(VFXRuntimeSpawnerProgramData program)
    {
        NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero)
        {
            device.Dispose();
            return null;
        }
        ulong effectId = unchecked((ulong)Interlocked.Increment(ref s_nextNativeEffectId));
        return new NativeSpawner(device, effectId, program);
    }

    private static VFXRuntimeAssetData RuntimeData(
        VFXRuntimeSpawnerProgramData program,
        IReadOnlyList<VFXRuntimeAttributeData>? attributes = null,
        IReadOnlyList<VFXRuntimeOutputEventData>? outputEvents = null)
        => new(
            attributes ?? new[] { new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1) },
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            new[]
            {
                new VFXRuntimeSystemData("Spawn", VFXRuntimeSystemKind.Spawn, 0),
                new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 64)
            },
            outputEvents ?? Array.Empty<VFXRuntimeOutputEventData>())
        {
            SpawnerPrograms = new[] { program }
        };

    private static byte[] ConvertV11SpawnerPayloadToV5(
        byte[] v8,
        int loopCountMin,
        int loopCountMax)
    {
        v8 = ConvertV14SystemLayout(v8);
        Assert.Equal(11u, BinaryPrimitives.ReadUInt32LittleEndian(v8.AsSpan(4, 4)));
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(v8.AsSpan(8, 4));
        byte[] payloadV8 = v8.AsSpan(12, payloadLength).ToArray();
        int loopCountOffset;
        int exposedPropertiesOffset;
        using (var payloadStream = new MemoryStream(payloadV8, writable: false))
        using (var reader = new BinaryReader(payloadStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal("spawnCount", ReadPayloadString(reader));
            reader.ReadByte();
            reader.ReadInt32();
            reader.ReadInt32();
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(2, reader.ReadInt32());
            for (int index = 0; index < 2; index++)
            {
                ReadPayloadString(reader);
                reader.ReadByte();
                reader.ReadUInt32();
            }
            Assert.Equal(0, reader.ReadInt32());
            exposedPropertiesOffset = checked((int)payloadStream.Position);
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal(100L, reader.ReadInt64());
            Assert.Equal("Spawn", ReadPayloadString(reader));
            Assert.Equal((byte)VFXRuntimeSpawnerValueMode.Infinite, reader.ReadByte());
            Assert.Equal((byte)VFXRuntimeSpawnerValueMode.Constant, reader.ReadByte());
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadSingle();
            reader.ReadSingle();
            loopCountOffset = checked((int)payloadStream.Position);
        }
        using (var withoutExposedProperties = new MemoryStream(payloadV8.Length - sizeof(int)))
        {
            withoutExposedProperties.Write(payloadV8, 0, exposedPropertiesOffset);
            withoutExposedProperties.Write(
                payloadV8, exposedPropertiesOffset + sizeof(int),
                payloadV8.Length - exposedPropertiesOffset - sizeof(int));
            payloadV8 = withoutExposedProperties.ToArray();
        }
        loopCountOffset -= sizeof(int);
        int eventStrideOffset = checked(loopCountOffset + 2 * sizeof(double) + 4 * sizeof(float));
        const int emptyV8BlockExtensionBytes = sizeof(int) + sizeof(byte) + sizeof(byte) +
                                                sizeof(int) + sizeof(int) + sizeof(byte) + sizeof(int);
        using var v6PayloadStream = new MemoryStream(payloadV8.Length - sizeof(int) - emptyV8BlockExtensionBytes);
        v6PayloadStream.Write(payloadV8, 0, eventStrideOffset);
        v6PayloadStream.Write(
            payloadV8, eventStrideOffset + sizeof(int),
            payloadV8.Length - eventStrideOffset - sizeof(int) - emptyV8BlockExtensionBytes);
        byte[] payloadV6 = v6PayloadStream.ToArray();
        using var migratedPayloadStream = new MemoryStream(payloadV6.Length - 8);
        migratedPayloadStream.Write(payloadV6, 0, loopCountOffset);
        using (var writer = new BinaryWriter(migratedPayloadStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(loopCountMin);
            writer.Write(loopCountMax);
        }
        migratedPayloadStream.Write(
            payloadV6, loopCountOffset + 2 * sizeof(double),
            payloadV6.Length - loopCountOffset - 2 * sizeof(double));
        byte[] migratedPayload = migratedPayloadStream.ToArray();
        byte[] hash = SHA256.HashData(migratedPayload);
        using var result = new MemoryStream(12 + migratedPayload.Length + hash.Length);
        using (var writer = new BinaryWriter(result, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(BinaryPrimitives.ReadUInt32LittleEndian(v8.AsSpan(0, 4)));
            writer.Write(5u);
            writer.Write(migratedPayload.Length);
            writer.Write(migratedPayload);
            writer.Write(hash);
        }
        return result.ToArray();
    }

    private static byte[] ConvertV14SystemLayout(byte[] source)
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
        byte[] downgradedPayload = downgraded.ToArray();
        byte[] hash = SHA256.HashData(downgradedPayload);
        using var result = new MemoryStream(12 + downgradedPayload.Length + hash.Length);
        using var writer = new BinaryWriter(result, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0, 4)));
        writer.Write(11u);
        writer.Write(downgradedPayload.Length);
        writer.Write(downgradedPayload);
        writer.Write(hash);
        writer.Flush();
        return result.ToArray();
    }

    private static string ReadPayloadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        Assert.InRange(length, 1, 1024 * 1024);
        byte[] value = reader.ReadBytes(length);
        Assert.Equal(length, value.Length);
        return System.Text.Encoding.UTF8.GetString(value);
    }

    private static uint[] UnityRandomState(uint seed)
    {
        var state = new uint[4];
        state[0] = seed;
        for (int index = 1; index < state.Length; index++)
            state[index] = unchecked(1812433253u * state[index - 1] + 1u);
        return state;
    }

    private static float NextUnityRandom(uint[] state)
    {
        uint value = state[0] ^ (state[0] << 11);
        state[0] = state[1];
        state[1] = state[2];
        state[2] = state[3];
        state[3] = state[3] ^ (state[3] >> 19) ^ value ^ (value >> 8);
        return (state[3] & 0x007fffffu) / 8388607f;
    }

    private static VFXRuntimeSpawnerProgramData Program(
        IReadOnlyList<VFXRuntimeSpawnerBlockData> blocks,
        VFXRuntimeSpawnerValueMode loopDurationMode = VFXRuntimeSpawnerValueMode.Infinite,
        VFXRuntimeSpawnerValueMode loopCountMode = VFXRuntimeSpawnerValueMode.Infinite,
        VFXRuntimeSpawnerValueMode delayBeforeMode = VFXRuntimeSpawnerValueMode.Disabled,
        VFXRuntimeSpawnerValueMode delayAfterMode = VFXRuntimeSpawnerValueMode.Disabled,
        float loopDuration = 0f,
        int loopCount = 0,
        float delayBefore = 0f,
        float delayAfter = 0f)
        => new(
            100,
            "Spawn",
            loopDurationMode,
            loopCountMode,
            delayBeforeMode,
            delayAfterMode,
            loopDuration, loopDuration,
            loopCount, loopCount,
            delayBefore, delayBefore,
            delayAfter, delayAfter,
            new[]
            {
                new VFXRuntimeSpawnerControlData(VisualEffectAsset.PlayEventName, 0),
                new VFXRuntimeSpawnerControlData(VisualEffectAsset.StopEventName, 1)
            },
            new[] { new VFXRuntimeSpawnerOutputData(200, "Particles", null) },
            blocks);

    private static VFXRuntimeSpawnerBlockData ConstantRate(float rate)
        => new(1, VFXRuntimeSpawnerBlockKind.ConstantRate, rate, rate, 0f, 0f, false);

    private static VFXRuntimeSpawnerBlockData VariableRate(
        float minRate, float maxRate, float minPeriod, float maxPeriod)
        => new(2, VFXRuntimeSpawnerBlockKind.VariableRate,
            minRate, maxRate, minPeriod, maxPeriod, false);

    private static VFXRuntimeSpawnerBlockData Burst(float count, float delay, bool periodic = false)
        => new(3, VFXRuntimeSpawnerBlockKind.Burst, count, count, delay, delay, periodic);

    private static VFXRuntimeSpawnerBlockData SetAttribute(
        int targetOffsetWords,
        VFXRuntimeValueType valueType,
        VFXRuntimeInitializeRandomMode randomMode,
        IReadOnlyList<uint> valueA,
        IReadOnlyList<uint> valueB)
        => new(4, VFXRuntimeSpawnerBlockKind.SetAttribute, 0f, 0f, 0f, 0f, false)
        {
            TargetOffsetWords = targetOffsetWords,
            TargetValueType = valueType,
            RandomMode = randomMode,
            ValueA = valueA,
            ValueB = valueB
        };

    private static uint FloatWord(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static float ReadFloat(byte[] bytes, int word)
        => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(
            bytes.AsSpan(word * sizeof(uint), sizeof(uint))));

    private static VisualEffect RuntimeEffect(float rate)
        => RuntimeEffect(rate, out _);

    private static VisualEffect RuntimeEffect(float rate, out VisualEffectAsset asset)
    {
        VFXRuntimeInputEventTargetData target = new(
            200, "Particles", new long[] { 100 }, new[] { "Spawn" });
        var data = new VFXRuntimeAssetData(
            new[] { new VFXRuntimeAttributeData("spawnCount", VFXRuntimeValueType.Float, 0, 1) },
            new[] { VisualEffectAsset.PlayEventName, VisualEffectAsset.StopEventName },
            new[]
            {
                new VFXRuntimeInputEventData(VisualEffectAsset.PlayEventName, new[] { target }),
                new VFXRuntimeInputEventData(VisualEffectAsset.StopEventName, new[] { target })
            },
            new[]
            {
                new VFXRuntimeSystemData("Spawn", VFXRuntimeSystemKind.Spawn, 0),
                new VFXRuntimeSystemData("Particles", VFXRuntimeSystemKind.Particle, 128)
            },
            Array.Empty<VFXRuntimeOutputEventData>())
        {
            SpawnerPrograms = new[] { Program(new[] { ConstantRate(rate) }) }
        };
        asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return new VisualEffect { visualEffectAsset = asset };
    }

    private static VisualEffect RuntimeEffect(
        VFXRuntimeSpawnerProgramData program,
        IReadOnlyList<VFXRuntimeAttributeData> attributes,
        IReadOnlyList<VFXRuntimeOutputEventData>? outputEvents = null)
    {
        VFXRuntimeAssetData data = RuntimeData(program, attributes, outputEvents);
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(data.Serialize());
        return new VisualEffect { visualEffectAsset = asset };
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

    private sealed class NativeSpawner : IDisposable
    {
        internal NativeSpawner(
            NativeGraphicsDevice device,
            ulong effectId,
            VFXRuntimeSpawnerProgramData program)
        {
            Device = device;
            EffectId = effectId;
            Program = program;
            Assert.True(device.SetVFXSpawnerPrograms(effectId, new[] { program }));
        }

        internal NativeGraphicsDevice Device { get; }
        internal ulong EffectId { get; }
        internal VFXRuntimeSpawnerProgramData Program { get; }

        internal AnityNative.GraphicsVFXSpawnerState State
        {
            get
            {
                Assert.True(Device.TryGetVFXSpawnerState(EffectId, Program.ContextId, out var state));
                return state;
            }
        }

        internal void Play(uint seed)
            => Assert.True(Device.ControlVFXSpawner(
                EffectId, Program.ContextId, true, seed, true, out _));

        internal void Stop()
            => Assert.True(Device.ControlVFXSpawner(
                EffectId, Program.ContextId, false, 0, false, out _));

        internal AnityNative.GraphicsVFXSpawnerState Tick(float deltaTime)
        {
            Assert.True(Device.TickVFXSpawners(EffectId, deltaTime, 1, out var states));
            return Assert.Single(states);
        }

        public void Dispose() => Device.Dispose();
    }
}
